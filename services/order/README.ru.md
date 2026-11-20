# order service

владелец: Вита, Милена

> ограничения: состояние хранится в файле `data/orders.json` рядом с dll, возвратов и снятия резервов нет. событие уходит в брокер после изменения состояния: если брокер упадёт посередине, состояние и поток событий могут разойтись.

## запуск

```bash
# visual studio: открой Orders.sln, выбери профиль "college broker" или "local broker" и жми F5

# одной командой на windows
powershell -ExecutionPolicy Bypass -File scripts\run-service.ps1 -Name order

# на ubuntu и macos
./scripts/run-service.sh order
```

* **порт:** 5001
* **читает:** `payment.completed2`, `payment.failed2`, `inventory.reserved2`, `inventory.failed2`
* **пишет:** `order.created2`, `order.cancelled2`

## API

| метод | путь | описание |
|---|---|---|
| POST | `/orders` | создать заказ `{"userId","items":[{"sku","productName","quantity","unitPrice"}]}` |
| GET | `/orders`, `/orders/{id}` | просмотр |
| POST | `/orders/{id}/cancel` | отмена `{"reason"}` |
| PATCH | `/orders/{id}/status` | `{"status":"Confirmed"}` или `"Cancelled"` |
| GET | `/health` | проверка |

заказ подтверждается, когда пришли оплата и резерв склада в любом порядке. если брокер недоступен, `POST /orders` отвечает 503 и заказ не создаётся.

контракт событий: [contracts/events.md](../../contracts/events.md)
