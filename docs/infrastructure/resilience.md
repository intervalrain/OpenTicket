# Resilience (彈性處理)

OpenTicket Resilience 模組提供基於 Polly v8 的彈性處理機制，適用於任何需要重試、熔斷或超時控制的場景。

## 為什麼選擇 Polly?

| Solution | 優點 | 缺點 |
|----------|------|------|
| **Polly v8** | ✅ .NET Foundation 官方認證<br>✅ Microsoft 推薦並整合<br>✅ 社群活躍、文件完整 | ⚠️ v7 → v8 API 變動大 |
| **Microsoft.Extensions.Resilience** | ✅ 官方套件、DI 整合 | ⚠️ 底層仍是 Polly |
| **自行實作** | ✅ 完全掌控 | ❌ 重複造輪子 |

**結論**：Polly 是 .NET 生態系唯一且官方推薦的 Resilience 解決方案。

## 快速開始

### 1. 註冊服務

```csharp
// Program.cs 或 Module
services.AddResilience(configuration);

// 或自訂設定
services.AddResilience(configuration, options =>
{
    options.MaxRetryAttempts = 5;
    options.Timeout = TimeSpan.FromSeconds(60);
});
```

### 2. 設定 appsettings.json

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

## 使用方式

### 基本用法

```csharp
public class ExternalApiService
{
    private readonly IResilienceService _resilience;
    private readonly HttpClient _httpClient;

    public ExternalApiService(IResilienceService resilience, HttpClient httpClient)
    {
        _resilience = resilience;
        _httpClient = httpClient;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken ct)
    {
        return await _resilience.ExecuteAsync(
            async token =>
            {
                var response = await _httpClient.PostAsJsonAsync("/api/payments", request, token);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<PaymentResult>(token);
            },
            new ResilienceContext
            {
                OperationName = "ProcessPayment",
                CorrelationId = request.OrderId.ToString()
            },
            ct);
    }
}
```

### 資料庫連線

```csharp
public class OrderRepository : IOrderRepository
{
    private readonly IResilienceService _resilience;
    private readonly AppDbContext _context;

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _resilience.ExecuteAsync(
            async token => await _context.Orders.FindAsync([id], token),
            new ResilienceContext { OperationName = "GetOrderById" },
            ct);
    }
}
```

### 訊息佇列處理

```csharp
public class SeatLockWorker : BackgroundService
{
    private readonly IMessageConsumer _consumer;
    private readonly IResilientMessageHandler _resilientHandler;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _consumer.SubscribeAsync<SeatLockMessage>(
            topic: "seat-lock-requests",
            consumerGroup: "seat-lock-workers",
            handler: async (context, cancellationToken) =>
            {
                await _resilientHandler.HandleAsync(
                    context,
                    ProcessSeatLockAsync,
                    cancellationToken);
            },
            ct);
    }
}
```

## 彈性策略說明

### Retry (重試)

當遇到暫時性錯誤時自動重試：

| 設定 | 說明 | 預設值 |
|------|------|--------|
| `MaxRetryAttempts` | 最大重試次數 | 3 |
| `RetryDelay` | 重試間隔（基礎值） | 1 秒 |
| `MaxRetryDelay` | 最大重試間隔 | 30 秒 |
| `UseExponentialBackoff` | 使用指數退避 | true |
| `JitterFactor` | 隨機抖動因子 (0-1) | 0.2 |

**指數退避範例**（JitterFactor=0）：
- 第 1 次重試：1 秒後
- 第 2 次重試：2 秒後
- 第 3 次重試：4 秒後

### Circuit Breaker (熔斷器)

當失敗率過高時，暫時停止請求避免雪崩：

| 設定 | 說明 | 預設值 |
|------|------|--------|
| `EnableCircuitBreaker` | 啟用熔斷器 | true |
| `CircuitBreakerFailureRatio` | 觸發熔斷的失敗率 | 0.5 (50%) |
| `CircuitBreakerMinimumThroughput` | 計算前最小請求數 | 10 |
| `CircuitBreakerSamplingDuration` | 統計取樣時間窗口 | 60 秒 |
| `CircuitBreakerDuration` | 熔斷持續時間 | 30 秒 |

**狀態轉換**：
```
Closed ──(失敗率 > 50%)──▶ Open ──(30秒後)──▶ Half-Open ──(成功)──▶ Closed
                                                   │
                                                   └──(失敗)──▶ Open
```

### Timeout (超時)

限制單一操作的最長執行時間：

| 設定 | 說明 | 預設值 |
|------|------|--------|
| `Timeout` | 操作超時時間 | 30 秒 |

## 暫時性錯誤判定

以下例外會被視為**暫時性錯誤**並觸發重試：

| 例外類型 | 說明 |
|----------|------|
| `TimeoutException` | 操作超時 |
| `*TransientException` | 類型名稱包含 "Transient" |
| `*ConnectionException` | 類型名稱包含 "Connection" |
| `*NetworkException` | 類型名稱包含 "Network" |
| `*UnavailableException` | 類型名稱包含 "Unavailable" |

以下例外**不會**觸發重試：

| 例外類型 | 說明 |
|----------|------|
| `OperationCanceledException` | 操作被取消 |
| `TaskCanceledException` | 任務被取消 |
| `BrokenCircuitException` | 熔斷器開啟中 |

## 適用場景

| 場景 | 說明 |
|------|------|
| **外部 API 呼叫** | 支付閘道、身份驗證、第三方服務 |
| **資料庫連線** | 連線池耗盡、暫時性網路問題 |
| **訊息佇列** | Broker 連線中斷、消費者處理失敗 |
| **快取存取** | Redis 連線問題 |
| **檔案系統** | 暫時性 IO 錯誤 |

## 最佳實踐

### 1. 選擇適當的重試次數

```csharp
// 外部 API：較多重試，因為網路問題常見
options.MaxRetryAttempts = 5;

// 資料庫：較少重試，因為通常是永久性錯誤
options.MaxRetryAttempts = 2;
```

### 2. 使用 ResilienceContext 提供追蹤資訊

```csharp
await _resilience.ExecuteAsync(
    action,
    new ResilienceContext
    {
        OperationName = "ProcessPayment",      // 用於日誌
        CorrelationId = Activity.Current?.Id,  // 分散式追蹤
        Properties = new Dictionary<string, object>
        {
            ["OrderId"] = orderId,
            ["Amount"] = amount
        }
    },
    ct);
```

### 3. 區分暫時性與永久性錯誤

```csharp
try
{
    await _resilience.ExecuteAsync(ProcessAsync, ct);
}
catch (ValidationException ex)
{
    // 永久性錯誤：不應重試
    throw;
}
catch (BrokenCircuitException ex)
{
    // 熔斷器開啟：服務暫時不可用
    return ServiceUnavailable();
}
```

## API 參考

### IResilienceService

```csharp
public interface IResilienceService
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        ResilienceContext? context = null,
        CancellationToken ct = default);

    Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        ResilienceContext? context = null,
        CancellationToken ct = default);
}
```

### ResilienceContext

```csharp
public class ResilienceContext
{
    public string? OperationName { get; init; }
    public string? CorrelationId { get; init; }
    public IDictionary<string, object>? Properties { get; init; }
}
```
