using OpenTicket.Ddd.Application.Cqrs.Validation;

namespace OpenTicket.Ddd.Application.Cqrs.Behaviors;

/// <summary>
/// Pipeline behavior that validates requests before processing.
/// Collects all validation errors from registered validators and throws ValidationException if any fail.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResult">The type of result.</typeparam>
public sealed class ValidationBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken ct = default)
    {
        var validators = _validators.ToList();

        if (validators.Count == 0)
            return await next();

        var validationTasks = validators.Select(v => v.ValidateAsync(request, ct));
        var validationResults = await Task.WhenAll(validationTasks);

        var errors = validationResults
            .SelectMany(r => r.Errors)
            .ToList();

        if (errors.Count > 0)
            throw new ValidationException(errors);

        return await next();
    }
}
