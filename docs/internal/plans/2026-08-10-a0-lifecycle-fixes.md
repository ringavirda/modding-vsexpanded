# A0 — Lifecycle bug fixes: Implementation Plan

**Status** DONE 2026-08-12, as stage A0 of the framework-composition arc
([2026-08-10-framework-composition-staging.md](2026-08-10-framework-composition-staging.md)). Kept for the
reasoning behind the five fixes, not as work to do.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the five verified block-entity lifecycle defects — a readout that can never reach the client, footprint cells orphaned by explosions, teardown that runs on removal but not on unload, an undisposed GUI, and stored item stacks that a schematic paste resolves to the wrong item.

**Architecture:** No architecture. Each fix is local to the block entity or block that owns the defect, and follows a vanilla precedent cited in the task. A0 is deliberately independent of A1–A4 so it can land without waiting for the framework work.

**Tech Stack:** C# 12, .NET 7/8/10 multi-target, Vintage Story 1.20–1.22 API, xUnit + NSubstitute, csharpier.

## Global Constraints

- **⛔ Do not run `git commit`, `git add`, `git stash`, or any command that writes to the index or history.** The user owns git history. Leave all work in the working tree. The controller snapshots the tree for review; committing would corrupt that.
- **Record instead of committing:** each task ends by appending a short entry to `docs/internal/worklog/2026-08.md` under the existing `## 2026-08-10` heading — what changed and why, in the file's voice (plain declarative, third person, no first or second person).
- **Three targets must pass:** `scripts/exmod.ps1 test all` runs 1.22/net10.0, 1.21/net8.0, 1.20/net7.0. A fix that compiles only on the newest API is not done.
- **Format before finishing:** `scripts/exmod.ps1 format`. Do not run `format -Check` — it requires a clean tree and will fail by design here.
- **Comment style is enforced by a test.** `CommentStyleGuards` fails any doc comment over 16 lines or with more than 3 `<para>` blocks. Comments describe what the code does and why; they never narrate history ("used to be", "was broken", "now fixed"). See `CONTRIBUTING.md`.
- **Test count floors** live in `scripts/test-floors.txt`. Raise the floor for a suite when you add tests to it. Never lower one to make a run pass.
- **Vanilla source** is readable at `.compat/vintagestory/` and mapped in `docs/internal/vanilla/`. Cite it when following a precedent.
- **Spelling:** UK in docs and comments (`behaviour`), US in code identifiers matching the vanilla API (`BlockEntityBehavior`).

---

## File Structure

| File | Responsibility |
|---|---|
| `mods/iiex/src/BlockNetworkPipe/BlockEntities/BlockEntityPressureValve.cs` | Task 1 — vent readout reaches the client |
| `mods/exlib/src/Blocks/Structures/BlockFilledMegastructure.cs` | Task 2 — footprint cleanup survives an explosion |
| `mods/exlib/src/Blocks/Structures/BlockEntityMultiblockStructure.cs` + 7 others | Task 3 — teardown symmetry |
| `mods/iiex/src/BlockStructures/Crafting/BlockEntities/BlockEntityDesignTable.cs` | Task 4 — dialog disposal |
| 8 block entities across iwex and smex | Task 5 — collectible mappings |

---

## Task 1: The pressure valve's vent readout reaches the client

`_lastVentVolume` is assigned only in `OnTick`, which is registered server-side only (`:51-52`). `ToTreeAttributes` writes only `gatePressure` (`:282-285`). `GetBlockInfo` reads `_lastVentVolume` (`:273`), so on the client it is permanently `0` and the `lpex:gaspressurevalve-info-overflow` line can never appear. The block also calls `MarkDirty(true)` when the value changes (`:99-100`) — paying for a chunk re-tesselation to sync a value it never sends.

**Files:**
- Modify: `mods/iiex/src/BlockNetworkPipe/BlockEntities/BlockEntityPressureValve.cs:99-101`, `:282-294`
- Test: `mods/iiex/tests/Networks/PressureValveReadoutTests.cs` (create)

