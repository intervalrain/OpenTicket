# OpenTicket Auth SDK

**Version:** 1.0
**Last Updated:** 2025-01

---

## 1. Overview

OpenTicket Auth SDK provides a comprehensive authentication and authorization framework with:

- **Authentication**: Identity management with OAuth providers (Google, Facebook, GitHub, Apple)
- **Authorization**: Resource-based access control for domain entities
- **Rate Limiting**: Action-based quotas with subscription tiers
- **JWT Token**: Secure token generation and validation

```
┌────────────────────────────────────────────────────────────────────────────────────┐
│                                       Auth Architecture                            │
├────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                    │
│  ┌────────────────────────────┐    ┌──────────────────┐    ┌─────────────────┐     │
│  │       API Controller       │───▶│   ICurrentUser   │◀───│  JWT Provider   │     │
│  │                            │    │     Provider     │    │  (HttpContext)  │     │
│  └────────────────────────────┘    └──────────────────┘    └─────────────────┘     │
│                │                            │                       │              │
│                ▼                            ▼                       ▼              │
│  ┌────────────────────────────┐    ┌──────────────────┐    ┌─────────────────┐     │
│  │   IAuthorizationService    │    │  IRateLimitSvc   │    │ ITokenService   │     │
│  └────────────────────────────┘    └──────────────────┘    └─────────────────┘     │
│                │                            │                       │              │
│                ▼                            ▼                       ▼              │
│  ┌────────────────────────────────────────────────────────────────────────────┐    │
│  │                                   Domain Resources                         │    │
│  │                    Note.CanRead()  Note.CanModify()  SharedWith[]          │    │
│  └────────────────────────────────────────────────────────────────────────────┘    │
│                                                                                    │
└────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Quick Start

### 2.1 Service Registration

```csharp
// Program.cs or Module
public static IServiceCollection AddOpenTicketAuth(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Option 1: Mock Identity (MVP/Testing)
    services.AddIdentity(IdentityProvider.Mock);

    // Option 2: OAuth Identity (Production)
    services.AddOAuthIdentity(configuration);

    // Authorization & Rate Limiting (auto-registered via Application module)
    services.AddOpenTicketApplication();

    return services;
}
```

### 2.2 Configuration

```json
// appsettings.json
{
  "OAuth": {
    "Jwt": {
      "Secret": "your-256-bit-secret-key-here-minimum-32-chars",
      "Issuer": "OpenTicket",
      "Audience": "OpenTicket.Api",
      "ExpirationMinutes": 60
    },
    "Google": {
      "ClientId": "your-google-client-id",
      "ClientSecret": "your-google-client-secret"
    },
    "Facebook": {
      "ClientId": "your-facebook-app-id",
      "ClientSecret": "your-facebook-app-secret"
    },
    "GitHub": {
      "ClientId": "your-github-client-id",
      "ClientSecret": "your-github-client-secret"
    },
    "Apple": {
      "ClientId": "your-apple-service-id",
      "TeamId": "your-apple-team-id",
      "KeyId": "your-apple-key-id",
      "PrivateKey": "-----BEGIN PRIVATE KEY-----..."
    }
  }
}
```

---

## 3. Core Abstractions

### 3.1 CurrentUser

Represents the authenticated user in the current request context.

```csharp
namespace OpenTicket.Application.Contracts.Identity;

public class CurrentUser
{
    /// <summary>
    /// Unique user identifier (strongly typed).
    /// </summary>
    public UserId Id { get; init; }

    /// <summary>
    /// User's email address.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Assigned roles (e.g., "Admin", "User").
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Whether the user is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Whether the user has an active subscription.
    /// </summary>
    public bool HasSubscription { get; init; }

    // Helper methods
    public bool IsInRole(string role) =>
        Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public bool IsAdmin => IsInRole("Admin");
}
```

### 3.2 Roles

Standard role constants used throughout the application.

```csharp
namespace OpenTicket.Application.Contracts.Identity;

public static class Roles
{
    /// <summary>
    /// Administrator with full system access.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Regular authenticated user.
    /// </summary>
    public const string User = "User";
}
```

### 3.3 ICurrentUserProvider

Interface to access the current user from any service.

```csharp
namespace OpenTicket.Application.Contracts.Identity;

public interface ICurrentUserProvider
{
    /// <summary>
    /// Gets the current authenticated user.
    /// </summary>
    CurrentUser CurrentUser { get; }
}
```

---

## 4. Authorization Service

### 4.1 IAuthorizationService

Resource-based authorization for domain entities.

```csharp
namespace OpenTicket.Application.Contracts.Authorization;

public interface IAuthorizationService
{
    /// <summary>
    /// Authorize an action on a resource.
    /// </summary>
    Task<ErrorOr<Success>> AuthorizeAsync<TResource>(
        TResource resource,
        ResourceAction action,
        CancellationToken ct = default);

