# Crucible steel furnace
**Status** designed, shapes drawn 2026-08-01/02 - nothing built; no block, no BE, no pot item, no metal def, no recipe   **Mod** iiex (`IronIndustryExpanded`)

**Owns**
* the draft crucible furnace (Huntsman, 1740): melting holes below floor level, ash pit and grate beneath, flue at the bottom, tall stack on top;
* the two independent axes: hole count sets throughput, chimney height sets temperature, and that both need machinery the layout DSL does not have;
* its charge and product: blister steel + coke + a sealed pot → crucible steel, the tool/weapon steel;
* the pot as a consumable fireclay item - the documented exception to the clay heat gate;
* why it is iiex and not smex, and why it is built in banks;
* the requirement that the natural-draught factor become a function of stack height, and the arithmetic behind it.

**Does not own** - cited only: the `T_process = T_in − T_loss` law, the melt-speed factor, the Idle/Firing/Melting FSM and every `Bf*` / `Cupola*` key ([heat balance](../mechanics/heat-balance.md)) · what blister / shear / crucible steel are ([materials.md](../materials.md)) · the canal a pot pours into ([molten network](../mechanics/molten-network.md)) · the layout DSL ([multiblock](../mechanics/multiblock.md)) · the cupola's own numbers ([cupola](cupola.md)) · the tilting non-ferrous crucible, a different and deferred machine ([STATE.md](../../internal/plans/STATE.md)).

**Depends on**
[heat balance](../mechanics/heat-balance.md) · [multiblock & fillers](../mechanics/multiblock.md) ·
[molten network](../mechanics/molten-network.md) · [recipes & config](../mechanics/recipes-config.md) ·
[materials.md](../materials.md) · [cupola](cupola.md) (the machine it is deliberately not) ·
[shear](shear.md) (blade sets are one of its consumers) · [STATE.md § D9](../../internal/plans/STATE.md)

---

## Settled 2026-08-02

| Decision | Detail |
|---|---|
| Pot material | fireclay (`clay-fire`), clayformed - a documented exception to the clay heat gate. See § The pot |
| Pot shape | one pot and four pots, mirroring vanilla's `crucible.json` / `fourcrucible.json` |
| Pot life | 3 firings, then it breaks after the pour |
| Pots per furnace | 4, fixed - in the hearth block, not a variable hole count. Build more furnaces |
| Charge | 110 u blister steel → 100 u crucible steel per pot (~9 % melt loss), plus a thin slag cover |
| Feedstock prep | cold-worked blister ingot on the anvil with a helve hammer → 3 chunks (25 u) + 5 bits (5 u) = 100 u exactly. Hot-worked keeps vanilla behaviour → shear steel |
| Preheat | via the damper, not the forge. Damper open = damped fire = the anneal; closing it on cold pots destroys them |
| Cracked pot | spills its charge one block down into the cellar as items, recoverable. The pot is lost, the steel is not |
| Refractory | any tier anywhere (`refractorybricks-good-tier*`). No new refractory material, no tier gate |
| Pour target | a cast-iron ingot mould, itself sand-cast. Separate shapes per mould, as vanilla has |
| Consumers | any vanilla steel item - tools, weapons, armour - plus longer-lasting drill/shear/machine heads. Not a gate anywhere: wrought-iron heads still work, crucible-steel ones just last |

### Why the two-stream feedstock split is right

Cold blister steel crushes; hot blister steel forges. One input, two products, selected by a property the player
already manages. Pig helve-breaking is the same operation and already exists.

### Why the damper carries the preheat

Preheating in a vanilla forge does not work: a forge holds one stack and a pot tracking firings cannot stack, so it
would mean four forge cycles per heat. The damper is the furnace's only heat control, so one lever throw replaces
those four cycles and the damper becomes a two-phase rhythm instead of a setting.

Two things this needs when built: the crack must be legible in the moment (sound + a cracked-pot element, cheap
because the shape's `Crucibles/CrucibleN` groups are already separate), and it must not eat the charge (see the
cellar spill above).

### The heat arithmetic must change too — charge loss is wrong for a firebox

Raising the draught term alone does not work: it would demand `airFactor` 0.855 (puddling) to 0.96 (crucible), the
top of its range, which means chimneys tall enough to make the puddling furnace taller than the blast furnace.

