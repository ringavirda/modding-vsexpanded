# Boring Machine
**Status** designed - art drawn, nothing built (no block, no block entity, no def, no recipe, no lang key, no test)   **Mod** iiex (`IronIndustryExpanded`)

**Owns**
* the machine's build state, and the record of which docs disagree;
* its footprint, cell split and drive contract as a two-cell vertical megablock: which cell carries the window, which carries the head, and where power arrives;
* the drill-bit tooling contract - a bit is fitted, swappable tooling carrying a material tier, and the tier gates the hardest metal the machine will cut;
* the job table for this machine specifically: which input + which schematic produces which part, and what each output feeds;
* the art inventory - the shape file, its groups, its four animation clips and their state;
* what it reuses from the design table's window and what it would have to write fresh;
* its proposed numbers, cost key, drops, gotchas and test surface.

**Depends on** - cited, never restated here:
[diagram crafting](../mechanics/diagram-crafting.md) owns the station idiom, Model A, the `diagram-{type}` item, the schematic-is-reusable rule and the exlib station-window plan ·
[mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md) owns the `"mpenergy"` run and the four node contracts ·
[multiblock & fillers](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md) owns the filler footprint system, behaviour-capable filler cells and the rule for when a filler cell is a graph node ·
[recipes & config](../mechanics/recipes-config.md) owns `ExBlockDef` / `ExRecipeDef`, the RCC stage builder, the cost catalogue and the golden harness ·
[density rule](../mechanics/density-rule.md) owns every mass, including `blank` @ 200 u ·
[casting cell](casting-cell.md) owns the pattern → cavity → cast-part chain that feeds this machine ·
[long cell](long-cell.md) owns the cast-stock route ·
[rolling mill](rolling-mill.md) owns the mill and its pass model, the other source of `blank` ·
[shear](shear.md) owns the crop verb · [steam hammer](steam-hammer.md) owns blanking/stamping ·
[gears](gears.md) owns the gear items this machine cuts ·
[pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) owns the pipe tiers · [cast-parts](../items/cast-parts.md) owns the cast-part catalogue and the machine bills ·
[conventions.md](../conventions.md) · [STATE.md](../../../../docs/superpowers/plans/STATE.md)

---

## Role

The finishing station: the machine that turns a rough-formed piece into a part with a dimension. Everything upstream shapes metal in bulk; this cuts it to spec.

| Verb | Station | What it does |
|---|---|---|
| cast rough | [casting cell](casting-cell.md) / [long cell](long-cell.md) | shape without precision |
| reduce | [rolling mill](rolling-mill.md) | thins a piece |
| crop | [shear](shear.md) | parts stock into a product across |
| blank / stamp | [steam hammer](steam-hammer.md) | punches a shape out of a strip |
| finish | this | bores, turns and cuts teeth - the only verb that removes metal to a tolerance |

Three reasons it exists, all downstream:

1. A cast cylinder is not an engine cylinder. Sand casts a rough cored blank; the bore has to be cut true or the piston does not seal. The historical precedent is Wilkinson's water-powered boring mill (1774), which made Watt's engine possible.
2. The cast pipe tier has no other route. Cast pipe is assembled from cast pipe-parts finished on the boring machine ([conventions.md § pipe tiers](../conventions.md), [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md)), so with the machine unbuilt, iiex's own pipe tier has no survival source.
3. Gears are cut from blanks, not cast to shape. A coarse mill gear could be cast; a gear that must mesh is cut from a disc on a gear-cutting machine, the boring mill's close cousin ([cast-parts](../items/cast-parts.md)). The `gear12` / `gear24` / `gearbevel` diagrams are therefore cutting schematics, not mold patterns.

Placement: a machine lives with the content it feeds ([STATE.md](../../../../docs/superpowers/plans/STATE.md)). The cylinder, the cast pipe-parts and the machine gears are all steam-tier parts, so the machine is iiex's; the cast blanks it eats are iiex's. iiex casts rough, iiex machines to spec.

The drive rides "MP (waterwheel)" at iron tier: per the settled power progression the run is the same at both tiers and only the prime mover changes - vanilla waterwheel at iron, steam engine at low-pressure.

---

## Structure

Two-cell vertical megablock, per [diagram crafting](../mechanics/diagram-crafting.md), which owns that statement:

