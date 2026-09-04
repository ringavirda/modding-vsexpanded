# Tall Hopper
**Status** live   **Mod** iiex

**Owns**
- The hopper's tank model: one `ItemStack`, one material and one stamped mix at a time,
  machine-agnostic acceptance delegated to the core below.
- The continuous drip: the cadence, the per-second rate, and the fact that which cell the material
  lands in is entirely the furnace's decision.
- The hopper's footprint, its top-filler interaction routing, its grid recipe and its drops.

**Does not own** - cited only, never restated:
- Burden item identity, stack attributes, the flux-ratio stamp - [burden](../items/burden.md).
- The charge-column model, the band-order rule, why coke is charged separately, and what the furnace does
  with a charged shaft - [layered-charge](../layered-charge.md),
  [heat balance](../mechanics/heat-balance.md), [cold blast furnace](blast-furnace-cold.md),
  [cupola](cupola.md).
- Fillers, footprints, filler interaction rerouting, the build-outline projection -
  [multiblock](../mechanics/multiblock.md).
- Code-first defs, recipes, cost catalogue - [recipes-config](../mechanics/recipes-config.md).
- Unit mass - [density rule](../mechanics/density-rule.md).
- Where the burden came from - [burdenmaker](burdenmaker.md).

---

## Role

The furnace owns charge columns - per-column stacks of banded material inside its shaft
([layered-charge](../layered-charge.md)). The hopper is a two-cell-high tank the player fills at the
mouth; it drips its contents onto the column the furnace nominates, once per second, forever, with no
on/off toggle.

It is a dumb tank: it holds one material at a time and has no opinion about what is chargeable -
`Accepts` asks the anchored core (`core.IsChargeItem`, `BlockEntityHopperTall.cs:121-124`). One hopper
block therefore serves four machines - the blast furnace, the cupola, the heating furnace and the beehive
coke oven - and each admits its own charge and its own fuel. A hopper standing over nothing accepts
nothing: there is no machine to declare a charge, which is also what stops a hopper being filled and then
walled into a furnace that refuses what is already in it.

The player charges in rounds through this one block. Fill it with coke, let it drip, fill it with burden:
coke under burden, which is what the furnace's fuel-only-onto-burden rule enforces from the other side.
The single-stack tank rule is load-bearing: one load lays one band type. A tank that could hold coke and
burden at once would drip them interleaved, and no round could ever be laid.

Contrast smex's bell hopper, which manages a drop cadence and a stop state.

---

## Structure

A megablock of two cells - the smallest one in the suite.

| | |
|---|---|
| Footprint | 1 × 2 × 1 - the principal plus one filler directly above |
| Layout | `StructureFootprint.Layout` with `Origin(0, 1)` and slice `"#\n0"` (`BlockHopperTall.cs:51-62`) |
| Resolved filler | `(0, +1, 0)` |
| Orientation | `side` variant (`.SideVariant()` + the `ExOrientable` behaviour, `BlockHopperTall.cs:43`, `:70`); `StructureAngle => ExOrientation.AngleFromSide(Variant["side"])` (`:77`) |
| Interactive cell | the top filler only |

The footprint itself is rotation-invariant (the filler sits directly above the base), but the block carries
a side variant so a furnace layout can demand a correctly-facing hopper: the oriented-parts rule rotates
the required facing with the structure, which is what stops a rotated cupola charging outside itself.

Interaction lands on the top cell, not the base. Deposit/withdraw is reachable only through the filler,
which reroutes to the base's block entity via `IFillerInteractionTarget` (`BlockHopperTall.cs:164-192`).
The base cell itself carries the `MultiblockStructure` behaviour so the build-outline gesture works there
directly; the filler path re-implements that gesture (`BlockHopperTall.cs:124-131`) so it is consumed
before the tank deposit - otherwise Ctrl+Shift+right-click would empty the tank instead of previewing the
furnace.

### Where it sits in a furnace layout

The hopper is a functional cell of the furnace's own multiblock layouts (the layout code and the blocktype
goldens are the authority). It resolves its owning furnace by scanning for a `BlockEntityFurnaceCore`
through a `MultiblockAnchorLink` (`BlockEntityHopperTall.cs:42-52`) with the shared component-scan bounds
(`BlockEntityFurnaceCore.ComponentScanHorizontal` / `ComponentScanBelow` / `ComponentScanAbove`). That one
link answers the acceptance question, the drip target, the shaft-charge HUD line and the build-outline
projection.

---

## Assets

| Asset | Path | State |
|---|---|---|
| Editable shape | - | missing. There is no `assets/editable/shapes/` source for this block |
| Runtime shape | `assets/iiex/shapes/hopper-tall.json` | present - flat cubes, 32 px tall (2 blocks) |
| Animations | - | none authored, none needed (the hopper is not an RCC block and has no `Animatable` behaviour) |
| Textures | `front1`, `iron3`, `iron2`, `iron5` | declared in the shape |

The block renders its plain mesh: `SolidNonOpaque()` (`BlockHopperTall.cs:63`), no RCC, no animator, no
surface renderer. There is no visible fill level - the tank contents are readable only from the block
info line.

