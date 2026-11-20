<div align="center">

# inventory service

inventory service for lab 9: .NET 10 Worker Service + Apache Kafka, team 2

</div>

> limits: stock lives in memory and is lost on restart. the broker `10.9.2.80:9092` was not reachable from the machine this was built on, so the API and tests work but the link with a real Kafka is not verified. the service exposes an API only, no UI.

---

## what it does

* **consumer:** `order.created2`, reserves items atomically, a repeated event with the same `orderId` gets the same answer
* **producer:** `inventory.reserved2` on success, `inventory.failed2` with a reason
* **API:** add, view, change stock, sufficiency check, seeder
* **contract:** [contracts/events.md](../../contracts/events.md)

## one-command run

```bash
# windows: broker check, build, tests, start, seeder, logs in logs/
powershell -ExecutionPolicy Bypass -File scripts\run.ps1 -Seed

# ubuntu and macos: same thing
./scripts/run.sh 10.9.2.80:9092 2 --seed
```

endpoints and settings are listed in [README.ru.md](README.ru.md).

---

[русская версия](README.ru.md)
