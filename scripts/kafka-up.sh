#!/usr/bin/env bash
# local kafka bootstrap: compose up, create 7 topics, smoke test produce/consume
# usage: ./scripts/kafka-up.sh [suffix]
set -u

suffix="${1:-2}"
container="kafka-lab"
bin="/opt/kafka/bin"
root="$(cd "$(dirname "$0")/.." && pwd)"
topics=""
for t in user.registered order.created order.cancelled payment.completed payment.failed inventory.reserved inventory.failed; do topics="$topics $t$suffix"; done

# 1. docker must exist
echo "== check docker"
docker version --format 'client {{.Client.Version}}, server {{.Server.Version}}' || { echo "FAIL: docker is not available"; exit 1; }

# 2. start broker and ui
echo "== compose up"
(cd "$root" && docker compose up -d) || { echo "FAIL: docker compose up"; exit 1; }

# 3. wait until broker answers, up to 90 s
echo "== wait for broker"
ready=0
for i in $(seq 1 30); do
  if docker exec "$container" "$bin/kafka-broker-api-versions.sh" --bootstrap-server localhost:9092 >/dev/null 2>&1; then ready=1; break; fi
  echo "attempt $i/30: not ready"; sleep 3
done
if [ "$ready" -ne 1 ]; then echo "FAIL: broker did not come up"; docker logs --tail 30 "$container"; exit 1; fi
echo "broker is up"

# 4. create topics, 1 partition, idempotent
echo "== create topics (suffix '$suffix')"
for t in $topics; do
  docker exec "$container" "$bin/kafka-topics.sh" --bootstrap-server localhost:9092 --create --if-not-exists --topic "$t" --partitions 1 --replication-factor 1
done
docker exec "$container" "$bin/kafka-topics.sh" --bootstrap-server localhost:9092 --list

# 5. smoke test: one message through payment.completed
echo "== smoke test"
probe="payment.completed$suffix"
printf 'probe-%s:{"eventId":"smoke","orderId":"smoke"}\n' "$(date +%s)" | docker exec -i "$container" "$bin/kafka-console-producer.sh" --bootstrap-server localhost:9092 --topic "$probe" --property parse.key=true --property key.separator=:
got="$(docker exec "$container" "$bin/kafka-console-consumer.sh" --bootstrap-server localhost:9092 --topic "$probe" --from-beginning --max-messages 1 --timeout-ms 15000 2>/dev/null)"
echo "consumed: $got"
case "$got" in *smoke*) echo "PASS: produce and consume work"; exit 0;; esac
echo "FAIL: message did not come back"; exit 1
