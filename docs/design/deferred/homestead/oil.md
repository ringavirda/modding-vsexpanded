# Oil - the derrick and the refining branch

**Status** deferred   **Would live in** Industrial Homestead
**Deferred by** the metalworking-only cut - [scope.md](../../scope.md), which owns the decision
("a whole extraction industry with no metalworking consumer"). The specification comes from the archived
iiex spec (git history) and is restated below.

**Owns**

- The derrick's design as it stood - worldgen reservoir, prospectable, finite and non-recharging, lift
  depth-gated by pump tier - and the products the refining branch was to yield.
- The fact that separates oil from every other Homestead item: it is an elex dependency, not merely a
  domestic one. Petcoke - the filler half of a graphite arc-furnace electrode - is a refining residue, which
  makes this the one deferred domestic branch a spine tier is waiting on.
- The severity of that dependency: a degraded path, not a wall.
- The R1 consequence for any refinery: one medium per run means one run per fraction. Recorded nowhere else.

**Depends on**

[scope.md](../../scope.md) - the cut, the carve-outs, elex's three dependency severities ·
[conventions.md](../../conventions.md) - the phase-change/distillation model that stays, and R1 ·
the archived iiex spec (git history) · [arc furnace](../elex/arc-furnace.md) - the electrode mechanic and
its carbon fallback · [pipe network](../../mechanics/pipe-network.md) - media, one-medium-per-run, burst and
joints · [pumps](../../machines/pumps.md) - the lift device a derrick would reuse ·
[fuels](../../items/fuels.md) - what the spine actually burns.

---

## What it is

The petroleum industry from 1859: a derrick over a reservoir, crude lifted by pump, and a still that splits
it by boiling point.

| Cut | Period product |
|---|---|
| light | naphtha |
| middle | kerosene - the lamp fuel the whole industry existed for |
| heavy | lubricating oil |
| residue | petroleum coke (petcoke) - the bottom product, what does not boil in range |

The residue row is already written into a live section of the docs:
[conventions.md](../../conventions.md) § Distillation & phase change names the residue case of the shipped
model as "the bottom product (pitch / petroleum coke)".

---

## Why it is deferred

[scope.md](../../scope.md) - a whole extraction industry with no metalworking consumer.

Two supporting facts that are this page's to hold:

- The derrick is a second extraction industry. The suite already has one (ore: prospecting, deposits,
  crushing, burden-making). Oil adds worldgen reservoirs, prospecting for them, and a depleting resource -
  see [Gotchas](#gotchas) 1. That is a systems bill, not a block.
- Nothing in the spine burns oil or needs lubrication. `grep -rniE "lubric|grease" src/` returns nothing;
  the only mention of lubricating oil anywhere in the docs is the archived product list. No bearing, shaft or
  engine page asks for it. Lubrication is not a modelled concept, so the heavy cut has no consumer even in
  principle.

### But oil is not only domestic - elex wants it

A graphite electrode is petcoke filler + coal-tar pitch binder, baked and graphitised (the Acheson process).
Both ingredients come from Homestead, and from two different branches:

| Electrode ingredient | Source | Homestead branch |
|---|---|---|
| **petcoke** filler | refining residue | oil - this page |
| **coal-tar pitch** binder | coal carbonised in retorts → tar | the [gasworks](gasworks.md) |

([scope.md](../../scope.md) § elex.) Graphite needs both deferred branches, which is why a spine tier
appears in a domestic mod's dependency list at all.

The severity is a degraded path, not a wall. Early Héroult furnaces ran carbon electrodes made from coke and
anthracite; Acheson graphite is 1896 and needle coke far later. The difference is one number - electrode
consumption rate: carbon burns away fast, graphite slowly - which is R5 (gate efficiency, not possibility)
and gives elex a consumable sink either way ([arc furnace](../elex/arc-furnace.md)).

The suite gets no pitch and no petcoke from its own process, by design. The gas carve-out kept producer gas
(lean fuel, no tar) and deferred coal gas (rich feedstock, tar-bearing) - [scope.md](../../scope.md) owns
that criterion. Oil is the mirror image on the residue side: the metalworking line's fuels
([fuels](../../items/fuels.md)) are coke and charcoal, neither of which leaves a still bottom. There is no
in-scope route to petcoke; the carbon electrode is the period-correct answer.

---

## What exists today

No block, no item, no medium, no worldgen.

```
$ grep -rniE "petcoke|kerosene|derrick|naphtha|crudeoil|petroleum" src/ --include=*.cs
(0 results)
```

No lang key, no handbook page, no recipe, no config key. `mods/exlib/assets/exlib/config/liquids.json` declares exactly
four media - Air, Steam, Exhaust, Water - so crude, kerosene and naphtha have no medium and, under R1, could
not ride a pipe without one.

Two doc-comment traces of the model (comments, not code, and neither is content):

| trace | file:line | what it says |
|---|---|---|
| the still generalisation | `mods/exlib/src/Fluids/IMediumTaxonomy.cs:58` | `TryVaporisation` "generalises the boiler's water → steam step to every distillation fraction" |
| the liquid phase's archetypes | `mods/exlib/src/Fluids/LiquidPhase.cs:12` | "Water / Oil / molten-as-liquid - incompressible, mixes only with the same liquid code" |

### What the phase-change carve-out already gives a refinery for free

[scope.md](../../scope.md) keeps the model, and the shipped implementation is medium-driven rather
than steam-specific:

| capability | where |
|---|---|
| per-medium boil point + vaporisation target + volume factor | `mods/exlib/src/Fluids/LiquidDef.cs:36-48` |
| per-medium dew point + condensation target + volume factor | `:26-34` |
| temperature-gated passive phase change both ways | `IMediumTaxonomy.cs:40-64`; `ExLiquids.cs:158-196` |
| catalogue overlaid from any domain's `config/liquids.json` at `AssetsFinalize` | `ExLiquids.cs:8-19` |
| the boiler asking the catalogue instead of hardcoding steam | [Cornish boiler](../../machines/boiler-cornish.md):233-236, `:493-494` |

The archived spec relied on exactly this: all fractions "are `LiquidDef` media riding the same pipes,
condensers, valves and tanks - shipped as the add-on's `config/liquids.json`". That extension point is live
today.

---

## The design as it stands

### The derrick

From the archived iiex spec:

| | |
|---|---|
| Form | multiblock over a worldgen oil reservoir |
| IO | crude-oil seep → crude oil on the liquid network |
| Reservoir | prospectable, finite - does not recharge |
| Lift | depth-gated by the pump tier - reuses the [fluid pump](../../machines/pumps.md) |
| Status | *(planned)*, never started |

Two of those are worth not losing: lift reuses the existing pump ladder, so the derrick is a wellhead rather
than a new power model; and depth is the gate, which is the "efficiency, not possibility" shape R5 asks for.

### The still

The refining machine is the general fractionating still: a multiblock whose height is the number of cuts -
short pot = one or two, tall column = the full light → middle → heavy → residue set - running one recipe per
charge. The model under it is the live carve-out ([conventions.md](../../conventions.md) § Distillation &
phase change); the block is deferred with everything it would process.

The still is charge-driven: it takes coal tar (gasworks) or crude (oil). Its existence therefore depends on
at least one of the two deferred branches, and neither on its own.

### The products

From the archived iiex spec:

| Product | Intended consumer | Status of that consumer |
|---|---|---|
| **kerosene** | kerosene lanterns (items) | Homestead's, deferred with it |
| **naphtha** | reagent for the dye chain | Homestead's |
| **lubricating oil** | - | none anywhere - lubrication is not modelled (grep above) |
| **petcoke** (residue) | graphite electrodes | elex's, deferred but on the spine |

---

## What it would unblock

| Waiting on oil | Severity |
|---|---|
| elex's graphite electrodes | degraded path, not a wall. Carbon electrodes from coke/charcoal are in scope and period-correct; the gap is one number, electrode consumption rate ([scope.md](../../scope.md)) |
| The fractionating still having a charge at all | half a wall - the still needs crude or coal tar; oil is one of the only two sources, the [gasworks](gasworks.md) is the other |
| Homestead's kerosene lantern line | wall, but entirely inside Homestead |
| Lubricating oil | nothing - no consumer exists in any doc or any file |

[scope.md](../../scope.md) records that elex could ship a real subset with no chemistry at all - arc furnace
+ HSS on carbon electrodes, DC only, impure copper wire, no alternators. Oil is not in that blocking set:
its absence costs elex efficiency, where the acid's absence costs elex a whole product.

---

## Gotchas

1. "Finite, does not recharge" is the only depleting resource in the entire suite. Everything else - ore,
   stone, coal - is worldgen-abundant and effectively inexhaustible in play. A reservoir with a
   remaining-volume state that a player can exhaust needs prospecting UI, a persisted per-deposit quantity,
   and an answer to "what happens to the derrick and everything downstream of it when it runs dry". That is
   a system, not a block, and it is the single largest hidden cost on this page.

2. R1 makes a refinery *n* pipe runs, one per fraction. A run carries one medium at a time
   ([conventions.md](../../conventions.md)), and a liquid "mixes only with the same liquid code"
   (`mods/exlib/src/Fluids/LiquidPhase.cs:12`). So a still's take-offs cannot share a run: crude in,
   naphtha out, kerosene out and heavy out are four separate networks that must not touch, each with its own
   condenser bridging it - the steam condenser's connector-not-node shape
   (`mods/iiex/src/BlockNetworkPipe/BlockEntities/BlockEntitySteamCondenser.cs:16-21`, discussed at
   [fluid tank](../../machines/fluid-tank.md):104-108) is the only pattern that allows it. This is the most
   expensive structural consequence of R1 for any deferred chemistry, and it is recorded nowhere else.

3. petcoke is not coke - do not register it as fuel. `mods/iiex/assets/iiex/config/materialroles.json:4` binds
   the `fuel` role to `game:coke` with a carbon value of 2 (charcoal 1 at `:5`), and the furnace burns
   carbon computed from role membership ([burden](../../items/burden.md), [fuels](../../items/fuels.md)). A
   petcoke item quietly added to that role would change every charge's fuel arithmetic. If petcoke ever
   exists it is an electrode feedstock only.

4. The still's variable height is not a new capability. Height-by-build is the suite's settled
   multiply-don't-enlarge idiom (smokestack courses, cupola shafts). The still would be another instance,
   not a novel structure.

5. Oil's fractions want their own `liquids.json`, not exlib's. The loader overlays per-domain catalogues
   over the compiled baseline (`ExLiquids.cs:8-19`), and the archived spec already assumed the add-on ships
   its own. Adding crude to `mods/exlib/assets/exlib/config/liquids.json` would put a deferred medium in the library
   every mod loads.

6. [conventions.md](../../conventions.md) § Networks still lists "the chemistry fractions" among pipe
   media. True as a statement of what the network can carry, misleading as scope - already logged at
   [scope.md](../../scope.md). Read those lines as capability, not as a plan.

---

## Open

- Whether oil belongs in Homestead at all. It is an extraction industry with a worldgen and depletion bill
  of its own, sitting inside a mod otherwise about gas, chemistry and the home. Splitting it out would also
  make elex's dependency a single named mod rather than "half of Homestead".
- Whether elex may ship graphite ahead of oil. The answer implied throughout is no - both electrode
  ingredients are Homestead's ([scope.md](../../scope.md)) - but nobody has written the ruling down, and the
  carbon fallback makes it a live temptation to fudge.
- Lubricating oil's consumer. If lubrication is ever modelled - wear on bearings, shafting efficiency - the
  heavy cut acquires its first real customer and this page's severity table changes. Nothing proposes it
  today.
