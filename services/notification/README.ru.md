# Микросервис #5: NotificationService & Test Seeder

> перенесено из ветки `feature/notification` (коммит `e683e3a`, автор SkvernoSlov): теперь сервис лежит в `services/notification`, .NET 10 вместо 11, суффикс топиков `2`. команды ниже запускай из этой папки. схемы событий в `docs/events-proposal.md` это предложение Захара, общий контракт лежит в `../../contracts/events.md` и с ним пока расходится, см. раздел несостыковок в чате.

Микросервис #5 для лабораторной работы 9 (команда 2): событийно-ориентированный микросервис уведомлений на .NET Worker Service (`Microsoft.NET.Sdk.Worker`) и Apache Kafka (`Confluent.Kafka`).

> **Примечание по брокеру**: Брокер колледжа `10.9.2.80:9092` доступен внутри сети колледжа. При работе вне сети колледжа запускайте локальный брокер через `docker compose up -d` (`localhost:9092`).

---

## 1. Структура решения

```
kafka-lab-vipcs/
├── NotificationService.sln           # Файл решения .NET (включает сервис и сидер)
├── contracts/
│   └── events.md                     # Контракты сообщений, JSON-схемы и общие правила
├── src/
│   ├── NotificationService.Worker/   # Микросервис уведомлений (Worker Service)
│   │   ├── Contracts/                # DTO и толерантные настройки JsonSerializerOptions
│   │   ├── Domain/                   # Сущность Notification и enum NotificationType
│   │   ├── Application/              # INotificationProcessor и INotificationHistoryService
│   │   ├── Infrastructure/
│   │   │   ├── Persistence/          # INotificationRepository и JsonNotificationRepository
│   │   │   └── Kafka/                # KafkaNotificationConsumerWorker и KafkaSettings
│   │   └── appsettings.json          # Конфигурация (BootstrapServers: 10.9.2.80:9092)
│   └── NotificationSeeder.Worker/    # CLI тест-продюсер для отправки событий
│       ├── SeederWorker.cs           # Автоматизированный тестовый сценарий
│       └── appsettings.json          # Конфигурация (BootstrapServers: 10.9.2.80:9092)
├── data/
│   └── notifications-history.json    # Локальный файл персистентности истории уведомлений
├── docker-compose.yml                # Локальная Kafka (KRaft) и Kafka UI
└── README.md
```

---

## 2. Обрабатываемые топики и форматирование

`NotificationService.Worker` подписывается одновременно на 4 основных топика (и опциональный `order.status.changed`):

1. **`order.created`** — Шаблон: `"Order #{OrderId} has been created for a total of {TotalAmount:F2}. Awaiting inventory and payment."`
2. **`payment.completed`** — Шаблон: `"Payment of {Amount:F2} for Order #{OrderId} succeeded (Ref: {TransactionReference})."`
3. **`payment.failed`** — Шаблон: `"Payment of {Amount:F2} for Order #{OrderId} failed. Reason: {FailureReason}."`
4. **`order.cancelled`** — Шаблон: `"Order #{OrderId} has been cancelled. Reason: {Reason}."`
5. *(Опционально)* **`order.status.changed`** — Шаблон: `"Order #{OrderId} status changed from {PreviousStatus} to {Status}. Reason: {Reason}."`

Суффикс топиков настраивается динамически через параметр `Kafka:TopicSuffix` (например, `2` для топиков `order.created2` и т.д.).

---

## 3. Сборка и запуск

### 3.1. Сборка решения
```bash
dotnet build NotificationService.sln
```

### 3.2. Запуск сервиса уведомлений (NotificationService.Worker)
```bash
dotnet run --project src/NotificationService.Worker/NotificationService.Worker.csproj
```
Сервис:
- Подключается к `10.9.2.80:9092` (или к указанному в конфиге брокеру).
- Подписывается на топики с consumer group `notification-service`.
- Логирует сформированные уведомления в консоль (симуляция отправки пользователю).
- Проверяет идемпотентность по `eventId` через потокобезопасный in-memory индекс.
- Сохраняет историю в файл `data/notifications-history.json`.
- Делает ручной коммит смещений (manual commit) только после успешного сохранения.

### 3.3. Запуск сидера (NotificationSeeder.Worker)
В отдельном терминале запустите тест-продюсер:
```bash
dotnet run --project src/NotificationSeeder.Worker/NotificationSeeder.Worker.csproj
```
Сидер выполняет 3 сценария:
1. **Happy Path Flow**:
   - Отправляет `order.created` для Заказа #101 (Пользователь A).
   - Ждёт 1 секунду, затем отправляет `payment.completed` для Заказа #101.
2. **Payment Failure & Cancellation Flow**:
   - Отправляет `order.created` для Заказа #102 (Пользователь B).
   - Ждёт 1 секунду, затем отправляет `payment.failed` ("Insufficient funds in bank account") для Заказа #102.
   - Ждёт 1 секунду, затем отправляет `order.cancelled` для Заказа #102.
3. **Проверка идемпотентности**:
   - Ждёт 1 секунду и повторно отправляет дубликат `order.created` с тем же самым `eventId`.
   - `NotificationService.Worker` распознает дубликат и пропускает его с сообщением `[IDEMPOTENCY SKIP]`.

---

## 4. Хранилище истории (Persistence)

- **Файл**: `data/notifications-history.json` (создаётся автоматически в рабочей директории).
- **Надёжность**: Запись выполняется атомарно, при перезапуске история считывается и восстанавливает in-memory индексы.
- **Интерфейс**: `INotificationHistoryService` предоставляет методы `GetAllAsync()`, `GetByUserIdAsync(Guid)`, `GetByOrderIdAsync(Guid)`, `GetStatsAsync()`.
