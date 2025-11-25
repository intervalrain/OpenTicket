# OpenTicket - Abstraction Layers Design

**Version:** 1.0
**Last Updated:** 2025-01

---

## 1. Abstraction Philosophy

### 1.1 Core Principles

```
┌─────────────────────────────────────────────────────────────────┐
│                   Abstraction Principles                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. Interface First                                             │
│     - Define interface → Implement Mock → Implement Production  │
│     - Business Logic does not depend on implementation          │
│                                                                 │
│  2. Dependency Inversion                                        │
│     - High-level module does not depend on low-level module     │
│     - All modules depends on abstractions                       │
│                                                                 │
│  3. Progressive Enhancement                                     │
│     - MVP: In-Memory / Mock Implementation                      │
│     - Production: Real service implementation                   │
│     - No need to modify Business Logic                          │
│                                                                 │    
│  4. Testability                                                 │
│     - All external dependency can be mocked                     │
│     - Unit test does not need real infrastructure               │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 Layer Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Presentation Layer                         │
│                (API Controllers, SignalR Hubs)                  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Application Layer                          │
│              (Use Cases, Command/Query Handlers)                │
│                                                                 │
│   ITicketService, IOrderService, IPaymentService, etc.          │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                       Domain Layer                              │
│            (Entities, Value Objects, Domain Services)           │
│                                                                 │
│   Seat, Order, Ticket, Payment (Pure Domain Logic)              │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Infrastructure Layer                         │
│           (Repositories, External Services, Messaging)          │
│                                                                 │
│   ISeatRepository, IPaymentProvider, IEventBus, etc.            │
│                                                                 │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐             │
│   │  In-Memory  │  │    Mock     │  │ Production  │             │
│   │   Impl      │  │    Impl     │  │    Impl     │             │
│   └─────────────┘  └─────────────┘  └─────────────┘             │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. Core Abstractions

### 2.1 Event Bus Abstraction

**Purpose:** 跨 Context 通訊的統一介面

```csharp
// OpenTicket.Abstractions/Messaging/IEventBus.cs
namespace OpenTicket.Abstractions.Messaging;

/// <summary>
/// 事件匯流排抽象介面
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 發布整合事件
    /// </summary>
    Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken ct = default)
        where TEvent : IIntegrationEvent;

    /// <summary>
    /// 訂閱整合事件
    /// </summary>
    Task SubscribeAsync<TEvent>(
        Func<TEvent, CancellationToken, Task> handler,
        SubscriptionOptions? options = null,
        CancellationToken ct = default)
        where TEvent : IIntegrationEvent;

    /// <summary>
    /// 發布並等待回應 (Request-Reply)
    /// </summary>
    Task<TResponse> RequestAsync<TRequest, TResponse>(
        TRequest request,
        TimeSpan timeout,
        CancellationToken ct = default)
        where TRequest : IRequest<TResponse>;
}

/// <summary>
/// 整合事件標記介面
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
    string CorrelationId { get; }
}

/// <summary>
/// 訂閱選項
/// </summary>
public record SubscriptionOptions
{
    public string? QueueGroup { get; init; }
    public bool Durable { get; init; } = true;
    public int MaxConcurrent { get; init; } = 1;
}
```

**Implementations:**

```csharp
// In-Memory (MVP / Testing)
public class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        if (_handlers.TryGetValue(typeof(TEvent), out var handlers))
        {
            foreach (var handler in handlers)
            {
                var typedHandler = (Func<TEvent, CancellationToken, Task>)handler;
                _ = typedHandler(@event, ct); // Fire and forget for in-memory
            }
        }
        return Task.CompletedTask;
    }

    public Task SubscribeAsync<TEvent>(
        Func<TEvent, CancellationToken, Task> handler,
        SubscriptionOptions? options = null,
        CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        _handlers.AddOrUpdate(
            typeof(TEvent),
            _ => new List<Delegate> { handler },
            (_, list) => { list.Add(handler); return list; });
        return Task.CompletedTask;
    }

    public Task<TResponse> RequestAsync<TRequest, TResponse>(
        TRequest request, TimeSpan timeout, CancellationToken ct = default)
        where TRequest : IRequest<TResponse>
    {
        throw new NotSupportedException("Request-Reply not supported in InMemory mode");
    }
}

