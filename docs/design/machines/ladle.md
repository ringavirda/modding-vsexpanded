# Ladle

**Status** designed - nothing exists. There is no `Ladle` type anywhere in `src/`: no block, no
block entity, no item, no behaviour, no config key, no recipe, no shape, no lang key. A repo-wide search finds
the word only in design documents and in two forward-looking source comments
(`ExpandedLib/Blocks/Structures/BEBehaviorMoltenCell.cs:20` "later the ladle / casting cell";
`IronworkingExpanded/BlockStructures/Casting/Blocks/BlockCastMold.cs:12` naming a ladle as a possible pour
source).   **Mod** iwex (static merger; smex extends it with alloy resolve-on-pour). The movable
ladle is lpex's.

> The absence is load-bearing. R3 reserves the ladle as the only block that merges molten canals,
> so every alloying rule in [materials.md](../materials.md) is unreachable: the whole
> "Ladle mixing rules" section (`materials.md:102-129`), the entire alloy-composition table, hadfield, the
> bronzes, HSS. The molten network merges nothing today
> ([molten network](../mechanics/molten-network.md) § 6).

## Settled rulings

Home (settled 2026-08-05): iwex, not smex, and it does not pour through a canal tier of its own. The
alloying role is smex-tier, the pouring role gates iwex casting, and the block is the same object
in both eras - smex later extends it with alloy resolve-on-pour rather than shipping a second one.

Two shape variants, a real choice rather than a skin:

| variant | built from | why it exists |
|---|---|---|
| **plate** | hammered iron plates | iron plates are vanilla items, so this variant is not gated on the rolling mill - the ladle can be built early and fast |
| **cast** | cast iron | cheaper per unit, and reserves plates for things that need them |

`castshell` returns to iwex for this. iwex owns it, lpex consumes it (water tank, ore crushers,
engines), which is the normal dependency direction and needs no cross-mod pattern indirection.

Size: Bessemer-scale. A 3×3×3 footprint with a hemispherical vessel, holding ~24 000 u or more,
roughly 4× a cupola hearth. A holding vessel, not a hand shank.

Settled: it is a static in-line canal merger. No rails, no launder tier, no adjacency rule - R3
reserves the ladle as the only block that merges molten canals. Canals in, one vessel, canal out: a node
in the network, not a vehicle and not a bypass around it.

One pour means continuity, not speed. A cold shut is metal arriving onto metal that has already
frozen; if the feed never stalls, a slow fill is still one pour, because the metal at the front stays liquid
while it is being fed. There is no rate requirement to engineer around, and the pressure rate genuinely
creates is already modelled: fill a cavity too slowly and it finishes below `minPourTemp`, which is a
misrun - the mechanic [casting-cell](casting-cell.md) already ships.

The ladle's roles, in order of what they gate:

| role | what it unlocks |
|---|---|
| **merge** | every alloy in [materials.md](../materials.md) - the whole table, hadfield, the bronzes, HSS - plus the Bessemer's mandatory recarburisation. Nothing else in the mod merges canals |
| **hold** | ~24 000 u of buffer between a furnace's tapping rhythm and the casting rhythm |
| **alloy on pour** | mix by held proportion, resolve on pour, off-spec → waste alloy |

### Two ladles — a static iwex merger and a movable lpex one (settled 2026-08-05)

| | mod | what it is | route it offers |
|---|---|---|---|
| **static ladle** | iwex | in-line canal merger, 3×3×3, ~24 000 u | cheap, low throughput - merges canals, gates every alloy, feeds moulds through canals |
| **movable ladle** | lpex | steam-moved vessel on track | expensive, high throughput - gathers from several furnaces and pours a cavity out fast |

lpex is the home because moving a massive ladle needs power: a 24 000-unit vessel is not pushed by
hand, so the machine belongs to the tier that has an engine. smex is the customer that needs it:
3 000-unit slabs, poured from metal gathered across several converters or open hearths.

The movable ladle is deferred on cost, not rejected. What it would need, none of which exists: a rail
network (track blocks, a graph, junctions); a moving multiblock entity - a 3×3×3 structure that is not a
block while in motion, with no precedent in the mod (every multiblock today is static and cell-addressed);
placement and docking; save/load of a thing between cells. Nothing about casting is blocked by a static
ladle, because one pour needs continuity, not speed.

#### It cools slowly, and that is the square-cube law, not a rule

