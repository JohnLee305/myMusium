[CmdletBinding()]
param([int]$Port=4181,[string]$ArchivePath)
$ErrorActionPreference='Stop'
$experiment=Split-Path $PSScriptRoot -Parent
if(-not $ArchivePath){$ArchivePath=Join-Path $experiment 'museum-archive'}
$ArchivePath=(Resolve-Path -LiteralPath $ArchivePath).Path
if($Port -lt 1024 -or $Port -gt 65535){throw 'Invalid port'}
$desktop=[Environment]::GetFolderPath('DesktopDirectory')
$shell=New-Object -ComObject WScript.Shell
foreach($spec in @(
    @{name='VR 박물관 관리';root=$experiment;port=$Port},
    @{name='VR 박물관 관리 (타이쿤 실험)';root=$experiment;port=$Port},
    @{name='VR 박물관 관리 (기존 버전)';root='C:\Dev\vr-museum';port=4177}
)){
    $script=Join-Path $spec.root 'tools\curator-control.ps1'
    if(-not(Test-Path -LiteralPath $script)){throw "관리 도구 없음: $script"}
    $path=Join-Path $desktop ($spec.name+'.lnk')
    $link=$shell.CreateShortcut($path)
    $link.TargetPath="$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"
    $link.Arguments='-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+$script+'" -Port '+$spec.port
    if($spec.root -eq $experiment){$link.Arguments+=' -ArchivePath "'+$ArchivePath+'"'}
    $link.WorkingDirectory=$spec.root
    $link.Description='박물관 관리 서버 켜기·끄기 및 관리페이지 열기'
    $link.WindowStyle=7
    $link.Save()
    $check=$shell.CreateShortcut($path)
    if($check.Arguments -ne $link.Arguments){throw '관리 바로가기 확인 실패'}
    Write-Output ($spec.name+' -> '+$script+' : '+$spec.port)
}
