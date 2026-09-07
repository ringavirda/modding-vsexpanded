# Gasworks (retort house, hydraulic main, condensers, purifier)

**Status** deferred - nothing exists in code
**Would live in** the planned Industrial Homestead mod (its anchor machine: the ammonia that
links refrigeration to gasmaking is internal to it)
**Deferred by** the metalworking-only cut, recorded in [scope.md](../../scope.md) (the row, and the
criterion that separates it from the gas producer). Do not re-argue either here.

**Owns**

* what carbonisation yields, and therefore why this machine cannot be trimmed into scope;
* the tar chain downstream - who is waiting on coal tar, how badly, and where the other half of a
  graphite electrode comes from;
* the archived gasworks design (retort house + water-sealed gas main) restated in one place;
* the dependency knots a gasworks hits on the shipped pipe network: R1 against a three-output machine,
  the silent gas-merge, and the fact that no gas medium exists to declare it with;
* the three things that already exist and would not need building.

**Does not own** — cited only, never restated:

| Fact | Owner |
|---|---|
| The metalworking-only cut, its carve-outs, the release target | [scope.md](../../scope.md) |
| The gas producer as a machine - its inputs, numbers, medium requirement, the "no gasholder" ruling, and the producer-gas ↔ coal-gas comparison table | [gas-producer](../../machines/gas-producer.md) |
| Why the beehive oven recovers nothing | [coke oven](../../machines/coke-oven.md):33-38 |
| One medium per run, capacity, pressure, leaks, merge/split, bursts | [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) |
| The phase-change / distillation model | [conventions.md](../../conventions.md) § Distillation & phase change |
| The gasholder-as-storage-node problem, and why it cannot be built today | [fluid tank](../../machines/fluid-tank.md) |
| Petcoke, the other graphite ingredient | [oil](oil.md) |
| The acids and the sulfur question | [chemistry](chemistry.md) |
| Gas lamps, the gasworks' domestic consumer | [gas lighting](gas-lighting.md) |
| R1 single medium · R2 declared recovery · R5 gate efficiency · R7 nothing hidden | [conventions.md](../../conventions.md) |

**Depends on** [scope.md](../../scope.md) · [gas-producer](../../machines/gas-producer.md) ·
[coke oven](../../machines/coke-oven.md) · [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) ·
[fluid tank](../../machines/fluid-tank.md) · [chemistry](chemistry.md) · [oil](oil.md) ·
[gas lighting](gas-lighting.md) · the archived iiex spec (git history) · [arc furnace](../elex/arc-furnace.md)

---

## What it is

A gasworks carbonises coal: coal is sealed in fireclay or cast-iron retorts with no air and heated
from outside by a coke-fired flue, so the coal cannot burn - it decomposes. One charge leaves four products,
and the plant is built to collect all of them:

| Product | Where it is caught | What it was for |
|---|---|---|
| town gas (coal gas) | off the retort mouths into the hydraulic main, then condensers → scrubber → purifier → holder | lighting, later cooking and heating |
| coal tar | drops out in the water-sealed hydraulic main and the condensers | roofing, creosote, dyes, and - calcined - pitch |
| ammoniacal liquor | same place, the aqueous layer under the tar | ammonia salts, fertiliser, refrigerant |
| coke | raked out of the retort at the end of the charge | fuel - including the plant's own flue |

Historically: Murdoch's mill lighting (1792) → the London Gas Light & Coke Company (1812) → municipal
works everywhere by mid-century. The **hydraulic main** is the characteristic piece of equipment: the
retort offtakes dip into a trough of water so every retort is gas-sealed from every other, and the tar
and liquor knocked out of the hot gas collect in it.

## Why it is deferred

The decision and its reasoning live in [scope.md](../../scope.md) - link, do not re-argue.
The consequence for this machine specifically:

> A gasworks cannot be trimmed to fit the metalworking line, because the trimmed version is a machine
> the suite already has. Strip the tar, the liquor and the ammonia - the chemistry - and what remains is
> "blow heat through coal, get a fuel gas and a solid residue". That is the
> [gas producer](../../machines/gas-producer.md) if you keep the gas, and the
> [beehive coke oven](../../machines/coke-oven.md) if you keep the solid. Both are in scope, both are
> designed, and between them they cover every metallurgical use a works has for gasmaking.

The three machines are not three tiers of one thing. They are three policies about the volatiles:

| Machine | What happens to the volatiles | In scope? | Owner |
|---|---|---|---|
| Beehive coke oven | burned inside the oven for its own heat - recovered as nothing | yes (iiex) | [coke oven](../../machines/coke-oven.md):33-38 |
| Gas producer | never made - the bed is already coke, so there is nothing left to drive off | yes (smex) | [gas-producer](../../machines/gas-producer.md) |
| Gasworks | collected and sold | deferred | *this page* |

The two in-scope machines are the two that throw the chemistry away. [scope.md](../../scope.md) records
that the two decisions are really one decision.

## What exists today

In `src/`, a repo-wide grep for `coalgas|coal.?gas|gasworks|gasholder|retort|distill|coaltar|petcoke|graphite|electrolys|ammonia|benzene|kerosene|aniline` (`--include=*.cs`) returns one hit, and it is a doc comment:

