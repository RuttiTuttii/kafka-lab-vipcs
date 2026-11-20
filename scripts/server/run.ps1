# usage: powershell -ExecutionPolicy Bypass -File scripts\server\run.ps1 -Target user@host -Action diag|deploy [-Yes] [-Suffix 2]
param(
    [Parameter(Mandatory = $true)][string]$Target,
    [Parameter(Mandatory = $true)][ValidateSet("diag", "deploy")][string]$Action,
    [switch]$Yes,
    [string]$Suffix = "2"
)

$here = $PSScriptRoot
$hostIp = $Target.Split("@")[-1]
$flag = ""
if ($Yes) { $flag = "--yes" }

# copy the script first: piping into ssh from powershell would add CRLF
$script = if ($Action -eq "diag") { "remote-diag.sh" } else { "remote-deploy.sh" }
scp "$here\$script" "${Target}:/tmp/$script"
if ($LASTEXITCODE -ne 0) { Write-Host "FAIL: scp did not work, check ssh access"; exit 1 }

# strip CR on the server side, then run
if ($Action -eq "diag") {
    ssh $Target "tr -d '\r' < /tmp/$script | bash -s"
} else {
    ssh $Target "tr -d '\r' < /tmp/$script | bash -s -- $hostIp $Suffix $flag"
}
