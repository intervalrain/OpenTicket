# OpenTicket - Development Guide & Roadmap

**Version:** 1.0
**Last Updated:** 2025-01

---

## 1. Development Philosophy

### 1.1 Core Principles

```
┌─────────────────────────────────────────────────────────────────┐
│                   Development Philosophy                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. Simple to Complex                                           │
│     MVP First, increase complexity gradually                    │
│                                                                 │
│  2. Abstraction First                                           │
│     Interface first, then implementation                        │
│                                                                 │
│  3. Vertical Slice                                              │
│     Every phase should commit complete executable function      │
│                                                                 │
│  4. Continuous Integration                                      │
│     Every commitment can be deployed                            │
│                                                                 │
│  5. Test-Driven                                                 │
│     Test first, then implementation                             │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 MVP First Approach

```
Phase 0 (MVP)                Phase 1                    Phase 2
┌─────────────────┐         ┌─────────────────┐        ┌─────────────────┐
│                 │         │                 │        │                 │
│  ✓ In-Memory    │   ───►  │  ✓ PostgreSQL   │  ───►  │  ✓ Multi-Region │
│  ✓ Mock Payment │         │  ✓ Redis Cache  │        │  ✓ Real Payment │
│  ✓ Mock Auth    │         │  ✓ NATS         │        │  ✓ OAuth        │
│  ✓ Single Node  │         │  ✓ SignalR      │        │  ✓ Advanced Bot │
│                 │         │                 │        │                 │
│ Complete Ticket │         │     Staging     │        │  Production     │
│ Flow            │         │                 │        │                 │
└─────────────────┘         └─────────────────┘        └─────────────────┘
```

---

## 2. Project Structure

### 2.1 Solution Structure

```
OpenTicket/
├── docs/                           # 文件
│   ├── PRD.md
│   ├── architecture/
│   ├── domain/
│   ├── api/
│   └── development/
│
├── src/
│   ├── OpenTicket.Abstractions/    # 核心抽象介面
│   │   ├── Messaging/
│   │   ├── Persistence/
│   │   ├── Payment/
│   │   ├── Identity/
│   │   ├── Ticketing/
│   │   └── Notification/
│   │
│   ├── OpenTicket.Domain/          # 領域模型 (Pure Domain)
│   │   ├── Venues/
│   │   ├── Events/
│   │   ├── Tickets/
│   │   ├── Orders/
│   │   ├── Payments/
│   │   └── Common/
│   │
│   ├── OpenTicket.Application/     # 應用服務層
│   │   ├── Venues/
│   │   ├── Events/
│   │   ├── Tickets/
│   │   ├── Orders/
│   │   └── Common/
│   │
│   ├── OpenTicket.Infrastructure/  # 基礎設施實作
│   │   ├── InMemory/               # MVP In-Memory 實作
│   │   ├── Persistence/            # EF Core, Redis
│   │   ├── Messaging/              # NATS
│   │   ├── Payment/                # Stripe, LinePay
│   │   ├── Identity/               # Google, Apple OAuth
│   │   └── Notification/           # SignalR
│   │
│   ├── OpenTicket.Api/             # API 入口點
│   │   ├── Controllers/
│   │   ├── Hubs/                   # SignalR Hubs
│   │   ├── Middleware/
│   │   └── Program.cs
│   │
│   └── OpenTicket.Worker/          # 背景服務 (Optional)
│       ├── SeatLockWorker/
│       └── NotificationWorker/
│
├── tests/
│   ├── OpenTicket.Domain.Tests/
│   ├── OpenTicket.Application.Tests/
│   ├── OpenTicket.Infrastructure.Tests/
│   ├── OpenTicket.Api.Tests/
│   └── OpenTicket.IntegrationTests/
│
├── docker/
│   ├── docker-compose.yml          # 本地開發環境
│   ├── docker-compose.prod.yml
│   └── Dockerfile
│
├── scripts/
│   ├── setup-dev.sh
│   └── run-tests.sh
│
├── OpenTicket.sln
└── README.md
```

### 2.2 Project Dependencies

```
┌─────────────────────────────────────────────────────────────────┐
│                     Dependency Graph                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│                    ┌─────────────────────┐                      │
│                    │   OpenTicket.Api    │                      │
│                    └──────────┬──────────┘                      │
│                               │                                 │
│              ┌────────────────┼────────────────┐                │
│              │                │                │                │
│              ▼                ▼                ▼                │
│  ┌───────────────────┐ ┌───────────────┐ ┌──────────────────┐   │
│  │   Application     │ │Infrastructure │ │     Worker       │   │
│  └─────────┬─────────┘ └───────┬───────┘ └────────┬─────────┘   │
│            │                   │                  │             │
│            │     ┌─────────────┼──────────────────┘             │
│            │     │             │                                │
│            ▼     ▼             ▼                                │
│  ┌───────────────────┐ ┌───────────────────┐                    │
│  │      Domain       │ │   Abstractions    │                    │
│  │  (No dependencies)│ │  (No dependencies)│                    │
│  └───────────────────┘ └───────────────────┘                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

