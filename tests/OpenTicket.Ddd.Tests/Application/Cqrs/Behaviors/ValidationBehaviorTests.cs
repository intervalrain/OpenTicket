using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.Cqrs.Behaviors;
using OpenTicket.Ddd.Application.Cqrs.Validation;
using Shouldly;

namespace OpenTicket.Ddd.Tests.Application.Cqrs.Behaviors;

public class ValidationBehaviorTests
{
    public record TestCommand(string Name, int Age) : ICommand<string>;

    public class TestCommandValidator : IValidator<TestCommand>
    {
        public Task<ValidationResult> ValidateAsync(TestCommand instance, CancellationToken ct = default)
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(instance.Name))
                errors.Add(new ValidationError(nameof(TestCommand.Name), "Name is required"));

            if (instance.Age < 0)
                errors.Add(new ValidationError(nameof(TestCommand.Age), "Age must be non-negative"));

            return Task.FromResult(errors.Count > 0
                ? ValidationResult.Failure(errors)
                : ValidationResult.Success());
        }
    }

    public class AnotherValidator : IValidator<TestCommand>
    {
        public Task<ValidationResult> ValidateAsync(TestCommand instance, CancellationToken ct = default)
        {
            if (instance.Name?.Length > 50)
                return Task.FromResult(ValidationResult.Failure(
                    new ValidationError(nameof(TestCommand.Name), "Name must be 50 characters or less")));

            return Task.FromResult(ValidationResult.Success());
        }
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldProceedToNext()
    {
        // Arrange
        var validators = new List<IValidator<TestCommand>> { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        var command = new TestCommand("John", 25);
        var nextCalled = false;

        // Act
        var result = await behavior.HandleAsync(command, () =>
        {
            nextCalled = true;
            return Task.FromResult("Success");
        });

        // Assert
        nextCalled.ShouldBeTrue();
        result.ShouldBe("Success");
    }

    [Fact]
    public async Task HandleAsync_WithInvalidRequest_ShouldThrowValidationException()
    {
        // Arrange
        var validators = new List<IValidator<TestCommand>> { new TestCommandValidator() };
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        var command = new TestCommand("", -1);

        // Act & Assert
        var exception = await Should.ThrowAsync<ValidationException>(
            () => behavior.HandleAsync(command, () => Task.FromResult("Success")));

        exception.Errors.Count.ShouldBe(2);
        exception.Errors.ShouldContain(e => e.PropertyName == "Name");
        exception.Errors.ShouldContain(e => e.PropertyName == "Age");
    }

    [Fact]
    public async Task HandleAsync_WithNoValidators_ShouldProceedToNext()
    {
        // Arrange
        var validators = new List<IValidator<TestCommand>>();
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        var command = new TestCommand("", -1); // Invalid but no validators
        var nextCalled = false;

        // Act
        var result = await behavior.HandleAsync(command, () =>
        {
            nextCalled = true;
            return Task.FromResult("Success");
        });

        // Assert
        nextCalled.ShouldBeTrue();
        result.ShouldBe("Success");
    }

    [Fact]
    public async Task HandleAsync_WithMultipleValidators_ShouldAggregateErrors()
    {
        // Arrange
        var validators = new List<IValidator<TestCommand>>
        {
            new TestCommandValidator(),
            new AnotherValidator()
        };
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        var command = new TestCommand(new string('a', 51), -1); // Triggers both validators

        // Act & Assert
        var exception = await Should.ThrowAsync<ValidationException>(
            () => behavior.HandleAsync(command, () => Task.FromResult("Success")));

        exception.Errors.Count.ShouldBe(2);
        exception.Errors.ShouldContain(e => e.ErrorMessage == "Age must be non-negative");
        exception.Errors.ShouldContain(e => e.ErrorMessage == "Name must be 50 characters or less");
    }

    [Fact]
    public async Task HandleAsync_WithMultipleValidatorsAllPassing_ShouldProceedToNext()
    {
        // Arrange
        var validators = new List<IValidator<TestCommand>>
        {
            new TestCommandValidator(),
            new AnotherValidator()
        };
        var behavior = new ValidationBehavior<TestCommand, string>(validators);
        var command = new TestCommand("ValidName", 25);

        // Act
        var result = await behavior.HandleAsync(command, () => Task.FromResult("Success"));

        // Assert
        result.ShouldBe("Success");
    }
}
