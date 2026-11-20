# order service

owner: Vita, Milena

> limits: state lives in `data/orders.json` next to the dll, there are no refunds and no reservation release. the event is published after the state change: if the broker dies in between, state and event stream can diverge.

## run

```bash
# visual studio: open Orders.sln, pick the "college broker" or "local broker" profile and press F5

# one command on windows
powershell -ExecutionPolicy Bypass -File scripts\run-service.ps1 -Name order

# ubuntu and macos
./scripts/run-service.sh order
```

* **port:** 5001
* **consumes:** `payment.completed2`, `payment.failed2`, `inventory.reserved2`, `inventory.failed2`
* **produces:** `order.created2`, `order.cancelled2`

event contract: [contracts/events.md](../../contracts/events.md). endpoints are listed in README.ru.md.