The cause is `T_loss`, not the draught curve. `T_loss = 430` = radiation 120 + charge loss 310, and
`BfChargeLossFull` scales on the charge count against `ChargeCapacityUnits`
(`BlockEntityFurnaceCore.cs:1871-1873`). A pure-fuel firebox reports `fuelFrac = 1.0`, so it takes the maximum
cold-burden penalty when it has no burden. A crucible furnace's thermal sink is four pots; a puddling furnace's is
one bed charge; neither is a 320-unit column of cold ore descending through the fire.

Charge loss becomes a per-machine virtual. The blast furnace keeps 310, having a real burden column; firebox
machines override it to what they actually heat:

| | `T_loss` | needs `airFactor` | at `natural = 0.5 + 0.10·√courses` |
|---|---|---|---|
| puddling (bed charge ≈100) | 220 | (1482+220−950)/1125 = 0.668 | ≈ 3 courses |
| crucible (4 pots ≈60) | 180 | (1600+180−950)/1125 = 0.738 | ≈ 6 courses |
| cold blast (unchanged) | 430 | has blast → 1.0 | n/a |

Six courses is what the layout draft already drew, and puddling lands on three, a low squat reverberatory with a
modest stack. Relative ordering: crucible stack > blast furnace ≈ puddling.

### The draught curve *peaks* — it does not clamp *(settled 2026-08-02)*

```
natural(courses) = base + gain·√courses − friction·courses²
```

A hard cap would be invisible - courses past it change nothing, with no feedback. A decline is observable and is
the honest physics: draught force rises with height, flue friction and stack heat loss rise faster, so net draught
has a real optimum.

At `base 0.5, gain 0.11, friction 0.00102` the peak sits near 9 courses:

| courses | `natural` | |
|---|---|---|
| 0 | 0.500 | bare flue |
| 3 | 0.681 | puddling's ≈1482 °C |
| 6 | 0.733 | |
| 9 | 0.747 | peak - this furnace's ≈1600 °C |
| 12 | 0.735 | already past it |
| 20 | 0.584 | worse than three courses |
| 30 | 0.184 | catastrophically worse than no chimney at all |

No clamp and no special case: building past the peak ruins the furnace rather than capping out.

This furnace sets the peak, and that is the binding constraint on the whole curve. It is the hottest
natural-draught machine in the mod, so `T_process` at the optimum must clear ≈1600 °C, which needs `T_in` ≈1750 and
therefore `natural_peak ≥ ≈0.74`. A gentler curve makes this machine unbuildable at any height. Everything else
lives below that peak: puddling at ~3 courses, the reheat furnace ranging roughly 1090-1370 °C across its entire
buildable span, wholly below iron's 1538.

The block-info readout is therefore non-optional: the player must see something like `stack: 11 courses -> draught
0.74 ▼ (peak at 9)`, or a decline is indistinguishable from a bug. Same R7 requirement the counted-stack design
carries.

All three coefficients are proposals to calibrate in play. Settled: the shape (rise, peak, decline), and that this
machine's requirement fixes the peak.

### The reverberatory transfer loss, and why it must NOT be a branch constant *(2026-08-02)*

A reverberatory furnace is cooler at the work than at the fire: the flame crosses the bridge and loses heat on the
way. That separation is what lets puddling work iron without the fuel touching it, and it costs temperature, so the
firebox branch carries a transfer loss the shaft branch does not.

That loss is the ceiling on the reheat furnace, replacing a hand-set cap. With draught saturating at
`natural = 0.85`, `T_in` tops out at `950 + 900 × 1.25 × 0.85 ≈ 1906`; against a reverberatory `T_loss` of
roughly `120 radiation + 250 transfer + 50 charge = 420`, `T_process` ceilings at ≈1486 °C, just under iron's
1538. A reverberatory furnace cannot melt iron and no constant has to say so; the same fact makes puddling a
pasty-state process, and thirty courses still only give a very hot furnace.

This is what makes a player-built chimney worth having on the reheat furnace: reheating targets a band (rolling
~1100-1250 °C, shingling hotter, annealing far cooler), not maximum heat. The stack becomes a heat-treatment dial,
and heat treatment is the system the longer-lasting crucible-steel tool heads wait on.