    /// <summary>
    /// Current authenticated user.
    /// </summary>
    CurrentUser CurrentUser { get; }

    /// <summary>
    /// Whether current user is admin.
    /// </summary>
    bool IsAdmin { get; }
}

public enum ResourceAction
{
    Read,
    Create,
    Update,
    Delete
}
```

### 4.2 Usage in Handlers

```csharp
public sealed class UpdateNoteCommandHandler
    : ICommandHandler<UpdateNoteCommand, ErrorOr<Updated>>
{
    private readonly IRepository<Note> _noteRepository;
    private readonly IAuthorizationService _authorizationService;

    public async Task<ErrorOr<Updated>> HandleAsync(
        UpdateNoteCommand command,
        CancellationToken ct = default)
    {
        var note = await _noteRepository.FindAsync(command.Id, ct);

        if (note is null)
            return Error.NotFound("Note.NotFound", "Note not found.");

        // Check authorization
        var authResult = await _authorizationService.AuthorizeAsync(
            note,
            ResourceAction.Update,
            ct);

        if (authResult.IsError)
            return authResult.Errors;

        // Proceed with update...
        note.Update(command.Title, command.Body);
        await _noteRepository.UpdateAsync(note, ct);

        return Result.Updated;
    }
}
```

### 4.3 Authorization Rules

| Resource | Action | Rule |
|----------|--------|------|
| Note | Read | Creator OR SharedWith OR Admin |
| Note | Create | Any authenticated user |
| Note | Update | Creator OR Admin |
| Note | Delete | Creator OR Admin |

### 4.4 Domain Model Integration

Add access control methods to domain entities:

```csharp
public class Note : Entity<Guid>
{
    public UserId CreatorId { get; private set; }

    private readonly List<UserId> _sharedWith = [];
    public IReadOnlyList<UserId> SharedWith => _sharedWith.AsReadOnly();

    /// <summary>
    /// Check if user can read this note.
    /// </summary>
    public bool CanRead(UserId userId)
    {
        return CreatorId == userId || IsSharedWith(userId);
    }

    /// <summary>
    /// Check if user can modify this note.
    /// </summary>
    public bool CanModify(UserId userId)
    {
        return CreatorId == userId;
    }

    /// <summary>
    /// Share note with another user.
    /// </summary>
    public void ShareWith(UserId userId)
    {
        if (!IsSharedWith(userId) && userId != CreatorId)
        {
            _sharedWith.Add(userId);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Revoke share access.
    /// </summary>
    public void RevokeShare(UserId userId)
    {
        _sharedWith.Remove(userId);
        UpdatedAt = DateTime.UtcNow;
    }

    private bool IsSharedWith(UserId userId) =>
        _sharedWith.Contains(userId);
}
```

---

## 5. Rate Limiting Service

### 5.1 IRateLimitService

Action-based quota enforcement.

```csharp
namespace OpenTicket.Application.Contracts.RateLimiting;

public interface IRateLimitService
{
    /// <summary>
    /// Check if action is allowed under rate limit.
    /// </summary>
    Task<ErrorOr<Success>> CheckRateLimitAsync(
        UserId userId,
        RateLimitedAction action,
        CancellationToken ct = default);

    /// <summary>
    /// Record an action for rate limit tracking.
    /// </summary>
    Task RecordActionAsync(
        UserId userId,
        RateLimitedAction action,
        CancellationToken ct = default);

    /// <summary>
    /// Get remaining quota for an action.
    /// </summary>
    Task<int> GetRemainingQuotaAsync(
        UserId userId,
        RateLimitedAction action,
        CancellationToken ct = default);
}

public enum RateLimitedAction
{
    CreateNote
}
```

### 5.2 Rate Limit Rules

| User Type | CreateNote/Day |
|-----------|----------------|
| Non-subscriber | 3 |
| Subscriber | Unlimited |
| Admin | Unlimited |

### 5.3 Usage in Handlers

```csharp
public sealed class CreateNoteCommandHandler
    : ICommandHandler<CreateNoteCommand, ErrorOr<CreateNoteCommandResult>>
{
    private readonly IRepository<Note> _noteRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IRateLimitService _rateLimitService;

    public async Task<ErrorOr<CreateNoteCommandResult>> HandleAsync(
        CreateNoteCommand command,
        CancellationToken ct = default)
    {
        var currentUser = _currentUserProvider.CurrentUser;

        // Check rate limit
        var rateLimitResult = await _rateLimitService.CheckRateLimitAsync(
            currentUser.Id,
            RateLimitedAction.CreateNote,
            ct);

        if (rateLimitResult.IsError)
            return rateLimitResult.Errors;

        // Create note
        var note = Note.Create(command.Title, command.Body, currentUser.Id);
        await _noteRepository.InsertAsync(note, ct);

        // Record action for rate limiting
        await _rateLimitService.RecordActionAsync(
            currentUser.Id,
            RateLimitedAction.CreateNote,
            ct);

        return new CreateNoteCommandResult(note.Id);
    }
}
```

### 5.4 Rate Limit Implementation

Uses distributed cache for quota tracking:

```csharp
public sealed class RateLimitService : IRateLimitService
{
    private readonly IDistributedCache _cache;
    private readonly ICurrentUserProvider _currentUserProvider;

    private const int NonSubscriberDailyNoteLimit = 3;

    public async Task<ErrorOr<Success>> CheckRateLimitAsync(
        UserId userId,
        RateLimitedAction action,
        CancellationToken ct = default)
    {
        var currentUser = _currentUserProvider.CurrentUser;

        // Bypass for admin and subscribers
        if (currentUser.IsAdmin || currentUser.HasSubscription)
            return Result.Success;

        var limit = GetLimit(action);
        if (limit < 0) // Unlimited
            return Result.Success;

        var key = GetRateLimitKey(userId, action);
        var count = await _cache.GetAsync<int?>(key, ct) ?? 0;

        if (count >= limit)
        {
            return Error.Forbidden(
                "RateLimit.Exceeded",
                $"Rate limit exceeded. Daily limit: {limit}.");
        }

        return Result.Success;
    }

    private static string GetRateLimitKey(UserId userId, RateLimitedAction action)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return $"ratelimit:{action}:{userId.Value}:{today}";
    }
}
```

---

## 6. OAuth / JWT Authentication

### 6.1 ITokenService

JWT token generation for authenticated users.

```csharp
namespace OpenTicket.Application.Contracts.Identity;

public interface ITokenService
{
    /// <summary>
    /// Generate JWT token for user.
    /// </summary>
    string GenerateToken(CurrentUser user);

