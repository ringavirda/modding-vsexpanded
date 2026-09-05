# Burdenmaker

**Status** live (built 2026-08-07)   **Mod** iiex

**Owns** - the facts this page is canonical for:

* the burdenmaker's role: one block that both proportions burden and buffers it,
  and why no mixing mechanism is needed;
* its footprint, principal cell, and the per-cell interaction map;
* the two hoppers - which material each takes, and the fact that neither is gated or locked;
* the gate: one sliding lid under both hoppers, and what opening it does;
* the crate semantics - free add and remove, no batch state, no MP;
* its shape, element groups, animations and RCC stage sequence;
* its construction recipe, capacities and drops.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| Why burden is ore + flux and coke is charged separately | [layered-charge](../layered-charge.md) |
| Burden item identity and what remains of its stamp | [burden](../items/burden.md) |
| Where burden goes next, and the charging rule | [tall-hopper](tall-hopper.md) · [blast-furnace-cold](blast-furnace-cold.md) |
| Megablock footprints, fillers, per-cell interaction routing | [multiblock](../mechanics/multiblock.md) |
| Code-first defs, RCC stage machinery, goldens, the cost catalogue | [recipes-config](../mechanics/recipes-config.md) |
| `1 vx³ = 2.5 u` | [density-rule](../mechanics/density-rule.md) |
| Where crushed ore comes from | [ore-crusher](ore-crusher.md) · [roasting](../processes/roasting.md) |
| R2 recoverability | [recoverability](../mechanics/recoverability.md) |

**Depends on** [layered-charge](../layered-charge.md) · [burden](../items/burden.md) ·
[multiblock](../mechanics/multiblock.md) · [recipes-config](../mechanics/recipes-config.md) ·
[roasting](../processes/roasting.md)

---

## Role

With coke charged as its own courses ([layered-charge](../layered-charge.md)), proportioning collapses to a
single ratio - ore against flux - so no measuring mechanism is needed. The burdenmaker is not a mixer: it is
a stock house, two hoppers over a bunker with a gate between them, covering both the proportioning and the
buffering.

No mechanism means no power: there is no MP port, so the burdenmaker is buildable before any power exists.
That is what keeps nothing before cast iron requiring power, and why iiex has no dangling MP consumer
upstream of the blast furnace.

**Crate semantics.** Materials go in and come out freely, one or a stack at a time, with no batch state to
get stuck in. There are no batch refusals because there is no batch, no lock and no cycle to interrupt.

---

## Structure

A **megablock**: one real block, the rest invisible fillers.

```
layer 0        layer 1
# # #          # # #
# O #          . . .
```

`Origin(-1, -1)`; rows run +Z, glyphs run +X. 9 cells:

| Level | Row | Cells | Carries |
|---|---|---|---|
| y = 0 | z = −1 | `(-1,0,-1)` `(0,0,-1)` `(1,0,-1)` | the bunker basin, front half |
| y = 0 | z = 0 | `(-1,0,0)` `(0,0,0)` `(1,0,0)` | the bunker basin, back half - `(0,0,0)` is the principal |
| y = 1 | z = −1 | `(-1,1,-1)` `(0,1,-1)` `(1,1,-1)` | the two hoppers and their gate |
| y = 1 | z = 0 | - | open |

The y = 0 level is a single walled basin, not two rows: both `Base` (z = 0) and `BaseExtension` (z = −1)
contribute a floor and an outer wall, with side walls closing the ends, so burden collects across the whole
level. The hoppers sit above its front half only, which leaves the back open for the player to reach in.

### Per-cell interaction

The hoppers' widths map exactly onto cells, so classification needs no geometry:

| Cell | Shape extent | Class | Right-click does |
|---|---|---|---|
| `(-1,1,-1)`, `(0,1,-1)` | large hopper, X −12…15 | **Ore hopper** | add / take crushed or roasted iron ore |
| `(1,1,-1)` | small hopper, X 17…28 | **Flux hopper** | add / take lime |
| `(0,0,0)` | principal | **Gate** | open / close the sliding lid |
| the other five y = 0 cells | basin | **Bunker** | take burden - never deposit |

