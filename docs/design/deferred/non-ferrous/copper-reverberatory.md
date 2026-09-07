# Copper reverberatory furnace

**Status** deferred - no block, no BE, no burden family, no metal def, no recipe, no shape, no lang key,
no test.
**Would live in** the **Copper add-on**, parent `smex` ([overview.md](../../overview.md); the design comes
from the archived smex spec, under a header that already marked itself unscheduled).
**Deferred by** D8 - non-ferrous is later ([STATE.md](../../../../../docs/superpowers/plans/STATE.md)); the cut is owned by
[scope.md](../../scope.md) § Non-ferrous.

**Owns** - the facts this page is canonical for:

* what the copper reverberatory was specified to be, and its second job as the copper-side waste-alloy
  recycler;
* the fact that the machine is already built and the content is not - the reverberatory pattern ships live in
  `iiex`, with a file-by-file account of which override does what;
* the feedstock knot: matte smelting needs a copper sulphide, and there is none in the game;
* the temperature knot: a natural-draught reverberatory lands ~2 °C below copper's melting point under the
  shipped constants;
* what the elex arc route would and would not replace.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| The non-ferrous cut and its reasoning | [scope.md](../../scope.md) |
| `T_process = T_in − T_loss`, the fuel/air factors and every `Bf*` key | [heat balance](../../mechanics/heat-balance.md) |
| The worked natural-draught-vs-blown reverberatory ledger | [open hearth](../../machines/open-hearth.md):259-268 |
| The reverberatory that *is* built (the reheat furnace) and its numbers | [reheat furnace](../../machines/reheat-furnace.md) |
| Converter copper, the bronzes, and the waste-alloy recovery rule | [materials.md](../../materials.md) |
| Roasting as a process, and the fact that this mod models no sulphur chemistry | [roasting](../../processes/roasting.md):47, :69-71 |
| What happens to the matte afterwards | [Pierce-Smith](pierce-smith.md) |

**Depends on** [scope.md](../../scope.md) · the archived smex spec (git history) ·
[heat balance](../../mechanics/heat-balance.md) · [open hearth](../../machines/open-hearth.md) ·
[reheat furnace](../../machines/reheat-furnace.md) · [roasting](../../processes/roasting.md) ·
[materials.md](../../materials.md) · [Pierce-Smith](pierce-smith.md) ·
[tilting crucible](tilting-crucible.md)

---

## What it is

A long, low, coal-fired furnace with the fire in its own chamber at one end. The flame is drawn over a shallow
hearth and reverberates off the low roof onto the charge; the fuel and the metal never touch. In copper
practice it is the bulk smelting step - ore + flux in one end, matte and slag out the other. The same
silhouette calcines ore, puddles iron and reheats billets, which is why the suite already owns three of it.

---

## Why it is deferred

D8: non-ferrous is later, and the release target is the complete ferrous line. The reasoning, and the
distinction between this deferral and Homestead's, are owned by [scope.md](../../scope.md) § Non-ferrous.

This is not a deferred mechanism. The reverberatory machine is live in the tree three times over (below).
What was deferred is a burden family, a metal, and a product.

---

## What exists today

Nothing copper-side.

| Probe | Result |
|---|---|
| `grep -rniE "coppermatte\|blistercopper\|piercesmith" src/ assets/` | 0 hits (excluding `bin/`) |
| `grep -rni "copper" src/ --include=*.cs` | 4 hits, all vanilla-facing plumbing - enumerated on [tilting crucible](tilting-crucible.md) § What exists today |
| metal defs | 4 files, all ferrous (`mods/iiex/assets/iiex/config/metals/`, `mods/siex/assets/siex/config/metals/`) |
| a copper burden family, ore, matte item or slag variant | none |

The machine class is live, and a copper mode is an override of it rather than a new furnace:

| Piece | Where | What it already does |
|---|---|---|
| the reverberatory itself | `BlockEntityHeatingFurnace.cs:31` | the thinnest furnace in the tree - reverberatory geometry (`#region` at `:33`), plain-fuel firebox read (`:65-89`), and an empty `SmeltCycle` (`:107`) because it melts nothing |
| "no blast" | `BlockEntityFireboxFurnace.cs` | `RequiresBlast => false`, and the drawing marks no `CellRole.Tuyere`/`GasOutlet` - nothing in the blower ecosystem can starve it |
| the puddling variant | `BlockEntityPuddlingFurnace.cs:27` | the same core run as a reverberatory rather than a shaft: the charge walk is redirected (`:9-13`) |
| the work door | `BlockEntityChargeDoor.cs:20` | "a reverberatory furnace's work door", two independent open/shut flags (`:10`) |
| the only air control | `BlockEntityPuddlingChimneyCap.cs:19` | a damper on a lever - a reverberatory has no blower (`:12`) |
| why the core needs no special case | `BlockEntityFurnaceCore.cs:946` | the charge-offset walk is what turns a shaft into a reverberatory |
| the calciner reading | [roasting](../../processes/roasting.md):207 | "a calciner *is* a reverberatory furnace" - the same block, a different mode |

---

## The design as it stands

From the archived smex spec and [materials.md](../../materials.md), consolidated:

| | Specified as |
|---|---|
| Footprint | multiblock |
| Input → output | crushed copper ore + heat → copper matte (+ slag) |
| Mechanic | coal burns in a separate part of the multiblock - the flame plays over the charge, fuel never mixes with it |
| Second job | the copper-side waste-alloy recycler - off-spec ladle mixes recover their base copper here or in the arc furnace ([materials.md](../../materials.md)) |
| Heat model | fuel flame + regenerator, the same `T_in` branch the open hearth uses ([conventions.md](../../conventions.md)) |
| Product downstream | matte → [Pierce-Smith](pierce-smith.md) → converter copper ([materials.md](../../materials.md)) |
| Acid plant | none. The sulfur/sulfuric this line historically threw off would come from a chemistry mod, and the add-on "only ever consumes it, and here only cosmetically" |
| Period | reverberatory copper, 1860s–70s |

The second job is the one worth keeping. [materials.md](../../materials.md) makes waste alloy the universal
punishment for an off-spec ladle mix and then names exactly two copper-side recovery routes - this furnace and
elex's arc furnace. Defer both and the copper half of R2's recovery promise has no machine at all.

---

## What it would unblock

| Waiting on it | Severity | Why |
|---|---|---|
| [**Pierce-Smith converter**](pierce-smith.md) | hard wall | a converter blows matte. No matte, nothing to blow. They are one chain, not two features |
| **Converter copper** ([materials.md](../../materials.md)) | wall | the only route to it runs through both machines |
| **The bronzes** | wall on the base metal | but see the [tilting crucible](tilting-crucible.md): vanilla ships copper, so a bronze recipe is not blocked - the mod's bulk-copper economy is |
| **Copper waste-alloy recovery** | degraded | elex's arc furnace is the stated alternative, and elex is deferred too - so today the route is empty on both sides |
| **elex's copper chain** | degraded, not a wall | elex offers an electric copper smelter that "replaces the reverberatory only" - the Pierce-Smith → blister → electrolysis chain is still required. So elex can skip this machine and cannot skip the next one |

---

## Gotchas

* **The game ships no copper sulphide, and matte is a sulphide.** Vanilla's graded-ore list
  (`survival/worldproperties/block/ore-graded.json`) is `nativecopper, limonite, quartz_nativegold, galena,
  cassiterite, chromite, ilmenite, sphalerite, quartz_nativesilver, galena_nativesilver, bismuthinite,
  magnetite, hematite, malachite, pentlandite, uranium, wolframite, rhodochrosite` - the two copper ores are
  native copper (already metal) and malachite (a carbonate). `grep -rl chalcopyrite` over `survival/` returns
  nothing. A matte-smelting chain therefore assumes a feedstock that does not exist, and the mod adds no ore of
  its own on the copper side. Three ways out, and one has to be chosen before anything is built: ship a
  sulphide ore; smelt malachite directly to metal and delete the matte step (historically what oxide ores did,
  and it deletes [Pierce-Smith](pierce-smith.md) with it); or make matte synthetically by adding sulfur -
  which is where the disputed sulfur dependency comes from.
* **A natural-draught reverberatory lands just below copper.** The worked ledger at
  [open hearth](../../machines/open-hearth.md):261-268 puts a natural-draught pure-fuel reverberatory at
  `T_process` = 1082.5 °C under the shipped constants, against copper's melting point of 1084.62 °C
  (vanilla `worldproperties/block/metal.json`). That is a 2 °C shortfall - i.e. this furnace hits the same
  ceiling as blockers B8 (puddling) and B15 (crucible). Either it is a blown furnace (`T_process` 1645 °C,
  same table) or the stack-height draught function lands first
  ([crucible-furnace](../../machines/crucible-furnace.md):180 proposes it as one fix for all three).
* **"No blast" and "needs blast" would collide.** `BlockEntityHeatingFurnace.cs:47` hard-codes
  `RequiresBlast => false` for the reverberatory family. If copper smelting needs the blown branch above, a
  copper mode cannot simply reuse that override - and a blown reverberatory is arguably a different machine.
* **Do not write a new furnace class.** Every reverberatory in the tree is the same core with a redirected
  charge walk (`BlockEntityFurnaceCore.cs:946`); a copper mode is a burden family, a metal def and a
  `SmeltCycle`, exactly as the cupola is a pure data override of the blast furnace.
* **R2 applies to the slag.** [conventions.md](../../conventions.md) requires every reduction/refining step to
  declare a recovery fraction and make the shortfall a visible, usable by-product. Copper slag has no item, no
  recovery number and no consumer anywhere in the docs.
* **It is not on the release path.** [scope.md](../../scope.md) § How to apply the cut, instruction 1.

---

## Open

1. **Which copper ore, and therefore whether "matte" survives at all** (see Gotchas). This is the decision the
   whole copper add-on hangs on, and it is upstream of both this machine and [Pierce-Smith](pierce-smith.md).
2. **Blown or natural draught** (see Gotchas). Changes the block, the ports and whether the twin-tub blower
   ecosystem reaches this tier.
3. **Process temperature.** No `Copper*` key is proposed anywhere; there is no analogue to
   `BfIronMeltingPoint = 1482` (`IiexConfig.cs:271`) or `CupolaCastIronMeltingPoint = 1200` (`:316`).
4. **Whether the waste-alloy recycler is the same mode or a second one.** [materials.md](../../materials.md)
   gives this furnace two jobs with different inputs; the cupola solves the identical problem with two burden
   families on one block.
5. **Whether the copper add-on ships inside `smex` or as its own project.**
   [overview.md](../../overview.md) leaves that open for every add-on.
6. **Slag: item, rate, consumer.** `iiex:slag` exists and is ferrous; copper slag is neither modelled nor
   named.