// NATS JetStream (Production)
public class NatsEventBus : IEventBus
{
    private readonly INatsConnection _connection;
    private readonly INatsJSContext _jetStream;

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        var subject = GetSubject<TEvent>(@event);
        var data = JsonSerializer.SerializeToUtf8Bytes(@event);
        await _jetStream.PublishAsync(subject, data, cancellationToken: ct);
    }

    public async Task SubscribeAsync<TEvent>(
        Func<TEvent, CancellationToken, Task> handler,
        SubscriptionOptions? options = null,
        CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        var stream = GetStreamName<TEvent>();
        var consumer = await _jetStream.CreateOrUpdateConsumerAsync(
            stream,
            new ConsumerConfig { DurableName = options?.QueueGroup },
            ct);

        await foreach (var msg in consumer.ConsumeAsync<byte[]>(cancellationToken: ct))
        {
            var @event = JsonSerializer.Deserialize<TEvent>(msg.Data);
            if (@event != null)
            {
                await handler(@event, ct);
                await msg.AckAsync(cancellationToken: ct);
            }
        }
    }
}
```

### 2.2 Repository Abstraction

**Purpose:** 資料存取的統一介面

```csharp
// OpenTicket.Abstractions/Persistence/IRepository.cs
namespace OpenTicket.Abstractions.Persistence;

/// <summary>
/// 通用 Repository 介面
/// </summary>
public interface IRepository<TEntity, TId> where TEntity : Entity<TId>
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(TId id, CancellationToken ct = default);
    Task<bool> ExistsAsync(TId id, CancellationToken ct = default);
}

/// <summary>
/// 可查詢 Repository 介面
/// </summary>
public interface IQueryableRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : Entity<TId>
{
    Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default);

    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default);

    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default);
}

/// <summary>
/// 基礎 Entity
/// </summary>
public abstract class Entity<TId>
{
    public TId Id { get; protected set; } = default!;
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
}
```

**Implementations:**

```csharp
// In-Memory Repository (MVP / Testing)
public class InMemoryRepository<TEntity, TId> : IQueryableRepository<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : notnull
{
    private readonly ConcurrentDictionary<TId, TEntity> _store = new();

    public Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<TEntity>>(_store.Values.ToList());
    }

    public Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        if (!_store.TryAdd(entity.Id, entity))
            throw new InvalidOperationException($"Entity with Id {entity.Id} already exists");
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        _store[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TId id, CancellationToken ct = default)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(TId id, CancellationToken ct = default)
    {
        return Task.FromResult(_store.ContainsKey(id));
    }

    public Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        var compiled = predicate.Compile();
        return Task.FromResult<IReadOnlyList<TEntity>>(
            _store.Values.Where(compiled).ToList());
    }

    public Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        var compiled = predicate.Compile();
        return Task.FromResult(_store.Values.FirstOrDefault(compiled));
    }

    public Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default)
    {
        if (predicate == null)
            return Task.FromResult(_store.Count);
        var compiled = predicate.Compile();
        return Task.FromResult(_store.Values.Count(compiled));
    }
}

// EF Core Repository (Production)
public class EfCoreRepository<TEntity, TId> : IQueryableRepository<TEntity, TId>
    where TEntity : Entity<TId>
{
    private readonly DbContext _context;
    private readonly DbSet<TEntity> _dbSet;

    public EfCoreRepository(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync(new object[] { id! }, ct);
    }

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet.ToListAsync(ct);
    }

    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
    }

    // ... other implementations
}
```

### 2.3 Payment Provider Abstraction

**Purpose:** 支付服務的可插拔介面

```csharp
// OpenTicket.Abstractions/Payment/IPaymentProvider.cs
namespace OpenTicket.Abstractions.Payment;

