# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OpenTicket is a globally distributed ticketing platform built with .NET 9, NATS JetStream, and DDD principles. It's designed for high-throughput flash sale scenarios with zero double-sell guarantee through Actor Model seat locking.

## Build & Run Commands

```bash
# Run in MVP mode (no external dependencies)
cd src/OpenTicket.Api
dotnet run

# Run with infrastructure (Phase 1+)
docker-compose up -d
dotnet run --environment Production

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/OpenTicket.Domain.Tests

# Run single test
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"

# Build solution
dotnet build OpenTicket.sln

# Format code
dotnet format OpenTicket.sln
```

## Architecture

### Layer Structure (Clean Architecture)
```
OpenTicket.Api            → Entry point (depends on Application + Infrastructure)
OpenTicket.Application    → Use cases, services (depends on Domain + Abstractions)
OpenTicket.Infrastructure → Implementations (depends on Abstractions only)
OpenTicket.Domain         → Pure domain models (NO external dependencies)
OpenTicket.Abstractions   → Core interfaces (NO external dependencies)
```

### Bounded Contexts (DDD)
- **Ticket** (Core): Seat lock/release with Actor Model partitioning - prevents double-sell
- **Order**: Order lifecycle with state machine (Created → PaymentPending → Completed/Expired)
- **Payment**: Pluggable providers (IPaymentProvider) - Mock for MVP, Stripe for production
- **Identity**: Pluggable providers (IIdentityProvider) - Mock for MVP, OAuth for production
- **Venue/Event**: CRUD with seat map schema
- **Queue**: Rate limiting, bot detection
- **Notification**: SignalR real-time updates

### Key Design Patterns
- **Actor Model**: Ticket Service uses single-threaded partition processing via `Channel<T>` to eliminate race conditions
- **Partition Strategy**: `Hash(SessionId + AreaId) % NumPartitions` for seat locks
- **Abstraction-First**: All external dependencies behind interfaces for testability and MVP/Production switching
- **Event-Driven**: Cross-context communication via NATS JetStream events

### Infrastructure Mode Toggle
Set `Infrastructure:Mode` in appsettings.json:
- `"MVP"`: Uses InMemory implementations (no external dependencies)
- `"Production"`: Uses PostgreSQL, Redis, NATS, SignalR

---

## Git Flow

### Branch Strategy
```
main                          # Production-ready code, protected
  └── develop                 # Integration branch for features
        ├── feature/TKT-001-seat-lock
        ├── feature/TKT-002-order-service
        ├── bugfix/TKT-003-lock-timeout
        └── hotfix/TKT-004-critical-fix
```

### Branch Naming Convention
```
feature/{ticket-id}-{short-description}   # New features
bugfix/{ticket-id}-{short-description}    # Bug fixes
hotfix/{ticket-id}-{short-description}    # Production hotfixes (branch from main)
release/{version}                         # Release preparation
```

### Workflow Rules
1. **Never commit directly to `main` or `develop`**
2. **All changes require Pull Request with at least 1 approval**
3. **Feature branches**: Branch from `develop`, merge back to `develop`
4. **Hotfix branches**: Branch from `main`, merge to both `main` and `develop`
5. **Squash merge** for feature branches to keep history clean
6. **Delete branch** after merge

---

## Commit Style

### Commit Message Format
```
type(scope): subject

[optional body]

[optional footer]
```

### Types
| Type | Description |
|------|-------------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Code style (formatting, semicolons, etc.) |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `perf` | Performance improvement |
| `test` | Adding or updating tests |
| `chore` | Build process, dependencies, tooling |
| `ci` | CI/CD configuration |
| `revert` | Revert a previous commit |

### Scopes
```
venue, event, ticket, order, payment, identity, queue, notification, infra, api, docs
```

### Rules
- **Subject**: Imperative mood, lowercase, no period, max 50 chars
- **Body**: Explain *what* and *why*, not *how*. Wrap at 72 chars
- **Footer**: Reference issues (e.g., `Closes #123`, `Refs #456`)

### Examples
```
feat(ticket): implement seat lock with TTL expiration

Add Redis-backed seat lock service with configurable TTL.
Uses SET NX EX for atomic lock acquisition.

Closes #42

---

fix(order): correct payment timeout calculation

The order expiration was using local time instead of UTC,
causing premature expiration in different timezones.

Refs #58

---

refactor(payment): extract provider factory pattern
```

---

## Coding Style

### C# Conventions

#### Naming
```csharp
// Namespaces: OpenTicket.{Layer}.{Feature}
namespace OpenTicket.Application.Tickets;

// Interfaces: I prefix
public interface ITicketService { }

// Classes: PascalCase, no prefix
public class TicketService : ITicketService { }

// Async methods: Async suffix
public Task<SeatLockResult> LockSeatAsync(SeatLockRequest request, CancellationToken ct = default);

// Private fields: _camelCase
private readonly ISeatLockService _seatLockService;

// Constants: PascalCase
public const int DefaultLockTtlSeconds = 120;

// Events: Event suffix
public record SeatLockedEvent : IIntegrationEvent { }

// DTOs: Request/Response/Result suffix
public record SeatLockRequest { }
public record SeatLockResult { }
```

#### File Organization
```csharp
// Order: usings → namespace → type
using System;
using OpenTicket.Domain.Common;

namespace OpenTicket.Domain.Tickets;

public class Ticket : Entity<Guid>
{
    // Order within class:
    // 1. Constants
    // 2. Static fields
    // 3. Instance fields
    // 4. Constructors
    // 5. Properties
    // 6. Public methods
    // 7. Private methods
}
```

