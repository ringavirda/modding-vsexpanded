# Casting

**Status** partial - the loop is live at two stations (the bed and the cell) and unbuilt at the third (the
long cell)
**Mods** iiex (all three sand stations, the sand, the patterns for iron parts) · iiex (machine-part patterns -
none shipped) · smex (the steel the long cell would take)

**Owns** - the facts this page is canonical for:

* the sand loop as a sequence of player verbs, and what each verb costs at each station;
* the sand-is-not-consumed rule: shake-out destroys the impression, never the sand, and the two different
  ways the three stations recover from that;
* the pattern-carries-the-spec rule as a suite-wide principle - one item carries the whole specification, no
  station names a product, adding a castable part is a data-only act, and one of the three stations does not
  obey it. The `mold` attribute schema and its validation are
  [casting cell](../machines/casting-cell.md)'s; this page owns the principle and its scope;
* the three casting routes, the furnace that feeds each, and why they are not interchangeable;
* the split between sand casting and permanent-mold casting (tool molds parked on the canal tap and
  pedestal);
* the process-level arithmetic: clicks per casting, castings per pattern, and the one-time sand cost of
  commissioning each station;
* the casting-shape asset census - how many shapes are drawn, how many are tracked, how many are wired.

**Does not own** - cited only, never restated:
[casting bed](../machines/casting-bed.md) - the slot model, the 20-casting capacity, the carve/harvest verbs,
the denomination, and the slag-brick dispatch ·
[casting cell](../machines/casting-cell.md) - the `mold` attribute schema, `MoldSpec`, the `Decide`
precedence table, the shipped pattern catalogue and every cavity capacity, pattern durability, misrun and
short pour, the launder-face intake rule, and the green-sand craft ·
[long cell](../machines/long-cell.md) - its drawn geometry, its measured cavities, its lane ladder, and the
build order ·
[molten canal](../machines/molten-canal.md) - the runs that feed every station, the tap and the pedestal,
the large/small mold split and the clay heat gate ·
[molten network](../mechanics/molten-network.md) - `IMoltenCell`, `BEBehaviorMoltenCell`, the flow driver,
pull rates, the solidify/hardened model ·
[density rule](../mechanics/density-rule.md) - 1 vx³ = 2.5 u and the measured mass of every shipped item ·
[recoverability](../mechanics/recoverability.md) - the ≤ 32 / ≤ 48 handling invariant ·
[cold blast furnace](../machines/blast-furnace-cold.md) · [hot blast furnace](../machines/blast-furnace-hot.md)
· [cupola](../machines/cupola.md) · [Bessemer](../machines/bessemer.md) ·
[open hearth](../machines/open-hearth.md) · [ladle](../machines/ladle.md) - the furnaces at the head of each
route · [puddling furnace](../machines/puddling-furnace.md) - the biggest consumer of solid pigs ·
[direct charging](direct-charging.md) - the route that skips casting entirely ·
[pig](../items/pig.md) · [cast parts](../items/cast-parts.md) · [stock](../items/stock.md) - the item families
· [recipes & config](../mechanics/recipes-config.md)

---

## What it is

Green-sand casting: pack damp clay-bound sand around a wooden **pattern**, take the pattern out, pour metal
into the void it left, break the sand away.

What the real process has that the mod abstracts away:

| Real step | In the mod | Why |
|---|---|---|
| Two-part flask - ram the drag, turn it, ram the cope, draw the pattern, close up | one rammed body, one right-click | the cope/drag split is bookkeeping, not a decision |
| Sprue, runner, riser, in-gates designed per casting | only the [bed](../machines/casting-bed.md) has a runner, carved as its spine | gating design is not learnable in a block game |
| Cores set into the mold for hollow work | none - the cast barrel is drawn as a hollow cavity, not cored | the cored mold is designed and unbuilt ([casting cell](../machines/casting-cell.md) § Open) |
| Fettling - cut the gates and risers off, ~10 % scrap | none; capacity is exactly the cavity | left open ([casting cell](../machines/casting-cell.md) § Open) |
| Reconditioning - mill the shake-out sand, re-temper with water and clay | none; sand never degrades | see § The loop |
| Pattern-making from drawings | diagram → knife → planks (`PatternRecipeDefinitions.cs:22-39`) | kept; it gates what can be cast |