/// <summary>
/// 支付提供者介面
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// 提供者名稱
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 支援的支付方式
    /// </summary>
    IReadOnlyList<PaymentMethod> SupportedMethods { get; }

    /// <summary>
    /// 發起支付
    /// </summary>
    Task<PaymentInitiationResult> InitiatePaymentAsync(
        PaymentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// 驗證支付狀態
    /// </summary>
    Task<PaymentVerificationResult> VerifyPaymentAsync(
        string transactionId,
        CancellationToken ct = default);

    /// <summary>
    /// 處理 Webhook 回調
    /// </summary>
    Task<WebhookProcessResult> ProcessWebhookAsync(
        string payload,
        IDictionary<string, string> headers,
        CancellationToken ct = default);

    /// <summary>
    /// 退款
    /// </summary>
    Task<RefundResult> RefundAsync(
        RefundRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// 支付請求
/// </summary>
public record PaymentRequest
{
    public required Guid OrderId { get; init; }
    public required Money Amount { get; init; }
    public required string Description { get; init; }
    public required string ReturnUrl { get; init; }
    public required string WebhookUrl { get; init; }
    public PaymentMethod? PreferredMethod { get; init; }
    public IDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// 支付發起結果
/// </summary>
public record PaymentInitiationResult
{
    public required bool Success { get; init; }
    public string? TransactionId { get; init; }
    public string? PaymentUrl { get; init; }  // 跳轉支付頁面
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 金額
/// </summary>
public record Money(decimal Amount, string Currency);

/// <summary>
/// 支付方式
/// </summary>
public enum PaymentMethod
{
    CreditCard,
    DebitCard,
    BankTransfer,
    LinePay,
    ApplePay,
    GooglePay,
    PayPal
}
```

**Implementations:**

```csharp
// Mock Payment Provider (MVP / Testing)
public class MockPaymentProvider : IPaymentProvider
{
    public string ProviderName => "Mock";
    public IReadOnlyList<PaymentMethod> SupportedMethods =>
        new[] { PaymentMethod.CreditCard };

    private readonly ConcurrentDictionary<string, PaymentStatus> _payments = new();

    public Task<PaymentInitiationResult> InitiatePaymentAsync(
        PaymentRequest request, CancellationToken ct = default)
    {
        var transactionId = Guid.NewGuid().ToString("N");
        _payments[transactionId] = PaymentStatus.Pending;

        // 模擬自動成功
        Task.Delay(1000, ct).ContinueWith(_ =>
            _payments[transactionId] = PaymentStatus.Succeeded);

        return Task.FromResult(new PaymentInitiationResult
        {
            Success = true,
            TransactionId = transactionId,
            PaymentUrl = $"http://localhost/mock-payment/{transactionId}"
        });
    }

    public Task<PaymentVerificationResult> VerifyPaymentAsync(
        string transactionId, CancellationToken ct = default)
    {
        var status = _payments.GetValueOrDefault(transactionId, PaymentStatus.Unknown);
        return Task.FromResult(new PaymentVerificationResult
        {
            TransactionId = transactionId,
            Status = status,
            Success = status == PaymentStatus.Succeeded
        });
    }

    public Task<WebhookProcessResult> ProcessWebhookAsync(
        string payload, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        // Mock implementation - parse JSON and update status
        return Task.FromResult(new WebhookProcessResult { Success = true });
    }

    public Task<RefundResult> RefundAsync(
        RefundRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(new RefundResult
        {
            Success = true,
            RefundId = Guid.NewGuid().ToString("N")
        });
    }
}

// Stripe Payment Provider (Production)
public class StripePaymentProvider : IPaymentProvider
{
    private readonly StripeClient _client;

    public string ProviderName => "Stripe";
    public IReadOnlyList<PaymentMethod> SupportedMethods =>
        new[] { PaymentMethod.CreditCard, PaymentMethod.ApplePay, PaymentMethod.GooglePay };

    public async Task<PaymentInitiationResult> InitiatePaymentAsync(
        PaymentRequest request, CancellationToken ct = default)
    {
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(request.Amount.Amount * 100),
                        Currency = request.Amount.Currency.ToLower(),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = request.Description
                        }
                    },
                    Quantity = 1
                }
            },
            Mode = "payment",
            SuccessUrl = request.ReturnUrl,
            CancelUrl = request.ReturnUrl,
            Metadata = new Dictionary<string, string>
            {
                ["order_id"] = request.OrderId.ToString()
            }
        };

        var service = new SessionService(_client);
        var session = await service.CreateAsync(options, cancellationToken: ct);

        return new PaymentInitiationResult
        {
            Success = true,
            TransactionId = session.Id,
            PaymentUrl = session.Url
        };
    }

    // ... other implementations
}
```

### 2.4 Identity Provider Abstraction

**Purpose:** 身份驗證的可插拔介面

```csharp
// OpenTicket.Abstractions/Identity/IIdentityProvider.cs
namespace OpenTicket.Abstractions.Identity;

/// <summary>
/// 身份驗證提供者介面
/// </summary>
public interface IIdentityProvider
{
    /// <summary>
    /// 提供者名稱
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 驗證身份
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// 取得用戶資訊
    /// </summary>
    Task<UserInfo?> GetUserInfoAsync(
        string providerUserId,
        CancellationToken ct = default);

