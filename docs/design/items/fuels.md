# Fuels — coke, charcoal, coal, and the beds they burn in

**Status** built for the shaft, the firebox and the Cornish boiler - coke and charcoal are two priced
fuels a shaft furnace charges, burns and tells apart, at a 2:1 carbon ratio; the firebox furnaces and the
Cornish boiler burn from their own fuel beds and admit a fuel by its own
`combustibleProps.BurnTemperature` against a floor, with the metallurgical exclusion of low-rank coal
living on the furnace rather than the bed; bulk coking and producer gas are designed only
**Mod** the role data is iiex (`assets/iiex/config/materialroles.json`); the registry that serves it is
exlib; the cowper and the Lancashire boiler still burn vanilla coal piles

**Owns** - the facts this page is canonical for:

* the suite's fuel taxonomy: that "fuel" means the `fuel` material role, which two codes hold it, what the
  per-item value means, and that coal holds no role at all;
* `assets/iiex/config/materialroles.json` as a file - every role it grants, and the fact that it is the
  only `materialroles.json` in the repo;
* that granting `Roles.Fuel` grants a shaft-charge permit, and the taxonomy rule that follows from it -
  bituminous and anthracite must never receive the role;
* the carbon conversion (`CarbonPerUnit` = role value ÷ `BfFuelCarbonReference`), what a missing value
  prices at, and which direction that error runs in;
* the fuel substrates - which machines hold fuel in firebox beds, which burn a vanilla coal pile, and how
  the C# finds each;
* the firebox's own burn-temperature floor (`BEBehaviorFirebox.IsFuel`) as a taxonomy distinct from the
  role, and the furnace-level metallurgical exclusion (`BlockEntityFireboxFurnace.AcceptsFireboxFuel`)
  layered on top of it;
* the cowper's substring classification as a third, incompatible fuel test;
* the vanilla fuels' own published numbers, and that `BurnTemperature` and `BurnDuration` are read
  directly: the temperature is the bed's whole admission test and the Cornish boiler's rate term, the
  duration is how fast that boiler draws its bed down;
