#requires -Version 7.0
[CmdletBinding()]
param([ValidateSet('Build','Launch','Verify')][string]$Task='Launch')
$ErrorActionPreference='Stop'
$museumRoot=Split-Path $PSScriptRoot -Parent
$editor='C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$prototype=Join-Path $museumRoot 'apps/quest-viewer/Builds/DecorPrototype/DecorPrototype.exe'
$logs=Join-Path $museumRoot 'logs'
New-Item -ItemType Directory -Path $logs -Force|Out-Null
if($Task -eq 'Build'){
 if(Get-Process Unity -ErrorAction SilentlyContinue){throw 'Close Unity before this batch build.'}
 $log=Join-Path $logs 'decor-05-build.log'
 $arguments=@('-batchmode','-nographics','-quit','-projectPath',"`"$museumRoot\apps\quest-viewer`"",'-executeMethod','FamilyMuseum.Editor.DecorPrototypeSetup.CreateAndBuild','-logFile',"`"$log`"")
 $process=Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
 $process.WaitForExit()
 if($process.ExitCode -ne 0){throw "Prototype build failed. See $log"}
 Write-Output "Prototype built: $prototype"; return
}
if(-not(Test-Path -LiteralPath $prototype)){throw 'Build the prototype first with -Task Build.'}
if($Task -eq 'Verify'){
 $output=Join-Path $logs ('decor-ui-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
 New-Item -ItemType Directory -Path $output|Out-Null
 # A visual Player check must present its swapchain; a hidden initial window can produce a black capture.
 $process=Start-Process -FilePath $prototype -ArgumentList @('-screen-fullscreen','0','-decor-smoke',"`"$output`"",'-logFile',"`"$output\player.log`"") -WindowStyle Normal -PassThru
 $deadline=(Get-Date).AddSeconds(120)
 while(-not(Test-Path -LiteralPath "$output\ready-for-concurrent.txt") -and -not $process.HasExited -and (Get-Date) -lt $deadline){Start-Sleep -Milliseconds 200}
 if(-not(Test-Path -LiteralPath "$output\ready-for-concurrent.txt")){if(-not $process.HasExited){Stop-Process -Id $process.Id};throw "Prototype verification did not reach restart phase. See $output"}
 foreach($phase in @('concurrent','restart')){
  $probe=Join-Path $output $phase
  $child=Start-Process -FilePath $prototype -ArgumentList @('-screen-fullscreen','0','-decor-probe',"`"$output\store`"","`"$probe`"",'-logFile',"`"$output\$phase-player.log`"") -WindowStyle Normal -PassThru
  if(-not $child.WaitForExit(45000)){Stop-Process -Id $child.Id;throw "Probe timed out: $phase"}
  if($child.ExitCode -ne 0){throw "Probe failed: $phase"}
  $expected=[IO.File]::ReadAllText("$output\expected-world.json")
  if([IO.File]::ReadAllText("$probe\world.json") -cne $expected){throw "Reopened content mismatch: $phase"}
  $state=[IO.File]::ReadAllText("$probe\state.txt")
  if($phase -eq 'concurrent'){
   if($state -ne 'readOnly' -or -not([IO.File]::ReadAllText("$probe\message.txt").Contains('읽기 전용'))){throw 'Concurrent UI must show read-only'}
   [IO.File]::WriteAllText("$output\concurrent-done.txt",'done')
   if(-not $process.WaitForExit(10000)){Stop-Process -Id $process.Id;throw 'Writer did not close'}
  }elseif($state -ne 'ready'){throw 'Writer lock must release after process exits'}
 }
 if($process.ExitCode -ne 0 -or -not(Test-Path -LiteralPath "$output\results.txt")){throw "Prototype verification failed. See $output"}
 Get-Content -LiteralPath "$output\results.txt" -Tail 1
 Write-Output 'DECOR_RESTART_PASS: separate concurrent process read-only UI and fresh process exact saved world'
 Write-Output "Screenshots and evidence: $output"; return
}
# Launch is explicitly interactive and uses only the synthetic fixture, never museum-archive.
Start-Process -FilePath $prototype -ArgumentList @('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-logFile',"`"$logs\decor-prototype-interactive.log`"") -WindowStyle Normal
