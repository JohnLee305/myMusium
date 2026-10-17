#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$domain=Join-Path $root 'apps/quest-viewer/Assets/Museum/Runtime/Decor/Domain'
Add-Type -Path @('DecorData.cs','DecorJson.cs','DecorReferenceValidator.cs','DecorCommands.cs','DecorEditSession.cs','DecorInventoryQuery.cs','DecorGeometry.cs','DecorPlacementValidator.cs' | ForEach-Object { Join-Path $domain $_ })
$script:checks=0
function Check([bool]$condition,[string]$name) { if (-not $condition) { throw "FAIL: $name" }; $script:checks++; Write-Output "PASS: $name" }
function Base {
 $d=$null; $r=$null
 if (-not [FamilyMuseum.Decor.DecorJson]::TryRead((Get-Content -LiteralPath (Join-Path $root 'tests/fixtures/decor/valid-placed.json') -Raw -Encoding UTF8),[ref]$d,[ref]$r)) { throw 'Invalid base fixture' }
 return $d
}
function Report($d) { return [FamilyMuseum.Decor.DecorPlacementValidator]::Validate($d.catalog,$d.world) }
function Read-Clone([string]$wire) { $d=$null; $r=$null; if (-not [FamilyMuseum.Decor.DecorJson]::TryRead($wire,[ref]$d,[ref]$r)) { throw 'Clone invalid' }; return $d }
function Has($report,[string]$code,[string]$id='') { return @($report.issues | Where-Object { $_.code -eq $code -and (-not $id -or $_.primaryId -eq $id) }).Count -gt 0 }
function Room([double]$x=0,[double]$z=0,[double]$yaw=0) { return [FamilyMuseum.Decor.ItemLocation]@{kind='room';roomId='room.a';positionM=[double[]]@($x,0,$z);yawDeg=$yaw} }
function Item([string]$id,[double]$x=0,[double]$z=0,[double]$yaw=0) { return [FamilyMuseum.Decor.ItemInstance]@{id=$id;definition=[FamilyMuseum.Decor.DefinitionRef]@{id='core.plinth.small';version=1};variantId='green';location=(Room $x $z $yaw)} }
function Child([string]$id,[string]$parent='plinth.001',[double]$x=.15) { return [FamilyMuseum.Decor.ItemInstance]@{id=$id;definition=[FamilyMuseum.Decor.DefinitionRef]@{id='decor.vase.small';version=1};variantId='ivory';location=[FamilyMuseum.Decor.ItemLocation]@{kind='parent';parentId=$parent;surfaceId='top';localPositionM=[double[]]@($x,1,0);yawDeg=0}} }
function Near([double]$a,[double]$b) { return [Math]::Abs($a-$b) -lt 1e-9 }
function Throws([scriptblock]$action,[string]$name) { $bad=$false; try { & $action | Out-Null } catch { $bad=$true }; Check $bad $name }
$fixtures=Join-Path $root 'tests/fixtures/decor'
foreach ($case in (Get-Content -LiteralPath (Join-Path $fixtures 'cases.json') -Raw -Encoding UTF8 | ConvertFrom-Json)) {
 $wire=Get-Content -LiteralPath (Join-Path $fixtures $case.file) -Raw -Encoding UTF8
 $d=$null; $report=[FamilyMuseum.Decor.DecorPlacementValidator]::ValidateJson($wire,[ref]$d)
 if (-not $case.schemaValid) { Check (Has $report 'SCHEMA_INVALID') ('schema precedes geometry: '+$case.file); continue }
 foreach ($code in $case.semanticContains) { Check (Has $report $code) ($case.file+' detects '+$code) }
 if ($case.semanticContains.Count -eq 0) { Check $report.ok ('full placement pass: '+$case.file) }
 Check ([FamilyMuseum.Decor.DecorJson]::Write($d) -ceq [FamilyMuseum.Decor.DecorJson]::Write((Read-Clone $wire))) ('validation does not mutate: '+$case.file)
}
$p=[double[]]@(.125,1.1234,-.125); $o=[double[]]@(0,0,0)
$snap=[FamilyMuseum.Decor.DecorGeometry]::SnapPosition($p,$o,.25)
Check ((Near $snap[0] .25) -and (Near $snap[2] -.25) -and (Near $snap[1] 1.123)) 'positive and negative half steps away from zero, height only quantized'
Check ($p[0] -eq .125 -and $p[1] -eq 1.1234) 'snap never mutates caller position'
$snap=[FamilyMuseum.Decor.DecorGeometry]::SnapPosition(@(.075,1,-.075),@(.0,0,.0),.05)
Check ((Near $snap[0] .1) -and (Near $snap[2] -.1)) '5cm half steps avoid binary rounding bias'
$snap=[FamilyMuseum.Decor.DecorGeometry]::SnapPosition(@(.04,0,.04),@(.015,0,.015),.05)
Check ((Near $snap[0] .065) -and (Near $snap[2] .065)) 'snap uses surface origin rather than global zero'
Check ((Near ([FamilyMuseum.Decor.DecorGeometry]::QuantizeM(.0005)) .001) -and (Near ([FamilyMuseum.Decor.DecorGeometry]::QuantizeM(-.0005)) -.001)) '1mm quantization is symmetric at half millimeter'
Check ([FamilyMuseum.Decor.DecorGeometry]::NormalizeYaw(360) -eq 0 -and [FamilyMuseum.Decor.DecorGeometry]::NormalizeYaw(-90) -eq 270) 'editor angle normalization handles wrap and negative input'
Throws { [FamilyMuseum.Decor.DecorGeometry]::SnapPosition(@([double]::NaN,0,0),@(0,0,0),.25) } 'nonfinite snap input rejected'
Throws { [FamilyMuseum.Decor.DecorGeometry]::SnapPosition(@(0,0,0),@(0,0,0),.1) } 'unsupported snap step rejected'
Throws { [FamilyMuseum.Decor.DecorGeometry]::TransformPoint(@(0,0,0),45,@(1,0,0)) } 'unsupported geometry yaw never silently snapped'
$expected=@(@(.15,1,0),@(0,1,-.15),@(-.15,1,0),@(0,1,.15))
for ($n=0;$n -lt 4;$n++) {
 $v=[FamilyMuseum.Decor.DecorGeometry]::TransformPoint(@(2,0,3),($n*90),@(.15,1,0))
 Check ((Near $v[0] (2+$expected[$n][0])) -and (Near $v[1] 1) -and (Near $v[2] (3+$expected[$n][2]))) ("Unity Y yaw transform: $($n*90)")
}
# Independent eight-corner trigonometric oracle, including non-centered collision proxies.
for ($sample=1;$sample -le 10;$sample++) {
 foreach ($yaw in @(0,90,180,270)) {
  $center=[double[]]@((.03*$sample),.4,(-.02*$sample)); $size=[double[]]@((.2+.03*$sample),.8,(.5+.02*$sample)); $position=[double[]]@((-2+.1*$sample),0,3)
  $box=[FamilyMuseum.Decor.DecorBox]@{centerM=$center;sizeM=$size}
  $bounds=[FamilyMuseum.Decor.DecorGeometry]::Bounds($box,$position,$yaw)
  $corners=@(); $radians=$yaw*[Math]::PI/180
  foreach ($sx in @(-1,1)) { foreach ($sy in @(-1,1)) { foreach ($sz in @(-1,1)) {
   $x=$center[0]+$sx*$size[0]/2; $y=$center[1]+$sy*$size[1]/2; $z=$center[2]+$sz*$size[2]/2
   $corners += ,@(($position[0]+$x*[Math]::Cos($radians)+$z*[Math]::Sin($radians)),($position[1]+$y),($position[2]-$x*[Math]::Sin($radians)+$z*[Math]::Cos($radians)))
  } } }
  $xs=@($corners | ForEach-Object { $_[0] }); $ys=@($corners | ForEach-Object { $_[1] }); $zs=@($corners | ForEach-Object { $_[2] })
  Check ((Near $bounds.minX (($xs|Measure-Object -Minimum).Minimum)) -and (Near $bounds.maxX (($xs|Measure-Object -Maximum).Maximum)) -and (Near $bounds.minY (($ys|Measure-Object -Minimum).Minimum)) -and (Near $bounds.maxY (($ys|Measure-Object -Maximum).Maximum)) -and (Near $bounds.minZ (($zs|Measure-Object -Minimum).Minimum)) -and (Near $bounds.maxZ (($zs|Measure-Object -Maximum).Maximum))) "eight-corner rotated box oracle sample=$sample yaw=$yaw"
 }
}
$d=Base; $d.world.instances[0].location.yawDeg=45; $before=[FamilyMuseum.Decor.DecorJson]::Write($d)
Check (Has (Report $d) 'ROTATION_UNSUPPORTED' 'plinth.001') 'unsupported root rotation is explicit'
Check ([FamilyMuseum.Decor.DecorJson]::Write($d) -ceq $before) 'unsupported rotation is not changed during validation'
$d=Base; $d.world.instances[1].location.yawDeg=45
Check (Has (Report $d) 'ROTATION_UNSUPPORTED' 'vase.001') 'unsupported child local rotation rejected'
$d=Base; $d.world.instances[0].location.yawDeg=360
Check (Has (Report $d) 'SCHEMA_INVALID') 'raw 360-degree stored input must be normalized before submission'
$d=Base; $d.world.instances[0].location=Room 2 3 90; $d.world.instances[1].location.localPositionM[0]=.36
$r=Report $d; $issue=@($r.issues | Where-Object code -eq SUPPORT_OVERHANG)[0]
Check ($null -ne $issue -and (Near $issue.position[0] 2) -and (Near $issue.position[1] 1) -and (Near $issue.position[2] 2.64)) 'child geometry diagnostic uses rotated and translated parent frame'
$d=Base; $d.world.instances[1].location.localPositionM[0]=.29
Check ((Report $d).ok) 'child bottom footprint may touch exact support edge'
$d.world.instances[1].location.localPositionM[0]=.291
Check ((Report $d).ok) 'support overhang at 1mm tolerance allowed'
$d.world.instances[1].location.localPositionM[0]=.29101
Check (Has (Report $d) 'SUPPORT_OVERHANG') 'support overhang beyond tolerance rejected'
$d=Base; $d.world.instances[1].location.localPositionM[1]=1.001
Check ((Report $d).ok) 'support height within 1mm tolerance accepted'
$d.world.instances[1].location.localPositionM[1]=1.00101
Check (Has (Report $d) 'SUPPORT_HEIGHT_INVALID') 'floating support beyond tolerance rejected'
$d=Base; $d.catalog.items[1].collision.centerM[1]=.08
Check ((Has (Report $d) 'SUPPORT_HEIGHT_INVALID') -and (Has (Report $d) 'ITEM_COLLISION')) 'proxy protruding below support is not exempted as parent contact'
$d=Base; $d.catalog.items[0].supports[0].centerM[1]=.5; $d.world.instances[1].location.localPositionM[1]=.5
Check (Has (Report $d) 'ITEM_COLLISION') 'declared mount inside parent body cannot bypass collision'
$d.catalog.items[1].collision.sizeM[1]=.0005; $d.catalog.items[1].collision.centerM[1]=.00025; $d.catalog.items[1].dimensionsM[1]=.0005
Check (Has (Report $d) 'ITEM_COLLISION') 'sub-millimeter proxy fully embedded in solid still collides'
$d=Base; $d.world.instances[0].location=[FamilyMuseum.Decor.ItemLocation]@{kind='storage'}; $d.world.instances[1].location.localPositionM[0]=.4
Check (Has (Report $d) 'SUPPORT_OVERHANG') 'stored assembly still validates child support'
$d=Base; $d.world.instances += (Child 'vase.002' 'plinth.001' .15)
Check (Has (Report $d) 'ITEM_COLLISION') 'siblings sharing same point collide'
$d.world.instances[2].location.localPositionM[0]=.27
Check ((Report $d).ok) 'sibling boundary contact allowed'
$d.world.instances[2].location.localPositionM[0]=.269
Check ((Report $d).ok) 'sibling 1mm numerical overlap accepted'
$d.world.instances[2].location.localPositionM[0]=.26899
Check (Has (Report $d) 'ITEM_COLLISION') 'sibling penetration beyond tolerance rejected'
$d=Base; $d.world.instances[0].location=[FamilyMuseum.Decor.ItemLocation]@{kind='storage'}; $d.world.instances += (Child 'vase.002' 'plinth.001' .15)
Check (Has (Report $d) 'ITEM_COLLISION') 'stored siblings cannot overlap'
$d=Base; $d.world.instances += (Item 'plinth.002' .8)
Check ((Report $d).ok) 'furniture boundary contact allowed'
$d.world.instances[2].location.positionM[0]=.799
Check ((Report $d).ok) 'furniture 1mm overlap tolerance accepted'
$d.world.instances[2].location.positionM[0]=.79899
Check (Has (Report $d) 'ITEM_COLLISION') 'furniture penetration beyond tolerance rejected'
$d=Base; $d.world.instances += (Item 'plinth.002'); $secondRoom=[FamilyMuseum.Decor.DecorRoom]@{id='room.b';name='second';bounds=$d.world.rooms[0].bounds;reservedZones=@()}; $d.world.rooms += $secondRoom; $d.world.instances[2].location.roomId='room.b'
Check ((Report $d).ok) 'independent rooms may contain same local coordinates'
$d=Base; $d.world.instances[0].location=[FamilyMuseum.Decor.ItemLocation]@{kind='storage'}; $item=Item 'plinth.002'; $item.location=[FamilyMuseum.Decor.ItemLocation]@{kind='storage'}; $d.world.instances += $item; $d.world.instances += (Child 'vase.002' 'plinth.002')
Check ((Report $d).ok) 'different stored assemblies do not collide at their virtual origin'
$d=Base; $d.world.instances[0].location=Room 9.6
Check ((Report $d).ok) 'root may touch exact room wall boundary'
$d.world.instances[0].location.positionM[0]=9.601
Check ((Report $d).ok) 'room boundary within 1mm accepted'
$d.world.instances[0].location.positionM[0]=9.60101
Check (Has (Report $d) 'OUTSIDE_ROOM' 'plinth.001') 'rotated proxy extent not only origin must fit room'
$d=Base; $d.world.instances[0].location=Room 9.6; $d.catalog.items[0].supports[0].centerM[0]=.5; $d.world.instances[1].location.localPositionM[0]=.5
Check ((Has (Report $d) 'OUTSIDE_ROOM' 'vase.001') -and -not (Has (Report $d) 'OUTSIDE_ROOM' 'plinth.001')) 'attached child can exceed room even when parent fits'
$d=Base; $d.world.instances[0].location.positionM[1]=.001
Check ((Report $d).ok) 'floor anchor within tolerance accepted'
$d.world.instances[0].location.positionM[1]=.00101
Check (Has (Report $d) 'FLOOR_HEIGHT_INVALID') 'floating furniture beyond floor tolerance rejected'
$d=Base; $d.world.rooms[0].bounds.centerM[1]=3; $d.world.instances[0].location.positionM[1]=1
Check ((Report $d).ok) 'floor plane derives from room bounds rather than hard-coded zero'
$d=Base; $d.world.instances[0].location=Room 0 -7.5
Check (Has (Report $d) 'RESERVED_ZONE_BLOCKED') 'door safety volume cannot be occupied'
$d=Base; $d.world.instances[0].location=Room 0 -7.5; $d.catalog.items[0].supports[0].centerM[1]=3; $d.world.instances[1].location.localPositionM[1]=3
Check (-not (Has (Report $d) 'RESERVED_ZONE_BLOCKED' 'vase.001')) 'elevated item does not block shorter door safety volume'
$d=Base; $d.world.instances[0].location=Room .123 .057
Check ((Report $d).ok -and $d.world.instances[0].location.positionM[0] -eq .123) 'valid loaded free positions are never snapped during validation'
$d=Base; $d.world.instances[1].definition.id='absent'; $d.world.instances[0].location.positionM[0]=999
Check ((Has (Report $d) 'DEFINITION_NOT_FOUND') -and -not (Has (Report $d) 'OUTSIDE_ROOM')) 'broken reference gate avoids guessed dependent geometry'
$d=Base; $d.world.instances=@(); $d.world.rooms[0].reservedZones=@()
for ($n=0;$n -lt 64;$n++) { $d.world.instances += (Item "grid.$n" (($n%8)*2-7) ([Math]::Floor($n/8)*2-7) (($n%4)*90)) }
Check ((Report $d).ok) '64 separated rotated furniture proxies fit without fixed 30-slot assumptions'