The mod adds one thing the real process does not have: the pattern is also the specification. It carries the
cavity, the capacity, the output item and the minimum pour temperature as data.

---

## The loop

Five verbs. Every station runs the same five; they differ only in who pays for what.

```
COMMISSION   ram the station with sand           once, ever
IMPRESS      form the cavity                     once per casting
POUR         canal delivers; the station pulls   unattended
FREEZE       wait for the metal to harden        unattended
SHAKE OUT    right-click empty-handed            once per casting  → the impression is gone, the sand is not
```

| Verb | Casting bed | Casting cell | Long cell |
|---|---|---|---|
| **Commission** | 12 × `game:sand-{sand}` as RCC stage 3 (`BlockSandCastingBed.cs:84-93`) | 1 × `iiex:greensand`, `RamSand` (`CastingCellLogic.cs:78-79`) | — |
| **Impress** | carve - a bare right-click on a slot cell; no material, no pattern (`BlockEntitySandCastingBed.cs:343`) | ram a `pattern-*`, costing 1 of its durability (`CastingCellLogic.cs:81-82`) | — |
| **Pour** | pulls from any horizontal neighbour of the principal (`BlockEntitySandCastingBed.cs:267-287`) | pulls from the launder face only (`BlockEntitySandCastingCell.cs:155-174`) | — |
| **Freeze** | per-cell thermal tick; a mold hoards its charge because a drain fitting never gives back (`BlockEntitySandCastingBed.cs:302-303`) | same model, one cell | — |
| **Shake out** | harvest → pigs/chunks/bits or slag bricks; slot drops to plain sand and stops being a channel until re-carved (`:402-406`) | harvest → the spec's output; sand rakes back to `Full` and is immediately re-impressible (`CastingCellLogic.cs:98`) | — |

The sand is never consumed after commissioning. `CastingCellLogic.AfterShakeOut` is the constant that says so
(`CastingCellLogic.cs:98`), and the bed says the same thing by leaving the slot as sand rather than removing
it. The standing cost of a casting operation is labour, not material, so the loop has no reconditioning step:
nothing was spent.

The two stations pay that labour differently:

* the cell re-impresses for one pattern-durability point - free in materials, but the pattern is a consumable
  tool that runs out;
* the bed re-impresses for one bare click and nothing else - a carve costs no item at all, forever.

---

## The three routes

Three casting routes, fed by three different furnaces. They are not alternatives; each is the only way to get
its own product, and each is gated by which furnace the player has built.

| Route | Melting furnace | Station | What comes out | State today |
|---|---|---|---|---|
| **Bulk** | [cold](../machines/blast-furnace-cold.md) / [hot blast furnace](../machines/blast-furnace-hot.md) - `MetalProductCode = "pigiron"` (`BlockEntityShaftFurnace.cs:151`) | [casting bed](../machines/casting-bed.md) | `iiex:pig` / `pigchunk` / `pigbit`, and `iiex:slagbrick` from the same carve | live |
| **Parts** | [cupola](../machines/cupola.md) - `MetalProductCode = "castiron"` (`BlockEntityCupolaFurnace.cs:95`) | [casting cell](../machines/casting-cell.md) | `castplate-heavy`, `cast-barrel`, and the two iron molds | live |
| **Stock** | [Bessemer](../machines/bessemer.md) / [open hearth](../machines/open-hearth.md), through the [ladle](../machines/ladle.md) | [long cell](../machines/long-cell.md) | `castbillet` · `castbloom` · `castslab` | block does not exist; ladle does not exist |

Why the split holds:

1. A heat has to go somewhere the instant it is tapped. A blast-furnace campaign is thousands of units, so
   the bulk route has to be the one with no per-casting cost. That is the bed.
2. A part is a decision, not a stream, so the parts route charges an impression per unit and is fed by the
   small batch furnace ([cupola § It is small on purpose](../machines/cupola.md)).
3. The 1 × 1 cell tops out at a 12-voxel cavity; a slab, bloom or billet is longer than that, which is the
   long cell's reason to exist ([long cell § Role](../machines/long-cell.md)).

### The fourth route is not sand casting

Metal can also be poured into a **permanent mold** - a fired-clay tool mold on the
[pedestal](../machines/molten-canal.md), or a cast-iron one under the tap. The mold is reusable and the
cavity is fixed: no ramming, no pattern, no shake-out, and no way to make a shape the mold-maker did not
anticipate.

