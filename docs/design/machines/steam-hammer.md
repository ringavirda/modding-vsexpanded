# Steam hammer

**Status** designed - nothing built. No block, no block entity, no die item, no recipe, no lang key, no
runtime shape. A repo-wide grep for `hammer` in `src/` returns only vanilla tool items (`item-hammer-iron`,
`item-helvehammer-*`). Three editable shapes are drawn.
**Mod** iiex (`IronIndustryExpanded`)

## Owns

* the machine: its sparse megablock footprint, the single-action LP ram (steam lifts, gravity drops), the
  two network faces, the lever cell, and the four-state animation machine;
* the docked anvil as a separate block, the docking gesture, and the rule that a die-set is one item
  rendering both faces;
* the staged work-item renderer, and the reason vanilla's `BlockAnvil` renderer is refused;
* the machine's half of its exactly two jobs - the lever-pull read moment for
  [shingling](../processes/shingling.md) and the die mount for [stamping](../processes/stamping.md); the
  processes themselves are those pages';
* the die catalogue for this machine and the output-thickness tier gate (LP ≤ 1 voxel, HP ≥ 2);
* the measured geometry of the three drawn shapes, their animation clips, their texture state and what
  must happen to them.

## Does not own - cited only, never restated

| Fact | Owner |
|---|---|
| shearing, the crop, `Outputs` / `OutputAt`, the cold-cut torque gate, and the shear-cuts-across / die-cuts-out split as a rule | [shear](shear.md) |
| the shingling process - the pile-picks-the-form rule, the 2/6-ball thresholds, the remainder rule, the helve split | [shingling](../processes/shingling.md) |
| the stamping process - the `boilerplate` → 3-plate yield, the blanking-not-shearing arithmetic, the operations ledger | [stamping](../processes/stamping.md) |
| the puddling heat, the wrought ball, and the shingled-stock ladder's masses | [puddling](../processes/puddling.md), [shingling](../processes/shingling.md) |
| `1 vx³ = 2.5 u` and every mass derived from it | [density rule](../mechanics/density-rule.md) |
| the wide stock the stamping die eats, and the train that rolls it | [wide hall](wide-hall.md), [rolling mill](rolling-mill.md) |
| where LP steam comes from, its pressure band, burst, `LitresPerPipe`, valves | [pipe network](../mechanics/pipe-network.md) |
| the filler footprint system, behaviour-capable filler cells, interaction rerouting | [multiblock & fillers](../mechanics/multiblock.md) |
| code-first defs, RCC construction stages, the recipe-cost catalogue, goldens | [recipes & config](../mechanics/recipes-config.md) |
| the ≤ 32 / ≤ 48 handling limits and the soft-lock definition | [recoverability](../mechanics/recoverability.md) |
| the placement rule and the fastener split that put this machine in iiex | [STATE.md](../../internal/plans/STATE.md) |
| the cast-vs-forged rule that makes dies forged and the frame cast | [casting](../processes/casting.md) |

**Depends on** [pipe network](../mechanics/pipe-network.md) · [multiblock & fillers](../mechanics/multiblock.md) ·
[recipes & config](../mechanics/recipes-config.md) · [density rule](../mechanics/density-rule.md) ·
[shear](shear.md) · [wide hall](wide-hall.md) · [rolling mill](rolling-mill.md) ·
[reheat furnace](reheat-furnace.md) · [STATE.md § placement rule](../../internal/plans/STATE.md)

---

## Role

Two jobs, each a process with its own page; this page owns the machine that runs them.

| Job | Why it needs this machine | Owner |
|---|---|---|
| Shingling wrought slabs | the 6-ball slab is a pile no helve can lift; without the hammer the wide route has no wrought stock at all | [shingling](../processes/shingling.md) |
| Stamping / blanking | the path from wide plate into the thing everything consumes | [stamping](../processes/stamping.md) |

Nasmyth built the hammer in 1839 to forge a paddle shaft, so shingling is its period-correct job.

It is iiex because of what it feeds, not what it is made of: its frame is cast iron and its dies are
wrought/steel, both iiex materials, but its products are the shingled slab and the stamped plate, the wide
route's inputs and outputs. The placement rule sends a machine to the mod that consumes it
([STATE.md](../../internal/plans/STATE.md), row *steam hammer + stamping dies → iiex*).

### The hammer never shears

The rule - the shear cuts across, the die cuts out - belongs to the [shear](shear.md), and the
arithmetic that makes plate-blanking a die job rather than a crop is
[stamping](../processes/stamping.md) § Why it is blanking and not shearing. The consequence for the machine:
no shear operation may ever be listed on it.

