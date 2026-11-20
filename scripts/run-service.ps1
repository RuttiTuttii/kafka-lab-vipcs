# runs ONE service on this pc against the broker, logs go to the console and to logs\<name>.log
# usage: powershell -ExecutionPolicy Bypass -File scripts\run-service.ps1 -Name order [-Broker 10.9.2.80:9092] [-Suffix 2] [-SkipBuild]
param(
    [Parameter(Mandatory = $true)][ValidateSet("user", "order", "payment", "inventory", "notification")][string]$Name,
    [string]$Broker = "10.9.2.80:9092",
    [string]$Suffix = "2",
    [switch]$SkipBuild
)

$root = Split-Path -Parent $PSScriptRoot
$map = @{
    user         = @{ Sln = "Users.sln";               Proj = "Users.Service";               Port = 5003 }
    order        = @{ Sln = "Orders.sln";              Proj = "Orders.Service";              Port = 5001 }
    payment      = @{ Sln = "Payments.sln";            Proj = "Payments.Service";            Port = 5002 }
    inventory    = @{ Sln = "Inventory.sln";           Proj = "Inventory.Service";           Port = 5004 }
    notification = @{ Sln = "NotificationService.sln"; Proj = "NotificationService.Worker"; Port = 0 }
}
$svc = $map[$Name]
$dir = Join-Path $root "services\$Name"
$bin = Join-Path $dir "src\$($svc.Proj)\bin\Release\net10.0"
$logDir = Join-Path $root "logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir "$Name.log"

function Step($text) { Write-Host ""; Write-Host "== $text" }

# 1. dotnet sdk
Step "check dotnet"
$v = dotnet --version 2>$null
if (-not $v) { Write-Host "FAIL: dotnet sdk not found"; exit 1 }
Write-Host "dotnet $v"

# 2. broker reachability: only a warning, the service waits for the broker and retries
Step "check broker $Broker"
$parts = $Broker.Split(":")
$tcp = Test-NetConnection $parts[0] -Port ([int]$parts[1]) -WarningAction SilentlyContinue
Write-Host "ping=$($tcp.PingSucceeded) tcp=$($tcp.TcpTestSucceeded) from=$($tcp.SourceAddress.IPAddress)"
if (-not $tcp.TcpTestSucceeded) { Write-Host "WARN: broker port is not reachable, the service will keep retrying" }

# 3. build
if (-not $SkipBuild) {
    Step "build $Name"
    dotnet build (Join-Path $dir $svc.Sln) -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: build"; exit 1 }
}

# 4. run in the foreground, state files live in data\ next to the dll
Step "run $Name (ctrl+c to stop), log: $log"
$env:Kafka__BootstrapServers = $Broker
$env:Kafka__TopicSuffix = $Suffix
if ($svc.Port -gt 0) { $env:Urls = "http://0.0.0.0:$($svc.Port)"; Write-Host "api: http://localhost:$($svc.Port)/health" }
Push-Location $bin
dotnet "$($svc.Proj).dll" 2>&1 | Tee-Object -FilePath $log
Pop-Location
