# Expanded Mods — Design Overview

The Expanded family is a linear 19th-century ferrous-metallurgy and steam-power progression,
split across several mods so a player can take it as far as they like. This document is the map: the
mods, how they depend on each other, and the order things unlock. Feature specs live one page per entity
under [machines/](machines/), [items/](items/), [processes/](processes/) and [mechanics/](mechanics/);
shared rules live in [conventions.md](conventions.md).

> How to read the docs. A fact marked *(live)* is already implemented in code. Everything else is
> the proposed baseline and is config-tunable via the exlib config system unless stated. Numbers
> appear once, on the page that owns them (conventions, or the entity page that owns the machine or
> item), and are cited elsewhere as "(see X)" - never restated.

---

## Design pillars

- **Operating efficiency is bought with build complexity.** Complexity goes into the build; efficiency
  comes out of the operation. A 3000 u cast slab is eight feeds and five stamps for fifteen plates,
  where hand-forging fifteen plates is an afternoon even with a helve. Optimise for fewer clicks on the
  operating side only: never simplify the factory to save the player a build, and never add clicks to a
  process to make it feel weightier.
- **Operating friction is the currency the tech tree is denominated in.** Every upgrade buys down a
  specific, named friction: hand forging → the mill; the carry-back walk → the train; crop-and-head each →
  the cutter bench; trips to the reheat furnace → a bigger power plant. Early friction must be real and
  felt, and removable by investment. Historicity and the hands-on feel of running real machinery are what
  make that purchase legible.
- **Cheap kickstart.** The answer to the item count needed to construct everything is cast iron: instead
  of hammering out dozens of plates the player casts a few frames and `castplate`s, and those make up most
  of a machine. Plus iron throughput - the blast furnace is ~2× a bloomery, and ore roasting on top. The
  machines still cost a ton of iron; nothing is materially cheaper, the iron is simply faster to produce
  and the parts faster to make. Steel is never required to start; it is the reward for progressing.
- **Smooth, gapless progression.** Each tier's output material is the next tier's build requirement -
  a linear tech tree, no hard circular dependencies. Construction is gated by material and
  machine-building, not ore rarity.
- **Immersive and historically grounded.** Real processes, machines and materials; gameplay
  liberties are called out where taken.
- **Pays off in bulk vanilla goods**, not just more machines.
- **Nothing is hidden** (R7). Every machine's state is legible to a player standing in front of it -
  temperature, pressure, what is in the pipe, how far through a cycle it is - and operation is in-world and
  verb-based: the player works the machine, not a menu. Machines carry status UI, two stations carry full
  windows (design table, boring machine) and more may follow. What is forbidden is state that cannot be seen.

---

## Mods & dependency chain

**Two content mods, one per loop** (ruling M1, 2026-08-13). Each loop is a tier of capability the
player unlocks and then re-tools around; the dependency order follows the tech-tree order.

```
exlib ──▶ iiex ──▶ siex ──▶ elex
       (iron)   (steel)   (electric, planned last)
```

| Mod | id | Loop | Owns | Depends on |
|---|---|---|---|---|
| **Expanded Library** | `exlib` | framework | network logic (Pipe/Molten/MP), definitions, registries, config, helpers, test harness | game |
| **Iron Industry Expanded** | `iiex` | **early industrial** | cold blast furnace + tub blowers, molten-canal network, burdenmaker, cupola, sand casting, puddling, the forming line (mill, wide hall, bending roller), **plated pipes**, iron gears — then the steam workshop it bootstraps into: Cornish boiler, Watt engine, pumps, **cast pipes**, MP power | exlib |
| **Steel Industry Expanded** | `siex` | **steel** | hot blast furnace + cowper stoves, Bessemer, open hearth, ladle, the gas producer and fuel-gas power, steel roll sets and cast stock, hadfield and the alloy line — then the high-pressure machines it gates: Lancashire boiler, Cornish engine, HP hammer, **rolled pipes** | iiex, exlib |
| **Electrical Expanded** | `elex` | electric (planned, last) | full-realism AC/DC grid: dynamo, alternator, electrolysis, arc furnace, HSS | siex, exlib |

⛔ **A loop is a capability tier, not a self-contained game.** The loops are **nested**: the steel loop
extends the early loop's machinery rather than replacing it, and cannot close on its own — the gas
producer is fed by an early-loop boiler, rolled pipe is curled on the early loop's bending roller, and
the ferroalloys hadfield needs are smelted in the early loop's cold blast furnace.

