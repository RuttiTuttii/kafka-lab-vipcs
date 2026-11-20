# runs ALL five services on this pc, a demo flow and a combined log stream
# usage: powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1 [-Broker 10.9.2.80:9092] [-Suffix 2] [-NoDemo] [-SkipBuild]
param(
    [string]$Broker = "10.9.2.80:9092",
    [string]$Suffix = "2",
    [switch]$NoDemo,
    [switch]$SkipBuild
)

$root = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $root "logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
Remove-Item "$logDir\*.log" -ErrorAction SilentlyContinue
$user = "11111111-1111-1111-1111-111111111111"

$list = @(
    @{ Name = "user";         Sln = "Users.sln";               Proj = "Users.Service";               Port = 5003 },
    @{ Name = "order";        Sln = "Orders.sln";              Proj = "Orders.Service";              Port = 5001 },
    @{ Name = "payment";      Sln = "Payments.sln";            Proj = "Payments.Service";            Port = 5002 },
    @{ Name = "inventory";    Sln = "Inventory.sln";           Proj = "Inventory.Service";           Port = 5004 },
    @{ Name = "notification"; Sln = "NotificationService.sln"; Proj = "NotificationService.Worker"; Port = 0 }
)

function Step($text) { Write-Host ""; Write-Host "== $text" }

Step "check dotnet"
$v = dotnet --version 2>$null
if (-not $v) { Write-Host "FAIL: dotnet sdk not found"; exit 1 }
Write-Host "dotnet $v"

Step "check broker $Broker"
$parts = $Broker.Split(":")
$tcp = Test-NetConnection $parts[0] -Port ([int]$parts[1]) -WarningAction SilentlyContinue
Write-Host "ping=$($tcp.PingSucceeded) tcp=$($tcp.TcpTestSucceeded)"
if (-not $tcp.TcpTestSucceeded) { Write-Host "WARN: broker port is not reachable, services will keep retrying" }

if (-not $SkipBuild) {
    Step "build"
    foreach ($s in $list) {
        dotnet build (Join-Path $root "services\$($s.Name)\$($s.Sln)") -c Release --nologo -v q | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: build $($s.Name)"; exit 1 }
        Write-Host "built $($s.Name)"
    }
}

Step "start services"
$env:Kafka__BootstrapServers = $Broker
$env:Kafka__TopicSuffix = $Suffix
$procs = @()
foreach ($s in $list) {
    if ($s.Port -gt 0) { $env:Urls = "http://0.0.0.0:$($s.Port)" } else { Remove-Item Env:\Urls -ErrorAction SilentlyContinue }
    $bin = Join-Path $root "services\$($s.Name)\src\$($s.Proj)\bin\Release\net10.0"
    $out = Join-Path $logDir "$($s.Name).log"
    $err = Join-Path $logDir "$($s.Name).err.log"
    $procs += Start-Process dotnet -ArgumentList "$($s.Proj).dll" -WorkingDirectory $bin -RedirectStandardOutput $out -RedirectStandardError $err -PassThru -NoNewWindow
    Write-Host "started $($s.Name)"
}

function Wait-Final($id) {
    for ($i = 0; $i -lt 45; $i++) {
        try {
            $o = Invoke-RestMethod "http://localhost:5001/orders/$id"
            if ($o.status -eq "Confirmed" -or $o.status -eq "Cancelled") { return "$($o.status) $($o.cancelReason)" }
        } catch { }
        Start-Sleep -Seconds 1
    }
    return "timeout"
}

function New-Order($sku, $qty, $price) {
    $body = @{ userId = $user; items = @(@{ sku = $sku; productName = $sku; quantity = $qty; unitPrice = $price }) } | ConvertTo-Json -Depth 5
    (Invoke-RestMethod -Method Post "http://localhost:5001/orders" -ContentType "application/json" -Body $body).id
}

try {
    Step "wait for apis"
    foreach ($p in 5001, 5002, 5003, 5004) {
        $ok = $false
        for ($i = 0; $i -lt 40; $i++) {
            try { Invoke-RestMethod "http://localhost:$p/health" -TimeoutSec 2 | Out-Null; $ok = $true; break } catch { Start-Sleep -Seconds 1 }
        }
        if (-not $ok) { Write-Host "FAIL: nothing on port $p, see logs\"; exit 1 }
        Write-Host "port $p is up"
    }

    if (-not $NoDemo) {
        Step "demo 1: normal order, expect Confirmed"
        $id = New-Order "sku-001" 2 1500; Write-Host "order $id -> $(Wait-Final $id)"
        Step "demo 2: not enough stock, expect Cancelled by inventory"
        $id = New-Order "sku-004" 99 100; Write-Host "order $id -> $(Wait-Final $id)"
        Step "demo 3: amount over payment limit, expect Cancelled by payment"
        $id = New-Order "sku-002" 1 60000; Write-Host "order $id -> $(Wait-Final $id)"
        Step "stock";    Invoke-RestMethod "http://localhost:5004/items" | ConvertTo-Json -Compress
        Step "payments"; Invoke-RestMethod "http://localhost:5002/payments" | ConvertTo-Json -Compress
    }

    # combined log stream until ctrl+c
    Step "logs in $logDir (ctrl+c stops everything)"
    $seen = @{}
    while ($true) {
        foreach ($s in $list) {
            $f = Join-Path $logDir "$($s.Name).log"
            if (-not (Test-Path $f)) { continue }
            $lines = @(Get-Content $f -ErrorAction SilentlyContinue)
            $from = 0; if ($seen.ContainsKey($f)) { $from = $seen[$f] }
            for ($i = $from; $i -lt $lines.Count; $i++) { Write-Host "[$($s.Name)] $($lines[$i])" }
            $seen[$f] = $lines.Count
        }
        Start-Sleep -Seconds 1
    }
}
finally {
    foreach ($p in $procs) { if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force } }
    Write-Host "stopped all services"
}
