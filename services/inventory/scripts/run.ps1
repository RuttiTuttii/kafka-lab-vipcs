# one-shot run: check dotnet and broker, build, test, start the service, optional seed, tail logs
# usage: powershell -ExecutionPolicy Bypass -File scripts\run.ps1 [-Broker 10.9.2.80:9092] [-Suffix 2] [-Seed] [-SkipTests] [-Port 5004]
param(
    [string]$Broker = "10.9.2.80:9092",
    [string]$Suffix = "2",
    [switch]$Seed,
    [switch]$SkipTests,
    [int]$Port = 5004
)

$root = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $root "logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$out = Join-Path $logDir "inventory-$stamp.out.log"
$err = Join-Path $logDir "inventory-$stamp.err.log"

function Step($text) { Write-Host ""; Write-Host "== $text" }

# 1. dotnet sdk
Step "check dotnet"
$v = dotnet --version 2>$null
if (-not $v) { Write-Host "FAIL: dotnet sdk not found"; exit 1 }
Write-Host "dotnet $v"

# 2. broker reachability, only a warning: the link may be flaky and the consumer retries
Step "check broker $Broker"
$parts = $Broker.Split(":")
$tcp = Test-NetConnection $parts[0] -Port ([int]$parts[1]) -WarningAction SilentlyContinue
Write-Host "ping=$($tcp.PingSucceeded) tcp=$($tcp.TcpTestSucceeded) from=$($tcp.SourceAddress.IPAddress)"
if (-not $tcp.TcpTestSucceeded) { Write-Host "WARN: broker port is not reachable, the service will keep retrying" }

# 3. build and tests
Step "build"
Push-Location $root
dotnet build -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "FAIL: build"; exit 1 }
if (-not $SkipTests) {
    Step "tests"
    dotnet test -c Release --no-build --nologo
    if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "FAIL: tests"; exit 1 }
}
Pop-Location

# 4. start the built dll from its output folder, where appsettings.json lives
Step "start service on port $Port"
$env:Kafka__BootstrapServers = $Broker
$env:Kafka__TopicSuffix = $Suffix
$env:Kafka__GroupId = "inventory-service$Suffix"
$env:Urls = "http://0.0.0.0:$Port"
$bin = Join-Path $root "src\Inventory.Service\bin\Release\net10.0"
$proc = Start-Process dotnet -ArgumentList "Inventory.Service.dll" -WorkingDirectory $bin -RedirectStandardOutput $out -RedirectStandardError $err -PassThru -NoNewWindow

try {
    # 5. wait for /health, up to 20 s
    $ok = $false
    for ($i = 1; $i -le 20; $i++) {
        try { Invoke-RestMethod "http://localhost:$Port/health" -TimeoutSec 2 | Out-Null; $ok = $true; break }
        catch { Start-Sleep -Seconds 1 }
    }
    if (-not $ok) {
        Write-Host "FAIL: service did not answer /health, logs:"
        Get-Content $out, $err -ErrorAction SilentlyContinue
        exit 1
    }
    Write-Host "service is up: http://localhost:$Port"

    # 6. optional seeder: sends test order.created events to the broker
    if ($Seed) {
        Step "seed orders"
        try { Invoke-RestMethod -Method Post "http://localhost:$Port/seed/orders" | ConvertTo-Json -Compress }
        catch { Write-Host "seed failed: $($_.Exception.Message)" }
        Start-Sleep -Seconds 5
    }

    Step "stock"
    Invoke-RestMethod "http://localhost:$Port/items" | ConvertTo-Json -Compress

    # 7. logs until ctrl+c
    Step "logs (ctrl+c to stop), files: $out"
    Get-Content $out -Wait -Tail 100
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    Write-Host ""
    Write-Host "stopped. logs: $out"
    if ((Test-Path $err) -and (Get-Item $err).Length -gt 0) { Write-Host "errors: $err" }
}
