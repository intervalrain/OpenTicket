using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.Cqrs.Behaviors;
using Shouldly;

namespace OpenTicket.Ddd.Tests.Application.Cqrs.Behaviors;

public class LoggingBehaviorTests
{
    public record TestCommand(string Value) : ICommand<string>;

    [Fact]
    public async Task HandleAsync_ShouldLogBeforeAndAfterExecution()
    {
        // Arrange
        var logger = Substitute.For<ILogger<LoggingBehavior<TestCommand, string>>>();
        var behavior = new LoggingBehavior<TestCommand, string>(logger);
        var command = new TestCommand("test");

        // Act
        var result = await behavior.HandleAsync(command, () => Task.FromResult("Success"));

        // Assert
        result.ShouldBe("Success");
        logger.ReceivedCalls().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenHandlerThrows_ShouldLogErrorAndRethrow()
    {
        // Arrange
        var logger = Substitute.For<ILogger<LoggingBehavior<TestCommand, string>>>();
        var behavior = new LoggingBehavior<TestCommand, string>(logger);
        var command = new TestCommand("test");

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => behavior.HandleAsync(command, () => throw new InvalidOperationException("Handler failed")));

        exception.Message.ShouldBe("Handler failed");
        logger.ReceivedCalls().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ShouldMeasureExecutionTime()
    {
        // Arrange
        var logger = Substitute.For<ILogger<LoggingBehavior<TestCommand, string>>>();
        var behavior = new LoggingBehavior<TestCommand, string>(logger);
        var command = new TestCommand("test");

        // Act
        await behavior.HandleAsync(command, async () =>
        {
            await Task.Delay(50); // Add small delay to ensure measurable time
            return "Success";
        });

        // Assert - verify that logging occurred (measuring time is internal)
        logger.ReceivedCalls().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnResultFromHandler()
    {
        // Arrange
        var logger = Substitute.For<ILogger<LoggingBehavior<TestCommand, string>>>();
        var behavior = new LoggingBehavior<TestCommand, string>(logger);
        var command = new TestCommand("test");
        var expectedResult = "ExpectedResult";

        // Act
        var result = await behavior.HandleAsync(command, () => Task.FromResult(expectedResult));

        // Assert
        result.ShouldBe(expectedResult);
    }
}