Dependency Rules:
- Domain has NO external dependencies (Pure C#)
- Abstractions has NO external dependencies (Pure C#)
- Application depends on Domain + Abstractions
- Infrastructure depends on Abstractions (implements interfaces)
- Api depends on Application + Infrastructure
```

---

## 3. Development Roadmap

### 3.1 Phase 0: MVP Foundation (Week 1-4)

**Goal:** 建立完整架構，可在 In-Memory 模式下完成購票流程

#### Week 1: Project Setup & Domain

| Task | Description | Deliverable |
|------|-------------|-------------|
| 1.1 | 建立 Solution 結構 | `.sln` + 所有 Projects |
| 1.2 | 定義 Abstractions | 所有核心 Interface |
| 1.3 | Domain: Venue | `Venue`, `SeatMap`, `Area`, `Seat` |
| 1.4 | Domain: Event | `Event`, `Session`, `PriceCategory` |
| 1.5 | Domain: Ticket | `SeatInventory`, `Ticket` |
| 1.6 | Domain: Order | `Order`, `OrderItem` |

```csharp
// Deliverable: Domain Models
public class Venue : AggregateRoot<Guid> { ... }
public class Event : AggregateRoot<Guid> { ... }
public class SeatInventory : AggregateRoot<(Guid SessionId, string AreaId)> { ... }
public class Order : AggregateRoot<Guid> { ... }
```

#### Week 2: In-Memory Infrastructure

| Task | Description | Deliverable |
|------|-------------|-------------|
| 2.1 | InMemoryEventBus | `IEventBus` 實作 |
| 2.2 | InMemoryRepository | `IRepository<T,TId>` 實作 |
| 2.3 | InMemorySeatLockService | `ISeatLockService` 實作 |
| 2.4 | MockPaymentProvider | `IPaymentProvider` 實作 |
| 2.5 | MockIdentityProvider | `IIdentityProvider` 實作 |

```csharp
// Deliverable: DI Registration
services.AddMvpInfrastructure(); // All In-Memory implementations
```

#### Week 3: Application Services

| Task | Description | Deliverable |
|------|-------------|-------------|
| 3.1 | VenueService | CRUD + SeatMap 管理 |
| 3.2 | EventService | Event/Session CRUD |
| 3.3 | TicketService | Seat Lock/Release/Confirm |
| 3.4 | OrderService | Order Lifecycle |
| 3.5 | Event Handlers | 跨 Context 事件處理 |

```csharp
// Deliverable: Application Services
public class TicketService : ITicketService
{
    public Task<SeatLockResult> LockSeatAsync(SeatLockRequest request);
    public Task<OrderResult> CreateOrderAsync(CreateOrderRequest request);
}
```

#### Week 4: API & Integration

| Task | Description | Deliverable |
|------|-------------|-------------|
| 4.1 | REST API Endpoints | Venue, Event, Ticket, Order APIs |
| 4.2 | API Error Handling | Global exception handler |
| 4.3 | Integration Tests | 完整購票流程測試 |
| 4.4 | API Documentation | Swagger/OpenAPI |

```
Deliverable: Working MVP
POST /api/seats/lock       → Lock seat
POST /api/orders           → Create order
GET  /api/orders/{id}      → Get order status
```

**Phase 0 Completion Criteria:**
- [ ] 可執行完整購票流程（選座→鎖定→下單→付款→出票）
- [ ] 所有 In-Memory 實作通過單元測試
- [ ] Integration Test 覆蓋 Happy Path
- [ ] Swagger 文件可用

---

### 3.2 Phase 1: Production Infrastructure (Week 5-10)

**Goal:** 替換為 Production-ready 基礎設施

#### Week 5-6: Database & Persistence

| Task | Description | Deliverable |
|------|-------------|-------------|
| 5.1 | PostgreSQL Schema | Migration scripts |
| 5.2 | EF Core DbContext | `OpenTicketDbContext` |
| 5.3 | EfCoreRepository | `IRepository` 實作 |
| 5.4 | Redis SeatLockService | `ISeatLockService` 實作 |

```yaml
# docker-compose.yml
services:
  postgres:
    image: postgres:16
  redis:
    image: redis:7
```

#### Week 7-8: Messaging & Real-time

| Task | Description | Deliverable |
|------|-------------|-------------|
| 7.1 | NATS JetStream Setup | Docker + Config |
| 7.2 | NatsEventBus | `IEventBus` 實作 |
| 7.3 | SignalR Hub | `TicketHub` |
| 7.4 | SignalR Notification | `INotificationService` 實作 |

```csharp
// Deliverable: Real-time updates
public class TicketHub : Hub
{
    public async Task JoinSession(Guid sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}");
    }
}
```

#### Week 9-10: Queue Gate & Rate Limiting

| Task | Description | Deliverable |
|------|-------------|-------------|
| 9.1 | Rate Limiting Middleware | Redis-backed limiter |
| 9.2 | Queue Gate Service | Virtual queue |
| 9.3 | Basic Bot Detection | Request pattern analysis |
| 9.4 | Load Testing | k6 / Artillery scripts |

```csharp
// Deliverable: Rate Limiting
app.UseRateLimiter(new RateLimiterOptions
{
    GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString(),
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6
            }))
});
```

**Phase 1 Completion Criteria:**
- [ ] PostgreSQL + Redis 運作正常
- [ ] NATS JetStream 事件傳遞正常
- [ ] SignalR 即時座位更新
- [ ] Rate Limiting 防止過載
- [ ] Load Test: 1000 RPS without errors

---

### 3.3 Phase 2: Production Features (Week 11-16)

**Goal:** 真實支付、身份驗證、進階防護

#### Week 11-12: Payment Integration

| Task | Description | Deliverable |
|------|-------------|-------------|
| 11.1 | Stripe Provider | `StripePaymentProvider` |
| 11.2 | Webhook Handler | Payment status sync |
| 11.3 | Refund Flow | Refund implementation |
| 11.4 | Payment Tests | Integration tests |

#### Week 13-14: Identity Integration

| Task | Description | Deliverable |
|------|-------------|-------------|
| 13.1 | Google OAuth | `GoogleIdentityProvider` |
| 13.2 | Apple OAuth | `AppleIdentityProvider` |
| 13.3 | JWT Token Service | Token generation/validation |
| 13.4 | User Registration | Complete auth flow |

#### Week 15-16: Advanced Protection

| Task | Description | Deliverable |
|------|-------------|-------------|
| 15.1 | Behavior Analysis | Mouse/click pattern |
| 15.2 | Proof-of-Work | Client-side challenge |
| 15.3 | Advanced Queue Gate | Priority queue |
| 15.4 | Monitoring & Alerts | Prometheus + Grafana |

**Phase 2 Completion Criteria:**
- [ ] Stripe 真實支付可用
- [ ] Google/Apple OAuth 登入
- [ ] Bot 偵測率 > 90%
- [ ] 完整監控 Dashboard

---

### 3.4 Phase 3: Scale & Multi-Region (Week 17-24)

**Goal:** 全球部署、高可用

#### Week 17-20: Horizontal Scaling

| Task | Description |
|------|-------------|
| 17.1 | Ticket Service Partitioning |
| 17.2 | Order Service Sharding |
| 17.3 | Database Read Replicas |
| 17.4 | Kubernetes Deployment |

#### Week 21-24: Multi-Region

| Task | Description |
|------|-------------|
| 21.1 | NATS Leafnode Setup |
| 21.2 | Region-aware Routing |
| 21.3 | Global Event Replication |
| 21.4 | Chaos Engineering Tests |

---

## 4. Coding Standards

### 4.1 C# Conventions

```csharp
// File naming: PascalCase.cs
// Namespace: OpenTicket.{Layer}.{Feature}
namespace OpenTicket.Application.Tickets;

