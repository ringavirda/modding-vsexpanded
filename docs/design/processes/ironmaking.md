# Ironmaking

**Status** live end to end (verified against source 2026-08-07) - every machine in the loop exists, has a
recipe and simulates; the whole lifecycle is pinned by
`test/IronworkingExpanded.Tests/Scenarios/ColdBlastFurnaceScenarioTests.cs`, which charges, lights, melts,
taps and extinguishes the real 160-cell structure on the clock. The numbers are first-pass calibration,
pinned by tests, not playtested
**Mods** iwex (the whole loop), smex (the hot-blast variant of one step, and the converter that consumes the
alternative exit)

**Owns** - the facts this page is canonical for:

* the loop itself: the ordered sequence of machines, which player verb drives each step, and which hand-offs
  are automatic and which need a player to carry a stack;
* the feedstock inventory - the four materials a furnace campaign needs, and where each comes from in
  vanilla;
* the blow-in - what lighting a furnace takes today, and the designed torch-and-clay-plug ritual that is not
  yet coded;
* the extinguish payout - `BfUnitsPerSolidNugget`, `BfBurnoutFuelRetainedBottom`/`Top`, the
  salvage-and-re-coke loop they create, and the fact that the payout denomination agrees with the pig bed's
  (delegated to this page by [cold blast furnace](../machines/blast-furnace-cold.md) § Numbers);
* the bed rotation as a player practice, its sizing rule, and the fact that nothing in code models it;
* direct charging as the loop's second exit, including the fact that the converter end of it is already
  built and what is missing;
* the end-to-end campaign arithmetic - what one burdenmaker batch and one shaft-full are worth in pig,
  derived from constants owned elsewhere.

**Does not own** - cited only, never restated:
[burdenmaker](../machines/burdenmaker.md) (the two hoppers, the gate, crate semantics) ·
[burden](../items/burden.md) (item identity, the stamp, the three flux bands) ·
[fuels](../items/fuels.md) (the `fuel` role, carbon values, the two-taxonomy rule) ·
[layered-charge](../layered-charge.md) (the charge overview: the two streams, the column model, band-order
charging) · [heat balance](../mechanics/heat-balance.md) (`T_process`, the derived state, the carbon rate
keys, blast demand, the chill, ignition) ·
[tall hopper](../machines/tall-hopper.md) (the tank, the drip, `NextChargeColumn`) ·
[twin-tub blower](../machines/twin-tub-blower.md) (output, ceiling, the tier gate) ·
[cold blast furnace](../machines/blast-furnace-cold.md) (structure, cell census, the crucible, the tap
drain path) ·
[hot blast furnace](../machines/blast-furnace-hot.md) (the sealed top, the bell pair, the exhaust budget) ·
[cowper](../machines/cowper.md) · [smokestack](../machines/smokestack.md) ·
[pipe network](../mechanics/pipe-network.md) · [molten network](../mechanics/molten-network.md) ·
[molten canal](../machines/molten-canal.md) · [casting bed](../machines/casting-bed.md) (slots, carve,
harvest, denomination) · [pig](../items/pig.md) (the pig / chunk / bit family and the 375 u pig) ·
[cupola](../machines/cupola.md) · [puddling furnace](../machines/puddling-furnace.md) ·
[bessemer](../machines/bessemer.md) (the converter end of direct charging) ·
[coking](coking.md) (where the coke comes from) · [roasting](roasting.md) (the unbuilt pre-step, and the
8.5 u/nugget recovery anchor) · [density rule](../mechanics/density-rule.md) (1 vx³ = 2.5 u) ·
[multiblock](../mechanics/multiblock.md) · [recipes & config](../mechanics/recipes-config.md)

---

## What it is

A blast furnace is a **counter-current shaft**. Charge - coke and ore-bearing burden, laid in alternating
courses - goes in the top and descends; air blown in at the tuyeres burns the coke to CO at the **raceway**,
and the hot gas rises through the descending column, heating the burden on its way past. The gangue combines
with the flux into a fluid slag that floats on the metal. Both are drawn off through separate tap-holes at
different heights, and the furnace never stops: it is charged, tapped and re-charged continuously.

