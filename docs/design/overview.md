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

The dependency order follows the tech-tree order: iron → steam → steel → high-pressure → electric.

```
exlib ──▶ iwex ──▶ lpex ──▶ smex ──▶ hpex
                                       ▲
                                elex ──┘ (also needs smex + chemistry)
```

| Mod | id | Tier | Owns | Depends on |
|---|---|---|---|---|
| **Expanded Library** | `exlib` | framework | network logic (Pipe/Molten/MP), definitions, registries, config, helpers, test harness | game |
| **Ironworking Expanded** | `iwex` | iron (low-tech) | cold blast furnace, molten-canal network, burdenmaker, mechanical (MP) air blower, plated pipes, `gear-iron` | exlib |
| **Low Pressure Expanded** | `lpex` | low steam power | Cornish boiler + Watt engine, cast pipes, steam pump + mechanical MP pump, MP power, fluid tank, the boring machine *(designed, art drawn - nothing built)* | iwex, exlib |
| **Steelmaking Expanded** | `smex` | steel | hot blast furnace + cowper stoves, Bessemer, open hearth, ladle, billet/forming | lpex, iwex, exlib |
| **High Pressure Expanded** | `hpex` | high steam (planned) | HP steam engines/boilers, large-scale/community machines (large blast furnace, large engines) | smex, … |
| **Electrical Expanded** | `elex` | electric (planned, last) | full-realism AC/DC grid: dynamo, alternator, electrolysis, arc furnace, HSS | hpex, smex |

Content add-ons (opt-in, off the core spine; may ship as sections of their parent mod or as
separate projects):

| Add-on | Parent | Adds | Scheduled? |
|---|---|---|---|
| **Crucible** | iwex | crucible-steel furnace (simple multiblock); cementation is vanilla - not implemented | off-spine, unscheduled |
| **Copper** | smex | reverberatory furnace, Pierce-Smith converter, zinc retorts (no acid plant - sulfur comes from chemistry) | off-spine, unscheduled |

---

## Scope — what we are building, and what we are not

Scope is owned by [scope.md](scope.md): the Homestead cut and its two carve-outs, the non-ferrous
deferral, elex's three chemistry dependencies with their differing severity, and the release target.
Scope questions are answered there, never here.

---

## The low-tech combo

`iwex` + `lpex` (optionally + `smex`) is a complete, self-contained experience: cold-iron
smelting → low steam power → steel, all runnable on water/MP power (a vanilla waterwheel drives
the mechanical blower and pump - no steam setup required to make iron). A player who wants to stay
low-tech never installs `hpex` or `elex`. The pipe tiers keep this clean: `iwex` ships its own
plated-pipe tier so it never depends on `lpex`'s cast pipes.

---

## Tech-tree spine (materials gate construction)

Construction is gated by material, not recipe unlock:

1. Cast iron (iwex) → Stage-II engines & LP machinery.
2. Steam (lpex) → hot blast + Bessemer (smex).
3. Hadfield steel + rolled pipe (smex) → HP boilers/engines (hpex).
4. HP power + pure copper (hpex + copper) → electric tier (elex).

No hard circular dependencies. See [materials.md](materials.md) for the material-gated power-tier rule
(LP = cast iron, HP = hadfield steel).

---

## Suggested build order

1. Iron (iwex). Coke oven, cold blast furnace (waterwheel-blown), cupola, sand casting, puddling
   furnace, reheat furnace + rolling mill, cast/forged components.
2. Low steam (lpex). Cornish boiler + Watt engine, blower/pump/flywheel, cast pipes, steam
   hammer, boring machine, fluid tank.
3. Steel (smex). Hot blast + cowpers + Bessemer, gas producer + open hearth, ladle, steel roll
   sets.
4. High steam (hpex). HP engines, large-scale machines, rolled-pipe fittings.
5. Electric (elex). Dynamo → alternator → electrolysis → arc → HSS. Last, isolated.

Off-spine branches (crucible, copper) and everything on [scope.md](scope.md)'s deferral list are not
part of this order.
