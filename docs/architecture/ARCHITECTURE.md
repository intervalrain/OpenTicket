# OpenTicket - System Architecture

**Version:** 1.0
**Last Updated:** 2025-01

---

## 1. Architecture Overview

### 1.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                                   Clients                                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐ │
│  │   Browser   │  │  Mobile App │  │  Third-party│  │   Admin Dashboard       │ │
│  │  (SignalR)  │  │  (SignalR)  │  │    API      │  │                         │ │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └───────────┬─────────────┘ │
└─────────┼────────────────┼────────────────┼─────────────────────┼───────────────┘
          │                │                │                     │
          └────────────────┴────────┬───────┴─────────────────────┘
                                    │
                                    ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                              Edge Layer                                  │
│  ┌──────────────────────────────────────────────────────────────────┐    │
│  │                        CDN / Load Balancer                       │    │
│  │                    (Cloudflare / AWS CloudFront)                 │    │
│  └──────────────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                            API Gateway Layer                             │
│  ┌──────────────────────────────────────────────────────────────────┐    │
│  │                      Queue Gate / API Gateway                    │    │
│  │              (Rate Limiting, Auth, Bot Detection, Routing)       │    │
│  │                         .NET Minimal API                         │    │
│  └──────────────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    │                               │
                    ▼                               ▼
┌───────────────────────────────────┐ ┌────────────────────────────────────┐
│        Command Path               │ │              Query Path            │
│  (Write Operations via NATS)      │ │    (Read Operations via HTTP)      │
└───────────────┬───────────────────┘ └───────────────────┬────────────────┘
                │                                         │
                ▼                                         ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                              Service Layer                               │
│                                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐   │
│  │    Ticket    │  │     Order    │  │    Payment   │  │  Identity   │   │
│  │    Service   │  │    Service   │  │    Service   │  │   Service   │   │
│  │ (Partitioned)│  │ (Partitioned)│  │  (Stateless) │  │ (Stateless) │   │
│  └───────┬──────┘  └───────┬──────┘  └───────┬──────┘  └──────┬──────┘   │
│          │                 │                 │                │          │
│  ┌───────┴──────┐  ┌───────┴──────┐  ┌───────┴──────┐  ┌──────┴──────┐   │
│  │     Queue    │  │     Venue    │  │     Event    │  │Notification │   │
│  │    Service   │  │    Service   │  │    Service   │  │   Service   │   │
│  │  (Stateless) │  │(Read Replica)│  │(Read Replica)│  │  (Fan-out)  │   │
│  └──────────────┘  └──────────────┘  └──────────────┘  └─────────────┘   │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
                │                               │
                ▼                               ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                         Messaging Layer                                  │
│  ┌───────────────────────────────────────────────────────────────────┐   │
│  │                        NATS JetStream                             │   │
│  │             (Event Bus, Message Queue, KV Store)                  │   │
│  │                                                                   │   │
│  │ Streams: seat.*, order.*, payment.*, identity.*, notification.*   │   │
│  └───────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘
                │                               │
                ▼                               ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                            Data Layer                                    │
│                                                                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐      │
│  │ PostgreSQL  │  │    Redis    │  │   NATS KV   │  │    Blob     │      │
│  │  (Primary)  │  │   (Cache)   │  │  (Seat Lock)│  │  (Storage)  │      │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘      │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Architecture Principles

| Principle | Description | Rationale |
|-----------|-------------|-----------|
| **Event-Driven** | 所有跨服務通訊透過事件 | 解耦、可擴展、可追溯 |
| **CQRS Light** | 命令與查詢分離（非完全分離） | 讀寫負載不同，分開優化 |
| **Actor Model** | 關鍵服務使用 Actor 模式 | 消除 Race Condition |
| **Partition-First** | 針對瓶頸做分片而非全面水平擴展 | 資源效率、降低複雜度 |
| **Abstraction-First** | 所有外部依賴透過介面抽象 | 可測試、可替換實作 |
| **Fail-Fast** | 快速失敗，明確回報 | 用戶體驗、問題定位 |

---

## 2. Component Architecture

### 2.1 API Gateway / Queue Gate

