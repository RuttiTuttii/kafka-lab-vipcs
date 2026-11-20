# Event Contracts (Events Specification) - Team 2

> Shared rules and JSON message contracts for cross-team alignment.
> Service #5: **NotificationService** consumes events across the lifecycle of orders and payments.

---

## 1. General Rules & Conventions

- **Data Format**: Standard JSON, keys in `camelCase`, timestamps in UTC ISO-8601 (e.g. `2026-11-20T11:00:00Z`), monetary amounts formatted as decimal numbers with a dot (e.g. `3000.00`).
- **Topic Suffix**: Topic suffix (e.g. `2` or empty) is read dynamically from configuration (`Kafka:TopicSuffix`), avoiding hardcoded topic names.
- **Message Partitioning Key**: The Kafka message key MUST be `orderId` (UUID string). This guarantees that all lifecycle events for the same order land on the same Kafka partition, preserving chronological ordering. (For user-only events, `userId` is used).
- **Consumer Group ID**: Configured as `notification-service` (or `notification-service${Kafka:TopicSuffix}`, e.g., `notification-service2`).
- **Idempotency**: Every published event MUST include an `eventId` (UUID). The consumer keeps a persistent thread-safe index of processed `eventId`s to deduplicate retried or duplicated messages (at-least-once delivery -> exactly-once business outcome).
- **Manual Offset Commit**: Offsets are committed manually by the consumer ONLY after the notification entity has been successfully generated and durably persisted to the repository.
- **Message Framing**: Events can be emitted directly as flat JSON payloads containing `eventId`, or wrapped inside an `EventEnvelope<T>` (`eventId`, `eventType`, `timestamp`, `payload`). `NotificationService` supports both transparently.

---

## 2. Topic Overview

| Topic Name | Producer Microservice | Consumer Microservice(s) | Key | Description |
|---|---|---|---|---|
| `order.created` (`order.created2`) | OrderService | InventoryService, PaymentService, NotificationService | `orderId` | Emitted when a customer places a new order. |
| `payment.completed` (`payment.completed2`) | PaymentService | OrderService, NotificationService | `orderId` | Emitted when order payment is successfully authorized. |
| `payment.failed` (`payment.failed2`) | PaymentService | OrderService, NotificationService | `orderId` | Emitted when order payment fails or is declined. |
| `order.cancelled` (`order.cancelled2`) | OrderService | NotificationService, (InventoryService) | `orderId` | Emitted when an order is cancelled. |
| `order.status.changed` (`order.status.changed2`) *(Optional)* | OrderService | NotificationService | `orderId` | Emitted when an order transitions between arbitrary lifecycle states. |

---

## 3. JSON Payload Schemas

### 3.1. Envelope Structure: `EventEnvelope<T>`

When using standard envelopes:

```json
{
  "eventId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "eventType": "order.created",
  "timestamp": "2026-11-20T11:00:00.000Z",
  "payload": { ... }
}
```

### 3.2. Topic: `order.created`

Published when an order has been created and awaits inventory reservation and payment.

#### Schema
```json
{
  "eventId": "uuid (optional when wrapped in EventEnvelope)",
  "orderId": "uuid",
  "userId": "uuid",
  "items": [
    {
      "productId": "uuid",
      "productName": "string",
      "quantity": "integer (positive)",
      "unitPrice": "decimal (> 0)"
    }
  ],
  "totalAmount": "decimal (>= 0)",
  "status": "string (e.g. 'Created')",
  "createdAt": "ISO-8601 string"
}
```

#### Example
```json
{
  "eventId": "11111111-1111-1111-1111-111111111101",
  "orderId": "a0000001-0000-0000-0000-000000000101",
  "userId": "u0000001-0000-0000-0000-00000000000a",
  "items": [
    {
      "productId": "p0000001-0000-0000-0000-000000000001",
      "productName": "Mechanical Keyboard RGB",
      "quantity": 2,
      "unitPrice": 1500.00
    }
  ],
  "totalAmount": 3000.00,
  "status": "Created",
  "createdAt": "2026-11-20T11:00:00Z"
}
```

---

### 3.3. Topic: `payment.completed`

Published when a payment is successfully processed by the payment provider.