// Interface: I prefix
public interface ITicketService { }

// Implementation: No prefix
public class TicketService : ITicketService { }

// Async methods: Async suffix
public Task<SeatLockResult> LockSeatAsync(...)

// Event classes: Event suffix
public record SeatLockedEvent : IIntegrationEvent { }

// Request/Response: Request/Result suffix
public record SeatLockRequest { }
public record SeatLockResult { }
```

### 4.2 Domain Model Rules

```csharp
// 1. Entities are mutable, but only through methods
public class Order : Entity<Guid>
{
    private Order() { } // EF Core

    public static Order Create(Guid userId, Guid sessionId)
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionId = sessionId,
            Status = OrderStatus.Created,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsPaid(Guid paymentId)
    {
        if (Status != OrderStatus.PaymentPending)
            throw new InvalidOperationException("Order not pending payment");

        PaymentId = paymentId;
        Status = OrderStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }
}

// 2. Value Objects are immutable
public record Money(decimal Amount, string Currency)
{
    public static Money TWD(decimal amount) => new(amount, "TWD");
    public static Money USD(decimal amount) => new(amount, "USD");

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Currency mismatch");
        return new Money(Amount + other.Amount, Currency);
    }
}

// 3. Domain Events record state changes
public record OrderCreatedEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string CorrelationId { get; init; } = string.Empty;

    public required Guid OrderId { get; init; }
    public required Guid UserId { get; init; }
    public required Guid SessionId { get; init; }
    public required IReadOnlyList<string> SeatIds { get; init; }
    public required Money TotalAmount { get; init; }
}
```

### 4.3 Error Handling

```csharp
// 1. Domain Exceptions
public class DomainException : Exception
{
    public string Code { get; }
    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}

