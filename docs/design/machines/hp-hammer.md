# HP steam hammer (double-action)

**Status** designed - nothing built, nothing drawn. There is no block, no block entity, no die item, no
recipe, no shape, no lang key and no config section. Its parent, the LP [steam hammer](steam-hammer.md), is
also unbuilt, so nothing here can be started until that page's Open list is cleared.
**Mod** hpex (`HighPressureExpanded`)

## Owns

Canonical only for what makes the HP hammer a different machine from the LP one. Everything true of both
belongs to [steam hammer](steam-hammer.md) and is linked, never restated.

* **Double action** - steam admitted on both strokes, against the LP machine's steam-lift / gravity-drop
  single action: what it changes physically, what it changes in the sim, and why it is the only reason this
  block exists;
* the die-complexity gate read from the HP side - that `≥ 2` voxel output thickness is this machine's
  admission ticket, that the gate lives on the die and not on the block, and the finding that the ≥ 2-voxel
  side of the catalogue is empty;
* the structural consequence of driving the ram down: why the LP hammer's cast-iron frame argument does not
  carry over, and how that lands on N3's cast ↔ fabricated substitution;
* this machine's HP power contract - which band it must sit in, against which live numbers, and the three
  mutually contradictory HP bands already in the docs;
* the state of the hadfield material gate as it applies here (it does not exist in code);
* that it does not shear either.

## Does not own — cited only, never restated

| Fact | Owner |
|---|---|
| the megablock footprint, the docked anvil, the die-set-renders-both-faces rule, the four-state animation machine, the staged work-item renderer, the drawn art, drops, the shingling job, the stamping job | [steam hammer](steam-hammer.md) |
| the shear-cuts-across / die-cuts-out rule, the crop, the cold-cut `MinTorque` gate | [shear](shear.md) |
| the `ItemDie` tooling contract, its fields, and who ships each die | [heading machine](heading-machine.md) |
| pipe tiers, joint families, burst, `LitresPerPipe`, the one-pool pressure model, valves | [pipe network](../mechanics/pipe-network.md) |
| where HP steam comes from and what raises it | [boiler-lancashire](boiler-lancashire.md) |
| the filler footprint system, behaviour-capable filler cells, interaction rerouting | [multiblock & fillers](../mechanics/multiblock.md) |
| code-first defs, RCC stages, the cost catalogue, goldens | [recipes & config](../mechanics/recipes-config.md) |
| `1 vx³ = 2.5 u` and every mass in the stock ladder | [density rule](../mechanics/density-rule.md) |
| the ≤ 32 / ≤ 48 handling limits | [recoverability](../mechanics/recoverability.md) |
| what hadfield, crucible steel and HSS are | [materials.md](../materials.md) |
| the placement rule that assigns machines to mods | [STATE.md § placement rule](../../internal/plans/STATE.md) |

**Depends on** [steam hammer](steam-hammer.md) · [pipe network](../mechanics/pipe-network.md) ·
[heading machine](heading-machine.md) · [shear](shear.md) ·
[multiblock & fillers](../mechanics/multiblock.md) · [recipes & config](../mechanics/recipes-config.md) ·
[density rule](../mechanics/density-rule.md) · [boiler-cornish](boiler-cornish.md) (the FSM the Lancashire
inherits) · [materials.md](../materials.md) · [STATE.md](../../internal/plans/STATE.md)

---

## Role

The ram is driven down, not dropped. Everything else follows from that.

| | LP hammer ([steam hammer](steam-hammer.md)) | **this machine** |
|---|---|---|
| Cylinder | single-acting - steam under the piston lifts the tup, the admission valve closes, gravity does the work | double-acting - steam above the piston on the down stroke as well |
| Blow energy | `m·g·h` - fixed by the tup mass and the lift | `m·g·h + p·A·h` - scales with inlet pressure |
| Blow rate | limited by free-fall | faster, because the return stroke is powered too |
| Blow control | one blow is one blow | the blow is a quantity the player's supply pressure sets |
| Products | ≤ 1 voxel thick - strips, thin blanks, the `boilerplate` stamp | ≥ 2 voxels - thick, complex, closed-die work |

