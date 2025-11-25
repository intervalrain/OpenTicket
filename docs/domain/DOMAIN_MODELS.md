# OpenTicket - Domain Models & Bounded Contexts

**Version:** 1.0
**Last Updated:** 2025-01

---

## 1. Strategic Domain Design

### 1.1 Domain Classification

```
┌─────────────────────────────────────────────────────────────────┐
│                      Domain Classification                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│   Core Domain                                                   │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │  Ticket Context - Seat Lock, Avoid Double Sell          │   │
│   │  Queue Context  - Traffic Controll, Anti-Bot            │   │
│   └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│   Supporting Domain                                             │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │  Venue Context  - Seat Arrangement for Places           │   │
│   │  Event Context  - Activity Management                   │   │
│   │  Order Context  - Order Management                      │   │
│   └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│   Generic Domain                                                │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │  Identity Context     - Auth(3rd party integration)     │   │
│   │  Payment Context      - Payment(3rd party integration)  │   │
│   │  Notification Context - Broadcast                       │   │
│   └─────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 Context Map

```
                       ┌────────────────────────────┐
                       │       API Gateway          │
                       │   (Anti-corruption Layer)  │
                       └─────────────┬──────────────┘
                                     │
        ┌────────────────────────────┼───────────────────────────┐
        │                            │                           │
        ▼                            ▼                           ▼
┌───────────────┐            ┌───────────────┐           ┌───────────────┐
│    Queue      │            │   Identity    │           │ Notification  │
│   Context     │            │   Context     │           │   Context     │
│  (Upstream)   │            │  (Upstream)   │           │ (Downstream)  │
└───────┬───────┘            └───────┬───────┘           └───────▲───────┘
        │                            │                           │
        │ Published                  │ Customer/                 │ Published
        │ Language                   │ Supplier                  │ Language
        │                            │                           │
        ▼                            ▼                           │
┌───────────────┐            ┌───────────────┐                   │
│    Venue      │◄───── ────►│    Event      │                   │
│   Context     │ Partnership│   Context     │                   │
│               │            │               │                   │
└───────┬───────┘            └───────┬───────┘                   │
        │                            │                           │
        │ Conformist                 │ Conformist                │
        │                            │                           │
        ▼                            ▼                           │
┌─────────────────────────────────────────────────────┐          │
│                  Ticket Context                     │──────────┤
│                  (Core Domain)                      │          │
│         ┌─────────────────────────────┐             │          │
│         │   Seat Lock Aggregate       │             │          │
│         │   (Single Source of Truth)  │             │          │
│         └─────────────────────────────┘             │          │
└───────────────────────┬─────────────────────────────┘          │
                        │                                        │
                        │ Customer/Supplier                      │
                        ▼                                        │
                ┌───────────────┐                                │
                │    Order      │────────────────────────────────┤
                │   Context     │                                │
                └───────┬───────┘                                │
                        │                                        │
                        │ Customer/Supplier                      │
                        ▼                                        │
                ┌───────────────┐                                │
                │   Payment     │────────────────────────────────┘
                │   Context     │
                │  (ACL to      │
                │   External)   │
                └───────────────┘
