# Tilting crucible

**Status** deferred - nothing exists: no block, no BE, no item, no shape, no lang key, no metal def, no
config key, no test.
**Would live in** unassigned. Its consumers are the copper add-on (parent `smex`,
[overview.md](../../overview.md):80); its interface is the iiex-owned molten canal, and no doc places it in
a mod. See § Open.
**Deferred by** D8 - non-ferrous is later ([STATE.md](../../../../../docs/plans/STATE.md)); the cut and its reasoning
are owned by [scope.md](../../scope.md) § Non-ferrous.

**Owns** - the facts this page is canonical for:

* the machine's job: the molten canal's entry point for small metals, the tilt as that interface, and which
  existing code path it attaches to;
* the contrast with the in-scope [draft crucible furnace](../../machines/crucible-furnace.md), and the
  physical reason the two machines differ;
* the cast-iron vessel's temperature margin against this mod's own metal catalogue;
* what a non-ferrous pour already does today;
* the design settled 2026-07-29 and the earlier same-day ruling it superseded.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| The non-ferrous cut, that it is a different deferral from Homestead's, and what deferring it costs R3 | [scope.md](../../scope.md) § Non-ferrous |
| The ferrous crucible furnace - form, charge, pot problem, draught arithmetic | [crucible-furnace](../../machines/crucible-furnace.md) |
| R3, the ladle, its merge, the chill model and the zinc coke cover | [conventions.md](../../conventions.md):38-40, [ladle](../../machines/ladle.md) |
| The molten flow driver, per-cell capacities, `IsFlowSource`, the type refusal | [molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) |
| The bronzes' compositions and the waste-alloy rule | [alloys](../../items/alloys.md) · [ladle](../../machines/ladle.md) |
| Which alloys are inside the release target | [alloying](../../processes/alloying.md):283-290 |

**Depends on** [scope.md](../../scope.md) · [crucible-furnace](../../machines/crucible-furnace.md) ·
[ladle](../../machines/ladle.md) · [molten canal](../../machines/molten-canal.md) ·
[molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) · [materials.md](../../materials.md) ·
[alloying](../../processes/alloying.md) · [copper reverberatory](copper-reverberatory.md) ·
[Pierce-Smith](pierce-smith.md)

---

## What it is

A small brick chamber with a coal fire under a cast-iron pot, the whole vessel hung on trunnions so it tips
to pour. The jobbing brass-and-bronze foundry's workhorse: charge scrap and ingot, melt, tilt, pour. The pot
is cheap iron because the metals it holds melt far below iron.

---

## Why it is deferred

D8: non-ferrous is later, and the release target is the complete ferrous line. The decision, the two
different deferrals (Homestead vs non-ferrous), and the accounting of what the non-ferrous cut costs are
owned by [scope.md](../../scope.md) § Non-ferrous, including that R3's ladle-merge has nothing to merge
until this machine lands.

---

## What exists today

Nothing. Not a stub, not a name.