Ctrl + right-click moves a whole stack, plain right-click one unit - the established idiom (ctrl, not
sneak, because vanilla ground-storage placement takes sneak first).

Help text is routed by cell off the same classifier - `iiex:burdenmaker-help-{addore, addore-stack, addflux,
addflux-stack, take, gate}`. This machine has no GUI, so the interaction overlay is the only thing that
tells the player the two hoppers differ; the wrong hopper simply refuses, silently. The accepted stacks in
each hint are resolved from the material registry, so an ore contributed by another mod appears there
without a code change.

The classification must invert the placement rotation. The two hoppers differ by X, so a classifier
comparing raw world offsets would put ore in the flux hopper at half the facings while looking correct
facing north - the facing every test fixture places a machine at. `Classify` takes the structure angle for
exactly this reason; do not simplify it away.

The 2 px of hopper geometry that reaches into y = 2 is accepted as overhang; no cell is reserved for it.

---

## Operation

```
crushed / roasted iron ore ──> large hopper ─┐
                                             ├─ open the gate ─> bunker ─> iiex:burden
game:lime ───────────────────> small hopper ─┘
```

1. Load either hopper, in any order, any amount, at any time. Nothing is locked and nothing is consumed.
2. When the ratio reads right, open the gate. Both hoppers drain together - there is one lid, so there is
   one decision. The drain is instant: this machine has no mechanism and therefore no throughput lever to
   hang a rate on (decided 2026-08-06).
3. The burden collects in the bunker. It must be taken out before the next charge, which keeps the machine
   honest about batch boundaries without a batch state machine.

The block info line carries both hopper contents and the flux fraction the current pair would produce, named
as under-fluxed / right / over-fluxed. With coke gone from the item, that is burden's only remaining quality
([burden](../items/burden.md)).

The player is the buffer: nothing decouples the burdenmaker's output from the furnace's appetite. Fine at
present batch sizes - watch it if the batch shrinks.

---

## Assets

Editable source: `workbench/shapes/machine-megablock-burdenmaker.json` → exported to
`mods/iiex/assets/iiex/shapes/ore/burdenmaker.json`. A `Root` element wraps the five groups in the editable, so the
RCC stages can address them; the export is `infra/tools/convert-shape.py`, which is also the only place that
can set the clip endings (`closed` = Repeat, `open` = Hold) - no C# test in this repo can see them.

| Group | Absolute extent | Is |
|---|---|---|
| `Base` | X −16…32, Y 0…16, Z 0…16 | basin floor + back wall, z = 0 |
| `BaseExtension` | X −16…32, Y 0…16, Z −16…4 | basin floor + front wall, z = −1 |
| `HopperMasonry` | X −16…32, Y 16…32, Z −16…0 | the brick surround at y = 1 |
| `Hoppers` | X −12…28, Y 24…34 | large X −12…15 (27 px), small X 17…28 (11 px) - ≈ 2.5 : 1 |
| `Lids` | X −12…28, Y 16…18 | rails plus one animated `Lid` spanning both hoppers |

Textures: `fire1` (running fire brick), `burned` (vessel sides - rails and crossbar), `iron4` (sheet iron -
hoppers and lid).

**Animations.** `closed` is the idle clip and `open` is the drawn-back gate; both are single-keyframe pose
toggles driving `Lid`'s `offsetZ` (0 → 4). Two invariants any re-export must preserve:

1. `open` must be `onAnimationEnd: Hold`, not `EaseOut`. The lid pose is a held end-state cleared by
   `StopAnimation`; a single keyframe with `EaseOut` eases back to rest, so the gate slides shut on its own.
2. `closed` serves as the idle pose, and `RightClickConstructable` suppresses the default mesh - so it must
   keep running for the block to be visible at all. A one-shot `EaseOut` clip will not hold the mesh up.

