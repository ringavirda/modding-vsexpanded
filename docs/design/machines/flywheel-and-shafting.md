# Flywheel & shafting

**Status** live and craftable - all five pieces place, connect, buffer, couple and animate, pinned by 45
test methods across six files; grid recipes cover the spur gear, both flywheels, the shaft and the three
transmission housings. The bevel-gear item still has no recipe.
**Mod** iiex (`IronIndustryExpanded`), folder `BlockNetworkEnergy/`

**Owns** the five pieces of `"mpenergy"` hardware as content:

* the flywheel block - two sizes × two orientations, both authored footprints (the ASCII drawings, the
  hub glyph, the volume-clear placement rule), the spin-animation wiring and the `OnExchanged` rebuild;
* the cast-iron shaft - three orientations including vertical, its thin collision/selection box, and the
  shaft → bevel conversion gesture;
* the bevel junction - that it connects on every face, that its geared faces are derived live rather than
  stored, the composed shaft-body + per-face-gear mesh and its south-authored rotation table, and its drops;
* the transmission block - the 2 × 2 footprint, the three types × four sides, the three-stage RCC and
  what it costs, the clutch lever cell and its interaction routing, and its no-drops rule;
* the bevel-gear item definition;
* every asset in `assets/iiex/shapes/mpenergy/` and its wiring state, including the orphaned and
  retired editable sources;
* the family's grid recipes (`Recipes/Grid/EnergyRecipeDefinitions.cs`) and the transmission's RCC cost.

**Does not own - cited only, never restated**: the energy model (`E = ½Iω²`, the torque balance, the tick),
the four node contracts, the vanilla-MP bridge and its torque curve, `CoupleRatio`, the direction flag, the
hub-cell coordinate constants, the transmission's numeric ratios, the 2 % sync throttle, `SpinSpeed` /
`IsTurning` / `BranchSpinSign`, and every `Mp*` / `Flywheel*` / `ShaftInertia` config key - all
[mp-energy](../mechanics/mp-energy.md). The filler footprint system, behaviour-capable filler cells and the
no-filler-graph-node rule - [multiblock](../mechanics/multiblock.md). Code-first defs, RCC mechanics and
goldens - [recipes & config](../mechanics/recipes-config.md). The one machine that spends the energy -
[rolling mill](rolling-mill.md).

**Depends on** [mp-energy](../mechanics/mp-energy.md) · [multiblock](../mechanics/multiblock.md) ·
[recipes & config](../mechanics/recipes-config.md) · [rolling mill](rolling-mill.md)

---

## Role

Vanilla mechanical power has no storage: a waterwheel's output is a speed, available or not. A rolling
pass wants far more torque for two seconds than any period prime mover makes continuously.

These five blocks are the drive train. The flywheel is the reservoir and the doorway - where a vanilla
waterwheel or windmill enters the `"mpenergy"` run at all. The shaft is the run itself, and carries enough
rotating mass that a short line buffers a little on its own. The bevel is the corner. The transmission is
the gearbox, and the only place two separate runs meet.

A flywheel is inertia, not a battery: a drive that cannot out-torque the load plus standing friction never
spins it up at all, so there is no trickle-charging a pulse. The build ritual that follows - spin the wheel
up, then roll - is owned by [mp-energy](../mechanics/mp-energy.md).

At iron tier the prime mover is vanilla, a waterwheel or windmill bridged through the flywheel's hub, so
mechanical power exists long before a boiler does; in iiex the player swaps the producer for an engine and
the network is unchanged.

