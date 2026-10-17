[CmdletBinding()]
param([int]$Rooms=0,[ValidateSet(0,30,60,120)][int]$LoadCount=0)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$folder=Join-Path $root ('logs/auto-crowd-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $folder|Out-Null
if($Rooms -eq 0){
    $source=(Get-Content (Join-Path $root 'logs/deadlock-fixture-path.txt') -Raw).Trim()
    Copy-Item -LiteralPath "$source/manifest.json" -Destination $folder
    Copy-Item -LiteralPath "$source/artifacts" -Destination $folder -Recurse
}else{
    $refs=@();$roomList=@();$order=@()
    for($i=0;$i -lt $Rooms;$i++){
        $id="load-$i";$modelFolder=Join-Path $folder "artifacts/$id/models"
        New-Item -ItemType Directory -Path $modelFolder -Force|Out-Null
        Copy-Item -LiteralPath "$root/museum-archive/artifacts/sample-books/models/pc.glb" -Destination "$modelFolder/pc.glb"
        @{schemaVersion='1.0';id=$id;title="공개 부하 샘플 $i";description='동선 및 부하 검사';model="artifacts/$id/models/pc.glb";scale=1}|ConvertTo-Json|Set-Content "$folder/artifacts/$id/artifact.json" -Encoding utf8
        $ref="artifacts/$id/artifact.json";$refs+=$ref;$order+="room-$i"
        $roomList+=@{id="room-$i";title="부하 검사실 $i";artifacts=@($ref);cells=@(0)}
    }
    @{schemaVersion='1.0';artifacts=$refs;rooms=$roomList;roomOrder=$order}|ConvertTo-Json -Depth 8|Set-Content "$folder/manifest.json" -Encoding utf8
}
@{schemaVersion='1.0';enabled=$true;mode='auto';count=12;speed=1;appearances=@()}|ConvertTo-Json|Set-Content "$folder/visitors.json" -Encoding utf8
$arguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',('"'+$folder+'"'),'-logFile',('"'+$folder+'/player.log"'))
if($LoadCount){$arguments+=@('-museum-motion-audit',('"'+$folder+'"'),'-museum-motion-seconds','30','-museum-crowd-load',[string]$LoadCount)}
else{$arguments+=@('-museum-screenshot',('"'+$folder+'/start.png"'),'-museum-auto-crowd-smoke')}
$player=Start-Process -FilePath "$root/Builds/Windows/VRMuseum.exe" -ArgumentList $arguments -WindowStyle Normal -PassThru
$null=$player.Handle
if(-not $player.WaitForExit(300000)){throw "Auto test timeout: $folder"}
$log=Get-Content "$folder/player.log" -Raw
if($LoadCount){
    if($player.ExitCode -ne 0 -or -not $log.Contains('MOTION_AUDIT_COMPLETE') -or -not $log.Contains("AUTO_LOAD_SEEDED: count=$LoadCount ")){throw "Load failed: $folder"}
    Get-Content "$folder/summary.txt" -TotalCount 1
}else{
    $log -split "`n"|Where-Object {$_ -match 'AUTO_CROWD_CHECK'}|Write-Output
    if($player.ExitCode -ne 0 -or -not $log.Contains('AUTO_CROWD_SMOKE_PASS') -or -not $log.Contains('MUSEUM_SMOKE_PASS')){throw "Automatic crowd failed: $folder"}
}
Write-Output "AUTO_CROWD_PASS: $folder"
