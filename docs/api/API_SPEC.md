# OpenTicket - API Specification

**Version:** 1.0
**Base URL:** `https://api.openticket.io/v1`
**Last Updated:** 2025-01

---

## 1. API Design Principles

### 1.1 RESTful Conventions

| Method | Usage | Idempotent |
|--------|-------|------------|
| GET | 讀取資源 | Yes |
| POST | 建立資源 / 執行動作 | No |
| PUT | 完整更新資源 | Yes |
| PATCH | 部分更新資源 | Yes |
| DELETE | 刪除資源 | Yes |

### 1.2 Response Format

**Success Response:**
```json
{
  "data": { ... },
  "meta": {
    "requestId": "uuid",
    "timestamp": "2025-01-20T10:00:00Z"
  }
}
```

**Error Response:**
```json
{
  "error": {
    "code": "ERROR_CODE",
    "message": "Human readable message",
    "details": { ... }
  },
  "meta": {
    "requestId": "uuid",
    "timestamp": "2025-01-20T10:00:00Z"
  }
}
```

### 1.3 Common Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `VALIDATION_ERROR` | 400 | 請求資料驗證失敗 |
| `UNAUTHORIZED` | 401 | 未認證 |
| `FORBIDDEN` | 403 | 無權限 |
| `NOT_FOUND` | 404 | 資源不存在 |
| `CONFLICT` | 409 | 資源衝突 |
| `RATE_LIMITED` | 429 | 超過頻率限制 |
| `INTERNAL_ERROR` | 500 | 內部錯誤 |

---

## 2. Authentication

### 2.1 JWT Token

所有需要認證的 API 都需要在 Header 帶上 JWT Token：

```http
Authorization: Bearer <jwt_token>
```

### 2.2 Token Endpoints

#### POST /auth/login

登入取得 Token。

**Request:**
```json
{
  "email": "user@example.com",
  "password": "********"
}
```

**Response:**
```json
{
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "dGhpcyBpcyBhIHJlZnJl...",
    "expiresIn": 3600,
    "tokenType": "Bearer"
  }
}
```

#### POST /auth/oauth/{provider}

OAuth 登入（Google, Apple）。

**Request:**
```json
{
  "code": "oauth_authorization_code",
  "redirectUri": "https://app.openticket.io/callback"
}
```

#### POST /auth/refresh

刷新 Token。