**Interfaces:**
- Consumes: nothing.
- Produces: nothing later depends on this.

- [ ] **Step 1: Write the failing test**

Create `mods/iiex/tests/Networks/PressureValveReadoutTests.cs`. Model it on an existing lpex block-entity test for fixture setup — read `mods/iiex/tests/` for the established pattern first. The test must assert the round trip, not the field:

```csharp
[Fact]
public void The_vent_volume_survives_a_tree_round_trip() {
  // GetBlockInfo runs client-side, where only what ToTreeAttributes wrote exists. A field the
  // server tick sets and the tree omits reads zero on the client for ever.
  var be = NewValveWithVentVolume(12.5f);
  var tree = new TreeAttribute();

  be.ToTreeAttributes(tree);
  var restored = NewValve();
  restored.FromTreeAttributes(tree, World);

  Assert.Equal(12.5f, LastVentVolumeOf(restored), 3);
}
```

`LastVentVolumeOf` reads the private field via `ReflectionHelpers.FindField` — `mods/exlib/testing/ReflectionHelpers.cs` already provides this; use it rather than widening the field's visibility.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test mods/iiex/tests/LowPressureExpanded.Tests.csproj -f net8.0 -p:Legacy=true --filter PressureValveReadoutTests`
Expected: FAIL — the restored value is `0`.

- [ ] **Step 3: Write the field to the tree**

In `ToTreeAttributes`, beside `gatePressure`:

```csharp
tree.SetFloat("lastVentVolume", _lastVentVolume);
```

and in `FromTreeAttributes`:

```csharp
_lastVentVolume = tree.GetFloat("lastVentVolume", 0f);
```

Then reconsider the `MarkDirty(true)` at `:99-100`. The mesh does not change when the vent volume changes, so the redraw flag is wrong regardless; `MarkDirty()` syncs the block entity without forcing a chunk re-tesselation. Change it to `MarkDirty()`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test mods/iiex/tests/LowPressureExpanded.Tests.csproj -f net8.0 -p:Legacy=true --filter PressureValveReadoutTests`
Expected: PASS.

- [ ] **Step 5: Format, full suite, record**

```
scripts/exmod.ps1 format
scripts/exmod.ps1 test all
```
Raise the `LowPressureExpanded.Tests` floor in `scripts/test-floors.txt` by the number of tests added. Append the worklog entry. **Do not commit.**

---

## Task 2: A destroyed megastructure takes its filler cells with it

`BlockFilledMegastructure` clears its footprint only from `OnBlockBroken` (`:70-79`). `Block.OnBlockExploded` never calls `OnBlockBroken` — it goes straight to `world.BulkBlockAccessor.SetBlock(0, pos)` (`.compat/vintagestory/vsapi/Common/Collectible/Block/Block.cs:2536`). A worldedit delete or any other `SetBlock` behaves the same. The result is invisible, solid, unbreakable orphan cells.

Vanilla's precedent: `BlockLargeGear3m` clears its filler blocks in `OnBlockRemoved(world, pos)`, which the engine calls on every removal path.

**Files:**
- Modify: `mods/exlib/src/Blocks/Structures/BlockFilledMegastructure.cs:70-79`
- Test: `mods/exlib/tests/Blocks/StructureFillerBehaviorTests.cs` (extend — the file exists)

**Interfaces:**
- Consumes: nothing.
- Produces: nothing.

- [ ] **Step 1: Write the failing test**

Add to `mods/exlib/tests/Blocks/StructureFillerBehaviorTests.cs`, matching the fixture style already in that file:

```csharp
[Fact]
public void Replacing_the_principal_without_breaking_it_still_clears_the_footprint() {
  // An explosion sets the block to air through the bulk accessor and never calls OnBlockBroken,
  // so cleanup hung off OnBlockBroken leaves solid invisible cells behind.
  var w = NewWorldWithMegastructure(out BlockPos principal, out BlockPos[] footprint);

  w.Accessor.SetBlock(0, principal);   // what OnBlockExploded does

  foreach (var cell in footprint)
    Assert.Equal(0, w.Accessor.GetBlockId(cell));
}
```

Check how `TestWorld` routes `SetBlock` to `Block.OnBlockRemoved`; if the harness does not, make the test call `OnBlockRemoved(world, pos)` directly and say so in a comment, rather than asserting on a path the harness cannot model.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test mods/exlib/tests/ExpandedLib.Tests.csproj -f net8.0 -p:Legacy=true --filter Replacing_the_principal`
Expected: FAIL — footprint cells still hold the filler block.