Heat loss scales with surface area; heat content scales with volume. The cooling rate therefore goes as
area ÷ volume ∝ 1/L - double a vessel's linear size and it cools at half the rate. Torpedo ladle cars move
hundreds of tonnes over hours losing tens of degrees; a hand ladle loses that in a minute. Refractory lining
and preheating are the two other levers: a cold ladle chills the first metal poured into it.

Implement it as a derived quantity, never a per-block constant: `BEBehaviorMoltenCell`'s cooldown should
take a surface-to-volume factor, so a big vessel holds heat because it is big rather than because a config
key says so. The same law then covers a thin canal chilling fast and a crucible hearth not - one factor,
three machines. A hand-tuned `MoltenCooldownSpeed` per block would give the same numbers today and diverge
the moment anything is resized. It also closes "molten solidifies too fast" without a special case: the
player's lever is to build the bigger machine.

#### The two routes, side by side

| | canal | movable ladle |
|---|---|---|
| cost | cheap, early | expensive, steam-tier |
| throughput | low - `MoltenFlowRate` | high |
| volume delivered | whatever the run carries | ~24 000 u in one trip |
| heat en route | chills fast (thin section) | barely cools (square-cube) |
| best at | one mould, near the furnace | many moulds, or big ones, far from it |

### Skip hoist — smex's, not iwex's

The cold blast furnace is late-18th/early-19th century and was charged by hand - barrow over a bridge from
a hillside, which the tall hopper stands in for. Skip hoists belong to the hot blast era, so that machine
is smex's. The movable-entity subsystem still has two consumers, in different mods - lpex's movable ladle
and smex's skip hoist - so it remains a shared system worth scoping once rather than twice; nothing in iwex
waits on it.

**Owns** - the facts this page is canonical for:

* the ladle's status: exactly what exists (nothing) and the two comments that anticipate it;
* the mixing mechanic: mix by held proportion, resolve on pour, off-spec → waste alloy that keeps
  the base mass - and why this is not vanilla `AlloyRecipe` emission;
* the chill model - the arithmetic that makes "small additions go in solid, large ones must be
  molten" fall out of temperature rather than out of a rule - with its worked table;
* the fact that the Bessemer route cannot work without it (mandatory recarburisation, N1);
* how a merging block is built without touching exlib, and why it must not be a molten-graph node;
* its proposed numbers, verbs, drops and the order the pieces have to land in.

**Does not own** - cited only, never restated:
[materials.md](../materials.md) - the alloy catalogue, every target ratio, the waste-alloy recovery routes,
and the rule that `MetalDef.Alloy` must stay inert ·
[molten network](../mechanics/molten-network.md) - `IMoltenCell`, `BEBehaviorMoltenCell`, `FlowEdge`, the
no-op merge/split, the metal-type refusal, the two cluster drivers ·
[molten canal](molten-canal.md) - canal cells, the start, the tap, the mold pedestal, seals and valves ·
[Bessemer](bessemer.md) - the blow, its carbon model, its cold-scrap gate, its capacity ·
[open hearth](open-hearth.md) - bath alloying (D6), the low-N grade ·
[cupola](cupola.md) - melting ferroalloys is the cupola's third job, and why fuel contact makes it the
right machine for that ·
[blast furnace (cold)](blast-furnace-cold.md) - ferroalloys as a burden family, the cold furnace's second act ·
[long cell](long-cell.md) · [casting cell](casting-cell.md) · [casting bed](casting-bed.md) - where the mixed
metal goes · [heat balance](../mechanics/heat-balance.md) - the furnace `T_process` law the ladle does not
use (it has no fire) · [recipes & config](../mechanics/recipes-config.md) ·
[STATE.md](../../plans/STATE.md) - D3, D6, N1, and the ferroalloy rulings

**Depends on** [molten network](../mechanics/molten-network.md) · [molten canal](molten-canal.md) ·
[materials.md](../materials.md) · [Bessemer](bessemer.md) · [open hearth](open-hearth.md) ·
[cupola](cupola.md) · [recipes & config](../mechanics/recipes-config.md)

---

## Role

The one place in the suite where two metals become one. Everything else in the molten system refuses to mix:
`FlowEdge` will not move metal into a cell holding a different code, `PushMetalRaw` refuses the same, and
graph merge/split are no-ops ([molten network](../mechanics/molten-network.md) § 6). Two metals sit
side by side in one canal run and stay two metals. R3 exists so that exactly one block breaks that rule,
and the ladle is it.

