[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$experiment=Split-Path $PSScriptRoot -Parent
$baseline='C:\Dev\vr-museum'
$desktop=[Environment]::GetFolderPath('DesktopDirectory')
$shell=New-Object -ComObject WScript.Shell
foreach($spec in @(
    @{name='VR 박물관 (PC 관람)';root=$experiment;description='최신 타이쿤 실험 PC 관람'},
    @{name='VR 박물관 (타이쿤 실험)';root=$experiment;description='최신 타이쿤 실험 PC 관람 (같은 앱)'},
    @{name='VR 박물관 (기존 버전 보관)';root=$baseline;description='관람객 추가 전 보존한 박물관'}
)){
    $player=Join-Path $spec.root 'Builds\Windows\VRMuseum.exe'
    if(-not(Test-Path -LiteralPath $player)){throw "실행파일 없음: $player"}
    $archive=Join-Path $spec.root 'museum-archive'
    $log=Join-Path $spec.root 'logs\viewer-desktop.log'
    $link=$shell.CreateShortcut((Join-Path $desktop ($spec.name+'.lnk')))
    $link.TargetPath=$player
    $link.WorkingDirectory=$spec.root
    $link.Arguments='-museum-archive "'+$archive+'" -screen-fullscreen 0 -screen-width 1280 -screen-height 800 -logFile "'+$log+'"'
    $link.Description=$spec.description
    $link.Save()
    $check=$shell.CreateShortcut((Join-Path $desktop ($spec.name+'.lnk')))
    if($check.TargetPath -ne $player -or $check.Arguments -notlike ('*'+$archive+'*')){throw '바로가기 확인 실패'}
    Write-Output ($spec.name+' -> '+$player)
}