| Cell | Job |
|---|---|
| bottom - principal | main interaction; opens the station window; carries the block entity |
| top - filler | swaps the drill head; carries the MP connection (south face at default north orientation) |

Both cells report powered/unpowered in block-info, so the machine's state is legible without opening the window (R7, [conventions.md:48-54](../conventions.md)).

Measured bounding boxes from `workbench/shapes/machine-mp-megablock-boringmachine.json` (87 elements, 6 top-level groups):

| Group | from → to (voxels) | Reads as |
|---|---|---|
| whole model | X −3 … 16 · Y −2 … 25 · Z −3 … 16 | Y 25 > 16 ⇒ two cells tall; overhangs its cell on −X, −Z and below |
| `Base` | (0, 0, 0) → (12, 4, 16) | the bed |
| `BaseTop` | (0, −1, 0) → (12, 6, 16) | the sliding table (`Cube9` is what `base-move` traverses) |
| `Supports` | (−1, 0, 0) → (16, 22, 14) | the two standards, spanning both cells |
| `AxleGroup` | (−3, −2, −3) → (12, 25, 16) | the drive train - the tallest group; `Axle` itself spans Y 1 … 25 |
| `Controls` | (0, −2, −2) → (14, 20, 7) | `DrillControl` (upper lever) + `BaseControl` (lower lever) |
| `DrillBit` | (−0.5, 0, 0) → (8.5, 22, 6.5) | the head, spanning both cells; `Cube41` is its root |

Consequences the builder must honour:

- The model overhangs its own cell (−3 on X and Z, −2 on Y). That is legal - copy `SolidNonOpaque` from the mill (`BlockRollingMill.cs:62-63`) - but the overhang is downward and sideways, so the placement rule and collision boxes need care.
- A filler cell can be a graph node when it declares a `BEBehaviorNetworkMember` ([multiblock](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md)), so the top cell may join the `"mpenergy"` run directly and the principal no longer has to be the `BlockNetworkNode`. If the machine takes vanilla MP instead, the filler carries the port directly, as the twin-tub blower and the flywheel do with `exlib.BEBehaviorMPFillerPort`. Which of the two is undecided - see Open.
- The head-swap gesture lives on the top cell, so the filler must be interaction-routing (`AllowAttach` / behaviour-capable, [multiblock](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md)), not a plain collision filler.

---

## Assets

The art exists and is unwired. Nothing in `src/` references it.

| Asset | State |
|---|---|
| editable shape | `workbench/shapes/machine-mp-megablock-boringmachine.json` - drawn, 87 elements, 4 animation clips. Untracked (`git status` reports `??`); the old path `workbench/shapes/machine-boringmachine.json` shows as deleted, i.e. this is a rename to the `machine-{drive}-{size}-{name}` convention |
| runtime shape | missing - `mods/iiex/assets/iiex/shapes/` holds only `boiler/`, `engine/` and `pipes/` (16 files); nothing is copied out of `editable/` |
| textures | see the path table below - not shippable as authored |
| lang | `mods/iiex/assets/iiex/lang/en.json` carries no `boringmachine-*` key |
| handbook | `mods/iiex/docs/handbook/` has 5 pages (`00-steampower` … `04-startersetup`); none is this machine. The sync pipeline joins on the `NN-` prefix and drift fails a test |
| schematic textures | `diag-item-cylinder.png`, `diag-item-gear12.png`, `diag-item-gear24.png`, `diag-item-gearbevel.png`, `diag-item-rollers*.png` are drawn in `workbench/textures/` but not shipped - `mods/iiex/assets/iiex/textures/item/diagram/` holds 22 files and none of them is a cutting schematic |

Texture paths - the same bug the design table had.

| Key | Authored value | Verdict |
|---|---|---|
| `cast-iron1` | `F:/repos/modding-vsex/exmods/workbench/textures/cast-iron1` | absolute local path, identical to the `paper` bug fixed on the [design table](design-table.md). Ships as `iiex:block/metal/castiron` (`mods/iiex/assets/iiex/textures/block/metal/castiron.png`) |
| `iron5` | `block/metal/sheet-plain/iron5` | undomained → resolves to `iiex:block/…`, which does not exist. Needs `game:` |
| `steel1` | `block/metal/sheet-plain/steel1` | undomained |
| `iron` | `block/metal/tarnished/iron` | undomained |
| `generic` | `block/wood/planks/generic` | undomained |