What the mod keeps: the layered two-stream charge, per-column descent, counter-current heat exchange with
carried band temperature, the carbon burned at the raceway as the one throttle, two taps at two heights, and
continuous unattended running.

What the mod abstracts away:

| Real thing | What the mod does instead |
|---|---|
| Gas chemistry (CO/CO₂ ratio, indirect vs direct reduction, solution loss) | one `T_process` law plus a heat-only gas pass; carbon is burned at the raceway and nowhere else ([heat balance](../mechanics/heat-balance.md), [layered-charge](../layered-charge.md)) |
| The full mass balance | a **yield per ore unit**: iron out is `BfIronPerOreUnit` × the ore content of the burden actually melted, slag at a fixed 6:1 ratio to it - proportional to the charge, but with no per-heat ledger of gangue, moisture or losses |
| Silicon, sulphur and phosphorus in the pig; foundry vs forge grades | one product, `iwex:ingot-pigiron` |
| Burden distribution gear, the rotating chute | "lowest column first" - hopper charging self-levels, hand charging does not |
| Cast house, runners, sows and pigs, a crane | a canal and a sand bed |
| Days-long blow-in, months-long campaigns, relining | lighting is positional and near-instant; a campaign ends when the carbon in the shaft does |

Production is metered by carbon burned and yield by ore content melted (`BfBurdenPerCarbonUnit`,
`BfIronPerOreUnit`), so what comes out is a function of what went in. There is no ledger: nothing tracks a
specific heat's inputs to its outputs.

---

## The loop

Steps 1-4 are preparation, 5-8 are the campaign, 9-11 are the cast house. Exit B replaces 9-11 once a
converter exists.

| # | Step | Machine | Player verb | Comes out | Automatic? |
|---|---|---|---|---|---|
| 1 | dig / crush ore | vanilla | pan, crush | `game:crushed-iron` | - |
| 2 | grind flux | vanilla quern | grind limestone / chalk / marble | `game:lime` | - |
| 3 | make fuel | vanilla coke oven or charcoal pit | see [coking](coking.md) | `game:coke` / `game:charcoal` | - |
| 4 | combine | [burdenmaker](../machines/burdenmaker.md) | load the two hoppers, open the gate | `iwex:burden` - ore + flux only, flux-graded | no - hand-loaded; the gate is one click |
| 5 | charge in rounds | [tall hopper](../machines/tall-hopper.md) | load coke, let it lay; load burden, let it lay | fuel and burden bands on the shaft's columns | the drip is automatic; the loads, and the ratio between them, are the player's |
| 6 | blow | [twin-tub blower](../machines/twin-tub-blower.md) on an axle | build it, couple an axle | air into the blast main | yes, continuous |
| 7 | light | nothing - the furnace derives its own state | none | a lit raceway | yes - see § Blow-in |
| 8 | melt | [blast furnace](../machines/blast-furnace-cold.md) | none | molten pig + molten slag in the hearth pools | yes - carbon burned is the throttle |
| 9 | tap | `iwex:furnace-irontap` (low) and `-slagtap` (high) | RMB empty-handed to toggle | metal into a [canal](../machines/molten-canal.md) start below the spout | opening is manual; draining is not |
| 10 | cast | [casting bed](../machines/casting-bed.md) | carve the slots once; RMB each hardened mold | pigs / chunks / bits, slag bricks | flow is automatic; harvest is not |
| 11 | clear and rotate | more beds | break out and re-carve | a bed ready for the next tap | no - see § The bed rotation |
| B | direct charge | [bessemer](../machines/bessemer.md) | route the canal to the converter's input tap instead | molten pig into the vessel | yes |

### What the player is actually doing

There is no door, no switch and no start button anywhere in this loop. Every verb in the table either loads
something or opens something; the operating decisions are the two ratios - flux at the burdenmaker, coke at
the hopper - and the complexity is all in the build (`conventions.md`: buy operating simplicity with build
complexity).