* producer gas's place in the taxonomy - the one gaseous fuel that stayed in scope, with no role, no medium
  and no code (everything else about it is [gas producer](../machines/gas-producer.md)'s).

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| Band order, courses, the charge column, and how fuel reaches a shaft at all | [layered charge](../layered-charge.md) |
| The burdenmaker's inputs and the burden stamp | [burdenmaker](../machines/burdenmaker.md) |
| What the carbon fraction does to `T_in`, blast pressure and tuyere draw | [heat balance](../mechanics/heat-balance.md) |
| The burden item, its stamp, its grades | [burden](burden.md) |
| The firebox block, the bed's layer arithmetic, the pool rule | [firebox](../machines/firebox.md) |
| The bulk coke oven - its bank, its lid rule, its yields | [coke oven](../machines/coke-oven.md) |
| The gas producer, the tar boundary, the scope carve-out argument, the producer-gas medium spec | [gas producer](../machines/gas-producer.md) |
| The cowper's regenerator model and every `Cowper*` value | [cowper](../machines/cowper.md) |
| Boiler firing, choking and the extinguish timer | [Cornish boiler](../machines/boiler-cornish.md) · [Lancashire boiler](../machines/boiler-lancashire.md) |
| Layout legends, wildcard matching and filler semantics | [multiblock](../mechanics/multiblock.md) |
| The design table and the diagram system | [diagram crafting](../mechanics/diagram-crafting.md) |

**Depends on** [burden](burden.md) · [burdenmaker](../machines/burdenmaker.md) ·
[heat balance](../mechanics/heat-balance.md) · [coke oven](../machines/coke-oven.md) ·
[gas producer](../machines/gas-producer.md) · [cowper](../machines/cowper.md) ·
[multiblock](../mechanics/multiblock.md)

---

## Role

The suite adds no fuel item. It adds a classification - one JSON file that says which vanilla items count
as carbon and how much each is worth - and two substrates: the firebox bed, in a block for the hearths and
as a hosted behaviour inside the Cornish boiler, and the vanilla coal pile where it survives (the cowper
and the Lancashire boiler).

A fuel-role grant decides four separate things:

| Question | Answered by | Where |
|---|---|---|
| May this enter a shaft furnace's hopper at all? | `IsFuelStack` → the role | `BlockEntityShaftFurnace.IsChargeItem` (`:302-303`) |
| May it be laid on this column right now? | `IsFuelCode` on the column's top band | `BlockEntityFurnaceCore.NextChargeColumn` (`:947-959`) |
| How much carbon does a unit of it carry? | `CarbonPerUnit` → the role's value | `BlockEntityFurnaceCore.CarbonPerUnit` (`:1356`) |
| What does the HUD call it? | the resolved item's own display name | `BlockEntityFurnaceCore.CourseFuelName` (`:2533`) |

The heat side does not use the role. The firebox furnaces (puddling, reheat, crucible, the coke oven) and
the Cornish boiler burn from `BEBehaviorFirebox` beds, which admit fuel by
`combustibleProps.BurnTemperature` against `IiexValues.BoilerFuelMinTemp` - not a list, so any item
declaring a high enough burn temperature clears it. The owning machine narrows that with its own rule
where it has one (`BlockEntityFireboxFurnace.AcceptsFireboxFuel`); a boiler declares none, so it burns
whatever the bed takes. The [cowper](../machines/cowper.md) classifies the pile below it by substring; the
Lancashire boiler reads `IsBurning` and nothing else. "Can I burn X here?" has different answers depending
on the machine family.

### Two taxonomies, and they must stay apart

| | The shaft asks | The bed asks | The furnace asks |
|---|---|---|---|
| Question | is this a carbon reductant that survives being under a burden column? | does this burn hot enough at all? | will this carry a metallurgical heat? |
| Answer | `Roles.Fuel` | `BEBehaviorFirebox.IsFuel` - `BurnTemperature` ≥ `BoilerFuelMinTemp` | `BlockEntityFireboxFurnace.AcceptsFireboxFuel` |
| Coke | yes | yes | yes |
| Charcoal | yes | yes | yes |
| Bituminous / anthracite | no | yes | yes |
| Lignite | no | yes | no - low-rank coal, excluded by name (`BlockEntityFireboxFurnace.IsLowRank`) |

The third column is a *furnace's* question, and a boiler never asks it: a vessel that only has to beat
157 °C has no metallurgical stake in the rank of its coal, so the Cornish boiler burns lignite and takes
a 9 % rate cut for it ([Cornish boiler](../machines/boiler-cornish.md) § Fuel drives the rate). The coke
oven narrows the same seam a third way - it takes only the coals that coke - and each machine states its
own refusal, because "will not carry a metallurgical heat" is nonsense from a retort refusing charcoal
(`BlockEntityFireboxFurnace.RefuseFireboxFuel`, overridden at `BlockEntityCokeOven`).

Granting `Roles.Fuel` to raw coal would silently make it chargeable into a blast furnace, because
`BlockEntityShaftFurnace.IsChargeItem` accepts anything holding the role. Raw coal crushes to dust under a
burden column and [coking](../processes/coking.md) forbids it in a shaft. Bituminous coal is the
reverberatory fuel, so the firebox takes what the shaft must refuse. The two taxonomies overlap on two
codes and diverge on three; merging them deletes a whole tier's reason to exist.
`FuelRoleGrantTests` enumerates every grant and fails on one that is chargeable but unburnable, or priced
but unacceptable.

---

## The catalogue

| Fuel | Item code | Role + value | Vanilla burn (°C × ticks) | Read by | Made by |
|---|---|---|---|---|---|
| Coke | `game:coke` | `fuel`, 2 (`materialroles.json:4`) | 1340 × 40 (`.game/1.20/assets/survival/itemtypes/resource/coke.json`) | every shaft furnace (charge, band order, carbon, HUD); firebox beds | vanilla's coke oven - the suite adds none; the bulk [beehive oven](../machines/coke-oven.md) is designed, not built |
| Charcoal | `game:charcoal` | `fuel`, 1 (`materialroles.json:5`) | 1300 × 40 (`…/resource/charcoal.json`) | as coke, at half its carbon; firebox beds | vanilla charcoal pit |
| Anthracite | `game:ore-anthracite` | none | 1200 × 196 (`…/resource/ore-ungraded.json`) | firebox beds; the Cornish boiler's bed; the [cowper](../machines/cowper.md), by substring (§ The cowper's taxonomy) | mining |
| Bituminous coal | `game:ore-bituminouscoal` | none | 1200 × 84 (`…/resource/ore-ungraded.json`) | firebox beds; the Cornish boiler's bed; the coke oven's charge; the cowper, as "other coal" | mining |
| Lignite | `game:ore-lignite` | none | 1100 × 77 (`…/resource/ore-ungraded.json`) | the Cornish boiler's bed, at a 9 % rate cut; a firebox bed takes it and the reverberatory furnace above then refuses it; the cowper reads it as "other coal" | mining |
| Producer gas | — | none - no item, no medium, no code | — | nothing | nothing - see [gas producer](../machines/gas-producer.md) |