---

## Construction

Grid recipe - `Recipes/Grid/FurnaceRecipeDefinitions.cs:74-82`:

```
_ H _        H = hammer (tool, not consumed)
P S P        P = plate  × 1 each  → 4 plate
S P S        S = nails  × 1 each  → 4 nails
```

Output `iiex:hopper-tall-n` × 1. `Plate` and `Nails` come from the shared `ExIngredients` helpers.

In the recipe-cost catalogue as `hoppertall-grid` (`IiexRecipeConfig.cs:90`), so it responds to
`/exmod recipes iiex cheap`.

---

## Operation

```
charge material (burden, or the furnace's own fuel)
      │  right-click the TOP cell
      ▼
   tank: 1 stack, 1 stamped mix, 128 u
      │  8 u/s, server tick 1000 ms, no toggle
      ▼
 core.NextChargeColumn(material) - lowest column first, fuel only onto burden
      │
      ▼
 ChargeColumn.Push - the column grows; the furnace materialises charge-pile blocks to match
```

### Verbs

| Gesture (on the top cell) | Effect | file:line |
|---|---|---|
| right-click with a charge item | deposit one unit | `BlockHopperTall.cs:132-146` |
| ctrl + right-click | deposit the whole held stack | same path, `wholeStack` |
| right-click empty-handed | withdraw the entire tank | `BlockHopperTall.cs:147-158`, `BlockEntityHopperTall.cs:160-168` |
| ctrl+shift+right-click | toggle the furnace build outline | `BlockHopperTall.cs:124-131` |

A mismatched material or stamp raises `iiex-hoppertall-wronggrade` (`:140-145`). A full tank is swallowed
silently - that is a state the block info already shows, not an error.

### Acceptance

`Accepts` = the anchored core resolves and `core.IsChargeItem(stack)` and (tank empty or same collectible
with an identical stamp) (`BlockEntityHopperTall.cs:121-124`, `IsMergeable` `:172-175`). One stack cannot
carry two mixes, so the tank is strictly single-mix. The stamp comparison is `Burden.Read` equality - the
same rule that keeps differently-fluxed batches apart in every other container.

### The drip — `OnServerTick`, `BlockEntityHopperTall.cs:196-238`

Every 1000 ms, server-side (`:85`), if the tank is non-empty and a core resolves:

1. Read the tank's material code and confirm the core still charges it (`IsChargeCode`, `:206-208`) - a
   stack whose item no longer resolves holds rather than throws.
2. Ask the core for a target: `NextChargeColumn(material, out room)` (`:210`). The furnace answers with
   the lowest column that will legally take this material - fuel only onto burden - or null when the
   shaft is full or the band order refuses the material everywhere. The hopper then holds; it never
   spills, never voids.
3. `amount = min(max(1, HopperTallDropPerSecond), tank, room)` (`:214-217`).
4. `column.Push(material, amount, core.ChargeTemperature, Burden.Read(_tank))` (`:224`) - the stamped mix
   rides along, read off the tank rather than recomputed, because the flux ratio has to reach the raceway
   intact. Fuel carries `default`, which is what `Burden.Read` answers for an unstamped stack.
5. `core.SyncChargeBlocks()` (`:232`) - blocks appear as a column crosses a cell boundary, and every
   surviving pile republishes the snapshot its mesh reads.
6. Falling-dust particles below the hopper, a `StoneCrush` sound at it (`:235-236`).

There is no world scan, no `game:coalpile` and no per-cell cap in this block: which cell the material lands
in is entirely the furnace's `NextChargeColumn`, so the hopper contributes the tank, the cadence and
nothing else. A column's ceiling is its own cell count times the furnace's block quantum - owned by
[layered-charge](../layered-charge.md).

---

## Numbers

### Config — `src/IronIndustryExpanded/IiexConfig.cs`, `ModConfig/ex_values.json`, domain `iiex`

| key | value | file:line | what it does |
|---|---|---|---|
| `HopperTallCapacity` | `128 u` | `IiexConfig.cs:703` | tank size - exactly one burden stack |
| `HopperTallDropPerSecond` | `8 u/s` | `IiexConfig.cs:707` | drip rate while a target column exists |

There is no per-cell cap key and no drop-depth key: a column's ceiling is geometry, and how far down to
look is the anchor link's question, not this block's.

### Hard-coded — not config

| value | file:line | what it does |
|---|---|---|
| tick period `1000 ms` | `BlockEntityHopperTall.cs:85` | drip cadence; not the same knob as `HopperTallDropPerSecond` |
| tree key `"tank"` | `BlockEntityHopperTall.cs:250`, `:261` | the whole persisted state |
| grid recipe cost (4 plate + 4 nails) | `FurnaceRecipeDefinitions.cs:76-80` | |
| dust particles / `StoneCrush` at volume 0.4, range 16 | `BlockEntityHopperTall.cs:235-236` | drip feedback |

### Derived

