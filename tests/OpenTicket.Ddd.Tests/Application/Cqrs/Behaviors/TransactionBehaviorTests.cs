using NSubstitute;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.Cqrs.Behaviors;
using OpenTicket.Ddd.Infrastructure;
using Shouldly;

namespace OpenTicket.Ddd.Tests.Application.Cqrs.Behaviors;

public class TransactionBehaviorTests
{
    public record TestCommand(string Value) : ICommand<string>;
    public record TestQuery(int Id) : IQuery<string>;

    [Fact]
    public async Task HandleAsync_WithCommand_ShouldCallSaveChanges()
    {
        // Arrange
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var behavior = new TransactionBehavior<TestCommand, string>(unitOfWork);
        var command = new TestCommand("test");

        // Act
        var result = await behavior.HandleAsync(command, () => Task.FromResult("Success"));

        // Assert
        result.ShouldBe("Success");
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithQuery_ShouldNotCallSaveChanges()
    {
        // Arrange
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var behavior = new TransactionBehavior<TestQuery, string>(unitOfWork);
        var query = new TestQuery(1);

        // Act
        var result = await behavior.HandleAsync(query, () => Task.FromResult("QueryResult"));

        // Assert
        result.ShouldBe("QueryResult");
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenHandlerThrows_ShouldNotCallSaveChanges()
    {
        // Arrange
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var behavior = new TransactionBehavior<TestCommand, string>(unitOfWork);
        var command = new TestCommand("test");

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => behavior.HandleAsync(command, () => throw new InvalidOperationException("Handler failed")));

        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithCommand_ShouldExecuteHandlerBeforeSaveChanges()
    {
        // Arrange
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var behavior = new TransactionBehavior<TestCommand, string>(unitOfWork);
        var command = new TestCommand("test");
        var executionOrder = new List<string>();

        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                executionOrder.Add("SaveChanges");
                return Task.FromResult(1);
            });

        // Act
        await behavior.HandleAsync(command, () =>
        {
            executionOrder.Add("Handler");
            return Task.FromResult("Success");
        });

        // Assert
        executionOrder.ShouldBe(new[] { "Handler", "SaveChanges" });
    }
}
