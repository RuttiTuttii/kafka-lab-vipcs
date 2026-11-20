#!/usr/bin/env bash
# runs all five services from services/ against one broker, runs a demo flow, streams logs
# usage: ./scripts/run-all.sh [broker] [suffix] [--no-demo] [--skip-build]
set -u
broker="${1:-10.9.2.80:9092}"
suffix="${2:-2}"
shift 2 2>/dev/null || true
demo=1; build=1
for a in "$@"; do [ "$a" = "--no-demo" ] && demo=0; [ "$a" = "--skip-build" ] && build=0; done

root="$(cd "$(dirname "$0")/.." && pwd)"
logs="$root/logs"; mkdir -p "$logs"; rm -f "$logs"/*.log
user=11111111-1111-1111-1111-111111111111

# name|bin dir|dll|port|solution
services="user|services/user/src/Users.Service/bin/Release/net10.0|Users.Service|5003|Users.sln
order|services/order/src/Orders.Service/bin/Release/net10.0|Orders.Service|5001|Orders.sln
payment|services/payment/src/Payments.Service/bin/Release/net10.0|Payments.Service|5002|Payments.sln
inventory|services/inventory/src/Inventory.Service/bin/Release/net10.0|Inventory.Service|5004|Inventory.sln
notification|services/notification/src/NotificationService.Worker/bin/Release/net10.0|NotificationService.Worker|0|NotificationService.sln"

pids=""
cleanup() { echo; echo "stopping services"; for p in $pids; do kill "$p" 2>/dev/null; done; }
trap cleanup EXIT INT TERM

echo "== check dotnet"; dotnet --version || { echo "FAIL: dotnet sdk not found"; exit 1; }

echo "== check broker $broker"
host="${broker%%:*}"; bport="${broker##*:}"
if (exec 3<>"/dev/tcp/$host/$bport") 2>/dev/null; then echo "tcp ok"; else echo "WARN: broker port is not reachable, services will keep retrying"; fi

if [ "$build" -eq 1 ]; then
  echo "== build"
  echo "$services" | while IFS='|' read -r name bin dll port sln; do
    dir="$(dirname "$root/$bin")"; sdir="$root/services/$name"
    dotnet build "$sdir/$sln" -c Release --nologo -v q >/dev/null 2>&1 && echo "built $name" || { echo "FAIL: build $name"; dotnet build "$sdir/$sln" -c Release --nologo 2>&1 | grep -E 'error' | head -5; exit 1; }
  done || exit 1
fi

echo "== start services"
export Kafka__BootstrapServers="$broker" Kafka__TopicSuffix="$suffix"
while IFS='|' read -r name bin dll port sln; do
  [ "$port" != "0" ] && export Urls="http://0.0.0.0:$port" || unset Urls
  (cd "$root/$bin" && exec dotnet "$dll.dll" > "$logs/$name.log" 2>&1) &
  pids="$pids $!"
  echo "started $name (pid $!)"
done <<< "$services"

echo "== wait for apis"
for entry in order:5001 payment:5002 user:5003 inventory:5004; do
  n="${entry%%:*}"; p="${entry##*:}"; ok=0
  for i in $(seq 1 40); do curl -fs "http://localhost:$p/health" >/dev/null 2>&1 && { ok=1; break; }; sleep 1; done
  [ "$ok" -eq 1 ] && echo "$n up on $p" || { echo "FAIL: $n did not answer, log:"; tail -20 "$logs/$n.log"; exit 1; }
done

status() { curl -fs "http://localhost:5001/orders/$1" | grep -o '"status":"[A-Za-z]*"' | cut -d'"' -f4; }
wait_final() { # waits for Confirmed or Cancelled
  for i in $(seq 1 45); do s="$(status "$1")"; [ "$s" = "Confirmed" ] || [ "$s" = "Cancelled" ] && { echo "$s"; return; }; sleep 1; done; echo "timeout (last: $s)"
}
new_order() { # sku qty unitPrice
  curl -fs -X POST "http://localhost:5001/orders" -H 'content-type: application/json' \
    -d "{\"userId\":\"$user\",\"items\":[{\"sku\":\"$1\",\"productName\":\"$1\",\"quantity\":$2,\"unitPrice\":$3}]}" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4
}

if [ "$demo" -eq 1 ]; then
  echo "== demo 1: normal order, expect Confirmed"
  id1="$(new_order sku-001 2 1500)"; echo "order $id1 -> $(wait_final "$id1")"
  echo "== demo 2: not enough stock, expect Cancelled by inventory"
  id2="$(new_order sku-004 99 100)"; echo "order $id2 -> $(wait_final "$id2")"
  echo "== demo 3: amount over payment limit, expect Cancelled by payment"
  id3="$(new_order sku-002 1 60000)"; echo "order $id3 -> $(wait_final "$id3")"
  echo "== stock"; curl -s http://localhost:5004/items; echo
  echo "== payments"; curl -s http://localhost:5002/payments; echo
  echo "== user"; curl -s "http://localhost:5003/users/$user"; echo
fi

echo "== logs in $logs (ctrl+c stops everything)"
tail -n 0 -F "$logs"/*.log
