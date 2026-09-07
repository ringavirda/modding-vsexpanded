# Pierce-Smith converter

**Status** deferred - no copper mode, no matte item, no blister-copper metal def, no recipe, no lang key,
no test. The vessel it is specified to reuse is fully live.
**Would live in** the Copper add-on, parent `smex` ([overview.md](../../overview.md)); specified in the
archived smex spec under a header that marked itself unscheduled.
**Deferred by** D8 - non-ferrous is later ([STATE.md](../../../../../docs/plans/STATE.md)); the cut is owned by
[scope.md](../../scope.md) § Non-ferrous.

**Owns** - the facts this page is canonical for:

* the converter's specified form as a mode, not a machine, and a cost accounting of what that mode takes
  against the shipped `BlockEntityConverterControl`;
* the species mismatch: Bessemer converting tracks carbon, copper converting oxidises iron and sulphur, so
  the reusable part is the vessel, not the process model;
* the two-stage copper blow's mapping onto the vessel's two existing tilt states;
* the sulfur dependency and its disputed severity - the evidence, offered to
  [scope.md](../../scope.md)'s open question rather than ruling on it.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| The non-ferrous cut, and the elex chemistry-dependency severities | [scope.md](../../scope.md) |
| The Bessemer vessel - its blow, capacity, tilt states, MP-for-tilt rule, drops and tests | [Bessemer](../../machines/bessemer.md) |
| Where the matte comes from, and the ore that does not exist | [copper reverberatory](copper-reverberatory.md) |
| Converter copper, pure copper, and the historical timeline | [materials.md](../../materials.md) |
| Roasting as a process, and the absence of any sulphur chemistry in this mod | [roasting](../../processes/roasting.md):47, :69-71 |
| elex's electrolysis cell, its electrolyte and the alternator gate | [electrolysis cell](../elex/electrolysis-cell.md) |

**Depends on** [scope.md](../../scope.md) · [Bessemer](../../machines/bessemer.md) ·
[copper reverberatory](copper-reverberatory.md) · the archived smex spec (git history) ·
[materials.md](../../materials.md) · [ladle](../../machines/ladle.md) ·
[tilting crucible](tilting-crucible.md)

---

## What it is

A horizontal cylinder lined with refractory, blown through a row of tuyeres along its side rather than
through its bottom, and rotated on rollers so the tuyeres swing clear of the bath for charging and pouring.
It converts copper matte to blister copper in two stages with a slag skim between them. Manhès-David ~1880,
Pierce-Smith 1909; the same idea as the Bessemer, which is why the design reuses that vessel.

---

## Why it is deferred

D8: non-ferrous is later, and the release target is the complete ferrous line; the reasoning is owned by
[scope.md](../../scope.md) § Non-ferrous. The defer is doubled - even with D8 reversed the machine could not
be built, because [copper reverberatory](copper-reverberatory.md) has to make the matte first and that page's
ore question is unanswered.

---

## What exists today

Nothing copper-side; everything vessel-side.

| Probe | Result |
|---|---|
| `grep -rniE "piercesmith\|coppermatte\|blistercopper" src/ assets/` | 0 hits (excluding `bin/`) |
| `grep -rni "copper" src/ --include=*.cs` | 4 hits, all vanilla-facing plumbing - enumerated on [tilting crucible](tilting-crucible.md) § What exists today |
| a matte or blister-copper metal def | none; the 4 defs on disk are ferrous |

The vessel is live and tested ([Bessemer](../../machines/bessemer.md):505-514, :525-539):

| Piece | Where |
|---|---|
| `BlockEntityConverterControl` - charge, blow, carbon, tilt states, solidify latch, block-info readout | `…/Converter/BlockEntities/BlockEntityConverterControl.cs:44` |
| `ConverterOpState` - the four-state tilt machine | `…/Converter/ConverterTypes.cs:10` |
| `BlockConverterBessemer` / `BlockConverterControl` / `BlockConverterIntake` / `BlockConverterTransmission` | `…/Converter/Blocks/` |
| MP tilt drive | `BEBehaviorMPConverterTransmission.cs:17` (resistance 0.25) |
| the metal tokens, already indirected | `BlockEntityConverterControl.cs:95-99` - `PigCode` / `SteelCode` / `IronCode` / `SlagCode`, each `MetalRegistry.MoltenItemOf(shortCode)` |
| tests | `ConverterRig` builds the real footprint (`SteelPlantScenes.cs:31`); nine test files cover blow, pours, over-blow, mass conservation, chisel gates, orientation and goldens |