---

## Structure

A sparse megablock: 3 cells wide × 3 cells tall, with a full 3 × 3 base course. Standard megablock +
invisible-filler pattern - the fillers give per-cell collision, the top-centre block owns every behaviour
([multiblock & fillers](../mechanics/multiblock.md)).

| Layer | Cells | Note |
|---|---|---|
| `y = 0` | full 3 × 3 in plan | the foundation the standards stand on and the anvil docks beside |
| `y = 1` | vslab - block - vslab | the two frame standards with the ram between them |
| `y = 2` | a single centre block | the principal: carries the cylinder and both network faces |

| Cell | Role |
|---|---|
| top-centre (principal) | the BE; owns the steam draw, the blow loop and the anvil `ItemStack` |
| principal west face | LP-steam inlet - a pipe connector reading the adjacent cell; steam flowing ⇒ powered |
| principal east face | the spent-steam vent ("depleted out"); exhaust particles spawn here on each blow |
| the lever cell | a behaviour-capable filler routing the held right-click to the principal's BE |
| the anvil cell | not part of the megablock - a separate docked block, see below |

### The anvil is a separate docked block

The canal-tap / molten-barrel idiom, and a maintainer's call rather than a derived one:

* the hammer BE stores an anvil `ItemStack` and tesselates its mesh at the anvil cell in `OnTesselation`;
* sneak + RMB docks / undocks it, carrying any installed die out with it, as a mold carries its contents;
* payoff: anvil variety for free, and a customisation hook that costs no extra machine.

It is not a vanilla `BlockAnvil` variant: that class carries the free-sculpt hand-smithing voxel mechanic,
which a deterministic die machine does not want. It is its own block - a die/sow block that may share the
anvil look, not its class.

### Slot layout

| Slot | Holds |
|---|---|
| ram (`HammerDie` element) | the top half of the fitted die-set |
| docked anvil (`Die` element) | the bottom half |
| between them | the work item |

One "die-set" item that renders in both faces, rather than two independent top/bottom items, so they cannot
be mismatched. The drawn art already commits to this: `item-steamhammerdie-flat.json` contains `DieUp`, a
`Buffer` and `DieBottom` in one shape.

---

## Assets

All three shapes are drawn and wired to nothing.

| Asset | Path | State |
|---|---|---|
| Hammer (editable) | `workbench/shapes/machine-pipe-megablock-steamhammer.json` | drawn, unwired |
| Anvil (editable) | `workbench/shapes/machine-block-steamhammeranvil.json` | drawn, unwired |
| Die-set, flat (editable) | `workbench/shapes/item-steamhammerdie-flat.json` | drawn, unwired |
| Runtime shapes | `mods/iiex/assets/iiex/shapes/…` | none. `mods/iiex/assets/iiex/shapes/` holds only `boiler/`, `engine/`, `pipes/` |
| Handbook page | `mods/iiex/docs/handbook/` | none |
| Lang keys | `mods/iiex/assets/iiex/lang/en.json` | none |

### What the hammer shape actually draws

Measured off the file, rotations applied. Coordinates are Blockbench voxels; one cell = 16.

| Quantity | Value |
|---|---|
| Bounding box | x −12.6 … 25.0, y 0 … 57.0, z 0 … 13.0 |
| Span (w × h × d) | 37.6 × 57.0 × 13.0 voxels |
| Textures | `iron3` (riveted iron), `cast-iron1`, `iron5` (sheet-plain) |

| Top-level group | What it is |
|---|---|
| `Supports` | two identical frame standards - a base plate, a 4 × 12 × 4 column, side webs and feet. The second subtree is the first mirrored by `rotationY: −180`, so the pair face each other across the ram at x ≈ −8…−1 and x ≈ 17…25 |
| `Cylinder` | the entablature and cylinder assembly across the top of both standards, at y 28 … 45 |
| `Cylinder/…/SteamIntake` | the named steam inlet, x 0 … 4, y 37 … 38 - the west face of the top-centre cell |
| `Cylinder/…/DepletedOut` | the named exhaust boss - the east-face vent |
| `Cylinder/…/ControlCylinder` | the valve chest the control rod works |
| `HammerGroup/Rod` | the ram assembly's suspension element, x 7 … 9, y 31 … 47 - the element the animations drive; the whole ram hangs from it |
| `HammerGroup/Rod/HammerMass` | the tup, 8 × 8 × 4 at y 23 … 31 |
| `HammerGroup/Rod/HammerMass/HammerDie` | the top die seat, 6 × 2 × 4 |
| `HammerGroup/Rod/Piston` | the piston disc inside the cylinder, y 44 … 45 |
| `ControlLeaver/ControlRod` | the vertical rod from the lever up to the valve chest |
| `ControlLeaver/HandLeaver` | the hand lever, drawn at `rotationZ: −22.5` and reaching out to x = −12.6, i.e. into the west neighbour cell |