Three jobs, in the order the player meets them:

| Job | What goes in | Why it exists |
|---|---|---|
| **Recarburise** | blown iron + a recarburiser - FeMn trim for mild, a spiegeleisen dose for rail-grade; two distinct reagents, settled 2026-08-07 ([recarburising](../processes/recarburising.md)) | mandatory - see below. Without it the Bessemer route produces nothing usable |
| **Alloy** | a mild-steel base + an element, by proportion | hadfield is the alloying-mechanic introduction: the first metal that must be mixed rather than smelted ([materials.md](../materials.md)) |
| **Trim carbon** | powdered coke, hand-dropped | the counterpart to a converter blow - carbon comes out one way and goes back the other |

### The Bessemer route does not work without it

[STATE.md](../../plans/STATE.md) N1, settled 2026-07-29: the blow burns out all the carbon and the manganese,
leaving iron that is oxygen-saturated. Its name is blown iron and it is not a material to build with.
Mushet's 1856 spiegeleisen addition was not a refinement: without it the Bessemer process did not work at all.

```
molten pig ──▶ BESSEMER ──▶ blown iron ──▶ LADLE (recarburise + deoxidise) ──▶ steel
```

Powdered coke is not a substitute. Carbon alone cannot fix burnt Bessemer metal: it is
the manganese that scavenges the oxygen. The two additives coexist with no redundancy:

| Additive | Adds | Use |
|---|---|---|
| **powdered coke** | carbon only | open-hearth trim, adjusting carbon |
| **spiegeleisen** *(~3.8 % C, low Mn)* | carbon and deoxidises | mandatory after a Bessemer blow - at 10–15 % it is the rail-grade dose |
| **high-carbon ferromanganese** *(~80 % Mn)* | manganese, carbon and deoxidises | the mild-steel trim, and the alloying reagent |

The two ferroalloys are distinct items, not synonyms - ~10× apart in Mn strength (settled 2026-08-07,
[recarburising](../processes/recarburising.md)).

Where the ferroalloys come from is not this page's: ferromanganese and ferrochrome are blast-furnace
products (a burden family, [blast furnace (cold)](blast-furnace-cold.md)) and they are melted in the
[cupola](cupola.md), because cupola carburisation is free for metals that are high-carbon by definition.

### It is a megablock, not a multiblock (settled 2026-08-06)

One canal-fed vessel, 3×3×3, built by RCC: a single block entity over an invisible filler cluster,
addressed as one machine, with construction stages rather than a laid-out cell legend. Megablock is a third
category in this suite's vocabulary (block / megablock / multiblock, [conventions](../conventions.md)):
"not a multiblock" does not mean "crafted in a grid" - a megablock has RCC stages, and a stage can take
brick.

---

## Structure

Nothing is drawn yet, and there is no cell legend to lay out - a megablock is raised by RCC stages, not
by a layout (§ *It is a megablock*). What it still owes is a shape, which nothing has drawn. The design:

```
        canal (base metal) ─┐
                            ├──▶ ┌─────────┐
        canal (addition) ───┘    │  LADLE  │ ──▶ canal start · mold pedestal · long cell
                                 └────┬────┘
        hand-dropped coke ────────────┘
        hand-dropped solid ferroalloy
```

| Part | Design | Build it from |
|---|---|---|
| the vessel | one block. Holds one bath and a per-element addition tally | `MoltenCharge` (`ExpandedLib/Metals/MoltenCharge.cs:20`) - the exact type the [Bessemer](bessemer.md) holds its bath in |
| the intakes | pulls from any adjacent canal cell, on any horizontal face | the converter's own idiom: `GetMoltenCell(local)` → `DrainMetal(n)` (`BlockEntityConverterControl.cs:764-766`, `:436`); or the casting bed's external-cell pull (`BlockEntitySandCastingBed.cs:267-287`) |
| the pour | tilt, into a canal start / mold pedestal / long cell | `PushMetal(amount, stack, world)` (`BlockEntityMoltenCanal.cs:187`) - the same call both converter pours make |
| the hand port | RMB with powdered coke or a solid ferroalloy | `ExInventory.TakeHotbar` + a role test, exactly as `TryChargeScrap` does (`BlockEntityConverterControl.cs:615-661`) |
| the readout | live composition, temperature, and what it would pour as | R7 - block info, the shared `MoltenCharge` temperature read |

