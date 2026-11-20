# Microservice #5: NotificationService & Test Seeder

Microservice #5 for Lab 9 (Team 2): event-driven notification microservice built on .NET Worker Service (`Microsoft.NET.Sdk.Worker`) and Apache Kafka (`Confluent.Kafka`).

> **Broker Note**: The college broker `10.9.2.80:9092` is accessible within the college network. For local development outside campus, use `docker compose up -d` (`localhost:9092`).

---

## 1. Solution Structure

```
kafka-lab-vipcs/
├── NotificationService.sln           # .NET Solution containing service and seeder
├── contracts/
│   └── events.md                     # Shared message contracts, JSON schemas, & rules
├── src/
│   ├── NotificationService.Worker/   # Microservice #5 Worker Service
│   │   ├── Contracts/                # DTOs & tolerant JSON deserialization options
│   │   ├── Domain/                   # Notification entity & NotificationType enum
│   │   ├── Application/              # INotificationProcessor & INotificationHistoryService
│   │   ├── Infrastructure/
│   │   │   ├── Persistence/          # INotificationRepository & JsonNotificationRepository
│   │   │   └── Kafka/                # KafkaNotificationConsumerWorker & KafkaSettings
│   │   └── appsettings.json          # Config with BootstrapServers: 10.9.2.80:9092
│   └── NotificationSeeder.Worker/    # CLI Test Producer (publishes test flows)
│       ├── SeederWorker.cs           # Automated test suite (Happy path, Failure, Idempotency)
│       └── appsettings.json          # Config with BootstrapServers: 10.9.2.80:9092
├── data/
│   └── notifications-history.json    # Durable local persistence file (created on runtime)
├── docker-compose.yml                # Local Kafka (KRaft) & Kafka UI for offline testing
└── README.md
```

---

## 2. Consumed Topics & Event Schemas

`NotificationService.Worker` simultaneously subscribes to 4 primary topics (plus optional `order.status.changed`):

1. **`order.created`** — Formats: `"Order #{OrderId} has been created for a total of {TotalAmount:F2}. Awaiting inventory and payment."`
2. **`payment.completed`** — Formats: `"Payment of {Amount:F2} for Order #{OrderId} succeeded (Ref: {TransactionReference})."`
3. **`payment.failed`** — Formats: `"Payment of {Amount:F2} for Order #{OrderId} failed. Reason: {FailureReason}."`
4. **`order.cancelled`** — Formats: `"Order #{OrderId} has been cancelled. Reason: {Reason}."`
5. *(Optional)* **`order.status.changed`** — Formats: `"Order #{OrderId} status changed from {PreviousStatus} to {Status}. Reason: {Reason}."`

Topic names support dynamic suffix resolution (e.g. `order.created2`) via `Kafka:TopicSuffix` in `appsettings.json`.

---

## 3. How to Build & Run

### 3.1. Build the Solution
```bash
dotnet build NotificationService.sln
```

### 3.2. Run NotificationService.Worker
Start the notification consumer service:
```bash
dotnet run --project src/NotificationService.Worker/NotificationService.Worker.csproj
```
The service will:
- Connect to `10.9.2.80:9092` (or your configured broker).
- Subscribe to `order.created`, `payment.completed`, `payment.failed`, `order.cancelled` under group `notification-service`.
- Log formatted notifications in structured console format simulating dispatch to the user.
- Enforce deduplication via in-memory index on `eventId`.
- Persist history to `data/notifications-history.json`.
- Commit Kafka offsets manually only after durable persistence.

### 3.3. Run NotificationSeeder.Worker (Test Suite)
In a separate terminal, trigger the automated test suite:
```bash
dotnet run --project src/NotificationSeeder.Worker/NotificationSeeder.Worker.csproj
```
The seeder executes three test flows in sequence:
1. **Happy Path Flow**:
   - Publishes `order.created` for Order #101 (User A).
   - Waits 1 second, then publishes `payment.completed` for Order #101.
2. **Payment Failure & Cancellation Flow**:
   - Publishes `order.created` for Order #102 (User B).
   - Waits 1 second, then publishes `payment.failed` (e.g. "Insufficient funds in bank account") for Order #102.
   - Waits 1 second, then publishes `order.cancelled` for Order #102.
3. **Idempotency Check**:
   - Waits 1 second, then re-publishes a duplicate `order.created` message with the exact same `eventId` (`10100001-0000-0000-0000-000000000001`).
   - `NotificationService.Worker` skips the duplicate and logs `[IDEMPOTENCY SKIP]`.

---

## 4. History Storage & Persistence

- **Path**: `data/notifications-history.json` (relative to execution directory, configurable via `Persistence:StoragePath`).
- **Durability**: History is saved atomically and loaded into memory on startup so records survive worker restarts.
- **Querying**: `INotificationHistoryService` provides:
  - `GetAllAsync()`
  - `GetByUserIdAsync(Guid userId)`
  - `GetByOrderIdAsync(Guid orderId)`
  - `GetStatsAsync()` (Total notifications, unique users, unique orders, and type breakdown).