**Request:**
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJl..."
}
```

---

## 3. Venue API

### 3.1 GET /venues

取得場館列表。

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| page | int | No | 頁碼（預設 1） |
| pageSize | int | No | 每頁筆數（預設 20，最大 100） |
| city | string | No | 城市篩選 |

**Response:**
```json
{
  "data": {
    "items": [
      {
        "venueId": "550e8400-e29b-41d4-a716-446655440000",
        "name": "台北小巨蛋",
        "address": {
          "country": "TW",
          "city": "台北市",
          "street": "南京東路四段2號",
          "postalCode": "105"
        },
        "capacity": 15000,
        "status": "Active"
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "totalItems": 50,
      "totalPages": 3
    }
  }
}
```

### 3.2 GET /venues/{venueId}

取得場館詳情與座位圖。

**Response:**
```json
{
  "data": {
    "venueId": "550e8400-e29b-41d4-a716-446655440000",
    "name": "台北小巨蛋",
    "address": { ... },
    "capacity": 15000,
    "seatMap": {
      "seatMapId": "660e8400-e29b-41d4-a716-446655440001",
      "version": 1,
      "areas": [
        {
          "areaId": "A",
          "name": "A 區",
          "seats": [
            {
              "seatId": "A-001",
              "row": "1",
              "number": 1,
              "x": 100,
              "y": 200,
              "type": "Normal"
            }
          ]
        }
      ]
    }
  }
}
```

### 3.3 POST /venues (Admin)

建立場館。

**Request:**
```json
{
  "name": "台北小巨蛋",
  "address": {
    "country": "TW",
    "city": "台北市",
    "street": "南京東路四段2號",
    "postalCode": "105"
  },
  "capacity": 15000,
  "seatMap": {
    "areas": [
      {
        "areaId": "A",
        "name": "A 區",
        "seats": [
          {
            "seatId": "A-001",
            "row": "1",
            "number": 1,
            "x": 100,
            "y": 200,
            "type": "Normal"
          }
        ]
      }
    ]
  }
}
```

---

## 4. Event API

### 4.1 GET /events

取得活動列表。

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| page | int | No | 頁碼 |
| pageSize | int | No | 每頁筆數 |
| venueId | uuid | No | 場館篩選 |
| status | string | No | 狀態篩選（Published, OnSale） |
| startDate | date | No | 開始日期篩選 |
| endDate | date | No | 結束日期篩選 |

**Response:**
```json
{
  "data": {
    "items": [
      {
        "eventId": "770e8400-e29b-41d4-a716-446655440000",
        "name": "五月天演唱會",
        "description": "2025 巡迴演唱會",
        "venueId": "550e8400-e29b-41d4-a716-446655440000",
        "venueName": "台北小巨蛋",
        "status": "OnSale",
        "sessions": [
          {
            "sessionId": "880e8400-e29b-41d4-a716-446655440000",
            "startTime": "2025-03-15T19:00:00+08:00",
            "status": "OnSale",
            "availableSeats": 5000
          }
        ],
        "priceRange": {
          "min": { "amount": 1800, "currency": "TWD" },
          "max": { "amount": 6800, "currency": "TWD" }
        }
      }
    ]
  }
}
```

### 4.2 GET /events/{eventId}

取得活動詳情。

**Response:**
```json
{
  "data": {
    "eventId": "770e8400-e29b-41d4-a716-446655440000",
    "name": "五月天演唱會",
    "description": "2025 巡迴演唱會",
    "venue": {
      "venueId": "550e8400-e29b-41d4-a716-446655440000",
      "name": "台北小巨蛋"
    },
    "sessions": [
      {
        "sessionId": "880e8400-e29b-41d4-a716-446655440000",
        "startTime": "2025-03-15T19:00:00+08:00",
        "endTime": "2025-03-15T22:00:00+08:00",
        "saleStartTime": "2025-02-01T12:00:00+08:00",
        "saleEndTime": "2025-03-15T18:00:00+08:00",
        "status": "OnSale"
      }
    ],
    "priceCategories": [
      {
        "categoryId": "VIP",
        "name": "VIP 區",
        "areaIds": ["VIP"],
        "price": { "amount": 6800, "currency": "TWD" }
      },
      {
        "categoryId": "A",
        "name": "A 區",
        "areaIds": ["A1", "A2"],
        "price": { "amount": 4800, "currency": "TWD" }
      }
    ]
  }
}
```

### 4.3 POST /events (Organizer)

建立活動。

**Request:**
```json
{
  "name": "五月天演唱會",
  "description": "2025 巡迴演唱會",
  "venueId": "550e8400-e29b-41d4-a716-446655440000",
  "sessions": [
    {
      "startTime": "2025-03-15T19:00:00+08:00",
      "endTime": "2025-03-15T22:00:00+08:00",
      "saleStartTime": "2025-02-01T12:00:00+08:00",
      "saleEndTime": "2025-03-15T18:00:00+08:00"
    }
  ],
  "priceCategories": [
    {
      "categoryId": "VIP",
      "name": "VIP 區",
      "areaIds": ["VIP"],
      "price": { "amount": 6800, "currency": "TWD" }
    }
  ]
}
```

---

## 5. Session / Ticket API

### 5.1 GET /sessions/{sessionId}/seats

取得場次所有座位狀態。

**Response:**
```json
{
  "data": {
    "sessionId": "880e8400-e29b-41d4-a716-446655440000",
    "areas": [
      {
        "areaId": "A",
        "seats": [
          {
            "seatId": "A-001",
            "status": "Available",
            "price": { "amount": 4800, "currency": "TWD" }
          },
          {
            "seatId": "A-002",
            "status": "Locked"
          },
          {
            "seatId": "A-003",
            "status": "Sold"
          }
        ]
      }
    ],
    "summary": {
      "total": 15000,
      "available": 8000,
      "locked": 500,
      "sold": 6500
    }
  }
}
```

### 5.2 POST /sessions/{sessionId}/seats/lock

鎖定座位。

**Request:**
```json
{
  "seatIds": ["A-001", "A-002"]
}
```

**Response (Success):**
```json
{
  "data": {
    "lockId": "990e8400-e29b-41d4-a716-446655440000",
    "sessionId": "880e8400-e29b-41d4-a716-446655440000",
    "seats": [
      { "seatId": "A-001", "status": "Locked" },
      { "seatId": "A-002", "status": "Locked" }
    ],
    "expiresAt": "2025-01-20T10:05:00Z",
    "ttlSeconds": 120
  }
}
```

**Response (Failure - Seat Already Locked):**
```json
{
  "error": {
    "code": "SEAT_UNAVAILABLE",
    "message": "One or more seats are not available",
    "details": {
      "unavailableSeats": [
        { "seatId": "A-002", "reason": "Already locked by another user" }
      ]
    }
  }
}
```

### 5.3 DELETE /sessions/{sessionId}/seats/lock

釋放座位鎖定。

**Request:**
```json
{
  "seatIds": ["A-001", "A-002"]
}
```

**Response:**
```json
{
  "data": {
    "released": ["A-001", "A-002"]
  }
}
```

### 5.4 GET /sessions/{sessionId}/seats/{seatId}

取得單一座位狀態。

**Response:**
```json
{
  "data": {
    "seatId": "A-001",
    "sessionId": "880e8400-e29b-41d4-a716-446655440000",
    "status": "Available",
    "areaId": "A",
    "row": "1",
    "number": 1,
    "price": { "amount": 4800, "currency": "TWD" },
    "type": "Normal"
  }
}
```

---

## 6. Order API

### 6.1 POST /orders

建立訂單。

**Request:**
```json
{
  "sessionId": "880e8400-e29b-41d4-a716-446655440000",
  "seatIds": ["A-001", "A-002"],
  "paymentProvider": "Mock"
}
```

**Response:**
```json
{
  "data": {
    "orderId": "aa0e8400-e29b-41d4-a716-446655440000",
    "status": "Created",
    "items": [
      {
        "seatId": "A-001",
        "price": { "amount": 4800, "currency": "TWD" }
      },
      {
        "seatId": "A-002",
        "price": { "amount": 4800, "currency": "TWD" }
      }
    ],
    "totalAmount": { "amount": 9600, "currency": "TWD" },
    "expiresAt": "2025-01-20T10:10:00Z",
    "payment": {
      "paymentId": "bb0e8400-e29b-41d4-a716-446655440000",
      "provider": "Mock",
      "paymentUrl": "http://localhost/mock-payment/bb0e8400..."
    }
  }
}
```

### 6.2 GET /orders/{orderId}

取得訂單狀態。

**Response:**
```json
{
  "data": {
    "orderId": "aa0e8400-e29b-41d4-a716-446655440000",
    "status": "Completed",
    "items": [
      {
        "seatId": "A-001",
        "price": { "amount": 4800, "currency": "TWD" },
        "ticketId": "cc0e8400-e29b-41d4-a716-446655440000"
      },
      {
        "seatId": "A-002",
        "price": { "amount": 4800, "currency": "TWD" },
        "ticketId": "dd0e8400-e29b-41d4-a716-446655440001"
      }
    ],
    "totalAmount": { "amount": 9600, "currency": "TWD" },
    "payment": {
      "paymentId": "bb0e8400-e29b-41d4-a716-446655440000",
      "status": "Succeeded",
      "paidAt": "2025-01-20T10:03:00Z"
    },
    "createdAt": "2025-01-20T10:00:00Z",
    "completedAt": "2025-01-20T10:03:00Z"
  }
}
```

### 6.3 DELETE /orders/{orderId}

取消訂單（僅限未付款狀態）。

**Response:**
```json
{
  "data": {
    "orderId": "aa0e8400-e29b-41d4-a716-446655440000",
    "status": "Cancelled",
    "cancelledAt": "2025-01-20T10:05:00Z"
  }
}
```

### 6.4 GET /orders

取得用戶訂單列表。

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| page | int | No | 頁碼 |
| pageSize | int | No | 每頁筆數 |
| status | string | No | 狀態篩選 |

**Response:**
```json
{
  "data": {
    "items": [
      {
        "orderId": "aa0e8400-e29b-41d4-a716-446655440000",
        "event": {
          "eventId": "770e8400-e29b-41d4-a716-446655440000",
          "name": "五月天演唱會"
        },
        "session": {
          "sessionId": "880e8400-e29b-41d4-a716-446655440000",
          "startTime": "2025-03-15T19:00:00+08:00"
        },
        "seatCount": 2,
        "totalAmount": { "amount": 9600, "currency": "TWD" },
        "status": "Completed",
        "createdAt": "2025-01-20T10:00:00Z"
      }
    ]
  }
}
```

---

## 7. Ticket API

### 7.1 GET /tickets

取得用戶票券列表。

**Response:**
```json
{
  "data": {
    "items": [
      {
        "ticketId": "cc0e8400-e29b-41d4-a716-446655440000",
        "event": {
          "eventId": "770e8400-e29b-41d4-a716-446655440000",
          "name": "五月天演唱會"
        },
        "session": {
          "sessionId": "880e8400-e29b-41d4-a716-446655440000",
          "startTime": "2025-03-15T19:00:00+08:00",
          "venue": "台北小巨蛋"
        },
        "seat": {
          "seatId": "A-001",
          "area": "A 區",
          "row": "1",
          "number": 1
        },
        "status": "Issued",
        "qrCode": "data:image/png;base64,..."
      }
    ]
  }
}
```

### 7.2 GET /tickets/{ticketId}

取得票券詳情。

**Response:**
```json
{
  "data": {
    "ticketId": "cc0e8400-e29b-41d4-a716-446655440000",
    "orderId": "aa0e8400-e29b-41d4-a716-446655440000",
    "event": {
      "eventId": "770e8400-e29b-41d4-a716-446655440000",
      "name": "五月天演唱會",
      "description": "2025 巡迴演唱會"
    },
    "session": {
      "sessionId": "880e8400-e29b-41d4-a716-446655440000",
      "startTime": "2025-03-15T19:00:00+08:00",
      "endTime": "2025-03-15T22:00:00+08:00"
    },
    "venue": {
      "venueId": "550e8400-e29b-41d4-a716-446655440000",
      "name": "台北小巨蛋",
      "address": { ... }
    },
    "seat": {
      "seatId": "A-001",
      "areaId": "A",
      "areaName": "A 區",
      "row": "1",
      "number": 1
    },
    "price": { "amount": 4800, "currency": "TWD" },
    "status": "Issued",
    "qrCode": "OPENTICKET-CC0E8400...",
    "qrCodeImage": "data:image/png;base64,...",
    "issuedAt": "2025-01-20T10:03:00Z"
  }
}
```

### 7.3 POST /tickets/{ticketId}/verify (Staff)

驗證票券（入場驗票）。

**Request:**
```json
{
  "qrCode": "OPENTICKET-CC0E8400..."
}
```

**Response:**
```json
{
  "data": {
    "valid": true,
    "ticketId": "cc0e8400-e29b-41d4-a716-446655440000",
    "status": "Used",
    "seat": {
      "seatId": "A-001",
      "area": "A 區",
      "row": "1",
      "number": 1
    },
    "usedAt": "2025-03-15T18:30:00+08:00"
  }
}
```

---

## 8. WebSocket / SignalR API

### 8.1 Connection

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/tickets", {
        accessTokenFactory: () => getAccessToken()
    })
    .withAutomaticReconnect()
    .build();

await connection.start();
```

### 8.2 Methods

#### JoinSession

加入場次的座位狀態更新群組。

```javascript
await connection.invoke("JoinSession", sessionId);
```

#### LeaveSession

離開場次群組。

```javascript
await connection.invoke("LeaveSession", sessionId);
```

### 8.3 Events

#### SeatStatusChanged

座位狀態變更通知。

```javascript
connection.on("SeatStatusChanged", (data) => {
    // data: { sessionId, seatId, status, timestamp }
    console.log(`Seat ${data.seatId} changed to ${data.status}`);
});
```

**Payload:**
```json
{
  "sessionId": "880e8400-e29b-41d4-a716-446655440000",
  "seatId": "A-001",
  "status": "Locked",
  "timestamp": "2025-01-20T10:00:00Z"
}
```

#### OrderStatusChanged

訂單狀態變更通知（只推送給訂單擁有者）。

```javascript
connection.on("OrderStatusChanged", (data) => {
    // data: { orderId, status, timestamp }
});
```

---

## 9. Rate Limiting

### 9.1 Limits

| Endpoint | Limit | Window |
|----------|-------|--------|
| `/auth/*` | 10 req | 1 min |
| `/sessions/*/seats/lock` | 5 req | 1 min |
| `/orders` POST | 3 req | 1 min |
| Other endpoints | 100 req | 1 min |

### 9.2 Headers

超過限制時會回傳 `429 Too Many Requests`：

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 30
X-RateLimit-Limit: 5
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1705748400
```

---

## 10. Webhooks (Payment)

### 10.1 POST /webhooks/payment/{provider}

接收支付回調。

**Headers:**
```http
X-Webhook-Signature: sha256=...
Content-Type: application/json
```

**Stripe Payload Example:**
```json
{
  "type": "checkout.session.completed",
  "data": {
    "object": {
      "id": "cs_test_...",
      "payment_status": "paid",
      "metadata": {
        "order_id": "aa0e8400-e29b-41d4-a716-446655440000"
      }
    }
  }
}
```

---

## 11. SDK Examples

### 11.1 JavaScript/TypeScript

```typescript
import { OpenTicketClient } from '@openticket/sdk';

const client = new OpenTicketClient({
  baseUrl: 'https://api.openticket.io/v1',
  accessToken: 'your-jwt-token'
});

// Lock seats
const lockResult = await client.sessions.lockSeats('session-id', {
  seatIds: ['A-001', 'A-002']
});

if (lockResult.success) {
  // Create order
  const order = await client.orders.create({
    sessionId: 'session-id',
    seatIds: ['A-001', 'A-002'],
    paymentProvider: 'Stripe'
  });

  // Redirect to payment
  window.location.href = order.payment.paymentUrl;
}
```

### 11.2 C# / .NET

```csharp
using OpenTicket.Sdk;

var client = new OpenTicketClient(new OpenTicketOptions
{
    BaseUrl = "https://api.openticket.io/v1",
    AccessToken = "your-jwt-token"
});

// Lock seats
var lockResult = await client.Sessions.LockSeatsAsync("session-id", new LockSeatsRequest
{
    SeatIds = new[] { "A-001", "A-002" }
});

if (lockResult.Success)
{
    // Create order
    var order = await client.Orders.CreateAsync(new CreateOrderRequest
    {
        SessionId = "session-id",
        SeatIds = new[] { "A-001", "A-002" },
        PaymentProvider = "Stripe"
    });

    // Get payment URL
    var paymentUrl = order.Payment.PaymentUrl;
}
```
