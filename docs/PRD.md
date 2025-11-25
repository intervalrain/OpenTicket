# OpenTicket - Product Requirements Document (PRD)

**Version:** 1.0
**Last Updated:** 2025-01
**Status:** Draft

---

## 1. Executive Summary

OpenTicket 是一套全球分散式售票基礎建設平台，採用 Event-Driven Architecture 設計，解決現有票務系統的核心痛點：秒殺當機、重複售票、黃牛入侵、以及缺乏標準化的場館座位格式。

### 1.1 Product Vision

**打造全球級票務基礎建設（Global Ticket Infrastructure）**

- 支援每秒百萬級請求的秒殺場景
- 零重複售票（Zero Double-Sell）保證
- 可插拔式身份驗證與支付模組
- 開放式場館座位 Schema
- Multi-Region 全球部署能力

### 1.2 Business Model

| Tier | 功能 | 目標客戶 |
|------|------|----------|
| **Free** | 基礎售票、In-Memory 座位鎖、單區部署 | 小型活動主辦 |
| **Pro** | 進階防 Bot、多支付渠道、分析報表 | 中型票務商 |
| **Enterprise** | Multi-Region、SLA 保證、自訂 Identity Provider | 大型場館、國際票務 |

---

## 2. Domain Context Separation (DDD Bounded Contexts)

系統依據 Domain-Driven Design 切分為以下 Bounded Contexts：

```
┌─────────────────────────────────────────────────────────────────────┐
│                        OpenTicket Platform                          │
├─────────────────────────────────────────────────────────────────────┤
│  ┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────────────┐ │
│  │  Venue    │  │  Event    │  │  Ticket   │  │     Identity      │ │
│  │  Context  │  │  Context  │  │  Context  │  │     Context       │ │
│  └─────┬─────┘  └─────┬─────┘  └─────┬─────┘  └─────────┬─────────┘ │
│        │              │              │                  │           │
│  ┌─────┴─────┐  ┌─────┴─────┐  ┌─────┴─────┐  ┌─────────┴─────────┐ │
│  │  Payment  │  │   Order   │  │  Queue    │  │    Notification   │ │
│  │  Context  │  │  Context  │  │  Context  │  │      Context      │ │
│  └───────────┘  └───────────┘  └───────────┘  └───────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.1 Context 定義與職責

| Context | 核心職責 | 水平擴展策略 | Bottleneck 分析 |
|---------|----------|--------------|-----------------|
| **Venue** | 場館、座位圖管理 | Read Replica | 讀多寫少，快取優先 |
| **Event** | 活動、場次、票價管理 | Read Replica | 開賣前讀取高峰 |
| **Ticket** | 座位鎖定、票券狀態 | **Partition by SeatId** | **最大瓶頸** |
| **Identity** | 身份驗證、用戶管理 | Stateless 水平擴展 | 開賣瞬間驗證高峰 |
| **Payment** | 支付處理、退款 | 依 Payment Provider | 外部 API 限制 |
| **Order** | 訂單生命週期 | Partition by UserId | 寫入高峰 |
| **Queue** | 流量控制、排隊、防 Bot | Stateless + Redis | 入口流量控制 |
| **Notification** | 即時推播、通知 | Fan-out 模式 | WebSocket 連線數 |

### 2.2 Context 間通訊

```
┌──────────────────────────────────────────────────────────────┐
│                      NATS Event Bus                          │
│  (Async Event-Driven Communication between Contexts)         │
└──────────────────────────────────────────────────────────────┘
         ↑           ↑           ↑           ↑
    Integration  Integration Integration Integration
      Events       Events      Events      Events
         ↑           ↑           ↑           ↑
    ┌────────┐  ┌────────┐  ┌────────┐  ┌────────┐
    │ Ticket │  │ Order  │  │Payment │  │  ...   │
    └────────┘  └────────┘  └────────┘  └────────┘