The two technologies are wired to each other: the cell's first products are the iron molds that replace the
clay ones (`iiex:castmold-plate`, `iiex:castmold-doubleingot`, `PatternItemDefinitions.cs:81-94`), and those
molds then demand 200 u a fill and drop a `game:metalplate-{metal}` or two `game:ingot-{metal}`
(`BlockCastMold.cs:50-51`). Clay caps out below iron temperatures ([molten canal § the clay heat
gate](../machines/molten-canal.md)); sand casting is the way past the cap.

---

## The pattern-carries-the-spec rule

A station never names a product. The casting cell reads what to cast off the held stack, and matches a
pattern by `FirstCodePart() == "pattern"` alone (`BlockEntitySandCastingCell.cs:188`), domain-blind. A mod
adds a castable part with one pattern item and one filling shape, and iiex does not change and does not learn
that mod's name.

The three consequences that belong to the process rather than to the station:

| Consequence | Evidence |
|---|---|
| The catalogue is data, so it derives its own crafts. `PatternTypes` is `[.. Molds.Keys]`, and both the diagrams and the diagram→pattern recipes are generated from it | `PatternItemDefinitions.cs:115`, `PatternRecipeDefinitions.cs:23` |
| A pattern is a tool that wears, so the catalogue also sets the run length: one pattern is a fixed number of castings and then it is re-carved from its (reusable) diagram | `PatternRecipeDefinitions.cs:28`, durability cited from [casting cell](../machines/casting-cell.md) |
| The pattern is per type, not per wood - twelve woods of one type cast the same part | `PatternItemDefinitions.cs:144-147` |

One of the three stations does not obey the rule. The [casting bed](../machines/casting-bed.md) has no
patterns at all: its cavity is a pure function of which row was clicked (`SandBedLayout.ImpressionsPerMold`,
`SandBedLayout.cs:147`), hard-coded from the art's geometry. That is correct for the bulk route - the
denomination of a pig is not a choice - but it means the extension point covers one shipped station, not
three, and the long cell would be the first test of whether the rule scales to a multi-cell impression.

---

## Inputs and outputs

Shipped constants are [density rule](../mechanics/density-rule.md)'s measurements; settled targets are the
item pages'.

| Route | In | Out | Mass, shipped | Mass, settled |
|---|---|---|---|---|
| Bulk | molten pig off the furnace tap | `iiex:pig` | 375 u (`Items/ItemPig.cs:39`) | 375 u - landed |
| Bulk | — | `iiex:pigchunk` / `pigbit` | 25 / 5 u (`ItemPig.cs:40-41`) | landed with the pig |
| Bulk | molten slag off the upper tap, same carve | `iiex:slagbrick` | `= PigUnits` (`SlagItemDefinitions.cs:27`) | follows the pig |
| Parts | molten cast iron | `iiex:castplate-heavy` | 160 u (`CastPartItemDefinitions.cs:21`) | 500 u as a cast plate; the 600 u `heavyplate` is a separate rolled item |
| Parts | molten cast iron | `iiex:cast-barrel` | 200 u (`CastPartItemDefinitions.cs:24`) | unchanged |
| Parts | molten cast iron | `iiex:castmold-plate` (block) | 136 u cavity (`PatternItemDefinitions.cs:83`) | open |
| Parts | molten cast iron | `iiex:castmold-doubleingot` (block) | 152 u cavity (`:90`) | open |
| Stock | molten steel via the ladle | `castbillet` 3 × 3 × 27 | no item exists | 243 vx³ |
| Stock | molten steel via the ladle | `castbloom` 4 × 4 × 25 | no item exists | 400 vx³ → 1000 u |
| Stock | molten steel via the ladle | `castslab` 12 × 4 × 25 | no item exists | 1200 vx³ → 3000 u |

Failure outputs, both owned by [casting cell](../machines/casting-cell.md): a misrun (cavity filled below
`minPourTemp`) and a short pour (cavity never filled) both yield recovered scrap of whatever metal was in the
cavity, never the part. The bed has no equivalent - a runner's stranded charge comes back as recovered bits
(`BlockEntitySandCastingBed.cs:426-436`).

---

## Numbers

