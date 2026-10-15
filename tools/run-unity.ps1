[CmdletBinding()]
param([ValidateSet('Setup','Verify','Build')][string]$Task = 'Verify')
$ErrorActionPreference = 'Stop'
$museumRoot = Split-Path -Parent $PSScriptRoot
$editor = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if (Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $editor }) { throw 'Close the Unity Editor before running batch tasks.' }
$method = @{Setup='ConfigureAndCreate'; Verify='Verify'; Build='BuildDesktop'}[$Task]
$logPath = Join-Path $museumRoot "logs/unity-$Task-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
New-Item -ItemType Directory -Path (Split-Path $logPath -Parent) -Force | Out-Null
$arguments = @('-batchmode','-nographics','-quit','-projectPath',"`"$museumRoot\apps\quest-viewer`"",'-executeMethod',"FamilyMuseum.Editor.MuseumSetup.$method",'-logFile',"`"$logPath`"")
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
# Wait only for the Editor, not a persistent child licensing service.
$process.WaitForExit()
Write-Output "Unity exit code: $($process.ExitCode). Log: $logPath"
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $logPath -Tail 25; exit $process.ExitCode }
