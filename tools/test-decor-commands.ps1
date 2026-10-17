#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$domain=Join-Path $root 'apps/quest-viewer/Assets/Museum/Runtime/Decor/Domain'
Add-Type -Path @('DecorData.cs','DecorJson.cs','DecorReferenceValidator.cs','DecorCommands.cs','DecorEditSession.cs','DecorInventoryQuery.cs' | ForEach-Object { Join-Path $domain $_ })
$script:checks=0
function Check([bool]$condition,[string]$name) { if (-not $condition) { throw "FAIL: $name" }; $script:checks++; Write-Output "PASS: $name" }
function Base {
 $doc=$null; $report=$null
 $wire=Get-Content -LiteralPath (Join-Path $root 'tests/fixtures/decor/valid-placed.json') -Raw -Encoding UTF8
 if (-not [FamilyMuseum.Decor.DecorJson]::TryRead($wire,[ref]$doc,[ref]$report)) { throw 'Invalid base fixture' }
 return $doc
}
function Room([double]$x=0,[double]$z=0,[double]$yaw=0) { return [FamilyMuseum.Decor.ItemLocation]@{kind='room';roomId='room.a';positionM=[double[]]@($x,0,$z);yawDeg=$yaw} }
function Parent([string]$id='plinth.001',[double]$x=.15) { return [FamilyMuseum.Decor.ItemLocation]@{kind='parent';parentId=$id;surfaceId='top';localPositionM=[double[]]@($x,1,0);yawDeg=0} }
function Definition([string]$id) { return [FamilyMuseum.Decor.DefinitionRef]@{id=$id;version=1} }
function Has($result,[string]$code) { return @($result.issues | Where-Object code -eq $code).Count -gt 0 }
function Wire($session) { return [FamilyMuseum.Decor.DecorJson]::WriteWorld($session.BuildSnapshot()) }
function NewSession { $d=Base; return [FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$d.world) }

$d=Base; $source=[FamilyMuseum.Decor.DecorJson]::Write($d)
$s=[FamilyMuseum.Decor.DecorEditSession]::BeginEdit($d.world,$d.catalog)
Check ($s.UndoCount -eq 0 -and $s.RedoCount -eq 0 -and -not $s.IsClosed) 'begin with empty history'
$inventory=$s.Inventory(); Check ($inventory.ownedCount -eq 2 -and $inventory.placedCount -eq 2 -and $inventory.storedCount -eq 0) 'initial ownership derived from instances'
$copy=$s.BuildSnapshot(); $copy.instances[0].location.positionM[0]=999
Check ($s.BuildSnapshot().instances[0].location.positionM[0] -eq 0) 'snapshot cannot mutate session'
$d.catalog.items[0].variants[0].id='external.change'; $d.world.instances[0].location.positionM[0]=888
Check ($s.BuildSnapshot().instances[0].location.positionM[0] -eq 0) 'caller input cannot mutate session'
$d=Base; $s=[FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$d.world)
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::Store('cmd.store','plinth.001'))
Check ($r.accepted -and $r.changed -and $s.UndoCount -eq 1) 'store one assembly is one command'
$inventory=$s.Inventory()
Check ($inventory.ownedCount -eq 2 -and $inventory.storedCount -eq 2 -and $inventory.placedCount -eq 0) 'stored parent includes child in counts'
Check ($inventory.storedGroups.Count -eq 1 -and $inventory.storedGroups[0].rootCount -eq 1 -and $inventory.storedGroups[0].memberCount -eq 2 -and $inventory.storedGroups[0].hasChildren) 'assembly card includes child without separate ownership'
Check ($r.resultingDraft.instances[1].location.kind -eq 'parent' -and $r.resultingDraft.instances[1].location.parentId -eq 'plinth.001') 'store preserves attachment'
$r.resultingDraft.instances[0].id='external.modified'
Check ($s.BuildSnapshot().instances[0].id -eq 'plinth.001') 'command result is detached copy'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::PlaceStored('cmd.place','plinth.001',(Room 2 1 90)))
Check ($r.accepted -and $s.Inventory().ownedCount -eq 2 -and $s.Inventory().placedCount -eq 2) 're-place uses same IDs and quantity'
$snap=$s.BuildSnapshot()
Check ($snap.instances[0].location.yawDeg -eq 90 -and $snap.instances[1].location.localPositionM[0] -eq .15 -and $snap.instances[0].variantId -eq 'green') 'parent transform preserves child local transform and color'
Check ([FamilyMuseum.Decor.DecorJson]::Write($d) -ceq $source) 'store and re-place do not touch source snapshot'
$r=$s.Undo(); Check ($r.accepted -and $s.Inventory().storedCount -eq 2) 'undo place restores assembly storage'
$r=$s.Undo(); Check ($r.accepted -and $s.Inventory().placedCount -eq 2) 'undo store restores placement'
$r=$s.Redo(); Check ($r.accepted -and $s.Inventory().storedCount -eq 2) 'redo store restores exact assembly'
$r=$s.Redo(); Check ($r.accepted -and $s.BuildSnapshot().instances[0].location.positionM[0] -eq 2) 'redo place restores position'

