# Message Broker

OpenTicket Message Broker 提供統一的訊息佇列抽象層，支援多種 Provider 實作，適用於高流量分散式場景。

## 支援的 Provider

| Provider | 狀態 | 適用場景 |
|----------|------|----------|
| Redis Streams | ✅ 已實作 | 中等流量分散式部署 |
| NATS JetStream | ✅ 已實作 | 高流量、低延遲場景 |
| RabbitMQ | ⏳ 待實作 | 企業級訊息佇列 |
| Kafka | ⏳ 待實作 | 超高流量事件串流 |

## 快速開始

### 1. 註冊服務

```csharp
// Program.cs 或 Module
// 先註冊共用的 Resilience 服務
services.AddResilience(configuration);

// 再註冊 MessageBroker
services.AddMessageBroker(configuration, MessageBrokerOption.Redis);
// 或
services.AddMessageBroker(configuration, MessageBrokerOption.Nats);
```

### 2. 設定 appsettings.json

```json
{
  "MessageBroker": {
    "PartitionCount": 64,
    "BatchSize": 100,
    "PollTimeout": "00:00:05",
    "MaxRetryAttempts": 3,
    "RetryDelay": "00:00:01",
    "EnableDeduplication": false,
    "DeduplicationWindow": "00:05:00",

    "Redis": {
      "ConnectionString": "localhost:6379",
      "KeyPrefix": "msgbroker",
      "MaxStreamLength": 100000,
      "ClaimTimeout": "00:05:00"
    },

    "Nats": {
      "Url": "nats://localhost:4222",
      "StreamPrefix": "MSGBROKER",
      "MaxAge": "7.00:00:00",
      "AckWait": "00:00:30",
      "MaxDeliver": 5
    }
  }
}
```

## 核心概念

### Message (訊息)

所有訊息必須繼承 `Message` 基底類別：

```csharp
public record SeatLockMessage : Message
{
    public Guid SessionId { get; init; }
    public Guid AreaId { get; init; }
    public IReadOnlyList<string> SeatIds { get; init; } = [];

    // PartitionKey 決定訊息路由到哪個 Partition
    // 相同 PartitionKey 的訊息會被順序處理
    public override string PartitionKey => $"{SessionId}:{AreaId}";
}
```

### Partition (分區)

- 訊息根據 `PartitionKey` 的 Hash 值路由到特定分區
- 同一分區的訊息保證**順序處理**
- 預設 64 個分區，可根據流量調整

### Consumer Group (消費者群組)

- 多個 Worker 實例可加入同一個 Consumer Group
- 訊息在群組內**只被處理一次**
- 支援故障轉移和負載均衡

## 使用範例

### 發布訊息

```csharp
public class TicketService
{
    private readonly IMessageProducer _producer;

    public TicketService(IMessageProducer producer)
    {
        _producer = producer;
    }

    public async Task RequestSeatLockAsync(SeatLockRequest request, CancellationToken ct)
    {
        var message = new SeatLockMessage
        {
            SessionId = request.SessionId,
            AreaId = request.AreaId,
            SeatIds = request.SeatIds,
            CorrelationId = Activity.Current?.Id // 用於追蹤
        };

        await _producer.PublishAsync("seat-lock-requests", message, ct);
    }
}
```

### 訂閱訊息

```csharp
public class SeatLockWorker : BackgroundService
{
    private readonly IMessageConsumer _consumer;
    private readonly ISeatLockHandler _handler;

    public SeatLockWorker(IMessageConsumer consumer, ISeatLockHandler handler)
    {
        _consumer = consumer;
        _handler = handler;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _consumer.SubscribeAsync<SeatLockMessage>(
            topic: "seat-lock-requests",
            consumerGroup: "seat-lock-workers",
            handler: async (context, cancellationToken) =>
            {
                try
                {
                    await _handler.HandleAsync(context.Message, cancellationToken);
                    await context.AckAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // 重新排隊等待重試
                    await context.NakAsync(requeue: true, cancellationToken);
                }
            },
            ct);
    }
}
```

### 訂閱特定分區

用於需要分區親和性的場景：

```csharp
// 只處理分區 0-15 的訊息
for (int partition = 0; partition < 16; partition++)
{
    await _consumer.SubscribeToPartitionAsync<SeatLockMessage>(
        topic: "seat-lock-requests",
        consumerGroup: "seat-lock-workers",
        partition: partition,
        handler: HandleMessageAsync,
        ct);
}
```

## MessageContext API

訂閱處理時，透過 `IMessageContext<T>` 控制訊息確認：

| 方法 | 說明 |
|------|------|
| `AckAsync()` | 確認訊息已成功處理 |
| `NakAsync(requeue)` | 拒絕訊息，`requeue=true` 重新排隊 |
| `DelayAsync(delay)` | 延遲重新處理 (部分 Provider 支援) |

```csharp
handler: async (context, ct) =>
{
    var message = context.Message;
    var topic = context.Topic;
    var partition = context.Partition;

    // 處理邏輯...

    await context.AckAsync(ct);
}
```

## 管理 API

透過 `IMessageBroker` 取得管理功能：

```csharp
public class MessageBrokerHealthCheck : IHealthCheck
{
    private readonly IMessageBroker _broker;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        var isHealthy = await _broker.IsHealthyAsync(ct);
        var pending = await _broker.GetPendingCountAsync("seat-lock-requests", "workers", ct);

        return isHealthy
            ? HealthCheckResult.Healthy($"Pending: {pending}")
            : HealthCheckResult.Unhealthy("Broker unavailable");
    }
}
```

## 架構設計