```
┌─────────────────────────────────────────────────────────────────┐
│                      API Gateway / Queue Gate                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────┐                                               │
│  │   Request    │                                               │
│  │   Ingress    │                                               │
│  └──────┬───────┘                                               │
│         │                                                       │
│         ▼                                                       │
│  ┌──────────────┐     ┌──────────────┐     ┌──────────────┐     │
│  │    Rate      │────►│     Bot      │────►│    Auth      │     │
│  │   Limiter    │     │  Detection   │     │  Middleware  │     │ 
│  │  (Redis)     │     │  (Behavior)  │     │   (JWT)      │     │
│  └──────────────┘     └──────────────┘     └───────┬──────┘     │
│                                                    │            │
│         ┌──────────────────────────────────────────┤            │
│         │                                          │            │
│         ▼                                          ▼            │
│  ┌──────────────┐                          ┌──────────────┐     │
│  │   Command    │                          │    Query     │     │
│  │   Handler    │                          │   Handler    │     │
│  │ (NATS Pub)   │                          │ (Direct Call)│     │
│  └──────────────┘                          └──────────────┘     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**職責：**
- 流量控制（Rate Limiting）
- Bot 行為偵測
- JWT 驗證
- 請求路由
- 命令發布（NATS）
- 查詢代理

**技術選型：**
- .NET 9 Minimal API
- Redis（分散式限流計數器）
- NATS Client

### 2.2 Ticket Service (Core - Partitioned)

```
┌─────────────────────────────────────────────────────────────────┐
│                       Ticket Service                            │
│                    (Partitioned Deployment)                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  NATS Consumer: seat.lock.request.{partition}                   │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                   Partition Router                      │    │
│  │        Hash(SessionId + AreaId) % NumPartitions         │    │
│  └──────────────────────────┬──────────────────────────────┘    │
│                             │                                   │
│     ┌───────────────────────┼───────────────────────┐           │
│     │                       │                       │           │
│     ▼                       ▼                       ▼           │
│  ┌─────────┐           ┌─────────┐           ┌─────────┐        │
│  │Partition│           │Partition│           │Partition│        │
│  │    0    │           │    1    │           │    N    │        │
│  │         │           │         │           │         │        │
│  │┌───────┐│           │┌───────┐│           │┌───────┐│        │
│  ││ Actor ││           ││ Actor ││           ││ Actor ││        │
│  ││ Loop  ││           ││ Loop  ││           ││ Loop  ││        │
│  ││(Single││           ││(Single││           ││(Single││        │
│  ││Thread)││           ││Thread)││           ││Thread)││        │
│  │└───────┘│           │└───────┘│           │└───────┘│        │
│  │    │    │           │    │    │           │    │    │        │
│  │    ▼    │           │    ▼    │           │    ▼    │        │
│  │┌───────┐│           │┌───────┐│           │┌───────┐│        │
│  ││ Seat  ││           ││ Seat  ││           ││ Seat  ││        │
│  ││ State ││           ││ State ││           ││ State ││        │
│  ││(Memory││           ││(Memory││           ││(Memory││        │
│  ││+Redis)││           ││+Redis)││           ││+Redis)││        │
│  │└───────┘│           │└───────┘│           │└───────┘│        │
│  └─────────┘           └─────────┘           └─────────┘        │
│                                                                 │
│  Output: seat.locked, seat.lock.failed, seat.released           │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**核心設計：**

1. **Partition 策略**
   ```csharp
   int partition = Math.Abs((sessionId + areaId).GetHashCode()) % numPartitions;
   ```

2. **Actor Loop（單線程處理）**
   ```csharp
   // 使用 Channel 實現 Actor Model
   var channel = Channel.CreateUnbounded<SeatLockRequest>();

   // 單一 consumer 確保順序處理
   await foreach (var request in channel.Reader.ReadAllAsync(ct))
   {
       await ProcessSeatLockAsync(request);
   }
   ```

3. **Seat State 管理**
   - 熱資料：In-Memory Dictionary
   - 持久化：Redis SET NX EX
   - 備援：NATS KV Store

**Invariant 保證：**
- 同一座位同一時間只有一個 Lock
- Lock 必定有 TTL
- 單線程處理消除 Race Condition

### 2.3 Order Service (Partitioned)