- [ ] **Step 3: Move the cleanup**

Override `OnBlockRemoved(IWorldAccessor world, BlockPos pos)` on `BlockFilledMegastructure`, call `StructureFillers.RemoveFillers` there, then `base.OnBlockRemoved(world, pos)`. Keep the `OnBlockBroken` override only if it still does something the removal path does not — if its whole body was the filler cleanup plus the base call, delete the override.

⛔ Removing fillers must not recurse: `RemoveFillers` sets those cells to air, and if a filler's own removal path calls back into the principal the cleanup can re-enter. Check `StructureFillers.RemoveFillers` and `BlockStructureFiller.OnBlockRemoved` before assuming it is safe.

- [ ] **Step 4: Run test to verify it passes**

- [ ] **Step 5: Format, full suite, record**

⛔ Watch the megablock and RCC suites in smex and iwex here — several structures place fillers, and a double-removal or an early removal would show up there rather than in exlib.

---

## Task 3: Teardown runs on unload as well as removal

Eight block entities override `OnBlockRemoved` without `OnBlockUnloaded`. A chunk unload therefore skips whatever the removal path cleans up — listeners, renderers, highlights — while the vanilla base only unregisters its own tick listeners.

Two of the eight are exlib base classes, so fixing those two covers everything beneath them:

```
mods/exlib/src/Blocks/Networks/BlockEntityNetworkNode.cs
mods/exlib/src/Blocks/Structures/BlockEntityMultiblockStructure.cs
mods/iiex/src/BlockStructures/Furnaces/BlockEntities/BlockEntityHopperTall.cs
mods/iiex/src/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs
mods/iiex/src/BlockNetworkPipe/BlockEntities/BlockEntityFluidIntake.cs
mods/iiex/src/BlockNetworkPipe/BlockEntities/BlockEntityPressureValve.cs
mods/siex/src/BlockStructures/HotBlastFurnace/BlockEntities/BlockEntityHopperBell.cs
mods/siex/src/BlockStructures/SmokeStack/BlockEntities/BlockEntitySmokeStack.cs
```

⛔ **The two paths are not identical and must not be blindly unified.** Removal means the block is gone: drop contents, deregister from a network, clear a highlight permanently. Unload means the chunk left memory while the block still exists: release client resources and listeners, but **do not** drop items, and **do not** deregister a network node — `BlockEntityNetworkNode.OnBlockRemoved` calls `NetworkSystem.RemoveNode`, and doing that on unload would fracture a live network every time a player walks away.

For each of the eight, decide per line which side of that split it belongs to. The safe default for unload is: unregister listeners, dispose renderers and dialogs, clear client-side highlights. Nothing else.

**Files:**
- Modify: the eight files above
- Test: `mods/exlib/tests/Invariants/TeardownSymmetryTests.cs` (create)

**Interfaces:**
- Consumes: nothing.
- Produces: nothing.

- [ ] **Step 1: Write the failing guard**

The guard is the durable part; the per-file fixes follow from it. It must allow a deliberate asymmetry, so it checks for an explicit opt-out rather than banning the shape:

```csharp
[Fact]
public void A_block_entity_that_tears_down_on_removal_also_tears_down_on_unload() {
  // OnBlockUnloaded is the chunk-unload path. A block entity that cleans up only on removal leaks
  // whatever it holds every time a player walks away from it.
  var offenders = new List<string>();
  foreach (var f in SourceFiles("src")) {
    string text = File.ReadAllText(f);
    if (!text.Contains("override void OnBlockRemoved"))
      continue;
    if (text.Contains("override void OnBlockUnloaded"))
      continue;
    // A file may state that removal-only teardown is correct for it.
    if (text.Contains("removal-only teardown:"))
      continue;
    offenders.Add(Rel(f));
  }

  Assert.True(
    offenders.Count == 0,
    "these clean up on removal but not on chunk unload: " + string.Join(", ", offenders)
  );
}
```

- [ ] **Step 2: Run to verify it fails, naming all eight**

Run: `dotnet test mods/exlib/tests/ExpandedLib.Tests.csproj -f net8.0 -p:Legacy=true --filter TeardownSymmetryTests`
Expected: FAIL listing exactly the eight files above. If it lists more or fewer, reconcile before fixing anything.

- [ ] **Step 3: Fix the two base classes first, then re-run**

`BlockEntityMultiblockStructure` needs `OnBlockUnloaded` to clear `_highlightedStructure` the way `OnBlockRemoved` does at `:570-576` — vanilla's `BEBeeHiveKiln` clears it in both. `BlockEntityNetworkNode` must **not** call `RemoveNode` on unload; give it an `OnBlockUnloaded` that does the client-side teardown only, and add the `removal-only teardown:` note explaining why deregistration is removal-only if that ends up being the whole difference.

Re-run after the two bases: some of the remaining six may resolve through inheritance.

- [ ] **Step 4: Fix the remainder, run to verify the guard passes**

- [ ] **Step 5: Format, full suite, record**

⛔ This is the task most likely to break other suites, because teardown affects fixtures that reload block entities. `mods/exlib/testing/TestWorld.cs:304` has a reload helper whose comment already notes that a network node keeps its graph node across an unload — read it before changing `BlockEntityNetworkNode`.

---

## Task 4: The design table's dialog is closed with its block

`_dialog` is created in `ToggleDialog` (`:65-82`) and cleared only by its own `OnClosed` handler (`:78-80`). There is no `OnBlockRemoved` or `OnBlockUnloaded` override in the file. Break the table with the window open, or walk away until the chunk unloads, and the GUI stays on screen bound to a dead block entity; its `OnClosed` then sends a block-entity packet to a position that no longer has one.

Vanilla's precedent: `BEOpenableContainer` disposes in both `OnBlockUnloaded` and `OnBlockRemoved` (`.compat/vintagestory/vssurvivalmod/BlockEntity/BEOpenableContainer.cs:252-273`), via a `Dispose` that does `if (invDialog?.IsOpened() == true) invDialog?.TryClose(); invDialog?.Dispose();`.

**Files:**
- Modify: `mods/iiex/src/BlockStructures/Crafting/BlockEntities/BlockEntityDesignTable.cs`
- Test: `mods/iiex/tests/Blocks/Crafting/DesignTableDialogTests.cs` (create)

**Interfaces:**
- Consumes: the Task 3 guard now covers this file — adding `OnBlockRemoved` without `OnBlockUnloaded` would fail it.
- Produces: nothing.

- [ ] **Step 1: Write the failing test**

A GUI dialog cannot be constructed headlessly. Test the contract that matters and can be reached: that both teardown paths call the disposal helper. Extract the disposal into an internal method and assert it is invoked, or assert the field is null afterwards. Do not write a test that constructs `GuiDialogDesignTable`.

- [ ] **Step 2: Run to verify it fails**

- [ ] **Step 3: Add the disposal**

Follow the vanilla shape: one private `CloseDialog()` that closes if open and nulls the field, called from both `OnBlockRemoved` and `OnBlockUnloaded`, each after its `base` call.

- [ ] **Step 4: Run to verify it passes**

- [ ] **Step 5: Format, full suite, record**

---

## Task 5: Stored item stacks survive a schematic paste

