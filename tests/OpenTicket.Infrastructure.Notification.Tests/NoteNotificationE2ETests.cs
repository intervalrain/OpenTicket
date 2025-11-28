using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Application.Contracts.Notes.Events;
using OpenTicket.Application.Contracts.RateLimiting;
using OpenTicket.Application.Notes.Commands;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Application.IntegrationEvents.Idempotency;
using OpenTicket.Ddd.Application.IntegrationEvents.Internal;
using OpenTicket.Ddd.Application.IntegrationEvents.Outbox;
using OpenTicket.Ddd.Infrastructure;
using OpenTicket.Domain.Notes.Entities;
using OpenTicket.Domain.Shared.Identities;
using OpenTicket.Infrastructure.Identity.Mock;
using OpenTicket.Infrastructure.Notification.Abstractions;
using OpenTicket.Infrastructure.Notification.Handlers;
using OpenTicket.Infrastructure.Notification.Internal;
using Shouldly;

namespace OpenTicket.Infrastructure.Notification.Tests;

/// <summary>
/// End-to-end tests that verify the complete flow:
/// CreateNote Command -> Event Published -> Handler Invoked -> Email Sent
/// </summary>
public class NoteNotificationE2ETests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly List<NotificationMessage> _sentNotifications = [];

    public NoteNotificationE2ETests()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging();

        // Add Mock Identity
        services.Configure<MockUserOptions>(opt =>
        {
            opt.UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            opt.Email = "e2e-test@openticket.local";
            opt.Name = "E2E Test User";
            opt.Roles = ["User"];
        });
        services.AddScoped<ICurrentUserProvider, MockCurrentUserProvider>();

        // Add Repository (InMemory)
        services.AddSingleton<IRepository<Note>, InMemoryNoteRepository>();

        // Add Outbox infrastructure
        services.AddSingleton<IOutboxRepository, InMemoryOutboxRepository>();
        services.AddSingleton<IIdempotencyService, InMemoryIdempotencyService>();
        services.Configure<OutboxOptions>(opt =>
        {
            opt.BatchSize = 10;
            opt.MaxRetryAttempts = 3;
        });

        // Add Notification infrastructure with test capture
        services.Configure<NotificationOptions>(opt => opt.Enabled = true);
        services.AddSingleton<INotificationSender>(sp =>
            new TestNotificationSender(_sentNotifications));
        services.AddSingleton<INotificationService, NotificationService>();

        // Add Integration Event Publisher
        services.AddSingleton<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>();

        // Add Rate Limit Service (mock that always allows)
        services.AddSingleton<IRateLimitService, MockRateLimitService>();

        // Add Command Handler
        services.AddScoped<ICommandHandler<CreateNoteCommand, ErrorOr<CreateNoteCommandResult>>, CreateNoteCommandHandler>();

        // Add Event Handlers
        services.AddScoped<IIntegrationEventHandler<NoteCreatedEvent>, NoteCreatedEventHandler>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateNote_ShouldPublishEventAndSendNotification()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateNoteCommand, ErrorOr<CreateNoteCommandResult>>>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var command = new CreateNoteCommand("E2E Test Note", "This note was created in an E2E test.");

        // Act: Execute the command (which should publish the event)
        var result = await handler.HandleAsync(command);

        // Assert: Command succeeded
        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldNotBe(Guid.Empty);

        // Assert: Event was stored in outbox
        var pendingMessages = await outboxRepository.GetPendingAsync(10);
        pendingMessages.ShouldNotBeEmpty();
        pendingMessages.ShouldContain(m => m.EventType == "NoteCreatedEvent");
        pendingMessages.ShouldContain(m => m.AggregateId == result.Value.Id.ToString());

        // Act: Simulate outbox processor - deserialize and handle the event
        var message = pendingMessages.First(m => m.EventType == "NoteCreatedEvent");
        var @event = IntegrationEventSerializer.Deserialize<NoteCreatedEvent>(message.Payload);

        var eventHandler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<NoteCreatedEvent>>();
        await eventHandler.HandleAsync(@event!);

        // Assert: Notification was sent
        _sentNotifications.Count.ShouldBe(1);
        _sentNotifications[0].Recipient.ShouldBe("e2e-test@openticket.local");
        _sentNotifications[0].Subject.ShouldContain("E2E Test Note");
        _sentNotifications[0].Body.ShouldContain("This note was created in an E2E test.");
        _sentNotifications[0].Channel.ShouldBe(NotificationChannel.Email);
    }

    [Fact]
    public async Task CreateNote_WhenNotificationDisabled_ShouldStillPublishEvent()
    {
        // Arrange: Create a new service provider with notifications disabled
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<MockUserOptions>(opt =>
        {
            opt.Email = "disabled@openticket.local";
        });
        services.AddScoped<ICurrentUserProvider, MockCurrentUserProvider>();
        services.AddSingleton<IRepository<Note>, InMemoryNoteRepository>();
        services.AddSingleton<IOutboxRepository, InMemoryOutboxRepository>();
        services.AddSingleton<IIdempotencyService, InMemoryIdempotencyService>();
        services.Configure<OutboxOptions>(opt => { opt.BatchSize = 10; });
        services.Configure<NotificationOptions>(opt => opt.Enabled = false); // Disabled!
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>();
        services.AddSingleton<IRateLimitService, MockRateLimitService>();
        services.AddScoped<ICommandHandler<CreateNoteCommand, ErrorOr<CreateNoteCommandResult>>, CreateNoteCommandHandler>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateNoteCommand, ErrorOr<CreateNoteCommandResult>>>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var command = new CreateNoteCommand("Disabled Test", "Notifications are disabled.");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert: Command succeeded
        result.IsError.ShouldBeFalse();

        // Assert: Event was still published to outbox (decoupled from notification)
        var pendingMessages = await outboxRepository.GetPendingAsync(10);
        pendingMessages.ShouldNotBeEmpty();
        pendingMessages.ShouldContain(m => m.EventType == "NoteCreatedEvent");
    }

    [Fact]
    public async Task CreateMultipleNotes_ShouldSendMultipleNotifications()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateNoteCommand, ErrorOr<CreateNoteCommandResult>>>();
        var eventHandler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<NoteCreatedEvent>>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var commands = new[]
        {
            new CreateNoteCommand("Note 1", "First note body"),
            new CreateNoteCommand("Note 2", "Second note body"),
            new CreateNoteCommand("Note 3", "Third note body")
        };

        // Act: Create all notes
        var results = new List<ErrorOr<CreateNoteCommandResult>>();
        foreach (var command in commands)
        {
            results.Add(await handler.HandleAsync(command));
        }

        // Assert: All commands succeeded
        results.Count.ShouldBe(3);
        results.ShouldAllBe(r => !r.IsError && r.Value.Id != Guid.Empty);

        // Act: Process all events
        var pendingMessages = await outboxRepository.GetPendingAsync(10);
        foreach (var message in pendingMessages.Where(m => m.EventType == "NoteCreatedEvent"))
        {
            var @event = IntegrationEventSerializer.Deserialize<NoteCreatedEvent>(message.Payload);
            await eventHandler.HandleAsync(@event!);
        }

        // Assert: All notifications sent
        _sentNotifications.Count.ShouldBe(3);
        _sentNotifications.ShouldContain(n => n.Subject.Contains("Note 1"));
        _sentNotifications.ShouldContain(n => n.Subject.Contains("Note 2"));
        _sentNotifications.ShouldContain(n => n.Subject.Contains("Note 3"));
    }

    /// <summary>
    /// Test notification sender that captures sent notifications.
    /// </summary>
    private class TestNotificationSender : INotificationSender
    {
        private readonly List<NotificationMessage> _notifications;

        public TestNotificationSender(List<NotificationMessage> notifications)
        {
            _notifications = notifications;
        }

        public NotificationChannel Channel => NotificationChannel.Email;

        public Task<NotificationResult> SendAsync(NotificationMessage notification, CancellationToken ct = default)
        {
            _notifications.Add(notification);
            return Task.FromResult(NotificationResult.Ok(notification.Id, $"test-{Guid.NewGuid():N}"));
        }
    }

    /// <summary>
    /// Simple in-memory repository for E2E tests.
    /// </summary>
    private class InMemoryNoteRepository : IRepository<Note>
    {
        private readonly Dictionary<Guid, Note> _notes = new();

        public Task<Note> GetAsync(Guid id, CancellationToken ct = default)
        {
            if (!_notes.TryGetValue(id, out var note))
                throw new KeyNotFoundException($"Note with id {id} not found");
            return Task.FromResult(note);
        }

        public Task<Note?> FindAsync(Guid id, CancellationToken ct = default)
        {
            _notes.TryGetValue(id, out var note);
            return Task.FromResult(note);
        }

        public Task<IReadOnlyList<Note>> ListAllAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<Note>>(_notes.Values.ToList());
        }

        public Task InsertAsync(Note aggregate, CancellationToken ct = default)
        {
            _notes[aggregate.Id] = aggregate;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Note aggregate, CancellationToken ct = default)
        {
            _notes[aggregate.Id] = aggregate;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Note aggregate, CancellationToken ct = default)
        {
            _notes.Remove(aggregate.Id);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Mock rate limit service that always allows actions (for E2E tests).
    /// </summary>
    private class MockRateLimitService : IRateLimitService
    {
        public Task<ErrorOr<Success>> CheckRateLimitAsync(UserId userId, RateLimitedAction action, CancellationToken ct = default)
        {
            return Task.FromResult<ErrorOr<Success>>(Result.Success);
        }

        public Task RecordActionAsync(UserId userId, RateLimitedAction action, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task<int> GetRemainingQuotaAsync(UserId userId, RateLimitedAction action, CancellationToken ct = default)
        {
            return Task.FromResult(int.MaxValue);
        }
    }
}