```

---

## 2. Bounded Context Details

### 2.1 Venue Context

**職責：** 管理場館與座位圖定義

#### Aggregates

```
┌─────────────────────────────────────────────────────────┐
│                    Venue Aggregate                      │
├─────────────────────────────────────────────────────────┤
│  Venue (Aggregate Root)                                 │
│  ├── VenueId: Guid                                      │
│  ├── Name: string                                       │
│  ├── Address: Address (Value Object)                    │
│  ├── Capacity: int                                      │
│  ├── SeatMap: SeatMap (Entity)                          │
│  │   ├── SeatMapId: Guid                                │
│  │   ├── Version: int                                   │
│  │   └── Areas: List<Area>                              │
│  │       ├── AreaId: string                             │
│  │       ├── Name: string                               │
│  │       └── Seats: List<SeatDefinition>                │
│  │           ├── SeatId: string                         │
│  │           ├── Row: string                            │
│  │           ├── Number: int                            │
│  │           ├── X: int                                 │
│  │           ├── Y: int                                 │
│  │           └── Type: SeatType                         │
│  └── Status: VenueStatus                                │
└─────────────────────────────────────────────────────────┘
```

#### Domain Events

| Event | Description | Consumers |
|-------|-------------|-----------|
| `VenueCreated` | 場館建立 | Event Context |
| `SeatMapUpdated` | 座位圖更新 | Event Context, Ticket Context |

#### Value Objects

```csharp
public record Address(string Country, string City, string Street, string PostalCode);
public record SeatDefinition(string SeatId, string Row, int Number, int X, int Y, SeatType Type);
public enum SeatType { Normal, Wheelchair, Vip, Restricted }
public enum VenueStatus { Draft, Active, Inactive }
```

---

### 2.2 Event Context

**職責：** 管理活動、場次與票價

#### Aggregates

```
┌─────────────────────────────────────────────────────────┐
│                    Event Aggregate                      │
├─────────────────────────────────────────────────────────┤
│  Event (Aggregate Root)                                 │
│  ├── EventId: Guid                                      │
│  ├── Name: string                                       │
│  ├── Description: string                                │
│  ├── VenueId: Guid (Reference)                          │
│  ├── OrganizerId: Guid                                  │
│  ├── Sessions: List<Session> (Entity)                   │
│  │   ├── SessionId: Guid                                │
│  │   ├── StartTime: DateTime                            │
│  │   ├── EndTime: DateTime                              │
│  │   ├── SaleStartTime: DateTime                        │
│  │   ├── SaleEndTime: DateTime                          │
│  │   └── Status: SessionStatus                          │
│  ├── PriceCategories: List<PriceCategory>               │
│  │   ├── CategoryId: string                             │
│  │   ├── AreaIds: List<string>                          │
│  │   ├── Price: Money (Value Object)                    │
│  │   └── Quota: int?                                    │
│  └── Status: EventStatus                                │
└─────────────────────────────────────────────────────────┘
```

#### Domain Events

| Event | Description | Consumers |
|-------|-------------|-----------|
| `EventCreated` | 活動建立 | Notification |
| `SessionCreated` | 場次建立 | Ticket Context |
| `SaleStarted` | 開賣開始 | Queue Context, Notification |
| `SaleEnded` | 銷售結束 | Ticket Context |

#### Value Objects

```csharp
public record Money(decimal Amount, string Currency);
public enum SessionStatus { Draft, Scheduled, OnSale, SoldOut, Ended, Cancelled }
public enum EventStatus { Draft, Published, Cancelled, Completed }
```

---

### 2.3 Ticket Context (Core Domain)

**職責：** 座位鎖定、票券狀態管理（**最核心，防 Double-Sell**）

#### Aggregates

```
┌─────────────────────────────────────────────────────────┐
│                 SeatInventory Aggregate                 │
│            (Partitioned by SessionId + AreaId)          │
├─────────────────────────────────────────────────────────┤
│  SeatInventory (Aggregate Root)                         │
│  ├── SessionId: Guid                                    │
│  ├── AreaId: string                                     │
│  ├── Seats: Dictionary<string, SeatState>               │
│  │   └── SeatState                                      │
│  │       ├── SeatId: string                             │
│  │       ├── Status: SeatStatus                         │
│  │       ├── LockedBy: Guid? (UserId)                   │
│  │       ├── LockedAt: DateTime?                        │
│  │       ├── LockExpiry: DateTime?                      │
│  │       └── OrderId: Guid?                             │
│  └── Version: long (Optimistic Concurrency)             │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│                   Ticket Aggregate                      │
├─────────────────────────────────────────────────────────┤
│  Ticket (Aggregate Root)                                │
│  ├── TicketId: Guid                                     │
│  ├── OrderId: Guid                                      │
│  ├── SessionId: Guid                                    │
│  ├── SeatId: string                                     │
│  ├── UserId: Guid                                       │
│  ├── Price: Money                                       │
│  ├── Status: TicketStatus                               │
│  ├── QrCode: string                                     │
│  ├── IssuedAt: DateTime                                 │
│  └── UsedAt: DateTime?                                  │
└─────────────────────────────────────────────────────────┘
```

#### Domain Events (Critical)

| Event | Description | Consumers |
|-------|-------------|-----------|
| `SeatLockRequested` | 座位鎖定請求 | Ticket Service (Internal) |
| `SeatLocked` | 座位鎖定成功 | Order Context, Notification |
| `SeatLockFailed` | 座位鎖定失敗 | Notification |
| `SeatLockExpired` | 鎖定逾時 | Notification |
| `SeatReleased` | 座位釋放 | Notification |
| `SeatSold` | 座位售出 | Notification |
| `TicketIssued` | 票券發行 | Order Context, Notification |
| `TicketUsed` | 票券使用（入場） | Notification |

#### Invariants (業務規則)

```
1. 同一座位同一時間只能被一人鎖定
2. 鎖定有 TTL（預設 120 秒），逾時自動釋放
3. 已售出座位不可再鎖定
4. 只有鎖定該座位的用戶可以購買
5. 票券 QR Code 必須唯一且不可預測
```

#### Value Objects

```csharp
public enum SeatStatus { Available, Locked, Sold, Unavailable }
public enum TicketStatus { Issued, Used, Cancelled, Refunded }
```

---

### 2.4 Identity Context

**職責：** 用戶身份驗證與管理

#### Aggregates

```
┌─────────────────────────────────────────────────────────┐
│                    User Aggregate                       │
├─────────────────────────────────────────────────────────┤
│  User (Aggregate Root)                                  │
│  ├── UserId: Guid                                       │
│  ├── Email: string                                      │
│  ├── PhoneNumber: string?                               │
│  ├── DisplayName: string                                │
│  ├── PasswordHash: string?                              │
│  ├── ExternalLogins: List<ExternalLogin>                │
│  │   ├── Provider: string                               │
│  │   └── ProviderKey: string                            │
│  ├── IdentityVerification: IdentityVerification?        │
│  │   ├── Type: VerificationType                         │
│  │   ├── VerifiedAt: DateTime                           │
│  │   └── DocumentNumber: string (Encrypted)             │
│  ├── Status: UserStatus                                 │
│  └── CreatedAt: DateTime                                │
└─────────────────────────────────────────────────────────┘
```

#### Domain Events

| Event | Description | Consumers |
|-------|-------------|-----------|
| `UserRegistered` | 用戶註冊 | Notification |
| `UserVerified` | 身份驗證完成 | Order Context |
| `UserSuspended` | 用戶停權 | Queue Context |

---

### 2.5 Order Context

**職責：** 訂單生命週期管理

#### Aggregates

```
┌─────────────────────────────────────────────────────────┐
│                    Order Aggregate                      │
├─────────────────────────────────────────────────────────┤
│  Order (Aggregate Root)                                 │
│  ├── OrderId: Guid                                      │
│  ├── UserId: Guid                                       │
│  ├── SessionId: Guid                                    │
│  ├── Items: List<OrderItem>                             │
│  │   ├── SeatId: string                                 │
│  │   ├── Price: Money                                   │
│  │   └── Status: OrderItemStatus                        │
│  ├── TotalAmount: Money                                 │
│  ├── Status: OrderStatus                                │
│  ├── PaymentId: Guid?                                   │
│  ├── CreatedAt: DateTime                                │
│  ├── ExpiresAt: DateTime                                │
│  └── CompletedAt: DateTime?                             │
└─────────────────────────────────────────────────────────┘
```

#### State Machine

```
                    ┌─────────┐
                    │ Created │
                    └────┬────┘
                         │
            ┌────────────┼────────────┐
            │            │            │
            ▼            │            ▼
    ┌───────────┐        │     ┌───────────┐
    │  Expired  │        │     │ Cancelled │
    └───────────┘        │     └───────────┘
                         │
                         ▼
                 ┌───────────────┐
                 │ PaymentPending│
                 └───────┬───────┘
                         │
            ┌────────────┼────────────┐
            │            │            │
            ▼            ▼            ▼
    ┌───────────┐ ┌───────────┐ ┌───────────┐
    │  Failed   │ │ Completed │ │  Refunded │
    └───────────┘ └───────────┘ └───────────┘