```
mods/exlib/src/Fluids/IMediumTaxonomy.cs:58:  /// every distillation fraction. On <c>true</c> ...
```

Assets and lang are empty too - `grep -rniE "coal gas|gasworks|gasholder|retort|gas lamp|coal tar" assets/`
returns nothing, in any of the three languages.

Three relevant things do exist, and a gasworks would not have to build them:

| Exists | Where | Why it matters |
|---|---|---|
| The still's engine, as an interface | `IMediumTaxonomy.TryVaporisation` (`mods/exlib/src/Fluids/IMediumTaxonomy.cs:54-66`, declared at `:61`) - its summary says it "Generalises the boiler's water → steam step to every distillation fraction" | the fraction-by-boiling-point mechanic is already an API with one implementation (`ExLiquids.cs:181`) |
| A medium-agnostic condenser, live | `BlockEntitySteamCondenser.HasCondensableGas` reads the taxonomy rather than hard-coding steam (`mods/iiex/src/BlockNetworkPipe/BlockEntities/BlockEntitySteamCondenser.cs:236-255`); the summary names "any future condensable vapour a still routes through a condenser" | the condenser bank downstream of a hydraulic main is already a shipped block |
| A vanilla tar item with no producer | `.game/1.20/assets/survival/itemtypes/liquid/tar.json` - `tarportion`, an `ItemLiquidPortion`, lang `"item-tarportion": "Tar"` (`.game/1.20/assets/game/lang/en.json:4147`) | `grep -rln "tarportion" .game/1.20/assets/` matches only that itemtype and the lang files - no recipe, no block, no drop, in 1.20 or 1.22. Vanilla defines tar and never makes any |

The tar a gasworks would produce already has a vanilla item code, a texture and a translated name in
fourteen languages, and nothing in the game produces it.

What does not exist is the medium. `mods/exlib/assets/exlib/config/liquids.json` declares exactly four - `Air`,
`Steam`, `Exhaust`, `Water` - matching `ExLiquids.SeedDefaults()` (`mods/exlib/src/Fluids/ExLiquids.cs:44-69`).
There is no `CoalGas`, no `CoalTar`, no `AmmoniacalLiquor`, and R1 gives a run exactly one medium.

## The design as it stands

The archived iiex specification (git history), restated here in substance:

| Field | Archived spec |
|---|---|
| Block type | multiblock, with the retort house and the gas main as its parts |
| IO | coal (no air) → coal gas + coal tar + coke, plus ammonia and sulfur from the ammoniacal liquor |
| Mechanic | destructive distillation, gas-primary; retorts over a coke firebox; a water-sealed gas main dropping tar and liquor; the stream split is the tunable |
| Coupling | the ammonia by-product charges Homestead's own absorption refrigeration loop ([climate-control](climate-control.md)) |

Two design notes travel with it:

* "Stream split is the tunable" is the R5 handle ([conventions.md](../../conventions.md)). A hotter,
  longer carbonisation drives off more gas and leaves less tar; the player picks the ratio. That one
  number is the whole operating verb.
* The still is a separate block: height = number of cuts, one still runs any recipe by its charge. The
  gasworks makes tar; the still splits it. Do not merge them.

### The knots — what a gasworks runs into on the shipped network

| # | Knot | Where | Why it bites |
|---|---|---|---|
| 1 | R1 vs. a three-output machine | [conventions.md](../../conventions.md); owned by [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) | a run carries one medium. A gasworks emits gas, tar and liquor simultaneously. It therefore needs three separate ports onto three separate runs - or the liquids come off as items and sidestep the network entirely. The archived spec implies the first ("a gas main dropping tar/liquor"), but never says which |
| 2 | Two gases always mix, silently | `ExLiquids.cs:106-115`, comment at `:114` - "two gases always mix (Air/Steam/Exhaust family)"; the merged run takes the higher-priority label (`:118-119`) | join a coal-gas main to a steam or air main and you get one pool, relabelled, with no warning and no refusal. [gas-producer](../../machines/gas-producer.md):309-315 already logs this for producer gas - a town-gas main is worse, because lighting mains are distributed around a settlement and will pass near the works' own air and exhaust runs |
| 3 | No media to declare | `mods/exlib/assets/exlib/config/liquids.json`; loader at `ExLiquids.Load` (`ExLiquids.cs:74-97`) | Homestead ships them as its own `config/liquids.json` and the loader overlays it with no code, so this is cheap. But the priorities have to be chosen against the shipped ladder (Air 0 `ExLiquids.cs:46`, Steam 10 `:52`, Exhaust 20 `:58`) and against `ProducerGas`, which [gas-producer](../../machines/gas-producer.md):240 proposes at 30 |
| 4 | The gasholder is the fluid tank | the archived spec calls it "the core-iiex medium-agnostic storage node holding coal gas (telescoping bell cosmetic)" | so it is one block with two skins, and its blocking problem - capacity is per-node and uniform - is already written up at [fluid tank](../../machines/fluid-tank.md). Do not design a second storage node. `fluid-tank.md:56-58` cites the archived gasholder as one of two reasons the tank must stay medium-agnostic |

