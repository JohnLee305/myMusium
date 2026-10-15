$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $root "logs/visitor-diversity-tests-$(Get-Date -Format yyyyMMdd-HHmmss)"
$folder=Join-Path $fixture 'artifacts/variant-test'
New-Item -ItemType Directory -Force "$folder/models"|Out-Null
Copy-Item "$root/museum-archive/artifacts/sample-books/models/pc.glb" "$folder/models/pc.glb"
@{schemaVersion='1.0';id='variant-test';title='다양한 관람객 시험';description='100개 외형·리그·무작위 선택 검사';model='artifacts/variant-test/models/pc.glb';scale=1}|ConvertTo-Json|Set-Content "$folder/artifact.json" -Encoding utf8
$entries=@('artifacts/variant-test/artifact.json')
@{schemaVersion='1.0';artifacts=$entries;rooms=@(@{id='first';title='외형 시험실';artifacts=$entries;cells=@(2)})}|ConvertTo-Json -Depth 8|Set-Content "$fixture/manifest.json" -Encoding utf8
$log="$fixture/diversity.log"
$player=Start-Process "$root/Builds/Windows/VRMuseum.exe" -ArgumentList @('-screen-fullscreen','0','-screen-width','1600','-screen-height','1800','-museum-archive',"`"$fixture`"",'-museum-visitor-diversity-smoke','-museum-screenshot',"`"$fixture/diversity.png`"",'-logFile',"`"$log`"") -PassThru
$null=$player.Handle
if(-not $player.WaitForExit(180000)){try{$player.Kill()}catch{};throw 'Diversity timeout'}
$text=Get-Content $log -Raw
if($player.ExitCode -ne 0 -or -not $text.Contains('VISITOR_DIVERSITY_SMOKE_PASS') -or -not $text.Contains('MUSEUM_SMOKE_PASS') -or ([regex]::Matches($text,'VISITOR_VARIANT_CHECK: [^\r\n]+passed=True')).Count -ne 100 -or $text -match '(NullReferenceException:|MissingReferenceException:)'){throw "Diversity failed: $log"}
Write-Output "VISITOR_DIVERSITY_TEST_PASS: 100 animated variants / 12000 random draws / six distinct active | $fixture"