$d=Base; $s=[FamilyMuseum.Decor.DecorPlacementValidator]::BeginEdit($d.catalog,$d.world)
$before=[FamilyMuseum.Decor.DecorJson]::WriteWorld($s.BuildSnapshot())
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.door','plinth.001',(Room 0 -7.5)))
Check (-not $r.accepted -and (Has $r 'RESERVED_ZONE_BLOCKED') -and $s.UndoCount -eq 0 -and [FamilyMuseum.Decor.DecorJson]::WriteWorld($s.BuildSnapshot()) -ceq $before) 'command integration rejects blocked door without changing history or draft'
$bad=[FamilyMuseum.Decor.ItemLocation]@{kind='parent';parentId='plinth.001';surfaceId='top';localPositionM=[double[]]@(.4,1,0);yawDeg=0}
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.overhang','vase.001',$bad))
Check (-not $r.accepted -and (Has $r 'SUPPORT_OVERHANG') -and $s.Inventory().ownedCount -eq 2) 'command integration rejects bad support without quantity change'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.yaw','plinth.001',(Room 2 0 90)))
Check ($r.accepted -and $s.UndoCount -eq 1 -and $s.BuildSnapshot().instances[1].location.parentId -eq 'plinth.001') 'valid rotated assembly accepted with child attachment intact'
$r=$s.Undo(); $r=$s.Redo(); Check ($r.accepted -and $s.BuildSnapshot().instances[0].location.yawDeg -eq 90) 'geometry-validated history restores exact quarter turn'
$d=Base; $d.world.instances += (Item 'plinth.002' 2); $s=[FamilyMuseum.Decor.DecorPlacementValidator]::BeginEdit($d.catalog,$d.world)
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.collide','plinth.001',(Room 2)))
Check (-not $r.accepted -and (Has $r 'ITEM_COLLISION') -and $s.UndoCount -eq 0) 'moving parent into another assembly is rejected atomically'
$d=Base; $d.world.instances[0].location.yawDeg=45
Throws { [FamilyMuseum.Decor.DecorPlacementValidator]::BeginEdit($d.catalog,$d.world) } 'placement editor refuses invalid initial geometry'
$d=Base; $d.world.instances += (Item 'plinth.002'); $first=(Report $d).issues|ConvertTo-Json -Depth 6 -Compress
[Array]::Reverse($d.world.instances); [Array]::Reverse($d.catalog.items); $second=(Report $d).issues|ConvertTo-Json -Depth 6 -Compress
Check ($first -ceq $second) 'collision reports are independent of input order'
$large=[FamilyMuseum.Decor.DecorGeometry]::Bounds([FamilyMuseum.Decor.DecorBox]@{centerM=@(0,.5,0);sizeM=@(1,1,1)},@(0,0,0),0)
$thin=[FamilyMuseum.Decor.DecorGeometry]::Bounds([FamilyMuseum.Decor.DecorBox]@{centerM=@(0,.50025,0);sizeM=@(.1,.0005,.1)},@(0,0,0),0)
Check ([FamilyMuseum.Decor.DecorGeometry]::Overlaps($large,$thin) -and [FamilyMuseum.Decor.DecorGeometry]::Overlaps($thin,$large)) 'containment collision is symmetric and based on separating distance'
$d=Base; $d.world.instances[0].location.yawDeg=90; $d.catalog.items[0].supports[0].sizeXZ=@(.7,.3); $d.catalog.items[1].collision.sizeM=@(.6,.18,.1); $d.catalog.items[1].collision.centerM=@(0,.09,0); $d.world.instances[1].location.localPositionM=@(0,1,0)
Check ((Report $d).ok) 'long child fits rectangular support in matching local orientation'
$d.world.instances[1].location.yawDeg=90
Check (Has (Report $d) 'SUPPORT_OVERHANG') 'child local quarter-turn footprint is checked before parent world rotation'
Write-Output "DECOR_PLACEMENT_PASS checks=$script:checks (quarter-turn proxies; no mesh/navigation/Unity test)"
