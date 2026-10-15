param([Parameter(Mandatory=$true)][string]$ArchivePath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$cases=@(
    @{name='theme-bright';archive=$ArchivePath;extra=@('-museum-theme-smoke')},
    @{name='theme-night';archive=$ArchivePath;extra=@('-museum-second-room-preview')},
    @{name='theme-details';archive=$ArchivePath;extra=@('-museum-details-smoke')},
    @{name='theme-default';archive="$root\museum-archive";extra=@()},
    @{name='theme-invalid';archive="$ArchivePath\invalid";extra=@('-museum-expect-artifact-failure')}
)
foreach($case in $cases) {
    $log="$root\logs\$($case.name).log"
    $png="$root\logs\$($case.name).png"
    $playerArgs=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',"`"$($case.archive)`"",'-museum-screenshot',"`"$png`"",'-logFile',"`"$log`"")+$case.extra
    $process=Start-Process "$root\Builds\Windows\VRMuseum.exe" -ArgumentList $playerArgs -PassThru
    if(-not $process.WaitForExit(180000)) {throw "Timeout: $($case.name)"}
    $output=Get-Content $log -Raw
    if($process.ExitCode -ne 0 -or -not $output.Contains('MUSEUM_SMOKE_PASS')) {throw "Failed: $($case.name)"}
    if($case.name -eq 'theme-bright' -and -not $output.Contains('THEME_SMOKE_PASS')) {throw 'Theme colours failed'}
    if($case.name -eq 'theme-details' -and -not $output.Contains('DETAILS_SMOKE_PASS')) {throw 'Details selection failed'}
    Write-Output "PASS: $($case.name)"
}
Write-Output 'ROOM_THEME_TESTS_PASS: 5 cases'