$s=NewSession; $start=Wire $s
for ($i=1;$i -le 20;$i++) {
 $r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation("cmd.move.$i",'plinth.001',(Room ($i*.05) (-$i*.01) (($i%4)*90))))
 Check $r.accepted "move $i accepted"
}
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetVariant('cmd.color','plinth.001','ivory'))
Check $r.accepted 'variant change accepted'
$end=Wire $s
for ($i=0;$i -lt 21;$i++) { Check ($s.Undo().accepted) "undo $i" }
Check ((Wire $s) -ceq $start) 'all undo restores exact original positions and color'
for ($i=0;$i -lt 21;$i++) { Check ($s.Redo().accepted) "redo $i" }
Check ((Wire $s) -ceq $end) 'all redo restores exact final positions and color'
Check (Has ($s.Redo()) 'NOTHING_TO_REDO') 'empty redo is explicit failure'
$before=Wire $s; $u=$s.UndoCount
$bad=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetVariant('cmd.bad','plinth.001','absent'))
Check (-not $bad.accepted -and (Has $bad 'VARIANT_NOT_FOUND') -and (Wire $s) -ceq $before -and $s.UndoCount -eq $u) 'rejected change preserves draft and history'
$repeat=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetVariant('cmd.bad','plinth.001','absent'))
Check (-not $repeat.accepted -and $repeat.duplicate -and (Has $repeat 'VARIANT_NOT_FOUND')) 'rejected request retries repeat result without applying'
$repeat.issues[0].code='external.issue'
Check (Has ($s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetVariant('cmd.bad','plinth.001','absent'))) 'VARIANT_NOT_FOUND') 'cached rejection cannot be mutated through result'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetVariant('cmd.bad','plinth.001','green'))
Check (-not $r.accepted -and (Has $r 'COMMAND_ID_REUSED')) 'same ID with different rejected payload is forbidden'
$r=$s.Undo(); $redo=$s.RedoCount
$bad=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetVariant('cmd.bad2','plinth.001','absent'))
Check ($s.RedoCount -eq $redo) 'rejection preserves redo branch'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.fork','plinth.001',(Room 3)))
Check ($r.accepted -and $s.RedoCount -eq 0) 'new accepted change clears redo branch'
$repeat=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.fork','plinth.001',(Room 3)))
Check ($repeat.accepted -and $repeat.duplicate -and -not $repeat.changed) 'successful duplicate is idempotent'
$r=$s.Undo(); $before=Wire $s
$repeat=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.fork','plinth.001',(Room 3)))
Check ($repeat.duplicate -and (Wire $s) -ceq $before -and $s.RedoCount -eq 1) 'retry of undone command does not reapply it'