### The coke dial — step 5 is where the campaign is decided

Burden carries no fuel, and richness is not stamped on an item but laid on the shaft, one course at a time.
Each round is a coke load followed by a burden load, and the ratio between the two loads is the decision
([layered-charge](../layered-charge.md) § Charging). The consequences, each owned elsewhere:

| Course richness (carbon, by items) | What happens | Owner |
|---|---|---|
| below ≈ 17.3 % | the column packs dense and demands pressure the twin-tub blower cannot raise - the iron tier cannot even blow it | [twin-tub blower](../machines/twin-tub-blower.md) |
| below ≈ 23.9 % on cold blast | it blows, but the burden arrives at the raceway too cold to melt - the chill, and the campaign dies making nothing | [heat balance](../mechanics/heat-balance.md); the break-even is pinned by `ColdBlastFurnaceScenarioTests` |
| ≈ 30 % | the cold-blast working point: wasteful, and it works | the scenario suite's own reference charge |
| lean, on hot blast | a cowper's preheat clears the melt line on less carbon, and `MeltSpeedFactor` makes each carbon unit render more iron - Neilson, twice over | [heat balance](../mechanics/heat-balance.md) · [cowper](../machines/cowper.md) |

1. The decision is per-round, not per-campaign. A player can charge rich to blow in and lean once the shaft
   is hot; a mistake costs one course, not one campaign - unless it chills the column, the one mistake that
   does cost the campaign.
2. Both failure modes are legible: a course too lean to blow reads as a furnace that will not breathe, a
   course too lean to melt as a dark band sitting at the raceway.

Burden's own grade - the flux band - is a naming layer: the burdenmaker's readout and the tooltip agree on
under-fluxed / standard / over-fluxed, and nothing mechanical reads it yet ([burden](../items/burden.md)
§ Gotchas 3, Open 1).

### The tap

Two tap blocktypes since 2026-08-03 - `iwex:furnace-irontap` at the crucible floor, `iwex:furnace-slagtap`
higher in the same wall - so the wrong hole cannot complete a structure; and the live layout legends are
oriented (`BlockBlastFurnaceCoreCold.cs:66`, `:80`), so a tap facing the wrong way is a build-outline
mismatch rather than a silent dead tap. Opening one is refused with `iwex:tap-err-nocanal` unless a
[canal](../machines/molten-canal.md) start sits below the spout, i.e. at `side.Opposite` + one down
(`BlockFurnaceTap.cs`, pinned by `BlastFurnaceTapTests`).

Draining runs only while the furnace is at temperature; a pool stranded by a cooling furnace waits, and a
pool still standing at extinguish freezes onto the hearth (§ The extinguish payout).

### The bed rotation

Beds work in a pour / cool / slag rotation, and the bed count is a derived quantity:

```
beds needed  =  (cooling time + clearing time)  ÷  pour time
```

Under-provisioning is self-punishing rather than fatal, and nothing errors: a full bed stops accepting, the
canal backs up, back-pressure reaches the tap, and the furnace's own pool fills to its cap, which is a
disruption and eventually extinguishes the furnace ([heat balance](../mechanics/heat-balance.md)).

Nothing in `src/` models a rotation, a clearing time or a bed count. The stall path exists
([molten network](../mechanics/molten-network.md)); the rotation is a player practice built on top of it.
At the 375 u pig, one full cold shaft at the reference richness ≈ one full bed (§ Derived).

### Exit B — direct charging

Once a converter exists, the tap can be routed to it instead of to beds. The exit is a canal-routing
decision, not a mechanism: the converter's own layout requires a canal tap at its intake and `TickFilling`
drains that cell straight into the bath (`BlockEntityConverterControl`, layout at
[bessemer](../machines/bessemer.md) § The layout).

