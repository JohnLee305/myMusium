[CmdletBinding()]
param([string]$ArchivePath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $ArchivePath){$ArchivePath=Join-Path $root 'museum-archive'}
$archive=(Resolve-Path -LiteralPath $ArchivePath).Path
$folder=Join-Path $root ('logs/text-occlusion-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $folder|Out-Null
$log=Join-Path $folder 'player.log'
$arguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',('"'+$archive+'"'),'-museum-screenshot',('"'+$folder+'/start.png"'),'-museum-text-occlusion-smoke','-logFile',('"'+$log+'"'))
$player=Start-Process -FilePath (Join-Path $root 'Builds/Windows/VRMuseum.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
$null=$player.Handle
if(-not $player.WaitForExit(180000)){throw "Text occlusion test timed out: $log"}
$output=Get-Content -LiteralPath $log -Raw
$output -split "`n"|Where-Object {$_ -match 'WORLD_TEXT_'}|Write-Output
if($player.ExitCode -ne 0 -or -not $output.Contains('WORLD_TEXT_SMOKE_PASS') -or -not $output.Contains('MUSEUM_SMOKE_PASS')){throw "Text occlusion failed: $folder"}
Write-Output "TEXT_OCCLUSION_PASS: $folder"