The fixed design table is the worked example of what the runtime copy must look like - every key domained (`mods/iiex/assets/iiex/shapes/crafting/designtable.json`).

Animations - four clips, all authored, none loops.

| `code` | frames | `onAnimationEnd` | Drives |
|---|---|---|---|
| `drill` | 30 | `EaseOut` | `DrillBit/Cube41` rotY 0 → 720°, `AxleGroup/Axle` rotZ 0 → 360° - a 2 : 1 step-up is baked into the art |
| `drill-down` | 30 | `EaseOut` | a single pose at frame 0: `Cube41` offsetY −4, `Controls/DrillControl` rotX −90° |
| `base-move` | 60 | `EaseOut` | `BaseTop/Cube9` offsetZ 0 → −3 → 0 → +3 → 0 at frames 0/14/29/44/59, with `BaseControl` rotX ∓90° in step |
| `idle` | 30 | `EaseOut` | a single rest pose on `Cube41` |

`drill` and `base-move` must be re-authored to `onAnimationEnd: Repeat`. A running clip that does not repeat drops the suppressed mesh back to the static shape (`BlockEntityPuddlingChimneyCap.cs:31-33`); as drawn, the drill would spin once and the model would blink.

The two-lever design in the art is kept: `DrillControl` (up in the head cell) and `BaseControl` (down at the bed) are two separate poses, reading as head feed and table traverse - the two axes a boring mill has.

---

## Construction

There is no recipe, and this machine has no creative-only workaround for the cast pipe tier that depends on it.

Proposed bill, from [cast-parts](../items/cast-parts.md) - that page owns the bill, this one only records its build state:

| Line item | Exists today? |
|---|---|
| frame casting (`castframe`) × 2 | none no item, no pattern |
| heavy cast plate | yes `iiex:castplate-heavy` (`CastPartItemDefinitions.cs:31-42`) |
| `gear-iron` | yes but in iiex, not iiex - see [gears](gears.md) |
| rod | yes vanilla `game:rod-*` |
| nails & strips | yes vanilla, via `Nails` (`ExIngredients.cs:36`) |

One of the five lines is buildable from cast parts today. The `castframe` pattern is not in the shipped catalogue - `PatternItemDefinitions.Molds` holds exactly four entries (`heavyplate`, `moldplate`, `molddoubleingot`, `castbarrel` - `PatternItemDefinitions.cs:71-104`).

A cost key `boringmachine-grid` (and `-rcc` if it is built as a RightClickConstructable) belongs in `IiexRecipeConfig.Defaults()` (`:59-86`) so it rescales with `IiexConfig.RecipeLevel` (`:224`). The catalogue only understands `Type = "rcc"` and `Type = "grid"` (`IiexRecipeConfig.cs:40-54`).

Drill bits are separate tooling, not part of the block - the extension point, as with the mill's roll sets and the heading bench's dies ([heading-machine](heading-machine.md) owns the `ItemDie` contract). See Numbers for the tier ladder.

---

## Operation

```
rough part (blank / cylinder blank)  +  fitted bit  +  schematic  ──station window──▶  finished part
                                                                   (timed, animated, powered)
```

Jobs. Each row is one schematic; the schematic is an item output and is never consumed ([diagram crafting](../mechanics/diagram-crafting.md) owns that rule).

| Input | Schematic | Output | Feeds |
|---|---|---|---|
| cylinder blank (cast, cored) | `diag-item-cylinder` | bored cylinder | Watt engine · pumps · steam hammer ([cast-parts](../items/cast-parts.md)) |
| cylinder blank | *(pipe-part schematic)* | cast pipe-parts | the whole cast pipe tier - faster to assemble and higher burst than plated ([pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md)) |
| `blank` @ 200 u (8 × 2 × 5) | `diag-item-gear12` / `gear24` / `gearbevel` | machine gears | [gears](gears.md), transmissions, cranks |
| small / large cast gear blank | same three | 12-tooth pinion · 24-tooth spur · bevel | one large disc serves both spur and bevel ([cast-parts](../items/cast-parts.md)) |

`blank` has no item definition. It is specified at 8 × 2 × 5 = 80 vx³ = 200 u ([rolled-parts](../items/rolled-parts.md), mass owned by [density rule](../mechanics/density-rule.md)) and is produced at the wide 2.0 stand off `castbloom`, which is [rolling mill](rolling-mill.md) + the wide hall, also unbuilt. A repo-wide grep for a `blank` item finds nothing. Its two would-be sources - the wide train and the sand cell's gear-blank patterns - are both unbuilt as well.

