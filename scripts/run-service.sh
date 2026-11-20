#!/usr/bin/env bash
# runs ONE service on this machine against the broker, logs go to the console and to logs/<name>.log
# usage: ./scripts/run-service.sh <user|order|payment|inventory|notification> [broker] [suffix]
set -u
name="${1:?usage: run-service.sh <user|order|payment|inventory|notification> [broker] [suffix]}"
broker="${2:-10.9.2.80:9092}"
suffix="${3:-2}"
root="$(cd "$(dirname "$0")/.." && pwd)"

case "$name" in
  user)         sln=Users.sln;               proj=Users.Service;               port=5003 ;;
  order)        sln=Orders.sln;              proj=Orders.Service;              port=5001 ;;
  payment)      sln=Payments.sln;            proj=Payments.Service;            port=5002 ;;
  inventory)    sln=Inventory.sln;           proj=Inventory.Service;           port=5004 ;;
  notification) sln=NotificationService.sln; proj=NotificationService.Worker;  port=0 ;;
  *) echo "unknown service: $name"; exit 1 ;;
esac

mkdir -p "$root/logs"
echo "== build $name"
dotnet build "$root/services/$name/$sln" -c Release --nologo -v q || { echo "FAIL: build"; exit 1; }

export Kafka__BootstrapServers="$broker" Kafka__TopicSuffix="$suffix"
[ "$port" != "0" ] && export Urls="http://0.0.0.0:$port" && echo "api: http://localhost:$port/health"

echo "== run $name (ctrl+c to stop)"
cd "$root/services/$name/src/$proj/bin/Release/net10.0" && dotnet "$proj.dll" 2>&1 | tee "$root/logs/$name.log"
