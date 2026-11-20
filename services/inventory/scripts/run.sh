#!/usr/bin/env bash
# one-shot run: build, test, start the service, optional seed, tail logs
# usage: ./scripts/run.sh [broker] [suffix] [--seed]
set -u
broker="${1:-10.9.2.80:9092}"
suffix="${2:-2}"
seed="${3:-}"
port=5004
root="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$root/logs"
out="$root/logs/inventory-$(date +%Y%m%d-%H%M%S).out.log"

echo "== check broker $broker"
host="${broker%%:*}"; bport="${broker##*:}"
if (exec 3<>"/dev/tcp/$host/$bport") 2>/dev/null; then echo "tcp ok"; else echo "WARN: broker port is not reachable, the service will keep retrying"; fi

echo "== build and tests"
(cd "$root" && dotnet build -c Release --nologo -v q && dotnet test -c Release --no-build --nologo) || { echo "FAIL: build or tests"; exit 1; }

echo "== start service on port $port"
export Kafka__BootstrapServers="$broker" Kafka__TopicSuffix="$suffix" Kafka__GroupId="inventory-service$suffix" Urls="http://0.0.0.0:$port"
cd "$root/src/Inventory.Service/bin/Release/net10.0"
dotnet Inventory.Service.dll > "$out" 2>&1 &
pid=$!
trap 'kill $pid 2>/dev/null; echo; echo "stopped. logs: $out"' EXIT

ok=0
for i in $(seq 1 20); do curl -fs "http://localhost:$port/health" >/dev/null 2>&1 && { ok=1; break; }; sleep 1; done
[ "$ok" -eq 1 ] || { echo "FAIL: service did not answer /health"; cat "$out"; exit 1; }
echo "service is up: http://localhost:$port"

if [ "$seed" = "--seed" ]; then
  echo "== seed orders"; curl -fs -X POST "http://localhost:$port/seed/orders"; echo; sleep 5
fi
echo "== stock"; curl -fs "http://localhost:$port/items"; echo
echo "== logs (ctrl+c to stop), file: $out"
tail -n 100 -f "$out"