Orientation: one horizontal `side` variant for the pour direction, `HorizontalOrientable`, as every other
molten fitting has.

### It must not be a molten-graph node

This is the single most important implementation constraint, and the reason the machine is cheap:

* the molten graph's `Merge` / `Split` are no-ops and there is no pooled state to redistribute;
* `FlowEdge` refuses any transfer where the receiver holds a different metal code
  ([molten network](../mechanics/molten-network.md) § 6).

So a ladle that joined the graph would be refused its own second input by the network it is supposed to
merge. The pattern that works already exists in three places: a non-node block that reads adjacent cells
by code and drains them itself. The [Bessemer](bessemer.md) does this with its input tap; the sand
casting bed and cell do it with a hard-coded pull rate.

> The ladle needs no exlib change at all. The merge mechanic R3 reserves for it is, mechanically,
> "call `DrainMetal` on two neighbours instead of one".

---

## Assets

Nothing exists.

| Asset | State |
|---|---|
| editable shape | missing - `assets/editable/shapes/` has 97 files and no ladle |
| runtime shape | missing |
| animations | missing. It needs `idle` + a held pour tilt - the [Bessemer](bessemer.md) vessel's `filling`/`pouring` clips are `Hold`, one keyframe, and that is the right shape for a tilt (`assets/smex/shapes/converter/bessemer.json`) |
| ferroalloy metal defs | missing - `assets/*/config/metals/` holds `castiron`, `pigiron`, `slag` (iwex) and `bessemersteel` (smex). No `ferromanganese`, no `spiegeleisen`, no `ferrochrome`, no `hadfieldsteel`, no `blowniron`, no `wastealloy` |
| powdered coke | missing as an item |
| lang / handbook | no key, no page |

Reusable art: the molten barrel (a standing vessel with a rendered metal surface), the mold pedestal, and the
converter vessel's tilt-pose animator pattern.

---

## Construction

### Settled 2026-08-06 — a 3×3×3 megablock, built by RCC, lined with tier2 refractory brick

Fired clay is disqualified as a lining by the mod's own rule: it is capped at 1200 °C (vanilla's
`maxHeatableTemp: 1200`, and this mod's ceiling at `IwexConfig.cs:65`), while this vessel carries pig iron
at 1482 °C and steel above it - the same argument that forced the
[crucible furnace](crucible-furnace.md) to invent a refractory pot. A clay-lined ladle is a ladle whose
lining melts.

The lining is pinned to tier2 (basic), by ruling, on the historical route. A steel ladle is
basic-lined, and [conventions](../conventions.md) § *Refractory tier is refractory chemistry* cites this very
vessel as its evidence: *"olivine refractory brick is a real product and is classified basic - it is used in
steel ladles for exactly that reason."*

This is an exception to "any tier anywhere", which makes tier a gate only where the lining reacts with the
slag. Nothing in this ladle does: it merges metal canals and gates alloys, while slag has its own taps and
canals. So the pin buys authenticity, not a mechanic - the player is held to the real vessel rather than to
a consequence the simulation produces. It is not to be relaxed back to any-tier on the grounds that it gates
nothing.

The door it leaves open: if the ladle ever knows the provenance of what it carries, the lining
becomes a genuine second Gilchrist-Thomas decision - basic required for basic-process steel, acid eaten by
it - and the RCC stage is already there to gate on. That needs slag-carryover or provenance tracking the mod
does not have, so it is a later call and not a reason to soften the pin now.

| Stage | Cost | Modelled on |
|---|---|---|
| shell | 8 plate + 8 nails + 4 rod | `ExIngredients.cs:27-46`; cf. the converter control's `H_R,NPP,PPR` (`ConverterRecipeDefinitions.cs:24`) |
| lining | tier2 refractory brick (`ExCodes.RefractoryTier(2)`) | the [Bessemer](bessemer.md)'s `Root/InputLining`, stage 7 - the same move [conventions](../conventions.md) proposes there |

Cost key `ladle-grid` in `IwexRecipeConfig.DefaultCatalogue` (`IwexRecipeConfig.cs`).

It should not need a gear or an axle. The [Bessemer](bessemer.md) tilts on mechanical power because it
is a 3×3×3 vessel full of steel; a ladle is hand-tipped, like the molten barrel.

---

## Operation