```

#### Domain Events

| Event | Description | Consumers |
|-------|-------------|-----------|
| `OrderCreated` | 訂單建立 | Payment Context |
| `OrderExpired` | 訂單逾時 | Ticket Context (Release Seats) |
| `OrderCompleted` | 訂單完成 | Ticket Context (Issue Tickets), Notification |
| `OrderCancelled` | 訂單取消 | Ticket Context, Payment Context |
| `OrderRefunded` | 訂單退款 | Ticket Context, Notification |

---

### 2.6 Payment Context

**職責：** 支付處理（外部系統整合）

#### Aggregates

```
┌─────────────────────────────────────────────────────────┐
│                   Payment Aggregate                     │
├─────────────────────────────────────────────────────────┤
│  Payment (Aggregate Root)                               │
│  ├── PaymentId: Guid                                    │
│  ├── OrderId: Guid                                      │
│  ├── Amount: Money                                      │
│  ├── Provider: string                                   │
│  ├── ProviderTransactionId: string?                     │
│  ├── Status: PaymentStatus                              │
│  ├── Method: PaymentMethod?                             │
│  ├── CreatedAt: DateTime                                │
│  ├── ProcessedAt: DateTime?                             │
│  └── FailureReason: string?                             │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│                   Refund Aggregate                      │
├─────────────────────────────────────────────────────────┤
│  Refund (Aggregate Root)                                │
│  ├── RefundId: Guid                                     │
│  ├── PaymentId: Guid                                    │
│  ├── Amount: Money                                      │
│  ├── Reason: string                                     │
│  ├── Status: RefundStatus                               │
│  └── ProcessedAt: DateTime?                             │
└─────────────────────────────────────────────────────────┘
```

#### Domain Events

| Event | Description | Consumers |
|-------|-------------|-----------|
| `PaymentInitiated` | 支付發起 | - |
| `PaymentSucceeded` | 支付成功 | Order Context |
| `PaymentFailed` | 支付失敗 | Order Context, Notification |
| `RefundInitiated` | 退款發起 | - |
| `RefundCompleted` | 退款完成 | Order Context, Notification |

---

### 2.7 Queue Context

**職責：** 流量控制、排隊、防 Bot

#### Aggregates

```
┌─────────────────────────────────────────────────────────┐
│                QueueSession Aggregate                   │
├─────────────────────────────────────────────────────────┤
│  QueueSession (Aggregate Root)                          │
│  ├── SessionId: Guid (Event Session)                    │
│  ├── Config: QueueConfig                                │
│  │   ├── MaxConcurrentUsers: int                        │
│  │   ├── RequestsPerSecond: int                         │
│  │   └── EnableProofOfWork: bool                        │
│  ├── CurrentLoad: int                                   │
│  └── Status: QueueStatus                                │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│                 QueueTicket Aggregate                   │
├─────────────────────────────────────────────────────────┤
│  QueueTicket (Aggregate Root)                           │
│  ├── TicketId: Guid                                     │
│  ├── SessionId: Guid                                    │
│  ├── UserId: Guid                                       │
│  ├── Position: int                                      │
│  ├── Status: QueueTicketStatus                          │
│  ├── IssuedAt: DateTime                                 │
│  ├── ActivatedAt: DateTime?                             │
│  └── ExpiresAt: DateTime                                │
└─────────────────────────────────────────────────────────┘
```

---

### 2.8 Notification Context

**職責：** 即時通知、推播

#### Aggregates

```
┌─────────────────────────────────────────────────────────┐
│              NotificationSubscription                   │
├─────────────────────────────────────────────────────────┤
│  Subscription (Aggregate Root)                          │
│  ├── SubscriptionId: Guid                               │
│  ├── UserId: Guid                                       │
│  ├── ConnectionId: string (SignalR)                     │
│  ├── SubscribedTopics: List<string>                     │
│  │   (e.g., "session:{id}:seats", "order:{id}")         │
│  └── ConnectedAt: DateTime                              │
└─────────────────────────────────────────────────────────┘
```

---

## 3. Integration Events (跨 Context 通訊)

### 3.1 Event Naming Convention

```
{Context}.{Aggregate}.{Action}
```

Examples:
- `ticket.seat.locked`
- `order.order.created`
- `payment.payment.succeeded`

### 3.2 Event Schema (Protobuf-style)

```protobuf
message SeatLockedEvent {
  string event_id = 1;
  string timestamp = 2;
  string session_id = 3;
  string seat_id = 4;
  string user_id = 5;
  string lock_expiry = 6;
}

