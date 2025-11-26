using Microsoft.Extensions.DependencyInjection;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.Cqrs.Internal;
using Shouldly;

namespace OpenTicket.Ddd.Tests.Application.Cqrs;

public class DispatcherTests
{
    #region Test Commands and Queries

    public record TestCommand(string Value) : ICommand<string>;

    public class TestCommandHandler : ICommandHandler<TestCommand, string>
    {
        public Task<string> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            return Task.FromResult($"Handled: {command.Value}");
        }
    }

    public record TestQuery(int Id) : IQuery<TestQueryResult>;
    public record TestQueryResult(int Id, string Name);

    public class TestQueryHandler : IQueryHandler<TestQuery, TestQueryResult>
    {
        public Task<TestQueryResult> HandleAsync(TestQuery query, CancellationToken ct = default)
        {
            return Task.FromResult(new TestQueryResult(query.Id, $"Item-{query.Id}"));
        }
    }

    public record VoidCommand(string Action) : ICommand;

    public class VoidCommandHandler : ICommandHandler<VoidCommand, Unit>
    {
        public static string? LastAction { get; private set; }

        public Task<Unit> HandleAsync(VoidCommand command, CancellationToken ct = default)
        {
            LastAction = command.Action;
            return Task.FromResult(Unit.Value);
        }
    }

    #endregion

    #region Test Pipeline Behaviors

    public class TestPipelineBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
        where TRequest : notnull
    {
        public static int ExecutionCount { get; private set; }
        public static void Reset() => ExecutionCount = 0;

        public async Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct = default)
        {
            ExecutionCount++;
            return await next();
        }
    }

    public class FirstBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
        where TRequest : notnull
    {
        public static List<string> ExecutionOrder { get; } = new();
        public static void Reset() => ExecutionOrder.Clear();

        public async Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct = default)
        {
            ExecutionOrder.Add("First-Before");
            var result = await next();
            ExecutionOrder.Add("First-After");
            return result;
        }
    }

    public class SecondBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
        where TRequest : notnull
    {
        public async Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct = default)
        {
            FirstBehavior<TRequest, TResult>.ExecutionOrder.Add("Second-Before");
            var result = await next();
            FirstBehavior<TRequest, TResult>.ExecutionOrder.Add("Second-After");
            return result;
        }
    }

    #endregion

    private static IServiceProvider BuildServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddCqrs();
        services.AddScoped<ICommandHandler<TestCommand, string>, TestCommandHandler>();
        services.AddScoped<IQueryHandler<TestQuery, TestQueryResult>, TestQueryHandler>();
        services.AddScoped<ICommandHandler<VoidCommand, Unit>, VoidCommandHandler>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SendAsync_WithValidCommand_ShouldReturnResult()
    {
        // Arrange
        var sp = BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var command = new TestCommand("Hello");

        // Act
        var result = await dispatcher.SendAsync(command);

        // Assert
        result.ShouldBe("Handled: Hello");
    }

    [Fact]
    public async Task QueryAsync_WithValidQuery_ShouldReturnResult()
    {
        // Arrange
        var sp = BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var query = new TestQuery(42);

        // Act
        var result = await dispatcher.QueryAsync(query);

        // Assert
        result.Id.ShouldBe(42);
        result.Name.ShouldBe("Item-42");
    }

    [Fact]
    public async Task SendAsync_WithVoidCommand_ShouldExecuteHandler()
    {
        // Arrange
        var sp = BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        var command = new VoidCommand("TestAction");

        // Act
        var result = await dispatcher.SendAsync(command);

        // Assert
        result.ShouldBe(Unit.Value);
        VoidCommandHandler.LastAction.ShouldBe("TestAction");
    }

    [Fact]
    public async Task SendAsync_WithPipelineBehavior_ShouldExecuteBehavior()
    {
        // Arrange
        TestPipelineBehavior<TestCommand, string>.Reset();
        var sp = BuildServiceProvider(services =>
        {
            services.AddScoped(
                typeof(IPipelineBehavior<TestCommand, string>),
                typeof(TestPipelineBehavior<TestCommand, string>));
        });
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // Act
        await dispatcher.SendAsync(new TestCommand("Test"));

        // Assert
        TestPipelineBehavior<TestCommand, string>.ExecutionCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_WithMultipleBehaviors_ShouldExecuteInOrder()
    {
        // Arrange
        FirstBehavior<TestCommand, string>.Reset();
        var sp = BuildServiceProvider(services =>
        {
            services.AddScoped(
                typeof(IPipelineBehavior<TestCommand, string>),
                typeof(FirstBehavior<TestCommand, string>));
            services.AddScoped(
                typeof(IPipelineBehavior<TestCommand, string>),
                typeof(SecondBehavior<TestCommand, string>));
        });
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // Act
        await dispatcher.SendAsync(new TestCommand("Test"));

        // Assert
        FirstBehavior<TestCommand, string>.ExecutionOrder.ShouldBe(new[]
        {
            "First-Before",
            "Second-Before",
            "Second-After",
            "First-After"
        });
    }

    [Fact]
    public async Task SendAsync_WithNoHandler_ShouldThrowException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCqrs();
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new TestCommand("Test")));
    }

    [Fact]
    public void Unit_ShouldBeEqual()
    {
        // Arrange & Act
        var unit1 = Unit.Value;
        var unit2 = new Unit();

        // Assert
        unit1.ShouldBe(unit2);
        (unit1 == unit2).ShouldBeTrue();
        unit1.GetHashCode().ShouldBe(0);
        unit1.ToString().ShouldBe("()");
    }
}
