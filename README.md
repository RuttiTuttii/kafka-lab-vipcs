<div align="center">

# kafka-lab-vipcs

lab 9: five .NET Worker Service microservices talking over Apache Kafka. team 2

</div>

> limits: the college broker `10.9.2.80:9092` is reachable only from the college network, use docker compose at home. service code lives in separate repos, this one holds contracts and shared infrastructure only. there are no services here.

---

## <img src="assets/icons/network.svg" width="20" height="20" valign="middle" /> layout

```
kafka-lab-vipcs/
├── contracts/
│   └── events.md          topics and JSON schemas
├── assets/icons/          svg icons for docs
├── docker-compose.yml     kafka + kafka-ui
└── .github/workflows/
    └── ci.yml             ci template for a service repo
```

## <img src="assets/icons/users.svg" width="20" height="20" valign="middle" /> who does what

| service | producer | consumer | owners |
|---|---|---|---|
| user | `user.registered2` | `order.created2` | Alisa, Dasha |
| order | `order.created2`, `order.cancelled2` | `payment.*2`, `inventory.*2` | Vita, Milena |
| payment | `payment.completed2`, `payment.failed2` | `order.created2` | Matvey |
| inventory | `inventory.reserved2`, `inventory.failed2` | `order.created2` | Egor |
| notification | none | `order.created2`, `payment.*2`, `order.cancelled2` | Zakhar |

* **topics:** 7, all carry the suffix `2`, taken from config, not from code
* **broker:** one, `10.9.2.80:9092` at the college and `localhost:9092` at home
* **format:** JSON, camelCase, UTC ISO-8601 time

## <img src="assets/icons/file-json.svg" width="20" height="20" valign="middle" /> contract

event schemas, message keys, `group.id` and `eventId` rules are in [contracts/events.md](contracts/events.md). it is the single source of truth: change a schema by a pull request here and tell the chat.

## <img src="assets/icons/terminal.svg" width="20" height="20" valign="middle" /> local broker

```bash
# start kafka (KRaft, one broker) and kafka-ui
docker compose up -d

# browse topics and messages
open http://localhost:8080
```

### service settings

| setting | default | description |
|---|---|---|
| `Kafka:BootstrapServers` | `localhost:9092` | broker address, `10.9.2.80:9092` at the college |
| `Kafka:TopicSuffix` | `2` | suffix appended to every topic name |
| `Kafka:GroupId` | `<service>-service2` | consumer group id |

local overrides go to `appsettings.Local.json`, it is in `.gitignore`.

## <img src="assets/icons/git-branch.svg" width="20" height="20" valign="middle" /> git workflow

* **repos:** one per service, this one holds shared files
* **ci:** copy `.github/workflows/ci.yml` into your repo as is, it builds and tests everything with `dotnet`
* **contracts:** update them by a pull request here, Egor reviews
* **commits:** conventional commits, lowercase subject, e.g. `feat(payment): consumer order.created2`

## <img src="assets/icons/triangle-alert.svg" width="20" height="20" valign="middle" /> limitations

* **college broker:** unreachable from the internet, checked: ping 100% loss, port 9092 timeout
* **inventory.failed2:** nobody consumes it in the assignment, we propose order consumes it and cancels the order
* **payment refund:** if payment succeeded and inventory refused, there is no refund, only a log line
* **user and order.created2:** the assignment does not say why user consumes it, we log it

---

[русская версия](README.ru.md)
