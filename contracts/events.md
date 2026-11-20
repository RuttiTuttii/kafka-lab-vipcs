# контракты событий (команда 2)

> источник правды для всех пяти сервисов. схема совмещает поля сервиса уведомлений и сервиса склада: `items[].sku` нужен складу, `totalAmount` и `unitPrice` нужны уведомлениям. консьюмеры обязаны игнорировать неизвестные поля, поэтому добавлять поля можно, переименовывать и удалять нельзя. изменения идут pull request-ом и сообщением в чат.

## общие правила

* **формат:** JSON, ключи в camelCase, время в UTC ISO-8601 (`2026-11-20T11:00:00Z`), деньги числом с точкой
* **суффикс топиков:** `2`, берётся из конфига `Kafka:TopicSuffix`, в коде имена не хардкодим
* **ключ сообщения:** `orderId` (для `user.registered2` это `userId`), чтобы события одного заказа шли по порядку
* **consumer group:** имя сервиса плюс суффикс: `user-service2`, `order-service2`, `payment-service2`, `inventory-service2`, `notification-service2`
* **идемпотентность:** в каждом событии есть `eventId` (uuid), повторная обработка того же заказа не должна менять результат
* **чтение с начала:** `auto.offset.reset=earliest`, ручной commit после обработки. Сервис, который запустили позже других, прочитает всё, что пришло раньше
* **топики создаёт тот, кто стартовал первым:** на брокере колледжа их может не быть, поэтому каждый сервис при старте создаёт все 7 топиков (1 партиция, replication factor 1) и пропускает уже существующие
* **состояние:** каждый сервис хранит своё состояние в файле `data/*.json`, чтобы перезапуск не терял заказы и остатки

## топики

| топик | кто пишет | кто читает |
|---|---|---|
| `user.registered2` | user | никто |
| `order.created2` | order | user, payment, inventory, notification |
| `order.cancelled2` | order | notification |
| `payment.completed2` | payment | order, notification |
| `payment.failed2` | payment | order, notification |
| `inventory.reserved2` | inventory | order |
| `inventory.failed2` | inventory | order |

## жизненный цикл заказа

```
order.created2 ──► payment   ──► payment.completed2 ─┐
               └─► inventory ──► inventory.reserved2 ─┴─► order: Confirmed (нужны оба)

payment.failed2 или inventory.failed2 ──► order: Cancelled ──► order.cancelled2
```

* **Confirmed** только после `payment.completed2` и `inventory.reserved2`, порядок прихода любой
* **Cancelled** при любом из `payment.failed2`, `inventory.failed2` или ручной отмене
* **возврат и снятие резерва** не делаем: если оплата прошла, а склад отказал, деньги не возвращаются, если склад зарезервирован, а оплата отклонена, резерв не снимается

## схемы

```json
// user.registered2
{ "eventId": "uuid", "userId": "uuid", "email": "string", "name": "string", "registeredAt": "ISO-8601" }

// order.created2
{ "eventId": "uuid", "orderId": "uuid", "userId": "uuid",
  "items": [ { "sku": "sku-001", "productName": "string", "quantity": 2, "unitPrice": 1500.00 } ],
  "totalAmount": 3000.00, "currency": "RUB", "status": "Created", "createdAt": "ISO-8601" }

// order.cancelled2
{ "eventId": "uuid", "orderId": "uuid", "userId": "uuid", "reason": "string", "previousStatus": "Created", "cancelledAt": "ISO-8601" }

// payment.completed2
{ "eventId": "uuid", "paymentId": "uuid", "orderId": "uuid", "userId": "uuid", "amount": 3000.00,
  "transactionReference": "TXN-20261120-AB12CD", "completedAt": "ISO-8601" }

// payment.failed2
{ "eventId": "uuid", "paymentId": "uuid", "orderId": "uuid", "userId": "uuid", "amount": 3000.00,
  "failureReason": "limit_exceeded", "failedAt": "ISO-8601" }

// inventory.reserved2
{ "eventId": "uuid", "orderId": "uuid", "items": [ { "sku": "sku-001", "quantity": 2 } ], "reservedAt": "ISO-8601" }

// inventory.failed2
{ "eventId": "uuid", "orderId": "uuid", "reason": "insufficient_stock:sku-004", "failedAt": "ISO-8601" }
```

## общие тестовые данные

* **sku на складе:** `sku-001` (100), `sku-002` (50), `sku-003` (10), `sku-004` (5), `sku-005` (0)
* **пользователи:** `11111111-1111-1111-1111-111111111111` (Test User), `22222222-2222-2222-2222-222222222222` (Second User)
* **причины отказов:** `insufficient_stock:<sku>`, `unknown_sku:<sku>`, `empty_order`, `invalid_quantity`, `limit_exceeded`, `invalid_amount`