$d=Base; $d.world.instances=@(); $s=[FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$d.world)
$loc=Room; $cmd=[FamilyMuseum.Decor.DecorCommands]::CreateAndPlace('cmd.new','new.plinth',(Definition 'core.plinth.small'),'green',$loc)
$r=$s.ApplyCommand($cmd); Check ($r.accepted -and $s.Inventory().ownedCount -eq 1) 'create new owned instance once'
$loc.positionM[0]=700; Check ($s.BuildSnapshot().instances[0].location.positionM[0] -eq 0) 'command payload is copied'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::CreateAndPlace('cmd.child','new.vase',(Definition 'decor.vase.small'),'ivory',(Parent 'new.plinth')))
Check ($r.accepted -and $s.Inventory().ownedCount -eq 2) 'create surface child'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::CreateAndPlace('cmd.duplicate','new.plinth',(Definition 'core.plinth.small'),'green',(Room)))
Check (-not $r.accepted -and (Has $r 'INSTANCE_ALREADY_EXISTS') -and $s.Inventory().ownedCount -eq 2) 'duplicate ownership ID rejected'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::Store('cmd.newstore','new.plinth')); $r=$s.Undo(); $r=$s.Undo()
Check ($s.Inventory().ownedCount -eq 1) 'undo child creation removes only created child'
$r=$s.Redo(); Check ($s.BuildSnapshot().instances[1].id -eq 'new.vase') 'redo creation retains original generated ID'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::Store('cmd.childstore','new.vase'))
Check ($r.accepted -and $s.Inventory().storedCount -eq 1 -and $s.Inventory().placedCount -eq 1) 'store selected child independently'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::PlaceStored('cmd.reattach','new.vase',(Parent 'new.plinth')))
Check ($r.accepted -and $s.Inventory().placedCount -eq 2) 'reinstall stored child on support'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::PlaceStored('cmd.notstored','new.vase',(Parent 'new.plinth')))
Check (-not $r.accepted -and (Has $r 'ITEM_NOT_STORED')) 'placed instance cannot be treated as stored'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.orphan','new.vase',(Parent 'missing.parent')))
Check (-not $r.accepted -and (Has $r 'PARENT_NOT_FOUND')) 'commands cannot bypass parent reference checks'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.floorflower','new.vase',(Room)))
Check (-not $r.accepted -and (Has $r 'SURFACE_KIND_INVALID')) 'wrong placement kind rejected'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.nan','new.plinth',(Room ([double]::NaN))))
Check (-not $r.accepted -and (Has $r 'SCHEMA_INVALID')) 'nonfinite command cannot mutate draft'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::Store('cmd.none','missing.item'))
Check (-not $r.accepted -and (Has $r 'INSTANCE_NOT_FOUND')) 'missing target rejected'
$r=$s.ApplyCommand($null); Check (-not $r.accepted -and (Has $r 'COMMAND_INVALID')) 'null command rejected'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::Store('INVALID','new.plinth')); Check (Has $r 'COMMAND_INVALID') 'invalid command ID rejected'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::Store('cmd.parentstore','new.plinth'))
$u=$s.UndoCount; $r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::Store('cmd.noop','new.plinth'))
Check ($r.accepted -and -not $r.changed -and $s.UndoCount -eq $u) 'no-op store does not add undo entry or change count'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.bypassplace','new.plinth',(Room)))
Check (-not $r.accepted -and (Has $r 'ITEM_NOT_PLACED')) 'stored root uses PlaceStored rather than Move'

$d=Base; $d.world.instances=@(); $s=[FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$d.world)
foreach ($id in @('root.a','root.b')) { $r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::CreateAndPlace("cmd.$id",$id,(Definition 'core.plinth.small'),'green',(Room))); Check $r.accepted "create $id" }
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::Store('cmd.storea','root.a')); $r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::Store('cmd.storeb','root.b'))
$g=$s.Inventory().storedGroups[0]
Check ($g.rootCount -eq 2 -and $g.rootIds[0] -eq 'root.b') 'same definition and variant group, latest stored first'
$r=$s.Undo(); $r=$s.Redo(); Check ($s.Inventory().storedGroups[0].rootIds[0] -eq 'root.b') 'redo restores inventory recency metadata'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetVariant('cmd.colora','root.a','ivory'))
Check ($r.accepted -and $s.Inventory().storedGroups.Count -eq 2) 'different variants are separate storage groups'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetVariant('cmd.colorback','root.a','green'))
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::CreateAndPlace('cmd.assembly','child.a',(Definition 'decor.vase.small'),'ivory',(Parent 'root.a')))
Check ($r.accepted -and $s.Inventory().storedGroups.Count -eq 2) 'assembly and plain item do not group together'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::CreateAndPlace('cmd.assemblyb','child.b',(Definition 'decor.vase.small'),'ivory',(Parent 'root.b')))
Check ($r.accepted -and $s.Inventory().storedGroups.Count -eq 1 -and $s.Inventory().storedGroups[0].memberCount -eq 4) 'equivalent assemblies group without child ID dependency'
$loaded=[FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$s.BuildSnapshot())
Check ($loaded.Inventory().storedGroups[0].rootIds[0] -eq 'root.a') 'new session without recency falls back to stable ID order'

