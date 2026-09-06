# Iron chutes

**Status** designed, shapes drafted (2026-09-06) - no code   **Mod** iiex
**Since** 2026-09-06

**Owns** - the facts this page is canonical for:

* the decision to ship a wrought iron chute family, and the copper constraint it answers;
* the rule that it is availability and not a tier: same flow rate, same behaviour, only the material changes;
* the family's codes, its reuse of vanilla's class, block entity, rotations and attribute tables;
* the iron chute section and how it reaches the player;
* the art: our own shapes on vanilla's stations, and why they must stay on them.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| Code-first defs, the injection order, goldens, the cost catalogue | [recipes-config](../mechanics/recipes-config.md) |
| Item flow itself - pull, push and accept faces, the tick, the inventory | vanilla `BlockChute` / `BEItemFlow` |
| What feeds and drains from a chute at the machine end | [ore-crusher](ore-crusher.md), and every machine page with a coupled cell |
| Why the bending roller does not make sections | [bending](../processes/bending.md) § scope |
| The suite's material vocabulary and which texture means what | [materials](../materials.md) |

**Depends on** [recipes-config](../mechanics/recipes-config.md) · [ore-crusher](ore-crusher.md)

---

## Role

Vanilla's chute is copper. It is the only material offered, and it is the wrong one to gate bulk materials
handling on.

A chute section costs one copper ingot at the anvil, or one copper plate and two bars of tin or silver
solder in the grid. A chute block costs two to four sections:

| Block | Sections | Copper ingots by the anvil route |
|---|---|---|
| straight | 2 | 2 |
| elbow | 2 | 2 |
| t | 3 | 3 |
| 3way | 3 | 3 |
| cross | 4 | 4 |

A twenty block run is forty ingots of the metal a player spends on early tools, or forty bars of solder.
Copper is the early metal and its deposits are small; iron is the bulk metal of the era this suite covers.
By the time anyone is laying chute runs they are moving ore, not making their first axe.

It is also the odd material out. Bent sheet iron is what ore, coal and grain chutes were actually made
from. Copper ducting is the anomaly.

The machine that makes it acute is the [ore crusher](ore-crusher.md): a throughput block whose whole
purpose is to stop the player clicking, fed and drained by chutes at both ends.

---

## What this is not

**Not a tier.** The iron chute moves items at exactly vanilla's rate, with the same pull, push and accept
faces, the same lockability and the same block entity. Nothing about a run behaves differently. A faster
iron chute would obsolete the copper one and turn a convenience into a ladder, and flow rate is a property
of the system rather than of the sheet it is pressed from.

**Not a replacement.** Vanilla's copper chute stays exactly as it is, craftable from the first copper. It is
the cheaper option early, when copper is what a player has and iron is not.

**Not steel.** One iron line. A third material would add codes and answer nothing.

**Not cast iron.** A chute is thin bent sheet. Cast iron cannot be made that way, and a cast trough is a
different object with a different use.

---

## The family

Five shapes, twenty eight block codes, mirroring vanilla one for one so that the rotation tables, the
attribute tables and the player's muscle memory all carry over:

| Our code | Variants | Vanilla source | Shape |
|---|---|---|---|
| `iiex:chute-{type}-{vertical}-{side}` | elbow, 3way x up, down x 4 sides = 16 | `chute.json` | elbow, 3way |
| `iiex:chute-straight-{side}` | ns, we, ud = 3 | `chute-straight.json` | straight |
| `iiex:chute-t-{side}` | ns, we, ud-n, ud-e, ud-s, ud-w = 6 | `chute-t.json` | t |
| `iiex:chute-cross-{side}` | ns, we, ground = 3 | `chute-cross.json` | cross |

Everything below the code is vanilla's: `class: BlockChute`, `entityclass: ItemFlow`, the `Lockable` and
`WrenchOrientable` behaviours, `pullFacesByType` / `pushFacesByType` / `acceptFromFacesByType`,
`item-flowrate: 1`, `inventoryClassName: chute`, `quantitySlots: 1`, and the open and tumble sounds.

