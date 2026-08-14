# Arc furnace
**Status** deferred   **Would live in** `elex` (Electrical Expanded - a mod with no project, no asset domain
and no code)   **Deferred by** D8 ([STATE.md](../../../internal/plans/STATE.md)) - the release target is the ferrous line,
so elex ships after it; the cut itself is [scope](../../scope.md)'s

**Owns**
* the electrode consumption-rate mechanic - the single number that separates the in-scope carbon electrode from the deferred graphite one, and the wear/fitting precedent to model it on;
* the claim that this machine is a degraded path, not a wall, in mechanical terms;
* the HSS chain end to end - the arc furnace is HSS's only route, what feeds it, what waits on it, and the two metal identities that do not exist yet;
* the evidence that nothing is built for it.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| the cut, the carve-outs, the three chemistry severities, the release target | [scope](../../scope.md) |
| the grid model and the AC supply it runs on | [electrical grid](electrical-grid.md) · [alternator](alternator.md) |
| what HSS is - composition, properties, the alloy windows | [materials.md](../../materials.md) · [alloys](../../items/alloys.md):108 |
| why the mod has no tar, and that the chain starts at the coke oven | [coking](../../processes/coking.md):211-232 |
| `T_process = T_in − T_loss` and every term in it | [heat balance](../../mechanics/heat-balance.md) |
| R2 (declared recovery), R3 (ladle is the only merge), R5 (gate efficiency), R7 (nothing hidden) | [conventions.md](../../conventions.md) |
| the ~36 kW tandem Corliss that drives its generators | the archived hpex spec (git history) |
| the gasworks that would make its graphite binder | [gasworks](../homestead/gasworks.md) |

**Depends on** [scope](../../scope.md) · [electrical grid](electrical-grid.md) · [alternator](alternator.md) (three of them, and they are the real gate) · [electrolysis cell](electrolysis-cell.md) (its power supply is gated there) · [wire extruder](wire-extruder.md) · [open hearth](../../machines/open-hearth.md) (low-N feedstock) · [ladle](../../machines/ladle.md) (W + Cr go in there, R3) · the archived hpex spec (drive + the HP crusher that cracks chromite/wolframite) · [heat balance](../../mechanics/heat-balance.md) · [alloys](../../items/alloys.md)

---

## What it is

A **Héroult three-phase electric arc furnace** (~1900): three carbon electrodes, one per phase, struck down onto a charge in a refractory bath; the arc, not a fuel bed, supplies the heat. Its sibling, the **Stassano** furnace (~1898), smelted iron ore to steel electrically.

It is the one melter that bypasses the fuel/air-blast chain entirely - no coke, no cowpers, no blower, no burden - and pays for that in electricity.

---

## Why it is deferred

D8 ([STATE.md](../../../internal/plans/STATE.md)): the release target is the complete ferrous line, and elex is not on it. The reasoning belongs to [scope](../../scope.md) § The release target.

The electrode dependency is not the reason. [scope](../../scope.md) § elex rates electrodes a degraded path, not a wall, because carbon electrodes are in scope and period-correct. The furnace is deferred by release scheduling, not by chemistry.

---

## What exists today

Nothing. Not a block, not an item, not a lang key, not a shape, not a metal registration.

| Probe | Command | Result |
|---|---|---|
| the mod | `ls src/` | `ExpandedLib` `ExpandedLib.Generators` `HighPressureExpanded` `IronIndustryExpanded` `IronIndustryExpanded` `SteelmakingExpanded` - no `ElectricalExpanded` |
| its asset domain | `ls assets/` | `editable` `exlib` `game` `hpex` `iiex` `iiex` `smex` - no `elex` |
| the name | `grep -rni "elex\|ElectricalExpanded" src/` | 0 hits |
| the machine and its grid hardware | `grep -rniE "arcfurnace\|arc-furnace\|electrode\|dynamo\|alternator\|rectifier\|synchroniser" src/ assets/` | 0 hits |
| its product | `grep -rniE "electrolys\|hss\|highspeedsteel" src/ assets/` | 2 hits, both binary substring noise in art files (`assets/editable/refs/rolling/rolling-mill-for-puddling-ledebur-W31WYE.jpg`, `assets/editable/textures/vs_textures.psd`) - 0 in code |
| the deferral generally | `grep -rniE "coalgas\|sprinkler\|gasholder\|distill\|retort\|petcoke\|graphite\|electrolys" src/` | 1 hit - a doc comment at `src/ExpandedLib/Fluids/IMediumTaxonomy.cs:58` |

