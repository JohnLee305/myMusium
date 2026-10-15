[CmdletBinding()]
param([int]$Port = 4181, [string]$ArchivePath, [switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($ArchivePath)) { $ArchivePath = Join-Path $root 'museum-archive' }
$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
$node = if ($nodeCommand) { $nodeCommand.Source } else { Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe' }
if (-not (Test-Path -LiteralPath $node)) { throw 'Node.js was not found. Install Node.js or add node.exe to PATH.' }
if ($Port -lt 1024 -or $Port -gt 65535) { throw 'Port must be between 1024 and 65535.' }
$ArchivePath = [IO.Path]::GetFullPath($ArchivePath)
$url = "http://127.0.0.1:$Port"
$health = $null
try { $health = Invoke-RestMethod "$url/api/health" -TimeoutSec 2 } catch { }
if ($health -and ($health.service -ne 'vr-museum-curator' -or $health.archive -ne $ArchivePath)) { throw 'This port belongs to a different archive or application. Choose another -Port.' }
if (-not $health) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $out = Join-Path $root "logs/admin-$stamp.log"
    $err = Join-Path $root "logs/admin-$stamp-error.log"
    New-Item -ItemType Directory -Path (Join-Path $root 'logs') -Force | Out-Null
    $server = Join-Path $root 'apps/curator-admin/server.mjs'
    $process = Start-Process -FilePath $node -ArgumentList @("`"$server`"",'--port',"$Port",'--archive',"`"$ArchivePath`"") -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err -PassThru
    for ($i=0; $i -lt 30; $i++) {
        if ($process.HasExited) { throw "Admin server exited. Read $err" }
        try { $health = Invoke-RestMethod "$url/api/health" -TimeoutSec 1; break } catch { Start-Sleep -Milliseconds 200 }
    }
    if (-not $health -or $health.service -ne 'vr-museum-curator' -or $health.archive -ne $ArchivePath) { throw "Admin did not start correctly. Read $err" }
    Write-Output "Admin started (PID $($process.Id)). Log: $out"
}
if (-not $NoBrowser) { Start-Process $url }
Write-Output "Admin URL: $url"

