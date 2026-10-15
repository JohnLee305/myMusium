$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $root "logs/visitor-live-$(Get-Date -Format yyyyMMdd-HHmmss)"
$source=Get-ChildItem "$root/logs/visitor-settings-*" -Directory|Sort-Object LastWriteTime -Descending|Select-Object -First 1
if(-not $source){throw 'Run test-visitor-settings.ps1 first'}
Copy-Item -LiteralPath $source.FullName -Destination $fixture -Recurse
$port=4185; $url="http://127.0.0.1:$port"; $player=$null; $serverStarted=$false
function AwaitLog($path,$pattern){
    $deadline=[datetime]::Now.AddSeconds(40)
    do{if((Test-Path $path) -and (Get-Content $path -Raw) -match $pattern){return};Start-Sleep -Milliseconds 250}while([datetime]::Now -lt $deadline)
    throw "Log timeout: $pattern; $path"
}
function OpenPlayer($name){
    $log=Join-Path $fixture ($name+'.log')
    $script:player=Start-Process "$root/Builds/Windows/VRMuseum.exe" -ArgumentList @('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',"`"$fixture`"",'-logFile',"`"$log`"") -PassThru
    $null=$script:player.Handle
    AwaitLog $log 'GALLERY_READY: loaded=1 failed=0'
    return $log
}
function ClosePlayer($log){
    if(-not $script:player.CloseMainWindow()){throw 'Player window could not be closed'}
    if(-not $script:player.WaitForExit(15000)){throw 'Player shutdown timeout'}
    if($script:player.ExitCode -ne 0 -or (Get-Content $log -Raw) -match '(NullReferenceException:|MissingReferenceException:)'){throw 'Player shutdown error'}
    $script:player=$null
}
try{
    & "$root/tools/start-admin.ps1" -Port $port -ArchivePath $fixture -NoBrowser
    $serverStarted=$true
    $token=(Invoke-RestMethod "$url/api/session").token
    $log=OpenPlayer 'poll'
    $state=Invoke-RestMethod "$url/api/visitors"
    $state.settings.count=3;$state.settings.speed=0.8
    $body=@{revision=$state.revision;settings=$state.settings}|ConvertTo-Json -Depth 8
    Invoke-RestMethod "$url/api/visitors" -Method Post -ContentType 'application/json' -Headers @{'X-Museum-Token'=$token} -Body $body | Out-Null
    AwaitLog $log 'VISITOR_SETTINGS_APPLIED: enabled=True count=3 speed=0.8'
    ClosePlayer $log
    $log=OpenPlayer 'restart'
    AwaitLog $log 'VISITOR_SETTINGS_APPLIED: enabled=True count=3 speed=0.8'
    ClosePlayer $log
    Write-Output "VISITOR_LIVE_TEST_PASS: API save -> player polling -> player restart | $fixture"
}finally{
    if($player -and -not $player.HasExited){try{$player.Kill()}catch{}}
    if($serverStarted){& "$root/tools/stop-admin.ps1" -Port $port}
}