---

## Construction

A grid recipe places the shell (settled 2026-08-06): 12 same-colour fired bricks, `BBB,BBB` - the machine's
own 3 × 2 floor plan - with the catalogue row
`["burdenmaker-grid"] = Grid("iiex:burdenmaker-*")`. Brick only, because this is the only source of burden
and anything it asks for must already be makeable in a fresh world. The 3 × 2 shape also keeps it
unambiguous against other wildcard-brick recipes.

The element groups are the RCC stage sequence, and also the cost curve - masonry first and cheap, ironwork
last and dear:

| Stage | Elements | Character |
|---|---|---|
| 1 | `Root/Base` | free - the placed shell |
| 2 | `Root/BaseExtension` | brick |
| 3 | `Root/HopperMasonry` | brick |
| 4 | `Root/Hoppers` | sheet iron |
| 5 | `Root/Lids` | sheet iron + fittings |

Nothing may require an iiex item. iiex declares no dependency on iiex, and this machine is the only source
of burden - a cross-mod ingredient here once made burden unreachable for an iiex-only player. The
burdenmaker has no gear and must keep it that way.

---

## Numbers

Settled 2026-08-06:

| Key | Value | Note |
|---|---|---|
| `BurdenmakerOreCapacity` | 512 | the wide hopper |
| `BurdenmakerFluxCapacity` | 205 | the drawn 2.5 : 1 hopper width ratio |
| `BurdenmakerBunkerCapacity` | 1152 | the basin |

The flux hopper is not one batch's worth of lime - the hoppers are storage, not measures.

Caution: the slot ranges behind these keys must always leave the unit cap binding first. At slot counts of
4/2/9 a 64-stack ore would fill the wide hopper at 256 units against a configured 512 - the key promising
twice what the machine holds. The shipped counts are 8 / 4 / 18, against a conservative 64-stack worst case,
pinned by `Every_tank_can_actually_reach_its_configured_capacity`.

---

## Drops

Everything comes back: both hopper contents and the bunker contents, plus the RCC stage refunds. Under R2
nothing may be destroyed on break, and with no batch state there is no reason for any loss.

---

## Code

| Piece | Where |
|---|---|
| Shape export | `mods/iiex/assets/iiex/shapes/ore/burdenmaker.json` |
| Block def, 8 fillers, 5 RCC stages | `BlockBurdenmaker.Definitions` |
| Cell classifier | `BlockBurdenmaker.Classify(principal, clicked, angle)` |
| Hoppers, basin, gate, drops, readout | `BlockEntityBurdenmaker` |
| Per-cell interaction and per-cell help | `BlockBurdenmaker.HandleInteract` / `BuildInteractionHelp` |
| Grid recipe + catalogue row | `OreProcessingRecipeDefinitions.Burdenmaker` · `IiexRecipeConfig` |
| Handbook page | `mods/iiex/docs/handbook/01-orehandling.html` (+ `00-ironworking.html`), all three locales |

The handbook pair covers the burdenmaker and the two-stream charge, and is translated into `ru` and `uk`
(2026-08-07) - the first two iiex handbook pages translated at all. The other three iiex pages are still
English in the RU/UK files, a gap in this mod alone: iiex, hpex and smex all ship translated handbooks.

---

## Open

1. Whether roasted ore is a distinct input with a different flux requirement, or merely a better-yielding
   substitute for crushed ore. [roasting](../processes/roasting.md) is unbuilt and this is its first
   consumer. The interaction help already promises it - `iiex:burdenmaker-help-addore` reads "crushed or
   roasted iron ore" - while `materialroles.json` grants the `ironore` role to the `crushed-iron` path
   prefix only. The hint is forward-looking, not a description of what the machine takes today.

2. No in-world contents rendering. The three `OreSurfaceRenderer` instances (two hopper interiors and the
   basin) are not built, so what is loaded is legible only from block info.