Mixed runs work. Item flow moves a stack into whatever container sits at the neighbouring position, so an
iron elbow in the middle of a copper run is simply the next block along.

---

## The section

The iron chute section is **vanilla's item with iron added to its material list**, not a new item. Two
patch operations in `mods/iiex/assets/iiex/patches/vanilla/`:

| File | Operation |
|---|---|
| `game:itemtypes/resource/chutesection.json` | append `"iron"` to the `material` variant group |
| `game:recipes/smithing/chutesection.json` | allow the `iron` ingot variant, and change the hardcoded `chutesection-copper` output to `chutesection-{metal}` |

Appending a state to an existing variant group is additive: `chutesection-copper` keeps its code and every
existing recipe and save still resolves. **Never do the same to the chute blocks** - a new variant group on
those renames all 28 vanilla codes and breaks every world that has one placed.

The item's texture is already parameterised as `block/metal/sheet/{material}1`, and
`block/metal/sheet/iron1.png` ships with the game, so the iron section needs no art.

**Iron is not soldered.** Vanilla's grid route for a copper section is a plate plus two solder bars under a
soldering iron. That is right for copper and wrong for iron, which is riveted or folded hot. The iron
section comes off the anvil. If a grid route is wanted later for convenience, it is a plate and a hammer,
never solder.

---

## Recipes

Vanilla's five patterns, taking `chutesection-iron` and yielding our blocks. Same shapes, same counts, so a
player who knows the copper patterns knows these.

The cost catalogue derives the rest ([recipes-config](../mechanics/recipes-config.md)).

---

## Art

Our own shapes, in `workbench/shapes/networks/chute/chute-block-iron-{straight,elbow,t,cross,3way}.json`,
built by `builders/vsexpanded/chutes.py` in the tools repo.

**They sit on vanilla's stations exactly**, and must keep doing so: an 8 x 8 square trough with a 6 x 6
bore, its axis on the block centre line, authored in vanilla's base orientation (a straight along x, an
elbow arriving from above and leaving west, a t and a cross along x opening upward, a 3way leaving west and
south from a top inlet). That is what lets an iron block and a copper block meet in one run without a step
in the duct, and what lets the defs carry vanilla's rotation tables unchanged.

What makes them iron rather than copper: the body is sheet (`iron4`) instead of soldered copper, and every
opening that lands on a block face wears a riveted strap (`iron3`) set 1.5 in from that face, the way bent
sheet duct is lapped and joined. Cube counts run from 12 for a straight to 34 for a cross, against vanilla's
4 and 14; the straps are one constant in the builder if that proves too busy in a long run.

The section item keeps vanilla's shape. The straps belong to the assembled fitting, not to the pressed
blank.

---

## Implementation notes

* Code-first `ExBlockDef`s in iiex, one provider class for the family with the attribute tables shared
  between the four defs.
* ⛔ **The wrench base code must carry our domain.** Vanilla declares
  `WrenchOrientable { baseCode: "chute-{type}" }`. Left bare in our def, a wrench would rotate a player's
  iron chute into a copper one. It must resolve inside `iiex`.
* `AllowedOrientations` is derived from the def rather than restated ([recipes-config](../mechanics/recipes-config.md)).
* Lang rows for the new codes, and the released-codes manifest and referenced-codes guard both need them.
* Tests: golden def parity for the family, and one flow test placing an iron chute between two copper ones
  to pin that mixed runs move items.

---

## Open

1. **Whether the straps survive contact with a long run.** They read well one block at a time. If fifty of
   them in a line look busy, drop `BAND` in the builder and the family becomes vanilla's geometry in iron
   sheet, which is still a clear tier read.
2. **Whether a grid route for the section is wanted**, plate and hammer, alongside the anvil route.