```
┌─────────────┐     ┌────────────────────────────────────────┐
│   API       │     │           Message Broker               │
│   Server    │────▶│  ┌─────┬─────┬─────┬─────┬─────┐       │
│             │     │  │ P0  │ P1  │ P2  │ ... │ P63 │       │
└─────────────┘     │  └──┬──┴──┬──┴──┬──┴─────┴──┬──┘       │
                    │     │     │     │           │          │
                    └─────┼─────┼─────┼───────────┼──────────┘
                          │     │     │           │
                    ┌─────▼─────▼─────▼───────────▼──────────┐
                    │              Worker Pool               │
                    │  ┌────────┐ ┌────────┐ ┌────────┐      │
                    │  │Worker 1│ │Worker 2│ │Worker N│      │
                    │  └────────┘ └────────┘ └────────┘      │
                    └────────────────────────────────────────┘
```

## 最佳實踐

### PartitionKey 設計

```csharp
// Good: 細粒度分區，避免熱點
public override string PartitionKey => $"{SessionId}:{AreaId}";

// Bad: 粗粒度分區，可能造成熱點
public override string PartitionKey => SessionId.ToString();
```

### 錯誤處理

```csharp
handler: async (context, ct) =>
{
    try
    {
        await ProcessAsync(context.Message, ct);
        await context.AckAsync(ct);
    }
    catch (TransientException)
    {
        // 暫時性錯誤，重新排隊
        await context.NakAsync(requeue: true, ct);
    }
    catch (PermanentException)
    {
        // 永久性錯誤，丟棄訊息
        await context.NakAsync(requeue: false, ct);
        // 可選：發送到 Dead Letter Queue
    }
}
```

### 冪等性設計

由於訊息可能被重複投遞，處理邏輯必須設計為冪等：

```csharp
public async Task HandleAsync(SeatLockMessage message, CancellationToken ct)
{
    // 使用 MessageId 檢查是否已處理
    if (await _cache.ExistsAsync($"processed:{message.MessageId}"))
        return;

    await _seatService.LockAsync(message.SeatIds, ct);

    // 標記為已處理
    await _cache.SetAsync($"processed:{message.MessageId}", true, TimeSpan.FromHours(1));
}
```

## Resilience (彈性處理)

彈性處理機制已獨立為 `OpenTicket.Infrastructure.Resilience` 模組，提供 Retry、Circuit Breaker、Timeout 等策略。

> 詳細說明請參考 [Resilience 文件](./resilience.md)

### 設定

```json
{
  "Resilience": {
    "MaxRetryAttempts": 3,
    "RetryDelay": "00:00:01",
    "MaxRetryDelay": "00:00:30",
    "UseExponentialBackoff": true,
    "JitterFactor": 0.2,
    "EnableCircuitBreaker": true,
    "CircuitBreakerFailureRatio": 0.5,
    "CircuitBreakerDuration": "00:00:30",
    "CircuitBreakerSamplingDuration": "00:01:00",
    "CircuitBreakerMinimumThroughput": 10,
    "Timeout": "00:00:30"
  }
}
```

### 使用 IResilientMessageHandler

推薦使用 `IResilientMessageHandler` 包裝訊息處理邏輯：

```csharp
public class SeatLockWorker : BackgroundService
{
    private readonly IMessageConsumer _consumer;
    private readonly IResilientMessageHandler _resilientHandler;
    private readonly ISeatLockService _seatLockService;

    public SeatLockWorker(
        IMessageConsumer consumer,
        IResilientMessageHandler resilientHandler,
        ISeatLockService seatLockService)
    {
        _consumer = consumer;
        _resilientHandler = resilientHandler;
        _seatLockService = seatLockService;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _consumer.SubscribeAsync<SeatLockMessage>(
            topic: "seat-lock-requests",
            consumerGroup: "seat-lock-workers",
            handler: async (context, cancellationToken) =>
            {
                await _resilientHandler.HandleAsync(
                    context,
                    async (message, token) =>
                    {
                        await _seatLockService.LockSeatsAsync(message, token);
                    },
                    cancellationToken);
            },
            ct);
    }
}
```

### 使用 IResilienceService

也可以直接使用 `IResilienceService` 獲得更細粒度的控制：

```csharp
public class MyService
{
    private readonly IResilienceService _resilience;

    public async Task DoWorkAsync(CancellationToken ct)
    {
        await _resilience.ExecuteAsync(
            async token =>
            {
                // 會自動重試的操作
                await CallExternalServiceAsync(token);
            },
            new ResilienceContext
            {
                OperationName = "CallExternalService",
                MessageId = "123",
                Topic = "my-topic"
            },
            ct);
    }
}
```

### 彈性策略說明

| 策略 | 說明 |
|------|------|
| **Retry** | 暫時性錯誤自動重試，支援指數退避和 Jitter |
| **Circuit Breaker** | 當失敗率過高時，暫時停止請求避免雪崩 |
| **Timeout** | 限制單一操作的最長執行時間 |

### 暫時性錯誤判定

以下類型的例外會被視為暫時性錯誤並觸發重試：
- `TimeoutException`
- 類型名稱包含 `Transient`、`Connection`、`Network`、`Unavailable` 的例外

以下類型**不會**觸發重試：
- `OperationCanceledException`
- `TaskCanceledException`
- `BrokenCircuitException`

## Provider 特性比較

| 特性 | Redis Streams | NATS JetStream |
|------|---------------|----------------|
| 持久化 | 需配置 RDB/AOF | 內建 JetStream |
| 訊息順序 | Stream 內保證 | Subject 內保證 |
| Consumer Group | 原生支援 | Durable Consumer |
| 訊息確認 | XACK | Ack/Nak/Term |
| 延遲重試 | 透過 Polly | 透過 Polly |
| 叢集模式 | Redis Cluster | NATS Cluster |