#### Domain Model Rules
```csharp
// Entities: Private setters, mutation through methods only
public class Order : Entity<Guid>
{
    private Order() { } // EF Core constructor

    public static Order Create(Guid userId, Guid sessionId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Status = OrderStatus.Created,
        CreatedAt = DateTime.UtcNow
    };

    public void MarkAsPaid(Guid paymentId)
    {
        if (Status != OrderStatus.PaymentPending)
            throw new DomainException("INVALID_STATE", "Order not pending payment");

        PaymentId = paymentId;
        Status = OrderStatus.Completed;
    }
}

// Value Objects: Immutable records
public record Money(decimal Amount, string Currency)
{
    public static Money TWD(decimal amount) => new(amount, "TWD");
    public Money Add(Money other) => Currency == other.Currency
        ? new(Amount + other.Amount, Currency)
        : throw new DomainException("CURRENCY_MISMATCH", "Cannot add different currencies");
}

// Domain Exceptions: Error code + message
public class DomainException : Exception
{
    public string Code { get; }
    public DomainException(string code, string message) : base(message) => Code = code;
}
```

#### Result Pattern (Application Layer)
```csharp
public record Result<T>
{
    public bool Success { get; init; }
    public T? Value { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static Result<T> Ok(T value) => new() { Success = true, Value = value };
    public static Result<T> Fail(string code, string message) =>
        new() { Success = false, ErrorCode = code, ErrorMessage = message };
}
```

#### Async/Await
```csharp
// Always pass CancellationToken
public async Task<Result> ProcessAsync(Request request, CancellationToken ct = default)
{
    var data = await _repository.GetByIdAsync(request.Id, ct);
    // ...
}

// Use ConfigureAwait(false) in library code (Infrastructure)
public async Task<T> GetAsync(CancellationToken ct = default)
{
    return await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
}
```

#### Dependency Injection
```csharp
// Constructor injection, readonly fields
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IEventBus eventBus,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _eventBus = eventBus;
        _logger = logger;
    }
}
```

---

## Documentation Style

### Code Comments
```csharp
// Single-line: Explain WHY, not WHAT
// Lock expires after TTL to prevent orphaned locks from bot failures
var lockTtl = TimeSpan.FromSeconds(120);

/// <summary>
/// XML docs for public APIs only. Keep concise.
/// </summary>
/// <param name="sessionId">The session to lock seats for.</param>
/// <param name="seatIds">Seat identifiers to lock.</param>
/// <returns>Lock result with expiration time if successful.</returns>
/// <exception cref="DomainException">Thrown when seats are unavailable.</exception>
public Task<SeatLockResult> LockSeatsAsync(Guid sessionId, IEnumerable<string> seatIds);
```

### Markdown Documentation
- **Language**: English for code/API docs, Traditional Chinese (繁體中文) for PRD/business docs
- **Headers**: Use ATX style (`#`, `##`, `###`)
- **Code blocks**: Always specify language (```csharp, ```json, ```bash)
- **Tables**: Use for structured data, align pipes

### API Documentation (OpenAPI/Swagger)
```csharp
/// <summary>
/// Lock seats for purchase
/// </summary>
/// <remarks>
/// Seats are locked for 120 seconds. Must complete order before expiration.
/// </remarks>
/// <response code="200">Seats locked successfully</response>
/// <response code="409">One or more seats unavailable</response>
[HttpPost("{sessionId}/seats/lock")]
[ProducesResponseType(typeof(SeatLockResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
public async Task<IActionResult> LockSeats(Guid sessionId, [FromBody] LockSeatsRequest request)
```

### Changelog (CHANGELOG.md)
```markdown
## [1.2.0] - 2025-01-25

### Added
- Seat lock TTL configuration (#42)

### Changed
- Order expiration now uses UTC consistently (#58)

### Fixed
- Race condition in concurrent seat locking (#61)
```

---

## Testing Conventions

### Test Naming
```csharp
// Pattern: MethodName_StateUnderTest_ExpectedBehavior
[Fact]
public void LockSeat_WhenSeatAvailable_ReturnsSuccess()

[Fact]
public async Task CreateOrder_WithExpiredLock_ThrowsDomainException()

[Theory]
[InlineData(0)]
[InlineData(-1)]
public void SetPrice_WithInvalidAmount_ThrowsArgumentException(decimal amount)
```

### Test Structure (AAA Pattern)
```csharp
[Fact]
public async Task LockSeat_WhenAvailable_ShouldSucceed()
{
    // Arrange
    var service = new SeatLockService(_mockEventBus.Object);
    var request = new SeatLockRequest { SessionId = _sessionId, SeatId = "A-001" };

    // Act
    var result = await service.LockSeatAsync(request);

    // Assert
    Assert.True(result.Success);
    Assert.NotNull(result.LockId);
}
```

### Test Data Builders
```csharp
var venue = new VenueBuilder()
    .WithName("Concert Hall")
    .WithArea("A", seatCount: 100)
    .Build();
```

---

## Critical Invariants

1. **Zero Double-Sell**: Same seat can only be locked by one user at a time
2. **Lock TTL**: All seat locks have expiration (default 120 seconds)
3. **Single-threaded partition processing**: Never parallelize within a partition
4. **Domain layer purity**: No external dependencies in Domain project
5. **UTC everywhere**: All DateTime values must be UTC
6. **CancellationToken propagation**: Pass through all async call chains