| Exit | Cost to the player | Needs |
|---|---|---|
| A - beds | cast → cool → harvest → carry; the heat is thrown away and paid for again later | nothing |
| B - converter | none; the metal never solidifies | a built, empty, upright converter |

Three properties follow:

* It is self-balancing: a converter that is blowing or full stops draining its input tap, the canal backs
  up, and the player routes to beds instead. Neither exit ever becomes useless.
* The beds are never obsolete, because the iron line needs solid pigs -
  [puddling](../machines/puddling-furnace.md) charges pigs and the [cupola](../machines/cupola.md) remelts
  them. Only the steel line can take liquid.
* Metal cools per-cell in the canal ([molten network](../mechanics/molten-network.md)), so a long run to a
  distant converter loses heat: the optimal layout is converter beside furnace.

No routing aid exists: no valve automation, no destination selection, no HUD that says where a tap's metal
is going (§ Open).

---

## Blow-in

Lighting is positional, not quantitative. There is no fire threshold and no tunable total. A furnace is lit
when, and for exactly as long as, every column holds carbon at its own raceway: `TryIgniteCharge` and
`RacewayHoldsCarbon` ask the same question, so "will it light" and "is it still alight" cannot drift apart
(`BlockEntityShaftFurnace.cs`). A shaft with three campaigns' worth of charge piled into one column never
lights, because the other eight columns offer the blast nothing
(`A_shaft_piled_into_ONE_column_never_lights_however_much_is_in_it`). The requirement is the furnace's own
column count - nine on the cold furnace, one on the cupola - a number nobody tunes and a redrawn layout
updates for free.

The column then has to warm through. The flame is at the raceway within a tick, but the burden above it is
cold: the rising gas heats the descending charge band by band ([heat balance](../mechanics/heat-balance.md)
owns the transfer), and first iron follows only once burden that has descended into the raceway arrives
there at melting temperature. Blowing in is therefore a process the player watches - minutes of Firing
before the first Melting - not a transition they trigger.

### The ritual — designed, not yet coded

The designed blow-in makes lighting a sequence with a material cost, built entirely out of the tap:

* The clay plug is the tap's closed state. A newly built tap is plugged; plugging costs fire clay and
  breaking the plug opens the tap (the same held-stack + cost + refund shape the canal's seal uses,
  `TapPlugClayCost` / `TapUnplugClayRefund`). The plug renders as its own shape state - no animator - and
  an opened tap does not close itself: it runs until the crucible empties or the player re-plugs it.
* An open tap with no canal below stops refusing. Instead of the `iwex:tap-err-nocanal` refusal, an open tap
  with nothing to pour into reports the missing canal in its block info and delivers nothing.
* A lit torch through an open tap lights the furnace. The click routes to the furnace core and ignites the
  lowest chargeable round - the flame reaches up from the tap-hole into the raceway above the crucible. A
  plugged tap cannot be lit through.
* The sequence is: break the plug open → torch it → re-plug → blast on. Blowing in costs one clay plug.
* A lit front then climbs the shaft pile-to-pile and stops dead at a burden-only gap
  ([layered-charge](../layered-charge.md) § Still design-only).

Today none of this is coded: lit-ness is furnace-level, taps have no plug state, and the furnace catches by
itself the moment its raceway course is complete. The firebox furnaces (puddling, heating, reheat) keep
auto-ignition in the designed model too; the torch requirement is the shaft family's only. The burn-out
guarantee is already in place either way: `BfBurnoutFuelRetainedBottom` = 0, so a dead furnace has no carbon
left at its own raceway and cannot relight off its own salvage.

---

## Inputs and outputs

### Feedstock — what a campaign needs before anything is built