The gate is a thickness, not a tier tag: a gravity drop delivers a fixed energy, and past one voxel of
thickness the same blow only dents the work, because the work needed to fill a die scales with the volume
displaced. Adding `p·A` to the falling weight is the historical fix (Nasmyth 1839, single-acting; the
double-acting version followed for heavy closed-die forging).

It is hpex because of what it feeds. Under the placement rule
([STATE.md § placement rule](../../internal/plans/STATE.md)) a machine lives with the content it makes possible, and
the ≥ 2-voxel forgings this machine is for are the heavy rotating parts of the HP engines - a Corliss crank,
a connecting rod, a piston rod, the [bearings](bearings.md) housings - plus its own hardened die sets. None
of those exist as items; see [Open](#open).

### The HP hammer does not shear either

[shear](shear.md)'s rule applies unchanged at every tier: the shear cuts across, the die cuts out. A
straight-line part is the iiex MP shear's job whatever the metal, and the cold-cut question is decided by
`MinTorque` at the shear, not by steam pressure here.

---

## Structure

Not decided; the default is to copy the LP hammer until a reason appears not to. The megablock footprint, the
filler pattern, the lever cell and the docked anvil are [steam hammer](steam-hammer.md)'s.

| Aspect | Ruling |
|---|---|
| Footprint | inherit the sparse 3 × 3 × 3 megablock. A double-acting cylinder is not physically larger - it is the same cylinder with a second port and different valve gear |
| Network faces | inherit the count: one inlet, one exhaust. Double action changes when steam is admitted inside the cylinder, not how many pipes reach the machine. Do not model the second admission as a second pipe connector - the pipe model is one pool and a second face buys nothing ([pipe network](../mechanics/pipe-network.md)) |
| Anvil | inherit the docked-anvil block and the die-set idiom |
| Lever cell | inherit the behaviour-capable filler |

### The one structural fact that does *not* inherit: the frame

[steam hammer](steam-hammer.md) puts the frame standards, hammer mass and anvil in cast iron because they are
pure compression members - the ram only ever falls onto them. A powered down stroke puts the standards in
tension on every blow and reverses the load on the tie between them, and cast iron is the one structural
material in the suite explicitly wrong for reversal ([steam hammer](steam-hammer.md), Construction: *"a cast
rod snaps on the first stroke"*).

So the HP hammer is the first machine whose frame must be fabricated, and the first place where N3, the
cast-iron ↔ fabricated-steel substitution ([STATE.md § N3](../../internal/plans/STATE.md)), is mandatory rather than
optional: `castframe` → beam × N + plate + rivets, assembled by a recipe, with rivets as an ingredient so no
riveting machine is required.

**Declared liberty** *(2026-08-07)*. Real double-acting hammers, from 1843 onward, kept their cast-iron
frames and decoupled the anvil onto its own foundation so the frame never took the blow. The mod's "a cast
frame snaps under the double-acting cycle" gate is a gameplay liberty, not history - kept because it hands
fastener tier N3 its first hard consumer.

---

## Assets

Nothing exists for this machine, and its parent's art is untracked.

| Asset | State |
|---|---|
| editable shape | none. No HP variant is drawn |
| runtime shape | none. `assets/hpex/shapes/` holds only the Lancashire and Cornish assemblies |
| die item art | none |
| lang keys | none in `assets/hpex/lang/en.json` |
| handbook page | none. `docs/hpex/handbook/` contains exactly one file, `00-highpressure.html` |
| the LP hammer's three shapes | drawn, untracked in git, wired to nothing - see [steam hammer § Assets](steam-hammer.md) |

A distinct block needs a distinct silhouette ([heading machine:36](heading-machine.md)). The cheapest
difference is the mechanism's own: a double-acting cylinder has steam pipework running to both ends, so the
top and bottom cylinder covers each carry a visible pipe elbow where the LP shape has one `SteamIntake` boss.
Two elements.

Whatever is drawn, the LP shape's traps apply: a looping clip must be `onAnimationEnd: Repeat` or the mesh
vanishes, the animated element is `HammerGroup/Rod` and the tup hangs off it, and a megablock animator samples
light from one cell - all at [steam hammer § Gotchas](steam-hammer.md).

---

## Construction

There is no recipe and no block to hang one on. This is a blocker, and it inherits a second one: its parent
has no recipe either ([steam hammer § Construction](steam-hammer.md)).

### The material gate is asserted and does not exist

The design states that every hpex machine is built from hadfield steel, as a hard material gate. Three
problems, all live:

| Problem | Evidence |
|---|---|
| Hadfield does not exist in code. Three source mentions, all comments; no metal descriptor, no item, no alloy recipe | `HpexConfig.cs:107`, `HighPressureExpandedModSystem.cs:40`, `MachineRecipeDefinitions.cs:59` - the last one says the gate is still waiting on hadfield |
| The two live hpex machines do not honour it. The HP builds take the plain plated iiex segment, not a hadfield one, and the source comment says tier-gating "waits on … the hadfield material gate" | `MachineRecipeDefinitions.cs:59-61` |
| A hard material lockout contradicts the settled alloy rule. D3/R5 settled that alloys inherit their base's grade as a continuous penalty, not a lockout - critical machinery built from lesser steel gets a lower max pressure | [STATE.md § D3](../../internal/plans/STATE.md) |

The grade penalty has an obvious axis on a hammer: blow energy is a function of admitted pressure, and a
weaker frame has a lower admissible pressure. Built from Bessemer structural steel it works but cannot be
pushed to the top of the band, so the thickest dies stay out of reach - R5, gate efficiency, not possibility.

### The bill — proposed

| Part | Material | Why |
|---|---|---|
| frame standards, entablature | fabricated - `beam` × N + plate + rivets (N3) | tension on every blow; see [Structure](#structure). Not `castplate-heavy` |
| anvil / sow block | cast iron or a heavy cast steel block | still pure compression; the LP argument survives here alone |
| cylinder + covers | cast, bored | the [boring machine](boring-machine.md)'s job, as with every cylinder in the suite |
| piston, rod, valve gear | steel | reversal |
| dies | forged, quench-hardened | a die takes the blow and imparts the profile ([steam hammer](steam-hammer.md)) |

`beam`, `boilerplate`, rivets and the rolled plate all do not exist yet
([steam hammer § Open](steam-hammer.md)). Every ingredient in the bill is downstream of the forming line
being finished.

---

## Operation

Every verb is the LP hammer's - hold RMB on the lever to run the blow loop, sneak + RMB to dock/undock the
anvil, a die-set item to fit tooling, stock RMB'd onto the anvil to pile it. See
[steam hammer § Operation](steam-hammer.md).

### What changes

| | LP | HP |
|---|---|---|
| `idle` | no steam ⇒ ram parked at the bottom | the same, but a choice rather than physics. A double-acting ram with no steam is held nowhere; parking it down is a decision the animation makes |
| powered idle | steam present ⇒ ram raised and held | identical |
| blow | fixed energy | energy = f(inlet pressure), so the same die may refuse at 5 atm and take at 9 |
| exhaust | one vent per blow | two - the cylinder exhausts on both strokes. A rate question, not a face question |

### The die gate, read from this side

The gate is a property of the die, not of the block: the die's output thickness gates the hammer tier. So the
implementation is not a boolean `hpex` flag on the block but a capability number the block exposes and a
requirement the die declares - the shape `ItemDie` already has for the bench (`MinTorque`,
[heading machine § the `ItemDie` spec](heading-machine.md)).

| Proposed | Where it lives | Mirrors |
|---|---|---|
| `Hammer` : `lp` \| `hp` | on the die | `ItemDie.Bench` : `heading` \| `nail` - the field that keeps the nail die off the wrong bench |
| `MinBlowEnergy` : `float` | on the die | `ItemDie.MinTorque` |
| `BlowEnergy` | on the block, derived from inlet pressure | `LoadTorque` / network drive at the mpenergy benches |

Prefer `MinBlowEnergy` and let `Hammer` be a convenience. With an energy number the "≥ 2 voxel" rule becomes
a consequence rather than a hard-coded tier: the LP hammer's fixed drop energy falls below what a 2-voxel
cavity needs, and a low-grade HP frame that cannot be pushed past 6 atm lands in the same place. One number
expresses the tier gate and D3's continuous grade penalty. The `ItemDie` contract is
[heading machine](heading-machine.md)'s; this is a proposed extension, not a decision this page can make.

### The finding: the ≥ 2-voxel side of the catalogue has no *product*

Two families are named for this machine and neither has an item behind it:
*billet → forged heavy components (open die)*, and *tools, complex/thick forgings*. Neither names a stock
form, an output code or a mass, and `StockForm.All` holds `shingledbar` and `shingledslab` only (`StockForm.cs:80`).

Every die named anywhere in the design with an actual product, and its output thickness:

| Die | Output | Thickness | Tier | Machine |
|---|---|---|---|---|
| stamping | `boilerplate` → 3 × `game:metalplate` (9 × 1 × 9) | 1 | LP | [steam hammer](steam-hammer.md) |
| nail | `game:metalnailsandstrips` | 1 | MP | [nail machine](nail-machine.md) |
| bolt | bolts from rod @ 25 u | — | MP | [heading machine](heading-machine.md) |
| rivet | rivets from rod @ 25 u | — | MP | [heading machine](heading-machine.md) |
| ball | bearing balls from chrome rod | — | MP | [heading machine](heading-machine.md) → [bearings](bearings.md) |
| **— nothing —** | | **≥ 2** | **HP** | **this machine** |

So the gate currently gates nothing. The obvious candidates from the stock ladder are all rolled products,
and giving them to a die would duplicate the mill:

| Candidate | Section | Why it cannot be the HP hammer's product |
|---|---|---|
| `beam` | 4.5 × 2 × 9 | the mill's `flat` 2.0 schedule ([stock](../items/stock.md)) |
| `blank` | 8 × 2 × 5 | wide 2.0 off `castbloom` |
| `heavyplate` | 12 × 2 × 10 | wide 2.0, and the sand cell |

The HP hammer needs a product family nothing else can make, and the placement rule says which: the heavy
forged rotating parts of the HP engines - the *forged heavy components (open die)* family made concrete. A
crank, a connecting rod, a piston rod and an eccentric cannot be cast, rolled or bent, and they are hpex
content. Its second customer is hardened die sets for itself and blade sets for the [shear](shear.md); its
third is tools, which under D9 are crucible-steel work and reach back to iiex
([crucible furnace](crucible-furnace.md)). None of the three exists as an item; see [Open](#open).

---

## Numbers — all proposed; nothing about this machine is in config

There is no `HpHammer*` key anywhere. Every row below is either a constraint it must satisfy or a number
owned elsewhere it must be sized against.

### The band it has to live in — live values

| Key | Value | file:line | What it does |
|---|---|---|---|
| `LancashireBoilerSteamPerSecond` | 48 L/s | `HpexConfig.cs:56` | the entire HP supply. Everything on the line shares it |
| `LancashireBoilerMaxOutputPressure` | 12.0 atm | `HpexConfig.cs:60` | the choke - the highest pressure the line ever reaches |
| `LancashireBoilerCapacity` | 1200 L | `HpexConfig.cs:46` | vessel |
| `CornishEngineEngagePressure{Low,Normal,High}` | 5.0 / 6.0 / 7.0 atm | `HpexConfig.cs:70-72` | the only live HP band |
| `CornishEngineBreakPressure{Low,Normal,High}` | 8.0 / 8.0 / 8.0 atm | `HpexConfig.cs:76-78` | the live HP consumer breaks at 8 |
| `CornishEngineSteam{Low,Normal,High}` | 8 / 16 / 32 L/s | `HpexConfig.cs:84-86` | the precedent for a per-setting draw, and it eats up to ⅔ of the boiler alone |
| `RolledPipeBurstPressure` | 12 atm | `HpexConfig.cs:115` | exactly equal to the boiler choke - zero headroom |
| `RccBrokenDropsRatio` (hpex) | 0.8 | `HpexConfig.cs:124` | salvage; the HP hammer will inherit it |
| `RecipeLevel` (hpex) | `"normal"` | `HpexConfig.cs:131` | the cost tier its grid recipe would be priced at |
| Watt engine draw (LP, for scale) | 30 L/s fixed | [engine-watt](engine-watt.md) | the LP comparison the LP hammer was told to size against |

### The documented HP band and the shipped one

| Source | Claim |
|---|---|
| `src/HighPressureExpanded/README.md:15` | the Cornish engine is 6-8 atm |
| `docs/hpex/handbook/00-highpressure.html:19` | running at 6-8 atm |
| the shipped config | engages at 5/6/7, breaks at 8 (`HpexConfig.cs:70-78`) |

The HP hammer is the natural occupant of the 8-12 atm band that nothing lives in today - a hammer has no
break pressure; more steam is simply a harder blow. That must be a decision, not an accident: if the hammer
takes 8-12, then hpex has two distinct sub-bands and the docs must say so.

### Proposed sizing

| Quantity | Proposal | Constraint it satisfies |
|---|---|---|
| minimum inlet to raise the ram | ≈ 4 atm | above the LP tier's ceiling (the Cornish boiler chokes at 5, [boiler-cornish](boiler-cornish.md)) so an LP line cannot run it |
| working band | 6 → 12 atm, blow energy rising across it | occupies the empty upper band; no break point |
| steam draw | 24 L/s while the lever is held, 0 idle | half the Lancashire, so one boiler runs a hammer and a `Low` Cornish (8) with 16 L/s spare |
| blow interval | 0.6 s | faster than a gravity drop by construction; unchosen |
| exhaust | 2 vents per blow | the double stroke; unchosen as a rate |
| die durability | none | matches the LP hammer's non-decision; do not invent one for HP alone |

Every one of these is a proposal with no source. The one recorded rate near the hammer family - *~3 s per
pass*, from the retired combined forming-shop megablock - describes the mill. Do not adopt it, at either
tier.

---

## Drops

Inherit [steam hammer § Drops](steam-hammer.md) unchanged: the principal returns the machine, the fitted
die-set and anything piled on the anvil; a filler routes to the principal; the docked anvil carries its
installed die out with it. Nothing may be destroyed on break.

The one hpex-specific number is the salvage ratio: `RccBrokenDropsRatio` 0.8 (`HpexConfig.cs:124`) - hpex
carries its own because exlib's salvage lookup keys on the broken block's domain.

---

## Code — where it will hook in

Nothing exists; `grep -ri "hammer" src/HighPressureExpanded/` returns nothing. The build order is strictly
after its parent's.

| To build | Copy from | Note |
|---|---|---|
| the whole block | the LP [steam hammer](steam-hammer.md), once it exists | the LP class should be the base and this the leaf - the same relationship `BlockBoiler`/Lancashire and `BlockEngine`/Cornish already have |
| the leaf pattern | `src/HighPressureExpanded/BlockStructures/` - the Lancashire and Cornish engine are pure leaves over iiex bases | `HighPressureExpanded.csproj:81` documents the rule: hpex holds only the high-pressure leaves and inherits everything from iiex |
| pressure-banded behaviour | `CornishEngine{Engage,Break}Pressure*` and its three-setting wrench throttle | `HpexConfig.cs:70-78` |
| config | a new `HpHammer*` block in `HpexConfig` | `HpexConfig.cs:23` - `[ExConfigRegister("ex_values.json", "hpex", Manageable = true)]` |
| costs | `HpexRecipeConfig` | `src/HighPressureExpanded/HpexRecipeConfig.cs` |
| the die spec extension | [heading machine](heading-machine.md)'s `ItemDie` | needs that page's sign-off - see [Operation](#operation) |

The dependency this machine would need is declared but not real. `modinfo.json:14` declares a `smex`
dependency; the csproj has no `smex` project reference and explicitly documents the omission
(`HighPressureExpanded.csproj:81`, *"It does NOT reference smex"*). So an HP hammer that wants to know about
a smex material (hadfield, or a steel grade) cannot ask in code today - it can only ask through JSON
ingredient codes, which is why the hadfield gate is still a comment.

---

## Gotchas

* Nothing is built and the parent is not built either. Any estimate that treats this as "the LP hammer
  plus a flag" is wrong by the whole LP hammer.
* The gate has no product behind it. The ≥ 2-voxel die catalogue is empty; see
  [Operation](#operation). Shipping the block before a die exists gives the player a machine that does
  nothing - the dangling-end failure the placement rule forbids ([heading machine:170](heading-machine.md)).
* Stamping is LP work. Stamping a `boilerplate` into three `metalplate` is a 1-voxel output and therefore
  the LP hammer's job by the gate; this machine's side of the catalogue is thick, complex work only.
* Do not add a second pipe face for the second admission. The pipe network is one live pool
  ([pipe network](../mechanics/pipe-network.md)); a second inlet face would model nothing and would double
  the connector surface for free.
* A refused joint does not leak (B18). `ClassifyOpenings` counts an open face as a leak only when the
  neighbour is air (`PipeNetwork.cs:632` vs `BlockNetworkNode.cs:751-754`), so if this machine is given a
  welded (hpex) joint family and the player butts a cast segment against it, they get no leak, no warning,
  no signal - the machine simply never powers. Decide the joint family deliberately.
* The pressure valve cannot protect it (B6). hpex's "mandatory" valve is iiex-domain and flanged, so
  welded hpex pipe refuses to couple it, and its gate clamps to its own 5 atm burst, below the Cornish
  engine's 6/7 engage pressures (`BlockPipe.cs:239`, `BlockEntityPressureValve.cs:41`, `HpexConfig.cs:70-78`).
  A hammer with no break pressure does not need the valve, which is a point in favour of the 8-12 band, but
  do not claim the valve as protection.
* `RolledPipeBurstPressure` (12) equals the boiler choke (12) - `HpexConfig.cs:115` vs `:60`. A hammer
  drawing hard can only lower line pressure, so it does not worsen this, but any headroom argument that
  assumes a margin is wrong.
* The rolled pipe tier is uncraftable (B5) - four live blocktypes, four shapes, zero recipes
  ([STATE.md:52](../../internal/plans/STATE.md); `src/HighPressureExpanded/Recipes/` contains exactly one file). So an
  HP hammer plumbed in "HP pipe" would be plumbed in a tier the player cannot build.
* hadfield is a comment, not a material - see [Construction](#construction).
* The LP hammer's drawn mesh already overhangs its declared footprint (57 voxels tall against 48 for
  three cells). Do not copy the footprint from the art without reading
  [steam hammer § Gotchas](steam-hammer.md) first.

---

## Open

| # | Question | Blocking? |
|---|---|---|
| 1 | **Is it a separate block at all, or an upgrade to the LP hammer?** A double-acting cylinder is a valve-gear change on the same frame; historically hammers were rebuilt this way. An upgrade would mirror the mill's "one machine, more tooling" rule and would avoid a near-duplicate megablock - but the frame argument in [Structure](#structure) says the standards must change too, which argues for a distinct block. Undecided, and it is the first decision | yes |
| 2 | **What does it make?** The ≥ 2-voxel die catalogue is empty. Nothing can be built until at least one HP die and its product item are named | yes |
| 3 | **Blow energy as a number, or a boolean tier tag?** This page argues for `MinBlowEnergy` on the die; the contract belongs to [heading machine](heading-machine.md) | yes |
| 4 | **Which band?** 8-12, or 6-12 overlapping the Cornish engine. Whichever is chosen, the README and the handbook must be reconciled with the config - they currently disagree | yes |
| 5 | **Does the hadfield gate become a grade penalty (D3) or stay a lockout?** The design asserts a lockout; the settled alloy rule says penalty; the code says neither, because hadfield does not exist | yes |
| 6 | Everything on the LP hammer's Open list, since this machine is downstream of all of it - including that `shingledslab`, the wrought ball and `boilerplate` do not exist | yes |
| 7 | **Does it inherit the shingling job?** An HP hammer could shingle, and would do it faster. Letting it would make the LP machine redundant at the steel tier; refusing it means one machine that only ever forges. The suite's pattern is that later tiers extend rather than replace, which argues for inheriting it | |
| 8 | **Art**: nothing drawn. The double-pipe cylinder is the cheapest legible difference | |
| 9 | **Where a heavy forging's heat comes from.** A crank is far bigger than anything the [reheat furnace](reheat-furnace.md) is sized for, and no temperature gate is designed at either hammer tier | |
| 10 | Whether "HP steam hammer" is even the right name once the Corliss exists and hpex has two engines competing for one 48 L/s boiler. Sizing decision, not a naming one | |