This furnace is not reverberatory, and a branch constant would break it. The pots sit in the coke bed, surrounded
by fuel; the only separation is the pot wall, so there is no bridge and no transfer loss. It is on the firebox
branch for the right reasons (plain fuel bed, no blast, natural draught), but its thermal geometry is closer to a
shaft furnace's.

So transfer loss is a virtual with a branch default, not a branch constant: reverberatory machines (puddling,
reheat) pay it; this one overrides it to ≈0. Get that wrong and the cap that correctly stops a reheat furnace
melting also stops the crucible furnace ever working.

The same term fixes the "a fuller firebox is a colder furnace" inversion recorded in § Gotchas.

All numbers above are proposals to calibrate in play. Settled: per-machine charge loss, `√courses` draught,
saturating cap.

### Still blocking, and unchanged: the draught

§ Numbers' worked ceiling stands: a natural-draught firebox reaches `T_process = 1082.5 °C` against the ~1600 °C
this process needs. The fix and the damper decision are the same piece of work. `BfNaturalDraughtFactor` becomes
`NaturalDraughtFor(stackCourses, damperOpen)` - stack height is the temperature axis, the damper is the operating
input, and one function at the firebox branch's `natural` read (`BlockEntityFurnaceCore.cs:1859`) delivers the
fatal fix, the preheat mechanic and the cold-pot failure at once. It also retro-fixes B8 (the puddling furnace's
identical ceiling) and the coke oven.

### Machine tooling wear — ruled, not built

"Crucible-steel heads serve much longer" is free for player tools: `MetalDef.Durability` + `MetalToolEmitter`
already emit a whole tool family from one metal-def entry, so only the entry is missing. For drill bits, shear
blades and roll sets the rule is [tooling-wear](../mechanics/tooling-wear.md)'s (ruled 2026-08-05: tooling wears,
and the grade sets its life), but the mechanism is not built - it is the second prerequisite after the draught
function.

### Assets drawn

| Asset | File |
|---|---|
| hearth interior (base, 4 pots, blister/slag fills, covers) | `assets/editable/shapes/furnace-draftcruciblehearth.json` |
| the pot as an item | `assets/editable/shapes/item-steelcrucible.json` |
| crushed blister-steel chunk | `assets/editable/shapes/item-shingled-metalchunk.json` |
| layout draft | [layouts.md](../../internal/workbench/layouts.md) § 1 |

No coke element in the hearth shape, though `#coke` is declared: the coke around the pots is the charge-pile
problem the charge column already solves. `ChargeColumn` + `BandsAt` give layered coke that burns down and is
topped off, which is what a 3–4 hour melt needs: one charge is never enough.

The hearth shape hardcodes `refractory/tier3/front1`. Every furnace core in the mod uses `{tier}` so the block
wears what it was built from (`BlockBlastFurnaceCoreCold.cs:39`) - match that, since any tier is allowed.

---

## Role

The suite pays the player in materials and never in gear. Bessemer steel is cheap and structural, open-hearth steel is close to vanilla shear steel, HSS is far off; crucible steel is the tool and weapon steel (Huntsman 1740).

It closes a loop already in the tree. Crucible steel is made by melting blister steel in a sealed pot, and blister steel is a shipped vanilla item (`game:ingot-blistersteel`, refined on the anvil into `game:ingot-steel` by `survival/recipes/smithing/steel.json`), so vanilla's steel chain becomes crucible feedstock with no new upstream process - cementation stays vanilla.

It belongs to iiex, not siex: the process is coke-fired, natural draught, no steam and no MP, a century older than Bessemer. It is the "cool gear" payoff an iiex-only player needs without requiring steam. Its gate is not tier but batch size: one pot at a time.

**Why not the cupola.** The distinction is physical:

| | [Cupola](cupola.md) | Crucible |
|---|---|---|
| Fuel and metal | in contact - metal drips through burning coke | never touch - the charge is sealed in a pot, the fuel burns outside |
| Consequence | carburises, picks up sulfur | no pickup at all - homogeneous, slag-free |
| Right for | cast iron · ferroalloys | clean high-carbon steel |