    /// <summary>
    /// 驗證 Token
    /// </summary>
    Task<TokenValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken ct = default);
}

/// <summary>
/// 驗證請求
/// </summary>
public record AuthenticationRequest
{
    public required string ProviderName { get; init; }
    public string? Code { get; init; }           // OAuth Code
    public string? AccessToken { get; init; }    // OAuth Token
    public string? Email { get; init; }          // Email/Password
    public string? Password { get; init; }
    public string? PhoneNumber { get; init; }    // SMS OTP
    public string? OtpCode { get; init; }
    public string? RedirectUri { get; init; }
}

/// <summary>
/// 驗證結果
/// </summary>
public record AuthenticationResult
{
    public required bool Success { get; init; }
    public string? UserId { get; init; }
    public string? ProviderUserId { get; init; }
    public UserInfo? UserInfo { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 用戶資訊
/// </summary>
public record UserInfo
{
    public required string Id { get; init; }
    public string? Email { get; init; }
    public string? Name { get; init; }
    public string? Picture { get; init; }
    public bool EmailVerified { get; init; }
}
```

**Implementations:**

```csharp
// Mock Identity Provider (MVP / Testing)
public class MockIdentityProvider : IIdentityProvider
{
    public string ProviderName => "Mock";

    private readonly ConcurrentDictionary<string, UserInfo> _users = new();

    public Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request, CancellationToken ct = default)
    {
        // Mock: 任何 email 都能登入
        var userId = Guid.NewGuid().ToString();
        var userInfo = new UserInfo
        {
            Id = userId,
            Email = request.Email ?? "test@example.com",
            Name = "Test User",
            EmailVerified = true
        };

        _users[userId] = userInfo;

        return Task.FromResult(new AuthenticationResult
        {
            Success = true,
            UserId = userId,
            ProviderUserId = userId,
            UserInfo = userInfo,
            AccessToken = $"mock-token-{userId}"
        });
    }

    public Task<UserInfo?> GetUserInfoAsync(string providerUserId, CancellationToken ct = default)
    {
        _users.TryGetValue(providerUserId, out var userInfo);
        return Task.FromResult(userInfo);
    }

    public Task<TokenValidationResult> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        // Mock: 所有 token 都有效
        return Task.FromResult(new TokenValidationResult
        {
            IsValid = token.StartsWith("mock-token-"),
            UserId = token.Replace("mock-token-", "")
        });
    }
}

// Google OAuth Provider (Production)
public class GoogleIdentityProvider : IIdentityProvider
{
    private readonly GoogleAuthSettings _settings;
    private readonly HttpClient _httpClient;

    public string ProviderName => "Google";

    public async Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request, CancellationToken ct = default)
    {
        // Exchange code for tokens
        var tokenResponse = await ExchangeCodeForTokensAsync(
            request.Code!, request.RedirectUri!, ct);

        if (!tokenResponse.Success)
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorCode = "token_exchange_failed",
                ErrorMessage = tokenResponse.ErrorMessage
            };
        }

        // Get user info from Google
        var userInfo = await GetUserInfoFromGoogleAsync(tokenResponse.AccessToken!, ct);

        return new AuthenticationResult
        {
            Success = true,
            ProviderUserId = userInfo.Id,
            UserInfo = userInfo,
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken
        };
    }

    // ... other implementations
}
```

### 2.5 Seat Lock Service Abstraction

**Purpose:** 座位鎖定的核心服務抽象

```csharp
// OpenTicket.Abstractions/Ticketing/ISeatLockService.cs
namespace OpenTicket.Abstractions.Ticketing;