| Input | Vanilla source | Role in the loop | file:line |
|---|---|---|---|
| crushed iron ore | pan / crush vanilla ore | the burden's ore stream - `ironore` role, path prefix `crushed-iron` | `assets/iwex/config/materialroles.json:11` |
| `game:lime` | grind limestone / chalk / marble on a quern | the flux stream - burden's one remaining quality | `materialroles.json:3` |
| `game:coke` | vanilla coke oven - [coking](coking.md) | fuel: 1.0 carbon per item (role value 2 against `BfFuelCarbonReference` 2) | `materialroles.json:4` |
| `game:charcoal` | vanilla charcoal pit | fuel: 0.5 carbon per item (value 1) - two charcoal carry one coke's carbon ([fuels](../items/fuels.md)) | `materialroles.json:5` |
| air | [twin-tub blower](../machines/twin-tub-blower.md) on any vanilla axle source | the throttle - carbon burns only as fast as air arrives | `IwexConfig.cs` § Twin-tub blower |

Coke is not in the burden, and there is no separate fuel intake. The furnace has exactly one intake, the
tall hopper, and it takes coke and burden alternately; the coke ratio is a charging rhythm, not a mixing
ratio. Raw coal holds no role and must never be granted one - the `fuel` role is a shaft-charge permit, and
[coking](coking.md) exists precisely because raw coal does not survive a burden column
([fuels](../items/fuels.md) § Two taxonomies).

### Products

| Out | Where it appears | Denomination | file:line |
|---|---|---|---|
| molten pig iron | iron tap → canal → bed | 375 u per pig / 25 per chunk / 5 per bit | `ItemPig.cs:39-41` |
| molten slag | slag tap → canal → the same bed | `iwex:slagbrick`, 375 u (`SlagBrickUnits = ItemPig.PigUnits`) | `SlagItemDefinitions.cs:27` |
| `iwex:hearthmetal` | on the hearth floor, on extinguish | `game:metalbit-*` × the stamped count | `BlockHearthMetal.cs` |
| burnt-out charge | in the shaft, on extinguish | re-cokeable salvage - § The extinguish payout | `BlockEntityShaftFurnace.cs:1242-1292` |
| exhaust (hot furnace only) | the outlet cells | → cowpers + stack | [hot blast furnace](../machines/blast-furnace-hot.md) |

One carve shape casts both products - iron makes pigs, slag makes bricks - so a slag tap needs no second
station and no second gesture ([casting bed](../machines/casting-bed.md)).

---

## Numbers

Everything here is owned by this page or derived here from constants owned elsewhere, cited to its owner.

### Owned — the extinguish payout

Delegated to this page by [cold blast furnace](../machines/blast-furnace-cold.md) § Numbers. What a furnace
hands back when it dies, and the loop's recovery guarantee (R2).

| Key | Value | file:line | What it does |
|---|---|---|---|
| `BfUnitsPerSolidNugget` | 5 u | `IwexConfig.cs:496` | Molten units that freeze into one nugget of `iwex:hearthmetal` |
| `BfBurnoutFuelRetainedBottom` | 0.0 | `IwexConfig.cs:500` | Fraction of a fuel band surviving at the raceway, where the blast burned hardest - and the reason a dead furnace cannot relight itself |
| `BfBurnoutFuelRetainedTop` | 0.4 | `IwexConfig.cs:504` | Fraction surviving at the stockline, which the blast never reached |

How they are spent, on extinguish:

1. `SolidifyBottomLayer` (`BlockEntityFurnaceCore.cs:2108`). The pool becomes `floor(units ÷ 5)` nuggets,
   split deterministically (remainder to the first cells, no world RNG) across whichever `CellRole.Pool`
   cells are free. A cell whose column slice holds rejected charge is not free
   (`PileHoldsRejectedCharge`), so wrong-material salvage is never overwritten; if every pool cell is
   blocked the whole pool is silently lost, which is a real hole.
2. Burn-out rewrites the columns (`BlockEntityShaftFurnace.cs:1242-1292`). Fuel bands lose units by the
   height-interpolated retention; burden keeps its ore and flux verbatim; unstamped charge is stamped on the
   way out. The mechanism is [burden](../items/burden.md)'s; the payout is this page's.

The payout denomination is exact: `BfUnitsPerSolidNugget` = 5 and `ItemPig.BitUnits` = 5 are the same
number, so a nugget chipped out of a dead furnace and a bit shaken out of a bed are worth the same mass. The
alignment is a coincidence of two constants worth not breaking.