#### Schema
```json
{
  "eventId": "uuid (optional when wrapped in EventEnvelope)",
  "paymentId": "uuid",
  "orderId": "uuid",
  "userId": "uuid",
  "amount": "decimal (> 0)",
  "transactionReference": "string",
  "completedAt": "ISO-8601 string"
}
```

#### Example
```json
{
  "eventId": "11111111-1111-1111-1111-111111111102",
  "paymentId": "pay-0001-0000-0000-000000000101",
  "orderId": "a0000001-0000-0000-0000-000000000101",
  "userId": "u0000001-0000-0000-0000-00000000000a",
  "amount": 3000.00,
  "transactionReference": "TXN-20261120-984712",
  "completedAt": "2026-11-20T11:00:01Z"
}
```

---

### 3.4. Topic: `payment.failed`

Published when a payment attempt fails (e.g. insufficient funds, card expired, gateway rejection).

#### Schema
```json
{
  "eventId": "uuid (optional when wrapped in EventEnvelope)",
  "paymentId": "uuid",
  "orderId": "uuid",
  "userId": "uuid",
  "amount": "decimal (> 0)",
  "failureReason": "string",
  "failedAt": "ISO-8601 string"
}
```

#### Example
```json
{
  "eventId": "22222222-2222-2222-2222-222222222102",
  "paymentId": "pay-0002-0000-0000-000000000102",
  "orderId": "b0000002-0000-0000-0000-000000000102",
  "userId": "u0000002-0000-0000-0000-00000000000b",
  "amount": 3000.00,
  "failureReason": "Insufficient funds in bank account",
  "failedAt": "2026-11-20T11:05:01Z"
}
```

---

### 3.5. Topic: `order.cancelled`

Published when an order has been cancelled by the customer, by payment failure, or by stock shortage.

#### Schema
```json
{
  "eventId": "uuid (optional when wrapped in EventEnvelope)",
  "orderId": "uuid",
  "userId": "uuid",
  "reason": "string",
  "previousStatus": "string",
  "cancelledAt": "ISO-8601 string"
}
```

#### Example
```json
{
  "eventId": "22222222-2222-2222-2222-222222222103",
  "orderId": "b0000002-0000-0000-0000-000000000102",
  "userId": "u0000002-0000-0000-0000-00000000000b",
  "reason": "Payment failed: Insufficient funds in bank account",
  "previousStatus": "AwaitingPayment",
  "cancelledAt": "2026-11-20T11:05:02Z"
}
```

---

### 3.6. Topic: `order.status.changed` *(Optional Support)*

Published whenever an order transitions between statuses.

#### Schema
```json
{
  "eventId": "uuid (optional when wrapped in EventEnvelope)",
  "orderId": "uuid",
  "userId": "uuid",
  "status": "string",
  "previousStatus": "string",
  "reason": "string (optional)",
  "changedAt": "ISO-8601 string"
}
```

#### Example
```json
{
  "eventId": "33333333-3333-3333-3333-333333333103",
  "orderId": "a0000001-0000-0000-0000-000000000101",
  "userId": "u0000001-0000-0000-0000-00000000000a",
  "status": "Processing",
  "previousStatus": "Created",
  "reason": "Payment verified and inventory allocated",
  "changedAt": "2026-11-20T11:00:02Z"
}
```

---

## 4. Notification Formats Generated

When `NotificationService` processes each event, it formats a human-readable domain entity:

| Event Type | Generated Title | Formatted Message Template |
|---|---|---|
| `order.created` | **Order Created** | `Order #{OrderId} has been created for a total of {TotalAmount:F2}. Awaiting inventory and payment.` |
| `payment.completed` | **Payment Succeeded** | `Payment of {Amount:F2} for Order #{OrderId} succeeded (Ref: {TransactionReference}).` |
| `payment.failed` | **Payment Failed** | `Payment of {Amount:F2} for Order #{OrderId} failed. Reason: {FailureReason}.` |
| `order.cancelled` | **Order Cancelled** | `Order #{OrderId} has been cancelled. Reason: {Reason}.` |
| `order.status.changed` | **Order Status Updated** | `Order #{OrderId} status changed from {PreviousStatus} to {Status}. Reason: {Reason}.` |