Vanilla's own `burnTemperature` is read directly - it is the firebox bed's whole admission test
(`BEBehaviorFirebox.IsFuel`/`BurnTemperatureOf`), against `IiexValues.BoilerFuelMinTemp`, and the Cornish
boiler's rate term on top of that. `burnDuration` is read the same way (`BurnDurationOf`) and is what draws
that boiler's bed down, one unit per the fuel's own declared seconds - which makes duration, not flame
temperature, the thing that separates one boiler coal from another
([Cornish boiler](../machines/boiler-cornish.md)). Combustion temperature for the shaft/heat-balance path
remains [heat balance](../mechanics/heat-balance.md)'s `T_in`, computed from the charge, not from the item -
that reading is unrelated to the firebox's own admission test.

### The whole role file

`assets/iiex/config/materialroles.json` is nine grants, and it is the only file of its kind in the repo (a
repo-wide `find` for `materialroles.json` under `assets/` returns exactly one path). exlib ships none - it
is framework, and with no file and no contributor the registry is empty (`MaterialRoleLoader.cs:11-14`).

| Line | Role | Matcher | Value | Consulted by |
|---|---|---|---|---|
| `:3` | `flux` | `game:lime` | — (default 1) | the [burdenmaker](../machines/burdenmaker.md) (`BlockEntityBurdenmaker.cs:167`) |
| `:4` | `fuel` | `game:coke` | 2 | `IsFuelCode` / `CarbonPerUnit` |
| `:5` | `fuel` | `game:charcoal` | 1 | as coke |
| `:6-7` | `scrap` | `game:metalbit-iron` · `game:metalbit-steel` | — | [cupola](../machines/cupola.md) remelt charge; Bessemer scrap charge |
| `:8-10` | `scrap` | `iiex:pig` · `iiex:pigchunk` · `iiex:pigbit` | — | cupola remelt charge |
| `:11` | `ironore` | prefix `crushed-iron` | — | burdenmaker (`IronOreCompat.cs:28-29`) |

There is no `charge` row. Prepared burden is recognised by its own item identity (`Burden.Is`), never by a
role - the `charge` role is a taxonomy entry exlib defines (`MaterialRoleDef.cs:64`), iiex grants to
nothing, and no code consults.

Caution: `MaterialRoleSeeds` (`test/IronIndustryExpanded.Tests/Fixtures/MaterialRoleSeeds.cs`) is a second,
hand-written copy of this table for headless runs, and it has drifted before (`game:metalbit-steel` was
scrap in the game and not in the tests).
`FuelRoleGrantTests.Seed_matches_the_shipped_materialroles_json` compares the two as sets, in both
directions.

