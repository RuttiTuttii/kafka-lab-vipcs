#!/usr/bin/env bash
# guarded kafka deploy on the server: refuses if 9092 is already in use
# usage: HOST_IP=<ip> SUFFIX=<n> YES=--yes remote-deploy.sh   (or positional: <ip> <suffix> --yes)
set -eu
host_ip="${HOST_IP:-${1:-}}"
suffix="${SUFFIX:-${2:-2}}"
yes="${YES:-${3:-}}"

# detect the ip on the server when it was not passed
if [ -z "$host_ip" ]; then host_ip="$(hostname -I | awk '{print $1}')"; fi
echo "host_ip=$host_ip suffix=$suffix"
[ "$yes" = "--yes" ] || { echo "dry run: pass --yes to deploy (ask the teacher first)"; exit 0; }

# 1. never touch a running broker
if ss -tln 2>/dev/null | grep -q ':9092\b'; then
  echo "REFUSE: port 9092 is already in use, run diag first"; exit 1
fi

topics=""
for t in user.registered order.created order.cancelled payment.completed payment.failed inventory.reserved inventory.failed; do topics="$topics $t$suffix"; done
dir="$HOME/kafka-lab"
mkdir -p "$dir"

# 2a. docker path
if docker info >/dev/null 2>&1; then
  echo "== deploy via docker"
  cat > "$dir/docker-compose.yml" <<EOF
services:
  kafka:
    image: apache/kafka:3.8.0
    container_name: kafka-lab
    restart: unless-stopped
    ports: ["9092:9092"]
    environment:
      KAFKA_NODE_ID: 1
      KAFKA_PROCESS_ROLES: broker,controller
      KAFKA_LISTENERS: PLAINTEXT://:9092,CONTROLLER://:9093
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://$host_ip:9092
      KAFKA_CONTROLLER_LISTENER_NAMES: CONTROLLER
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT
      KAFKA_CONTROLLER_QUORUM_VOTERS: 1@localhost:9093
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 1
      KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS: 0
EOF
  (cd "$dir" && docker compose up -d)
  run_topics() { docker exec kafka-lab /opt/kafka/bin/kafka-topics.sh "$@"; }

# 2b. plain java path
elif command -v java >/dev/null 2>&1; then
  echo "== deploy via tarball"
  ver="3.9.0"
  if [ ! -d "$dir/kafka_2.13-$ver" ]; then
    curl -fsSL "https://archive.apache.org/dist/kafka/$ver/kafka_2.13-$ver.tgz" -o "$dir/k.tgz"
    tar -xzf "$dir/k.tgz" -C "$dir"
  fi
  k="$dir/kafka_2.13-$ver"
  cfg="$k/config/kraft/server.properties"
  sed -i "s|^advertised.listeners=.*|advertised.listeners=PLAINTEXT://$host_ip:9092|; s|^log.dirs=.*|log.dirs=$dir/data|" "$cfg"
  "$k/bin/kafka-storage.sh" format -t "$("$k/bin/kafka-storage.sh" random-uuid)" -c "$cfg" || true
  nohup "$k/bin/kafka-server-start.sh" "$cfg" > "$dir/kafka.log" 2>&1 &
  run_topics() { "$k/bin/kafka-topics.sh" "$@"; }
else
  echo "REFUSE: neither docker nor java on the server"; exit 1
fi

# 3. wait and create topics
for i in $(seq 1 30); do run_topics --bootstrap-server localhost:9092 --list >/dev/null 2>&1 && break; sleep 3; done
for t in $topics; do run_topics --bootstrap-server localhost:9092 --create --if-not-exists --topic "$t" --partitions 1 --replication-factor 1; done
run_topics --bootstrap-server localhost:9092 --list
echo "done. firewall is not touched, if needed run: sudo ufw allow 9092/tcp"
