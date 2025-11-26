using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Ddd.Application.Cqrs.Validation;

namespace OpenTicket.Application.Notes.Validators;

public sealed class PatchNoteCommandValidator : IValidator<PatchNoteCommand>
{
    private const int MaxTitleLength = 200;
    private const int MaxBodyLength = 10000;

    public Task<ValidationResult> ValidateAsync(PatchNoteCommand instance, CancellationToken ct = default)
    {
        var errors = new List<ValidationError>();

        if (instance.Id == Guid.Empty)
            errors.Add(new ValidationError(nameof(instance.Id), "Id is required"));

        // Only validate if values are provided (null means no change)
        if (instance.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(instance.Title))
                errors.Add(new ValidationError(nameof(instance.Title), "Title cannot be empty when provided"));
            else if (instance.Title.Length > MaxTitleLength)
                errors.Add(new ValidationError(nameof(instance.Title), $"Title must be {MaxTitleLength} characters or less"));
        }

        if (instance.Body?.Length > MaxBodyLength)
            errors.Add(new ValidationError(nameof(instance.Body), $"Body must be {MaxBodyLength} characters or less"));

        return Task.FromResult(errors.Count > 0
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success());
    }
}