/// <summary>
/// 座位鎖定服務介面
/// </summary>
public interface ISeatLockService
{
    /// <summary>
    /// 嘗試鎖定座位
    /// </summary>
    Task<SeatLockResult> TryLockSeatAsync(
        SeatLockRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// 釋放座位
    /// </summary>
    Task<SeatReleaseResult> ReleaseSeatAsync(
        Guid sessionId,
        string seatId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// 確認座位（付款成功後）
    /// </summary>
    Task<SeatConfirmResult> ConfirmSeatAsync(
        Guid sessionId,
        string seatId,
        Guid orderId,
        CancellationToken ct = default);

    /// <summary>
    /// 取得座位狀態
    /// </summary>
    Task<SeatStatus> GetSeatStatusAsync(
        Guid sessionId,
        string seatId,
        CancellationToken ct = default);

    /// <summary>
    /// 批量取得座位狀態
    /// </summary>
    Task<IReadOnlyDictionary<string, SeatStatus>> GetSeatsStatusAsync(
        Guid sessionId,
        IEnumerable<string> seatIds,
        CancellationToken ct = default);
}

/// <summary>
/// 座位鎖定請求
/// </summary>
public record SeatLockRequest
{
    public required Guid SessionId { get; init; }
    public required string SeatId { get; init; }
    public required Guid UserId { get; init; }
    public TimeSpan? LockDuration { get; init; }  // Default: 120 seconds
}

/// <summary>
/// 座位鎖定結果
/// </summary>
public record SeatLockResult
{
    public required bool Success { get; init; }
    public string? LockId { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public SeatLockFailureReason? FailureReason { get; init; }
}

/// <summary>
/// 鎖定失敗原因
/// </summary>
public enum SeatLockFailureReason
{
    SeatNotFound,
    SeatUnavailable,
    SeatAlreadyLocked,
    SeatAlreadySold,
    UserLimitExceeded,
    SessionNotOnSale
}

/// <summary>
/// 座位狀態
/// </summary>
public record SeatStatus
{
    public required string SeatId { get; init; }
    public required SeatState State { get; init; }
    public Guid? LockedBy { get; init; }
    public DateTime? LockExpiresAt { get; init; }
}

public enum SeatState
{
    Available,
    Locked,
    Sold,
    Unavailable
}
```

**Implementations:**

```csharp
// In-Memory Seat Lock Service (MVP)
public class InMemorySeatLockService : ISeatLockService
{
    private readonly ConcurrentDictionary<string, SeatLockInfo> _locks = new();
    private readonly TimeSpan _defaultLockDuration = TimeSpan.FromSeconds(120);
    private readonly IEventBus _eventBus;

    public InMemorySeatLockService(IEventBus eventBus)
    {
        _eventBus = eventBus;
        StartExpirationChecker();
    }

    public async Task<SeatLockResult> TryLockSeatAsync(
        SeatLockRequest request, CancellationToken ct = default)
    {
        var key = GetKey(request.SessionId, request.SeatId);
        var lockDuration = request.LockDuration ?? _defaultLockDuration;
        var expiresAt = DateTime.UtcNow.Add(lockDuration);

        var lockInfo = new SeatLockInfo
        {
            SessionId = request.SessionId,
            SeatId = request.SeatId,
            UserId = request.UserId,
            LockedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        // Atomic check-and-set
        var added = _locks.TryAdd(key, lockInfo);

        if (!added)
        {
            var existing = _locks[key];
            if (existing.ExpiresAt < DateTime.UtcNow)
            {
                // Lock expired, try to replace
                if (_locks.TryUpdate(key, lockInfo, existing))
                {
                    added = true;
                }
            }
        }

        if (added)
        {
            await _eventBus.PublishAsync(new SeatLockedEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                SessionId = request.SessionId,
                SeatId = request.SeatId,
                UserId = request.UserId,
                ExpiresAt = expiresAt
            }, ct);

            return new SeatLockResult
            {
                Success = true,
                LockId = key,
                ExpiresAt = expiresAt
            };
        }

        return new SeatLockResult
        {
            Success = false,
            FailureReason = SeatLockFailureReason.SeatAlreadyLocked
        };
    }

    public async Task<SeatReleaseResult> ReleaseSeatAsync(
        Guid sessionId, string seatId, Guid userId, CancellationToken ct = default)
    {
        var key = GetKey(sessionId, seatId);

        if (_locks.TryGetValue(key, out var lockInfo))
        {
            if (lockInfo.UserId == userId)
            {
                if (_locks.TryRemove(key, out _))
                {
                    await _eventBus.PublishAsync(new SeatReleasedEvent
                    {
                        EventId = Guid.NewGuid(),
                        Timestamp = DateTime.UtcNow,
                        SessionId = sessionId,
                        SeatId = seatId
                    }, ct);

                    return new SeatReleaseResult { Success = true };
                }
            }
        }

        return new SeatReleaseResult { Success = false };
    }

    // ... other implementations

    private string GetKey(Guid sessionId, string seatId) => $"{sessionId}:{seatId}";

    private void StartExpirationChecker()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                var now = DateTime.UtcNow;
                var expired = _locks.Where(kv => kv.Value.ExpiresAt < now).ToList();
                foreach (var kv in expired)
                {
                    if (_locks.TryRemove(kv.Key, out var lockInfo))
                    {
                        await _eventBus.PublishAsync(new SeatLockExpiredEvent
                        {
                            EventId = Guid.NewGuid(),
                            Timestamp = DateTime.UtcNow,
                            SessionId = lockInfo.SessionId,
                            SeatId = lockInfo.SeatId
                        });
                    }
                }
            }
        });
    }

    private record SeatLockInfo
    {
        public Guid SessionId { get; init; }
        public string SeatId { get; init; } = default!;
        public Guid UserId { get; init; }
        public DateTime LockedAt { get; init; }
        public DateTime ExpiresAt { get; init; }
    }
}
```

### 2.6 Notification Service Abstraction

**Purpose:** 即時通知的統一介面

```csharp
// OpenTicket.Abstractions/Notification/INotificationService.cs
namespace OpenTicket.Abstractions.Notification;

