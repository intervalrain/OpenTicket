using ErrorOr;
using NSubstitute;
using OpenTicket.Ddd.Application.Cqrs;
using OpenTicket.Ddd.Application.Cqrs.Authorization;
using OpenTicket.Ddd.Application.Cqrs.Behaviors;
using Shouldly;

namespace OpenTicket.Ddd.Tests.Application.Cqrs.Behaviors;

public class AuthorizationBehaviorTests
{
    private readonly IRequestAuthorizationService _authorizationService;
    private readonly AuthorizationBehavior<TestAuthorizeableCommand, ErrorOr<string>> _sut;

    public AuthorizationBehaviorTests()
    {
        _authorizationService = Substitute.For<IRequestAuthorizationService>();
        _sut = new AuthorizationBehavior<TestAuthorizeableCommand, ErrorOr<string>>(_authorizationService);
    }

    // Test command without [Authorize] attribute
    public record TestAuthorizeableCommand : IAuthorizeableCommand<ErrorOr<string>>;

    // Test command with [Authorize] attribute for roles
    [Authorize(Roles = "Admin,User")]
    public record TestAuthorizeableCommandWithRoles : IAuthorizeableCommand<ErrorOr<string>>;

    // Test command with multiple [Authorize] attributes
    [Authorize(Roles = "Admin")]
    [Authorize(Permissions = "Note.Create,Note.Update")]
    public record TestAuthorizeableCommandWithMultipleAttributes : IAuthorizeableCommand<ErrorOr<string>>;

    [Fact]
    public async Task HandleAsync_WithNoAuthorizeAttribute_ShouldProceedToNext()
    {
        // Arrange
        var command = new TestAuthorizeableCommand();
        var nextCalled = false;

        // Act
        var result = await _sut.HandleAsync(command, () =>
        {
            nextCalled = true;
            return Task.FromResult<ErrorOr<string>>("Success");
        });

        // Assert
        nextCalled.ShouldBeTrue();
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe("Success");
        _authorizationService.DidNotReceive().AuthorizeCurrentUser(
            Arg.Any<TestAuthorizeableCommand>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task HandleAsync_WithAuthorizeAttribute_WhenAuthorized_ShouldProceedToNext()
    {
        // Arrange
        var behavior = new AuthorizationBehavior<TestAuthorizeableCommandWithRoles, ErrorOr<string>>(_authorizationService);
        var command = new TestAuthorizeableCommandWithRoles();

        _authorizationService.AuthorizeCurrentUser(
            Arg.Any<TestAuthorizeableCommandWithRoles>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>())
            .Returns(Result.Success);

        var nextCalled = false;

        // Act
        var result = await behavior.HandleAsync(command, () =>
        {
            nextCalled = true;
            return Task.FromResult<ErrorOr<string>>("Success");
        });

        // Assert
        nextCalled.ShouldBeTrue();
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe("Success");

        _authorizationService.Received(1).AuthorizeCurrentUser(
            command,
            Arg.Is<IReadOnlyList<string>>(roles => roles.Contains("Admin") && roles.Contains("User")),
            Arg.Is<IReadOnlyList<string>>(perms => perms.Count == 0),
            Arg.Is<IReadOnlyList<string>>(policies => policies.Count == 0));
    }

    [Fact]
    public async Task HandleAsync_WithAuthorizeAttribute_WhenNotAuthorized_ShouldReturnErrors()
    {
        // Arrange
        var behavior = new AuthorizationBehavior<TestAuthorizeableCommandWithRoles, ErrorOr<string>>(_authorizationService);
        var command = new TestAuthorizeableCommandWithRoles();

        var expectedError = Error.Forbidden(
            "Authorization.MissingRole",
            "User is missing required roles.");

        _authorizationService.AuthorizeCurrentUser(
            Arg.Any<TestAuthorizeableCommandWithRoles>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>())
            .Returns(expectedError);

        var nextCalled = false;

        // Act
        var result = await behavior.HandleAsync(command, () =>
        {
            nextCalled = true;
            return Task.FromResult<ErrorOr<string>>("Success");
        });

        // Assert
        nextCalled.ShouldBeFalse();
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Authorization.MissingRole");
    }

    [Fact]
    public async Task HandleAsync_WithMultipleAuthorizeAttributes_ShouldCollectAllRequirements()
    {
        // Arrange
        var behavior = new AuthorizationBehavior<TestAuthorizeableCommandWithMultipleAttributes, ErrorOr<string>>(_authorizationService);
        var command = new TestAuthorizeableCommandWithMultipleAttributes();

        _authorizationService.AuthorizeCurrentUser(
            Arg.Any<TestAuthorizeableCommandWithMultipleAttributes>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>())
            .Returns(Result.Success);

        // Act
        await behavior.HandleAsync(command, () => Task.FromResult<ErrorOr<string>>("Success"));

        // Assert
        _authorizationService.Received(1).AuthorizeCurrentUser(
            command,
            Arg.Is<IReadOnlyList<string>>(roles => roles.Contains("Admin")),
            Arg.Is<IReadOnlyList<string>>(perms => perms.Contains("Note.Create") && perms.Contains("Note.Update")),
            Arg.Is<IReadOnlyList<string>>(policies => policies.Count == 0));
    }

    [Fact]
    public async Task HandleAsync_WithUnauthenticatedUser_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var behavior = new AuthorizationBehavior<TestAuthorizeableCommandWithRoles, ErrorOr<string>>(_authorizationService);
        var command = new TestAuthorizeableCommandWithRoles();

        var expectedError = Error.Unauthorized(
            "Authorization.Unauthenticated",
            "User is not authenticated.");

        _authorizationService.AuthorizeCurrentUser(
            Arg.Any<TestAuthorizeableCommandWithRoles>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>())
            .Returns(expectedError);

        // Act
        var result = await behavior.HandleAsync(command, () => Task.FromResult<ErrorOr<string>>("Success"));

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Authorization.Unauthenticated");
        result.FirstError.Type.ShouldBe(ErrorType.Unauthorized);
    }
}
