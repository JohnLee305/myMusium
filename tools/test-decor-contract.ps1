#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$domain = Join-Path $root 'apps/quest-viewer/Assets/Museum/Runtime/Decor/Domain'
# Compile the real production domain sources, without Unity, NuGet or a second algorithm.
Add-Type -Path @((Join-Path $domain 'DecorData.cs'), (Join-Path $domain 'DecorJson.cs'), (Join-Path $domain 'DecorReferenceValidator.cs'))
$fixtures = Join-Path $root 'tests/fixtures/decor'
$schema = Join-Path $root 'contracts/decor/edit-slice.schema.json'
$script:checks = 0
function Check([bool]$ok, [string]$name) {
    if (-not $ok) { throw "FAIL: $name" }
    $script:checks++; Write-Output "PASS: $name"
}
function Read-Base {
    $text = Get-Content -LiteralPath (Join-Path $fixtures 'valid-placed.json') -Raw -Encoding UTF8
    $doc = $null; $report = $null
    if (-not [FamilyMuseum.Decor.DecorJson]::TryRead($text, [ref]$doc, [ref]$report)) { throw 'Base fixture invalid' }
    return $doc
}
function Report($doc) { return [FamilyMuseum.Decor.DecorReferenceValidator]::Validate($doc.catalog, $doc.world) }
function Has($report, [string]$code) { return @($report.issues | Where-Object code -eq $code).Count -gt 0 }
function InvalidJson([string]$text, [string]$name) {
    $doc = $null
    $report = [FamilyMuseum.Decor.DecorReferenceValidator]::ValidateJson($text, [ref]$doc)
    Check ((Has $report 'SCHEMA_INVALID') -and $null -eq $doc -and $report.issues.Count -eq 1) $name
}
$geometry = @('OUTSIDE_ROOM','SUPPORT_OVERHANG','ITEM_COLLISION','RESERVED_ZONE_BLOCKED','SUPPORT_HEIGHT_INVALID')
$cases = Get-Content -LiteralPath (Join-Path $fixtures 'cases.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($case in $cases) {
    $text = Get-Content -LiteralPath (Join-Path $fixtures $case.file) -Raw -Encoding UTF8
    $schemaOk = Test-Json -Json $text -SchemaFile $schema -ErrorAction SilentlyContinue
    $doc = $null; $shape = $null
    $readOk = [FamilyMuseum.Decor.DecorJson]::TryRead($text,[ref]$doc,[ref]$shape)
    Check ($schemaOk -eq $case.schemaValid -and $readOk -eq $schemaOk) ('schema and C# agree: ' + $case.file)
    if (-not $readOk) { Check (Has $shape 'SCHEMA_INVALID') ('schema short circuit: '+$case.file); continue }
    $report = Report $doc
    $expected = @($case.semanticContains | Where-Object { $_ -notin $geometry })
    foreach ($code in $expected) { Check (Has $report $code) ($case.file + ' detects ' + $code) }
    if ($expected.Count -eq 0) { Check $report.ok ('reference-only pass: '+$case.file) }
    # Canonical roundtrip includes every field, coordinate and null/absent location branch.
    $wire = [FamilyMuseum.Decor.DecorJson]::Write($doc); $again=$null; $againReport=$null
    Check ([FamilyMuseum.Decor.DecorJson]::TryRead($wire,[ref]$again,[ref]$againReport)) ('roundtrip read: '+$case.file)
    Check ($wire -ceq [FamilyMuseum.Decor.DecorJson]::Write($again)) ('roundtrip stable: '+$case.file)
    Check (Test-Json -Json $wire -SchemaFile $schema) ('serialized wire matches schema: '+$case.file)
}
$baseText = Get-Content -LiteralPath (Join-Path $fixtures 'valid-placed.json') -Raw -Encoding UTF8
InvalidJson ($baseText.Replace('"revision": 0','"revision": 0, "revision": 1')) 'duplicate JSON key rejected'
InvalidJson ($baseText + ' null') 'trailing JSON rejected'
InvalidJson ($baseText.Replace('"revision": 0','"revision": 0.5')) 'fractional integer rejected'
InvalidJson ($baseText.Replace('"yawDeg": 0','"yawDeg": 1e999')) 'nonfinite number rejected'
InvalidJson ($baseText.Replace('"yawDeg": 0','"yawDeg": 360')) 'out-of-range yaw rejected'
InvalidJson ($baseText.Replace('"yawDeg": 0','"yawDeg": null')) 'required null rejected'
InvalidJson ($baseText.Replace('"kind": "parent"','"kind": "parent", "roomId": "room.a"')) 'mixed location union rejected'
InvalidJson ($baseText.Replace('"yawDeg": 0','"yawDeg": true')) 'boolean is not a number'
InvalidJson ($baseText.Replace('"yawDeg": 0','"yawDeg": 0x1')) 'non-JSON numeric syntax rejected'
InvalidJson ($baseText.Replace('"yawDeg": 0','"yawDeg": 01')) 'leading-zero number rejected'
InvalidJson ($baseText.Replace('"yawDeg": 0','"yawDeg": 1,5')) 'locale decimal comma rejected'
InvalidJson ('['*30 + '0' + ']'*30) 'excessive nesting rejected'
InvalidJson $null 'null input rejected'
InvalidJson '' 'empty input rejected'
$d=Read-Base; $d.world.catalogRevision='other.catalog'; Check (Has (Report $d) 'CATALOG_REVISION_MISMATCH') 'catalog pin mismatch'
$d=Read-Base; $d.world.rooms += $d.world.rooms[0]; Check (Has (Report $d) 'DUPLICATE_ROOM_ID') 'duplicate room ID'
$d=Read-Base; $d.catalog.items += $d.catalog.items[0]; Check (Has (Report $d) 'DUPLICATE_DEFINITION') 'duplicate definition key'
$d=Read-Base; $d.catalog.items[0].supports += $d.catalog.items[0].supports[0]; Check (Has (Report $d) 'DUPLICATE_SUB_ID') 'duplicate support ID'
$d=Read-Base; $d.catalog.items[0].variants += $d.catalog.items[0].variants[0]; Check (Has (Report $d) 'DUPLICATE_SUB_ID') 'duplicate variant ID'
$d=Read-Base; $d.world.instances[0].location.roomId='missing.room'; Check (Has (Report $d) 'ROOM_NOT_FOUND') 'missing room'
$d=Read-Base; $d.world.instances[1].location.surfaceId='missing.surface'; Check (Has (Report $d) 'SUPPORT_NOT_FOUND') 'missing support'
$d=Read-Base; $d.world.instances[1].definition.version=2; Check (Has (Report $d) 'DEFINITION_NOT_FOUND') 'exact definition version required'
$d=Read-Base; $d.world.instances[1].location.parentId='vase.001'; $r=Report $d; Check ((Has $r 'PARENT_CYCLE') -and -not (Has $r 'SUPPORT_NOT_FOUND')) 'self-cycle detected before support checks'
$d=Read-Base; $d.world.instances[0].location=$d.world.instances[1].location; $d.world.instances[0].location.parentId='vase.001'; $d.world.instances[1].location.parentId='vase.001'; Check (Has (Report $d) 'PARENT_CYCLE') 'cycle terminates even with incoming edge'
$d=Read-Base; $child = [FamilyMuseum.Decor.ItemInstance]::new(); $child.id='vase.002'; $child.definition=$d.world.instances[1].definition; $child.variantId='ivory'; $child.location=[FamilyMuseum.Decor.ItemLocation]::new(); $child.location.kind='parent'; $child.location.parentId='vase.001'; $child.location.surfaceId='top'; $child.location.localPositionM=@(0,1,0); $child.location.yawDeg=0; $d.world.instances += $child
Check (Has (Report $d) 'PARENT_DEPTH_INVALID') 'second attachment level rejected'
$d=Read-Base; $d.world.instances[1].location.localPositionM=@(-0.123,1,0.007)
$oldCulture=[Globalization.CultureInfo]::CurrentCulture
try {
    [Globalization.CultureInfo]::CurrentCulture=[Globalization.CultureInfo]::GetCultureInfo('de-DE')
    $wire=[FamilyMuseum.Decor.DecorJson]::Write($d); $copy=$null; $rr=$null
    $ok=[FamilyMuseum.Decor.DecorJson]::TryRead($wire,[ref]$copy,[ref]$rr)
    Check ($ok -and $copy.world.instances[1].location.localPositionM[0] -eq -0.123 -and $copy.world.instances[1].id -ceq 'vase.001') 'precision IDs and parent coordinates preserved across locale'
} finally { [Globalization.CultureInfo]::CurrentCulture=$oldCulture }
$d=Read-Base; $d.world.instances[0].definition.id='missing.definition'; $d.world.instances[1].variantId='missing.variant'
$first=(Report $d).issues | ConvertTo-Json -Depth 6 -Compress
[Array]::Reverse($d.world.instances); [Array]::Reverse($d.catalog.items)
$second=(Report $d).issues | ConvertTo-Json -Depth 6 -Compress
Check ($first -ceq $second) 'issue order independent of input order'
$d=Read-Base; $d.world.instances[1].location.yawDeg=[double]::NaN
Check (Has (Report $d) 'SCHEMA_INVALID') 'typed API also rejects nonfinite input'
$d=Read-Base; $d.world.instances[1].location=$null
Check (Has (Report $d) 'SCHEMA_INVALID') 'typed API rejects missing location without throwing'
$d=Read-Base; $d.catalog.items[0].name=[string]::Concat([string[]](@([char]::ConvertFromUtf32(0x1F3FA))*128))
Check ((Report $d).ok) '128 supplementary Unicode characters match schema string length'
$wire=[FamilyMuseum.Decor.DecorJson]::Write($d)
Check (Test-Json -Json $wire -SchemaFile $schema) 'Unicode boundary wire matches JSON Schema'
$d.catalog.items[0].name += [char]::ConvertFromUtf32(0x1F3FA)
Check (Has (Report $d) 'SCHEMA_INVALID') '129 Unicode characters rejected'
$d=Read-Base; $d.catalog.items[0].name="한글 `"제목`"`n설명`t" + [char]0
$wire=[FamilyMuseum.Decor.DecorJson]::Write($d); $copy=$null; $rr=$null
Check ([FamilyMuseum.Decor.DecorJson]::TryRead($wire,[ref]$copy,[ref]$rr) -and $copy.catalog.items[0].name -ceq $d.catalog.items[0].name) 'quoted Korean text and escaped controls roundtrip'
$d=Read-Base; $d.world.instances=@()
for ($n=0;$n -lt 512;$n++) {
    $item=[FamilyMuseum.Decor.ItemInstance]::new(); $item.id="chain.$n"; $item.definition=[FamilyMuseum.Decor.DefinitionRef]::new(); $item.definition.id='decor.vase.small'; $item.definition.version=1; $item.variantId='ivory'; $item.location=[FamilyMuseum.Decor.ItemLocation]::new(); $item.location.kind='parent'; $item.location.parentId="chain.$(($n+1)%512)"; $item.location.surfaceId='top'; $item.location.localPositionM=@(0,1,0); $item.location.yawDeg=0; $d.world.instances += $item
}
$r=Report $d
Check (@($r.issues | Where-Object code -eq PARENT_CYCLE).Count -eq 512) 'maximum 512-node cycle terminates and identifies every member'
$d=Read-Base; $alternate=[FamilyMuseum.Decor.ItemDefinition]::new(); $alternate.id=$d.catalog.items[0].id; $alternate.version=2; $alternate.name='second'; $alternate.kind='floorFurniture'; $alternate.dimensionsM=@(.8,1,.8); $alternate.collision=$d.catalog.items[0].collision; $alternate.supports=$d.catalog.items[0].supports; $alternate.snapM=.25; $alternate.variants=$d.catalog.items[0].variants; $d.catalog.items += $alternate
Check ((Report $d).ok) 'same definition ID with different version is not a duplicate'
$d=Read-Base; $world=$null; $catalog=$null; $rr=$null
$worldWire=[FamilyMuseum.Decor.DecorJson]::WriteWorld($d.world)
$catalogWire=[FamilyMuseum.Decor.DecorJson]::WriteCatalog($d.catalog)
Check ([FamilyMuseum.Decor.DecorJson]::TryReadWorld($worldWire,[ref]$world,[ref]$rr)) 'world document reads without fixture envelope'
Check ([FamilyMuseum.Decor.DecorJson]::TryReadCatalog($catalogWire,[ref]$catalog,[ref]$rr)) 'catalog document reads without fixture envelope'
Check ([FamilyMuseum.Decor.DecorReferenceValidator]::Validate($catalog,$world).ok -and -not $worldWire.Contains('"catalog":')) 'separate documents retain valid references and one world save boundary'
# Differential shape tests: each object must reject every missing key and extra key,
# including location unions. This guards the hand-written fixed-contract codec against schema drift.
$script:objectPaths=[Collections.Generic.List[object]]::new()
function Collect-Objects($value, [object[]]$path) {
    if ($value -is [Collections.IDictionary]) {
        $script:objectPaths.Add([object]@{path=$path;keys=@($value.Keys)})
        foreach ($key in @($value.Keys)) { Collect-Objects $value[$key] ($path+@($key)) }
    } elseif ($value -is [array]) {
        for ($i=0;$i -lt $value.Count;$i++) { Collect-Objects $value[$i] ($path+@($i)) }
    }
}
$baseTree=ConvertFrom-Json -InputObject $baseText -AsHashtable
Collect-Objects $baseTree @()
foreach ($entry in $script:objectPaths) {
    foreach ($key in (@($entry.keys)+@('__extra'))) {
        $tree=ConvertFrom-Json -InputObject $baseText -AsHashtable
        $node=$tree
        foreach ($part in $entry.path) { $node=$node[$part] }
        if ($key -eq '__extra') { $node['__extra']=1 } else { $node.Remove($key) | Out-Null }
        $wire=ConvertTo-Json -InputObject $tree -Depth 30 -Compress
        $valid=Test-Json -Json $wire -SchemaFile $schema -ErrorAction SilentlyContinue
        $doc=$null; $rr=$null; $read=[FamilyMuseum.Decor.DecorJson]::TryRead($wire,[ref]$doc,[ref]$rr)
        Check (-not $valid -and -not $read -and (Has $rr 'SCHEMA_INVALID')) ("required/extra field gate: $($entry.path -join '/')/$key")
    }
}
Write-Output "DECOR_CONTRACT_PASS checks=$script:checks (reference stages 1-5; geometry/UI/Unity not tested)"