⛔⛔ **A loop keeps every rung inside it.** The plated pipe tier, the iron gears and the rest of the
early parts are the **bootstrap rung** — a player builds them before steam and upgrades afterwards. They
are progression, not duplication, and the merge collapses none of them (ruling M3).

*Historical:* these two mods were previously five — `iiex`, `iiex`, `smex`, `hpex` on a linear spine,
with closure defined per mod. Ruling M1 merged them on the tier line; ruling M2 moved closure to the
loop. Only `exlib`, `ppex` (now folded into `iiex`) and `smex` (now folded into `siex`) were ever
published, so the migration is a code relocation rather than a player-facing loss.

Content add-ons (opt-in, off the core spine; may ship as sections of their parent mod or as
separate projects):

| Add-on | Parent | Adds | Scheduled? |
|---|---|---|---|
| **Crucible** | iiex | crucible-steel furnace (simple multiblock); cementation is vanilla - not implemented | off-spine, unscheduled |
| **Copper** | smex | reverberatory furnace, Pierce-Smith converter, zinc retorts (no acid plant - sulfur comes from chemistry) | off-spine, unscheduled |

---

## Scope — what we are building, and what we are not

Scope is owned by [scope.md](scope.md): the Homestead cut and its two carve-outs, the non-ferrous
deferral, elex's three chemistry dependencies with their differing severity, and the release target.
Scope questions are answered there, never here.

---

## The low-tech stop

`iiex` alone is a complete, self-contained experience: cold-iron smelting → casting and puddling →
the forming line → low steam power, all of it startable on water/MP power (a vanilla waterwheel drives
the mechanical blower and pump — no steam setup is required to make iron). A player who wants to stay
low-tech never installs `siex` or `elex`, and loses nothing half-finished by stopping there.

**The bootstrap rung is what makes that true, and it is deliberate.** The plated pipe tier and the iron
gears exist so the early loop can plumb and gear itself *before* it has steam, cast pipe or a boring
machine. Upgrading to the cast tier later is the reward, not the entry price. Ruling M3: a merge
collapses no rung — the plated tier and the cast tier both live in `iiex`, separated by the `tier`
variant rather than by which mod shipped them (ruling M4).

---

## Tech-tree spine (materials gate construction)

Construction is gated by material, not recipe unlock:

1. Cast iron (`iiex`) → Stage-II engines & LP machinery. **Within the loop.**
2. Steam (`iiex`) → hot blast + Bessemer (`siex`). **The loop boundary**, and the one place a material
   gate crosses mods.
3. Hadfield steel + cast steel stock (`siex`) → HP boilers/engines (`siex`). **Within the loop.**
   ⛔ The rolled *pipe* is gated by the alloy and the stock, not by a machine: it is curled on the early
   loop's bending roller, which the steel loop extends rather than replaces.
4. HP power + pure copper (`siex` + copper) → electric tier (`elex`).

No hard circular dependencies. See [materials.md](materials.md) for the material-gated power-tier rule
(LP = cast iron, HP = hadfield steel).

---

## Suggested build order

**Loop 1 — `iiex`, the early industrial loop.**

1. Iron. Coke oven, cold blast furnace (waterwheel-blown via the tub blowers), cupola, sand casting,
   puddling furnace, reheat furnace + rolling mill, cast/forged components — **plated pipes and iron
   gears are the bootstrap rung here**, made before any steam exists.
2. Low steam. Cornish boiler + Watt engine, blower/pump/flywheel, **cast pipes** (the upgrade from
   plated), steam hammer, boring machine, fluid tank. The loop closes: the workshop can build itself.

**Loop 2 — `siex`, the steel loop.** More expensive, more efficient, and built on loop 1's machines.

3. Steel. Hot blast + cowpers + Bessemer, gas producer + open hearth, ladle, steel roll sets and cast
   stock — the alloys and the stock only this loop can supply.
4. High pressure. Lancashire boiler, Cornish engine, HP hammer, **rolled pipes**, large-scale machines.
   Gated by hadfield and by cast steel stock, not by new forming machinery.

**Then** — 5. Electric (`elex`). Dynamo → alternator → electrolysis → arc → HSS. Last, isolated.

Off-spine branches (crucible, copper) and everything on [scope.md](scope.md)'s deferral list are not
part of this order.
