# контракты событий (команда 2)

> черновик, согласуем до того, как кто-то начнёт писать код. меняем только через pull request с ревью от Егора.

## общие правила

- формат JSON, поля в camelCase, время в UTC ISO-8601, деньги числом с точкой
- суффикс топиков `2` берётся из конфига `Kafka:TopicSuffix`, в коде имена топиков не хардкодим
- ключ сообщения всегда `orderId` (для user.registered2 это `userId`), чтобы события одного заказа шли по порядку
- `group.id` = имя сервиса + суффикс, например `payment-service2`
- каждое событие содержит `eventId` (uuid), по нему делаем идемпотентность на стороне consumer

## топики

| топик | producer | consumers |
|---|---|---|
| user.registered2 | user | нет |
| order.created2 | order | user, payment, inventory, notification |
| order.cancelled2 | order | notification |
| payment.completed2 | payment | order, notification |
| payment.failed2 | payment | order, notification |
| inventory.reserved2 | inventory | order |
| inventory.failed2 | inventory | order (предложение, в задании не указано) |

## схемы

```json
// user.registered2
{ "eventId": "uuid", "userId": "uuid", "email": "string", "name": "string", "registeredAt": "ISO-8601" }

// order.created2
{ "eventId": "uuid", "orderId": "uuid", "userId": "uuid",
  "items": [ { "sku": "string", "quantity": 1, "price": 100.00 } ],
  "amount": 100.00, "currency": "RUB", "createdAt": "ISO-8601" }

// order.cancelled2
{ "eventId": "uuid", "orderId": "uuid", "userId": "uuid", "reason": "string", "cancelledAt": "ISO-8601" }

// payment.completed2
{ "eventId": "uuid", "orderId": "uuid", "paymentId": "uuid", "amount": 100.00, "paidAt": "ISO-8601" }

// payment.failed2
{ "eventId": "uuid", "orderId": "uuid", "reason": "string", "failedAt": "ISO-8601" }

// inventory.reserved2
{ "eventId": "uuid", "orderId": "uuid", "items": [ { "sku": "string", "quantity": 1 } ], "reservedAt": "ISO-8601" }

// inventory.failed2
{ "eventId": "uuid", "orderId": "uuid", "reason": "string", "failedAt": "ISO-8601" }
```

## открытые вопросы

- inventory.failed2: в задании его никто не слушает. предложение: order слушает и переводит заказ в cancelled
- если оплата прошла, а склад отказал: достаточно залогировать, возврат в лабе не делаем
- зачем user слушает order.created2: в задании не сказано, по умолчанию пишем в лог
