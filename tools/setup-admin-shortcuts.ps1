[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$experiment=Split-Path $PSScriptRoot -Parent
$desktop=[Environment]::GetFolderPath('DesktopDirectory')
$shell=New-Object -ComObject WScript.Shell
foreach($spec in @(
    @{name='VR 박물관 관리';root=$experiment;port=4181},
    @{name='VR 박물관 관리 (타이쿤 실험)';root=$experiment;port=4181},
    @{name='VR 박물관 관리 (기존 버전)';root='C:\Dev\vr-museum';port=4177}
)){
    $script=Join-Path $spec.root 'tools\curator-control.ps1'
    if(-not(Test-Path -LiteralPath $script)){throw "관리 도구 없음: $script"}
    $path=Join-Path $desktop ($spec.name+'.lnk')
    $link=$shell.CreateShortcut($path)
    $link.TargetPath="$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"
    $link.Arguments='-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+$script+'" -Port '+$spec.port
    $link.WorkingDirectory=$spec.root
    $link.Description='박물관 관리 서버 켜기·끄기 및 관리페이지 열기'
    $link.WindowStyle=7
    $link.Save()
    $check=$shell.CreateShortcut($path)
    if($check.Arguments -ne $link.Arguments){throw '관리 바로가기 확인 실패'}
    Write-Output ($spec.name+' -> '+$script+' : '+$spec.port)
}