`ItemStack.ToBytes` serialises the collectible's runtime **id**, not its code (`.compat/vintagestory/vsapi/Common/Collectible/ItemStack.cs:329-335`). Ids are world-specific, so a block entity whose stacks are pasted into another world resolves the wrong item — or none. `OnStoreCollectibleMappings` / `OnLoadCollectibleMappings` plus `ItemStack.FixMapping` exist for exactly this, and are called from `BlockSchematic` (`:426`, `:1157`).

Eight block entities store stacks without them:

```
mods/iiex/src/BlockNetworkMolten/BlockEntities/BlockEntityMoltenCanalMoldPedestal.cs
mods/iiex/src/BlockNetworkMolten/BlockEntities/BlockEntityMoltenCanalTap.cs
mods/iiex/src/BlockStructures/Forming/BlockEntities/BlockEntityRollingMill.cs
mods/iiex/src/BlockStructures/Furnaces/BlockEntities/BlockEntityHeatingHearth.cs
mods/iiex/src/BlockStructures/Furnaces/BlockEntities/BlockEntityHopperTall.cs
mods/siex/src/BlockStructures/Converter/BlockEntities/BlockEntityConverterControl.cs
mods/siex/src/BlockStructures/HotBlastFurnace/BlockEntities/BlockEntityHopperBell.cs
mods/siex/src/BlockStructures/HotBlastFurnace/BlockEntities/BlockEntityHopperReinforced.cs
```

Two already do it correctly and are the template: `BlockEntityMoltenBarrel.cs:397-415` and `BlockEntityCastMold.cs:372-390`. Copy their shape.