The burn-out gradient is the loop's re-entry point. A dead furnace's shaft is not waste: dig the column out
and the bottom comes back as bare burden while the top comes back 40 % of its coke intact - where a band sat
matters. The salvage is real items off real bands (`ChargeColumn.TakeSpan`; breaking a pile splices its own
window out).

### Owned — the hand-carry census

A hand-off is "automatic" if a machine moves the material without a player click.

| Hand-off | Automatic? | Why not |
|---|---|---|
| ore / flux → burdenmaker hoppers | no | two materials, RMB or Ctrl+RMB each; no feeder exists |
| burdenmaker → its own bunker | yes | the gate - one click drains both hoppers together |
| bunker basin → tall hopper | no | nothing pulls from the basin; the player is the buffer ([burdenmaker](../machines/burdenmaker.md) § Operation) |
| coke → tall hopper | no | same carry, alternating with the burden loads |
| hopper → shaft | yes | the drip, via `NextChargeColumn` - every column of any shaft at any facing |
| shaft → pool → tap → canal → bed | yes | the whole molten path is unattended |
| bed → inventory | no | one RMB per hardened mold, up to 20 per bed |
| bed → cleared and re-carved | no | and each harvest destroys the impression |

Everything liquid is automatic; everything solid is carried. The two carries into the furnace are the same
missing building - a skip hoist (the reinforced hopper was sized for one) - and the bed harvest is the
missing cast house. Both are on the roadmap as buildings rather than as fewer clicks.

### Derived — what a campaign is worth

First-pass arithmetic at the shipped constants and the reference richness (20 % carbon by items),
volume-weighted flux at the standard 5 %. `MeltSpeedFactor` (0.5×–2.0×,
[heat balance](../mechanics/heat-balance.md)) multiplies iron per carbon, so a superheated furnace beats
every per-coke figure below.

