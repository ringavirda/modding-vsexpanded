# Benchtop chemistry & the acids

**Status** deferred - nothing exists in `src/`
**Would live in** the planned Industrial Homestead mod (the benchtop / vat station, plus the
fractionating still that feeds it)
**Deferred by** the metalworking-only cut, recorded in [scope.md](../../scope.md). Do not re-argue it here.

**Owns**

* the acid question: vanilla Vintage Story already ships sulfuric acid, with a recipe, and already ships
  sulfur - so two of elex's three chemistry dependencies cost nothing;
* the benchtop design - one station, five recipe families, keyed by charge;
* the distinction that makes the deferral cheap: an electrolyte is not a reagent, so elex's acid need is a
  fill-and-top-up, not a supply chain;
* the remaining cost of chemistry in this suite, which is plumbing, not chemistry.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| The cut itself, the carve-outs, the release target, and the three elex dependencies *as a severity table* | [scope.md](../../scope.md) |
| The phase-change / distillation **model** (the still's mechanism) | [conventions.md](../../conventions.md) § Distillation & phase change |
| The **gasworks** and everything downstream of tar | [gasworks](gasworks.md) |
| Crude oil, refining, petcoke | [oil](oil.md) |
| elex's grid, arc furnace, electrolysis cell and electrode mechanic | [electrical grid](../elex/electrical-grid.md) · [arc furnace](../elex/arc-furnace.md) · [electrolysis cell](../elex/electrolysis-cell.md) |
| The copper add-on (reverberatory, Pierce-Smith, zinc retorts) | [copper reverberatory](../non-ferrous/copper-reverberatory.md) · [Pierce-Smith](../non-ferrous/pierce-smith.md) |
| One medium per run, capacity, pressure | [pipe network](../../mechanics/pipe-network.md) |
| R1 · R2 · R5 · R7 | [conventions.md](../../conventions.md) |

**Depends on** [scope.md](../../scope.md) · [gasworks](gasworks.md) · [oil](oil.md) ·
[pipe network](../../mechanics/pipe-network.md) · [conventions.md](../../conventions.md) ·
the archived lpex spec (git history) · [electrolysis cell](../elex/electrolysis-cell.md)

---

## What it is

The **benchtop / vat station**: one small block that runs a family of reactions by its charge, the way one
still runs any distillation by what is put in it. In the archived lpex spec its recipe families are
**nitration, reduction, dyeing, kerosene acid-wash and acid-making**.

Its period anchor for acid is the **lead chamber process** (Roebuck, 1746): burn sulfur with saltpetre, catch
the fumes in a lead-lined chamber over water, get sulfuric acid. Small-scale practice before that used glass
bell vessels, which matters here because lead is non-ferrous and separately deferred
([scope.md](../../scope.md)). Nitric follows from saltpetre + sulfuric; the aniline dye chain (Perkin, 1856)
follows from coal tar and nitration, which is why chemistry and the [gasworks](gasworks.md) are one add-on
and not two.

## Why it is deferred

A chemical-feedstock industry has no metalworking consumer; the rule and the row are
[scope.md](../../scope.md)'s.

The suite's remaining chemistry needs are all consumption needs - elex's electrolyte, the copper add-on's
sulfur - and the archived design is a production industry. Nothing in the ferrous line needs to make acid;
it needs to have some.

## What exists today

### In this repo: nothing, and the one near-miss is a false friend

```
$ grep -rniE "acid|sulfur|sulphur|saltpet" src/ --include=*.cs
src/SteelmakingExpanded/BlockStructures/Converter/BlockEntities/BlockEntityConverterControl.cs:34:
  /// The blow is an <b>acid</b> Bessemer (materials.md high-N mild steel) ...
src/SteelmakingExpanded/SmexConfig.cs:156:  // ... Acid process: no flux, self-forming siliceous slag ...
src/SteelmakingExpanded/SmexConfig.cs:201:  // ... (the real acid-Bessemer figure; ~30 % is too generous)
```

Every hit is the acid Bessemer - a refractory-lining term (siliceous vs. basic), not a reagent. There is no
acid item, no acid medium, no chemistry block, and no lang key anywhere in `assets/`.

### In vanilla: sulfuric acid ships, and so does sulfur

| Thing | Where | Detail |
|---|---|---|
| **Sulfuric acid, as an item** | `.game/1.20/assets/survival/itemtypes/liquid/acid.json:2` | `acid-full`, class `ItemLiquidPortion`, `itemsPerLitre: 100` (`:18`) |
| **…with a working recipe** | `.game/1.20/assets/survival/recipes/cooking/acid.json` | 1 L `waterportion` + 1 `saltpeter` + 2 `powder-sulfur` → 100 portions of `acid-full-sulfuric` (`:13-38`), cooked in a cooking pot |
| **…documented in the in-game handbook** | `.game/1.20/assets/game/lang/en.json:12872-12875` | *"Sulfuric acid is produced by cooking (in a cooking pot) two portions of powdered sulfur with one portion of saltpeter, and one portion of water."* |
| **Sulfur** | `.game/1.20/assets/survival/itemtypes/resource/crushed/powder.json:4` | `powder-sulfur` is one of eight vanilla powders; sulfur ore exists too (`.game/1.20/assets/survival/shapes/item/ore/ungraded/sulfur.json`) |
| **Saltpetre** | `.game/1.20/assets/survival/itemtypes/resource/crushed/saltpeter.json`, ore at `blocktypes/stone/saltpeter.json` | vanilla, mineable |
| **A downstream vanilla consumer already** | `.game/1.20/assets/survival/recipes/cooking/sulfate.json` | 2 L sulfuric + 2 `crushed-chromite` → `sulfate-full-chromite`, used by `recipes/cooking/hide-chromium.json` |
| **Present in every supported version** | `.game/1.22/assets/survival/recipes/cooking/acid.json` | 1.20 and 1.22 both |

One limit: `allowedVariants: ["acid-full-sulfuric"]` (`acid.json:8-9`). The type variantgroup declares
`sulfuric`, `nitric`, `hydrochloric` (`:6`) but only sulfuric is registered. Nitric and hydrochloric are
defined-but-disabled - anything that needs those is still Homestead's problem.

## The design as it stands

From the archived lpex spec (git history):

| Piece | Archived spec | Note |
|---|---|---|
| **Chemistry benchtop / vat** | a shared small block / megablock - explicitly not a full multiblock - running reagents → product by recipe | the cheapest block shape in the vocabulary. Chemistry was never meant to be a build project |
| **Distillation still** | its own multiblock, height = number of cuts | the model is in scope and live ([conventions.md](../../conventions.md) § Distillation & phase change); only its chemical charges are deferred |
| **Products** | sulfuric + nitric acids and sulfur, consumed by the copper add-on; plus dyes, kerosene, ammonia | the acid and sulfur half of this list is redundant with vanilla - see above |
| **Media** | all fractions ship as one `config/liquids.json` in the add-on | the loader overlays every domain's file with no code (`ExLiquids.cs:74-97`) |

### The distinction that makes the whole thing small: electrolyte ≠ reagent

Recorded at [scope.md](../../scope.md) § elex and owned in mechanism by
[electrolysis cell](../elex/electrolysis-cell.md). Copper electrorefining recirculates its bath; copper
dissolves off the anode and plates onto the cathode, so the acid is a one-time charge plus top-ups.

With the vanilla finding, elex's #2 dependency is not "one small block". It is zero blocks: the quantity is
tiny and the source already exists.

## What it would unblock

| Consumer | Needs | Severity in [scope.md](../../scope.md) | Severity after checking vanilla |
|---|---|---|---|
| **elex** electrolysis cell | sulphuric acid electrolyte | "probably one small recipe" | none - vanilla ships the item *and* the recipe |
| **smex** copper add-on | sulfur, for roasting | "none recorded", the only dependency with no fallback | none - `powder-sulfur` is a vanilla item |
| **elex** arc furnace | coal-tar pitch (+ petcoke) for graphite electrodes | degraded path | unchanged - and it is not chemistry's: see [gasworks](gasworks.md) and [oil](oil.md) |
| **Homestead** itself | dyes, kerosene wash, nitration, ammonia salts | internal | internal - the bench is the mod's own workhorse |
| **The ferrous line** | — | — | nothing. The release target ([scope.md](../../scope.md)) has no chemistry on it |

elex has one chemistry dependency left, not three, and that one has a period-correct fallback. So the "shape
elex would take with no chemistry" in [scope.md](../../scope.md) - *"arc furnace + HSS on carbon electrodes,
DC only, impure copper wire, but no alternators, because pure copper needs the electrolysis bath"* - is too
pessimistic. If the bath can be filled from vanilla, pure copper is reachable and so are alternators. That is
a scope-owned statement, so this page flags it rather than rewriting it; see [Open](#open).

## Gotchas

* **Reachable is not the same as balanced.** Vanilla's acid is a cooking-pot recipe. Whether a firepit and a
  clay pot are an acceptable supply for an electrolysis hall is a balance question, and a fair answer may
  still be "Homestead adds the industrial route, vanilla covers the first fill". The scope cut is unaffected
  either way - this removes the blocker, not the design space.
* **The real remaining cost is plumbing, not chemistry.** Vanilla acid is an `ItemLiquidPortion` with
  `waterTightContainerProps` (`acid.json:15-32`, `allowSpill` at `:27`) - it lives in buckets and barrels.
  The suite's fluids live in `PipeNetwork` runs with a `MediumType` from `ExLiquids`
  ([pipe network](../../mechanics/pipe-network.md)). Nothing bridges the two, in either direction, anywhere in
  `src/`. Any machine that wants to plumb acid needs that bridge built first, and nobody has scoped it.
* **R1 makes an acid main expensive anyway.** One medium per run, and liquids - unlike gases - only merge
  with themselves (`ExLiquids.cs:115`, *"liquids: same only"*). An acid line is a dedicated line. For a
  one-time fill, a bucket is right.
* **The sulfur severity was over-rated.** The copper add-on only ever consumes sulfur, and only cosmetically
  ([copper reverberatory](../non-ferrous/copper-reverberatory.md)), which never matched the "none - no
  fallback" severity in the scope table. Both readings are moot: the sulfur is already in the game.
* **No danger model exists.** Acid, like ammonia, would want a hazard layer, and the suite has none - the
  closest live analogue is molten-metal contact damage. Not a blocker; a missing system.
* **Do not build an acid plant to get sulfur.** The archived lpex spec has the gasworks producing sulfur from
  ammoniacal liquor - historically defensible, and unnecessary in a game where sulfur is an ore.

## Open

| # | Question | Notes |
|---|---|---|
| 1 | **Should [scope.md](../../scope.md)'s elex severity table be rewritten?** | Recommend yes: the acid and sulfur rows go from "probably one small recipe" and "none recorded" to "none - vanilla", with this page as the citation. That is a scope-owned edit and this page must not make it |
| 2 | **Does the no-chemistry elex subset gain alternators?** | It hinges entirely on #1. If the bath is fillable from vanilla, "no alternators" no longer follows |
| 3 | **Does Homestead replace vanilla's cooking-pot acid, or scale it?** | Replacing a working vanilla recipe is a compatibility decision, not a design one |
| 4 | **Does anything ever need nitric or hydrochloric?** | Both are defined-but-disabled in vanilla (`acid.json:6`, `:8-9`). The dye chain historically wants nitric. Nothing ferrous does |
| 5 | **The item ↔ medium bridge** | Unscoped, unowned, and the actual prerequisite for plumbed chemistry. Worth a mechanics page of its own if it is ever built |
| 6 | **Whether Industrial Homestead gets a doc** | this tree is currently the nearest thing |