The frameworks it would sit on are live and shipped: the heat-balance law (`ExpandedLib/Heat/HeatBalance.cs:30`, `:55`), the molten network, the multiblock layout DSL, and the metal registry (`ExpandedLib/Metals/MetalCatalogueLoader.cs:58`).

---

## The design as it stands

### The machine

The archived elex spec holds the footprint, the ~86 kW three-phase supply (three engines + three alternators + a synchroniser), and the four melt roles with their tunable rates. Two properties matter to this page:

| Property | Value |
|---|---|
| Power | three-phase AC directly, one electrode per phase, no rectifier |
| Temperature | shared dynamic heat balance - `T_arc = f(delivered electrical power) − charge_sink` |
| Consequence | voltage sag or frequency droop → less power → cooler arc → it will not melt |
| Yield | mass-conserving, no bonus (R2) |

### The electrode mechanic — one number, two tiers

Three electrodes deplete with arc-hours and are replaced in-world (sneak + RMB), as a wear consumable. The tiering:

| Electrode | Made from | Where that comes from | Consumption | Conductivity |
|---|---|---|---|---|
| Carbon | coke / charcoal | in scope - the beehive oven's own coke | fast | lower |
| Graphite (Acheson, 1896) | calcined petcoke filler + coal-tar pitch binder | Industrial Homestead: oil → petcoke, [gasworks](../homestead/gasworks.md) → pitch | slow | higher |

The two differ in exactly one config number, the consumption rate. That is R5 (gate efficiency, not possibility, [conventions.md](../../conventions.md)), so the graphite tier is an upgrade and not a gate: with Homestead absent the furnace still runs, it just eats electrodes. The mechanic has no second axis - melt rate, roles and temperature are never gated on electrode grade.

The carbon electrode is the historical default, not a fallback: Acheson graphite postdates the furnace.

### How to implement the wear, when the time comes

Three shipped idioms cover it:

| Need | Precedent to copy | Where |
|---|---|---|
| a wear-consumable tooling item with a durability count | `WoodenPatternDurability = 24` | `src/IronIndustryExpanded/BlockStructures/Casting/PatternItemDefinitions.cs:109` |
| fit / swap tooling into a running machine, refusing mid-job | `TryFitRollSet` | `src/IronIndustryExpanded/BlockStructures/Forming/BlockEntities/BlockEntityRollingMill.cs:143` |
| tooling that carries its own spec instead of the machine naming products | `RollSetSpec` / `MoldSpec` | `src/IronIndustryExpanded/BlockStructures/Forming/RollSetSpec.cs:9-24` |
| return the fitted tooling on break rather than destroying it | `BlockEntityRollingMill.OnBlockBroken` | same file, `:374-382` (cited by [boring machine](../../machines/boring-machine.md):219) |

Caution: this inherits a load-time hole. `RollSetValidation` only checks that a spec parses (`RollSetValidation.cs:22-33`), never that an output code resolves. An electrode spec added the same way fails silently in the same way. See [wire extruder](wire-extruder.md) § What exists today for the live case.

### HSS — the product, and the only route to it

```
open hearth (low-N steel)  ──scrap/ingot──▶  ARC FURNACE (melt)  ──▶  molten low-N base
hpex HP crusher  ──chromite + wolframite──▶  W + Cr  ──ladle (R3)──▶  HSS
```

| Link | Number / rule | Source |
|---|---|---|
| feedstock must be open-hearth steel | Bessemer steel is too high in nitrogen | [materials.md](../../materials.md) |
| alloying happens in the ladle, not in the furnace | R3 | [conventions.md](../../conventions.md) |
| charge | ~624 u OH-steel base + ~144 u tungsten + ~32 u chromium | [materials.md](../../materials.md) |
| composition | ~77.25 % Fe, ~0.75 % C, ~18 % W, ~4 % Cr (no vanadium) | [materials.md](../../materials.md) |
| what it sells | hot-hard; needs no tempering - the top of the tool ladder | [alloys](../../items/alloys.md):108 |
| W and Cr are gated on hpex | only HP (hadfield) jaws crack chromite/wolframite | the archived hpex spec |

HSS needs two metal identities, and one of them is missing from vanilla. The vanilla metal worldproperty lists 23 codes including `chromium` but no `manganese` and no `tungsten` ([alloys](../../items/alloys.md):151-153, verified against `<VS>/assets/survival/worldproperties/block/metal.json`). The alloy needs its own identity and a tungsten one, plus a tungsten reduction story - the mechanism is an overlay through `MetalCatalogueLoader.cs:58`, the reduction story is unwritten.

