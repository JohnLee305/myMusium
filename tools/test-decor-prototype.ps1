#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$domain=Join-Path $root 'apps/quest-viewer/Assets/Museum/Runtime/Decor/Domain'
$storage=Join-Path $root 'apps/quest-viewer/Assets/Museum/Runtime/Decor/Storage'
$files=@('DecorData.cs','DecorJson.cs','DecorReferenceValidator.cs','DecorCommands.cs','DecorEditSession.cs','DecorInventoryQuery.cs','DecorGeometry.cs','DecorPlacementValidator.cs'|ForEach-Object{Join-Path $domain $_})
$files+=@('DecorWorldStore.cs','DecorFileReplace.cs','DecorPrototypePersistence.cs'|ForEach-Object{Join-Path $storage $_})
$files+=Join-Path $root 'tests/decor/DecorPrototypePersistenceTests.cs'
Add-Type -Path $files
$output=Join-Path $root ('logs/decor-prototype-tests-'+[guid]::NewGuid().ToString('N'))
[DecorPrototypePersistenceTests]::Run((Join-Path $root 'tests/fixtures/decor/valid-placed.json'),$output) | Write-Output