    /// <summary>
    /// Validate and parse JWT token.
    /// </summary>
    CurrentUser? ValidateToken(string token);
}
```

### 6.2 JWT Token Structure

```json
{
  "sub": "user-guid",
  "email": "user@example.com",
  "name": "John Doe",
  "roles": ["User"],
  "has_subscription": true,
  "iat": 1735344000,
  "exp": 1735347600,
  "iss": "OpenTicket",
  "aud": "OpenTicket.Api"
}
```

### 6.3 OAuth Flow

```
┌─────────┐     ┌─────────────┐     ┌──────────────┐     ┌─────────┐
│ Client  │────▶│ /auth/login │────▶│ OAuth        │────▶│ Google  │
│         │     │ /{provider} │     │ Redirect     │     │ FB/etc  │
└─────────┘     └─────────────┘     └──────────────┘     └─────────┘
                                                               │
     ┌─────────────────────────────────────────────────────────┘
     │
     ▼
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│ /auth/      │────▶│ Validate    │────▶│ Generate    │
│ callback    │     │ OAuth Token │     │ JWT Token   │
└─────────────┘     └─────────────┘     └─────────────┘
                                               │
                                               ▼
                                        ┌─────────────┐
                                        │ Return JWT  │
                                        │ to Client   │
                                        └─────────────┘
```

### 6.4 JwtCurrentUserProvider

Extracts user from HTTP context JWT claims:

```csharp
public class JwtCurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser CurrentUser
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User.Identity?.IsAuthenticated != true)
            {
                return new CurrentUser { IsAuthenticated = false };
            }

            var claims = httpContext.User.Claims.ToList();

            return new CurrentUser
            {
                Id = UserId.From(Guid.Parse(
                    claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value)),
                Email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
                    ?? string.Empty,
                Name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value
                    ?? string.Empty,
                Roles = claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList(),
                HasSubscription = bool.TryParse(
                    claims.FirstOrDefault(c => c.Type == "has_subscription")?.Value,
                    out var sub) && sub,
                IsAuthenticated = true
            };
        }
    }
}
```

---

## 7. Identity Provider Setup

### 7.1 Mock Provider (MVP/Testing)

```csharp
// Registration
services.Configure<MockUserOptions>(opt =>
{
    opt.UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    opt.Email = "test@openticket.local";
    opt.Name = "Test User";
    opt.Roles = ["User"];
    opt.HasSubscription = false;
});
services.AddScoped<ICurrentUserProvider, MockCurrentUserProvider>();
```

### 7.2 OAuth Provider (Production)

```csharp
// Registration
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["OAuth:Jwt:Issuer"],
            ValidAudience = configuration["OAuth:Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["OAuth:Jwt:Secret"]!))
        };
    });

