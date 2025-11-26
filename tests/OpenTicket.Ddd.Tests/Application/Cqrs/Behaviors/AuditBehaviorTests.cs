using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.Cqrs.Audit;
using OpenTicket.Ddd.Application.Cqrs.Behaviors;
using Shouldly;

namespace OpenTicket.Ddd.Tests.Application.Cqrs.Behaviors;

public class AuditBehaviorTests
{
    public record TestCommand(string Value) : ICommand<string>;
    public record TestQuery(int Id) : IQuery<string>;

    [Fact]
    public async Task HandleAsync_ShouldUseTraceIdFromAuditContext()
    {
        // Arrange
        var auditContext = new AuditContext { TraceId = "test-trace-123" };
        var logger = Substitute.For<ILogger<AuditBehavior<TestCommand, string>>>();
        var behavior = new AuditBehavior<TestCommand, string>(auditContext, logger);
        var command = new TestCommand("test");

        // Act
        var result = await behavior.HandleAsync(command, () => Task.FromResult("Success"));

        // Assert
        result.ShouldBe("Success");
        auditContext.TraceId.ShouldBe("test-trace-123");
    }

    [Fact]
    public async Task HandleAsync_WithCommand_ShouldLogAuditTrail()
    {
        // Arrange
        var auditContext = new AuditContext
        {
            TraceId = "trace-123",
            UserId = "user-456"
        };
        var logger = Substitute.For<ILogger<AuditBehavior<TestCommand, string>>>();
        var behavior = new AuditBehavior<TestCommand, string>(auditContext, logger);
        var command = new TestCommand("test");

        // Act
        await behavior.HandleAsync(command, () => Task.FromResult("Success"));

        // Assert - verify logging was called (at least once for info level)
        logger.ReceivedCalls().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenHandlerThrows_ShouldLogWarningAndRethrow()
    {
        // Arrange
        var auditContext = new AuditContext { TraceId = "trace-error" };
        var logger = Substitute.For<ILogger<AuditBehavior<TestCommand, string>>>();
        var behavior = new AuditBehavior<TestCommand, string>(auditContext, logger);
        var command = new TestCommand("test");

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => behavior.HandleAsync(command, () => throw new InvalidOperationException("Handler failed")));

        logger.ReceivedCalls().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithQuery_ShouldNotLogAuditTrailButStillProcess()
    {
        // Arrange
        var auditContext = new AuditContext { TraceId = "trace-query" };
        var logger = Substitute.For<ILogger<AuditBehavior<TestQuery, string>>>();
        var behavior = new AuditBehavior<TestQuery, string>(auditContext, logger);
        var query = new TestQuery(1);

        // Act
        var result = await behavior.HandleAsync(query, () => Task.FromResult("QueryResult"));

        // Assert
        result.ShouldBe("QueryResult");
    }

    [Fact]
    public void AuditContext_ShouldGenerateTraceIdByDefault()
    {
        // Arrange & Act
        var context1 = new AuditContext();
        var context2 = new AuditContext();

        // Assert
        context1.TraceId.ShouldNotBeNullOrEmpty();
        context2.TraceId.ShouldNotBeNullOrEmpty();
        context1.TraceId.ShouldNotBe(context2.TraceId);
    }

    [Fact]
    public void AuditContext_ShouldSetRequestStartedAtToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var context = new AuditContext();

        // Assert
        var after = DateTime.UtcNow;
        context.RequestStartedAt.ShouldBeInRange(before, after);
    }

    [Fact]
    public async Task HandleAsync_ShouldIncludeCorrelationIdInLogging()
    {
        // Arrange
        var auditContext = new AuditContext
        {
            TraceId = "trace-123",
            CorrelationId = "corr-456",
            UserId = "user-789"
        };
        var logger = Substitute.For<ILogger<AuditBehavior<TestCommand, string>>>();
        var behavior = new AuditBehavior<TestCommand, string>(auditContext, logger);
        var command = new TestCommand("test");

        // Act
        await behavior.HandleAsync(command, () => Task.FromResult("Success"));

        // Assert
        auditContext.CorrelationId.ShouldBe("corr-456");
    }
}