```
┌─────────────────────────────────────────────────────────────────┐
│                        Order Service                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  NATS Consumers:                                                │
│    - seat.locked (create order)                                 │
│    - payment.succeeded (complete order)                         │
│    - payment.failed (fail order)                                │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                   Order State Machine                   │    │
│  │                                                         │    │
│  │   Created ──► PaymentPending ──► Completed              │    │
│  │      │              │                                   │    │
│  │      ▼              ▼                                   │    │
│  │   Expired        Failed                                 │    │
│  │                                                         │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                 │
│  Partition: Hash(UserId) % NumPartitions                        │
│                                                                 │
│  Output: order.created, order.completed, order.expired          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 2.4 Payment Service (Stateless + Provider Abstraction)

```
┌─────────────────────────────────────────────────────────────────┐
│                       Payment Service                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  NATS Consumer: order.created                                   │
│  HTTP Endpoints: /webhook/{provider}                            │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                 IPaymentProvider                        │    │
│  │                                                         │    │
│  │  + CreatePaymentAsync(order)                            │    │
│  │  + VerifyPaymentAsync(transactionId)                    │    │
│  │  + RefundAsync(paymentId)                               │    │
│  │  + HandleWebhookAsync(payload)                          │    │
│  └─────────────────────────┬───────────────────────────────┘    │
│                            │                                    │
│        ┌───────────────────┼───────────────────┐                │
│        │                   │                   │                │
│        ▼                   ▼                   ▼                │
│  ┌──────────┐       ┌──────────┐       ┌──────────┐             │
│  │   Mock   │       │  Stripe  │       │ LinePay  │             │
│  │ Provider │       │ Provider │       │ Provider │             │
│  │          │       │  (ACL)   │       │  (ACL)   │             │
│  └──────────┘       └──────────┘       └──────────┘             │
│                                                                 │
│  Output: payment.succeeded, payment.failed                      │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 2.5 Notification Service (Fan-out)

