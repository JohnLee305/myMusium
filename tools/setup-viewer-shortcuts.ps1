[CmdletBinding()]
param([string]$ArchivePath)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path $PSScriptRoot -Parent
$state=Get-Content -LiteralPath (Join-Path $sourceRoot 'docs/PROJECT_STATE.json') -Raw | ConvertFrom-Json
if([IO.Path]::GetFullPath($sourceRoot) -ne [IO.Path]::GetFullPath($state.windowsDeployment.sourceRoot)){throw 'Use the canonical sourceRoot from PROJECT_STATE.json.'}
if(-not $ArchivePath){$ArchivePath=$state.windowsDeployment.archiveRoot}
$ArchivePath=(Resolve-Path -LiteralPath $ArchivePath).Path
$player=Join-Path $sourceRoot 'Builds/Windows/VRMuseum.exe'
if(-not(Test-Path -LiteralPath $player)){throw 'Build the latest viewer before creating shortcuts.'}
$desktop=[Environment]::GetFolderPath('DesktopDirectory')
$shell=New-Object -ComObject WScript.Shell
foreach($spec in @(@{name='VR 박물관 (PC 관람)';vr=$false},@{name='VR 박물관 (Quest Link)';vr=$true})){
 $link=$shell.CreateShortcut((Join-Path $desktop ($spec.name+'.lnk')))
 $link.TargetPath=$player;$link.WorkingDirectory=$sourceRoot
 $link.Arguments='-museum-archive "'+$ArchivePath+'" -screen-fullscreen 0 -screen-width 1280 -screen-height 800 -logFile "'+(Join-Path $sourceRoot 'logs/viewer-desktop.log')+'"'
 if($spec.vr){$link.Arguments='-museum-vr '+$link.Arguments}
 $link.Description='박물관 최신 통합판';$link.Save()
 $check=$shell.CreateShortcut((Join-Path $desktop ($spec.name+'.lnk')))
 if($check.TargetPath -ne $player -or -not $check.Arguments.Contains($ArchivePath)){throw 'Viewer shortcut verification failed'}
 Write-Output ($spec.name+' -> latest viewer')
}
$admin=$shell.CreateShortcut((Join-Path $desktop 'VR 박물관 관리.lnk'))
$admin.TargetPath=Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
$admin.WorkingDirectory=$sourceRoot
$admin.Arguments='-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+(Join-Path $sourceRoot 'tools/curator-control.ps1')+'" -Port '+$state.windowsDeployment.adminPort+' -ArchivePath "'+$ArchivePath+'"'
$admin.Description='박물관 최신 관리';$admin.Save()
$check=$shell.CreateShortcut((Join-Path $desktop 'VR 박물관 관리.lnk'))
if(-not $check.Arguments.Contains($sourceRoot) -or -not $check.Arguments.Contains($ArchivePath)){throw 'Admin shortcut verification failed'}
Write-Output 'VR 박물관 관리 -> latest admin'