Two matcher kinds only: exact domain-normalised `Code`, or a domain-blind `Code.Path.StartsWith` prefix
(`MaterialRoleDef.cs:23-29`, matched at `MaterialRoleRegistry.cs:121-126`). A def with neither is skipped
with a warning (`MaterialRoleLoader.cs:55-63`).

Adding a fuel is one JSON line, no recompile (`MaterialRoleDef.cs:8-10`). The only registrations that need
code are mod-gated ones, which go through the contributor seam
(`MaterialRoleRegistry.RegisterContributor`, `:57-61`; the one live example is
`IronOreCompat.Contribute`, `:34-51`, registered at `IronIndustryExpandedModSystem.cs:64`). Contributors are
re-invoked after every clear (`MaterialRoleLoader.cs:30-31`), which is what makes them survive a world
reload; the whole load runs at `AssetsFinalize` (`ExpandedLibModSystem.cs:74`).

### What "value 2" means, and the 2:1 ratio

The value is coke-equivalent carbon per item, not a heat figure and not a burn time. The shaft divides it
by a reference fuel:

```
CarbonPerUnit(material) = ValueOf(Roles.Fuel, material) / IiexValues.BfFuelCarbonReference
                          coke 2/2 = 1.0    charcoal 1/2 = 0.5    non-fuel = 0
```