message OrderCreatedEvent {
  string event_id = 1;
  string timestamp = 2;
  string order_id = 3;
  string user_id = 4;
  string session_id = 5;
  repeated string seat_ids = 6;
  MoneyValue total_amount = 7;
}

message MoneyValue {
  string amount = 1;
  string currency = 2;
}
```

### 3.3 Event Flow Examples

#### 購票流程

```
User selects seat
       │
       ▼
┌─────────────────┐     seat.lock.requested      ┌─────────────────┐
│  API Gateway    │─────────────────────────────►│ Ticket Service  │
└─────────────────┘                              └────────┬────────┘
                                                          │
                                          seat.locked     │
                              ┌───────────────────────────┴───────┐
                              │                                   │
                              ▼                                   ▼
                    ┌─────────────────┐                 ┌─────────────────┐
                    │ Order Service   │                 │ Notification    │
                    │ (create order)  │                 │ (push to UI)    │
                    └────────┬────────┘                 └─────────────────┘
                             │
                             │ order.created
                             ▼
                    ┌─────────────────┐
                    │ Payment Service │
                    └────────┬────────┘
                             │
                             │ payment.succeeded
                             ▼
                    ┌─────────────────┐     ticket.issued      ┌─────────────────┐
                    │ Order Service   │───────────────────────►│ Notification    │
                    │ (complete order)│                        └─────────────────┘
                    └────────┬────────┘
                             │
                             │ order.completed
                             ▼
                    ┌─────────────────┐
                    │ Ticket Service  │
                    │ (issue tickets) │
                    └─────────────────┘