### Animation clips (authored, all 30 frames)

| Clip | Keyframes | Intended state |
|---|---|---|
| `idle` | 1 | no steam; ram parked at the bottom |
| `steamup` | 1 | steam present; ram raised and held |
| `leaverdown` | 1 | the lever pulled |
| `hammerhit` | 2 | the blow, looped while the lever is held |

Caution: all four are authored `onAnimationEnd: EaseOut`. A looping blow must be `Repeat` or the mesh
vanishes - see the cycle-anim rule under [Gotchas](#gotchas).

### What the anvil and die shapes draw

| Shape | Bounding box | Elements |
|---|---|---|
| `machine-block-steamhammeranvil.json` | x 2 … 14, y 0 … 10, z 4 … 12 - fits one cell | `Base` (12 × 1 × 8 plate), the 10 × 5 × 6 anvil body on two feet, and a `Die` group already drawn in place on top |
| `item-steamhammerdie-flat.json` | x 3 … 13, y 0 … 8, z 4 … 12 | `DieUp` (the ram half) · `Buffer` (two guide rails) · `DieBottom` (the anvil half, geometrically identical to the anvil shape's `Die`) |

Caution: the anvil shape draws a die that is already fitted. Either the anvil is permanently die-bearing
(wrong - the die-set is removable tooling) or the `Die` subtree must become a `ShapeSelectiveElements`
toggle. Match per path-segment, and `/*` is what keeps a subtree.

Caution: both editable shapes carry absolute `F:/repos/…` and `F:/…/.game/1.22/…` texture paths (`iron3`,
`cast-iron1`, `iron5` on the anvil). Those are Blockbench working paths; the runtime shape must use
domain-relative references, and `editable/` is source-only by convention.

---

## Construction

There is no recipe, no RCC and no block to hang either on. This is a blocker: nothing about this machine
is reachable in survival or creative today.

The materials are settled, the bill is not (the cast-vs-forged rule is
[casting](../processes/casting.md)'s):

| Part | Material | Why |
|---|---|---|
| frame standards · hammer mass · anvil block | cast iron | compression members. Built from `castplate-heavy` - the heavy plate's structural third job alongside the puddling hearth and machining stock |
| piston · rod · lever | steel / wrought (`iron5`) | tension and reversal; a cast rod snaps on the first stroke |
| dies | forged, never cast | a die takes the blow and imparts the profile, so it is tough, not brittle |

Two die tiers, mirroring the boring-machine bit tiers: plain steel for soft stock, quench-hardened for
stamping hadfield or HSS.

It should be an RCC megablock with construction stages like every other machine of its size, and the
dual normal-plate-or-`boilerplate` ingredient path already exists for this kind of bill
([recipes & config](../mechanics/recipes-config.md)).

---

## Operation

### Verbs

| Held / gesture | Where | Result |
|---|---|---|
| hold RMB | the lever cell | run the blow loop: `leaverdown` + looped `hammerhit`, sparks, spent steam, one piston-stroke sound per cycle. Release ⇒ ease back to `steamup` |
| sneak + RMB | the anvil cell | dock / undock the anvil, carrying its installed die with it |
| a die-set item | the ram or the anvil | fit the tooling; the die selects the operation |
| balls / stock, RMB | the anvil | pile the work on the bottom die |

### The four states

| State | Condition | Ram |
|---|---|---|
| `idle` | no steam at the inlet | parked at the bottom |
| `steamup` | steam present | raised and held |
| operating | lever held | blow loop |
| - | - | (no fault state is designed; see [Open](#open)) |

Code-driven, nothing hand-synced.

### Job 1 - shingling

The process is [shingling](../processes/shingling.md)'s. The machine half: the hammer acts only when the
lever is pulled, and that discreteness gives it a moment to read the anvil - it takes the pile as it stands
at the first pull, forms the largest form it affords, and leaves the remainder. The 6-ball `shingledslab` is
a pile no helve can lift, so it is this machine's alone. The hammer only consolidates; it never sets the
section - the mill forms it.

### Job 2 - stamping

The process is [stamping](../processes/stamping.md)'s. The machine half is the die mount: batch size = die
cavity count, read off the die mesh, which is why the die mesh, not a config number, is the extension point.

### The visible work item - its own renderer

Vanilla's anvil renderer is welded to the free-sculpt smithing system (`ItemWorkItem` + voxel grid + per-blow
player choice). A die machine is deterministic (stock + die → output), so grafting it fights the design.
Build the render with the mod's own BE-renderer toolkit, as `MoltenRenderer` / the barrel content mesh / the
held-mold surface already do:

| Option | Shape |
|---|---|
| Staged / morphing stock (preferred) | the docked work item sits on the bottom die glowing off its own temperature; each `hammerhit` advances it through a few forge stages (raw → half-formed → the die's profile) with sparks and an impact flash |
| True moving voxels (possible, more work) | a small voxel grid on the BE, carved toward the die's target each blow. Closer to vanilla's look, but it slightly implies the shape is sculptable. Reach for it only if the staged version does not sell in game |

---

## Numbers

Nothing here is in config yet. Every row is a proposed value.

### Material flow

Every mass, threshold and yield in the hammer's two jobs is owned elsewhere and cited, never restated here:
the ball, the pile thresholds and the shingled forms by [shingling](../processes/shingling.md); the puddling
heat by [puddling](../processes/puddling.md); the `boilerplate` → 3-plate yield and its exactness by
[stamping](../processes/stamping.md); the feeds-per-plate trade by [rolling](../processes/rolling.md); the
underlying masses by the [density rule](../mechanics/density-rule.md).

### Tier gate - settled

| Rule | Value |
|---|---|
| die output thickness ≤ 1 voxel (strips, nails, thin brackets) | LP single-action - this machine |
| die output thickness ≥ 2 voxels (tools, complex/thick forgings) | HP double-action - hpex |
| batch size | = die cavity count, read off the die mesh |
| hardened dies required for | hadfield · HSS |

### Unchosen - every one of these is an open number

| Quantity | Status |
|---|---|
| steam draw (L/s) at the inlet | not chosen. The hammer is a pipe consumer, not an engine sub-machine, so it needs its own draw - compare the Watt engine's fixed 30 L/s ([engine-watt](engine-watt.md)) |
| minimum inlet pressure to raise the ram | not chosen. Must sit under the Cornish boiler's 5 atm choke and above whatever a valve gates ([boiler-cornish](boiler-cornish.md), [pipe network](../mechanics/pipe-network.md)) |
| blow interval (s) and steam per blow | not chosen |
| exhaust vent rate | not chosen |
| shingling blows per form | not chosen - the reference behaviour is a vanilla smithing recipe |
| die durability, if any | not designed |

---

## Drops

Not designed. What the pattern demands, by analogy with machines that already do it:

| Broken | Should return |
|---|---|
| the principal | the hammer, plus the fitted die-set, plus anything piled on the anvil |
| a filler (incl. the lever cell) | routes to the principal - [multiblock & fillers](../mechanics/multiblock.md) |
| the docked anvil | itself, carrying its installed die out with it - the mold-carries-its-contents idiom |

Nothing may be destroyed on break. Compare the rolling mill, which returns the stuck piece and the fitted
roll set from `OnBlockBroken` ([rolling mill](rolling-mill.md)).

---

## Code - where it will hook in

Nothing exists. Every row below is a plan, anchored to the class it must copy.

| To build | Copy from | file:line |
|---|---|---|
| the block | any pipe-network megablock principal; the shape wants the pipe-consumer idiom | `IronIndustryExpanded/BlockNetworkPipe/…` |
| the two network faces | machine ports are `INetworkConnector`s reading the adjacent cell | [pipe network](../mechanics/pipe-network.md) |
| the footprint | ASCII layout DSL + `FillerOffsets`, as the mill does | `BlockRollingMill.cs:74-94` |
| the lever cell | a behaviour-capable filler forwarding the held interaction | [multiblock & fillers](../mechanics/multiblock.md) |
| tooling fitted from the hand | `TryFitRollSet` / `FitRollSet` - the exact gesture and its refusal path | `BlockRollingMill.cs:293-320`, `BlockEntityRollingMill.cs:144-154` |
| the die spec record | `RollSetSpec` is the template; the die contract itself is the [heading machine](heading-machine.md)'s `ItemDie` | `RollSetSpec.cs:31`, `:108-201` |
| a load-time validity sweep for dies | `RollSetValidation.Validate` on `AssetsFinalize` | `RollSetValidation.cs:20-32` |
| the animator | `.EntityBehavior("Animatable")` + a toggle animator, as the flywheel and transmission do | [flywheel & shafting](flywheel-and-shafting.md) |
| the staged work-item renderer | `MoltenRenderer`, the barrel content mesh, the held-mold surface | [molten network](../mechanics/molten-network.md) |
| the code-first def + RCC stages | `ExBlockDef` / `ConstructionStages` | [recipes & config](../mechanics/recipes-config.md) |

**Build order when scheduled:**

1. megablock shell + LP-steam-driven ram (`idle` → `steamup` → lever-hold cycle);
2. docked-anvil socket + die-set items;
3. the staged work-item renderer;
4. pile-reads-form shingling (≥ 6 balls → slab);
5. the stamping die (`boilerplate` → 3 × `game:metalplate`), in the `rollset` / `pattern` tooling idiom;
6. the rivet die - iiex's one entry in the [heading machine](heading-machine.md)'s die catalogue. The
   rivet die goes on the heading machine, not on this one ([STATE.md](../../internal/plans/STATE.md)).

---

## Gotchas

* The drawn mesh does not fit the declared footprint. It measures 37.6 wide × 57.0 tall × 13.0 deep.
  A 3-cell-tall footprint is 48 voxels, so the cylinder and the ram's guide rod stand 9 voxels proud of the
  top cell; the `HandLeaver` reaches to x = −12.6, into the west neighbour. Overhang is legal
  (`SolidNonOpaque` keeps neighbour faces from culling, as the mill does at `BlockRollingMill.cs:61-63`)
  but the footprint must be chosen knowing it, and the drawn depth of 13 does not use the base course's
  full 3-cell plan at all.
* The anvil shape draws its bottom die already fitted - needs `ShapeSelectiveElements`, matched per
  path-segment.
* All four animation clips are `onAnimationEnd: EaseOut`. A running/looping clip must be `Repeat` or the
  mesh disappears. `hammerhit` is the loop.
* The animator must be re-initialised in `OnExchanged` if the block is ever wrench-rotatable, and
  `_animatorReady` must not be set unconditionally or `GetBlockInfo` NREs on the server.
* `HammerGroup/Rod` is the animated element and the ram hangs off it. The die, the mass and the piston
  are all its descendants, so the animation must drive `Rod`, not `HammerMass`.
* A megablock animator samples light from one cell. Sample the body cell, not the firebox-equivalent.
* Do not subclass a vanilla structure block for the anvil. `BlockAnvil` carries the sculpt mechanic;
  vanilla `BeeHiveKilnDoor` is the standing warning that subclassing vanilla blocks crashes on break unless
  `GetDrops` is overridden.
* The forming shop is a cluster, not one megablock. The mill is iiex's block on mpenergy, the hammer is
  iiex's on the pipe network, and the two never share a footprint. The "~3 s per pass" rate that appears in
  older drafts of a combined forming-shop megablock describes the mill and must not be adopted for the
  hammer.

---

## Open

| # | Work | Blocking? |
|---|---|---|
| 1 | Nothing is built. Block, BE, def, footprint, lang, handbook - all missing | yes |
| 2 | No recipe / RCC of any kind | yes |
| 3 | Every steam number is unchosen: draw, minimum pressure, blow interval, vent rate | yes |
| 4 | The die spec does not exist. It should be the [heading machine](heading-machine.md)'s `ItemDie`, and that page owns the contract - this machine is its second consumer | yes |
| ~~5~~ | ~~`shingledslab` does not exist as a `StockForm`~~ | **closed 2026-08-12** - the form is `shingledslab`, 8 × 3 × 20 at 1200 u. What the hammer shingles into now exists; the hammer does not |
| 6 | The wrought ball does not exist either | yes - nothing to shingle from |
| 7 | `boilerplate` does not exist | yes - nothing to stamp |
| 8 | Art: three shapes to wire, re-texture with domain-relative paths, and fit to whatever footprint is chosen | |
| 9 | What happens on a blow with no work under the die? Not designed - presumably a sound and nothing else, but the die/anvil pair should not damage itself | |
| 10 | Does the hammer need heat on the work? Shingling is white-hot by definition and stamping a cold `boilerplate` should presumably refuse - but no threshold is named anywhere, and the mill's `RollingTempC` is an iiex key the hammer would be reaching upward into | |
| 11 | The HP double-action hammer is hpex's, gated at ≥ 2-voxel output. It has no page and no design beyond that line | |
| 12 | Whether the anvil is a placeable block that docks or purely an `ItemStack` slot on the hammer. The design says the BE stores an `ItemStack` and tesselates it - so it is the latter, but it is also called "a separate docked block". Pick one and say so | |