public class SeatAlreadyLockedException : DomainException
{
    public SeatAlreadyLockedException(string seatId)
        : base("SEAT_ALREADY_LOCKED", $"Seat {seatId} is already locked") { }
}

// 2. Result Pattern for Application Layer
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

// 3. Global Exception Handler in API
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        var (statusCode, errorCode, message) = exception switch
        {
            DomainException de => (400, de.Code, de.Message),
            UnauthorizedAccessException => (401, "UNAUTHORIZED", "Unauthorized"),
            _ => (500, "INTERNAL_ERROR", "An error occurred")
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new
        {
            error = errorCode,
            message = message
        });
    });
});
```

---

## 5. Testing Strategy

### 5.1 Test Pyramid

```
                    ┌─────────────┐
                    │    E2E      │  ← Few (Critical flows only)
                    │   Tests     │
                    └──────┬──────┘
                           │
                    ┌──────┴──────┐
                    │ Integration │  ← Some (API + DB)
                    │   Tests     │
                    └──────┬──────┘
                           │
              ┌────────────┴────────────┐
              │       Unit Tests        │  ← Many (Domain + App)
              └─────────────────────────┘
```

### 5.2 Test Categories

```csharp
// Unit Tests (Domain + Application)
[Fact]
public void Order_WhenCreated_ShouldHaveCorrectStatus()
{
    var order = Order.Create(userId, sessionId);
    Assert.Equal(OrderStatus.Created, order.Status);
}

// Integration Tests (With In-Memory Infrastructure)
[Fact]
public async Task LockSeat_WhenAvailable_ShouldSucceed()
{
    await using var fixture = new TestFixture();
    var result = await fixture.TicketService.LockSeatAsync(request);
    Assert.True(result.Success);
}

