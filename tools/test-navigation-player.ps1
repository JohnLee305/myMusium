[CmdletBinding()]
param([string]$ArchivePath,[switch]$ShowWindow,[ValidateSet(6,12)][int]$GuestCount=6)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $ArchivePath){$ArchivePath=(Get-Content (Join-Path $root 'logs/navigation-fixture-path.txt') -Raw).Trim()}
$ArchivePath=(Resolve-Path -LiteralPath $ArchivePath).Path
if(-not $ArchivePath.StartsWith((Join-Path $root 'logs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use an isolated test archive under logs.'}
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$log=Join-Path $ArchivePath "tour-player-$GuestCount-$stamp.log"
$png=Join-Path $ArchivePath "tour-player-$GuestCount-$stamp.png"
$arguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',('"'+$ArchivePath+'"'),'-museum-tour-smoke','-museum-screenshot',('"'+$png+'"'),'-logFile',('"'+$log+'"'))
$arguments+=@('-museum-tour-count',[string]$GuestCount)
$windowStyle=if($ShowWindow){'Normal'}else{'Hidden'}
$player=Start-Process -FilePath (Join-Path $root 'Builds/Windows/VRMuseum.exe') -ArgumentList $arguments -WindowStyle $windowStyle -PassThru
$null=$player.Handle
if(-not $player.WaitForExit(120000)){try{$player.Kill()}catch{};throw "Tour timeout: $log"}
$output=Get-Content -LiteralPath $log -Raw
if($player.ExitCode -ne 0 -or -not $output.Contains('TOUR_SMOKE_PASS') -or -not $output.Contains('MUSEUM_SMOKE_PASS') -or $output -match '(NullReferenceException|MissingReferenceException):'){throw "Tour failed: $log"}
Write-Output "TOUR_PLAYER_PASS: $log | $png"
if($output -notmatch "TOUR_CROWD_CHECK: target=$GuestCount peak=$GuestCount .*passed=True"){throw "Requested crowd was not reached: $log"}
