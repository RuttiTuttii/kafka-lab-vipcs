<div align="center">

# kafka-lab-vipcs

лабораторная работа 9: пять микросервисов на .NET Worker Service, которые общаются через Apache Kafka. команда 2

</div>

> ограничения: брокер колледжа `10.9.2.80:9092` виден только из сети колледжа, дома работаем через docker compose. в колледже с пк `10.9.1.14` он на момент записи не отвечал по порту 9092. все пять сервисов проверены на локальном брокере с выключенным автосозданием топиков, на брокере колледжа не проверены. состояние хранится в файлах, возвратов и снятия резервов нет.

---

## <img src="assets/icons/network.svg" width="20" height="20" valign="middle" /> структура

```
kafka-lab-vipcs/
├── services/
│   ├── user/            Алиса, Даша (готов, .NET 10, порт 5003)
│   ├── order/           Вита, Милена (готов, .NET 10, порт 5001)
│   ├── payment/         Матвей (готов, .NET 10, порт 5002)
│   ├── inventory/       Егор (готов, .NET 10, порт 5004)
│   └── notification/    Захар (готов, .NET 10, без API)
├── contracts/
│   └── events.md        топики и схемы JSON
├── scripts/             run-service, run-all, kafka-up
├── docker-compose.yml   kafka + kafka-ui
└── .github/workflows/
    └── ci.yml           сборка и тесты .NET сервисов
```

## <img src="assets/icons/users.svg" width="20" height="20" valign="middle" /> кто что делает

| сервис | producer | consumer | кто |
|---|---|---|---|
| user | `user.registered2` | `order.created2` | Алиса, Даша |
| order | `order.created2`, `order.cancelled2` | `payment.*2`, `inventory.*2` | Вита, Милена |
| payment | `payment.completed2`, `payment.failed2` | `order.created2` | Матвей |
| inventory | `inventory.reserved2`, `inventory.failed2` | `order.created2` | Егор |
| notification | нет | `order.created2`, `payment.*2`, `order.cancelled2` | Захар |

* **топики:** 7, у всех суффикс `2`, он берётся из конфига, а не из кода
* **брокер:** один, `10.9.2.80:9092` в колледже и `localhost:9092` дома
* **формат:** JSON, camelCase, время в UTC ISO-8601
* **топики:** создаёт тот сервис, который стартовал первым, на брокере колледжа их может не быть

## <img src="assets/icons/file-json.svg" width="20" height="20" valign="middle" /> контракт

схемы всех событий, ключи сообщений, правила для `group.id` и `eventId` лежат в [contracts/events.md](contracts/events.md). это единственный источник правды, схему меняем pull request-ом сюда и пишем в чат.

## <img src="assets/icons/terminal.svg" width="20" height="20" valign="middle" /> запуск сервисов

сервисы можно запускать на разных пк и в любой последовательности. каждый при старте создаёт нужные топики, читает всё с начала (`earliest`) и хранит состояние в `data/`, поэтому сервис, который поднялся позже, догонит события, а перезапуск не теряет заказы.

```bash
# visual studio: открой services/<имя>/<решение>.sln, в выпадающем списке запуска выбери
# профиль "college broker" (10.9.2.80:9092) или "local broker" (localhost:9092) и жми F5

# один сервис одной командой, из корня репо на windows
powershell -ExecutionPolicy Bypass -File scripts\run-service.ps1 -Name payment

# все пять сервисов, демо-сценарий и общие логи на одном пк
powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1

# ubuntu и macos
./scripts/run-service.sh payment
./scripts/run-all.sh
```

| сервис | решение | порт | профиль vs |
|---|---|---|---|
| user | `Users.sln` | 5003 | Users.Service |
| order | `Orders.sln` | 5001 | Orders.Service |
| payment | `Payments.sln` | 5002 | Payments.Service |
| inventory | `Inventory.sln` | 5004 | Inventory.Service |
| notification | `NotificationService.sln` | нет | NotificationService.Worker |

`run-all` запускает три сценария: обычный заказ (Confirmed), нехватка склада (Cancelled от inventory), сумма выше лимита оплаты (Cancelled от payment).

## <img src="assets/icons/terminal.svg" width="20" height="20" valign="middle" /> локальный брокер

```bash
# поднимаем kafka (KRaft, один брокер) и kafka-ui
docker compose up -d

# смотрим топики и сообщения в браузере: http://localhost:8080
```

### настройки сервиса

| настройка | по умолчанию | описание |
|---|---|---|
| `Kafka:BootstrapServers` | `localhost:9092` | адрес брокера, в колледже `10.9.2.80:9092` |
| `Kafka:TopicSuffix` | `2` | суффикс, который клеится к каждому имени топика |
| `Kafka:GroupId` | `<сервис>-service2` | id consumer group |

локальные правки кладём в `appsettings.Local.json`, он в `.gitignore`.

## <img src="assets/icons/git-branch.svg" width="20" height="20" valign="middle" /> * **папки:** каждый пушит только в свой `services/<имя>`, чужое не трогаем
* **перед работой:** `git pull --rebase origin main`, чтобы не ловить конфликты
* **автор коммитов:** на пк колледжа глобальный git настроен на чужого человека, поэтому в репо один раз задай себя (команды ниже)
* **контракты:** меняем через pull request и пишем в чат, источник правды `contracts/events.md`
* **коммиты:** conventional commits, тема строчными, например `feat(payment): consumer order.created2`

```bash
# задаём себя только для этого репо, глобальный git не трогаем
git config user.name "твой-ник-на-github"
git config user.email "твой-id+ник@users.noreply.github.com"

# забираем свежее перед работой
git pull --rebase origin main

# отправляем только свою папку
git add services/payment
git commit -m "feat(payment): consumer order.created2"
git push origin main
```

## <img src="assets/icons/triangle-alert.svg" width="20" height="20" valign="middle" /> ограничения

* **брокер колледжа:** с интернета недоступен, проверено: ping 100% loss, порт 9092 таймаут
* **inventory.failed2:** в задании его никто не слушает, предлагаем, чтобы order его слушал и отменял заказ
* **возврат платежа:** если оплата прошла, а склад отказал, возврата нет, только лог
* **user и order.created2:** зачем user его слушает, в задании не сказано, пишем в лог

---

[english version](README.md)