A cupola physically cannot make crucible steel: fuel contact ruins the purity the process exists for. This is a second melting machine, not a data override of the first.

Built in banks. Same multiply-don't-enlarge pattern as the cupola, the nail benches and the mill hall: the block must be small and cheap, and the player builds several.

---

## Structure

```
        ┌──┐          tall stack        ── draught, and therefore temperature
        │  │
     ┌──┴──┴──┐       flue
 ────┤ ○  ○  ○ ├────  melting holes AT FLOOR LEVEL, pots BELOW it
     │ ▒▒▒▒▒▒ │       coke around the pots
     ├────────┤       grate
     │        │       ash pit
     └────────┘
```

| Part | Cell(s) | Job |
|---|---|---|
| ash pit | lowest layer | air enters here and the ash falls; it is the bottom of the draught column |
| grate | above the pit | holds the coke bed; the player never interacts with it |
| melting hole | floor row - the interactive cells | one pot each. Hole count = throughput |
| core | one hole row cell, the anchor | `BlockFurnaceCoreBase.Core(...)` (`BlockFurnaceCoreBase.cs:34-68`) - the same refractory-cube anchor every furnace uses |
| flue | above the holes | joins the holes to the stack |
| stack | as many courses as built | height = temperature |
| damper | stack top | the one air control a natural-draught furnace has |

### The stack is player-built and counted — settled 2026-08-02

Pot count is fixed at four; stack height is not. The chimney leaves the layout entirely: the layout declares the minimum viable furnace including the stack base, and the core walks up from that base counting the courses the player actually built. `NaturalDraughtFor(courses)` reads that count. This is the suite's core trade - buy operating capability with build complexity - and it dissolves the calibration problem, because no single constant has to hit 1482 °C for the puddling furnace and 1600 °C here.

A course is `. b . / b a b / . b .` - four brick-family blocks around one air cell. That is already what `siex:smokestack` draws (`BlockSmokeStackIntake.cs:50-53`) and what this furnace's own draft uses, so the validity check is one predicate for every machine.

| Rule | Why |
|---|---|
| the core owns the walk; the cap never searches for the core | if the cap were a `BlockEntityFurnacePart` using `MultiblockAnchorLink`, `ComponentScanBelow = 8` would cap chimney height at eight forever. The core is already walking up to count, so whatever it finds at the top is the cap, read by position. No anchor link, no radius limit |
| the cap terminates the walk | the walk continues while the ring is valid and stops at the first cell that is not one. A cap there is the top. The puddling furnace requires one; this furnace and the blast furnaces have none and terminate on air. One walk, every machine |
| a gap breaks the count | physically right - a leaking flue has no draught - and it gives a failure the player can see and fix |
| the function saturates, and the count is hard-capped | nothing stops a player stacking sixty courses. Real draught saturates, so an over-tall chimney should be a waste of bricks, not an exploit |
| cache the walk | it must not run per tick. Invalidate on neighbour change, as the rest of the mod does |

The chimney is outside `StructureComplete`, so the furnace completes and runs cold with no stack at all. That is legible only if the block info says so - `stack: 3 courses -> T_process 1240 °C` - which is R7 ("nothing is hidden").

The puddling cap and this furnace's bottom damper are the same mechanic at opposite ends of the flue, top cap versus bottom bypass. One implementation should serve both, so the crucible furnace's `K` door and `BlockPuddlingChimneyCap` share code.

The counted scan is needed because the layout DSL is a fixed cell table - `MultiblockLayout` declares an exact set of offsets and `IncompleteBlockCount` walks exactly those ([multiblock](../mechanics/multiblock.md)) - so "a taller stack" is not expressible as a layout.

---

## Assets

Editable shapes are drawn (§ Settled, "Assets drawn"); nothing is exported or wired.

| Asset | State |
|---|---|
| editable shapes | drawn - hearth interior, pot item, crushed blister-steel chunk (§ Settled) |
| runtime shape | missing - nothing exported into `assets/iiex/shapes/` |
| reference art | none in `assets/editable/refs/` - the Sheffield references are not in the repo |
| pot item def | missing (see Construction) |
| lang / handbook | no key, no page |

Reusable art and parts that already exist:

