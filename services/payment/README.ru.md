# payment service

владелец: Матвей

> ограничения: состояние хранится в файле `data/payments.json` рядом с dll, возвратов и снятия резервов нет. событие уходит в брокер после изменения состояния: если брокер упадёт посередине, состояние и поток событий могут разойтись.

## запуск

```bash
# visual studio: открой Payments.sln, выбери профиль "college broker" или "local broker" и жми F5

# одной командой на windows
powershell -ExecutionPolicy Bypass -File scripts\run-service.ps1 -Name payment

# на ubuntu и macos
./scripts/run-service.sh payment
```

* **порт:** 5002
* **читает:** `order.created2`
* **пишет:** `payment.completed2`, `payment.failed2`

## API

| метод | путь | описание |
|---|---|---|
| GET | `/payments`, `/payments/{orderId}` | информация о платежах и заказах |
| POST | `/payments/{orderId}/confirm` | подтвердить оплату (режим manual) |
| POST | `/payments/{orderId}/decline` | отказ `{"reason"}` (режим manual) |
| GET | `/health` | проверка |

| настройка | по умолчанию | описание |
|---|---|---|
| `Payment:Mode` | `auto` | `auto` решает сам, `manual` ждёт confirm или decline через API |
| `Payment:Limit` | `50000` | суммы больше лимита получают `limit_exceeded` |
| `Payment:DelayMs` | `300` | задержка имитации шлюза |

повтор `order.created2` для того же заказа не создаёт второй платёж, в том числе после перезапуска.

контракт событий: [contracts/events.md](../../contracts/events.md)