Process arithmetic only. Every input is cited; the derivation is this page's.

### Labour per casting

| Station | Clicks | Derivation |
|---|---|---|
| Casting cell | 2 per casting + 1 ram, once | `Imprint` then `Harvest` (`CastingCellLogic.cs:81`, `:71-74`) |
| Casting bed | 2 per slot, amortised over 2 or 3 impressions ⇒ 0.67 – 1.0 per casting | one carve + one harvest per slot; a slot holds `ImpressionsPerMold` castings (`SandBedLayout.cs:147`) |
| Casting bed, full cycle | 8 harvests + 8 re-carves = 16 clicks for 20 castings | 8 mold slots (`SandBedLayout.cs:150-151`) |

The bed is not zero-labour per casting, as [casting bed § Role](../machines/casting-bed.md) states: it is 0.8
clicks per casting against the cell's 2, a 2.5× advantage rather than a free one.

### Material per run

| Quantity | Derivation | Result |
|---|---|---|
| Green sand needed by a cell, ever | one `RamSand`, never returned (`CastingCellLogic.cs:98`) | 1 item |
| Cells commissioned by one green-sand craft | recipe yields 8 (`CastingRecipeDefinitions.cs:47`) from 8 sand + 1 blue clay (`:43`) | 8 cells per blue clay |
| Sand cost of a bed's 20 slots | RCC stage 3, once (`BlockSandCastingBed.cs:84-93`) | 12 sand blocks, forever |
| Material per pattern-lifetime of castings | one craft: diagram (tool, reusable) + knife (tool) + 2 planks (`PatternRecipeDefinitions.cs:26-37`) | 2 planks |

### Route capacity

| Quantity | Derivation | Result |
|---|---|---|
| Metal a fully carved bed absorbs | 20 castings × 375 u | 7500 u - the number behind the "one campaign ≈ one bed" anchor ([ironmaking](ironmaking.md) § Derived) |
| Heavy plates from one full cupola charge | 3200 u ([cupola § Rates](../machines/cupola.md)) ÷ 160 u | 20 plates |
| Cast barrels from the same charge | 3200 ÷ 200 | 16 barrels |
| Iron molds from the same charge | 3200 ÷ 136 | 23 plate molds |

