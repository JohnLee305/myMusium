[CmdletBinding()]
param([string]$ArchivePath,[switch]$NoRestore)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$state=Get-Content (Join-Path $root 'docs/PROJECT_STATE.json') -Raw|ConvertFrom-Json
if(-not $ArchivePath){$ArchivePath=$state.windowsDeployment.archiveRoot}
$archive=(Resolve-Path -LiteralPath $ArchivePath).Path
$before=@(Get-ChildItem -LiteralPath $archive -Recurse -File|Sort-Object FullName|ForEach-Object {"$($_.FullName)|$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"})
$liveExe=Join-Path $state.windowsDeployment.sourceRoot $state.windowsDeployment.viewerRelativePath
$restore=$false
foreach($viewer in @(Get-Process VRMuseum -ErrorAction SilentlyContinue)) {
    if($viewer.Path -eq $liveExe){$restore=$true;$null=$viewer.CloseMainWindow();if(-not $viewer.WaitForExit(10000)){throw 'Close the active viewer before auditing.'}}
}
$folder=Join-Path $root ('logs/motion-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $folder -Force|Out-Null
Set-Content (Join-Path $root 'logs/motion-audit-folder.txt') $folder
$argsList=@('-museum-archive',('"'+$archive+'"'),'-museum-motion-audit',('"'+$folder+'"'),'-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-logFile',('"'+$folder+'/player.log"'))
$player=$null
try {
    $player=Start-Process -FilePath (Join-Path $root 'Builds/Windows/VRMuseum.exe') -ArgumentList $argsList -WindowStyle Normal -PassThru
    $null=$player.Handle
    if(-not $player.WaitForExit(180000)){throw 'Motion audit timed out.'}
    if($player.ExitCode -ne 0){throw "Motion audit failed: $folder"}
    Get-Content (Join-Path $folder 'summary.txt')
} finally {
    if($player -and -not $player.HasExited){$null=$player.CloseMainWindow();$null=$player.WaitForExit(10000)}
    if($restore -and -not $NoRestore){
        $shellObject=New-Object -ComObject WScript.Shell
        $shortcut=$shellObject.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'VR 박물관 (PC 관람).lnk'))
        # Do not silently reopen a different application if the user's shortcut changed.
        if($shortcut.TargetPath -eq $liveExe){Start-Process -FilePath $liveExe -ArgumentList $shortcut.Arguments -WindowStyle Normal}
    }
}
$after=@(Get-ChildItem -LiteralPath $archive -Recurse -File|Sort-Object FullName|ForEach-Object {"$($_.FullName)|$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"})
if(Compare-Object $before $after){throw 'Archive changed during audit; preserve and inspect.'}
Write-Output "MOTION_AUDIT_PASS: $folder | private files unchanged=$($after.Count)"