/// <summary>
/// 通知服務介面
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 推播給特定用戶
    /// </summary>
    Task SendToUserAsync(
        Guid userId,
        Notification notification,
        CancellationToken ct = default);

    /// <summary>
    /// 推播給群組（如：觀看同一場次的所有用戶）
    /// </summary>
    Task SendToGroupAsync(
        string groupName,
        Notification notification,
        CancellationToken ct = default);

    /// <summary>
    /// 廣播
    /// </summary>
    Task BroadcastAsync(
        Notification notification,
        CancellationToken ct = default);
}

/// <summary>
/// 即時連線管理介面
/// </summary>
public interface IConnectionManager
{
    Task<bool> AddToGroupAsync(
        string connectionId,
        string groupName,
        CancellationToken ct = default);

    Task<bool> RemoveFromGroupAsync(
        string connectionId,
        string groupName,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetGroupMembersAsync(
        string groupName,
        CancellationToken ct = default);
}

/// <summary>
/// 通知
/// </summary>
public record Notification
{
    public required string Type { get; init; }  // e.g., "SeatStatusChanged"
    public required object Payload { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
```

---

## 3. Dependency Injection Configuration

### 3.1 Service Registration Patterns

```csharp
// OpenTicket.Infrastructure/DependencyInjection.cs
namespace OpenTicket.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// 註冊 MVP 模式的服務（In-Memory / Mock）
    /// </summary>
    public static IServiceCollection AddMvpInfrastructure(
        this IServiceCollection services)
    {
        // Event Bus
        services.AddSingleton<IEventBus, InMemoryEventBus>();

        // Repositories (In-Memory)
        services.AddSingleton(typeof(IRepository<,>), typeof(InMemoryRepository<,>));
        services.AddSingleton(typeof(IQueryableRepository<,>), typeof(InMemoryRepository<,>));

        // Seat Lock Service
        services.AddSingleton<ISeatLockService, InMemorySeatLockService>();

        // Payment Provider (Mock)
        services.AddSingleton<IPaymentProvider, MockPaymentProvider>();

        // Identity Provider (Mock)
        services.AddSingleton<IIdentityProvider, MockIdentityProvider>();

        // Notification Service (In-Memory)
        services.AddSingleton<INotificationService, InMemoryNotificationService>();

        return services;
    }

    /// <summary>
    /// 註冊 Production 模式的服務
    /// </summary>
    public static IServiceCollection AddProductionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Event Bus (NATS)
        services.AddSingleton<IEventBus, NatsEventBus>();
        services.Configure<NatsOptions>(configuration.GetSection("NATS"));

        // Database (PostgreSQL + EF Core)
        services.AddDbContext<OpenTicketDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Repositories (EF Core)
        services.AddScoped(typeof(IRepository<,>), typeof(EfCoreRepository<,>));
        services.AddScoped(typeof(IQueryableRepository<,>), typeof(EfCoreRepository<,>));

        // Seat Lock Service (Redis-backed)
        services.AddSingleton<ISeatLockService, RedisSeatLockService>();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
        });

        // Payment Providers
        services.AddScoped<IPaymentProvider, StripePaymentProvider>();
        // Or use factory for multiple providers
        services.AddScoped<IPaymentProviderFactory, PaymentProviderFactory>();

