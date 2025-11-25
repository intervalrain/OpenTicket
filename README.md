# OpenTicket

**全球分散式售票平台 (Global Distributed Ticketing Platform)**

[![.NET](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com/)
[![NATS](https://img.shields.io/badge/NATS-JetStream-green)](https://nats.io/)
[![License](https://img.shields.io/badge/License-MIT-blue)](LICENSE)

---

## Overview

OpenTicket 是一套專為高流量秒殺場景設計的售票基礎建設平台，採用 Event-Driven Architecture 與 DDD 設計，解決傳統票務系統的核心痛點：

- **秒殺當機** → Event-Driven + NATS 高吞吐量
- **重複售票 (Double-Sell)** → Actor Model 單線程處理
- **黃牛 Bot** → Queue Gate + 行為分析
- **座位圖格式不統一** → 開放式 Venue Schema
- **支付/身分綁定** → 可插拔 Provider 架構

## Key Features

| Feature | Description |
|---------|-------------|
| **Zero Double-Sell** | Actor Model + Partitioned Seat Lock |
| **High Throughput** | NATS JetStream 百萬級 QPS |
| **Real-time Sync** | SignalR 即時座位狀態推播 |
| **Multi-Region** | NATS Leafnode 全球部署 |
| **Pluggable** | Payment/Identity Provider 可插拔 |
| **Testable** | 所有依賴可 Mock，MVP 可離線運行 |

## Architecture

```
User → API Gateway → NATS Event Bus → Services → Database
                            ↓
                    SignalR → Real-time UI
```

詳細架構請參考 [Architecture Documentation](docs/architecture/ARCHITECTURE.md)。

## Domain Contexts

系統依 DDD Bounded Context 切分為：

| Context | 職責 | 擴展策略 |
|---------|------|----------|
| **Ticket** (Core) | 座位鎖定、票券 | Partition by SeatId |
| **Order** | 訂單生命週期 | Partition by UserId |
| **Payment** | 支付處理 | Stateless |
| **Identity** | 身份驗證 | Stateless |
| **Venue** | 場館座位圖 | Read Replica |
| **Event** | 活動場次 | Read Replica |
| **Queue** | 流量控制 | Stateless + Redis |
| **Notification** | 即時推播 | Fan-out |

詳細領域模型請參考 [Domain Models](docs/domain/DOMAIN_MODELS.md)。

## Quick Start

### Prerequisites

- .NET 9 SDK
- Docker Desktop (Optional for Phase 1+)

### Run in MVP Mode (No External Dependencies)

```bash
# Clone
git clone https://github.com/your-org/openticket.git
cd openticket

# Run
cd src/OpenTicket.Api
dotnet run

# Access
# API: http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

### Run with Infrastructure (Phase 1+)

```bash
# Start dependencies
docker-compose up -d

# Update config
# Set Infrastructure:Mode = "Production" in appsettings.json

# Run
dotnet run
```

## Documentation

| Document | Description |
|----------|-------------|
| [PRD](docs/PRD.md) | 產品需求規格書 |
| [Architecture](docs/architecture/ARCHITECTURE.md) | 系統架構設計 |
| [Domain Models](docs/domain/DOMAIN_MODELS.md) | 領域模型設計 |
| [Abstraction Layers](docs/development/ABSTRACTION_LAYERS.md) | 抽象層設計 |
| [Development Guide](docs/development/DEVELOPMENT_GUIDE.md) | 開發指南與 Roadmap |
| [API Specification](docs/api/API_SPEC.md) | API 規格文件 |

## Development Roadmap

| Phase | Duration | Goal |
|-------|----------|------|
| **Phase 0 (MVP)** | Week 1-4 | In-Memory 完整購票流程 |
| **Phase 1** | Week 5-10 | PostgreSQL, Redis, NATS, SignalR |
| **Phase 2** | Week 11-16 | 真實支付、OAuth、進階防護 |
| **Phase 3** | Week 17-24 | Multi-Region、高可用 |

## Project Structure

```
OpenTicket/
├── docs/                    # Documentation
├── src/
│   ├── OpenTicket.Abstractions/   # Core interfaces
│   ├── OpenTicket.Domain/         # Domain models
│   ├── OpenTicket.Application/    # Application services
│   ├── OpenTicket.Infrastructure/ # Infrastructure implementations
│   ├── OpenTicket.Api/            # API entry point
│   └── OpenTicket.Worker/         # Background workers
├── tests/                   # Test projects
├── docker/                  # Docker configurations
└── scripts/                 # Utility scripts
```

## Tech Stack

| Category | Technology |
|----------|------------|
| **Backend** | .NET 9, C# 13 |
| **Messaging** | NATS JetStream |
| **Database** | PostgreSQL, Redis |
| **Real-time** | SignalR |
| **Container** | Docker, Kubernetes |
| **Monitoring** | Prometheus, Grafana |

## Contributing

請參考 [Development Guide](docs/development/DEVELOPMENT_GUIDE.md) 中的 Contribution Guidelines。

## License

MIT License - see [LICENSE](LICENSE) for details.