| Need | Existing | file:line |
|---|---|---|
| the refractory anchor cube | `BlockFurnaceCoreBase.Core` - north face carries an orientation marker, south face a two-letter type label | `BlockFurnaceCoreBase.cs:34-68` |
| a damper on a lever | `BlockPuddlingChimneyCap` / `BlockEntityPuddlingChimneyCap` - `IsOpen`, `Toggle`, held `idle`/`open` poses | `BlockEntityPuddlingChimneyCap.cs:19-33` |
| stacked brick courses | `siex:smokestack`'s brick-family alternation `@(claybricks-good-fire\|refractorybricks-good-.*\|brickcourse-…)` | `BlockSmokeStackIntake.cs:50-53` |
| interactive floor cells | the hearth/filler idiom - which cell you click picks which hole | `BlockPuddlingHearth.cs:25-30`, `BlockHeatingHearth.cs:43-54` |
| the pot itself | vanilla `crucible-{color}-fired` shape, retextured to refractory | `survival/blocktypes/clay/fired/crucible.json` |

---

## Construction

No recipe. Proposed, and cheap because the machine is meant to be duplicated:

| Part | Cost | Note |
|---|---|---|
| core | refractory brick ×2 + fire clay | `ExIngredients.FireClay` (`ExIngredients.cs:52`), `Refractory(…)` as used at `FurnaceRecipeDefinitions.cs:49` |
| melting hole | refractory brick ×4 | one per hole, so throughput is bought a brick at a time |
| stack courses | any of the brick family the smokestack already accepts | no new block needed |
| damper | plate + nails, on the chimney-cap chassis | `ExIngredients.Nails` (`:36`) |

Cost keys `cruciblefurnacecore-grid`, `cruciblehole-grid` in `IiexRecipeConfig.DefaultCatalogue` (`IiexRecipeConfig.cs:47-70`).

### The pot is fireclay, and it is a deliberate exception to the clay gate *(settled 2026-08-02)*

| Source | Ceiling |
|---|---|
| vanilla `crucible-*-fired` | `maxHeatableTemp: 1200` (`survival/blocktypes/clay/fired/crucible.json`, `attributesByType`) |
| this mod's own clay rule | `ClayMoldHeatCeiling = 1100` °C - "clay is bronze max" (`IiexConfig.cs:65`, gate at `ClayHeatGate.cs:21-32`) |
| crucible steel needs | ≈ 1600 °C |

Those ceilings do not bar the pot. The clay ceiling governs molds, vessels reused indefinitely; the pot is a consumable that dies from exactly this abuse, so three heats at 1600 °C is the ceiling being enforced, paid in pots rather than in refusals. Sheffield pots were fireclay (Stourbridge clay, a high-alumina fireclay) and two or three heats was normal practice.

So: clayformed from `clay-fire`, exactly as vanilla's own crucible is, as a distinct item with its own heat allowance and a 3-firing life. Write the exception into `ClayHeatGate` explicitly rather than letting the pot slip past silently.

This removes the need for a refractory clay item entirely: vanilla clayforming accepts only `clay-*` in `["blue","fire","red"]`, and refractory brick is a grid recipe (`clay-fire` + crushed quartz/bauxite/olivine/ilmenite), so there is no clayformable refractory to pick a tier of.

The tilting non-ferrous crucible keeps its own argument unchanged: a cast-iron vessel cannot hold molten steel, so that machine stays incapable by construction rather than by rule ([STATE.md](../../internal/plans/STATE.md)).

---

## Operation

```
blister steel (+ a carbon/flux trim)  →  pot  →  hole  →  coke fire  →  pull  →  pour
```

| Verb | Where | Effect |
|---|---|---|
| RMB with a charged pot | a melting hole | seat it. Which cell you click picks which hole - the hearth idiom (`BlockHeatingHearth.cs:73-78`) |
| RMB with fuel | the coke bed | charge the fire; a firebox burns plain fuel, never a burden |
| RMB on the damper lever | stack top | open/shut the draught - `Toggle()` (`BlockEntityPuddlingChimneyCap.cs:26-29`) |
| RMB with tongs / empty hand | a hole with a finished pot | pull it. Vanilla already models this: the crucible carries `onTongTransform` and `tongOpening: "wide"` |
| RMB the pot | a canal start, a mold, or a barrel | pour. Already supported: `BlockMoltenCanalStart` accepts a `BlockSmeltedContainer` pour (`BlockMoltenCanalStart.cs:77`, help stacks at `:148`), and `BlockMoltenBarrel` caches every `crucible-*` block for the same interaction (`BlockMoltenBarrel.cs:84-91`) |

