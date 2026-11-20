# payment service

owner: Matvey

> limits: state lives in `data/payments.json` next to the dll, there are no refunds and no reservation release. the event is published after the state change: if the broker dies in between, state and event stream can diverge.

## run

```bash
# visual studio: open Payments.sln, pick the "college broker" or "local broker" profile and press F5

# one command on windows
powershell -ExecutionPolicy Bypass -File scripts\run-service.ps1 -Name payment

# ubuntu and macos
./scripts/run-service.sh payment
```

* **port:** 5002
* **consumes:** `order.created2`
* **produces:** `payment.completed2`, `payment.failed2`

event contract: [contracts/events.md](../../contracts/events.md). endpoints are listed in README.ru.md.
