using NSubstitute;
using OpenTicket.Application.Contracts.Identity;
using OpenTicket.Application.Contracts.RateLimiting;
using OpenTicket.Application.RateLimiting;
using OpenTicket.Domain.Shared.Identities;
using OpenTicket.Infrastructure.Cache.Abstractions;
using Shouldly;

namespace OpenTicket.Application.Tests.RateLimiting;

public class RateLimitServiceTests
{
    private readonly IDistributedCache _cache;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly RateLimitService _sut;

    public RateLimitServiceTests()
    {
        _cache = Substitute.For<IDistributedCache>();
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _sut = new RateLimitService(_cache, _currentUserProvider);
    }

    #region CheckRateLimitAsync

    [Fact]
    public async Task CheckRateLimitAsync_WhenAdmin_AlwaysAllows()
    {
        // Arrange
        var adminUser = CreateUser(isAdmin: true);
        _currentUserProvider.CurrentUser.Returns(adminUser);

        // Act
        var result = await _sut.CheckRateLimitAsync(adminUser.Id, RateLimitedAction.CreateNote);

        // Assert
        result.IsError.ShouldBeFalse();
        await _cache.DidNotReceive().GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenSubscriber_AlwaysAllows()
    {
        // Arrange
        var subscriberUser = CreateUser(hasSubscription: true);
        _currentUserProvider.CurrentUser.Returns(subscriberUser);

        // Act
        var result = await _sut.CheckRateLimitAsync(subscriberUser.Id, RateLimitedAction.CreateNote);

        // Assert
        result.IsError.ShouldBeFalse();
        await _cache.DidNotReceive().GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenNonSubscriberUnderLimit_AllowsAccess()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateUser(userId);
        _currentUserProvider.CurrentUser.Returns(user);
        _cache.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(2); // Under limit of 3

        // Act
        var result = await _sut.CheckRateLimitAsync(userId, RateLimitedAction.CreateNote);

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenNonSubscriberAtLimit_DeniesAccess()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateUser(userId);
        _currentUserProvider.CurrentUser.Returns(user);
        _cache.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(3); // At limit of 3

        // Act
        var result = await _sut.CheckRateLimitAsync(userId, RateLimitedAction.CreateNote);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("RateLimit.Exceeded");
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenNonSubscriberOverLimit_DeniesAccess()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateUser(userId);
        _currentUserProvider.CurrentUser.Returns(user);
        _cache.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(10); // Over limit of 3

        // Act
        var result = await _sut.CheckRateLimitAsync(userId, RateLimitedAction.CreateNote);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("RateLimit.Exceeded");
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenNoCountInCache_AllowsAccess()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateUser(userId);
        _currentUserProvider.CurrentUser.Returns(user);
        _cache.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((int?)null);

        // Act
        var result = await _sut.CheckRateLimitAsync(userId, RateLimitedAction.CreateNote);

        // Assert
        result.IsError.ShouldBeFalse();
    }

    #endregion

    #region RecordActionAsync

    [Fact]
    public async Task RecordActionAsync_WhenAdmin_DoesNotRecordAction()
    {
        // Arrange
        var adminUser = CreateUser(isAdmin: true);
        _currentUserProvider.CurrentUser.Returns(adminUser);

        // Act
        await _sut.RecordActionAsync(adminUser.Id, RateLimitedAction.CreateNote);

        // Assert
        await _cache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_WhenSubscriber_DoesNotRecordAction()
    {
        // Arrange
        var subscriberUser = CreateUser(hasSubscription: true);
        _currentUserProvider.CurrentUser.Returns(subscriberUser);

        // Act
        await _sut.RecordActionAsync(subscriberUser.Id, RateLimitedAction.CreateNote);

        // Assert
        await _cache.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_WhenNonSubscriber_IncrementsCount()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateUser(userId);
        _currentUserProvider.CurrentUser.Returns(user);
        _cache.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(2);

        // Act
        await _sut.RecordActionAsync(userId, RateLimitedAction.CreateNote);

        // Assert
        await _cache.Received(1).SetAsync(
            Arg.Any<string>(),
            3, // Incremented from 2 to 3
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordActionAsync_WhenNoExistingCount_SetsCountToOne()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateUser(userId);
        _currentUserProvider.CurrentUser.Returns(user);
        _cache.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((int?)null);

        // Act
        await _sut.RecordActionAsync(userId, RateLimitedAction.CreateNote);

        // Assert
        await _cache.Received(1).SetAsync(
            Arg.Any<string>(),
            1, // First action
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetRemainingQuotaAsync

    [Fact]
    public async Task GetRemainingQuotaAsync_WhenAdmin_ReturnsUnlimited()
    {
        // Arrange
        var adminUser = CreateUser(isAdmin: true);
        _currentUserProvider.CurrentUser.Returns(adminUser);

        // Act
        var result = await _sut.GetRemainingQuotaAsync(adminUser.Id, RateLimitedAction.CreateNote);

        // Assert
        result.ShouldBe(-1); // -1 means unlimited
    }

    [Fact]
    public async Task GetRemainingQuotaAsync_WhenSubscriber_ReturnsUnlimited()
    {
        // Arrange
        var subscriberUser = CreateUser(hasSubscription: true);
        _currentUserProvider.CurrentUser.Returns(subscriberUser);

        // Act
        var result = await _sut.GetRemainingQuotaAsync(subscriberUser.Id, RateLimitedAction.CreateNote);

        // Assert
        result.ShouldBe(-1); // -1 means unlimited
    }

    [Fact]
    public async Task GetRemainingQuotaAsync_WhenNonSubscriberWithNoActions_ReturnsFullQuota()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateUser(userId);
        _currentUserProvider.CurrentUser.Returns(user);
        _cache.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((int?)null);

        // Act
        var result = await _sut.GetRemainingQuotaAsync(userId, RateLimitedAction.CreateNote);

        // Assert
        result.ShouldBe(3); // Full quota of 3
    }

    [Fact]
    public async Task GetRemainingQuotaAsync_WhenNonSubscriberWithSomeActions_ReturnsRemainingQuota()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateUser(userId);
        _currentUserProvider.CurrentUser.Returns(user);
        _cache.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(2);

        // Act
        var result = await _sut.GetRemainingQuotaAsync(userId, RateLimitedAction.CreateNote);

        // Assert
        result.ShouldBe(1); // 3 - 2 = 1 remaining
    }

    [Fact]
    public async Task GetRemainingQuotaAsync_WhenNonSubscriberAtLimit_ReturnsZero()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateUser(userId);
        _currentUserProvider.CurrentUser.Returns(user);
        _cache.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(3);

        // Act
        var result = await _sut.GetRemainingQuotaAsync(userId, RateLimitedAction.CreateNote);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public async Task GetRemainingQuotaAsync_WhenNonSubscriberOverLimit_ReturnsZero()
    {
        // Arrange
        var userId = UserId.New();
        var user = CreateUser(userId);
        _currentUserProvider.CurrentUser.Returns(user);
        _cache.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(10);

        // Act
        var result = await _sut.GetRemainingQuotaAsync(userId, RateLimitedAction.CreateNote);

        // Assert
        result.ShouldBe(0); // Math.Max(0, 3-10) = 0
    }

    #endregion

    private static CurrentUser CreateUser(
        UserId? userId = null,
        bool isAdmin = false,
        bool hasSubscription = false)
    {
        IReadOnlyList<string> roles = isAdmin
            ? [Roles.Admin, Roles.User]
            : [Roles.User];

        return new CurrentUser
        {
            Id = userId ?? UserId.New(),
            Email = "test@example.com",
            Name = "Test User",
            Roles = roles,
            IsAuthenticated = true,
            HasSubscription = hasSubscription
        };
    }
}