The player carries the pot; the furnace does not tilt. It costs nothing to build, because the pour interface into the [molten network](../mechanics/molten-network.md) already exists and is used by vanilla crucibles. Tilting was chosen for the non-ferrous machine because that one has no other way to reach the canal.

States. The shared `Idle → Firing → Melting` FSM applies unchanged ([heat balance](../mechanics/heat-balance.md)); what differs is that the melt acts on each seated pot rather than on a burden column, the same override the reheat furnace already makes (`BlockEntityHeatingFurnace.SmeltCycle` is deliberately empty, `:107`). It also inherits the reverberatory pattern for the fire: no blast, no tuyeres, nothing to starve (`BlockEntityHeatingFurnace.cs:46-51`).

---

## Numbers

All proposed. No config section, no keys, no code. The right-hand column is what exists and what the proposal is measured against.

| Key | Proposed | file:line of the thing it derives from | What it does |
|---|---|---|---|
| `CrucibleMeltingPoint` | 1600 °C | cf. `BfIronMeltingPoint = 1482` (`IiexConfig.cs:271`), `CupolaCastIronMeltingPoint = 1200` (`:316`) | the process temperature crucible steel demands - the highest in the mod |
| firebox charge sizing | derived | the firebox branch sizes its fire through `ChargeCapacityUnits`, as the reheat furnace does | a firebox holds a fire, not a charge column |
| `CruciblePotUnits` | 100 u (one vanilla ingot's worth) | cf. `MoldDefaultUnits = 100` (`IiexConfig.cs:92`) | one pot, one batch - the batch-size gate is the machine's balance |
| `CrucibleMeltIntervalSec` | 60 | the firebox branch's melt-interval idiom | slow on purpose: tiny, slow, per-pot |
| `CrucibleMaxFuelBurnTime` | 1200 | `BfMaxFuelBurnTime = 1200` (`:280`) | inherit |
| `CrucibleHoleCount` | derived from the build, not config | - | throughput axis |
| `BfNaturalDraughtFactor` | must become a function of stack height | currently a flat `0.5` (`IiexConfig.cs:276`), read at `BlockEntityFurnaceCore.cs:1859` | temperature axis - see below |

### Worked: a natural-draught furnace cannot reach 1600 °C under the shipped constants

Against [heat balance](../mechanics/heat-balance.md)'s own terms (the firebox branch of `ComputeHeatBalance`), for a pure-fuel firebox - `fuelFrac = 1.0`, because a reverberatory firebox reports its mix as all fuel (`BlockEntityHeatingFurnace.cs:70-89`):

| Term | Value | Why |
|---|---|---|
| `fuelFactor` | 1.25 | `1 + 0.35 × (1.0 − 0.20)/0.20 = 2.4`, clamped by `BfMaxFuelFactor` (`IiexConfig.cs:196`) |
| `airFactor` | 0.5 | `natural + (1 − natural) × 0` with no blast - `BfNaturalDraughtFactor` (`:207`) |
| `T_in` | 1512.5 °C | `950 + 900 × 1.25 × 0.5` (`IiexConfig.cs:181`, `:184`) |
| `T_loss` | 430 | radiation `120` (`:221`) + full charge loss `310` (`:225`) at 20 °C ambient |
| `T_process` | 1082.5 °C | 500 °C short of what the process needs - and short even of iron's own 1482 line |

To land 1600 °C with a full firebox the natural factor must rise to `(1600 + 430 − 950) / 1125 ≈ 0.96`, natural draught nearly as effective as full blast. With an empty-hearth loss it is ≈ 0.68. Either way the shipped 0.5 cannot do it, so "chimney height sets temperature" is the only term available and the furnace does not work without it.

This is the same ceiling as blocker B8 (the puddling furnace: natural draught caps `T_in` at 1512.5 against an inherited 1482 melt point - [STATE.md](../../internal/plans/STATE.md)). One fix serves both, plus the coke oven: make the natural-draught factor a function of stack height, which retro-fits every natural-draught furnace at once. Draught rises with stack height and with the temperature difference.

---

## Drops

| Broken | Returns |
|---|---|
| the core | itself; the furnace is dead and cleared by breaking its walls - there is no door, the same rule every furnace core follows (`BlockFurnaceCoreBase.cs:8-11`) |
| a melting hole | its bricks, and any seated pot with its contents - a pot is the player's metal and must never be voided |
| stack courses | ordinary bricks, they are ordinary blocks |
| the damper | itself |

---

## Code

Nothing exists. `grep -i crucible src/` finds only the vanilla crucible pour paths (`BlockMoltenCanalStart.cs:61-77`, `BlockMoltenBarrel.cs:78-91`) and the clay heat gate (`ClayHeatGate.cs`, `ToolMoldHeatGatePatch.cs`).

| Piece | Where | Model it on |
|---|---|---|
| `BlockCrucibleFurnaceCore` | `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/` | `BlockCupolaFurnaceCore.cs:20-127` - the shortest complete example: `Core(domain, code, path, tiers…)` then `.Class`/`.EntityClass`/faces/`.MultiblockLayout` |
| the layout | `.MultiblockLayout(s => s.Origin(…).Legend(…).Layer(…))` | `BlockCupolaFurnaceCore.cs:59-125`; negative Y layers are legal and used (`BlockSmokeStackIntake.cs:55`) |
| `BlockEntityCrucibleFurnace` | `.../Furnaces/BlockEntities/` | `BlockEntityHeatingFurnace.cs:31-136` - the thinnest furnace variant in the tree: firebox geometry (`:37-51`), plain-fuel charge read (`:70-89`), empty `SmeltCycle` (`:107`), tunables block (`:113-127`) |
| melting hole | `.../Furnaces/Blocks/BlockCrucibleHole.cs` + BE | `BlockEntityFurnacePart` (`:32`) for the core link and the toggle animator; `BlockPuddlingHearth.cs:70-78` for cell → slot routing |
| damper | reuse | `BlockPuddlingChimneyCap` / `BlockEntityPuddlingChimneyCap.cs:19` |
| pot item | `.../Items/` | `StockItemDefinitions.cs:32-59` for the def shape; the vanilla crucible for the block behaviours (`GroundStorable`, `RightClickPickup`, `onTongTransform`) |
| pour | already works | `BlockMoltenCanalStart.cs:77` accepts a `BlockSmeltedContainer` |
| metal def | `assets/iiex/config/metals/cruciblesteel.json` | the shipped catalogue has `castiron`, `pigiron`, `slag` (iiex) and `bessemersteel` (smex) - a new metal is a JSON entry read by `MetalRegistry` |
| heat | inherited | `ComputeHeatBalance` (`BlockEntityFurnaceCore.cs:743-798`); override `MeltingPoint`, `MaxFuelBurnTime`, `MeltStartDelay`, `MeltIntervalSec`, `RequiresBlast => false` - and, by drawing no tuyere or outlet glyph, no `CellRole.Tuyere`/`GasOutlet`. Exactly what the reheat furnace does |

Where a caller hooks in: the stack-height draught term belongs in `ComputeHeatBalance`'s `natural` line (`BlockEntityFurnaceCore.cs:1859`) as `NaturalDraughtFor(stackCourses)` rather than a flat read, so every natural-draught furnace inherits it. That single edit is the crucible furnace's real prerequisite.

---

## Gotchas

- **The natural-draught ceiling is fatal, not tight.** See Numbers. Do not start this machine before the draught term lands, or it ships as a furnace that lights and never melts - blocker B2's failure mode on a different furnace.
- **A fired-clay pot is wrong by this mod's own rule** (`IiexConfig.cs:65`) and by vanilla's (`maxHeatableTemp: 1200`). Reusing `game:crucible` unpatched contradicts the mod's own clay ceiling in the one place it matters most.
- **The layout DSL cannot express a variable height or a variable hole count.** Both axes need the counted-scan approach; neither is a layout edit.
- **`ComponentScanAbove = 1`, `ComponentScanBelow = 8`** (`BlockEntityFurnaceCore.cs:1220-1222`). A part scans down eight cells at most, so an eight-course stack is the ceiling before the damper loses its core.
- **The cupola must not gain a crucible mode.** It is already a pure data override of the blast furnace, and ferroalloys are its second act. Fuel contact is the whole distinction; a `crucible` burden family would erase it.
- **This is not the tilting crucible.** That machine is non-ferrous, later, and deferred by decision; its tilt exists because it has no other way into the canal. This one has tongs.
- **Do not give it a blast.** It is natural draught by definition, and `RequiresBlast => false` is what stops the blower ecosystem starving it (`BlockEntityHeatingFurnace.cs:47`). A blown crucible furnace is a different and later machine.
- **`BfChargeLossFull` scales on the charge count against `ChargeCapacityUnits`** (`BlockEntityFurnaceCore.cs:1871-1873`), so a fuller firebox is a colder furnace. Correct for a burden column, wrong for a firebox, and it is what puts 310 of the 430 °C on the ledger. Scheduled to become a per-machine virtual (see § Settled).
- **That change is a shared-core change.** `BlockEntityFurnaceCore` feeds the two reverberatory hearths, the coke oven, the cowper stove and both boilers' fireboxes as well as this machine. Every one of them currently pays the full cold-burden penalty on a fuel-only charge, so every one of them gets hotter when this lands. That is the intended direction - B8 is the same bug - but it must be re-checked machine by machine, not assumed.
- **R7 is "Nothing is hidden", not "no GUI".** Block info must show the current temperature, the threshold and both sides of the ledger, as every other furnace does.

---

## Open

- Nothing is built. Core, hearth, pot, metal def, recipes, runtime shapes, lang, handbook, tests.
- The stack-height draught function - its cap, and whether it also scales with the temperature difference (the settled shape is rise, peak, decline; § Settled). The one thing the machine cannot start without, and it changes the puddling furnace and the coke oven too. It also carries the damper as its second input.
- Calibrating each drawn chimney. With height fixed by design rather than chosen by the player, every natural-draught furnace's stack needs a course count that lands its own process temperature: this one ≈1600 °C, the puddling furnace clear of 1482, the coke oven its own. The drafted six courses here are a starting point, not a derived value.
- Tooling wear. The rule exists - tooling wears and grade sets its life ([tooling-wear](../mechanics/tooling-wear.md)) - but the mechanism is unbuilt, and the longer-lasting heads wait on it.
- Whether the pot takes a carbon trim (powdered coke), which is how Huntsman actually hit a grade.
- What crucible steel is, mechanically. `materials.md:67` gives ~98.8 % Fe / ~1.2 % C. The metal def is still missing, but `MetalDef.Durability` + `MetalToolEmitter` already emit a whole tool family from one entry, so the machinery is there. D9 notes it raises vanilla's tool ceiling, deliberately, which nothing has been balanced against.
- Whether the four pot cells are furnace parts or footprint fillers. Parts get the core link and an animator for free (`BlockEntityFurnacePart.cs:32-60`); fillers get per-cell interaction routing for free ([multiblock](../mechanics/multiblock.md)). The puddling hearth chose fillers, the tuyere chose parts. The hearth shape's per-pot element groups suit either.
- Slag. A crucible melt makes essentially none - nothing to tap, nothing to plumb. Confirm the inherited `BfMaxMoltenSlag` path is simply not used rather than silently accumulating. Slag is still an input here (the thin flux cover), sourced from the blast furnace's own `iiex:slag` - a closed loop worth keeping.
- The non-ferrous tilting crucible stays deferred with everything non-ferrous, but it shares this page's pot problem in reverse (a cast-iron vessel, lower temperatures) and should reuse whatever pot model lands here.

Settled 2026-08-02: feedstock is blister, prepared by cold helve-crushing - shear steel is the hot branch of the same fork, so both stay useful; consumers are any vanilla steel item plus longer-lasting machine heads, a gate on nothing; the bank question closed as 4 fixed pots per furnace, build more furnaces, which removes the variable-hole-count problem - stack height is still variable, so the counted scan survives, and `ComponentScanBelow = 8` still caps it (the drafted chimney runs six courses over the hearth, two short of the limit).