// API Tests (WebApplicationFactory)
[Fact]
public async Task POST_LockSeat_Returns200_WhenSuccessful()
{
    var client = _factory.CreateClient();
    var response = await client.PostAsJsonAsync("/api/seats/lock", request);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

### 5.3 Test Data Builders

```csharp
public class VenueBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Venue";
    private int _capacity = 1000;
    private List<Area> _areas = new();

    public VenueBuilder WithId(Guid id) { _id = id; return this; }
    public VenueBuilder WithName(string name) { _name = name; return this; }
    public VenueBuilder WithArea(string areaId, int seatCount)
    {
        _areas.Add(new Area
        {
            AreaId = areaId,
            Seats = Enumerable.Range(1, seatCount)
                .Select(i => new SeatDefinition($"{areaId}-{i:D3}", ...))
                .ToList()
        });
        return this;
    }

    public Venue Build() => new Venue(_id, _name, _capacity, _areas);
}

// Usage
var venue = new VenueBuilder()
    .WithName("Concert Hall")
    .WithArea("A", 100)
    .WithArea("B", 200)
    .Build();
```

---

## 6. Local Development Setup

### 6.1 Prerequisites

```bash
# Required
- .NET 9 SDK
- Docker Desktop
- Git

# Recommended
- JetBrains Rider / VS Code
- Postman / Insomnia
- k6 (load testing)
```

### 6.2 Quick Start

```bash
# 1. Clone repository
git clone https://github.com/your-org/openticket.git
cd openticket

# 2. Start dependencies (Phase 1+)
docker-compose up -d

# 3. Run in MVP mode (no external dependencies)
cd src/OpenTicket.Api
dotnet run --configuration Debug --environment Development

# 4. Run tests
dotnet test

# 5. Access
# API: http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

### 6.3 docker-compose.yml

```yaml
version: '3.8'

services:
  # Phase 1+ Dependencies
  postgres:
    image: postgres:16
    environment:
      POSTGRES_USER: openticket
      POSTGRES_PASSWORD: openticket
      POSTGRES_DB: openticket
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7
    ports:
      - "6379:6379"

  nats:
    image: nats:2.10
    command: ["--jetstream", "--store_dir=/data"]
    ports:
      - "4222:4222"
      - "8222:8222"  # Monitoring
    volumes:
      - nats_data:/data

  # Optional: Monitoring
  prometheus:
    image: prom/prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./docker/prometheus.yml:/etc/prometheus/prometheus.yml

  grafana:
    image: grafana/grafana
    ports:
      - "3000:3000"

volumes:
  postgres_data:
  nats_data:
```

### 6.4 Environment Configuration

```json
// appsettings.Development.json
{
  "Infrastructure": {
    "Mode": "MVP"  // Change to "Production" when ready
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}

// appsettings.Production.json
{
  "Infrastructure": {
    "Mode": "Production",
    "Database": {
      "ConnectionString": "Host=postgres;Database=openticket;..."
    },
    "Redis": {
      "ConnectionString": "redis:6379"
    },
    "NATS": {
      "Url": "nats://nats:4222"
    }
  }
}
```

---

## 7. Definition of Done

### 7.1 Feature Checklist

每個功能完成前必須滿足：

- [ ] **Unit Tests**: Domain + Application 層測試通過
- [ ] **Integration Tests**: API 測試通過
- [ ] **Code Review**: 至少一人 Review
- [ ] **Documentation**: API 文件更新
- [ ] **No Regressions**: 既有測試全部通過

### 7.2 Phase Completion Criteria

| Phase | Criteria |
|-------|----------|
| **Phase 0** | MVP 完整購票流程可執行 |
| **Phase 1** | 可部署到 Staging 環境 |
| **Phase 2** | 可部署到 Production（限定流量）|
| **Phase 3** | 可支援全球部署與高流量 |

---

## 8. API Quick Reference

### 8.1 MVP API Endpoints

```
# Venue
GET    /api/venues                    # List venues
GET    /api/venues/{id}               # Get venue with seat map
POST   /api/venues                    # Create venue (Admin)

# Event
GET    /api/events                    # List events
GET    /api/events/{id}               # Get event with sessions
POST   /api/events                    # Create event (Organizer)

# Session (Tickets)
GET    /api/sessions/{id}/seats       # Get all seat status
POST   /api/sessions/{id}/seats/lock  # Lock seat
DELETE /api/sessions/{id}/seats/lock  # Release seat

# Order
POST   /api/orders                    # Create order
GET    /api/orders/{id}               # Get order status
DELETE /api/orders/{id}               # Cancel order

# SignalR Hub
/hubs/tickets                         # Real-time seat updates
  - JoinSession(sessionId)
  - LeaveSession(sessionId)
  - OnSeatStatusChanged(seatId, status)
```

### 8.2 Request/Response Examples

```json
// POST /api/sessions/{sessionId}/seats/lock
// Request
{
  "seatId": "A1-001"
}

// Response (Success)
{
  "success": true,
  "lockId": "abc123",
  "expiresAt": "2025-01-20T10:05:00Z"
}

// Response (Failure)
{
  "success": false,
  "errorCode": "SEAT_ALREADY_LOCKED",
  "errorMessage": "Seat A1-001 is already locked by another user"
}
```

```json
// POST /api/orders
// Request
{
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "seatIds": ["A1-001", "A1-002"]
}

// Response
{
  "orderId": "660e8400-e29b-41d4-a716-446655440001",
  "status": "Created",
  "totalAmount": {
    "amount": 6400,
    "currency": "TWD"
  },
  "expiresAt": "2025-01-20T10:10:00Z",
  "paymentUrl": "http://localhost/mock-payment/..."
}
```

---

## 9. Contribution Guidelines

### 9.1 Branch Strategy

```
main
  └── develop
        ├── feature/TKT-001-seat-lock
        ├── feature/TKT-002-order-service
        └── bugfix/TKT-003-lock-timeout
```

### 9.2 Commit Message Format

```
type(scope): subject

[optional body]

[optional footer]

Types: feat, fix, docs, style, refactor, test, chore
Scope: venue, event, ticket, order, payment, identity, infra
```

Examples:
```
feat(ticket): implement seat lock service
fix(order): correct expiration time calculation
docs(api): update swagger annotations
test(ticket): add integration tests for lock timeout
```

### 9.3 Pull Request Template

```markdown
## Summary
[Brief description of changes]

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Checklist
- [ ] Unit tests added/updated
- [ ] Integration tests pass
- [ ] Documentation updated
- [ ] No new warnings
```