---

## The design as it stands

From the archived smex spec:

| | Specified as |
|---|---|
| Footprint | multiblock (planned, reuses Bessemer) |
| Input → output | copper matte + pressurized air (+ MP tilt) → blister / molten copper |
| Mechanic | a side-blown converter, implemented as a copper-matte mode of the built Bessemer vessel, not a new machine |
| Product | converter copper, ~98 % Cu; impure rod/wire stock ([materials.md](../../materials.md)) |
| Alloys downstream | mixed in the ladle by held proportion, same as the ferrous alloys |

### What the mode actually costs - verified against the shipped code

[Bessemer](../../machines/bessemer.md):518-520 estimates it as "swap the four metal tokens at
`BlockEntityConverterControl.cs:95-99` and the carbon bands", and its own Open #6 (:654-656) qualifies that:
the tokens are indirected through `MetalRegistry`, the carbon bands are not. The estimate is right about the
tokens and understates the process model:

| Element | Reuse? | Why |
|---|---|---|
| vessel, shell, mirror, fillers, drops, chisel recovery | free | metal-agnostic already |
| four tilt states + their modifier-key verbs | free | `ConverterOpState`, `ConverterTypes.cs:10` |
| MP tilt drive, intake port, blast draw | free | [Bessemer](../../machines/bessemer.md):360-365 - power is for tilting, not for blowing |
| the four metal tokens | cheap | `MetalRegistry.MoltenItemOf(...)` at `:95-99`; three of the four map (matte → blister → slag) |
| the over-blow token (`IronCode`, `:98`) | no counterpart | the Bessemer's over-blow product is soft ingot iron; over-blown copper has no third product, the historical answer is to stop |
| the tracked species (`_carbon`, `:113`) | wrong quantity | a Bessemer blow oxidises the pig's own C and Si. A copper blow oxidises iron and sulphur out of the matte. Not a re-band of the carbon curve: a different state variable with a different endpoint |

The two-stage blow already has its two states. Copper converting is a slag blow (iron out, fluxed and
skimmed) followed by a copper blow (sulphur out, blister poured). The vessel has a shallow tilt that skims
floating slag (`SlagPouring`, Sprint+RMB, [Bessemer](../../machines/bessemer.md):270) and a deep tilt that
pours the product beneath it (`SteelPouring`, :271). The verb sequence charge → blow → skim → blow → pour
needs no new interaction.

### The sulfur dependency, and what has not been checked

[scope.md](../../scope.md) records this as a disputed severity and an open question: one reading rates
"sulfur for the copper add-on's roasting" as having no fallback, while the archived smex spec said the
add-on "only ever consumes it, and here only cosmetically". That ruling is scope.md's to make. The evidence,
uncited on either side:

| Evidence | Where | Bearing |
|---|---|---|
| `sulfur` is a vanilla ore - an ungraded ore variant with its own worldgen deposit | `survival/worldproperties/block/ore-ungraded.json`; `survival/worldgen/deposits/mineralore/sulfur.json` | sulfur is mined, not synthesised; it needs no chemistry mod |
| `game:powder-sulfur` is a vanilla item, a `powder` variant beside charcoal, flint, borax, sylvite | `survival/itemtypes/resource/crushed/powder.json` | a usable ingredient exists today |
| Vanilla already ships sulfuric acid: `acid-full-sulfuric`, cooked from 1 L water + 1 `saltpeter` + 2 `powder-sulfur` | `survival/recipes/cooking/acid.json` | see Gotchas - this bears on more than copper |
| The specified copper chain has no roasting step. The add-on's three machines are the reverberatory (crushed ore → matte), this converter, and zinc retorts | the archived smex spec | the no-fallback rating rates a step that is not in the design |
| This mod models no sulphur chemistry and there is no sulphide ore | [roasting](../../processes/roasting.md):47, :69-71 | roasting here is an oxide/fuel lever, not a desulphurising one |

This page's reading, offered to that open question and not as a ruling: the no-fallback severity looks
over-rated, and the real dependency is one page upstream - the ore, not the sulfur
([copper reverberatory](copper-reverberatory.md) § Gotchas).