services.AddScoped<ICurrentUserProvider, JwtCurrentUserProvider>();
services.AddScoped<ITokenService, JwtTokenService>();
```

---

## 8. API Controller Integration

### 8.1 Controller with ErrorOr Handling

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize] // Requires authentication
public class NoteController : ApiController
{
    private readonly IDispatcher _dispatcher;

    [HttpPost]
    [ProducesResponseType(typeof(CreateNoteCommandResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateNoteRequest request,
        CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(
            new CreateNoteCommand(request.Title, request.Body),
            ct);

        return result.Match(
            success => CreatedAtAction(
                nameof(GetById),
                new { id = success.Id },
                success),
            errors => Problem(errors));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateNoteRequest request,
        CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(
            new UpdateNoteCommand(id, request.Title, request.Body),
            ct);

        return result.Match(
            _ => NoContent(),
            errors => Problem(errors));
    }
}
```

### 8.2 Error Response Format

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403,
  "errors": {
    "Authorization": ["Note.ModifyDenied: Only the creator can modify this note."]
  }
}
```

---

## 9. Testing

### 9.1 Authorization Service Tests

```csharp
public class AuthorizationServiceTests
{
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly AuthorizationService _sut;

    [Fact]
    public async Task AuthorizeAsync_WhenAdmin_AlwaysAllowsAccess()
    {
        // Arrange
        var adminUser = CreateUser(isAdmin: true);
        _currentUserProvider.CurrentUser.Returns(adminUser);

        var note = Note.Create("Test", "Body", UserId.New());

        // Act
        var result = await _sut.AuthorizeAsync(note, ResourceAction.Delete);

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public async Task AuthorizeAsync_WhenSharedUser_CanOnlyRead()
    {
        // Arrange
        var sharedUserId = UserId.New();
        var user = CreateUser(sharedUserId);
        _currentUserProvider.CurrentUser.Returns(user);

        var note = Note.Create("Test", "Body", UserId.New());
        note.ShareWith(sharedUserId);

        // Act
        var readResult = await _sut.AuthorizeAsync(note, ResourceAction.Read);
        var updateResult = await _sut.AuthorizeAsync(note, ResourceAction.Update);

        // Assert
        readResult.IsError.ShouldBeFalse();
        updateResult.IsError.ShouldBeTrue();
        updateResult.FirstError.Code.ShouldBe("Note.ModifyDenied");
    }
}
```

### 9.2 Rate Limit Service Tests

```csharp
public class RateLimitServiceTests
{
    [Fact]
    public async Task CheckRateLimitAsync_WhenSubscriber_AlwaysAllows()
    {
        // Arrange
        var subscriberUser = CreateUser(hasSubscription: true);
        _currentUserProvider.CurrentUser.Returns(subscriberUser);

        // Act
        var result = await _sut.CheckRateLimitAsync(
            subscriberUser.Id,
            RateLimitedAction.CreateNote);

        // Assert
        result.IsError.ShouldBeFalse();
        // Cache should not be accessed
        await _cache.DidNotReceive().GetAsync<int?>(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckRateLimitAsync_WhenNonSubscriberAtLimit_DeniesAccess()
    {
        // Arrange
        var user = CreateUser();
        _currentUserProvider.CurrentUser.Returns(user);
        _cache.GetAsync<int?>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(3); // At limit

        // Act
        var result = await _sut.CheckRateLimitAsync(
            user.Id,
            RateLimitedAction.CreateNote);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("RateLimit.Exceeded");
    }
}
```

---

## 10. Summary

### 10.1 Component Matrix

| Component | Abstraction | Implementation | Layer |
|-----------|-------------|----------------|-------|
| Current User | `ICurrentUserProvider` | `MockCurrentUserProvider`, `JwtCurrentUserProvider` | Infrastructure.Identity |
| Authorization | `IAuthorizationService` | `AuthorizationService` | Application |
| Rate Limiting | `IRateLimitService` | `RateLimitService` | Application |
| Token Service | `ITokenService` | `JwtTokenService` | Infrastructure.Identity |

### 10.2 Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `Authorization.Unauthenticated` | 401 | User not authenticated |
| `Note.AccessDenied` | 403 | No read permission |
| `Note.ModifyDenied` | 403 | No modify permission |
| `RateLimit.Exceeded` | 403 | Daily quota exceeded |

### 10.3 Best Practices

1. **Always use ErrorOr**: Return `ErrorOr<T>` from handlers for consistent error handling
2. **Check authorization after fetch**: Load resource first, then check permissions
3. **Record rate limit after success**: Only count actions that succeed
4. **Domain owns access rules**: `CanRead()`, `CanModify()` belong in domain entities
5. **Inject ICurrentUserProvider**: Don't pass user ID through all layers
