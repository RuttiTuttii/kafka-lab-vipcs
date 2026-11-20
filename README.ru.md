<div align="center">

# kafka-lab-vipcs

лабораторная работа 9: пять микросервисов на .NET Worker Service, которые общаются через Apache Kafka. команда 2

</div>

> ограничения: брокер колледжа `10.9.2.80:9092` виден только из сети колледжа, дома работаем через docker compose. код сервисов живёт в отдельных репозиториях, здесь только контракты и общая инфраструктура. сервисов в этом репо нет.

---

## <img src="assets/icons/network.svg" width="20" height="20" valign="middle" /> структура

```
kafka-lab-vipcs/
├── contracts/
│   └── events.md          топики и схемы JSON
├── assets/icons/          svg-иконки для docs
├── docker-compose.yml     kafka + kafka-ui
└── .github/workflows/
    └── ci.yml             шаблон ci для репо сервиса
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

## <img src="assets/icons/file-json.svg" width="20" height="20" valign="middle" /> контракт

схемы всех событий, ключи сообщений, правила для `group.id` и `eventId` лежат в [contracts/events.md](contracts/events.md). это единственный источник правды, схему меняем pull request-ом сюда и пишем в чат.

## <img src="assets/icons/terminal.svg" width="20" height="20" valign="middle" /> локальный брокер

```bash
# поднимаем kafka (KRaft, один брокер) и kafka-ui
docker compose up -d

# смотрим топики и сообщения в браузере
open http://localhost:8080
```

### настройки сервиса

| настройка | по умолчанию | описание |
|---|---|---|
| `Kafka:BootstrapServers` | `localhost:9092` | адрес брокера, в колледже `10.9.2.80:9092` |
| `Kafka:TopicSuffix` | `2` | суффикс, который клеится к каждому имени топика |
| `Kafka:GroupId` | `<сервис>-service2` | id consumer group |

локальные правки кладём в `appsettings.Local.json`, он в `.gitignore`.

## <img src="assets/icons/git-branch.svg" width="20" height="20" valign="middle" /> как работаем с git

* **репозитории:** у каждого сервиса свой, этот хранит общее
* **ci:** `.github/workflows/ci.yml` копируешь в свой репо как есть, он собирает и тестирует всё через `dotnet`
* **контракты:** обновляешь их pull request-ом сюда, ревьюит Егор
* **коммиты:** conventional commits, тема строчными, например `feat(payment): consumer order.created2`

## <img src="assets/icons/triangle-alert.svg" width="20" height="20" valign="middle" /> ограничения

* **брокер колледжа:** с интернета недоступен, проверено: ping 100% loss, порт 9092 таймаут
* **inventory.failed2:** в задании его никто не слушает, предлагаем, чтобы order его слушал и отменял заказ
* **возврат платежа:** если оплата прошла, а склад отказал, возврата нет, только лог
* **user и order.created2:** зачем user его слушает, в задании не сказано, пишем в лог

---

[english version](README.md)