```
┌─────────────────────────────────────────────────────────────────┐
│                    Notification Service                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  NATS Consumers (Fan-out mode):                                 │
│    - seat.locked, seat.released                                 │
│    - order.*, payment.*                                         │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                   SignalR Hub                           │    │
│  │                                                         │    │
│  │   Groups:                                               │    │
│  │     - session:{sessionId}:seats                         │    │
│  │     - user:{userId}:orders                              │    │
│  │                                                         │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                 │
│  Scale-out: Redis Backplane for SignalR                         │
│                                                                 │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐          │
│  │  Instance 1 │◄──►│    Redis    │◄──►│  Instance N │          │
│  │  (SignalR)  │    │  Backplane  │    │  (SignalR)  │          │
│  └─────────────┘    └─────────────┘    └─────────────┘          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Data Architecture

### 3.1 Data Store Selection

| Data Type | Store | Rationale |
|-----------|-------|-----------|
| User, Order, Payment | PostgreSQL | ACID, 關聯查詢 |
| Seat Lock State | Redis + Memory | 低延遲、TTL 支援 |
| Event Stream | NATS JetStream | 事件溯源、Replay |
| Session Config | NATS KV | 分散式配置 |
| Static Assets | Blob Storage | 座位圖圖片等 |
| Search Index | Elasticsearch | 活動搜尋（Phase 2）|

### 3.2 Database Schema (PostgreSQL)

```sql
-- Identity Context
CREATE TABLE users (
    user_id UUID PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    phone_number VARCHAR(50),
    display_name VARCHAR(100),
    password_hash VARCHAR(255),
    status VARCHAR(20) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Venue Context
CREATE TABLE venues (
    venue_id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    address JSONB NOT NULL,
    capacity INT NOT NULL,
    status VARCHAR(20) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE seat_maps (
    seat_map_id UUID PRIMARY KEY,
    venue_id UUID REFERENCES venues(venue_id),
    version INT NOT NULL,
    schema JSONB NOT NULL,  -- Venue Schema JSON
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Event Context
CREATE TABLE events (
    event_id UUID PRIMARY KEY,
    venue_id UUID REFERENCES venues(venue_id),
    name VARCHAR(200) NOT NULL,
    description TEXT,
    organizer_id UUID NOT NULL,
    status VARCHAR(20) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE sessions (
    session_id UUID PRIMARY KEY,
    event_id UUID REFERENCES events(event_id),
    start_time TIMESTAMPTZ NOT NULL,
    end_time TIMESTAMPTZ NOT NULL,
    sale_start_time TIMESTAMPTZ NOT NULL,
    sale_end_time TIMESTAMPTZ NOT NULL,
    status VARCHAR(20) NOT NULL
);

CREATE TABLE price_categories (
    category_id UUID PRIMARY KEY,
    event_id UUID REFERENCES events(event_id),
    area_ids TEXT[] NOT NULL,
    price_amount DECIMAL(10,2) NOT NULL,
    price_currency VARCHAR(3) NOT NULL,
    quota INT
);

-- Order Context
CREATE TABLE orders (
    order_id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    session_id UUID NOT NULL,
    total_amount DECIMAL(10,2) NOT NULL,
    total_currency VARCHAR(3) NOT NULL,
    status VARCHAR(20) NOT NULL,
    payment_id UUID,
    created_at TIMESTAMPTZ NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ
);

CREATE TABLE order_items (
    order_item_id UUID PRIMARY KEY,
    order_id UUID REFERENCES orders(order_id),
    seat_id VARCHAR(50) NOT NULL,
    price_amount DECIMAL(10,2) NOT NULL,
    price_currency VARCHAR(3) NOT NULL
);

-- Ticket Context
CREATE TABLE tickets (
    ticket_id UUID PRIMARY KEY,
    order_id UUID NOT NULL,
    session_id UUID NOT NULL,
    seat_id VARCHAR(50) NOT NULL,
    user_id UUID NOT NULL,
    price_amount DECIMAL(10,2) NOT NULL,
    price_currency VARCHAR(3) NOT NULL,
    status VARCHAR(20) NOT NULL,
    qr_code VARCHAR(255) UNIQUE NOT NULL,
    issued_at TIMESTAMPTZ NOT NULL,
    used_at TIMESTAMPTZ,
    UNIQUE(session_id, seat_id)  -- 防止 double-sell
);

-- Payment Context
CREATE TABLE payments (
    payment_id UUID PRIMARY KEY,
    order_id UUID NOT NULL,
    amount DECIMAL(10,2) NOT NULL,
    currency VARCHAR(3) NOT NULL,
    provider VARCHAR(50) NOT NULL,
    provider_transaction_id VARCHAR(255),
    status VARCHAR(20) NOT NULL,
    method VARCHAR(50),
    created_at TIMESTAMPTZ NOT NULL,
    processed_at TIMESTAMPTZ,
    failure_reason TEXT
);

-- Indexes
CREATE INDEX idx_orders_user_id ON orders(user_id);
CREATE INDEX idx_orders_session_id ON orders(session_id);
CREATE INDEX idx_orders_status ON orders(status);
CREATE INDEX idx_tickets_session_seat ON tickets(session_id, seat_id);
CREATE INDEX idx_tickets_user_id ON tickets(user_id);
CREATE INDEX idx_sessions_event_id ON sessions(event_id);
CREATE INDEX idx_sessions_sale_time ON sessions(sale_start_time, sale_end_time);
```

### 3.3 Redis Data Structures

```
# Seat Lock (String with TTL)
SET seat:lock:{sessionId}:{seatId} {userId} NX EX 120

# Rate Limiting (Sliding Window)
ZADD ratelimit:{userId} {timestamp} {requestId}
ZREMRANGEBYSCORE ratelimit:{userId} 0 {timestamp - window}
ZCARD ratelimit:{userId}

# Queue Position (Sorted Set)
ZADD queue:{sessionId} {timestamp} {userId}
ZRANK queue:{sessionId} {userId}

# Session Seat Cache (Hash)
HSET session:{sessionId}:seats {seatId} {status}
HGETALL session:{sessionId}:seats
```

### 3.4 NATS JetStream Streams

```yaml
streams:
  - name: SEATS
    subjects:
      - seat.lock.request.*
      - seat.locked.*
      - seat.lock.failed.*
      - seat.released.*
      - seat.expired.*
    retention: limits
    max_age: 7d
    storage: file
    replicas: 3

  - name: ORDERS
    subjects:
      - order.created.*
      - order.completed.*
      - order.expired.*
      - order.cancelled.*
    retention: limits
    max_age: 30d
    storage: file
    replicas: 3

  - name: PAYMENTS
    subjects:
      - payment.initiated.*
      - payment.succeeded.*
      - payment.failed.*
      - refund.*
    retention: limits
    max_age: 90d
    storage: file
    replicas: 3
```

---

## 4. Scaling Strategy

### 4.1 Scaling Decision Matrix

```
┌─────────────────────────────────────────────────────────────────┐
│                    Scaling Decision Flow                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│                    ┌─────────────────┐                          │
│                    │  Identify       │                          │
│                    │  Bottleneck     │                          │
│                    └────────┬────────┘                          │
│                             │                                   │
│           ┌─────────────────┼─────────────────┐                 │
│           │                 │                 │                 │
│           ▼                 ▼                 ▼                 │
│    ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            │
│    │   CPU/IO    │  │   Memory    │  │  Network    │            │
│    │   Bound     │  │   Bound     │  │   Bound     │            │
│    └──────┬──────┘  └──────┬──────┘  └───────┬─────┘            │
│           │                │                 │                  │
│           ▼                ▼                 ▼                  │
│    ┌─────────────┐  ┌─────────────┐  ┌─────────────┐            │
│    │  Horizontal │  │  Vertical   │  │   CDN /     │            │
│    │   Scale +   │  │   Scale /   │  │   Cache     │            │
│    │  Partition  │  │   Cache     │  │             │            │
│    └─────────────┘  └─────────────┘  └─────────────┘            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 Per-Service Scaling

| Service | Bottleneck | Strategy | Implementation |
|---------|------------|----------|----------------|
| **API Gateway** | Request throughput | Horizontal | K8s HPA, Load Balancer |
| **Ticket Service** | Seat lock contention | Partition | NATS Consumer Groups |
| **Order Service** | DB writes | Partition by UserId | Sharded Postgres / Partition |
| **Payment Service** | External API | Rate limiting | Queue + Retry |
| **Identity Service** | Auth requests | Horizontal | Stateless + Redis session |
| **Notification** | WebSocket connections | Fan-out | Redis Backplane |
| **Database** | Read/Write | Read Replica + Sharding | PgBouncer + Citus |

### 4.3 Partition Implementation (Ticket Service)

```csharp
public class PartitionedTicketService
{
    private readonly int _numPartitions;
    private readonly Channel<SeatLockRequest>[] _partitionChannels;

    public PartitionedTicketService(int numPartitions)
    {
        _numPartitions = numPartitions;
        _partitionChannels = new Channel<SeatLockRequest>[numPartitions];

        for (int i = 0; i < numPartitions; i++)
        {
            _partitionChannels[i] = Channel.CreateUnbounded<SeatLockRequest>();
            StartPartitionProcessor(i);
        }
    }

    public async Task EnqueueLockRequest(SeatLockRequest request)
    {
        int partition = GetPartition(request.SessionId, request.AreaId);
        await _partitionChannels[partition].Writer.WriteAsync(request);
    }

    private int GetPartition(Guid sessionId, string areaId)
    {
        var key = $"{sessionId}:{areaId}";
        return Math.Abs(key.GetHashCode()) % _numPartitions;
    }

    private void StartPartitionProcessor(int partitionId)
    {
        Task.Run(async () =>
        {
            await foreach (var request in _partitionChannels[partitionId].Reader.ReadAllAsync())
            {
                // Single-threaded processing - no race condition
                await ProcessSeatLockAsync(request);
            }
        });
    }
}
```

---

## 5. Multi-Region Architecture (Phase 3)

### 5.1 NATS Leafnode Topology

```
                         ┌─────────────────┐
                         │   Global Hub    │
                         │  (US-Central)   │
                         │  NATS Cluster   │
                         └────────┬────────┘
                                  │
           ┌──────────────────────┼──────────────────────┐
           │                      │                      │
           ▼                      ▼                      ▼
   ┌───────────────┐     ┌───────────────┐     ┌───────────────┐
   │  Asia-Pacific │     │    Europe     │     │  US-East/West │
   │   Leafnode    │     │   Leafnode    │     │   Leafnode    │
   │               │     │               │     │               │
   │ ┌───────────┐ │     │ ┌───────────┐ │     │ ┌───────────┐ │
   │ │  Ticket   │ │     │ │  Ticket   │ │     │ │  Ticket   │ │
   │ │  Service  │ │     │ │  Service  │ │     │ │  Service  │ │
   │ │(Partition)│ │     │ │(Partition)│ │     │ │(Partition)│ │
   │ └───────────┘ │     │ └───────────┘ │     │ └───────────┘ │
   │               │     │               │     │               │
   │ ┌───────────┐ │     │ ┌───────────┐ │     │ ┌───────────┐ │
   │ │   Redis   │ │     │ │   Redis   │ │     │ │   Redis   │ │
   │ │  (Local)  │ │     │ │  (Local)  │ │     │ │  (Local)  │ │
   │ └───────────┘ │     │ └───────────┘ │     │ └───────────┘ │
   └───────────────┘     └───────────────┘     └───────────────┘
```

### 5.2 Event Routing Rules

```yaml
# Events that stay local
local_events:
  - seat.lock.request  # Process locally first
  - queue.ticket       # Local queue management

# Events that replicate globally
global_events:
  - seat.locked        # All regions need to know
  - seat.sold          # Inventory sync
  - order.completed    # Audit trail

# Region-specific routing
routing:
  - pattern: "seat.lock.request.asia.*"
    target: asia-pacific
  - pattern: "seat.lock.request.eu.*"
    target: europe
```

---

## 6. Security Architecture

### 6.1 Security Layers

```
┌─────────────────────────────────────────────────────────────────┐
│                      Security Layers                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Layer 1: Edge (CDN/WAF)                                        │
│  ├── DDoS Protection                                            │
│  ├── WAF Rules                                                  │
│  └── SSL/TLS Termination                                        │
│                                                                 │
│  Layer 2: API Gateway                                           │
│  ├── Rate Limiting (per user, per IP)                           │
│  ├── Bot Detection (behavior analysis)                          │
│  ├── JWT Validation                                             │
│  └── Request Sanitization                                       │
│                                                                 │
│  Layer 3: Service                                               │
│  ├── Authorization (RBAC)                                       │
│  ├── Input Validation                                           │
│  └── Audit Logging                                              │
│                                                                 │
│  Layer 4: Data                                                  │
│  ├── Encryption at Rest                                         │
│  ├── Encryption in Transit (mTLS)                               │
│  └── Data Masking (PII)                                         │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 6.2 Authentication Flow

```
┌─────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────┐
│  Client │────►│ API Gateway │────►│  Identity   │────►│   JWT   │
│         │     │             │     │  Service    │     │  Store  │
└─────────┘     └──────┬──────┘     └─────────────┘     └─────────┘
                       │
                       │ JWT Token
                       ▼
              ┌─────────────────┐
              │  Other Services │
              │  (JWT Validate) │
              └─────────────────┘
```

---

## 7. Observability

### 7.1 Monitoring Stack

```
┌─────────────────────────────────────────────────────────────────┐
│                    Observability Stack                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Metrics (Prometheus + Grafana)                                 │
│  ├── Request latency (P50, P95, P99)                            │
│  ├── Seat lock success rate                                     │
│  ├── Order completion rate                                      │
│  ├── Queue depth                                                │
│  └── Error rates                                                │
│                                                                 │
│  Logging (Seq / ELK)                                            │
│  ├── Structured logging                                         │
│  ├── Correlation ID tracking                                    │
│  └── Log aggregation                                            │
│                                                                 │
│  Tracing (OpenTelemetry + Jaeger)                               │
│  ├── Distributed tracing                                        │
│  ├── Service dependency mapping                                 │
│  └── Performance bottleneck identification                      │
│                                                                 │
│  Alerting (AlertManager / PagerDuty)                            │
│  ├── SLA breach alerts                                          │
│  ├── Error rate spikes                                          │
│  └── Capacity warnings                                          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 7.2 Key Metrics Dashboard

| Category | Metric | Alert Threshold |
|----------|--------|-----------------|
| **Availability** | Uptime | < 99.9% |
| **Performance** | Seat Lock P99 | > 100ms |
| **Performance** | API Response P99 | > 500ms |
| **Business** | Double-Sell Count | > 0 |
| **Business** | Order Success Rate | < 95% |
| **Capacity** | Queue Depth | > 10000 |
| **Capacity** | WebSocket Connections | > 80% capacity |

---

## 8. Deployment Architecture

### 8.1 Kubernetes Deployment

```yaml
# Example: Ticket Service Deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ticket-service
spec:
  replicas: 3  # One per partition
  selector:
    matchLabels:
      app: ticket-service
  template:
    metadata:
      labels:
        app: ticket-service
    spec:
      containers:
      - name: ticket-service
        image: openticket/ticket-service:latest
        env:
        - name: PARTITION_ID
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: NUM_PARTITIONS
          value: "3"
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "1Gi"
            cpu: "1000m"
```

### 8.2 CI/CD Pipeline

```
┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐
│  Code   │───►│  Build  │───►│  Test   │───►│ Staging │───►│  Prod   │
│  Push   │    │  & Lint │    │  Suite  │    │ Deploy  │    │ Deploy  │
└─────────┘    └─────────┘    └─────────┘    └─────────┘    └─────────┘
                                   │
                    ┌──────────────┼──────────────┐
                    │              │              │
                    ▼              ▼              ▼
              ┌──────────┐  ┌────────────┐  ┌──────────┐
              │   Unit   │  │ Integration│  │  Load    │
              │  Tests   │  │   Tests    │  │  Tests   │
              └──────────┘  └────────────┘  └──────────┘
```
