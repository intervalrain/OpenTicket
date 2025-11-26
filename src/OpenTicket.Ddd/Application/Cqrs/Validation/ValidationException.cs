namespace OpenTicket.Ddd.Application.Cqrs.Validation;

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ValidationException : Exception
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationException(IEnumerable<ValidationError> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors.ToList();
    }

    public ValidationException(params ValidationError[] errors)
        : this((IEnumerable<ValidationError>)errors)
    {
    }
}