| quantity | value | from |
|---|---|---|
| time to empty a full tank | 128 / 8 = 16 s | `Capacity / DropPerSecond` |

---

## Drops

`GetDrops` returns the hopper block itself plus the tank contents (`BlockHopperTall.cs:83-99`):

```csharp
if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityHopperTall be
    && be.TankContents is { } burden)
  drops.Add(burden.Clone());
```

Nothing is lost on break. The top filler cell is removed by `BlockFilledMegastructure`.

The drop is a clone of the live stack, so its stamp survives verbatim - a broken hopper hands back exactly
the material it held, not a re-stamped copy.

---

## Code

| Piece | file:line |
|---|---|
| `BlockHopperTall : BlockFilledMegastructure, IFillerHost, IFillerInteractionTarget, IExBlockDefProvider` | `BlockStructures/Furnaces/Blocks/BlockHopperTall.cs:24` |
| `Definitions` (code-first blocktype + footprint + side variant) | `BlockHopperTall.cs:32-71` |
| `StructureAngle` | `BlockHopperTall.cs:77` |
| `GetDrops` (adds the tank) | `BlockHopperTall.cs:83-99` |
| `HandleInteract` (projection first, then deposit/withdraw) | `BlockHopperTall.cs:108-162` |
| filler forwarding | `BlockHopperTall.cs:164-192` |
| `HopperInteractionHelp` | `BlockHopperTall.cs:201-252` |
| `BlockEntityHopperTall : BlockEntity, IMultiblockComponent` | `BlockStructures/Furnaces/BlockEntities/BlockEntityHopperTall.cs:31` |
| `Anchor` / `ResolveOwningAnchor` | `BlockEntityHopperTall.cs:42-55` |
| `Accepts` / `IsMergeable` | `BlockEntityHopperTall.cs:121-124`, `:172-175` |
| `TryDeposit` / `TryWithdraw` | `BlockEntityHopperTall.cs:132-168` |
| `OnServerTick` - the drip | `BlockEntityHopperTall.cs:196-238` |
| `GetBlockInfo` (+ shaft charge line) | `BlockEntityHopperTall.cs:268-292` |

### Where a caller hooks in

- The furnace owns the target. The contract is `BlockEntityFurnaceCore.NextChargeColumn` +
  `ChargeColumn.Push` + `SyncChargeBlocks`. Any new charging device only has to ask the core for a column
  and push into it; it never places blocks itself.
- The shaft HUD line is furnace-owned and re-surfaced here:
  `Anchor.Resolve()?.AppendShaftChargeInfo(dsc)` (`BlockEntityHopperTall.cs:291`).

### Tests

`test/IronIndustryExpanded.Tests/Blocks/Furnaces/HopperTallTests.cs` - tank fill/cap, the continuous drip
against the core's column contract, interaction routed from the top filler cell, persistence.

---

## Gotchas

1. The interaction-help carousel shows only burden. `ResolveBurdenStack` resolves `iiex:burden` alone
   (`BlockHopperTall.cs:248-252`), while `Accepts` takes anything the core charges - including the
   machine's own fuel. The hints therefore under-describe the block; the fix is a code change (the item
   list cannot be resolved headlessly, so it has shipped as-is).

2. The class doc on the block entity predates the column cutover. `BlockEntityHopperTall.cs:16-27` still
   says the hopper "drops either burden family and never gates acceptance" - there is one burden item, no
   family, and `Accepts` does gate (through the core). Read the member docs, not the class summary.

3. The projection gesture must be consumed before the deposit. `TryToggleProjection` is called first in
   `HandleInteract` (`BlockHopperTall.cs:124-131`) so ctrl+shift+right-click previews the furnace instead
   of emptying the tank. Reordering those blocks is a real regression with no test asserting the order.

4. Withdraw is all-or-nothing. `TryWithdraw` hands back the whole stack and nulls the tank
   (`BlockEntityHopperTall.cs:160-168`); there is no partial take.

5. Ctrl, not sneak - sneak+right-click with a held item is taken by vanilla ground-storage placement
   before it reaches the block.

6. The tank has no visible fill. No renderer, no animation, no block state change. The only signals are
   the block info line and the drip sound.

7. `HopperTallDropPerSecond` is misnamed if the tick period changes. The rate is applied once per
   hard-coded 1000 ms tick, not scaled by `dt`; halving the tick would double the throughput silently.

---

## Open

- No automated fill. The tank is hand-charged only; there is no path from the burdenmaker's basin into a
  hopper - the player is the buffer. A chute/screw/skip-hoist feed is roadmap material, as a building
  rather than fewer clicks ([ironmaking](../processes/ironmaking.md)).
- No editable shape. The runtime shape is the only copy; there is no source file under
  `assets/editable/shapes/`.
- No fill indicator. A 2-block machine with a hidden 128-unit buffer is the one place in the iron tier
  where "nothing is hidden" (R7) is only satisfied by the text HUD.
- Fix the help carousel to show every material the anchored core accepts (Gotcha 1), and reword the
  block-entity class doc (Gotcha 2).
