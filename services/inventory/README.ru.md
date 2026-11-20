<div align="center">

# inventory service

inventory service для лабораторной работы 9: .NET 10 Worker Service + Apache Kafka, команда 2

</div>

> ограничения: состояние склада хранится в памяти и теряется при перезапуске. брокер `10.9.2.80:9092` проверен только с моего мака, без доступа к нему: API и тесты работают, связка с реальным Kafka не проверена. сервис не пишет отдельные интерфейсы, только API.

---

## что делает

* **consumer:** `order.created2`, резервирует товары атомарно, повтор события по `orderId` даёт тот же ответ
* **producer:** `inventory.reserved2` при успехе, `inventory.failed2` с причиной (`insufficient_stock:<sku>`, `unknown_sku:<sku>`, `empty_order`, `invalid_quantity`)
* **API:** добавление, просмотр, изменение остатков, проверка достаточности, сидер
* **контракт:** [contracts/events.md](../../contracts/events.md)

## структура

```
src/Inventory.Service/
├── Contracts/     события и имена топиков
├── Domain/        InventoryStore, склад в памяти
├── Kafka/         consumer order.created, EventPublisher
├── Options/       KafkaOptions, суффикс топиков из конфига
└── Program.cs     minimal API и запуск
tests/Inventory.Tests/   6 тестов склада
scripts/run.ps1          сборка, тесты, запуск, логи (Windows)
scripts/run.sh           то же для Ubuntu и macOS
```

## запуск одной командой

```bash
# windows: проверка брокера, сборка, тесты, запуск, сидер, логи в logs/
powershell -ExecutionPolicy Bypass -File scripts\run.ps1 -Seed

# ubuntu и macos: то же самое
./scripts/run.sh 10.9.2.80:9092 2 --seed
```

логи пишутся в `logs/inventory-<время>.out.log` и идут в консоль, остановка по ctrl+c.


## API

| метод | путь | описание |
|---|---|---|
| GET | `/items` | все остатки |
| GET | `/items/{sku}` | остаток по sku |
| POST | `/items` | добавить товар `{"sku","quantity"}`, 409 если уже есть |
| PATCH | `/items/{sku}` | изменить количество `{"quantity"}` |
| POST | `/check` | проверить достаточность `{"items":[{"sku","quantity"}]}` |
| POST | `/seed/orders` | отправить 5 тестовых `order.created2` |
| GET | `/health` | проверка живости |

## настройки

| настройка | по умолчанию | описание |
|---|---|---|
| `Kafka:BootstrapServers` | `10.9.2.80:9092` | адрес брокера |
| `Kafka:TopicSuffix` | `2` | суффикс топиков |
| `Kafka:GroupId` | `inventory-service2` | consumer group |
| `Seed:Stock` | sku-001..sku-005 | начальные остатки |
| `Urls` | `http://0.0.0.0:5004` | адрес API |

переопределять можно через `appsettings.Local.json` (в `.gitignore`) или переменные окружения вида `Kafka__BootstrapServers`.

## ограничения

* **память:** остатки и результаты резервов живут в процессе, после перезапуска остаётся только `Seed:Stock`
* **возврат:** отмена заказа и возврат остатка не реализованы, в задании их нет
* **проверка:** тесты покрывают склад, consumer и producer проверены только на недоступном брокере (логи ошибок подключения)

---

[english version](README.md)