⛔ A block entity backed by an `InventoryBase` should forward to the inventory (`container.OnStoreCollectibleMappings`, as vanilla's `BEContainer.cs:124-132` does) rather than enumerate slots by hand. Check which of the eight hold an inventory and which hold loose fields — they need different bodies.

**Files:**
- Modify: the eight files above
- Test: `mods/iiex/tests/Blocks/CollectibleMappingTests.cs` (create), plus a smex counterpart

**Interfaces:**
- Consumes: nothing.
- Produces: nothing.

- [ ] **Step 1: Write the failing guard**

As in Task 3, the durable artefact is a guard, not eight hand-written tests:

```csharp
[Fact]
public void A_block_entity_that_stores_a_stack_maps_its_collectibles() {
  // ItemStack.ToBytes writes the runtime id, so a stack stored without OnStore/OnLoad
  // CollectibleMappings resolves to whatever holds that id in the destination world.
  var offenders = new List<string>();
  foreach (var f in SourceFiles("src")) {
    string text = File.ReadAllText(f);
    if (!text.Contains("class BlockEntity") || !text.Contains("SetItemstack("))
      continue;
    if (text.Contains("OnStoreCollectibleMappings"))
      continue;
    offenders.Add(Rel(f));
  }

  Assert.True(offenders.Count == 0, string.Join(", ", offenders));
}
```

- [ ] **Step 2: Run to verify it fails, naming exactly the eight**

- [ ] **Step 3: Implement, one file at a time, re-running the guard**

- [ ] **Step 4: Add one behavioural test**

The guard proves the methods exist; one real test must prove one of them works. Pick the simplest of the eight, store a stack, run `OnStoreCollectibleMappings` into a dictionary, and assert the collectible's code is recorded.

- [ ] **Step 5: Format, full suite, record**

---

## Self-Review

**Spec coverage.** Staging table row 1 → Task 1. Row 2 → Task 2. Rows 3 and 4 → Task 3 (the highlight fix is one of the eight files, so they merge). Row 5 → Task 4. Row 6 → Task 5. All six rows covered by five tasks.

**Placeholder scan.** Tasks 1 and 3 carry complete test code. Tasks 2, 4 and 5 describe the test shape and name the vanilla precedent rather than pre-writing fixture code, because each depends on a harness fact the implementer must read first — whether `TestWorld` routes `SetBlock` through `Block.OnBlockRemoved`, how a headless test can observe a GUI field, which of the eight block entities hold an inventory. Each says what to check and where.

**Type consistency.** `SourceFiles(string)` and `Rel(string)` appear in the Task 3 and Task 5 guards; both follow the existing helper shape in `mods/exlib/tests/Invariants/CommentStyleGuards.cs`. The Task 5 guard lives in an iwex test file but scans `src` repo-wide, matching how `CommentStyleGuards` already works from exlib's suite — if the implementer prefers, it belongs in `ExpandedLib.Tests/Invariants/` beside the others, and that is the better home.

**Known risk.** Task 3 is the one that can break unrelated suites, because teardown changes affect any fixture that reloads a block entity. Its Step 5 names the specific helper to read first.

---

## Task 6: The five blocks that hand-roll the same filler cleanup

Task 2 fixed `BlockFilledMegastructure`. Five blocks call `StructureFillers.RemoveFillers` from
`OnBlockBroken` **without** deriving from it, so they still leak orphan footprint cells on an
explosion, a worldedit delete, or any other `SetBlock`:

```
mods/iiex/src/BlockNetworkEnergy/Blocks/BlockFlywheel.cs:174 -> :182
mods/iiex/src/BlockStructures/Forming/Blocks/BlockRollingMill.cs:181 -> :189
mods/iiex/src/BlockStructures/Furnaces/Blocks/BlockTwinTubMPBlower.cs:143 -> :150
mods/iiex/src/BlockStructures/ManualPump/Blocks/BlockManualFluidPump.cs:105 -> :111
mods/siex/src/BlockStructures/Converter/Blocks/BlockConverterBessemer.cs:148 -> :164
```

They duplicate the cleanup because they cannot extend `BlockFilledMegastructure` — each has already
spent its one block base class on `BlockNetworkNode` or similar. A1 removes that constraint, after
which they can inherit the behaviour and these overrides can be deleted. Until then the fix is
duplicated deliberately, and the duplication is the reason A1 is worth doing.

⛔ `BlockBoiler.cs:117` also calls `RemoveFillers`, from an explosion path it already handles
explicitly. Read it before changing anything: it may already be correct, and it is the model for
what the others are missing.

**Files:**
- Modify: the five files above
- Test: `mods/exlib/tests/Invariants/FillerCleanupHookTests.cs` (create)

**Interfaces:**
- Consumes: Task 2's `BlockFilledMegastructure.OnBlockRemoved` as the reference shape.
- Produces: nothing.

- [ ] **Step 1: Write the failing guard**

A guard, not five hand-written tests — the point is that the class stays closed:

```csharp
[Fact]
public void Filler_cleanup_hangs_off_removal_not_breaking() {
  // Block.OnBlockExploded sets the block to air through the bulk accessor and never calls
  // OnBlockBroken, so cleanup reachable only from OnBlockBroken leaves solid invisible cells.
  var offenders = new List<string>();
  foreach (string f in SourceFiles("src")) {
    string text = File.ReadAllText(f);
    if (!text.Contains("RemoveFillers"))
      continue;
    if (!text.Contains("override void OnBlockBroken"))
      continue;
    if (text.Contains("override void OnBlockRemoved"))
      continue;
    offenders.Add(Rel(f));
  }

  Assert.True(
    offenders.Count == 0,
    "these clear their footprint only on a player break: " + string.Join(", ", offenders)
  );
}
```

- [ ] **Step 2: Run it; it must name exactly the five files above**

If it names more or fewer, reconcile before changing anything.

- [ ] **Step 3: Move each block's cleanup to `OnBlockRemoved(world, pos)`**

Follow `BlockFilledMegastructure` as amended by Task 2. Keep each `OnBlockBroken` only if it does
something removal does not — several of these also drop contents or forward to a principal, which is
break-specific and must stay.

⛔ `BlockRollingMill:189` guards its `RemoveFillers` call inside a conditional. Preserve that
condition; do not lift the call out of it.

- [ ] **Step 4: Run the guard, then the suites for all three affected mods**

- [ ] **Step 5: Format, full suite, record**
