namespace OpenTicket.Ddd.Application.Cqrs.Validation;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public record ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];

    public static ValidationResult Success() => new();
    public static ValidationResult Failure(params ValidationError[] errors) => new() { Errors = errors };
    public static ValidationResult Failure(IEnumerable<ValidationError> errors) => new() { Errors = errors.ToList() };
}

/// <summary>
/// Represents a validation error.
/// </summary>
public record ValidationError(string PropertyName, string ErrorMessage);

/// <summary>
/// Validator interface for validating requests.
/// </summary>
/// <typeparam name="T">The type of object to validate.</typeparam>
public interface IValidator<in T>
{
    /// <summary>
    /// Validates the specified instance.
    /// </summary>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<ValidationResult> ValidateAsync(T instance, CancellationToken ct = default);
}
