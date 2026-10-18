#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$domain=Join-Path $root 'apps/quest-viewer/Assets/Museum/Runtime/Decor/Domain'
Add-Type -Path @('DecorData.cs','DecorJson.cs','DecorReferenceValidator.cs','DecorCommands.cs','DecorEditSession.cs','DecorInventoryQuery.cs','DecorGeometry.cs','DecorPlacementValidator.cs','DecorInteraction.cs' | ForEach-Object {Join-Path $domain $_})
$script:checks=0
function Check([bool]$ok,[string]$message){if(-not $ok){throw "FAIL $message"};$script:checks++;Write-Output "PASS $message"}
$d=$null;$report=$null
[void][FamilyMuseum.Decor.DecorJson]::TryRead((Get-Content -LiteralPath (Join-Path $root 'tests/fixtures/decor/valid-placed.json') -Raw),[ref]$d,[ref]$report)
$edit=[FamilyMuseum.Decor.DecorInteraction]::new($d.catalog,$d.world)
function Room([double]$x,[double]$z=0,[double]$yaw=0){return [FamilyMuseum.Decor.ItemLocation]@{kind='room';roomId='room.a';positionM=[double[]]@($x,0,$z);yawDeg=$yaw}}
function Child([double]$x=.15){return [FamilyMuseum.Decor.ItemLocation]@{kind='parent';parentId='plinth.001';surfaceId='top';localPositionM=[double[]]@($x,1,0);yawDeg=0}}
$original=[FamilyMuseum.Decor.DecorJson]::WriteWorld($edit.Snapshot())
$edit.BeginNew($d.catalog.items[0],$null)
Check ($edit.HasPreview -and -not $edit.PreviewValid) 'new preview requires actual target'
$edit.RotatePreview(1)
Check ($edit.PreviewYaw -eq 90) 'rotation before first target is retained'
$edit.SetTarget((Room 3))
Check $edit.PreviewValid 'valid new position'
Check ($edit.Inventory().ownedCount -eq 2 -and $edit.UndoCount -eq 0) 'preview has no ownership or history'
$copy=$edit.PreviewItem;$copy.variantId='invalid';$copy.location.positionM[0]=99
Check ($edit.PreviewItem.variantId -ne 'invalid' -and $edit.PreviewItem.location.positionM[0] -eq 3) 'preview accessor does not expose mutable state'
$edit.CancelPreview();Check (-not $edit.HasPreview -and [FamilyMuseum.Decor.DecorJson]::WriteWorld($edit.Snapshot()) -ceq $original) 'cancel preserves baseline'
$edit.BeginNew($d.catalog.items[0],$null);$edit.SetTarget((Room 0))
Check (-not $edit.PreviewValid -and $null -eq $edit.Commit($false)) 'collision cannot submit'
Check ($edit.UndoCount -eq 0 -and $edit.Inventory().ownedCount -eq 2) 'rejected click leaves counts/history unchanged'
$edit.SetTarget((Room 0 -7.5));Check (-not $edit.PreviewValid) 'door zone rejected'
$edit.SetTarget((Room 10));Check (-not $edit.PreviewValid) 'room outside rejected'
$edit.SetTarget((Room 3));$id=$edit.PreviewItem.id;$r=$edit.Commit($true)
Check ($r.accepted -and $edit.Inventory().ownedCount -eq 3 -and $edit.UndoCount -eq 1) 'new placement adds one item and one command'
Check ($edit.HasPreview -and $edit.PreviewItem.id -ne $id -and -not $edit.PreviewValid) 'repeat uses new ID and rechecks collision'
$edit.SetTarget((Room 5));$id2=$edit.PreviewItem.id;$r=$edit.Commit($false)
Check ($r.accepted -and -not $edit.HasPreview -and $edit.Inventory().ownedCount -eq 4) 'repeat second commit exact count'
$r=$edit.Undo();Check ($r.accepted -and -not ($edit.Snapshot().instances.id -contains $id2)) 'undo removes second new item'
$edit.BeginNew($d.catalog.items[0],$null);$edit.SetTarget((Room 99));$historyBefore=$edit.UndoCount;$redoBefore=$edit.RedoCount;[void]$edit.Commit($false);$edit.CancelPreview()
Check ($edit.UndoCount -eq $historyBefore -and $edit.RedoCount -eq $redoBefore -and $redoBefore -eq 1) 'invalid preview click after Undo preserves Redo'
$r=$edit.Redo();Check ($r.accepted -and ($edit.Snapshot().instances.id -contains $id2)) 'redo restores identical ID'
$edit.Select('plinth.001');$r=$edit.RotateSelected(1)
Check ($r.accepted -and ($edit.Snapshot().instances|Where-Object id -eq 'plinth.001').location.yawDeg -eq 90) 'selected quarter rotation'
Check (($edit.Snapshot().instances|Where-Object id -eq 'vase.001').location.parentId -eq 'plinth.001') 'rotation preserves child attachment'
[void]$edit.BeginMove('plinth.001');$edit.SetTarget((Room 7 2 90))
Check $edit.PreviewValid 'assembly move validates all members'
Check (($edit.PreviewWorld().instances|Where-Object id -eq 'vase.001').location.localPositionM[0] -eq .15) 'assembly preview preserves child local coordinates'
$history=$edit.UndoCount;$edit.CancelPreview();Check ($history -eq $edit.UndoCount) 'existing move cancel creates no history'
$edit.BeginNew($d.catalog.items[0],'green');$edit.SetTarget((Room -6));$beforeColor=[FamilyMuseum.Decor.DecorJson]::WriteWorld($edit.Snapshot());[void]$edit.SetVariant('ivory')
Check ($edit.PreviewItem.variantId -eq 'ivory' -and [FamilyMuseum.Decor.DecorJson]::WriteWorld($edit.Snapshot()) -ceq $beforeColor) 'preview color does not mutate existing draft'
$edit.CancelPreview()
$edit.Select('vase.001');[void]$edit.BeginMove('vase.001');$edit.SetTarget((Child .2));Check $edit.PreviewValid 'individual child can move'
$r=$edit.Commit($false);Check $r.accepted 'child placement commits'
$edit.Select('plinth.001');$r=$edit.SetVariant('ivory');Check $r.accepted 'placed color command'
$r=$edit.StoreSelected();Check ($r.accepted -and $edit.Inventory().storedCount -eq 2) 'parent store includes attached child'
$beforeStore=$edit.Inventory().ownedCount;[void]$edit.BeginMove('plinth.001');$edit.SetTarget((Room -3 0 180));$r=$edit.Commit($false)
Check ($r.accepted -and $edit.Inventory().ownedCount -eq $beforeStore -and $edit.Inventory().storedCount -eq 0) 'restore uses same ownership and assembly'
$parent=$edit.Snapshot().instances|Where-Object id -eq 'plinth.001';$child=$edit.Snapshot().instances|Where-Object id -eq 'vase.001'
Check ($parent.variantId -eq 'ivory' -and $child.location.parentId -eq $parent.id -and $child.location.localPositionM[0] -eq .2) 'restore retains color, child ID and offset'
Check ([FamilyMuseum.Decor.DecorPlacementValidator]::Validate($d.catalog,$edit.Snapshot()).ok) 'resulting assembly geometry valid'
for($n=0;$n -lt 50;$n++){$edit.BeginNew($d.catalog.items[1],$null);$edit.SetTarget((Child));$edit.CancelPreview()}
Check (-not $edit.HasPreview -and $edit.Inventory().ownedCount -eq $beforeStore) '50 preview cancels preserve ownership'
Check ([FamilyMuseum.Decor.DecorJson]::WriteWorld($d.world) -ceq $original) 'caller original never mutated'
Write-Output "DECOR_INTERACTION_PASS checks=$script:checks (input-independent production coordinator; Player checked separately)"
