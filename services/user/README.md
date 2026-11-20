# user service

owner: Alisa, Dasha

> limits: state lives in `data/users.json` next to the dll, there are no refunds and no reservation release. the event is published after the state change: if the broker dies in between, state and event stream can diverge.

## run

```bash
# visual studio: open Users.sln, pick the "college broker" or "local broker" profile and press F5

# one command on windows
powershell -ExecutionPolicy Bypass -File scripts\run-service.ps1 -Name user

# ubuntu and macos
./scripts/run-service.sh user
```

* **port:** 5003
* **consumes:** `order.created2` (считает заказы пользователя)
* **produces:** `user.registered2`

event contract: [contracts/events.md](../../contracts/events.md). endpoints are listed in README.ru.md.
