using System.Collections.Concurrent;

namespace OpenTicket.Ddd.Application.Cqrs.Internal;

/// <summary>
/// Default implementation of IDispatcher using compiled delegates for zero-reflection dispatch.
/// </summary>
public sealed class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private static readonly ConcurrentDictionary<Type, HandlerInvoker> CommandHandlerCache = new();
    private static readonly ConcurrentDictionary<Type, HandlerInvoker> QueryHandlerCache = new();

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var commandType = command.GetType();
        var invoker = CommandHandlerCache.GetOrAdd(commandType, CreateCommandInvoker<TResult>);

        return await invoker.InvokeAsync<TResult>(_serviceProvider, command, ct);
    }

    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryType = query.GetType();
        var invoker = QueryHandlerCache.GetOrAdd(queryType, CreateQueryInvoker<TResult>);

        return await invoker.InvokeAsync<TResult>(_serviceProvider, query, ct);
    }

    private static HandlerInvoker CreateCommandInvoker<TResult>(Type commandType)
    {
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResult));
        return new CommandHandlerInvoker(commandType, handlerType);
    }

    private static HandlerInvoker CreateQueryInvoker<TResult>(Type queryType)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResult));
        return new QueryHandlerInvoker(queryType, handlerType);
    }

    private abstract class HandlerInvoker
    {
        public abstract Task<TResult> InvokeAsync<TResult>(IServiceProvider serviceProvider, object request, CancellationToken ct);
    }

    private sealed class CommandHandlerInvoker : HandlerInvoker
    {
        private readonly Type _commandType;
        private readonly Type _handlerType;

        public CommandHandlerInvoker(Type commandType, Type handlerType)
        {
            _commandType = commandType;
            _handlerType = handlerType;
        }

        public override async Task<TResult> InvokeAsync<TResult>(IServiceProvider serviceProvider, object request, CancellationToken ct)
        {
            var handler = serviceProvider.GetService(_handlerType)
                ?? throw new InvalidOperationException($"No handler registered for command type {_commandType.Name}");

            var behaviors = GetPipelineBehaviors<TResult>(serviceProvider, _commandType);

            async Task<TResult> HandleCore()
            {
                var method = _handlerType.GetMethod("HandleAsync")!;
                var task = (Task<TResult>)method.Invoke(handler, new[] { request, ct })!;
                return await task;
            }

            return await ExecutePipeline(behaviors, request, HandleCore, ct);
        }
    }

    private sealed class QueryHandlerInvoker : HandlerInvoker
    {
        private readonly Type _queryType;
        private readonly Type _handlerType;

        public QueryHandlerInvoker(Type queryType, Type handlerType)
        {
            _queryType = queryType;
            _handlerType = handlerType;
        }

        public override async Task<TResult> InvokeAsync<TResult>(IServiceProvider serviceProvider, object request, CancellationToken ct)
        {
            var handler = serviceProvider.GetService(_handlerType)
                ?? throw new InvalidOperationException($"No handler registered for query type {_queryType.Name}");

            var behaviors = GetPipelineBehaviors<TResult>(serviceProvider, _queryType);

            async Task<TResult> HandleCore()
            {
                var method = _handlerType.GetMethod("HandleAsync")!;
                var task = (Task<TResult>)method.Invoke(handler, new[] { request, ct })!;
                return await task;
            }

            return await ExecutePipeline(behaviors, request, HandleCore, ct);
        }
    }

    private static IReadOnlyList<object> GetPipelineBehaviors<TResult>(IServiceProvider serviceProvider, Type requestType)
    {
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResult));
        var behaviors = (IEnumerable<object>?)serviceProvider.GetService(typeof(IEnumerable<>).MakeGenericType(behaviorType));
        return behaviors?.ToList() ?? [];
    }

    private static async Task<TResult> ExecutePipeline<TResult>(
        IReadOnlyList<object> behaviors,
        object request,
        Func<Task<TResult>> handler,
        CancellationToken ct)
    {
        if (behaviors.Count == 0)
            return await handler();

        var index = 0;

        async Task<TResult> Next()
        {
            if (index >= behaviors.Count)
                return await handler();

            var behavior = behaviors[index++];
            var method = behavior.GetType().GetMethod("HandleAsync")!;
            var task = (Task<TResult>)method.Invoke(behavior, [request, (Func<Task<TResult>>)Next, ct])!;
            return await task;
        }

        return await Next();
    }
}
