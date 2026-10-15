# Read-only checks. No installation, registry changes, or authentication access.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Continue'
$projectRoot = Split-Path -Parent $PSScriptRoot
Write-Output "VR Museum environment check: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')"
Write-Output "Project: $projectRoot"
foreach ($commandName in @('git', 'code', 'node')) {
    $foundCommand = Get-Command $commandName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($foundCommand) { Write-Output "$commandName : $($foundCommand.Source)" }
    else { Write-Output "$commandName : not on PATH" }
}
if (Get-Command git -ErrorAction SilentlyContinue) {
    git --version
    git lfs version
    git -C $projectRoot status --short --branch
}
$programChecks = @{
    UnityHub = 'C:\Program Files\Unity Hub\Unity Hub.exe'
    UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
    Blender = 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe'
    MetaLink = 'C:\Program Files\Oculus\Support\oculus-client\OculusClient.exe'
}
foreach ($check in $programChecks.GetEnumerator()) {
    if (Test-Path -LiteralPath $check.Value) {
        $installedFile = Get-Item -LiteralPath $check.Value
        Write-Output "$($check.Key): $($installedFile.VersionInfo.ProductVersion) | $($check.Value)"
    } else { Write-Output "$($check.Key): not found at expected path $($check.Value)" }
}
$openXrSettings = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Khronos\OpenXR\1' -ErrorAction SilentlyContinue
Write-Output "OpenXR runtime: $($openXrSettings.ActiveRuntime)"
Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^Unity|^blender$|^msiexec$' } | Select-Object Id, ProcessName
Get-PSDrive -PSProvider FileSystem | Select-Object Name, @{Name='FreeGiB'; Expression={[math]::Round($_.Free / 1GB, 1)}}
Write-Output 'Headset connection, Unity license, and XR behavior require separate verification.'
