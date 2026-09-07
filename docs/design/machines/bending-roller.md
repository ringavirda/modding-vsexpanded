# Bending roller (plate bending rolls)

**Status** designed - nothing built, and nothing drawn. No block, no block entity, no tooling item, no
recipe, no shape, no lang key. A repo-wide grep of `src/` for `bend`, `roller` or `conical` returns only pipe
bend segments. Generalised from the design's "conical pipe roller"
([STATE.md:429-434](../../../../docs/plans/STATE.md)).
**Mod** iiex (`IronIndustryExpanded`)

## Owns

* curvature as a verb: that bending is not a reduction, that three rolls in a triangle are needed to do it,
  and why it cannot be expressed as a roll set on the [rolling mill](rolling-mill.md);
* the one-machine-four-tools catalogue: conical → pipe from skelp · cylindrical → shells and barrels from
  plate · large radius → wheel rims;
* the placement: why nothing at iron tier bends, and therefore why this is iiex;
* the machine's own proposed footprint, drive contract, verbs and drops;
* the state of its art (there is none) and its build order.

## Does not own — cited only, never restated

| Fact | Owner |
|---|---|
| the reduction model - `δ_max = μ²R`, spread, elongation, the two-round pass, cooling, every `Rolling*` key, `RollSetSpec`, `WorkPiece` | [rolling mill](rolling-mill.md) |
| the energy model, `IMpEnergyConsumer`, the pulsed-load argument, every `Mp*` key | [mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md) |
| the shafts, bevels, transmission and flywheel that drive it | [flywheel & shafting](flywheel-and-shafting.md) |
| the `ItemDie` tooling contract and the die-fed block-entity base this machine's tooling should copy | [heading machine](heading-machine.md) |
| the crop that cuts plate to length before it is bent | [shear](shear.md) |
| the stock the plate is rolled from, and the six-stand train that rolls it | [wide hall](wide-hall.md) |
| every product mass and `1 vx³ = 2.5 u` | [density rule](../mechanics/density-rule.md), [rolled-parts](../items/rolled-parts.md) |
| pipe tiers, burst pressures, the flanged/welded joint-family rule | [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) |
| the cast-vs-forged rule and the cast part catalogue this machine substitutes for | [cast-parts](../items/cast-parts.md) |
| the fabricated-substitute decision (D2 / N3) and the placement rule | [STATE.md](../../../../docs/plans/STATE.md) |
| the ≤ 32 / ≤ 48 handling invariant | [recoverability](../mechanics/recoverability.md) |
| code-first defs, RCC stages, cost catalogue, goldens | [recipes & config](../mechanics/recipes-config.md) |

**Depends on** [mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md) · [rolling mill](rolling-mill.md) ·
[wide hall](wide-hall.md) · [shear](shear.md) · [heading machine](heading-machine.md) ·
[flywheel & shafting](flywheel-and-shafting.md) · [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) ·
[recipes & config](../mechanics/recipes-config.md) · [STATE.md § N3](../../../../docs/plans/STATE.md) ·
[cast-parts](../items/cast-parts.md)

---

## Role

### Two rolls cannot bend

A rolling mill is a two-high stand: the piece passes through one gap and comes out thinner. Plate bending
rolls are three rolls in a triangle: the piece comes out curved and the same thickness. A different
operation, not a variant of the same one ([STATE.md:446-448](../../../../docs/plans/STATE.md)).

Everything in `RollingPass` describes reducing thickness:

| Relation | file:line | Describes |
|---|---|---|
| `δ_max = μ²R` | `RollingPass.cs:43-44`, `:52-57` | whether the rolls bite a draft |
| `w = w₀·(t₀/t)^e` | `:146-160` | how much the piece spreads as it thins |
| `L = L₀·(t₀/t)/(w/w₀)` | `:168-176` | how much it elongates as it thins |
| `T ← ambient + (T−ambient)·e^(−rate·dt)` | `:190-196` | how fast it cools |