```

**通訊原則：**
- Context 內部：同步呼叫（直接方法調用）
- Context 之間：非同步事件（NATS）
- Query 需求：可透過 API Gateway 聚合

---

## 3. Functional Requirements

### 3.1 Venue Context

| Feature ID | 功能 | Priority | MVP |
|------------|------|----------|-----|
| VEN-001 | 場館 CRUD | P0 | Yes |
| VEN-002 | 座位圖 Schema 定義 | P0 | Yes |
| VEN-003 | 座位圖視覺化渲染資料 | P0 | Yes |
| VEN-004 | 多區域座位配置 | P1 | No |
| VEN-005 | 動態座位屬性（輪椅、VIP）| P2 | No |

### 3.2 Event Context

| Feature ID | 功能 | Priority | MVP |
|------------|------|----------|-----|
| EVT-001 | 活動 CRUD | P0 | Yes |
| EVT-002 | 場次管理 | P0 | Yes |
| EVT-003 | 票價區間設定 | P0 | Yes |
| EVT-004 | 開賣時間排程 | P0 | Yes |
| EVT-005 | 活動搜尋 | P1 | No |

### 3.3 Ticket Context (Core)

| Feature ID | 功能 | Priority | MVP |
|------------|------|----------|-----|
| TKT-001 | 座位鎖定（Lock） | P0 | Yes |
| TKT-002 | 座位釋放（Release） | P0 | Yes |
| TKT-003 | 鎖定逾時自動釋放 | P0 | Yes |
| TKT-004 | 座位狀態查詢 | P0 | Yes |
| TKT-005 | 票券產生 | P0 | Yes |
| TKT-006 | 票券驗證（入場） | P1 | No |
| TKT-007 | 票券轉讓 | P2 | No |

### 3.4 Identity Context

| Feature ID | 功能 | Priority | MVP |
|------------|------|----------|-----|
| IDN-001 | 用戶註冊/登入 | P0 | Yes |
| IDN-002 | JWT Token 管理 | P0 | Yes |
| IDN-003 | SMS OTP 驗證 | P1 | No |
| IDN-004 | OAuth Provider（Google/Apple）| P1 | No |
| IDN-005 | 實名制身分證驗證 | P2 | No |
| IDN-006 | 自訂 Identity Provider 介面 | P1 | No |

### 3.5 Payment Context

| Feature ID | 功能 | Priority | MVP |
|------------|------|----------|-----|
| PAY-001 | Mock Payment（開發用） | P0 | Yes |
| PAY-002 | 支付狀態機 | P0 | Yes |
| PAY-003 | 信用卡支付 | P1 | No |
| PAY-004 | Line Pay | P2 | No |
| PAY-005 | 退款處理 | P1 | No |
| PAY-006 | 自訂 Payment Provider 介面 | P1 | No |

### 3.6 Order Context

| Feature ID | 功能 | Priority | MVP |
|------------|------|----------|-----|
| ORD-001 | 建立訂單 | P0 | Yes |
| ORD-002 | 訂單狀態機 | P0 | Yes |
| ORD-003 | 訂單查詢 | P0 | Yes |
| ORD-004 | 訂單取消 | P0 | Yes |
| ORD-005 | 訂單歷史 | P1 | No |

### 3.7 Queue Context

| Feature ID | 功能 | Priority | MVP |
|------------|------|----------|-----|
| QUE-001 | 基礎 Rate Limiting | P0 | Yes |
| QUE-002 | 虛擬排隊機制 | P1 | No |
| QUE-003 | Bot 行為偵測 | P1 | No |
| QUE-004 | Proof-of-Work Challenge | P2 | No |

### 3.8 Notification Context

| Feature ID | 功能 | Priority | MVP |
|------------|------|----------|-----|
| NTF-001 | 座位狀態即時推播 | P0 | Yes |
| NTF-002 | 訂單狀態推播 | P0 | Yes |
| NTF-003 | Email 通知 | P1 | No |
| NTF-004 | SMS 通知 | P2 | No |

---

## 4. Non-Functional Requirements

### 4.1 Performance

| Metric | Target (MVP) | Target (Production) |
|--------|--------------|---------------------|
| Seat Lock Latency | < 100ms | < 50ms |
| API Response (P99) | < 500ms | < 200ms |
| Throughput (seat.lock) | 10K/sec | 100K+/sec |
| WebSocket Connections | 10K | 1M+ |

### 4.2 Reliability

| Metric | Target |
|--------|--------|
| Double-Sell Rate | **0%** (Zero Tolerance) |
| System Uptime | 99.9% |
| Data Durability | 99.999% |

### 4.3 Scalability

| Component | Scaling Strategy |
|-----------|------------------|
| API Gateway | Horizontal (Stateless) |
| Ticket Service | **Partition by SeatId Hash** |
| Order Service | Partition by UserId |
| NATS | Leafnode Cluster (Multi-Region) |
| Database | Read Replica + Sharding |

---

## 5. Abstraction Strategy

### 5.1 Core Abstractions (Interfaces)

每個模組必須定義抽象介面，允許：
1. **Mock 實作**：開發階段無需真實依賴
2. **In-Memory 實作**：單元測試、整合測試
3. **Production 實作**：真實服務整合

```
┌─────────────────────────────────────────────────────────┐
│                    Abstraction Layer                    │
├─────────────────────────────────────────────────────────┤
│  IPaymentProvider      → MockPayment, StripePayment     │
│  IIdentityProvider     → MockIdentity, OAuthIdentity    │
│  ISeatRepository       → InMemorySeat, RedisSeat        │
│  IEventBus             → InMemoryBus, NatsBus           │
│  INotificationService  → MockNotify, SignalRNotify      │
└─────────────────────────────────────────────────────────┘
```

### 5.2 Repository Pattern

所有 Data Access 透過 Repository 介面：

```csharp
public interface IRepository<T, TId> where T : Entity<TId>
{
    Task<T?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(TId id, CancellationToken ct = default);
}
```

### 5.3 Event Bus Abstraction

```csharp
public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IIntegrationEvent;
    Task SubscribeAsync<T>(Func<T, Task> handler, CancellationToken ct = default) where T : IIntegrationEvent;
}
```

---

## 6. MVP Scope Definition

### 6.1 MVP 功能範圍

**包含：**
- 單一場館、單一活動的完整購票流程
- 座位選擇 → 鎖定 → 下單 → Mock 支付 → 取得票券
- 即時座位狀態同步
- 基礎 Rate Limiting
- In-Memory 資料儲存

**不包含：**
- Multi-Region 部署
- 真實支付整合
- 進階防 Bot
- 實名制驗證
- 票券轉讓

### 6.2 MVP User Stories

```
US-001: 作為購票者，我可以瀏覽活動列表
US-002: 作為購票者，我可以查看場館座位圖
US-003: 作為購票者，我可以選擇座位並鎖定
US-004: 作為購票者，我可以在限時內完成結帳
US-005: 作為購票者，我可以看到即時座位狀態變化
US-006: 作為活動主辦，我可以建立活動與場次
US-007: 作為活動主辦，我可以設定票價區間
```

---

## 7. Success Metrics

### 7.1 Technical KPIs

| Metric | MVP Target | 6-Month Target |
|--------|------------|----------------|
| Double-Sell Incidents | 0 | 0 |
| Seat Lock Success Rate | > 99% | > 99.9% |
| P99 Latency | < 500ms | < 200ms |
| System Uptime | > 99% | > 99.9% |

### 7.2 Business KPIs

| Metric | 6-Month Target |
|--------|----------------|
| Beta Partners | 3 場館 |
| Transactions Processed | 10,000+ |
| User Satisfaction | > 4.0/5.0 |

---

## 8. Risks & Mitigations

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Double-Sell Bug | Critical | Low | Actor Model, Comprehensive Testing |
| NATS Cluster Failure | High | Low | Multi-Region Replication |
| Bot Attack | High | High | Queue Gate, Rate Limiting, PoW |
| Payment Provider Outage | Medium | Medium | Multi-Provider Fallback |

---

## 9. Glossary

| Term | Definition |
|------|------------|
| Seat Lock | 座位暫時鎖定，限時內完成結帳 |
| Double-Sell | 同一座位賣給多人的錯誤 |
| Queue Gate | 流量控制閘門，防止瞬間湧入 |
| Bounded Context | DDD 中的領域邊界 |
| Integration Event | Context 間的非同步通訊事件 |

---

## Appendix A: Related Documents

- [Architecture Overview](./architecture/ARCHITECTURE.md)
- [Domain Models](./domain/DOMAIN_MODELS.md)
- [API Specification](./api/API_SPEC.md)
- [Development Guide](./development/DEVELOPMENT_GUIDE.md)
