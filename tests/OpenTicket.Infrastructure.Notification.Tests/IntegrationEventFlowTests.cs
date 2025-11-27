using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTicket.Application.Contracts.Notes.Events;
using OpenTicket.Ddd.Application.IntegrationEvents;
using OpenTicket.Ddd.Application.IntegrationEvents.Idempotency;
using OpenTicket.Ddd.Application.IntegrationEvents.Internal;
using OpenTicket.Ddd.Application.IntegrationEvents.Outbox;
using OpenTicket.Infrastructure.Notification.Abstractions;
using OpenTicket.Infrastructure.Notification.Handlers;
using OpenTicket.Infrastructure.Notification.Internal;
using Shouldly;

namespace OpenTicket.Infrastructure.Notification.Tests;

/// <summary>
/// Integration tests that verify the complete flow:
/// Event -> Outbox -> Publisher -> Handler -> Notification
/// </summary>
public class IntegrationEventFlowTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly List<NotificationMessage> _sentNotifications = [];

    public IntegrationEventFlowTests()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging();

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

        // Add Event Handlers
        services.AddScoped<IIntegrationEventHandler<NoteCreatedEvent>, NoteCreatedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<NoteUpdatedEvent>, NoteUpdatedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<NotePatchedEvent>, NotePatchedEventHandler>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CompleteFlow_PublishEvent_ProcessOutbox_HandleEvent_SendNotification()
    {
        // Arrange
        var publisher = _serviceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var outboxRepository = _serviceProvider.GetRequiredService<IOutboxRepository>();

        var @event = new NoteCreatedEvent
        {
            NoteId = Guid.NewGuid(),
            Title = "Integration Test Note",
            Body = "This is testing the complete flow",
            CreatedAt = DateTime.UtcNow,
            NotifyEmail = "flow-test@example.com",
            CorrelationId = "flow-test-123"
        };

        // Act 1: Publish event (stores in outbox)
        await publisher.PublishAsync(@event);

        // Assert 1: Event is in outbox
        var pendingMessages = await outboxRepository.GetPendingAsync(10);
        pendingMessages.Count.ShouldBe(1);
        pendingMessages[0].EventType.ShouldBe("NoteCreatedEvent");
        pendingMessages[0].AggregateId.ShouldBe(@event.NoteId.ToString());

        // Act 2: Process outbox and handle event
        var handler = _serviceProvider.GetRequiredService<IIntegrationEventHandler<NoteCreatedEvent>>();
        await handler.HandleAsync(@event);

        // Assert 2: Notification was sent
        _sentNotifications.Count.ShouldBe(1);
        _sentNotifications[0].Recipient.ShouldBe("flow-test@example.com");
        _sentNotifications[0].Subject.ShouldContain("Integration Test Note");
        _sentNotifications[0].CorrelationId.ShouldBe("flow-test-123");
    }

    [Fact]
    public async Task Idempotency_SameEventProcessedTwice_ShouldOnlySendOnce()
    {
        // Arrange
        var idempotencyService = _serviceProvider.GetRequiredService<IIdempotencyService>();
        var handler = _serviceProvider.GetRequiredService<IIntegrationEventHandler<NoteCreatedEvent>>();

        var @event = new NoteCreatedEvent
        {
            NoteId = Guid.NewGuid(),
            Title = "Idempotency Test",
            Body = "Testing idempotency",
            CreatedAt = DateTime.UtcNow,
            NotifyEmail = "idempotency@example.com"
        };

        const string consumerGroup = "notification-handlers";

        // First check - not processed yet
        var alreadyProcessed = await idempotencyService.HasBeenProcessedAsync(@event.EventId, consumerGroup);
        alreadyProcessed.ShouldBeFalse();

        // Act 1: First processing
        await handler.HandleAsync(@event);
        await idempotencyService.MarkAsProcessedAsync(@event.EventId, @event.EventType, consumerGroup);

        // Assert 1: One notification sent
        var firstCount = _sentNotifications.Count;
        firstCount.ShouldBe(1);

        // Check - now marked as processed
        alreadyProcessed = await idempotencyService.HasBeenProcessedAsync(@event.EventId, consumerGroup);
        alreadyProcessed.ShouldBeTrue();

        // Act 2: Simulate duplicate delivery - check idempotency first
        if (!await idempotencyService.HasBeenProcessedAsync(@event.EventId, consumerGroup))
        {
            await handler.HandleAsync(@event);
        }

        // Assert 2: Still only one notification (idempotency prevented duplicate)
        _sentNotifications.Count.ShouldBe(firstCount);
    }

    [Fact]
    public async Task OutboxPattern_MultipleEvents_ShouldProcessAllInOrder()
    {
        // Arrange
        var publisher = _serviceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var outboxRepository = _serviceProvider.GetRequiredService<IOutboxRepository>();

        var events = Enumerable.Range(1, 5)
            .Select(i => new NoteCreatedEvent
            {
                NoteId = Guid.NewGuid(),
                Title = $"Note {i}",
                Body = $"Body {i}",
                CreatedAt = DateTime.UtcNow.AddSeconds(i),
                NotifyEmail = $"user{i}@example.com"
            })
            .ToList();

        // Act: Publish all events
        foreach (var @event in events)
        {
            await publisher.PublishAsync(@event);
        }

        // Assert: All events in outbox
        var pending = await outboxRepository.GetPendingAsync(10);
        pending.Count.ShouldBe(5);

        // Process each event
        var handler = _serviceProvider.GetRequiredService<IIntegrationEventHandler<NoteCreatedEvent>>();
        foreach (var @event in events)
        {
            await handler.HandleAsync(@event);
        }

        // Assert: All notifications sent
        _sentNotifications.Count.ShouldBe(5);
        for (int i = 0; i < 5; i++)
        {
            _sentNotifications[i].Recipient.ShouldBe($"user{i + 1}@example.com");
        }
    }

    [Fact]
    public async Task EventSerialization_ShouldPreserveAllData()
    {
        // Arrange
        var publisher = _serviceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var outboxRepository = _serviceProvider.GetRequiredService<IOutboxRepository>();

        var @event = new NoteUpdatedEvent
        {
            NoteId = Guid.NewGuid(),
            PreviousTitle = "Old Title",
            NewTitle = "New Title",
            PreviousBody = "Old Body",
            NewBody = "New Body",
            UpdatedAt = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            NotifyEmail = "serialize@example.com",
            CorrelationId = "serialize-test"
        };

        // Act: Publish and retrieve
        await publisher.PublishAsync(@event);
        var pending = await outboxRepository.GetPendingAsync(10);

        // Assert: Payload contains all data
        var payload = pending[0].Payload;
        payload.ShouldContain("Old Title");
        payload.ShouldContain("New Title");
        payload.ShouldContain("Old Body");
        payload.ShouldContain("New Body");
        payload.ShouldContain("serialize-test");
    }

    /// <summary>
    /// Test notification sender that captures sent notifications
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
}