Player verbs.

| Gesture | Effect |
|---|---|
| RMB the bottom cell | open the station window |
| RMB the top cell with a bit | fit / swap the drill head; refuse mid-job (mirror `TryFitRollSet`, `BlockEntityRollingMill.cs:143-155`) |
| in-window | move stacks into the input slot, pick a schematic from the list, read the info panel, start |
| block-info, either cell | powered / unpowered, and job progress |

States - three, and they must be visually distinct ([diagram crafting](../mechanics/diagram-crafting.md)):

| State | Drill | Work cycle |
|---|---|---|
| unpowered | still | none; block-info says why |
| powered, idle | spins (`drill`) | none |
| working | spins | `drill-down` → `base-move` cycles → up → repeat, until the queue drains |

A stack runs item by item and takes proportionally longer, so the animation reads as job progress.

---

## Numbers

Everything in this section is proposed. The machine has no config section and no keys. The right column is what exists today and what the value would be read from or calibrated against.

| Key | Proposed | file:line | What it does |
|---|---|---|---|
| `BoringTickMs` | 1000 ms | state `ProductionTickMs` on the hosted process - `BEBehaviorProductionMachine.cs:25` (`protected virtual`, default 1000) | job tick |
| `BoringSecondsPerItem` | ≈ 2 s | - | "a craft takes a couple of seconds" ([diagram crafting](../mechanics/diagram-crafting.md)); a stack multiplies it |
| `BoringPortResistance` | 0.5 | `BEBehaviorMPFillerPort.DefaultResistance = 0.5f` - `BEBehaviorMPFillerPort.cs:30` | vanilla-MP load, if the vanilla route is chosen |
| bit tier ladder | cast iron → quench-hardened steel → HSS | [STATE.md § D9](../../../../docs/superpowers/plans/STATE.md) (crucible steel's consumers include "boring-machine bits") | bit material gates the hardest metal the machine will cut |
| bit durability | - | model on `WoodenPatternDurability = 24` (`PatternItemDefinitions.cs:109`) | a bit is a wear part, like the pattern |
| cost key | `boringmachine-grid` | `IiexRecipeConfig.cs:59-86` | rescales with `RecipeLevel` (`IiexConfig.cs:224`) |

Hard-coded values that will bite:

| Constant | Value | file:line | Note |
|---|---|---|---|
| `ProductionTickMs` | 1000 ms | `BEBehaviorProductionMachine.cs:25` | hard-coded virtual default, not config |
| `MaxCatchupTickMultiple` | 2f | `BEBehaviorProductionMachine.cs:77` | hard-coded `private const`; clamps one catch-up `dt` (`:146`), so the bound is twice **this machine's** interval |
| `DefaultResistance` | 0.5f | `BEBehaviorMPFillerPort.cs:30` | hard-coded; overridable per-declaration via a `resistance` property (`:68`) |
| `MpMaxSpeed` | 2f | `ExlibConfig.cs:98` | owned by [mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md) |
| window packet ids | 1000 / 1001 / 1002 | `BlockEntityDesignTable.cs:40`, `:41`, `:44`; `GuiDialogDesignTable.cs:27` | hard-coded literals; 1000/1001 are the vanilla container open/close protocol - a second station must use the same two and pick its own ≥ 1002 |

Masses (`blank` 200 u, cylinder cavity 297, gear-blank cavities 235 / 255) belong to [density rule](../mechanics/density-rule.md), [casting cell](casting-cell.md) and [cast-parts](../items/cast-parts.md).

---

## Drops

Proposed, since nothing is built.

| Broken | Returns |
|---|---|
| the machine | itself, one item - `MaxStackSize(1)` is the machine convention (`BlockRollingMill.cs:53`, `BlockDesignTable.cs:40`) |
| the fitted bit | spawned at the block, not destroyed - copy `BlockEntityRollingMill.OnBlockBroken` (`:374-383`), which spawns the jammed piece and the roll set before calling base |
| the window inventory | dropped - `BlockEntityContainer` does this for free, as the design table gets it (`BlockEntityDesignTable.cs:27`) |
| a part mid-job | handed back unchanged; a cut is committed only on completion |
| the top filler | routes to the principal - [multiblock](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md) owns break rerouting |

If it is built as a RightClickConstructable instead, `NoDrops()` + an RCC scatter is the other established shape (`BlockTransmission.cs:52`, `:220-225`).

---

## Code

Nothing exists. A repo-wide `grep -rni boring src/` returns one hit and it is a comment: `CastPartItemDefinitions.cs:12` ("boring-machine blank stock").

| Piece | Where it goes | Model it on |
|---|---|---|
| `BlockBoringMachine` | `mods/iiex/src/BlockStructures/Machining/Blocks/` | `BlockDesignTable.cs:24` (`Block` + `IExBlockDefProvider` + `OnBlockInteractStart` → BE) plus `BlockTwinTubMPBlower` / `BlockFlywheel` for the filler footprint |
| `BlockEntityBoringMachine` | `.../Machining/BlockEntities/` | `BlockEntityContainer` for the window inventory, hosting a `BEBehaviorProductionMachine` (`exlib/src/Blocks/Machines/BEBehaviorProductionMachine.cs:15`) for the timed job + away-catch-up. `BlockEntityRollingMill.cs:28`, `:48` is the worked example of the host pattern |
| the window | `.../Machining/Gui/GuiDialogBoringMachine.cs` | `GuiDialogDesignTable.cs:23` - `GuiDialogBlockEntity`, `IsDuplicate` guard (`:50`), `Compose()` (`:77`), a dropdown + `AddDynamicText` info panel (`:138-140`), a button that sends one packet (`:173-182`) |
| packet handshake | on the BE | `BlockEntityDesignTable.OnReceivedClientPacket` (`:103-144`) - the open/close/action protocol, the `Claims.TryAccess` audit (`:115-122`) and the `packetid < 1000 → InvNetworkUtil` route (`:125-129`) |
| typed slots | inventory class | `InventoryDesignTable` (`:230-247`) + `ItemSlotDesignInput` (`:251`) / `ItemSlotDesignOutput` (`:276`) |
| the job itself (pure) | `.../Machining/BoringJob.cs` | `BlockEntityDesignTable.TryDraft` (`:175-206`) is the shape: validate inputs, resolve the output, guard the output slot, mutate, `MarkDirty()` - headless-testable |
| power read | inside the tick | vanilla route: `BEBehaviorMPFillerPort.IsTurning` / `.Speed` (`:45`, `:49`). `mpenergy` route: `(NetworkSystem?.GetNetworkAt(Pos) as MpEnergyNetwork)?.State?.Speed` - `BlockEntityRollingMill.cs:89-93` |
| animation drive | on job state | `MPAnim.AdvanceFrame` via the port's `CurrentAngleRad` (`BEBehaviorMPFillerPort.cs:42`); clips are authored as one revolution so playback is `ω / 2π` (`EnergyAnim.cs:23-24`) |
| def + recipe | `Machining/BoringMachineDefinitions.cs` + an `ExRecipeDef` grid | `BlockDesignTable.Definitions` (`:26-44`), `CraftingStationRecipeDefinitions.cs:24-37` |

The exlib station-window base does not exist. [diagram crafting](../mechanics/diagram-crafting.md) plans one (slots + list + info panel + guide viewer, parameterised); today `GuiDialogDesignTable` lives in iiex and enumerates `capi.World.Items` directly (`:53-56`). So "reuses the design table's station-window infra" means copy an iiex class into iiex, or promote it to exlib first. Promoting it is the right call and a prerequisite: iiex depends on iiex, so a cross-mod `using IronIndustryExpanded.…` would work but would put an iiex GUI class in iiex's dependency surface.

Caller-side contract for anyone adding a job: declare it on the schematic item, in the `MoldSpec` / `RollSetSpec` idiom (`MoldSpec.cs:18-25`, `RollSetSpec.cs:9-24`) - `{input code, min bit tier, output code, count, seconds}`. No boring-machine code should ever name a product.

---

## Gotchas

- `overview.md:70` lists the boring machine as shipped iiex content. It is not built - [diagram crafting](../mechanics/diagram-crafting.md) records that correctly. The other two overview mentions (`:127` "in scope and simply not built yet", `:160` build order) are consistent with planned and need no change; line 70 does.
- `overview.md:70` also still calls iiex "Pipes & Power Expanded". The mod's display name is Low Pressure Expanded (`mods/iiex/src/modinfo.json`).
- ~~The timed job and the window inventory are two bases.~~ Retired: the production tick is a behaviour (`BEBehaviorProductionMachine`), so the base slot goes to `BlockEntityContainer` for the inventory - the shape `BlockEntityDesignTable` already has (`:27`) - and the machine adds the process in its constructor and publishes its gate through `IProductionReadiness`. [framework composition](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/framework-composition.md) owns the rule; `BlockEntityRollingMill` is the shipped host.
- A custom GUI over a container desyncs without packet routing. `BlockEntityContainer` does not route the window's packets; the recorded scar is the hopper. `BlockEntityDesignTable.OnReceivedClientPacket` (`:103-144`) is the reference implementation, including the claim-access audit.
- The drill clip does not repeat. All four clips are `onAnimationEnd: EaseOut`. Re-author `drill` and `base-move` to `Repeat` or the mesh blinks back to static.
- The shape's textures are not shippable. One absolute Windows path and four undomained `block/…` paths - see Assets.
- ~~A filler cannot be a graph node.~~ Retired: a footprint cell that declares a `BEBehaviorNetworkMember` is a node, so the `"mpenergy"` port may live on the top cell after all. [multiblock](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/multiblock.md) owns the rule. What was the design's biggest structural constraint is no longer one.
- The schematic is never consumed; the diagram sometimes is. The boring machine's schematics are reusable tooling ([diagram crafting](../mechanics/diagram-crafting.md)), while a structure-core diagram is a consumed grid ingredient. Both are decided by the `.Tool()` flag, not by machine code.
- The machine's whole input chain is unbuilt. `blank`, `castframe`, the cylinder blank, the gear blanks and the cutting schematics do not exist as items; only `castplate-heavy` and `cast-barrel` do (`CastPartItemDefinitions.cs:28-29`).
- This machine gates the cast pipe tier. Until it exists, `iiex:pipe-*` has no cast-pipe-part route and the tier is reachable only through the recipes that currently accept the plain plated iiex segment (`MachineRecipeDefinitions.cs`'s `StraightPipe`, which is deliberately `iiex:pipe-plated-straight-*`). The dead cost key `pipe-straight-grid` (`IiexRecipeConfig.cs:72`) costs a grid recipe that does not exist ([STATE.md](../../../../docs/superpowers/plans/STATE.md)).

---

## Open

- Nothing is built. Block, BE, window, def, recipe, shape copy, lang, handbook and tests are all absent. The drawn shape exists; the shared station-UI infra does not.
- Vanilla MP or `mpenergy`? The shape is named `machine-mp-…`; the twin-tub blower and flywheel precedent is vanilla MP on a filler port; the flywheel/transmission/mill run is `mpenergy`. The choice decides whether the top cell can carry the port at all. Undecided, and it blocks the block layout.
- Which base class. Production tick vs container - see Gotchas. Composing the tick as a behaviour would be new framework work; re-implementing a 1 s listener on the container is ~20 lines and loses away-catch-up.
- Promote the station window to exlib, or copy it? [diagram crafting](../mechanics/diagram-crafting.md) says promote. Nobody has scoped what the parameterised base is once the design table's diagram-list logic (`GuiDialogDesignTable.cs:53-73`) is factored out.
- The schematic item family is unspecified. Four cutting-schematic textures are drawn; no item, no variant list, no owner mod, no craft. Are they `diagram-{type}` variants (iiex-contributed, per [diagram crafting](../mechanics/diagram-crafting.md)) or a separate `schematic-{type}` item? The diagram catalogue's picker matches on `FirstCodePart() == "diagram"` (`GuiDialogDesignTable.cs:69-73`), so a separate family would be invisible to the design table.
- Bit tiers are named and unspecified. Cast iron → quench-hardened steel → HSS is a three-step ladder with no items, no durability, no gate implementation, and no decision on whether a bit is durability-consumed or permanent-with-a-tier. It parallels the [shear](shear.md)'s blade sets, which are equally unspecified - the two should be decided together, since both are crucible steel's consumers.
- Whether the drill head is fitted from the top cell or the window. The art gives it its own cell and its own lever; the window would be simpler. The art's answer costs an interaction-routing filler.
- No test surface exists. [diagram crafting](../mechanics/diagram-crafting.md) names the headless half (input + schematic → output matching) and the in-game half (window packet sync, the animation). The headless half is the pure `BoringJob` record above and should be written first.