| Quantity | Value | Derived from |
|---|---|---|
| cold shaft capacity | 38 cells × 32 items = 1 216 items | `ChargeItemsPerBand` 2 × 16 bands (`IwexConfig.cs:759`); 38 chargeable cells (the layout, [cold blast furnace](../machines/blast-furnace-cold.md)) - capacity is geometry, `ChargeCapacityUnits`, not a key |
| a reference charge | 973 burden + 243 coke items | 4 : 1 - `BfBurdenPerCarbonUnit` (`IwexConfig.cs:383`) read as the 20 % grade |
| coke to fill it | 243 items ≈ 3.8 stacks | coke stacks to 64 |
| iron per coke item | 4 × 0.95 × 8.5 = 32.3 u | `BfBurdenPerCarbonUnit` × ore share × `BfIronPerOreUnit` (`IwexConfig.cs:470`; the 8.5 anchor is [roasting](roasting.md)'s) |
| pig from the full charge | 973 × 0.95 × 8.5 ≈ 7 860 u ≈ 21 pigs | above; pig 375 u ([pig](../items/pig.md)) |
| slag alongside | 973 × 0.95 × 8.5⁄6 ≈ 1 310 u ≈ 3.5 bricks | `BfSlagPerOreUnit` = 8.5/6 (`IwexConfig.cs:478`) |
| shaft-fulls per bed | 7 860 ÷ (20 × 375) = 1.05 | bed capacity 20 castings ([casting bed](../machines/casting-bed.md)) - the settled anchor: one shaft ≈ one bed |
| campaign length, blown | 243 ÷ (0.175 × 2) ≈ 695 s ≈ 11½ min | `BfRacewayCarbonPerTuyerePerSecond` × 2 tuyeres (`IwexConfig.cs:352`) |
| one burdenmaker batch | 512 ore + ~27 lime → ≈ 539 burden (≈ 4.2 stacks) | `BurdenmakerOreCapacity` (`IwexConfig.cs:703`); 5 % flux |
| batches per reference shaft | 973 ÷ 539 ≈ 1.8 | above |
| the same charge on charcoal | 486 items ≈ 7.6 stacks, and it runs cooler | charcoal is 0.5 carbon/item; a charcoal course must also be richer to melt - break-even ≈ 0.32 by volume ([fuels](../items/fuels.md)) |

### Cited — owned elsewhere, values not repeated here

| Key / fact | Owner |
|---|---|
| `BfRacewayCarbonPerTuyerePerSecond`, `BfBurdenPerCarbonUnit`, `BfRacewayGasPerCokeUnit`, `BfShaftGasTransferFrac`, `BfFuelCarbonReference`, `T_process` and every `BfCombustion*`/loss/blast key, the melt condition, the chill, ignition, the derived state | [heat balance](../mechanics/heat-balance.md) |
| `BfIronPerOreUnit` = 8.5 and why it is stated per ore unit, never per band | [metal recovery](../mechanics/metal-recovery.md) · [roasting](roasting.md) · `OreRecoveryGuardRailTests` |
| `ChargeItemsPerBand`, band order, courses, descent | [layered-charge](../layered-charge.md) |
| `BurdenProfiles` (the three flux bands), the stamp, `Burden.Is`/`IsCode` | [burden](../items/burden.md) |
| `BurdenmakerOreCapacity` / `FluxCapacity` / `BunkerCapacity` | [burdenmaker](../machines/burdenmaker.md) |
| `HopperTallCapacity`, `HopperTallDropPerSecond`, `NextChargeColumn` | [tall hopper](../machines/tall-hopper.md) |
| `TwinTubBlowerOutputPerSecond`, `TwinTubBlowerMaxPressure`, the 17.3 % gate | [twin-tub blower](../machines/twin-tub-blower.md) |
| `BfMaxMoltenIron`/`Slag`, `TapDrainPerTick`, the stack factors, the hearth band keys | [cold blast furnace](../machines/blast-furnace-cold.md) |
| `MoltenFlowRate`, per-cell capacity, cooldown, back-pressure | [molten network](../mechanics/molten-network.md) |
| `PigUnits` / `ChunkUnits` / `BitUnits`, `SlagBrickUnits`, bed capacity 20 | [pig](../items/pig.md) · [casting bed](../machines/casting-bed.md) |
| 1 vx³ = 2.5 u; 375 = 150 vx³ | [density rule](../mechanics/density-rule.md) |

---

## Why it is like this

The friction removed is proportioning and placement, not effort: vanilla smelts iron a bloomery-load at a
time, by hand, while this loop runs unattended from step 5 until the metal is solid and carriable. The
friction kept is layout and rhythm - the two manual hand-offs (burdenmaker → hopper, bed → inventory) are
where a real works needed a building, and the charging rhythm is band-order charging, with the shaft wall
showing the player their own sloppiness ([layered-charge](../layered-charge.md)).

Blast demand is a property of the charge, not of the furnace: coke is the permeable skeleton of the column,
so a rich charge blows easily and eats fuel while a lean one packs dense and needs pressure the iron tier
cannot raise. The tier gate needs no branch on tier anywhere - it is four numbers in an order
([twin-tub blower](../machines/twin-tub-blower.md)). Neilson's hot blast cut fuel by roughly two thirds
historically, and here that is a lean charge becoming viable rather than a hidden multiplier.

The cast house is pressure relief rather than optional storage: a tapped furnace cannot hold its metal, and
the pool cap is a disruption. Beds are free to run once carved, so they absorb a whole heat and become the
overflow once the steel tier is built rather than being replaced.

---

## Gotchas

1. Tap facing is load-bearing, and the live legends enforce it (verified against source 2026-08-07). The
   taps are typed blocks (`iwex:furnace-irontap` / `-slagtap`) and the layout legends are oriented - iron
   `WithSide(WEST)`, slag `WithSide(EAST)`, tuyeres likewise (`BlockBlastFurnaceCoreCold.cs:66`, `:80`,
   `:94-95`) - so a wrong-facing or wrong-type part reads as a build-outline mismatch rather than a silent
   dead tap. The invariant, pinned by `BlastFurnaceTapTests`: the `side` variant faces into the furnace, and
   the tap pours to `side.Opposite`, one down. Caution: [layouts-workbench](../../workbench/layouts.md)'s
   cold draft, marked stale on that page, still carries bare tap legends (the hot draft's facings are
   correct); the source and the golden are the arbiters, not the workbench drafts.

2. A shaft of the wrong material still lights, still burns and still burns out. Only conversion is gated:
   the state derives to Firing, never Melting, while unconvertible charge stands in the shaft, and the
   salvage survives the freeze ([burden](../items/burden.md) § Gotchas 2; `PileHoldsRejectedCharge`). The
   HUD's shaft-charge line and the honest Firing label are the only warnings.

3. The chill is the loop's intended failure, and it is fast. An under-coked course arrives at the raceway
   cold, cannot melt, and the column above physically rests on it - a hang. A hung column brings no fresh
   carbon down, so the fire in front of that tuyere starves within a couple of minutes and the whole shaft
   goes Idle. Visible (the band sits there, dark), diagnosable, and the fix is more coke next campaign
   ([heat balance](../mechanics/heat-balance.md)).

4. Coke burns whenever the furnace is lit, not only while it produces. A furnace sitting hot and melting
   nothing - chilled, conversion-blocked, or simply out of burden - is still burning its campaign away.
   Campaign length is the carbon charged.

5. Breaking a bed voids its molten charge silently ([casting bed](../machines/casting-bed.md) § Open).
   Everything else in the loop returns its contents on break: the burdenmaker drops both hoppers and the
   bunker, the hopper drops its tank, a charge pile splices its own window out of the column and drops it.

6. A charcoal pit's own pile is invisible to the loop. Vanilla's `charcoalpile` is a different block from
   `coalpile`, and nothing in the suite matches it - pit charcoal must be picked up and re-charged through
   the hopper ([fuels](../items/fuels.md) § Gotchas).

---

## Open

1. The automation gap is one missing building. Burdenmaker → hopper and coke → hopper are hand carries, and
   a skip hoist would close both; it has been assumed by two generations of hopper sizing and exists
   nowhere. The bed harvest is the same story as a cast house.

2. The bed rotation has no representation. Whether it should acquire one - a clearing timer, a bed-ready
   signal, anything - or stay a pure player practice is undecided. Today the only feedback a player gets
   that they are under-provisioned is the furnace dying.

3. Direct charging has no routing aid. The exit works, but selecting it means physically building a
   different canal. Whether a valve, a switchable junction or a tap-side destination display belongs here is
   open, and it interacts with the fact that a canal run can already carry two metals side by side with
   nothing to combine them ([molten canal](../machines/molten-canal.md) § Open).

4. The ferroalloy second act is unbuilt, and it is what keeps the cold furnace relevant after hot blast
   ([cold blast furnace](../machines/blast-furnace-cold.md) § Its second act). No third charge material, no
   ferroalloy metal descriptor, no second product code.

5. Flux is inert ([burden](../items/burden.md) Open 1). It is the burden's only quality and it drives
   nothing - no slag volume, no melt behaviour, no refusal. The loop's step 4 currently asks the player to
   hit a ratio that has a name and no consequence.

6. Roasting is unbuilt and already promised. The burdenmaker's help text offers "crushed or roasted iron
   ore" while only `crushed-iron` holds the role; whether roasted ore is a distinct input or a
   better-yielding substitute is [roasting](roasting.md)'s call ([burdenmaker](../machines/burdenmaker.md)
   § Open 1). The 8.5 → 9.2 u/nugget recovery step is the loop's designed reward for building it.

7. The economy has not been re-checked against the anchors as a whole since the charge chain landed
   ([burden](../items/burden.md) Open 4). [economy-landing](../items/economy-landing.md) owns the trigger.