        // Identity Providers
        services.AddScoped<IIdentityProvider, GoogleIdentityProvider>();
        // Or use factory for multiple providers
        services.AddScoped<IIdentityProviderFactory, IdentityProviderFactory>();

        // Notification Service (SignalR)
        services.AddSignalR().AddStackExchangeRedis(configuration.GetConnectionString("Redis")!);
        services.AddScoped<INotificationService, SignalRNotificationService>();

        return services;
    }
}
```

### 3.2 Provider Factory Pattern

```csharp
// OpenTicket.Abstractions/Payment/IPaymentProviderFactory.cs
public interface IPaymentProviderFactory
{
    IPaymentProvider GetProvider(string providerName);
    IReadOnlyList<string> GetAvailableProviders();
}

// OpenTicket.Infrastructure/Payment/PaymentProviderFactory.cs
public class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly IReadOnlyDictionary<string, IPaymentProvider> _providers;

    public PaymentProviderFactory(IEnumerable<IPaymentProvider> providers)
    {
        _providers = providers.ToDictionary(
            p => p.ProviderName,
            p => p,
            StringComparer.OrdinalIgnoreCase);
    }

    public IPaymentProvider GetProvider(string providerName)
    {
        if (!_providers.TryGetValue(providerName, out var provider))
        {
            throw new PaymentProviderNotFoundException(providerName);
        }
        return provider;
    }

    public IReadOnlyList<string> GetAvailableProviders()
    {
        return _providers.Keys.ToList();
    }
}
```

---

## 4. Testing with Abstractions

### 4.1 Unit Test Example

```csharp
public class OrderServiceTests
{
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<ISeatLockService> _seatLockMock;
    private readonly Mock<IRepository<Order, Guid>> _orderRepoMock;
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _eventBusMock = new Mock<IEventBus>();
        _seatLockMock = new Mock<ISeatLockService>();
        _orderRepoMock = new Mock<IRepository<Order, Guid>>();

        _sut = new OrderService(
            _eventBusMock.Object,
            _seatLockMock.Object,
            _orderRepoMock.Object);
    }

    [Fact]
    public async Task CreateOrder_WithLockedSeats_ShouldSucceed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seatId = "A1-001";

        _seatLockMock
            .Setup(x => x.GetSeatStatusAsync(sessionId, seatId, default))
            .ReturnsAsync(new SeatStatus
            {
                SeatId = seatId,
                State = SeatState.Locked,
                LockedBy = userId
            });

        // Act
        var result = await _sut.CreateOrderAsync(new CreateOrderRequest
        {
            SessionId = sessionId,
            UserId = userId,
            SeatIds = new[] { seatId }
        });

        // Assert
        Assert.True(result.Success);
        _orderRepoMock.Verify(x => x.AddAsync(It.IsAny<Order>(), default), Times.Once);
        _eventBusMock.Verify(x => x.PublishAsync(
            It.IsAny<OrderCreatedEvent>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithUnlockedSeats_ShouldFail()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seatId = "A1-001";

        _seatLockMock
            .Setup(x => x.GetSeatStatusAsync(sessionId, seatId, default))
            .ReturnsAsync(new SeatStatus
            {
                SeatId = seatId,
                State = SeatState.Available
            });

        // Act
        var result = await _sut.CreateOrderAsync(new CreateOrderRequest
        {
            SessionId = sessionId,
            UserId = userId,
            SeatIds = new[] { seatId }
        });

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SEAT_NOT_LOCKED", result.ErrorCode);
    }
}
```

### 4.2 Integration Test with In-Memory Services

```csharp
public class TicketingIntegrationTests : IClassFixture<InMemoryTestFixture>
{
    private readonly InMemoryTestFixture _fixture;

