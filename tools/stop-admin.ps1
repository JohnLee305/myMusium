[CmdletBinding()]
param([int]$Port=4181)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$url="http://127.0.0.1:$Port"
$health=Invoke-RestMethod "$url/api/health" -TimeoutSec 2
if($health.service -ne 'vr-museum-curator') {throw 'This is not the museum admin server.'}
$listener=Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort $Port -State Listen
$serverPath=Join-Path $root 'apps\curator-admin\server.mjs'
$serverProcess=Get-CimInstance Win32_Process -Filter "ProcessId = $($listener.OwningProcess)"
if(-not $serverProcess -or -not $serverProcess.CommandLine.Contains($serverPath)){throw 'Server process does not match this project.'}
# The shutdown endpoint verifies the token and refuses while a save is in flight.
$session=Invoke-RestMethod "$url/api/session" -TimeoutSec 3
Invoke-RestMethod "$url/api/shutdown" -Method Post -ContentType 'application/octet-stream' -Headers @{'X-Museum-Token'=$session.token;'X-Museum-Metadata'='%7B%7D'} -Body ([byte[]]@()) -TimeoutSec 5 | Out-Null
for($i=0;$i -lt 30;$i++) {
 $connection=Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
 if(-not $connection){Write-Output "Museum admin stopped on port $Port.";return}
 Start-Sleep -Milliseconds 100
}
throw 'Shutdown is still completing. Try again shortly.'