`BfFuelCarbonReference` (= 2, coke's own value) says which fuel every coke figure in `IiexConfig` is
written in. Move it and every one of them changes meaning; the ratio itself lives in the JSON and only the
calibration point lives in config.

Settled 2026-08-06: charcoal is 1, half of coke. At 2:1 charcoal costs about ten points of course richness
- break-even sits near φ = 0.32 by volume against coke's ~0.24 - so it is a viable early furnace fuel that
a player graduates away from. At 4:1 the volume needed to clear the melt line no longer fits in a round.

Carbon is spent per segment, never per band. `BurnCarbon` converts inside the rewrite walk, because a
raceway slice routinely spans a coke course and a charcoal one; resolving the weight once before the walk
would spend one at the other's price depending only on which the player laid first. A band-counting model,
where "fuel fraction" is volume, makes charcoal a pure speed buff: it reads 30 % at 30 %, melts at coke's
temperature, makes the same iron from the same items and merely finishes sooner.

A forgotten value is silently charcoal, never silently coke. `MaterialRoleRegistry.ValueOf` returns the
first matching def that carries a value, else the caller's fallback of 1.0 - which against a reference of
2.0 is half of coke. A mod's new fuel under-performing is a charge the player can see and fix; one that
out-performs coke for free is a furnace running hotter than its charge justifies with no attributable
symptom.

---

## Where fuel physically sits

Three substrates, by machine family.

### Shaft furnaces — the charge column

Fuel in a shaft stands in `iiex:furnace-chargepile` blocks the furnace owns and places, as its own bands in
the charge column ([layered charge](../layered-charge.md)). The layout legend is
`*:@(air|coalpile|furnace-chargepile)` (`IiexCodes.ChargeShaft`, `IiexCodes.cs:50`):

| Machine | Legend site |
|---|---|
| cold blast furnace | `BlockBlastFurnaceCoreCold.cs:107` |
| cupola | `BlockCupolaFurnaceCore.cs:79` |
| hot blast furnace (smex) | `BlockBlastFurnaceCoreHot.cs:87` |

The tall hopper drips into charge columns directly; it neither scans for nor seeds `game:coalpile`
(`BlockEntityHopperTall.cs:185`).

### Firebox furnaces — the fuel bed

The puddling and reheat furnaces hold fuel in a required `iiex:furnace-firebox` block per fuel cell
(`BlockPuddlingFurnaceCore.cs:104`, `BlockHeatingFurnaceCore.cs:72`; the block and its layer arithmetic are
[firebox](../machines/firebox.md)'s). The bed is `BEBehaviorFirebox` - one fuel per bed, 6 layers × 2 units
per cell by default, refill and dig-out through the bed itself. A legend that admitted air here would let
an empty firebox complete the furnace, which is why the block is required rather than an `@(air|coalpile)`
cell (`BlockPuddlingFurnaceCore.cs:104-110`).

`BEBehaviorFirebox.IsFuel` (`:34-35`) reads the stack's own `combustibleProps.BurnTemperature` against
`IiexValues.BoilerFuelMinTemp` - no list, so a fuel another mod ships is admitted by declaring what it
already declares. Lignite clears this floor; a reverberatory hearth refuses it anyway, one layer up, in
`BlockEntityFireboxFurnace.AcceptsFireboxFuel` (`:198-199`), whose private `IsLowRank` (`:203-204`) is the
name check that used to live on the bed. A bed holds one fuel at a time (`Accepts`, `:160-162`).

### Internal beds — the Cornish boiler

The bed is a behaviour rather than a block feature, so a machine can host one with no firebox block in
sight. The Cornish boiler declares `BEBehaviorFirebox` on its own block entity, with its own geometry -
4 layers × 4 units = 16, drawn as the boiler shape's own `CoalLayers/L1`..`L4`
(`BlockBoilerCornish.cs:49-56`) - and reads it back through `BlockEntityBoiler.Bed`
(`BlockEntityBoiler.Client.cs:144`). No layout legend, no fuel cell, nothing for the player to place: the
fire is inside the vessel, charged and lit through its main hatch.

It declares no `AcceptsFireboxFuel` of its own, so every fuel the bed takes fires it, lignite included.
What the fuel changes is the rate and how long the bed lasts, both read off the same `combustibleProps`
([Cornish boiler](../machines/boiler-cornish.md) § Fuel drives the rate).

### Vanilla coal piles — the cowper and the Lancashire boiler

Two machines still burn a free-placed vanilla `game:coalpile`
(`VanillaCodes.CoalBed` = `@(air|coalpile)`, `VanillaCodes.cs:192`):

| Machine | How the cell is declared | How it reads the pile |
|---|---|---|
| cowper stove (siex) | layout legend `'c'` (`BlockCowperStoveIntake.cs:58`) | prefix test on the block below, then a substring branch on the pile's contents (`BlockEntityCowperStove.cs`) |
| Lancashire boiler (siex) | **not declared at all** - the vessel lost its `MultiblockLayout` with the shared multiblock base, so nothing requires the pile. The boiler simply looks at its `fuelOffset` `(0,0,-1)`, a cell outside its own footprint | `GetBlockEntity(FuelWorldPos) as BlockEntityCoalPile`, then `IsBurning` + non-empty (`BlockEntityBoiler.PileIsBurning`, `:469-475`) |

The shared `BlockEntityBoiler` carries both fire models and picks between them on whether the leaf declares
a bed (`:308-310`). The pile branch goes away when the Lancashire is converted
([Lancashire boiler](../machines/boiler-lancashire.md)).

The pile is vanilla, unmodified. `.game/1.20/assets/survival/blocktypes/coalpile.json` declares one block
whose texture set covers `charcoal`, `coke`, `ore-anthracite`, `ore-lignite`, `ore-bituminouscoal` and
`ember` (`:17-24`), `materialDensity: 600`, `replaceable: 100`, and `drops: []` (`:29`). Whatever recovery
a pile gives is the block entity's, not a drop table's. `StartsWith("coalpile")` does not match vanilla's
`charcoalpile` - the charcoal pit's own output block is invisible to the machines that still read piles.

### The charge read, per machine family

| Family | What it counts | Consequence | file:line |
|---|---|---|---|
| blast furnace / cupola | burden or fuel, as separate bands in a charge column | a shaft full of coke lights and burns; it makes no iron, because there is no ore between the fuel | `BlockEntityShaftFurnace.cs:278-282`, `BlockEntityFurnaceCore.NextChargeColumn` |
| firebox furnaces (reheat, puddling) | bed units, reported as pure fuel ("a firebox is all coke and nothing else") | nothing is refused at the read, because a bed only ever holds fuel it already accepted; identity is gated at the bed, not the walk | `BlockEntityFireboxFurnace.cs:108-126` |
| Cornish boiler | its own bed's `Units`, plus the charged fuel's `BurnTemperature` and `BurnDuration` | the fuel is spent: one unit leaves the bed per the fuel's own declared seconds, and the bed going empty puts the fire out | `BlockEntityBoiler.cs:308-310`, `:491-504` |
| Lancashire boiler | `IsBurning` and "not empty". Nothing else. No item test, no stack consumption | any pile of anything fires it forever | `BlockEntityBoiler.PileIsBurning`, `:469-475` |

---

## The cowper's taxonomy

Caution: smex classifies fuel its own, incompatible way. The [cowper stove](../machines/cowper.md) reads
the pile below it and branches on `path.Contains("anthracite")` - anthracite, other coal, or nothing -
choosing one of three soak factors (`BlockEntityCowperStove.cs:127-149`; the values are
[cowper](../machines/cowper.md)'s).

It does not use the role registry, and the branch has sharp edges:

* a substring test, so `crushed-anthracite` or any modded `*anthracite*` matches by accident;
* `game:coke` - the tier's actual fuel - falls into the "other coal" branch, not a coke branch;
* charcoal falls into "other coal" too;
* adding an anthracite-grade fuel through `materialroles.json` (the documented seam) has no effect here.

Proposed: a `fuel` value the stove reads, or a second role (`hotfuel`?) - either way one JSON entry plus
one line. The firebox's own admission test (`BEBehaviorFirebox.IsFuel`) no longer keeps a list of its own -
it reads `combustibleProps` directly - so this substring problem is now the cowper's alone.

---

## Assets

None. The fuel line adds no shape, no texture, no animation and no lang key of its own.

| Asset | Path | State |
|---|---|---|
| role catalogue | `assets/iiex/config/materialroles.json` | present, nine grants; the only one in the repo |
| coke / charcoal / coal items | vanilla | untouched - no patch, no override, no recipe in any `Recipes/` provider references either code |
| coal pile block | vanilla `game:coalpile` | untouched |
| burden's coal-black look | `game:block/coal/orecoalmix` | borrowed by [burden](burden.md), not by a fuel |
| charcoal in art | `assets/iiex/shapes/crafting/designtable.json:13` binds `game:block/coal/charcoal` for the sticks on the desk | decorative only |

No recipe in the suite consumes coke or charcoal. A grep for `coke`/`charcoal` across every shipped recipe
golden (`test/*/goldens/*/recipes/`) and every `Recipes/` provider returns nothing. Fuel enters the economy
as charge bands or firebox beds - dripped by a hopper or loaded by hand
([layered charge](../layered-charge.md)).

---

## Code

| Type / member | file:line | Role |
|---|---|---|
| `Roles` (constants) | `src/ExpandedLib/Materials/MaterialRoleDef.cs:49` | `Flux` `:52`, `Fuel` `:55`, `IronOre` `:58`, `Scrap` `:61`, `Charge` `:64` - strings, not an enum, so a mod can invent a role by shipping one (`:44-48`) |
| `MaterialRoleDef` | `…/MaterialRoleDef.cs:18` | `Role` `:21`, `Code` `:25`, `PathPrefix` `:29`, `Value` `:33` |
| `MaterialRoleCatalogue` | `…/MaterialRoleDef.cs:38` | the file shape: one `materials` array |
| `MaterialRoleRegistry` | `…/MaterialRoleRegistry.cs:23` | `Register` `:37`, `Clear` `:52`, `RegisterContributor` `:57`, `IsRole` `:77`/`:89`, `ValueOf` `:94`/`:107`, `OfRole` `:113`, `Matches` `:121` |
| `MaterialRoleLoader` | `…/MaterialRoleLoader.cs:16` | `Load` `:20` (clear → overlay → contributors), `Overlay` `:39` (pure, unit-testable) |
| `IronOreCompat` | `src/IronIndustryExpanded/Compat/IronOreCompat.cs:16` | the one live contributor: IndustrialStory and Expanded Matter ore codes, gated on `IsModEnabled` (`:34-51`) |
| `BlockEntityFurnaceCore.IsFuelCode` / `IsFuelStack` / `CarbonPerUnit` | `…/Furnaces/BlockEntityFurnaceCore.cs:1313`, `:1328`, `:1356` | the shaft-side fuel seams |
| `BEBehaviorFirebox.IsFuel` | `…/Furnaces/BEBehaviorFirebox.cs:34-35` | the bed-side fuel test - `BurnTemperature` against `BoilerFuelMinTemp`, no list |
| `BlockEntityFireboxFurnace.AcceptsFireboxFuel` | `…/Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs:197-198` | the furnace-side narrowing - refuses low-rank coal (`IsLowRank`, `:202-203`) that the bed itself would take |

### Where a caller hooks in

* To make an item a fuel: add `{ "role": "fuel", "code": "<domain>:<code>", "value": <n> }` to a domain's
  `config/materialroles.json`. No code, no recompile. The grant is a shaft-charge permit: `IsChargeItem`
  accepts burden identity or the fuel role (`BlockEntityShaftFurnace.cs:302-303`), which is why coal must
  never receive it (§ Two taxonomies, and they must stay apart).
* To make a machine care what it burns: call `MaterialRoleRegistry.ValueOf(Roles.Fuel, stack, 1f)`. It is
  world-free and null-safe (`:107-110`), so it works headless.
* To gate on a mod being present: `MaterialRoleRegistry.RegisterContributor` from the mod's `Start`
  (`:57-61`), following `IronOreCompat.Init` (`:19-20`).

---

## Gotchas

1. A code literal where a role test belongs is invisible until a second fuel exists, and the suite shipped
   three of them. `NextChargeColumn`'s band-order rule read `top == material` rather than
   `IsFuelCode(top)`, which is the same answer for every case a one-fuel shaft can produce: coke onto coke
   is refused either way. With charcoal in the game it laid a second fuel course straight onto the first -
   nothing throws, the units add up, the bands draw, and the furnace lights on a shaft that can never make
   iron. The other two were in test fixtures, where a `segment.Material == "game:coke"` reader made every
   campaign assertion read zero carbon on a charcoal furnace and pass for the wrong reason. Fixed
   2026-08-06; `HopperTallTests.Charcoal_will_not_go_onto_a_column_topped_with_COKE` is the only case in
   the suite that can tell the two spellings apart.

2. A coal-pile boiler never consumes fuel, and the two boilers are on opposite sides of that. The
   Cornish's bed is drawn down one unit per the fuel's own `burnDuration` (`BlockEntityBoiler.cs:508-521`);
   the Lancashire's read is `pile?.IsBurning` and whether slot 0 is empty, with no stack ever decremented
   (`:469-475`), so whatever the vanilla pile does on its own is the entire fuel economy of that vessel.

3. The firebox's fuel test reads `combustibleProps.BurnTemperature` directly against
   `IiexValues.BoilerFuelMinTemp` (`BEBehaviorFirebox.cs:34-35`), not a list or a substring - a modded fuel
   is admitted by declaring what it already declares, with no code change needed. It does not consult the
   role registry: bituminous and anthracite burn in a firebox while holding no role, which is the
   two-taxonomy rule working as intended.

4. `FirstCodePart() == "coal"` matches nothing in vanilla. The design table accepts a drawing medium if
   `code.Path == "charcoal" || code.FirstCodePart() == "coal"` (`BlockEntityDesignTable.cs:157-159`), but
   vanilla's coals are `ore-lignite` / `ore-bituminouscoal` / `ore-anthracite` - first code part `ore` -
   and the only vanilla code beginning "coal" is the `coalpile` block, whose first part is `coalpile`. The
   coal branch is dead; only charcoal draws. (The table itself is
   [design table](../machines/design-table.md)'s; the fuel-identity bug is this page's.)

5. A `fuel` def with no `value` prices at 1, with no warning (`MaterialRoleRegistry.cs:94-104`) - which
   since the 2:1 retune is exactly charcoal, the worse of the two shipped fuels, so omitting the field
   costs a mod author carbon rather than granting it. Pinned by
   `FuelRoleGrantTests.A_fuel_grant_with_no_Value_burns_at_the_registry_fallback_not_at_cokes`.

6. Values are read per-item, not per-unit-mass. A lump of coke is worth 2 whatever its stack size. There is
   no density, no volume and no relation to [density rule](../mechanics/density-rule.md)'s 2.5 u/vx³ - fuel
   is counted, metal is massed, and the two scales never meet.

7. `ValueOf` returns the first matching def with a value, so two `fuel` defs for the same code silently
   resolve by file order across domains (`MaterialRoleRegistry.cs:96-103`). There is no conflict warning.

8. `charcoalpile` is invisible to the machines that still read piles (§ Where fuel physically sits). The
   one-character prefix difference is easy to miss when authoring a new layout legend.

9. The registry is empty before `AssetsFinalize`. Any classification done earlier in load order classifies
   nothing rather than throwing (`MaterialRoleRegistry.cs:18-20`), so a mis-ordered read is a silent no-op,
   not an error.

---

## Open

1. The heat side has no role. The `fuel` role drives charging, band order, pricing and naming across all
   three shaft furnaces; the machines that burn for heat rather than for carbon each carry their own test -
   the firebox bed's burn-temperature floor, the cowper's anthracite substring, the Lancashire boiler's
   bare `IsBurning`. The fix
   is not to widen `Roles.Fuel` to reach them, because that grants a shaft-charge permit as a side effect
   (§ Two taxonomies, and they must stay apart). It is a second role (`hotfuel`?) that coal may hold and
   the shaft never asks about, with the firebox and the cowper as its first two consumers. The bed's own
   floor has narrowed the gap: everything reading a bed now reads `combustibleProps`, so what is left
   outside is the cowper and the one boiler still on a pile.

2. Coal is not a shaft fuel anywhere, and it must not become one via `Roles.Fuel`. The handbook says raw
   coal "makes a charge the furnace will grade as off-spec", which is not what the code does: coal is
   refused outright at the shaft, never accepted at a poor value. The handbook line is the thing to change,
   because granting coal a low `fuel` value would make it chargeable into a blast furnace, which
   [coking](../processes/coking.md) forbids. Raw coal's legitimate fire is the firebox, where it already
   burns.

3. ~~Bulk coking is unbuilt.~~ **Closed 2026-08-21** (U9.1-U9.4): the
   [beehive oven](../machines/coke-oven.md) stands, bakes and is craftable, so the one item carrying the
   `fuel` role at full value has a supply route inside the suite.

4. Producer gas is the one gaseous fuel that stayed in scope, and there is nothing to point at. No item, no
   `LiquidDef`, no medium string, no config key, and the machine that would make it has no consumer either.
   The carve-out reasoning, the medium spec and the build list are all
   [gas producer](../machines/gas-producer.md)'s; what this page records is that the fuel taxonomy has no
   gas branch at all, and that when it gains one it will not go through `Roles.Fuel` (a gas arrives on a
   pipe, not in a pile).

5. No fuel is recoverable from a fire that consumes it. R2 (declared recovery) is satisfied for burden - a
   dead furnace's column is re-cokeable ([ironmaking](../processes/ironmaking.md)) - and a firebox bed
   gives layers back (`BEBehaviorFirebox.TryTakeLayer`) and keeps a salvage fraction on burn-out. A boiler
   bed has neither route: the vessel offers no take-a-course verb at all - an empty hand at its main hatch
   is the swing/light gesture - so a mischarged bed can only be burned off, and breaking the vessel takes
   what is left with it. Whether that matters is undecided; the
   [gas producer](../machines/gas-producer.md) raises the same question for its bed.