```

---

## 4. Horizontal Scaling Strategy

### 4.1 Per-Context Scaling

| Context | Scaling Strategy | Partition Key | Bottleneck |
|---------|------------------|---------------|------------|
| **Ticket** | Partition | `SessionId + AreaId` | Seat Lock 競爭 |
| **Order** | Partition | `UserId` | 寫入量 |
| **Payment** | Horizontal | N/A (Stateless) | 外部 API |
| **Identity** | Horizontal | N/A (Stateless) | 登入驗證峰值 |
| **Queue** | Horizontal + Redis | N/A | 入口流量 |
| **Notification** | Fan-out | `SessionId` | WebSocket 連線數 |
| **Venue** | Read Replica | N/A | 讀取量 |
| **Event** | Read Replica | N/A | 讀取量 |

### 4.2 Ticket Context Partitioning (詳細)

```
                         NATS JetStream
                              │
                    seat.lock.request.*
                              │
              ┌───────────────┼───────────────┐
              │               │               │
              ▼               ▼               ▼
     ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
     │ Partition 0 │  │ Partition 1 │  │ Partition 2 │
     │ (hash % 3=0)│  │ (hash % 3=1)│  │ (hash % 3=2)│
     │             │  │             │  │             │
     │ Actor Loop  │  │ Actor Loop  │  │ Actor Loop  │
     │ (Single     │  │ (Single     │  │ (Single     │
     │  Thread)    │  │  Thread)    │  │  Thread)    │
     └─────────────┘  └─────────────┘  └─────────────┘

