$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fixtures=Join-Path $root "logs/vr-interaction-tests-$(Get-Date -Format yyyyMMdd-HHmmss)"
foreach($case in @('normal','long','small','missing')) {
    $archive=Join-Path $fixtures $case
    $folder=Join-Path $archive 'artifacts/inspection-test'
    New-Item -ItemType Directory -Force "$folder/models" | Out-Null
    Copy-Item "$root/museum-archive/artifacts/sample-books/models/pc.glb" "$folder/models/pc.glb"
    $description='책의 앞뒤와 모서리를 회전하며 살펴보세요. 닫으면 원래 전시실로 돌아갑니다.'
    if($case -eq 'long'){$description=((1..14|ForEach-Object{"이야기 $_ — 가족이 함께 읽었던 책입니다."}) -join "`n")}
    $scale=if($case -eq 'small'){.01}else{1}
    $model=if($case -eq 'missing'){'artifacts/inspection-test/models/missing.glb'}else{'artifacts/inspection-test/models/pc.glb'}
    @{schemaVersion='1.0';id='inspection-test';title='겹겹의 이야기 · 회전·확대';description=$description;model=$model;scale=$scale}|ConvertTo-Json|Set-Content "$folder/artifact.json" -Encoding utf8
    $entries=@('artifacts/inspection-test/artifact.json')
    @{schemaVersion='1.0';artifacts=$entries;rooms=@(@{id='first';title='회전·확대 시험실';artifacts=$entries;cells=@(2)},@{id='empty';title='빈 방';artifacts=@();cells=@()})}|ConvertTo-Json -Depth 10|Set-Content "$archive/manifest.json" -Encoding utf8
    $log="$fixtures/$case.log";$png="$fixtures/$case.png"
    $playerArgs=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',"`"$archive`"",'-museum-screenshot',"`"$png`"",'-museum-vr-interaction-smoke','-logFile',"`"$log`"")
    if($case -eq 'missing'){$playerArgs+='-museum-expect-artifact-failure'}
    if($case -eq 'normal'){$playerArgs+='-museum-vr-interaction-preview'}
    # A visible viewer window is needed to inspect actual rendered screenshots.
    $process=Start-Process "$root/Builds/Windows/VRMuseum.exe" -ArgumentList $playerArgs -PassThru
    $null=$process.Handle
    if(-not $process.WaitForExit(120000)){throw "Inspection timeout: $case"}
    $output=Get-Content $log -Raw
    if($process.ExitCode -ne 0 -or -not $output.Contains('VR_INTERACTION_SMOKE_PASS') -or -not $output.Contains('MUSEUM_SMOKE_PASS')){throw "Inspection failed: $case; $log"}
    Write-Output "PASS: $case"
}
Write-Output "VR_INTERACTION_TESTS_PASS: 4 cases | $fixtures"

