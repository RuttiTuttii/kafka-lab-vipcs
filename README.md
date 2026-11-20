# kafka-lab-vipcs

> limits: the college broker 10.9.2.80:9092 is reachable only from the college network. use docker compose for local work. the services are not written yet, only the repo skeleton exists.

lab 9: microservices on .NET Worker Service + Apache Kafka, team 2.

## layout

```
kafka-lab-vipcs/
  contracts/       event schemas and topic table
  docker-compose.yml
  .github/workflows/ci.yml   ci template for a service repo
```

service code lives in separate repos, this one holds shared files only.

## local broker

```bash
# start kafka (KRaft, one broker) and kafka-ui on http://localhost:8080
docker compose up -d
```

topics of team 2 carry the suffix `2`, see `contracts/events.md`. workflow is in `CONTRIBUTING.md`. russian version: `README.ru.md`.
