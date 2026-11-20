# kafka-lab-vipcs

> ограничения: брокер колледжа 10.9.2.80:9092 виден только из сети колледжа, дома работаем через docker compose. сервисов пока нет, есть только каркас репозитория.

лабораторная работа 9: микросервисы на .NET Worker Service + Apache Kafka, команда 2.

## структура

```
kafka-lab-vipcs/
  contracts/       схемы событий и таблица топиков
  docker-compose.yml
  .github/workflows/ci.yml   шаблон ci для репо сервиса
```

код сервисов живёт в отдельных репозиториях, здесь только общее.

## локальный брокер

```bash
# поднимаем kafka (KRaft, один брокер) и kafka-ui на http://localhost:8080
docker compose up -d
```

топики команды 2 имеют суффикс `2`, список в `contracts/events.md`. правила работы с git в `CONTRIBUTING.md`. английская версия: `README.md`.
