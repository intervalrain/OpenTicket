using NSubstitute;
using OpenTicket.Application.Authorization;
using OpenTicket.Application.Contracts.Authorization;
using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Domain.Notes.Entities;
using OpenTicket.Domain.Shared.Identities;
using Shouldly;

namespace OpenTicket.Application.Tests.Authorization;

public class AuthorizationServiceTests
{
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly AuthorizationService _sut;

    public AuthorizationServiceTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _sut = new AuthorizationService(_currentUserProvider);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenUserNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var user = new CurrentUser
        {
            Id = UserId.New(),
            Email = "",
            Name = "",
            Roles = [],
            IsAuthenticated = false
        };
        _currentUserProvider.CurrentUser.Returns(user);
        var note = Note.Create("Test", "Body", UserId.New());

        // Act
        var result = await _sut.AuthorizeAsync(note, ResourceAction.Read);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Authorization.Unauthenticated");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAdmin_AlwaysAllowsAccess()
    {
        // Arrange
        var adminUser = CreateUser(isAdmin: true);
        _currentUserProvider.CurrentUser.Returns(adminUser);

        var otherUserId = UserId.New();
        var note = Note.Create("Test", "Body", otherUserId);

        // Act - even though admin is not the creator
        var readResult = await _sut.AuthorizeAsync(note, ResourceAction.Read);
        var updateResult = await _sut.AuthorizeAsync(note, ResourceAction.Update);
        var deleteResult = await _sut.AuthorizeAsync(note, ResourceAction.Delete);

        // Assert
        readResult.IsError.ShouldBeFalse();
        updateResult.IsError.ShouldBeFalse();
        deleteResult.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenCreator_CanReadModifyDelete()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateUser(userId);
        _currentUserProvider.CurrentUser.Returns(user);

        var note = Note.Create("Test", "Body", userId);

        // Act
        var readResult = await _sut.AuthorizeAsync(note, ResourceAction.Read);
        var updateResult = await _sut.AuthorizeAsync(note, ResourceAction.Update);
        var deleteResult = await _sut.AuthorizeAsync(note, ResourceAction.Delete);

        // Assert
        readResult.IsError.ShouldBeFalse();
        updateResult.IsError.ShouldBeFalse();
        deleteResult.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenSharedUser_CanOnlyRead()
    {
        // Arrange
        var sharedUserId = UserId.New();
        var user = CreateUser(sharedUserId);
        _currentUserProvider.CurrentUser.Returns(user);

        var creatorId = UserId.New();
        var note = Note.Create("Test", "Body", creatorId);
        note.ShareWith(sharedUserId);

        // Act
        var readResult = await _sut.AuthorizeAsync(note, ResourceAction.Read);
        var updateResult = await _sut.AuthorizeAsync(note, ResourceAction.Update);
        var deleteResult = await _sut.AuthorizeAsync(note, ResourceAction.Delete);

        // Assert
        readResult.IsError.ShouldBeFalse();
        updateResult.IsError.ShouldBeTrue();
        updateResult.FirstError.Code.ShouldBe("Note.ModifyDenied");
        deleteResult.IsError.ShouldBeTrue();
        deleteResult.FirstError.Code.ShouldBe("Note.ModifyDenied");
    }

    [Fact]
    public async Task AuthorizeAsync_WhenUnrelatedUser_CannotReadOrModify()
    {
        // Arrange
        var unrelatedUserId = UserId.New();
        var user = CreateUser(unrelatedUserId);
        _currentUserProvider.CurrentUser.Returns(user);

        var creatorId = UserId.New();
        var note = Note.Create("Test", "Body", creatorId);

        // Act
        var readResult = await _sut.AuthorizeAsync(note, ResourceAction.Read);
        var updateResult = await _sut.AuthorizeAsync(note, ResourceAction.Update);
        var deleteResult = await _sut.AuthorizeAsync(note, ResourceAction.Delete);

        // Assert
        readResult.IsError.ShouldBeTrue();
        readResult.FirstError.Code.ShouldBe("Note.AccessDenied");
        updateResult.IsError.ShouldBeTrue();
        updateResult.FirstError.Code.ShouldBe("Note.ModifyDenied");
        deleteResult.IsError.ShouldBeTrue();
        deleteResult.FirstError.Code.ShouldBe("Note.ModifyDenied");
    }

    [Fact]
    public async Task AuthorizeAsync_ForUnknownResource_AllowsAccess()
    {
        // Arrange
        var user = CreateUser();
        _currentUserProvider.CurrentUser.Returns(user);
        var unknownResource = new { Name = "Unknown" };

        // Act
        var result = await _sut.AuthorizeAsync(unknownResource, ResourceAction.Read);

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void CurrentUser_ReturnsCurrentUser()
    {
        // Arrange
        var user = CreateUser();
        _currentUserProvider.CurrentUser.Returns(user);

        // Act
        var result = _sut.CurrentUser;

        // Assert
        result.ShouldBe(user);
    }

    [Fact]
    public void IsAdmin_WhenUserIsAdmin_ReturnsTrue()
    {
        // Arrange
        var adminUser = CreateUser(isAdmin: true);
        _currentUserProvider.CurrentUser.Returns(adminUser);

        // Act
        var result = _sut.IsAdmin;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsAdmin_WhenUserIsNotAdmin_ReturnsFalse()
    {
        // Arrange
        var user = CreateUser(isAdmin: false);
        _currentUserProvider.CurrentUser.Returns(user);

        // Act
        var result = _sut.IsAdmin;

        // Assert
        result.ShouldBeFalse();
    }

    private static CurrentUser CreateUser(UserId? userId = null, bool isAdmin = false)
    {
        IReadOnlyList<string> roles = isAdmin
            ? [WellKnownRoles.Admin, WellKnownRoles.User]
            : [WellKnownRoles.User];

        return new CurrentUser
        {
            Id = userId ?? UserId.New(),
            Email = "test@example.com",
            Name = "Test User",
            Roles = roles,
            IsAuthenticated = true
        };
    }
}
