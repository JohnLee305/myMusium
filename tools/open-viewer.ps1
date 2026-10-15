[CmdletBinding()]
param([switch]$Vr)
$ErrorActionPreference = 'Stop'
$museumRoot = Split-Path -Parent $PSScriptRoot
$player = Join-Path $museumRoot 'Builds/Windows/VRMuseum.exe'
if (-not (Test-Path -LiteralPath $player)) { throw 'Build the viewer first with tools/run-unity.ps1 -Task Build.' }
$logPath = Join-Path $museumRoot "logs/viewer-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
$arguments = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-logFile',"`"$logPath`"")
if ($Vr) { $arguments += '-museum-vr' }
# Interactive viewer: show its window to the user.
Start-Process -FilePath $player -ArgumentList $arguments
Write-Output "Viewer launched. Log: $logPath"