Both ores are vanilla: `wolframite` appears in vanilla `blocktypes/stone/ore-ungraded.json` and `looseores.json`, and chromite is verified vanilla down to `game:crushed-chromite` ([bearings](../../machines/bearings.md):225-228). The arc furnace's alloying elements are not blocked on worldgen.

---

## What it would unblock

| Waiting on it | Severity | Why |
|---|---|---|
| HSS as a material | wall - it is the only route | no other machine melts it; [alloys](../../items/alloys.md):134 calls open-hearth-exclusive feedstock "the one exception" and this the only route |
| Boring-machine bit tier 3 | degraded | the ladder is cast iron → quench-hardened steel → HSS ([boring machine](../../machines/boring-machine.md):194); tiers 1-2 exist in design, so the machine works without tier 3 - it just never reaches its ceiling |
| Hardened die sets / shear blades | degraded | quench-hardened dies already cover stamping hadfield and HSS ([steam hammer](../../machines/steam-hammer.md):234, :345) |
| The coke-free recycle loop | degraded | scrap remelt at ~30 u/s is an alternative to the open hearth's scrap route, not a replacement |
| Direct iron smelting without a blast furnace | none | an optional route; it replaces smelting only, never carburising or alloying |
| Copper smelting (reverberatory alternative) | — | doubly deferred: the role's whole downstream is non-ferrous ([scope](../../scope.md)) |
| hpex's HP crusher having a second consumer | minor | without HSS the crusher's chromite output feeds only ferrochrome ([bearings](../../machines/bearings.md):232) |

Nothing in the shipped ferrous line is walled by this machine. It is the ceiling of the tool-material ladder; crucible steel (D9, [STATE.md](../../../internal/plans/STATE.md)) was adopted so the suite had a gear reward that did not wait on elex.

---

## Gotchas

* The no-tar chain is not reversible by reopening the coke-oven decision. Both graphite ingredients come from Homestead - oil for petcoke, gasworks for pitch - so adding by-product recovery to the beehive oven does not produce a graphite electrode. [coking](../../processes/coking.md):216-232 draws the chain.
* The ladle merges nothing today ([ladle](../../machines/ladle.md):13). HSS is a ladle alloy under R3, so the arc furnace inherits whatever state that mechanic is in; it is the same idle-rule problem the tilting crucible has from the non-ferrous side ([scope](../../scope.md)).
* A latent bootstrap loop sits in the bearing chain ([STATE.md](../../../internal/plans/STATE.md)) - "only HP jaws crack chromite" would make an hpex machine the sole source of a material hpex needs. It is safe today only because chromite is vanilla. HSS puts a second consumer on that same crusher, so if the gate is ever enforced literally, this furnace tightens the loop rather than relieving it.
* Its power supply is gated by a different deferred page. Three-phase means three [alternators](alternator.md); alternators need pure copper windings; pure copper needs the [electrolysis cell](electrolysis-cell.md). This machine is not the one to bring forward first - the DC subset ([scope](../../scope.md)) reaches HSS only if the arc furnace can be made to accept a DC or single-phase supply, which nothing has decided.
* The copper-smelter role has a second owner. The arc route replaces the reverberatory only, leaving the Pierce-Smith → blister → electrolysis chain intact - see [copper reverberatory](../non-ferrous/copper-reverberatory.md) before assuming the electric route shortens anything.

---

## Open

| # | Question | Notes |
|---|---|---|
| 1 | The consumption numbers. Arc-hours per electrode, and the carbon : graphite ratio | the entire mechanic is this one pair of numbers; nothing on paper fixes them |
| 2 | Is an electrode durability-consumed tooling, or a stack that is eaten? | the same undecided question as boring-machine bits and shear blade sets ([boring machine](../../machines/boring-machine.md):273, which says those two "should be decided together") - make it three |
| 3 | Where does HSS's metal identity live, and how is tungsten reduced? | neither metal is vanilla ([alloys](../../items/alloys.md):151-153); an overlay through `MetalCatalogueLoader.cs:58` is the mechanism, the reduction story is not written |
| 4 | Can the furnace run single-phase / DC at reduced rate? | decides whether the no-chemistry subset ([scope](../../scope.md)) actually reaches HSS, or stops one machine short |
| 5 | Does the copper-smelter role survive D8 at all? | it is deferred twice over; when non-ferrous returns, decide whether the arc route is even wanted alongside the reverberatory |