| Probe | Result |
|---|---|
| `grep -rniE "tiltingcrucible\|coppermatte\|blistercopper\|zincretort" src/ assets/` | 0 hits (excluding `bin/`, where the matches are the substring "matters") |
| `grep -rni "copper" src/ --include=*.cs` | 4 hits, all vanilla-facing plumbing: `MetalCatalogueLoader.cs:107` (a comment about worldproperty codes), `MetalToolEmitter.cs:223`, `:343` (vanilla shape paths), `IiexConfig.cs:104` (a comment naming copper's 1084 °C melting point) |
| `grep -niE "copper\|bronze\|brass\|zinc" assets/*/lang/en.json` | 2 hits, both about a texture: a copper-textured rim on the pressure valve (`mods/siex/assets/siex/lang/en.json:171`, `mods/iiex/assets/iiex/lang/en.json:133`) |
| metal defs on disk | 4 files, all ferrous: `mods/iiex/assets/iiex/config/metals/{castiron,pigiron,slag}.json`, `mods/siex/assets/siex/config/metals/bessemersteel.json` |
| editable or runtime shape | none |

Two design docs name it as a dependency: [alloying](../../processes/alloying.md):290 lists "tilting
crucible *(designed)*" as the element source for all four bronzes, and
[crucible-furnace](../../machines/crucible-furnace.md):223, :240 twice say "this is not the tilting
crucible".

---

## The design as it stands

Settled 2026-07-29.

### The two crucible machines, and why they differ

| | [Draft crucible furnace](../../machines/crucible-furnace.md) - in scope | Tilting crucible - deferred |
|---|---|---|
| Form | melting holes below floor level, ash pit + grate beneath, flue at the bottom, tall stack | a cast-iron vessel in a small chamber, coal below, no stack |
| Draught | natural, from stack height alone | natural, minimal |
| Reaches | ~1600 °C | tin 232 · lead 327 · zinc 419 · copper 1085 |
| Charge held in | a refractory/fireclay pot - a new item, because fired clay is capped at 1200 °C | the cast-iron vessel itself |
| Pour | the player lifts the pot with tongs | the machine tilts |
| Product | crucible steel | bronze, brass, and small metals into the canal |

> Chimney height is the melting point. Crucible steel needs ~1600 °C out of natural draught with no blast at
> all; bronze needs ~950 °C, which a small chamber with coal under it reaches easily, so it needs no stack.

The tier gate follows from the same numbers. A cast-iron vessel cannot hold molten steel: this mod's
catalogue gives `castiron` a melting point of 1200 °C (`mods/iiex/assets/iiex/config/metals/castiron.json`) against
iron's 1538 °C (vanilla `worldproperties/block/metal.json`). The tilting machine is incapable of the ferrous
job by construction, not by rule - the same argument
[crucible-furnace](../../machines/crucible-furnace.md):128 makes in reverse about its own pot.

### The tilt is the canal interface

The molten network's producers today are the big ferrous furnaces, so at machine scale it carries iron and
nothing else. The tilting crucible is to tin and copper what the blast furnace's tap is to pig iron: a
hand-carried pot pours a mold, a tilting vessel pours a run.

The code path already exists and needs nothing new. The furnace tap is not a molten-graph node - it finds
the canal start by a fixed offset and calls `ILiquidMetalSink` on it
([molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md):276):

| Step | Existing implementation |
|---|---|
| find the receiving cell | `BlockEntityMoltenMetalTap.TryPourMetal` walks `facing.Opposite` + down (`:168`, offset at `:177`) |
| ask before pouring | `CanReceiveOrSoak` - the looser predicate, so a brim-full start soaks heat instead of plugging (`BlockEntityMoltenCanalStart.cs:76-78`) |
| pour | `ReceiveLiquidMetal(stack, ref amount, temperature)` (`:87-130`); `amount` comes back as the leftover, not the consumed amount (`BlockEntityMoltenMetalTap.cs:192-197`) |
| the receiving cell | `BlockEntityMoltenCanalStart` - `IsFlowSource => true` (`:28`), capacity ×2 = 100 u (`:24-25`) |

So the machine is a non-node producer beside a canal start, exactly like the tap. It never joins the molten
graph and inherits none of the graph's ordering questions.

### Charge - the metals, with vanilla's own numbers

Vanilla ships every one of these as a metal with a declared melting point
(`worldproperties/block/metal.json`):

| Metal | Melt °C | Boil °C | Note |
|---|---|---|---|
| tin | 231.93 | 2602 | |
| lead | 327.46 | 1749 | non-ferrous itself, and no other machine in the suite wants it |
| zinc | 419 | 906 | see the coke cover, below |
| copper | 1084.62 | 2562 | the hot end of the machine's range |
| tin bronze | 950 | 2300 | |
| brass | 920 | 1100 | vanilla models brass as boiling at 1100 |
| black bronze | 1020 | 2300 | |
| bismuth bronze | 850 | 2300 | |

The coke cover is arithmetic: vanilla puts zinc's boiling point (906 °C) below copper's melting point
(1084.62 °C), so any bath hot enough to hold copper is hot enough to boil the zinc out of it. The rule
itself, and its consequence for the ladle's bath state, belong to [ladle](../../machines/ladle.md):247-252.

### What was superseded

Two rulings were written on 2026-07-29; only the second is the design.

| Earlier ruling | Later ruling - the design |
|---|---|
| one crucible furnace, tilting, two eras - the isolating pot is the defining property in both cases, so one machine serves crucible steel now and non-ferrous later | two machines: the melting point is why. Natural draught to 1600 °C demands a tall stack; 950 °C does not |

[crucible-furnace](../../machines/crucible-furnace.md) is written against the later ruling, which is why
that page's machine does not tilt.

---

## What it would unblock

| Waiting on it | Severity | Why |
|---|---|---|
| R3's merge having a second metal to merge | degraded, not a wall | the ladle can ship and merge ferroalloys on an iron-only network; the mechanic stays underused. Full accounting in [scope.md](../../scope.md) § Non-ferrous |
| The four bronzes ([alloys](../../items/alloys.md)) | wall - for the mod's alloy system | [alloying](../../processes/alloying.md):290 names this machine as their only element source. Players are not walled: vanilla's own crucible → clay-mold bronze route is untouched |
| Brass, and therefore gauges ([alloys](../../items/alloys.md)) | wall | and it needs the coke cover on top |
| The [copper reverberatory](copper-reverberatory.md) and [Pierce-Smith](pierce-smith.md) chain | not blocked by this | that chain makes copper; this machine only re-melts and pours it. Independent deferrals that share a tier |
| elex's impure copper wire | not blocked by this | elex takes converter copper, not a re-melt ([electrical grid](../elex/electrical-grid.md) § Gotchas) |

Nothing in the release target waits on this machine.

---

## Gotchas

* The canal is not iron-only by code, and the design docs read as if it were.
  `BlockEntityMoltenCanalStart.CanReceive` gates on exactly two things - not solidified, and either empty or
  holding the same metal code (`:66-69`) - with no ferrous check anywhere. And
  `BlockMoltenCanalStart.OnLoaded` already caches every vanilla `crucible-*-smelted` block (`:57-71`) to
  advertise the pour in interaction help (`:148`), with the comment at `:76-77` stating that vanilla
  `BlockSmeltedContainer` (a smelted crucible) pours its molten metal into the canal network here. A player
  can hand-pour molten bronze into a canal start today. What is missing is a machine-scale, repeatable
  non-ferrous feed.
* The vessel margin is thin at the copper end, by this mod's own numbers. `castiron` melts at 1200 °C
  (`mods/iiex/assets/iiex/config/metals/castiron.json`); copper melts at 1084.62 °C. That is 115 °C of headroom, and
  the fire has to be hotter than the charge, not equal to it. Tin, lead and zinc (232 / 327 / 419) are
  comfortable; copper is not. Either the copper charge sits in a clay/refractory pot inside the cast-iron
  chamber, or `castiron`'s melting point moves, or the machine's top metal is bronze rather than pure
  copper. Decide before drawing it - the answer changes the silhouette.
* An unregistered metal survives the canal by convention, not by design. With no `MetalDef`, thresholds fall
  back to the globals - `MetalLiquidThreshold = 0.8`, `MetalHardenedThreshold = 0.3` (`ExlibConfig.cs:63`,
  `:67`) - and recovery uses the convention `ingot-X → metalbit-X` in the same domain
  (`MetalRegistry.cs:86-89`), which vanilla happens to satisfy because `metalbit` loads its variants from the
  `block/metal` worldproperty. So frozen copper chisels back to `game:metalbit-copper` and R2 holds by luck.
  If `SolidDropOf` ever failed to resolve, the fallback is `MetalRecoveryFallback = "iiex:slag"`
  (`ExlibConfig.cs:75`) - the player's copper becomes slag. Write the metal defs.
* The canal is not needed to cast bronze, only to merge it. `ClayMoldHeatCeiling = 1100 °C`
  (`IiexConfig.cs:65`, gate at `ClayHeatGate.cs:31-32`, "clay is bronze max" at `ClayHeatGate.cs:8`) already
  admits every metal in the table above, copper included, by 16 °C at worst. The tilting crucible's value is
  the run, not the pour.
* Do not give it a stack. A tall chimney on a bronze furnace is decoration, and it would make the two
  machines look alike again.
* Do not reuse the ferrous pot item. [crucible-furnace](../../machines/crucible-furnace.md):240 notes this
  machine shares its pot problem in reverse - that page needs a refractory pot because clay cannot reach
  1600 °C; this one needs to decide whether it has a pot at all.
* Lead has no consumer in this suite. It appears in the metal list because a cast-iron pot can melt it, not
  because anything wants it. [scope.md](../../scope.md) notes lead is itself deferred; adding it here would
  create a metal with one producer and no customer.
* It is not on the release path. Before proposing it, read [scope.md](../../scope.md) § How to apply the
  cut - the first instruction is "don't".

---

## Open

1. Which mod owns it. [overview.md](../../overview.md):80's Copper add-on row lists reverberatory,
   Pierce-Smith and zinc retorts, not this machine; :79's Crucible/iiex row lists only the crucible-steel
   furnace. It is in neither table. Its consumers are smex-copper, its interface is iiex's canal, and its
   sibling is an iiex machine. Nobody has chosen, and the choice affects whether an iiex-only player can ever
   build it.
2. Whether the vessel is cast iron all the way to copper (see Gotchas). This is the one question that changes
   the machine's appearance, its build recipe and its top metal.
3. The metal defs. `copper`, `tin`, `zinc`, `bismuth`, and the four bronzes have no
   `assets/*/config/metals/*.json` entry, so they have no molten item, no `solidDrop`, no cast domain and no
   per-metal thresholds. Nothing non-ferrous can enter the canal deliberately until they exist.
4. Producer-node or adjacent-cell pourer. The tap idiom (above) is the cheap answer and the one the design
   assumes; a real graph node would be a change to [molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md).
5. What drives the tilt - a lever, or mechanical power as the [Bessemer](../../machines/bessemer.md):101
   uses it. The Bessemer's precedent is MP-for-tilt-only; a hand lever costs nothing and matches the
   machine's scale.
6. Whether vanilla's crucible route is retired, coexists, or is the tier below it. The suite's pattern is
   that nothing is obsoleted, which argues for coexistence - hand crucible for one mold, tilting crucible
   for a run.
7. Throughput and capacity. No number is proposed anywhere. The canal start holds 100 u
   (`BlockEntityMoltenCanalStart.cs:24-25`) and the network moves 50 u/s end to end (D5b,
   [STATE.md](../../../../../docs/plans/STATE.md)); nothing says what one tilt is worth.