The large flywheel and both ratio transmissions have nothing to justify them yet. The rolling mill is the
only consumer, and it is itself [blocked](rolling-mill.md#blockers).

---

## Structure

### Flywheel — a vertical disc on a horizontal shaft

Two sizes, each `ns` / `we`. The disc's face lies in the X-Y plane and it is thin in Z, so the shaft runs
along Z in the authored (`ns`) frame; `we` is the 90° Y rotation, applied to shape and footprint by the same
`StructureAngle` (`BlockFlywheel.cs:144`). Its connectors therefore sit on the two shaft-axis faces - the
same linear orientation model a straight pipe or canal uses.

Footprints are authored as `Face` grids, a front elevation thin in Z
(`BlockFlywheel.cs:52-64`, `:68-92`):

```
normal (3×3×1)          large (5×5×2)
Origin(-1, 2)           Origin(-2, 4)
z = 0                   z = 0            z = 1
  # # #                   # # # # #        # # # # #
  # M #                   # # # # #        # # # # #
  # O #                   # # M # #        # # M # #
                          # # # # #        # # # # #
                          # # O # #        # # # # #
```

`O` = the principal (the placed cell, bottom-centre of the front face). `#` = an invisible solid filler.
`M` = a hosted filler carrying two `exlib.BEBehaviorMPFillerPort` behaviours, north and south
(`:45-48`) - the vanilla-MP participant an axle couples to. Nine cells for the normal wheel
(8 fillers + principal), fifty for the large one (49 + principal). The large wheel has a hub on each
shaft face so an axle can couple from either side.

`Host()` marks a cell `allowAttach: true` (`FillerLayoutBuilder.cs:69-76`), which is what lets the player
place an axle against the hub at all. Placement refuses unless the entire disc volume is clear
(`BlockFlywheel.cs:149-167`); the fillers are cleared before the base break runs, so no invisible solid cell
is ever orphaned (`:179-190`).

The hub-cell coordinates and the fact that the BE duplicates them by hand are owned by
[mp-energy](../mechanics/mp-energy.md).

### Shaft and bevel — one cell each

A thin octagonal bar on the run's axis, three orientations - `ns`, `we` and `ud`, so a run can climb
(`BlockCastIronShaft.cs:40`). Collision and selection are a central `0.3125 … 0.6875` column running the full
cell depth, rotated per orientation by the base (`:46-47`); the block is neither side-solid nor side-opaque
(`:48-49`), so a shaft reads as a shaft rather than a wall.

The bevel is the same block with a gear on it. It is not placed: the player uses one `iiex:bevelgear` on
a shaft and `SetBlock` swaps the block (and therefore the BE class - shaft `RemoveNode`, bevel `AddNode`) for
`mpenergy-bevel-<same orientation>` (`BlockCastIronShaft.cs:62-88`). It declares the same three
orientations as the shaft, and must: a `ud` run that could climb but never turn off would be a dead end
(`BlockCastIronBevel.cs:46-48`).

A bevel presents a connector on every face (`BlockCastIronBevel.cs:65-69`) - both axis continuations and
all four perpendicular branches. Which of those faces actually grows a gear is derived live from the world
each tesselation, never stored (`BlockEntityCastIronBevel.cs:23-37`): a face is geared when it is
perpendicular to the shaft axis and a connected `mpenergy` neighbour reciprocates across it. Place a
perpendicular shaft and the gear appears; break it and the gear goes. `OnNeighbourBlockChange` re-marks the
BE dirty to make that happen (`BlockCastIronBevel.cs:104-112`).

### Transmission — a 2 × 2 gearbox that is deliberately not a node

Three types (`x2`, `x4`, `clutch`) × the four horizontal sides, built as a `BlockFilledMegastructure` with
three fillers (`BlockTransmission.cs:57-63`):

```
X-slice, S→N       -  i      i = the quarter-block clutch-lever cell (1,1,0)
                   O  -      O = principal, - = plain filler
```

The principal reads the `mpenergy` network on the cell in front and the cell behind it and projects
them onto the gear constraint without ever merging them. That it is not a graph node - and why it must not
become one - is owned by [mp-energy](../mechanics/mp-energy.md), as are the ratios and the constraint maths.

The lever interaction is localised to the `(1,1,0)` filler, rotated to the placed side
(`BlockEntityTransmission.cs:91-95`). Every other click on a built transmission is swallowed so nothing can
be placed against it; clicks before construction fall through to the RCC behaviour
(`BlockTransmission.cs:116-136`).

---

## Assets

| Piece | Runtime shape | Clips | Textures | Elements |
|---|---|---|---|---|
| Flywheel (normal) | `assets/iiex/shapes/mpenergy/flywheel.json` | `idle`, `cycle` (30 f) | `iron5`, `cast-iron1` | Supports · ShaftHousing · AxleShaft · Mass |
| Flywheel (large) | `.../flywheel-large.json` | `idle`, `cycle` (30 f) | `iron3`, `iron5`, `cast-iron1` | same four |
| Shaft and bevel | `.../shaft.json` | none | `cast-iron1` | `Cube2` |
| Bevel gear (block part) | `.../bevelgear.json` | none | `cast-iron1` | `HubS` - authored facing south |
| Transmission x2 / x4 | `.../transmission-x2.json`, `-x4.json` | `idle`, `cycle` (30 f) | `plain`, `iron5`, `cast-iron1` | Base · MainShafts · SupportShaft |
| Transmission clutch | `.../transmission-clutch.json` | `connected`, `disconnected`, `mainshaft1cycle`, `mainshaft2cycle`, `sideshaftcycle` (30 f) | `iron5`, `cast-iron1` | Base · MainShafts · SupportShaft |
| Bevel-gear item | `iiex:item/gearbevel` (`assets/iiex/shapes/item/gearbevel.json`) | - | - | - |

Every clip is authored as one revolution of its reference shaft; the playback convention that depends on
that is owned by [mp-energy](../mechanics/mp-energy.md).

**Animation wiring**

| Piece | Animator | Where |
|---|---|---|
| Flywheel | `ToggleAnimator` + `.EntityBehavior("Animatable")` | `BlockFlywheel.cs:111`, `BlockEntityFlywheel.cs:115-121`, `ApplySpin` at `:151-188` |
| Transmission | `ConstructedAnimator` (RCC-suppressed mesh) | `BlockTransmission.cs:66`, `BlockEntityTransmission.cs:83-84`, `ApplyPose` at `:115-120` |
| Shaft | none | - |
| Bevel | none - static composed mesh | `BlockEntityCastIronBevel.cs:54-80` |

The shaft run is visually dead. The disc at one end spins and the gear train at the other end spins;
everything between them stands still, because `shaft.json` and `bevelgear.json` carry no clips and neither
block declares `Animatable`. `EnergyAnim.BranchSpinSign` - the pure function that says which way a bevel
branch turns - is therefore computed, tested, and never rendered.

**Editable sources - all six are retired, and one is an orphan**

| Retired (`git status` ` D`) | Replaced by (untracked `??`) |
|---|---|
| `mp-castiron-flywheel.json`, `-flywheel-large.json` | `mp-megablock-flywheel.json`, `mp-megablock-flywheellarge.json` |
| `mp-castiron-gear-bevel.json` | `mp-block-gearbevel.json` |
| `mp-castiron-gear-transmission{x2,x4,clutch}.json` | `mp-megablock-geartransmission{x2,x4,clutch}.json` |
| `mp-castiron-axle.json`, `mp-castiron-axlesupport.json` | `mp-block-shaft.json`, `mp-block-shaftsupport.json` |

The shipped runtime shapes were exported from the deleted sources, so re-exporting any of them needs the
new files wired first. `mp-block-shaftsupport.json` is a drawn block with no code at all: `grep -i
shaftsupport src/` finds nothing. Either build it or drop the art.

---

## Construction

Grid recipes for the family live in `Recipes/Grid/EnergyRecipeDefinitions.cs`:

| Output | Recipe | Note |
|---|---|---|
| `iiex:spurgear` ×1 | chisel + 2 × `game:ingot-iron` (`:43-50`) | the bootstrap route - wasteful on purpose |
| `iiex:spurgear` ×2 | chisel + 1 × `iiex:ingot-castiron` (`:52-59`) | the cupola route - cast iron is the tier's cheap bulk metal |
| flywheel (normal) | diagram (tool) + 4 `castwheelsection` + 4 `castplate-heavy` + 1 spurgear (`:91-101`) | costed in mass |
| flywheel (large) | diagram (tool) + 8 `castwheelsection` + a normal flywheel (`:105-113`) | upgraded in place, not built from scratch |
| shaft ×2 | hammer + rod + plate (`:124-132`) | the cheapest thing on the network by design |
| transmission housing (x2) | hammer + 4 plate (`:135-142`) | the bare RCC base; the real cost is in the stages |
| transmission housing (x4 / clutch) | an x2 housing + 2 plate / 2 rod (`:144-159`) | the same housing re-cased, then built up by its own stages |

Both flywheel grids are diagram-led flat material lists - the plan plus its bill of materials, one cell
per distinct ingredient ([diagram-crafting](../mechanics/diagram-crafting.md)); each size has its own
diagram (a 5×5×2 wheel is a different drawing from a 3×3×1 one).

The bevel is not crafted: it is made in world by using an `iiex:bevelgear` on a placed shaft, which is
also why its blocktype carries no creative entry and is `HandbookExclude()`d
(`BlockCastIronBevel.cs:45`). The bevel-gear item has no recipe - its definition
(`BevelGearItemDefinitions.cs:14-24`) is complete but sourceless: consumed to make a bevel, recovered when
one is broken, and nothing else creates it.

A placed transmission housing is completed as a three-stage RightClickConstructable
(`BlockTransmission.cs:67-80`):

| Stage | Requires | Adds shape elements | Lang key |
|---|---|---|---|
| 1 | - | `Base` | - |
| 2 | `iiex:spurgear` × 2 · `game:ingot-iron` × 4 | `MainShafts` | `iiex:rcc-ingredient-transmissiongears`, `…shafts` |
| 3 | `game:ingot-iron` × 2 | `SupportShaft` | `iiex:rcc-ingredient-transmissionsupport` |

Total 2 spur gears + 6 iron ingots on top of the housing. The stage gears are iiex's own cast gear;
reaching upward for `iiex:gear-iron` makes the block unbuildable for an iiex-only player, and the placement
rule forbids it (`BlockTransmission.cs:70-72`). `ShapeSelectiveElements("Base/*")` (`:87`) is what makes
the unbuilt block show only its base. `NoDrops()` (`:52`) plus an empty `GetDrops` means the block itself
never drops - the RCC scatters the construction materials instead.

---

## Operation

### Flywheel

Place it on a clear 3 × 3 × 1 (or 5 × 5 × 2) volume, couple a vanilla axle to the hub cell, and the
bridge feeds the run. There are no verbs - no clicks, no inventory, no configuration. The wheel is the
gauge: the disc turns at the run's speed, so its visible rate is the reservoir's charge
(`BlockEntityFlywheel.cs:151-188`), and looking at it prints charge / speed / supply / draw
(`:284-310`). The block-info values, the throttled sync that carries them to clients, and what each means are
owned by [mp-energy](../mechanics/mp-energy.md).

Both sizes are `stackSize 1` and `SolidNonOpaque` - the disc is authored larger than its cell, so the placed
cell must stay solid without culling its neighbours' faces (`BlockFlywheel.cs:103`, `:126`).

### Shaft → bevel

| Held | On | Result |
|---|---|---|
| `iiex:bevelgear` | a `mpenergy-shaft` | consumes one gear, swaps the cell for a bevel of the same axis (`BlockCastIronShaft.cs:62-88`) |
| anything else | a shaft | falls through to the base behaviour |

There is no face or support to pick: the branches follow whatever perpendicular shafts are then placed
against it, and a neighbour that is already adjacent connects immediately, because the bevel's connectors
are unconditional.

### Transmission

Build the three stages, then - on the clutch variant only - right-click the quarter-block lever cell to
engage or disengage (`BlockEntityTransmission.ToggleEngaged`, `:99-106`). `x2` and `x4` are always coupled
and have no verbs at all. A disengaged clutch leaves the two runs fully independent, and shows it: each main
shaft animates from its own side, so one visibly turns while the other stands still
(`:146-175`). The coupling shaft's clip runs only while engaged, because the disengaged lever pose slides it
out of mesh.

The lever hint is shown only on a built clutch's lever cell (`BlockTransmission.cs:185-213`).

---

## Numbers

Every simulation constant - inertias, bridge torque, friction, ω_max, mesh loss, ratios, tick intervals, sync
thresholds, hub coordinates - is owned by [mp-energy](../mechanics/mp-energy.md) § Numbers. What follows is
the block half only.

### Flywheel — `BlockFlywheel.cs`

| Key | Value | file:line | What it does |
|---|---|---|---|
| footprint, normal | 3 × 3 × 1 = 9 cells (8 fillers) | `:52-64` | `Face` grid, `Origin(-1, 2)` |
| footprint, large | 5 × 5 × 2 = 50 cells (49 fillers) | `:68-92` | two `Face` grids, `Origin(-2, 4)` |
| hub behaviours | `exlib.BEBehaviorMPFillerPort` ×2 (north + south) per hub | `:45-48` | hard-coded `FillerBehaviorSpec` statics |
| `StructureAngle` | `ns` → 0, `we` → 90 | `:144` | rotates footprint and shape by the same angle |
| `maxStackSize` | `1` | `:103` | |
| entity behaviour | `Animatable` | `:111` | required or the posed mesh drops to the static shape |
| render | `SolidNonOpaque` | `:126` | the disc overhangs its cell |

### Shaft / bevel — `BlockCastIronShaft.cs`, `BlockCastIronBevel.cs`

| Key | Value | file:line |
|---|---|---|
| orientations | `ns`, `we`, `ud` (both blocks) | `:40`, `:49` |
| `maxStackSize` | `64` (both) | `:37`, `:41` |
| collision / selection | `0.3125, 0.3125, 0 → 0.6875, 0.6875, 1` (both) | `:46-47`, `:53-54` |
| `sideSolid` / `sideOpaque` | `false` / `false` (both) | `:48-49`, `:55-56` |
| handbook | shaft grouped `mpenergy-shaft-*`; bevel excluded | `:38`, `:45` |
| gear rotation table | south = identity; N `π`; E `π/2`; W `3π/2`; U `−π/2`; D `+π/2` | `EnergyMeshes.cs:30-40` - hard-coded, pure, pinned |
| shaft / gear shape refs | `iiex:shapes/mpenergy/shaft.json`, `…/bevelgear.json` | `EnergyMeshes.cs:16-21` - hard-coded `AssetLocation` statics |

### Bevel-gear item — `BevelGearItemDefinitions.cs`

| Key | Value | file:line | Note |
|---|---|---|---|
| `GearUnits` | `40` | `:12` | hard-coded `private const`; the cast iron a gear is worth for remelt |
| shape | `iiex:item/gearbevel` | `:18` | |
| `maxStackSize` | `16` | `:19` | |
| `materialDensity` | `7200` | `:20` | cast iron, not the 7800/7870 used for wrought/steel elsewhere |
| `combustibleProps.meltingPoint` | `1150` | `:21` | cast iron melts well below wrought |

### Transmission — `BlockTransmission.cs`

| Key | Value | file:line |
|---|---|---|
| types × sides | `x2`, `x4`, `clutch` × N/E/S/W | `:34-35`, `:79-80` |
| footprint | 3 fillers: `(1,0,0)`, `(0,1,0)`, `(1,1,0)` | `:57-63` - hard-coded `FillerCellSpec` literals |
| lever cell | `(1,1,0)`, rotated by `StructureAngle` | `BlockEntityTransmission.cs:91-95` - hard-coded |
| `miningTier` / `resistance` | `0` / `4.5` | `:46-47` |
| `maxStackSize` | `1` | `:48` |
| drops | `NoDrops()` + empty `GetDrops` | `:52`, `:220-225` |
| RCC cost | 2 × `iiex:spurgear`, 6 × `game:ingot-iron` | `:67-80` |
| selective elements | `Base/*` | `:87` |
| collision / selection | full cube | `:89-90` |
| `sideSolid` / `sideOpaque` | `false` / `false` | `:91-92` |
| `StructureAngle` | `ExOrientation.AngleFromSide(Variant["side"])` | `:109` |

---

## Drops

| Broken | Returns |
|---|---|
| Flywheel | the wheel (default `BlockNetworkNode` drop, `stackSize 1`). Fillers are cleared first so none is orphaned (`BlockFlywheel.cs:179-190`) |
| Shaft | the shaft (default drop, `stackSize 64`) |
| Bevel | the shaft it was made from + one `iiex:bevelgear` - an explicit `GetDrops` override, so the conversion is fully reversible (`BlockCastIronBevel.cs:118-135`). The shaft variant is reconstructed from `Orientation`, defaulting to `ns` |
| Transmission | nothing from the block. The RCC behaviour scatters whatever construction materials went in (`BlockTransmission.cs:52`, `:219-225`) |
| A transmission filler | routes to the principal, per the shared filler system ([multiblock](../mechanics/multiblock.md)) |

---

## Code

| Type / member | file:line | Role |
|---|---|---|
| `BlockFlywheel` | `BlockNetworkEnergy/Blocks/BlockFlywheel.cs:33` | `BlockNetworkNode` + `IExBlockDefProvider` + `IFillerHost` |
| `.Definitions` | `:97-127` | two sizes × two orientations; per-size `FillerOffsetsByType` |
| `.CanPlaceBlock` / `.OnBlockPlaced` / `.OnBlockBroken` | `:149-190` | volume-clear gate, filler placement, filler clearing before the base break |
| `BlockEntityFlywheel` | `BlockEntities/BlockEntityFlywheel.cs:34` | storage + producer + direction |
| `.ApplySpin` | `:151-188` | holds exactly one clip - `cycle` while turning, `idle` at rest |
| `.OnExchanged` | `:192-199` | wrench rotation keeps the BE, so the animator is rebuilt and re-posed |
| `.GetBlockInfo` | `:284-310` | charge / speed / supply / draw |
| `BlockCastIronShaft` | `Blocks/BlockCastIronShaft.cs:19` | straight segment; `OnBlockInteractStart` at `:62-88` is the bevel conversion |
| `BlockEntityCastIronShaft` | `BlockEntities/BlockEntityCastIronShaft.cs:14` | pass-through node + small buffer |
| `BlockCastIronBevel` | `Blocks/BlockCastIronBevel.cs:21` | `HasConnectorAt => true` (`:65-69`); `IsPerpendicular` (`:77-78`); `HasConnectedNeighbor` (`:82-92`); `GetDrops` (`:118-135`) |
| `BlockEntityCastIronBevel` | `BlockEntities/BlockEntityCastIronBevel.cs:19` | `GearedFaces` (`:23-37`); `OnTesselation` composes body + per-face gears (`:54-80`); `OnExchanged` drops the caches (`:84-89`) |
| `EnergyMeshes` | `BlockNetworkEnergy/EnergyMeshes.cs:14` | `GearRotation` (`:30-40`), `TesselateShaft` (`:44-57`), `TesselateGear` (`:62-78`) |
| `BlockTransmission` | `Blocks/BlockTransmission.cs:29` | `BlockFilledMegastructure` + `IFillerInteractionTarget`; `HandleInteract` at `:116-136` |
| `BlockEntityTransmission` | `BlockEntities/BlockEntityTransmission.cs:35` | `BlockEntityProductionMachine`; `IsLeverCell` (`:91-95`), `ToggleEngaged` (`:99-106`), `UpdateSpin` (`:146-175`) |
| `BevelGearItemDefinitions` | `Items/BevelGearItemDefinitions.cs:10` | the gear item |
| `EnergyAnim` | `BlockNetworkEnergy/EnergyAnim.cs:10` | pure motion conventions - owned by [mp-energy](../mechanics/mp-energy.md) |

### Where a caller hooks in

To put a machine on the run, or to add a producer or storage node, follow
[mp-energy](../mechanics/mp-energy.md) § Where a caller hooks in; the contracts are that page's, not
this one's.

What belongs here: a machine that needs to bridge vanilla MP copies the flywheel's hub pattern - a
footprint cell declared with `Host('M', new FillerBehaviorSpec("exlib.BEBehaviorMPFillerPort", "<facing>"), …)`
(`BlockFlywheel.cs:45-48`, `:52-64`), then read `port.Speed` back off that cell in the BE. The twin-tub
blower uses the same idiom.

A machine that must span cells and still conduct needs dedicated invisible node blocks, not fillers -
the rolling mill's axle cells ([rolling mill](rolling-mill.md), [multiblock](../mechanics/multiblock.md)).

### Tests

`test/IronIndustryExpanded.Tests/Blocks/Energy/` - six files, 45 methods: `FlywheelTests` (13),
`CastIronBevelTests` (10), `TransmissionTests` (9), `EnergyAnimTests` (6), `CastIronShaftTests` (4),
`EnergyMeshesTests` (3). The network model itself is pinned in
`test/ExpandedLib.Tests/Networks/MpEnergyNetworkStateTests.cs` and `…TickTests.cs`.

---

## Gotchas

- The bevel-gear item has no recipe - the one uncraftable piece. A survival run can climb and turn a
  corner only with a creative-given gear or one salvaged from an existing bevel.
- The shaft and bevel never animate, so a run reads as motionless between the flywheel and the gearbox,
  and `EnergyAnim.BranchSpinSign` renders nothing. See [Assets](#assets).
- `mp-block-shaftsupport.json` is drawn and has no code. Also, both retired axle sources
  (`mp-castiron-axle*.json`) are deleted while the runtime `shaft.json` was exported from them.
- A bevel cannot be turned back into a shaft in place. The gesture is one-way; breaking it returns the
  parts. The interaction help does not say so.
- `BlockCastIronBevel.GetDrops` defaults `Orientation` to `"ns"` (`:127`). A bevel whose orientation
  failed to resolve silently drops the wrong shaft variant.
- None of the balance numbers has been playtested under a survival build cost - the recipes are new and
  the mass-priced flywheel (3040 u) has not yet been earned in play.
- `IsPerpendicular` is a substring test on the orientation string - `!orientation.Contains(face.Code[0])`
  (`BlockCastIronBevel.cs:77-78`). It works because `ns`/`we`/`ud` and the face codes share first letters,
  but it is a stringly-typed axis check one rename away from breaking with no compile error.
- The bevel's per-face gear mesh cache is never invalidated by anything except `OnExchanged`
  (`BlockEntityCastIronBevel.cs:46`, `:84-89`). Correct today (the geometry per face is fixed), but a
  texture or shape reload will not reach it.
- The clutch has no readout beyond the lever pose. Owned as an open question by
  [mp-energy](../mechanics/mp-energy.md); the lever cell is a quarter-block filler and easy to miss when
  looking at the machine.
- `BlockFlywheel.cs:28-29` says "The spin animation and any producers that spend the stored energy are
  follow-ups; until one lands the run simply sits idle." Both landed. Stale class doc.
- `BlockEntityFlywheel.cs:55` says "The shaft speed is unused for now" and the parameter is genuinely
  ignored. True, and flagged as open by [mp-energy](../mechanics/mp-energy.md); the method's signature
  implies a droop curve that does not exist.
- The flywheel's `Inertia` and the bridge's torque read config live, so a `/exmod config` retune takes
  effect on the next tick with no rebuild - but the wheel's capacity is derived, so retuning inertia
  silently changes how much energy a charged run is holding.

---

## Open

- A recipe for the bevel-gear item - the last uncraftable piece of the family.
- A shaft support block - the art is drawn (`mp-block-shaftsupport.json`), the block does not exist. A
  long horizontal run currently floats.
- Shaft and bevel animation. A `cycle` clip on `shaft.json` and `bevelgear.json` plus `Animatable` on
  both blocks would make `BranchSpinSign` visible and the run legible end to end.
- Nothing justifies the large wheel or the ratios yet. One consumer exists and it is blocked. The
  settled forming line adds three more mpenergy stations - the shear, the nail machine and the rivet
  machine, all 1 × 1 benches - plus a six-stand mill train on one shared drive shaft; those are what
  turn the ratios and the large wheel into decisions.
- The shared-shaft train is the first real multi-consumer test. Six stands idling on one line plus one
  under load is a sustained draw a single bridged waterwheel will not carry, which is the case for a
  bigger prime mover, and it arrives when iiex hands one over. Nothing in the current model has been
  exercised against more than one load.
- Re-export the runtime shapes from the new editable sources, then delete the retired ones from the
  index.
- Framework-level gaps - pulsed supply, bridge speed droop, driving back into vanilla MP, the governor and
  over-speed burst - are owned by [mp-energy](../mechanics/mp-energy.md) § Open.