---

## What it would unblock

| Waiting on it | Severity | Why |
|---|---|---|
| Converter copper ([materials.md](../../materials.md)) | wall | it is the only producer; the reverberatory makes matte, not metal |
| elex's electrolysis cell → pure copper ([electrolysis cell](../elex/electrolysis-cell.md)) | wall | the cell refines an impure copper anode; there is nothing to hang on it without this machine |
| elex's alternators, and therefore AC | wall | pure copper is the stated gate |
| elex's DC-only subset | degraded | [scope.md](../../scope.md) records that subset as running on impure copper wire, which still comes from here |
| The bronzes' base metal | degraded | vanilla copper exists; what is missing is the mod's bulk route |
| elex's electric copper smelter | does not help | it "replaces the reverberatory only"; the Pierce-Smith → blister → electrolysis chain is still required |

Of the three deferred non-ferrous machines this has the longest downstream shadow: every copper consumer in
the suite, including elex's whole AC half, passes through it.

---

## Gotchas

* "Reuses the Bessemer" is true of the block and not of the process (see the cost table above). Scheduling
  this from the spec's parenthetical (planned, reuses Bessemer) sizes it as a data override and misses a
  second state variable, a second endpoint and a missing third product.
* It is blocked upstream, not by its own difficulty: no matte, no converter, and the matte question is an ore
  question the game does not currently answer ([copper reverberatory](copper-reverberatory.md)). Reversing D8
  alone would not make this buildable.
* The vanilla acid recipe bears on elex, not just copper. `survival/recipes/cooking/acid.json` produces
  `acid-full-sulfuric` from two vanilla resources in a vanilla cooking pot.
  [scope.md](../../scope.md) argues elex's electrolyte is "plausibly one recipe or one small block" from
  vanilla sulfur and saltpetre; that recipe already exists in the base game. The ruling stays scope.md's.
* Side-blown is cosmetic under the mode model. The shipped intake is a single fixed connector port
  (`BlockConverterIntake`, [Bessemer](../../machines/bessemer.md):509); a row of side tuyeres is a shape
  change, not a mechanic. A silhouette that must read as Pierce-Smith is new art, also deferred.
* Rotation vs tilt. A Pierce-Smith rotates on rollers where a Bessemer tilts on trunnions. The four tilt
  states cover the same verbs; do not add a fifth for "roll out of blast" - that is `Normal`.
* R2 applies to the slag blow. Converter slag is a declared by-product, not a loss
  ([conventions.md](../../conventions.md)). Nothing names a copper slag item.
* The Bessemer's own open issues come along with the mode. Its layout is the one structure in the suite
  that cannot be read off the layouts workbench ([Bessemer](../../machines/bessemer.md):645-647), and its
  blow length is charge-independent (:649-652). A copper mode inherits both.
* It is not on the release path. [scope.md](../../scope.md) § How to apply the cut, instruction 1.

---

## Open

1. Whether matte exists at all - the upstream ore question
   ([copper reverberatory](copper-reverberatory.md) § Open 1). If malachite is smelted directly to metal, this
   machine has no job and the copper add-on loses a third of itself.
2. What replaces `_carbon`. A sulphur/iron fraction, a two-phase timer, or a staged blow with no tracked
   species at all. The last is cheapest and still reads correctly: the player's decision is when to skim and
   when to stop.
3. What over-blowing does. The Bessemer's third product (soft ingot iron) has no copper counterpart. An
   over-blown copper heat could yield an off-spec metal recoverable in the reverberatory, reusing
   [materials.md](../../materials.md)'s waste-alloy machinery instead of a new punishment.
4. Whether the copper mode is a variant block, a burden-family switch, or a lining item. The mode has to be
   selectable in-world and legible under R7 ([conventions.md](../../conventions.md)); nothing says how.
5. Capacity and rate. D4 settled the ferrous converter at 6000 u ([STATE.md](../../../../../docs/plans/STATE.md)); no
   copper number is proposed, and a copper heat has no natural "2 slab pours / 3 bloom pours" anchor to size
   against.
6. Blister copper as a material. [materials.md](../../materials.md) names converter copper at ~98 % Cu but no
   `blister` stage; the design collapses blister and converter copper into one item without saying so.