Partition Key: Hash(SessionId + AreaId) % NumPartitions
```

**設計原則：**
1. 同一區域的座位由同一 Partition 處理
2. 單一 Partition 內單線程處理，消除 Race Condition
3. Partition 數量可動態調整（水平擴展）

---

## 5. Anti-Corruption Layer (ACL)

### 5.1 External Payment Providers

```
┌─────────────────────────────────────────────────────────┐
│                   Payment Context                       │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌─────────────────────────────────────────────────┐    │
│  │              IPaymentProvider                   │    │
│  │  + InitiatePaymentAsync()                       │    │
│  │  + VerifyPaymentAsync()                         │    │
│  │  + RefundAsync()                                │    │
│  └──────────────────────┬──────────────────────────┘    │
│                         │                               │
│         ┌───────────────┼───────────────┐               │
│         │               │               │               │
│         ▼               ▼               ▼               │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐        │
│  │   Stripe    │ │   LinePay   │ │    Mock     │        │
│  │   Adapter   │ │   Adapter   │ │   Adapter   │        │
│  │   (ACL)     │ │   (ACL)     │ │             │        │
│  └──────┬──────┘ └──────┬──────┘ └─────────────┘        │
│         │               │                               │
└─────────┼───────────────┼───────────────────────────────┘
          │               │
          ▼               ▼
    ┌───────────┐   ┌───────────┐
    │  Stripe   │   │  LinePay  │
    │   API     │   │   API     │
    └───────────┘   └───────────┘
```

### 5.2 External Identity Providers

```
┌─────────────────────────────────────────────────────────┐
│                   Identity Context                      │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌─────────────────────────────────────────────────┐    │
│  │              IIdentityProvider                  │    │
│  │  + ValidateAsync()                              │    │
│  │  + GetUserInfoAsync()                           │    │
│  └──────────────────────┬──────────────────────────┘    │
│                         │                               │
│    ┌────────────┬───────┼───────┬────────────┐          │
│    │            │       │       │            │          │
│    ▼            ▼       ▼       ▼            ▼          │
│ ┌──────┐   ┌──────┐ ┌──────┐ ┌──────┐   ┌──────┐        │
│ │Google│   │Apple │ │ SMS  │ │ ID   │   │ Mock │        │
│ │OAuth │   │OAuth │ │ OTP  │ │Verify│   │      │        │
│ └──────┘   └──────┘ └──────┘ └──────┘   └──────┘        │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## 6. Ubiquitous Language (統一語言)

| Term (EN) | Term (ZH) | Definition |
|-----------|-----------|------------|
| Venue | 場館 | 舉辦活動的實體場所 |
| Seat Map | 座位圖 | 場館的座位配置定義 |
| Event | 活動 | 演唱會、球賽等售票活動 |
| Session | 場次 | 活動的特定日期時間場次 |
| Seat Lock | 座位鎖定 | 暫時保留座位供用戶結帳 |
| Lock TTL | 鎖定時限 | 座位鎖定的有效時間 |
| Order | 訂單 | 用戶的購票訂單 |
| Ticket | 票券 | 已購買的入場憑證 |
| Queue Ticket | 排隊號碼 | 虛擬排隊的順序憑證 |
| Double-Sell | 重複售票 | 同一座位賣給多人的錯誤 |
| Burst Traffic | 洪峰流量 | 開賣瞬間的大量請求 |