Every one is a function of thickness change. Bending changes none of them; it produces curvature, which
`WorkPiece` has no axis for (`WorkPiece.cs:35` - the record carries form, strips, turned-flags and nothing
else). Expressing bending as a `RollSetSpec` would mean a `gaps` array that silently means something else,
the overload the roll-set idiom exists to avoid ([STATE.md:450-453](../../../../docs/plans/STATE.md)).

It shares the code - `mpenergy` consumer, tooling item, block-entity base - without sharing the block, the
same relationship the [shear](shear.md), [nail machine](nail-machine.md) and
[heading machine](heading-machine.md) already have with each other
([STATE.md:455-456](../../../../docs/plans/STATE.md)).

### It closes the last open consumer question

`boilerplate` was left open, "to accrete uses as features land"
([rolled-parts](../items/rolled-parts.md)). This is the feature:

> A rolled shell is what a boiler barrel is. The same plate bends into machine shells, barrels, pipe and
> wheel rims. Boilerplate → bending roller → the shell of everything
> ([STATE.md:440-442](../../../../docs/plans/STATE.md)).

### It is what makes the fabricated substitutes buildable

Every cast-iron structural part gets a rolled/fabricated steel equivalent (D2/N3). All of them reduce to two
operations, and only bending needs a machine ([STATE.md:417-427](../../../../docs/plans/STATE.md)):

| Cast part | Fabricated from | Operation | Made on |
|---|---|---|---|
| `castplate` | rolled plate | — | the mill *(done)* |
| `castframe` | beam × N + plate + rivets | assemble | a recipe / RCC stage - a riveted plate girder |
| `castshell` | plate + rivets | bend + rivet the seam | this machine |
| `cast-barrel` | plate + rivets | bend + rivet | this machine |
| `castwheelsection` | bent rim + bar spokes + hub + rivets | bend + assemble | this machine + recipe |

Rivets are the ingredient, so the joining is abstracted into the recipe and no riveting machine is required.
A powered riveter stays available later as a pure throughput upgrade, but nothing is blocked without one
([STATE.md:436-438](../../../../docs/plans/STATE.md)). The rivet itself comes off the
[heading machine](heading-machine.md) wearing iiex's rivet die.

### Why iiex

The placement rule sends a machine to the mod that consumes its output, not the one whose materials it is
made of ([STATE.md:458-476](../../../../docs/plans/STATE.md), row *bending roller → iiex*):

> Nothing at iron tier bends. iiex's pipes are plated from flat plates - which is what the `plated` tier's
> name records - and its cast parts are cast, not fabricated. The first thing in the suite that needs a
> curved plate is a boiler shell, and that is iiex's.

Historically plate bending rolls sat next to the shear and the punch in the boiler shop, which is what the
1867 machine-tool plate at `workbench/refs/rivetsnails/` is a page of
([STATE.md:433-434](../../../../docs/plans/STATE.md)).

---

## Structure

Nothing is decided beyond "not the mill". What the design fixes, and what it leaves open:

| Aspect | Proposal | Source |
|---|---|---|
| Network | `"mpenergy"`, an `IMpEnergyConsumer` | [STATE.md:455](../../../../docs/plans/STATE.md) |
| Shafting | cast-iron shaft / bevel, not vanilla MP and not wooden axles - cast iron is the shared prerequisite for both the frame and the shafting, so there is no second tier to gate | [flywheel & shafting](flywheel-and-shafting.md) |
| Frame | cast-iron: `castframe` standards on a plate bed, the boiler-shop C-frame silhouette | [cast-parts](../items/cast-parts.md) |
| Footprint | unchosen. The three 1 × 1 benches (shear, nail, heading) are small so a player builds several; a plate roll is a wide machine and the pieces it eats are 15 voxels across, so 1 × 1 is probably wrong | — |
| Tooling | one roll-set-shaped item selecting the roll geometry, in the established `rollset` / `pattern` / `ItemDie` idiom | [STATE.md:430-432](../../../../docs/plans/STATE.md) |
| Drive | the load is a steady draw, not a pulse - unlike the shear, nail and heading benches, which are pulsed and each carry their own flywheel | derived here; see [Open](#open) |

The one structural thing that is settled is that this is a separate block, sharing a base class with the
die-fed benches and not sharing a frame with them - "three shapes, three blocks"
([STATE.md:455-456](../../../../docs/plans/STATE.md)).

---

## Assets

Nothing is drawn. Not the machine, not a roll set, not a single output item.

| Asset | Path | State |
|---|---|---|
| machine shape (editable) | — | none |
| runtime shape | `mods/iiex/assets/iiex/shapes/…` | none (`boiler/`, `engine/`, `pipes/` only) |
| roll-tooling item shapes | — | none. `item-rollers-*.json` are the mill's roll sets; `item-rollers-castblank.json` is a cast roll blank, and would be the right feedstock for this machine's tooling too |
| lang keys / handbook page | — | none |

The three drawn references that bear on the shape:

| Reference | Path |
|---|---|
| the 1867 machine-tool plate (shear · punch · rivet machine) | `workbench/refs/rivetsnails/machine-tools-1-rivet-making-machine-…-1867-technology-RY93PB.jpg` |
| the hydraulic riveter (the upgrade this machine does not need) | `workbench/refs/rivetsnails/the-portable-hydraulic-riveter-…-2E4KE93.jpg` |
| the mill hall, for the shafted-bench silhouette | `workbench/refs/rolling/C0229569-Zinc_rolling_mills,_19th_century.jpg` |

Drawing notes transferred from the bench family ([heading machine](heading-machine.md)): a heavy cast bed or
C-frame · a big flywheel on a geared shaft · a collecting tray under the working point that tells the player
what the machine makes without a tooltip · an inclined feed table pointing at the face the player interacts
with. For a bending roll the third roll and the hinged end housing (which is how a closed shell is got off
the rolls) are the two features that make it read as a bender and not as a mill - draw them.

---

## Construction

There is no recipe. Nothing costs anything, and no build order has been written.

The bill should follow the machine-parts rule ([cast-parts](../items/cast-parts.md)): three to five
lines, drawn from the shared catalogue rather than a bespoke casting -

| Likely | From |
|---|---|
| `castframe` ×2 (the standards) | the [casting cell](casting-cell.md) / [long cell](long-cell.md) |
| `castplate-heavy` (the bed) | the same |
| gear / pinion, rod, nails & strips | iiex's own |
| the rolls themselves - cast chilled, like the mill's | no `rollers-castblank` pattern is wired |

Do not invent a new part item for it. The standing rule is "no new part item unless it is used by at least
two machines, or it is a machine's signature" ([cast-parts](../items/cast-parts.md)).

---

## Operation

### The four jobs — one machine, four tools

| Tooling | In | Out | Consumer |
|---|---|---|---|
| conical rolls | `skelp` (8 × 1 × 10, 200 u) | rolled pipe segment → bell-welded → rolled pipe | hpex's rolled pipe tier |
| cylindrical rolls | plate / `boilerplate` | `castshell` substitute; machine bodies and enclosures | cistern · crusher casing · boiler barrel |
| cylindrical rolls | plate | `cast-barrel` substitute | the molten barrel, the fluid tank |
| large radius | bar / plate | bent rim → `castwheelsection` substitute | flywheels, wheels |

([STATE.md:425-432](../../../../docs/plans/STATE.md); `skelp`'s geometry and mass at [rolled-parts](../items/rolled-parts.md).)

Four jobs instead of one is what justifies building it - pipe alone never did
([STATE.md:432-433](../../../../docs/plans/STATE.md)).

### The verbs — proposed, none built

| Held / gesture | Where | Result |
|---|---|---|
| a roll set | anywhere on the machine | fit the geometry; hand back the previous set. Refuse mid-operation |
| plate / skelp, RMB | the feed side | start a pass; the piece comes out curved, same thickness |
| a wrench | anywhere | free a stuck piece, state unchanged |

These mirror `BlockRollingMill.HandleInteract` exactly (`BlockRollingMill.cs:247-273`, `:293-320`), which is
the point of sharing the base.

### What the work piece needs that `WorkPiece` has not got

This is the real design work, and it is not done. Bending needs at least:

| State | Why |
|---|---|
| a curvature / radius field, or a discrete `formed` stage | there is no axis for it today (`WorkPiece.cs:35`) |
| which tool formed it | a shell rolled on conical rolls is a pipe segment, not a barrel |
| closed vs open | a seam that has been brought together is what a rivet recipe consumes |

The cheapest honest answer is probably not to extend `WorkPiece` at all but to make the roller a stage-in /
item-out station like the [shear](shear.md) - plate in, a named formed item out - because unlike rolling
there is no schedule: one pass, one curve, done. That also keeps `WorkPiece`'s planned deletion clean.
Unsettled - see [Open](#open).

---

## Numbers

There are none. Not one value for this machine exists in config, in code or in any design doc. Everything
below is either a constraint it must satisfy or a number owned elsewhere that it consumes.

### Consumed — owned elsewhere, listed so the machine can be sized against them

| Input | Value | Owner |
|---|---|---|
| `skelp` | 8 × 1 × 10 = 80 vx³ = 200 u, wide 1.0 off `castbloom` | [rolled-parts](../items/rolled-parts.md), [density rule](../mechanics/density-rule.md) |
| `boilerplate` | 15 × 1 × 16 = 240 vx³ = 600 u, wide 1.0 off either slab | [rolled-parts](../items/rolled-parts.md) |
| `heavyplate` | 12 × 2 × 10 = 240 vx³ = 600 u | [rolled-parts](../items/rolled-parts.md) |
| `game:metalplate` | 9 × 1 × 9 = 81 vx³ = 200 u | [rolled-parts](../items/rolled-parts.md) |
| handling limit any bent piece must respect | ≤ 32 lengthwise / ≤ 48 crosswise | [recoverability](../mechanics/recoverability.md) |
| drive headroom one bridged waterwheel leaves | ≈ 0.4 N·m | [mp-energy](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/mp-energy.md), via [rolling mill](rolling-mill.md) § Worked pass |
| network standing resistance | `MpIdleTorque` 0.5 N·m, `MpFrictionCoeff` 0.05, `MpMaxSpeed` 2 rad/s | `ExlibConfig.cs:92`, `:86`, `:98` |

### Unchosen — every one of these

| Quantity | Note |
|---|---|
| `LoadTorque` while bending | must sit inside the headroom above, or the machine is a steam-tier-only build by accident |
| whether bending needs heat | boiler plate was cold-rolled to shape in period, and cold bending is the historically normal case - but the mill's whole idiom is hot work. If it is cold work, this is the first cold-forming machine in the suite, and that is a design statement rather than an omission |
| pass duration | — |
| tooling `MinTorque` | the field exists on `RollSetSpec` (`RollSetSpec.cs:37`) and is parsed, stored, validated and never read; the [shear](shear.md) claims its first real use |
| how many plates make a shell / a barrel / a rim | this decides whether the fabricated substitutes are cheaper or dearer than the cast originals, which is the entire point of D2 |
| rivet count per fabricated part | ditto |

The last two are the balance question that matters, and nothing anywhere has answered it: "cast when you have
a cupola, fabricate when you have a mill" ([STATE.md:234-236](../../../../docs/plans/STATE.md)) only works if the two
routes cost comparably.

---

## Drops

Not designed. The pattern it must follow ([rolling mill](rolling-mill.md) § Drops): the block returns itself,
the fitted tooling, and any piece in the rolls. Nothing may be destroyed on break.

---

## Code — where it will hook in

| To build | Copy from | file:line |
|---|---|---|
| the block | `BlockNetworkNode` with `NetworkType => "mpenergy"` | `BlockRollingMill.cs:31`, `:36` |
| the block entity | `BlockEntityNetworkNode` + `IMpEnergyConsumer` | `BlockEntityRollingMill.cs:33` |
| the consumer contract | `LoadTorque(speed)` - deliberately speed-independent for plastic work | `BlockEntityRollingMill.cs:314-326`, reasoning at `:306-312` |
| the tooling item + fit gesture | `TryFitRollSet` / `FitRollSet`, and the `rollset`-attribute pattern | `BlockEntityRollingMill.cs:144-154`, `BlockRollingMill.cs:293-320` |
| the tooling spec record | `RollSetSpec` for the shape, `ItemDie` for the contract | `RollSetSpec.cs:31`; [heading machine](heading-machine.md) |
| load-time tooling validation | `RollSetValidation.Validate` on `AssetsFinalize` | `RollSetValidation.cs:20-32` |
| the wrench recovery | `ReleaseStuckPiece` | `BlockEntityRollingMill.cs:218-229` |
| the code-first def + RCC stages | `ExBlockDef` / `ConstructionStages` | [recipes & config](../mechanics/recipes-config.md) |

Do not copy `RollingPass`. None of it applies - see [Role](#role).

---

## Gotchas

* The settled placement is iiex ([STATE.md:476](../../../../docs/plans/STATE.md)), and the machine is the general
  bender, not a pipe-only one.
* It is not an upgrade to the mill and must never be modelled as a roll set. The `gaps` array would have to
  mean radius; `RollSetSpec.IsWide` (`RollSetSpec.cs:70`) already means "one gap = one stand in a train",
  and overloading it a second way is how the roll-set idiom rots.
* `RollSetSpec.Outputs` is a `Dictionary<float, string>` keyed on gap and compared with `==`
  (`RollSetSpec.cs:95-101`). If the bender's tooling is written against that record it inherits a
  float-equality lookup keyed on a quantity it does not have.
* `skelp` does not exist, and neither does `boilerplate`, so this machine has no input today. Its outputs
  are not the problem: `cast-barrel` (200 u), `castshell` and `castwheelsection` (600 u each) all ship as
  cast items. The structural pair are pinned at exactly the 600 u the bent equivalents are costed at, so the
  fabricated route can be a second recipe producing the same item rather than a new one
  ([cast parts](../items/cast-parts.md)).
* hpex's rolled pipe tier is uncraftable - B5: four live blocktypes, four shapes, zero recipes
  ([STATE.md:52](../../../../docs/plans/STATE.md)). The skelp → segment → bell-weld → rolled pipe chain through this
  machine is the intended fix, so B5 and this page are the same work item.
* A rolled run cannot use iiex's fittings. The welded joint family means rolled (hpex) pipe joins only
  rolled pipe; there is no valve, outlet or passthrough for it until hpex ships its own
  ([pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md)). Making rolled pipe reachable therefore does not by itself
  make an HP line buildable.
* Cast iron cannot be bent any more than it can be rolled - it shatters
  ([cast-parts](../items/cast-parts.md)). Every input to this machine is wrought or steel, which is another
  reason it cannot be an iiex machine even though its frame is cast.
* There is no mill roll set that produces pipe. The mill makes the skelp; the weld and the curl are this
  machine's.

---

## Open

| # | Question | Weight |
|---|---|---|
| 1 | Station or work-piece machine? Plate in → named formed item out (like the [shear](shear.md)), or a `WorkPiece` that gains a curvature axis. The station reading is simpler, matches "one pass, one curve", and avoids extending a record that is scheduled for deletion | decides everything below |
| 2 | Hot or cold? Bending plate cold is the period-normal case and would make this the suite's first cold-forming machine. If it is hot, it needs the [reheat furnace](reheat-furnace.md) in its loop and every piece gains a heat budget | |
| 3 | Footprint. 1 × 1 like the benches, or wide enough to read as a plate roll? The pieces are 15 voxels across | |
| 4 | The fabrication balance - plates + rivets per `castshell` / `cast-barrel` / `castwheelsection`, against the cast originals' cupola cost. D2's whole "cast vs fabricate is a real choice" claim rests on it | |
| 5 | Does it need its own flywheel? The three die benches are pulsed loads and each carries one ([heading machine](heading-machine.md)); a bending roll is a steady draw, so it may be the first `mpenergy` consumer that is not pulsed | |
| 6 | The bell-weld step. `skelp` → pipe segment implies a weld, and welding is not a verb the suite has. Is it a stage on this machine, a grid recipe, or does the roller simply output the segment? | |
| 7 | Nothing to draw from. No shape reference has been chosen and no art exists | |
| 8 | A powered riveter as a later throughput upgrade is explicitly allowed but undesigned ([STATE.md:437-438](../../../../docs/plans/STATE.md)) | — |
