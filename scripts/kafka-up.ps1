# local kafka bootstrap: compose up, create 7 topics, smoke test produce/consume
# usage: powershell -ExecutionPolicy Bypass -File scripts\kafka-up.ps1 [-Suffix 2]
param([string]$Suffix = "2")

$ErrorActionPreference = "Continue"
$container = "kafka-lab"
$bin = "/opt/kafka/bin"
$topics = "user.registered order.created order.cancelled payment.completed payment.failed inventory.reserved inventory.failed".Split(" ") | ForEach-Object { "$_$Suffix" }
$root = Split-Path -Parent $PSScriptRoot

function Step($text) { Write-Host ""; Write-Host "== $text" }

# 1. docker must exist
Step "check docker"
docker version --format "client {{.Client.Version}}, server {{.Server.Version}}"
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAIL: docker is not available or the daemon is not running (start Docker Desktop, WSL2 backend)"
    exit 1
}

# 2. start broker and ui
Step "compose up"
Push-Location $root
docker compose up -d
$up = $LASTEXITCODE
Pop-Location
if ($up -ne 0) { Write-Host "FAIL: docker compose up exited with $up"; exit 1 }

# 3. wait until broker answers, up to 90 s
Step "wait for broker"
$ready = $false
for ($i = 1; $i -le 30; $i++) {
    docker exec $container "$bin/kafka-broker-api-versions.sh" --bootstrap-server localhost:9092 *> $null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Write-Host "attempt $i/30: not ready"
    Start-Sleep -Seconds 3
}
if (-not $ready) {
    Write-Host "FAIL: broker did not come up, last logs:"
    docker logs --tail 30 $container
    exit 1
}
Write-Host "broker is up"

# 4. create topics, 1 partition, idempotent
Step "create topics (suffix '$Suffix')"
foreach ($t in $topics) {
    docker exec $container "$bin/kafka-topics.sh" --bootstrap-server localhost:9092 --create --if-not-exists --topic $t --partitions 1 --replication-factor 1
}
docker exec $container "$bin/kafka-topics.sh" --bootstrap-server localhost:9092 --list

# 5. smoke test: one message through payment.completed
Step "smoke test"
$probe = "payment.completed$Suffix"
$msg = "probe-" + [guid]::NewGuid().ToString() + ':{"eventId":"smoke","orderId":"smoke"}'
$msg | docker exec -i $container "$bin/kafka-console-producer.sh" --bootstrap-server localhost:9092 --topic $probe --property parse.key=true --property key.separator=:
$got = docker exec $container "$bin/kafka-console-consumer.sh" --bootstrap-server localhost:9092 --topic $probe --from-beginning --max-messages 1 --timeout-ms 15000 2>$null
Write-Host "consumed: $got"
if ($got -match "smoke") { Write-Host "PASS: produce and consume work"; exit 0 }
Write-Host "FAIL: message did not come back"
exit 1