The converter-charge-to-slab arithmetic is [Bessemer § Open #2](../machines/bessemer.md)'s.

### Asset census — `assets/iiex/shapes/casting/`

| Measure | Count | Note |
|---|---|---|
| Shape files present | 21 | 12 `cell-filling-*`, 6 `longcell-filling-*`, 3 shells |
| Referenced anywhere in `src/` | 8 | the 4 pattern fillings + `cell-filling-base` + `cell-filling-half` + the bed and cell shells |
| Drawn with no consumer | 13 | 6 orphan cell fillings + all 7 long-cell files |
| Untracked in git (`??`) | 14 | the 13 orphans plus `sandcastingbed.json`, which the live bed block loads |

Caution: the editable sources for the casting family are marked deleted (`D`) in the working tree, and the
redrawn long-cell fillings use different names (`molten-sandlongcellfilling-*.json`, not
`sandcasting-longcell-filling*.json`) - the naming in [long cell § Assets](../machines/long-cell.md) is stale
against the tree.

---

## Why it is like this

* Cast iron cannot be forged: a pig on the anvil only shatters into denominations (`ItemPig.cs:86-141`).
  Casting is the only way out of cast iron; were it forgeable, the
  [puddling furnace](../machines/puddling-furnace.md) would have no reason to exist.
* Sand is the general technology: a permanent mold is one shape, already owned; sand is any shape a positive
  can be carved for. The iron molds sit at the end of a sand-casting chain, the historical order.
* Sand is not a consumable because foundry sand is milled and re-tempered. The loop charges the ram-up:
  [R2](../conventions.md)'s "declared recovery, nothing hidden" applied to a material that comes back.
* A casting station is a hole full of sand, not a machine that knows what it makes, so the pattern carries
  the spec. That is the cross-mod extension point: iiex's machine parts arrive as pattern definitions with
  no iiex change.
* The three products have different shapes of decision: bulk pig is a stream that must not need babysitting,
  a machine part is a build, stock is volume only a converter can supply.

---

## Gotchas

1. **The stock route has no block, no ladle, and no items.** Three of the four things it needs are missing
   at once ([long cell](../machines/long-cell.md), [ladle](../machines/ladle.md)).

2. **`MoldSpec.Size` is parsed and never consulted.** A `longcell` pattern authored today rams cleanly into
   the 1 × 1 cell and casts there, silently ([casting cell § Gotcha 1](../machines/casting-cell.md)). Until
   the long cell exists the rule "the pattern is the spec" is not enforceable on the one field that says
   which station the spec is for.

3. **The two stations use two different sands.** The cell takes prepared `iiex:greensand` and refuses
   everything else on a full-code match (`CastingCellLogic.cs:109`); the bed bakes ordinary construction sand
   into a `{sand}` block variant. Same process, two materials, and the doc-comment that explains why green
   sand exists (`GreenSandItemDefinitions.cs:20-25`) does not mention the bed.

4. **The green-sand doc-comment names a member that does not exist.** It says "shake-out returns it" and
   `<see cref>`s `CastingCellLogic.SandIsReturned` (`GreenSandItemDefinitions.cs:22-24`). The behaviour is
   right - sand is not spent - but the mechanism is `AfterShakeOut` (2026-07-28).

5. **Patterns are charged at ram-up, not at shake-out.** A pattern is worn by impressions, so a run of short
   pours or misruns costs pattern life for nothing.

6. **A cast whose pattern's mod was removed is unrecoverable.** `Harvest` returns false when the spec cannot
   be resolved ([casting cell § Gotcha 5](../machines/casting-cell.md)) - the metal stays and the click keeps
   resolving to `Harvest` forever. A cross-mod extension point with no fallback path.

7. **Every station voids its charge when broken.** Bed, cell and (by analogy) long cell all drop nothing for
   metal standing in them, with no spill sound and no recovery - unlike every other molten holder in the mod.

8. **Nothing in the loop scraps anything.** No gates, no risers, no fettling, no sand loss. Mass in equals
   mass out to the unit, which is right for [R2](../conventions.md) but means the process has no yield lever
   at all - the only failure modes are the two the cell already has.

---

## Open

1. **Re-mass the parts catalogue.** The pig landed at 375 u; `castplate-heavy` 160 → 500 is still owed and
   moves the cell's cavity, the recipe costs and every machine bill that consumes the plate together
   ([density rule § Open 3](../mechanics/density-rule.md), [economy landing](../items/economy-landing.md)).
   Doing one item at a time will produce a fourth set of masses.

2. **The cast-stock ladder does not divide cleanly.** At 1 vx³ = 2.5 u the settled sections give bloom
   4 × 4 × 25 = 400 vx³ → 1000 u and slab 12 × 4 × 25 = 1200 vx³ → 3000 u, both clean - but billet
   3 × 3 × 27 = 243 vx³ → 607.5 u, which is not an integer and does not divide the bloom or the slab.
   Either the billet's length or the ladder's rounding policy has to move
   ([density rule § Rounding](../mechanics/density-rule.md)).

3. **Should the bed obey the pattern rule?** Today it cannot cast anything but pigs and slag bricks because
   its cavities are hard-coded per row. A "bed pattern" would let a player choose the denomination, which is
   probably wrong (bulk should not be a decision) - but the asymmetry should be stated as a decision rather
   than left as an implementation difference.

4. **Sand recovery is a lever nobody has pulled.** Sand is currently free after commissioning. If the loop
   ever needs a consumable, sand degradation (N shake-outs before a re-ram) is the obvious one and needs no
   new item - but it would also delete the one thing that makes the bed cheap, so it is a real trade.

5. **13 drawn shapes have no consumer and 14 are untracked in git.** Every long-cell filling and six cell
   fillings are art with nothing on the other end - and the live bed's own shell shape is uncommitted, so a
   clean clone of the repo builds a bed with no model.

6. **No gate or riser allowance, no cores, no fettling.** Three real steps skipped; each is a place the
   process could gain a decision if it ever needs one ([casting cell § Open](../machines/casting-cell.md)).

7. **The permanent-mold half is undocumented as a route.** The tap/pedestal molds are described by
   [molten canal](../machines/molten-canal.md) as fittings, and nowhere as a casting technology in
   competition with sand. If iron molds are meant to be the player's reward for sand casting, something
   should say so where the player can read it.