    public TicketingIntegrationTests(InMemoryTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FullPurchaseFlow_ShouldCompleteSuccessfully()
    {
        // Arrange
        var sessionId = await _fixture.CreateTestSession();
        var userId = Guid.NewGuid();
        var seatId = "A1-001";

        // Act 1: Lock seat
        var lockResult = await _fixture.SeatLockService.TryLockSeatAsync(
            new SeatLockRequest
            {
                SessionId = sessionId,
                SeatId = seatId,
                UserId = userId
            });

        Assert.True(lockResult.Success);

        // Act 2: Create order
        var orderResult = await _fixture.OrderService.CreateOrderAsync(
            new CreateOrderRequest
            {
                SessionId = sessionId,
                UserId = userId,
                SeatIds = new[] { seatId }
            });

        Assert.True(orderResult.Success);

        // Act 3: Process payment (Mock auto-succeeds)
        await Task.Delay(1500); // Wait for mock payment

        // Assert: Order should be completed
        var order = await _fixture.OrderRepository.GetByIdAsync(orderResult.OrderId!.Value);
        Assert.Equal(OrderStatus.Completed, order!.Status);

        // Assert: Ticket should be issued
        var tickets = await _fixture.TicketRepository.FindAsync(
            t => t.OrderId == orderResult.OrderId);
        Assert.Single(tickets);
        Assert.Equal(seatId, tickets[0].SeatId);
    }
}

public class InMemoryTestFixture : IDisposable
{
    public IEventBus EventBus { get; }
    public ISeatLockService SeatLockService { get; }
    public IOrderService OrderService { get; }
    public IRepository<Order, Guid> OrderRepository { get; }
    public IRepository<Ticket, Guid> TicketRepository { get; }

    public InMemoryTestFixture()
    {
        var services = new ServiceCollection();
        services.AddMvpInfrastructure();
        services.AddApplicationServices();

        var provider = services.BuildServiceProvider();

        EventBus = provider.GetRequiredService<IEventBus>();
        SeatLockService = provider.GetRequiredService<ISeatLockService>();
        OrderService = provider.GetRequiredService<IOrderService>();
        OrderRepository = provider.GetRequiredService<IRepository<Order, Guid>>();
        TicketRepository = provider.GetRequiredService<IRepository<Ticket, Guid>>();
    }

    public async Task<Guid> CreateTestSession()
    {
        // Create test venue, event, session...
        return Guid.NewGuid();
    }

    public void Dispose() { }
}
```

---

## 5. Configuration-Based Switching

### 5.1 appsettings.json

```json
{
  "Infrastructure": {
    "Mode": "MVP",  // "MVP" or "Production"

    "EventBus": {
      "Type": "InMemory",  // "InMemory" or "NATS"
      "NATS": {
        "Url": "nats://localhost:4222",
        "StreamPrefix": "openticket"
      }
    },

    "Database": {
      "Type": "InMemory",  // "InMemory" or "PostgreSQL"
      "PostgreSQL": {
        "ConnectionString": "Host=localhost;Database=openticket;..."
      }
    },

    "Cache": {
      "Type": "InMemory",  // "InMemory" or "Redis"
      "Redis": {
        "ConnectionString": "localhost:6379"
      }
    },

    "Payment": {
      "DefaultProvider": "Mock",  // "Mock", "Stripe", "LinePay"
      "Stripe": {
        "ApiKey": "sk_test_...",
        "WebhookSecret": "whsec_..."
      }
    },

    "Identity": {
      "DefaultProvider": "Mock",  // "Mock", "Google", "Apple"
      "Google": {
        "ClientId": "...",
        "ClientSecret": "..."
      }
    }
  }
}
```

### 5.2 Conditional Registration

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var mode = configuration.GetValue<string>("Infrastructure:Mode");

        if (mode == "MVP")
        {
            return services.AddMvpInfrastructure();
        }

        return services.AddProductionInfrastructure(configuration);
    }
}
```

---

## 6. Summary

### 6.1 Abstraction Checklist

| Abstraction | Interface | MVP Impl | Prod Impl | Tests |
|-------------|-----------|----------|-----------|-------|
| Event Bus | `IEventBus` | `InMemoryEventBus` | `NatsEventBus` | Unit + Integration |
| Repository | `IRepository<T,TId>` | `InMemoryRepository` | `EfCoreRepository` | Unit |
| Payment | `IPaymentProvider` | `MockPaymentProvider` | `StripePaymentProvider` | Unit + Integration |
| Identity | `IIdentityProvider` | `MockIdentityProvider` | `GoogleIdentityProvider` | Unit |
| Seat Lock | `ISeatLockService` | `InMemorySeatLockService` | `RedisSeatLockService` | Unit + Integration |
| Notification | `INotificationService` | `InMemoryNotificationService` | `SignalRNotificationService` | Integration |

### 6.2 Benefits

1. **Business Logic 獨立**：核心邏輯不依賴具體實作
2. **快速原型**：MVP 可在無外部依賴下運行
3. **測試友善**：所有依賴可 Mock
4. **漸進式升級**：從 In-Memory → Production 無需改 Business Code
5. **多提供者支援**：Payment、Identity 可同時支援多家