## The tar chain — who is downstream, and how badly

This is the part of the gasworks that reaches outside Homestead. Everything else the plant makes is
consumed inside the same mod.

| Product | Goes to | Who is waiting | Severity |
|---|---|---|---|
| coal tar → distilled → pitch | the binder half of an Acheson graphite electrode | elex's [arc furnace](../elex/arc-furnace.md) | degraded path, not a wall - carbon electrodes from coke/charcoal are the in-scope, period-correct fallback; see [scope.md](../../scope.md) |
| petcoke (not this machine) → the filler half | same electrode | same | owned by [oil](oil.md) - a graphite electrode needs both, so the gasworks alone does not deliver one |
| ammoniacal liquor → ammonia | Homestead's absorption refrigeration charge | internal ([climate-control](climate-control.md)) | internal |
| ammoniacal liquor → sulfur (the archived claim) | smex's copper add-on | see [chemistry](chemistry.md) | vanilla already ships sulfur, so this row may be moot |
| town gas | gas lamps, and Homestead's cooking/heating | internal | see [gas lighting](gas-lighting.md) |
| coke | the iron tier | nobody | iiex's [beehive oven](../../machines/coke-oven.md) already makes coke, in bulk, from vanilla brick |

The gasworks' largest product by mass is coke, which the metalworking line already has a dedicated machine
for; its smallest product by mass, tar, is the only one anything in this suite ever wanted, and that
consumer has a working fallback.

## What it would unblock

| Consumer | What it gets | Wall or degraded path? |
|---|---|---|
| elex arc furnace | graphite electrodes → lower electrode consumption rate | degraded path. The mechanic is one number, and carbon electrodes are what a 19th-century furnace actually ran on - [scope.md](../../scope.md). Graphite is a later-era luxury, not an unlock |
| Homestead - lighting, dyes, kerosene wash, refrigeration | its own fuel gas and its own reagent stream | a wall, and a total one. The ammonia that charges the refrigeration loop is a gasworks by-product, so there is no Homestead without this machine |
| smex copper add-on | sulfur | probably nothing - see [chemistry](chemistry.md) |
| The ferrous line (`exlib → iiex → iiex → smex → hpex`) | nothing at all | the release target is defined in [scope.md](../../scope.md) and this machine is not on it |

## Gotchas

* "Just gas lighting" cannot ship without the gasworks. The archived design merged the old Heating and
  "Lights, Fuel & Colors" add-ons into one because the ammonia link makes refrigeration internal to
  gasmaking. Pull any consumer forward and the retort house comes with it.
* Do not "solve" the tar problem by making iiex's coke oven a by-product oven. A beehive oven burned its
  volatiles, which is why it was the cheap oven ([coke oven](../../machines/coke-oven.md):33-38, which
  states the oven yields coke only - no tar, no gas, no ammonia). [scope.md](../../scope.md) records that
  this and the producer-gas carve-out are the same decision; reversing one reverses both.
* [conventions.md](../../conventions.md) § Networks still lists "coal gas" among the pipe media. True as
  a statement of what the network can carry, misleading as a statement of scope. Already logged at
  [scope.md](../../scope.md); noted here because a reader arriving from the network docs hits it
  before the cut.
* Town gas is carbon monoxide. The archived spec gives ammonia a toxicity hazard but says nothing about
  the gas itself. If Homestead builds a danger layer, this is its most obvious customer - and it
  interacts badly with knot #2, because a silent merge would move a lethal gas onto a run the player
  thinks is air.
* The condenser is already medium-agnostic on purpose. `BlockEntitySteamCondenser.cs:236-241` names the
  still explicitly as the reason. Do not re-specialise the condenser to steam in a cleanup pass.

## Open

| # | Question | Notes |
|---|---|---|
| 1 | Items or media for tar and liquor? | The cheapest answer sidesteps knot #1 entirely: gas on the pipe network, tar and liquor as `ItemLiquidPortion` in buckets - and vanilla's `tarportion` is already exactly that class |
| 2 | What priority does coal gas take? | Against Air 0 / Steam 10 / Exhaust 20 (`ExLiquids.cs:44-69`) and the proposed ProducerGas 30 ([gas-producer](../../machines/gas-producer.md):240). Whatever wins the merge should be the gas whose label being wrong is most dangerous |
| 3 | Is the retort house one multiblock or a bank? | The archived spec says one multiblock with the gas main as a part. The suite's standing preference is multiply-don't-enlarge ([crucible furnace](../../machines/crucible-furnace.md):40, [cupola](../../machines/cupola.md):52), and a retort house is a bank of retorts sharing a flue |
| 4 | Does the stream split stay the tunable? | It is the only operating verb the archived design gives this machine. If it goes, the gasworks is a recipe with a chimney |
| 5 | Does Industrial Homestead get a doc? | several pages already point into a mod with no file; this tree is the closest thing it has |
| 6 | Whether the sulfur claim was ever true | See [chemistry](chemistry.md): vanilla ships sulfur as an ore and a powder, so a gasworks that "produces sulfur" may be solving a problem that does not exist |
