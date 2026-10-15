$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixtures = Join-Path $root "logs/details-tests-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
New-Item -ItemType Directory -Path $fixtures -Force | Out-Null
foreach ($case in @('normal','long','missing','invalid')) {
    $archive = Join-Path $fixtures $case
    $folder = Join-Path $archive 'artifacts/detail-test'
    New-Item -ItemType Directory -Path "$folder/models" -Force | Out-Null
    Copy-Item -LiteralPath "$root/museum-archive/artifacts/sample-vase/models/pc.glb" -Destination "$folder/models/pc.glb"
    $title = '기억을 담은 항아리'
    $description = "가족의 일상과 추억을 담은 전시품입니다.`n`n전시품이나 전시대를 선택하면 이야기를 읽을 수 있습니다."
    if ($case -eq 'long') {
        $title = '오래된 물건에 남아 있는 가족의 기억과 세대를 이어 전해지는 이야기 — 긴 제목과 줄바꿈을 확인하는 전시품'
        $description = ((1..16 | ForEach-Object { "이야기 $_ — 기억을 담은 전시품입니다." }) -join "`n") + "`n마지막 문장: 끝까지 읽었습니다."
    }
    $model = if ($case -eq 'missing') {'artifacts/detail-test/models/missing.glb'} else {'artifacts/detail-test/models/pc.glb'}
    @{schemaVersion='1.0';id='detail-test';title=$title;description=$description;model=$model;scale=1} | ConvertTo-Json | Set-Content "$folder/artifact.json" -Encoding utf8
    if ($case -eq 'invalid') { '{invalid json' | Set-Content "$folder/artifact.json" -Encoding utf8 }
    $paths = @('artifacts/detail-test/artifact.json')
    @{schemaVersion='1.0';artifacts=$paths;rooms=@(@{id='details';title='설명 패널 시험';artifacts=$paths},@{id='empty';title='빈 전시실';artifacts=@()})} | ConvertTo-Json -Depth 8 | Set-Content "$archive/manifest.json" -Encoding utf8
    $log = Join-Path $fixtures "$case.log"
    $png = Join-Path $fixtures "$case.png"
    $playerArgs = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-museum-archive',"`"$archive`"",'-museum-screenshot',"`"$png`"",'-museum-details-smoke','-logFile',"`"$log`"")
    if ($case -in @('missing','invalid')) { $playerArgs += '-museum-expect-artifact-failure' }
    $process = Start-Process -FilePath "$root/Builds/Windows/VRMuseum.exe" -ArgumentList $playerArgs -PassThru
    if (-not $process.WaitForExit(180000)) { throw "Details test timed out: $case" }
    $output = Get-Content $log -Raw
    if ($process.ExitCode -ne 0 -or -not $output.Contains('MUSEUM_SMOKE_PASS') -or -not $output.Contains('DETAILS_SMOKE_PASS')) { throw "Details test failed: $case; $log" }
    Write-Output "PASS: $case"
}
Write-Output "DETAILS_TESTS_PASS: 4 cases | $fixtures"