### The verbs

| Verb | Effect |
|---|---|
| **RMB, empty hand** | tilt-pour into whatever is in front of it, at a fixed rate |
| **RMB with powdered coke** | drop in a fixed mass of carbon per unit - its effect on % C is relative to the iron present ([recarburising](../processes/recarburising.md)) |
| **RMB with a solid ferroalloy** | add its mass to the tally and apply the chill (below) |
| **passive** | pull from any adjacent canal cell that is not already the metal it holds |
| **Sneak + RMB** | dump the bath (waste it deliberately) - the escape hatch for a mix gone wrong before it freezes |

### Mix by held proportion, resolve on pour

The bath carries a **base metal** and a per-element **addition tally**. Nothing is decided while the ladle is
filling - the composition is arithmetic over the tally, shown live (R7). On the pour, the held
proportions are matched against the registered target windows:

* inside every window → the alloy metal pours;
* outside any window → waste alloy, which keeps the full base-metal mass and is otherwise useless.

Worked examples for an 800 u small billet (fractions from
[alloys § Compositions](../items/alloys.md#compositions-target-fractions)): hadfield = ~700 u mild steel
+ ~100 u manganese-bearing addition (12.5 % Mn); HSS = ~624 u open-hearth base + ~144 u tungsten +
~32 u chromium; tin bronze (deferred scope) = ~704 u copper + ~96 u tin.

> Off-ratio never snaps to the nearest alloy. That is the distinction from vanilla, and it is why
> `MetalDef.Alloy` / `MetalAlloySpec` must stay inert ([materials.md](../materials.md)). Confirmed in code:
> `MetalDef.Alloy` is declared and never read - `grep -rn "\.Alloy\b\|MetalAlloySpec" src/` returns only
> its three declaration sites (`ExpandedLib/Metals/MetalDef.cs:71`, `:138`, `:144`).

Waste alloy is recoverable - in the [cupola](cupola.md) as cast iron, as capped cold scrap in a live
[Bessemer](bessemer.md) heat, or in the arc furnace. The routes and the "never the blast furnace" rule are
[materials.md](../materials.md)'s.

### The chill model — the mechanic is temperature, and it gates nothing

Settled 2026-07-29 ([STATE.md](../../plans/STATE.md) § "How ferroalloys are added"): both routes are real, and the
quantity decides which one is usable.

| Addition | Route | Why |
|---|---|---|
| **small** - recarburising, deoxidising | solid lumps, thrown in | the heat absorbs the chill easily |
| **large** - spiegeleisen at 10–15 %, or 12.5 % Mn for hadfield | molten, poured from a [cupola](cupola.md) | a cold charge that size freezes the heat |

> Recarburising is solid and free. Hadfield needs a cupola. The difference between making steel and
> making alloy steel becomes an infrastructure requirement rather than an unlock - R5 exactly, since the
> cold route is never blocked, only punished.

The proposed arithmetic. Adding `m` units at `T_add` to a bath of `M` units at `T`:

```
T_mixed = (M·T + m·T_add) / (M + m)
T_new   = T_mixed − LadleSolidChillC · m / (M + m)      // solid addition
T_new   = T_mixed                                        // molten addition, no latent-heat penalty
```

The second term is the latent heat of fusion expressed as a temperature: melting the addition costs heat
the bath has to supply. For iron, `L_f / c_p ≈ 550 K`, which is the physical value.

Worked on a 5400 u bath (one settled converter heat's product, [Bessemer](bessemer.md#numbers)), against
Bessemer steel's melting point of 1500 °C (`assets/smex/config/metals/bessemersteel.json`). The
hadfield dose is ferromanganese at ~80 % Mn, so the real addition is 1000 u
(`0.125 × 5400 / 0.675`, [alloying](../processes/alloying.md) § Numbers) - nothing in the suite produces
pure manganese.

| Case | m | T_add | `T_mixed` | chill | `T_new` | Result |
|---|---|---|---|---|---|---|
| recarburise, bath at 1800 | 300 u solid | 20 °C | 1706.3 | 28.9 | 1677 °C | free |
| recarburise, bath at 1700 | 300 u solid | 20 °C | 1611.6 | 28.9 | 1583 °C | free |
| hadfield, FeMn 80 %, bath at 1800 | 1000 u solid | 20 °C | 1521.9 | 85.9 | 1436 °C | frozen - pour lost |
| hadfield, FeMn 80 %, bath at 1700 | 1000 u solid | 20 °C | 1437.5 | 85.9 | 1352 °C | frozen - pour lost |
| hadfield, FeMn 80 %, molten, bath at 1700 | 1000 u | 1400 °C | 1653.1 | 0 | 1653 °C | |

The ruling falls out of the arithmetic - no gate, no check, no "you may not". At the 1000 u
dose the solid charge freezes even an 1800 °C bath by 64 °C; to survive, the bath would need ~1876 °C -
above the converter's own 1800 °C `T_process` ceiling ([Bessemer](bessemer.md#numbers)) - or the dose
split, and since a ladle has no fire, splitting the addition does not change the energy balance, only the
moment it is paid. So "hadfield needs a cupola" is flatly true. A fast-hands marginal case, if ever wanted,
must be bought by lowering `LadleSolidChillC`.

The bath also cools on its own while the player fetches the addition, at the vanilla per-in-game-hour
rate. The [Bessemer](bessemer.md) halves that rate for its insulated vessel
(`BessemerCooldownCoefficient` 0.5, `SmexConfig.cs:240`); the ladle needs the same knob, and the choice of
value is how long the player has to work.

### Zinc needs a coke cover

Brass boils its zinc off unless the bath is covered with coke ([materials.md](../materials.md)). That is a
second use for the same hand-drop port and a second reason the ladle needs a bath state, not just a tally,
but it is non-ferrous and therefore deferred with everything non-ferrous
([STATE.md](../../plans/STATE.md) D8).

---

## Numbers

All proposed. No config section, no keys, no code. The right-hand column is what exists.

| Key | Proposed | Measured against (file:line) | What it does |
|---|---|---|---|
| `LadleCapacity` | 6000 u | settled converter capacity ([STATE.md](../../plans/STATE.md) D4); `CanalDefaultUnitCapacity` 50 (`IwexConfig.cs:86`); `MoldDefaultUnits` 100 (`:92`) | one converter heat = one ladle. but the converter pours 5400 u of steel from a 6000 u charge - see Open #3 |
| `LadlePullRate` | 25 u/tick | the casting bed's and cell's hard-coded `PullRatePerTick = 25` (`BlockEntitySandCastingBed.cs:44`, `BlockEntitySandCastingCell.cs:33`) | ship it as config, not a fourth hard-coded copy (D5b) |
| `LadlePourRate` | 44 u/s | `BessemerPourRate` 44 (`SmexConfig.cs:231`) | match the converter so a ladle never becomes the bottleneck |
| `LadleCooldownCoefficient` | 0.5 | `BessemerCooldownCoefficient` 0.5 (`SmexConfig.cs:240`), on `IwexValues.MoltenCooldownSpeed` 24 (`IwexConfig.cs:31`) | how long the player has to work a mix |
| `LadleSolidChillC` | 550 °C | `L_f/c_p` for iron. 800 makes the hadfield-solid case a hard refusal | the whole chill mechanic, in one number |
| `LadleAlloyTolerance` | ±0.02 mass fraction | — | the width of every alloy window. One number for the whole table, not per-alloy |
| `LadleCarbonPerCokeUnit` | fixed mass, not a percentage | `materials.md:122-125` - "each unit adds a fixed mass of carbon (its effect on % C is relative to the iron present)" | the carbon trim |

### What has no number yet, and needs one

* The alloy windows themselves. [alloys § Compositions](../items/alloys.md#compositions-target-fractions)
  gives targets (hadfield ~12.5 % Mn; HSS ~18 % W + ~4 % Cr; tin bronze ~12 % Sn) but no tolerance and no
  source of truth in code.
* Ferroalloy composition - half-pinned 2026-08-07: spiegeleisen is ~3.8 % C, low Mn and FeMn is
  ~80 % Mn ([alloys](../items/alloys.md) owns the identity rows). FeMn is high-carbon by definition
  ([cupola](cupola.md)), so its addition still changes two numbers at once, and its exact carbon
  fraction is the one number nothing has chosen ([alloying](../processes/alloying.md)).
* What "blown iron" is. No metal def, no melting point, no `solidDrop`.

---

## Drops

| Broken | Returns |
|---|---|
| the ladle, empty | itself |
| the ladle, **solidified** | the [Bessemer](bessemer.md#drops)'s rules, unchanged: metal bits at 5 u per bit via `MoltenChisel.BuildRecovery` (`ExpandedLib/Metals/MoltenChisel.cs:53`), with a chisel-out path for a small residue and a random mangling loss for a break |
| the ladle, **liquid** | decide. The converter voids a liquid charge outright (`BlockEntityConverterControl.cs:1090-1102`). For a ladle that is harsher than it looks - the player is mid-mix by definition |

Everything in that column already exists as a shared helper; none of it needs writing again.

---

## Code

Nothing exists. The two comments that anticipate it:

| Comment | Says |
|---|---|
| `ExpandedLib/Blocks/Structures/BEBehaviorMoltenCell.cs:20` | *"The principal that hosts a fixed cluster of them (the sand casting bed; later the ladle / casting cell) drives flow across the cluster itself"* |
| `IronworkingExpanded/BlockStructures/Casting/Blocks/BlockCastMold.cs:12` | names a ladle as a possible pour source for a mold |

| Piece | Where it would go | Model it on |
|---|---|---|
| `BlockLadle` | `src/IronworkingExpanded/BlockStructures/Ladle/Blocks/` | `BlockMoltenBarrel.cs` (a standing vessel with a rendered surface) for the block; `BlockConverterControl.cs:84-167` for modifier-key verb routing |
| `BlockEntityLadle` | `…/Ladle/BlockEntities/` | `BlockEntityConverterControl.cs:44` - the closest existing analogue by far: it already holds a `MoltenCharge`, drains a neighbouring canal cell, pushes to another, tracks a per-heat tally (`_carbon`, `_pigCharged`, `_scrapUnits`), syncs a live cooldown coefficient, latches solidification, serialises the lot and prints a readout |
| the bath | reuse | `MoltenCharge` (`ExpandedLib/Metals/MoltenCharge.cs:20`) - create, retype, temperature, live cooldown, `IsBelowMeltingPoint`, `BuildRecovery`, tree round-trip. Nothing here needs writing |
| the merge | reuse | `GetMoltenCell(offset)?.DrainMetal(n)` on two or more neighbours (`BlockEntityConverterControl.cs:764-766`) |
| the pour | reuse | `BlockEntityMoltenCanal.PushMetal` (`:187`) - plus `SoakHeat` on refusal, so a blocked destination does not plug |
| solid additions | reuse | `MaterialRoleRegistry.IsRole(role, stack)` + `ExInventory.TakeHotbar` (`BlockEntityConverterControl.cs:619`, `:648`) - a new `Roles.Ferroalloy` beside `Roles.Scrap` |
| the tilt pose | reuse | `ToggleAnimator` / `ConstructedAnimator` and a `Hold` clip |
| chisel-out | reuse | `IChiselableMolten` + `MoltenChisel.TryChisel` (`ExpandedLib/Metals/IChiselableMolten.cs`, `MoltenChisel.cs:53`) |
| alloy windows | new - the only genuinely new data | a catalogue keyed by product metal, read at load like `MetalCatalogueLoader` reads `config/metals/` |

### Where a caller hooks in

The ladle is ~90 % existing parts. Strip the blow out of `BlockEntityConverterControl` and what is left -
hold a `MoltenCharge`, drain a neighbour, push to a neighbour, track a tally, chill, freeze, chisel, print -
is the ladle. The only new code is the composition resolver and its window catalogue.

Once it exists, three things unlock at once with no further work: recarburisation (so the
[Bessemer](bessemer.md) route completes), hadfield (so hpex gains its material gate), and the whole
`materials.md` alloy table.

### Tests

None. When built: `test/SteelmakingExpanded.Tests/Blocks/Converter/ConverterControlProcessTests.cs` is the
template for the arithmetic (mass conservation at `:237`, the three temperature cases at `:261`/`:279`/`:304`),
and `Fixtures/SteelPlantScenes.cs:31` for a rig that builds the real thing rather than faking its state.
The standing gap it must not repeat: no converter test asserts a rate as a number, which is how
`BessemerPourRate` moved from 16 to 44 unnoticed.

---

## Gotchas

1. **A ladle that joins the molten graph cannot do its job.** `FlowEdge` refuses a transfer into a cell
   holding a different metal, and merge/split are no-ops
   ([molten network](../mechanics/molten-network.md) § 6). It must pull by code, not by graph. This is the one
   mistake that would take the whole feature back to the start.

2. **R3 is a reservation, not a description.** `conventions.md:38-40` states the ladle is the only merge
   point; nothing in code enforces or provides it. Anything else that starts merging silently breaks the
   invariant, and there is no test that would notice.

3. **`MetalDef.Alloy` is a loaded gun.** It exists (`MetalDef.cs:71`), it has a full `MetalAlloySpec` shape
   (`:138-146`), and it is read by nothing. Wiring it up would emit vanilla `AlloyRecipe`s, which snap
   off-ratio mixes to the nearest alloy - the exact behaviour the design forbids
   ([materials.md](../materials.md)).

4. **Bath alloying (D6) is the primary route, and the ladle is the second.** The
   [open hearth](open-hearth.md) can alloy in the bath; the ladle can too. They must share one readout
   format and one window catalogue, or the suite gains two alloy systems that drift apart.

5. **The chill constant does not decide the hadfield ruling.** At the 1000 u FeMn dose the hadfield-solid
   case freezes from any reachable bath at `LadleSolidChillC = 550` (§ the chill model); the constant
   only sets how much margin the small trim additions keep. Lowering it far enough would resurrect a
   fast-hands marginal case - a deliberate purchase, not an accident of the table.

6. **Ferroalloy additions change two things.** Ferromanganese is high-carbon by definition, so a Mn addition
   is also a carbon addition. A model that tracks only the alloying element will let players hit hadfield's
   12.5 % Mn while silently missing its ~1.2 % C target ([materials.md](../materials.md)).

7. **The pour destination decides the product, and there are three of them** - canal start, mold pedestal,
   long cell - each with a different capacity and a different rate. [STATE.md](../../plans/STATE.md) D5b wants one
   number (50 u/s) for the whole molten network; two of the four current rates are hard-coded.

8. **Zinc's coke cover is non-ferrous and deferred**, but it is the one addition whose absence destroys
   material rather than mis-grading it. Do not build the hand-drop port in a way that cannot express "the bath
   is covered".

9. **Nothing rewards a second ladle** (settled 2026-08-05). Parallelism is throughput only
   ([plant-layout](../mechanics/plant-layout.md)); a second ladle earns its place by holding a second heat.
   Merging two cupolas' output through one ladle is plumbing the player builds and pays for - the opposite
   of a grouping bonus, and untouched by the rule.

---

## Open

1. **Nothing is built**, and it is the highest-leverage gap in the steel tier: block, BE, composition
   resolver, window catalogue, ferroalloy metal defs, powdered coke, shape, recipe, lang, handbook, tests.

2. **Blown iron does not exist either.** The [Bessemer](bessemer.md#open) currently pours a finished,
   tool-capable `smex:ingot-bessemersteel`. Making the ladle mandatory means adding a `blowniron` metal def
   and retyping the blow's output - which must not land before the ladle does, or the steel tier has no
   product at all.

3. **What exactly is 6000 u a capacity of?** The
   converter's 6000 u is a pig charge that yields 5400 u of steel. If a ladle is meant to take one
   heat, it needs 5400 - and a real hadfield heat is ~6590–6740 u (base 5400 + a separate recarburiser
   0–284 u + 1000 u of FeMn at 80 %, [alloying](../processes/alloying.md) § Numbers). That is over
   the proposed 6000 u capacity by 10–12 %. Either the
   ladle is sized for base + both additions (≈ 6800 u), or a hadfield heat is mixed in two goes - an open
   sizing question, not settled here; it must be decided with
   [bessemer § Open #2](bessemer.md#open), not after it.

4. **Where do the alloy windows live?** A JSON catalogue beside `config/metals/` is the obvious answer and
   matches how metals already load - but the targets currently live in `materials.md`, which
   [STATE.md](../../plans/STATE.md) says should become generated. The two must not both be canonical.

5. **Is the static ladle enough?** Historically a ladle is carried by crane from converter to casting
   floor; the settled static block means the canal has to reach every casting station, and the
   [long cell](long-cell.md) is a large megablock. The movable lpex ladle (§ Settled rulings) is the
   deferred answer - worth confirming the reach before the layout is drawn.

6. **No handbook page and no in-game teaching path.** Hadfield is called "the alloying-mechanic introduction"
   ([materials.md](../materials.md)); an introduction that nothing explains is a wall. The handbook pipeline
   (`docs/smex/handbook/` ↔ `assets/smex/lang/en.json`) is where it goes.
