$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$archive=(Get-Content "$root/logs/reaction-fixture-path.txt" -Raw).Trim()
if(-not $archive.StartsWith((Join-Path $root 'logs/reaction-tests-'),[StringComparison]::OrdinalIgnoreCase)){throw 'Expected an isolated reaction test archive.'}
$log=Join-Path $archive 'player.log'
$png=Join-Path $archive 'player.png'
$arguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',"`"$archive`"",'-museum-screenshot',"`"$png`"",'-museum-reaction-smoke','-logFile',"`"$log`"")
$process=Start-Process "$root/Builds/Windows/VRMuseum.exe" -ArgumentList $arguments -PassThru
$null=$process.Handle
if(-not $process.WaitForExit(180000)){try{$process.Kill()}catch{};throw "Reaction player timeout: $log"}
$output=Get-Content $log -Raw
if($process.ExitCode -ne 0 -or -not $output.Contains('REACTION_SMOKE_PASS') -or -not $output.Contains('MUSEUM_SMOKE_PASS') -or $output -match '(NullReferenceException|MissingReferenceException):'){throw "Reaction player failed: $log"}
Write-Output "REACTION_PLAYER_PASS: $log | $png"
