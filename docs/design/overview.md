# Expanded Mods — Design Overview

The **Expanded** family is a linear 19th-century ferrous-metallurgy and steam-power progression,
split across several mods so a player can take it as far as they like. This document is the map: the
mods, how they depend on each other, and the order things unlock. Per-mod feature specs live in the
sibling files; shared rules and numbers live in [conventions.md](conventions.md) and
[materials.md](materials.md).

> **How to read the docs.** A fact marked *(live)* is already implemented in code. Everything else is
> the **proposed baseline** and is config-tunable via the exlib config system unless stated. Numbers
> appear **once**, in their canonical table (conventions/materials/the mod doc that owns the machine),
> and are cited elsewhere as "(see X)" — never restated.

---

## Design pillars

- **Cheap kickstart.** Iron-tier machinery (water + cast iron) bootstraps everything. Steel is never
  required to *start*; it is the reward for progressing.
- **Smooth, gapless progression.** Each tier's output material is the next tier's build requirement —
  a linear tech tree, no hard circular dependencies. Construction is gated by **material and
  machine-building, not ore rarity**.
- **Immersive + historically grounded.** Real processes, machines and materials; deliberate gameplay
  liberties are called out where taken.
- **Pays off in bulk vanilla goods** (not just more machines).
- **No GUI windows** unless unavoidable; all interaction is in-world and verb-based, all state readable
  from block-info.

---

## Mods & dependency chain

The dependency order follows the tech-tree order: **iron → steam → steel → high-pressure → electric**.

```
exlib ──▶ iwex ──▶ ppex ──▶ smex ──▶ hpex
                                       ▲
                                elex ──┘ (also needs smex + chemistry)
```

| Mod | id | Tier | Owns | Depends on |
|---|---|---|---|---|
| **Expanded Library** | `exlib` | framework | network **logic** (Pipe/Molten/MP), definitions, registries, config, helpers, test harness | game |
| **Ironworking Expanded** | `iwex` | iron (low-tech) | cold blast furnace, molten-canal network, ore bunker/mixer, **mechanical (MP) air blower**, **bolted pipes**, `gear-iron` | exlib |
| **Pipes & Power Expanded** | `ppex` | low steam power | Cornish boiler + Watt engine, **cast pipes**, steam pump **+ mechanical MP pump**, MP power, fluid tank/sprinkler | iwex, exlib |
| **Steelmaking Expanded** | `smex` | steel | hot blast furnace **+ cowper stoves**, Bessemer, open hearth, ladle, billet/forming | ppex, iwex, exlib |
| **High Pressure Expanded** | `hpex` | high steam (planned) | HP steam engines/boilers, large-scale/community machines (large blast furnace, large engines) | smex, … |
| **Electrical Expanded** | `elex` | electric (planned, last) | full-realism AC/DC grid: dynamo, alternator, electrolysis, arc furnace, HSS | hpex, smex, chemistry |

**Content add-ons** (opt-in, off the core spine; may ship as sections of their parent mod or as
separate projects):

| Add-on | Parent | Adds |
|---|---|---|
| **Crucible** | iwex | crucible-steel furnace (simple multiblock); cementation is **vanilla** — not implemented |
| **Domestic** (lights, fuel, colours & climate) | ppex | the coal-gas / coal-chemistry complex: **gasification plant** (multiblock), distillation still, oil derrick, benchtop chemistry → gas lighting, kerosene, aniline dyes, and **climate control** (radiator heat + **ammonia refrigeration** cooling); **produces the sulfur/acids** the copper add-on consumes |
| **Copper** | smex | reverberatory furnace, Pierce-Smith converter, **zinc retorts** (no acid plant — sulfur comes from chemistry) |

> The plan's old "IMEX (Ironmaking Expanded)" is this file's `iwex` (Ironworking Expanded). The old
> Sec-14 map that had the copper add-on producing the acid is **inverted** here: chemistry makes it.
> The **Domestic** add-on merges the former "Advanced Heating" and "Lights, Fuel & Colors" add-ons;
> references elsewhere to **"the chemistry add-on"** mean its chemistry subsystem (gasworks / still / benchtop).

---

## The low-tech combo

`iwex` + `ppex` (optionally + `smex`) is a **complete, self-contained experience**: cold-iron
smelting → low steam power → steel, all runnable on **water/MP power** (a vanilla waterwheel drives
the mechanical blower and pump — no steam setup required to make iron). A player who wants to stay
low-tech simply never installs `hpex` or `elex`. The pipe tiers make this clean: `iwex` ships its own
**bolted-pipe** tier so it never depends on `ppex`'s **cast pipes**.

---

## Tech-tree spine (materials gate construction)

Construction is gated by **material**, not recipe unlock:

1. **Cast iron** (iwex) → Stage-II engines & LP machinery.
2. **Steam** (ppex) → hot blast + Bessemer (smex).
3. **Hadfield steel + rolled pipe** (smex) → HP boilers/engines (hpex).
4. **HP power + pure copper** (hpex + copper) → electric tier (elex).

No hard circular dependencies. See [materials.md](materials.md) for the material-gated power-tier rule
(LP = cast iron, HP = hadfield steel).

---

## Suggested build order

1. **Iron (iwex).** Coke oven, cold blast furnace (waterwheel-blown), cupola + puddling, sand-cast
   pigs, cast/forged components, boring machine.
2. **Low steam (ppex).** Cornish boiler + Watt engine, blower/pump/flywheel, cast pipes, fluid
   tank + sprinklers.
3. **Steel (smex).** Hot blast + cowpers + Bessemer, ladle, billet/forming shop, open hearth.
4. **Branches, any order:** crucible / domestic (lights, fuel, colours & climate) / copper.
5. **High steam (hpex).** HP engines, large-scale machines.
6. **Electric (elex).** Dynamo → alternator → electrolysis → arc → HSS. Last, isolated.