$d=Base; $s=[FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$d.world,$null,2)
for ($i=1;$i -le 3;$i++) { $r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation("cmd.trim.$i",'plinth.001',(Room $i))) }
Check ($r.historyTrimmed -and $s.UndoCount -eq 2) 'history limit reports oldest undo trimmed'
$r=$s.Undo(); $r=$s.Undo(); Check ($s.BuildSnapshot().instances[0].location.positionM[0] -eq 1) 'bounded undo retains correct oldest available state'
$r=$s.Cancel(); Check ($r.accepted -and $s.IsClosed -and $s.UndoCount -eq 0 -and $s.RedoCount -eq 0 -and $s.BuildSnapshot().instances[0].location.positionM[0] -eq 0) 'cancel restores original beyond undo history limit and closes session'
Check (Has ($s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::Store('cmd.closed','plinth.001'))) 'SESSION_CLOSED') 'closed session rejects commands'
Check ((Has ($s.Undo()) 'SESSION_CLOSED') -and (Has ($s.Redo()) 'SESSION_CLOSED')) 'closed session rejects history operations'

$d=Base
$validator=[Func[FamilyMuseum.Decor.DecorCatalog,FamilyMuseum.Decor.DecorWorld,FamilyMuseum.Decor.ValidationReport]]{
 param($catalog,$world)
 if ($world.instances[0].location.positionM[0] -gt 2) { return [FamilyMuseum.Decor.ValidationReport]::new([FamilyMuseum.Decor.ValidationIssue[]]@([FamilyMuseum.Decor.ValidationIssue]::new('TEST_GEOMETRY_BLOCKED','plinth.001'))) }
 return [FamilyMuseum.Decor.ValidationReport]::new([FamilyMuseum.Decor.ValidationIssue[]]@())
}
$s=[FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$d.world,$validator,200)
$before=Wire $s
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.geometry','plinth.001',(Room 3)))
Check (-not $r.accepted -and (Has $r 'TEST_GEOMETRY_BLOCKED') -and (Wire $s) -ceq $before -and $s.UndoCount -eq 0) 'injected placement rejection preserves state'
$mutator=[Func[FamilyMuseum.Decor.DecorCatalog,FamilyMuseum.Decor.DecorWorld,FamilyMuseum.Decor.ValidationReport]]{
 param($catalog,$world)
 $world.instances[0].location.positionM[0]=999; $catalog.items[0].name='mutated'
 return [FamilyMuseum.Decor.ValidationReport]::new([FamilyMuseum.Decor.ValidationIssue[]]@())
}
$s=[FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$d.world,$mutator,200)
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.mutator','plinth.001',(Room 1)))
Check ($r.accepted -and $s.BuildSnapshot().instances[0].location.positionM[0] -eq 1) 'injected validator cannot mutate session or candidate'
$thrower=[Func[FamilyMuseum.Decor.DecorCatalog,FamilyMuseum.Decor.DecorWorld,FamilyMuseum.Decor.ValidationReport]]{
 param($catalog,$world)
 if ($world.instances[0].location.positionM[0] -ne 0) { throw 'test validator failure' }
 return [FamilyMuseum.Decor.ValidationReport]::new([FamilyMuseum.Decor.ValidationIssue[]]@())
}
$s=[FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$d.world,$thrower,200)
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.failure','plinth.001',(Room 1)))
Check (-not $r.accepted -and (Has $r 'VALIDATOR_FAILED') -and $s.UndoCount -eq 0) 'validator exception safely rejects command'
$s=NewSession; $wire=Wire $s; $preview=Room 9; $preview.positionM[0]=5
Check ((Wire $s) -ceq $wire -and $s.UndoCount -eq 0) 'preview outside command submission leaves draft unchanged'
Check ($s.BuildSnapshot().revision -eq 0) 'editing does not increment saved revision'
$d=Base; $d.world.instances=@(); $s=[FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$d.world)
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::CreateAndPlace('cmd.issue','once.only',(Definition 'core.plinth.small'),'green',(Room)))
$r=$s.Undo(); $before=Wire $s
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::CreateAndPlace('cmd.reuse','once.only',(Definition 'core.plinth.small'),'ivory',(Room)))
Check (-not $r.accepted -and (Has $r 'INSTANCE_ALREADY_EXISTS') -and (Wire $s) -ceq $before -and $s.RedoCount -eq 1) 'new command cannot reuse an undone instance identity'
$r=$s.Redo(); Check ($r.accepted -and $s.BuildSnapshot().instances[0].id -eq 'once.only') 'redo still restores the reserved original identity'
$d=Base; $s=[FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$d.world)
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.badparent','plinth.001',(Parent 'vase.001')))
Check (-not $r.accepted -and (Has $r 'PARENT_CYCLE') -and $s.UndoCount -eq 0) 'commands cannot introduce cyclic parent graph'
$s=NewSession; $r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.return','plinth.001',(Room 1)))
$r=$s.Undo(); $before=Wire $s
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetVariant('cmd.noopcolor','plinth.001','green'))
Check ($r.accepted -and -not $r.changed -and $s.RedoCount -eq 1 -and (Wire $s) -ceq $before) 'no-op variant retains redo and exact state'
$script:callbackSession=$null; $script:busyResult=$null
$reentrant=[Func[FamilyMuseum.Decor.DecorCatalog,FamilyMuseum.Decor.DecorWorld,FamilyMuseum.Decor.ValidationReport]]{
 param($catalog,$world)
 if ($null -ne $script:callbackSession) { $script:busyResult=$script:callbackSession.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::Store('cmd.nested','plinth.001')) }
 return [FamilyMuseum.Decor.ValidationReport]::new([FamilyMuseum.Decor.ValidationIssue[]]@())
}
$d=Base; $s=[FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$d.world,$reentrant,200); $script:callbackSession=$s
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.outer','plinth.001',(Room 1)))
Check ($r.accepted -and (Has $script:busyResult 'SESSION_BUSY') -and $s.Inventory().placedCount -eq 2 -and $s.UndoCount -eq 1) 'validator reentrant command is blocked without corrupting outer command'
$script:callbackSession=$null; $script:capturedCommand=$null
$mutateCommand=[Func[FamilyMuseum.Decor.DecorCatalog,FamilyMuseum.Decor.DecorWorld,FamilyMuseum.Decor.ValidationReport]]{
 param($catalog,$world)
 if ($null -ne $script:capturedCommand) { $script:capturedCommand.commandId='cmd.changed'; $script:capturedCommand.instanceId='other.item'; $script:capturedCommand.location.positionM[0]=800 }
 return [FamilyMuseum.Decor.ValidationReport]::new([FamilyMuseum.Decor.ValidationIssue[]]@())
}
$s=[FamilyMuseum.Decor.DecorEditSession]::new($d.catalog,$d.world,$mutateCommand,200)
$script:capturedCommand=[FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.captured','plinth.001',(Room 1))
$r=$s.ApplyCommand($script:capturedCommand)
$script:capturedCommand=$null
Check ($r.accepted -and $s.BuildSnapshot().instances[0].location.positionM[0] -eq 1) 'validator cannot mutate captured original command into candidate'
$r=$s.ApplyCommand([FamilyMuseum.Decor.DecorCommands]::SetLocation('cmd.captured','plinth.001',(Room 1)))
Check ($r.accepted -and $r.duplicate -and $s.UndoCount -eq 1) 'captured mutation cannot change receipt identity'
Write-Output "DECOR_COMMANDS_PASS checks=$script:checks (domain commands only; geometry/save/UI not tested)"
