# Tooling wear

**Status** ruled 2026-08-05 - nothing built  **Mods** exlib owns the mechanism; iwex, lpex, hpex and smex own the tooling
**Since** 2026-08-05

**Owns** - the facts this page is canonical for:

* whether machine tooling wears at all - and it does
* the one rule that covers dies, roll sets, blade sets, boring bits and patterns
* what sets a piece of tooling's life, and therefore what crucible steel buys

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| What each tooling item is, and its recipe | [dies.md](../items/dies.md) · [roll-sets.md](../items/roll-sets.md) · [patterns](../machines/casting-bed.md) |
| The boring machine's bit-material capability gate | [boring-machine.md](../machines/boring-machine.md) |
| Crucible steel's production route | [crucible-furnace.md](../machines/crucible-furnace.md) |
| The metal grades themselves | [materials.md](../materials.md) |

---

## 1. The rule

Machine tooling wears. One rule, one mechanism, across the whole family - dies, roll sets, shear blade
sets, boring bits and casting patterns. The metal it is made of sets its life.

| | |
|---|---|
| **what wears** | anything that is fitted to a machine and does work on stock |
| **what sets the life** | the grade of the metal the tooling is made from - wrought < cast < crucible steel |
| **what wearing out does** | the piece is spent and must be re-made; it does not degrade the product |
| **what does not wear** | the machine itself. A mill, a shear, a bench - the frame is permanent; the tooling in it is not |

The 24-impression pattern durability already shipped in `PatternItemDefinitions.cs` is the family's first
instance of the rule.

## 2. Why - the argument for crucible steel

`STATE.md` D9 sells crucible steel on "longer-lasting machine heads"; wear is what gives that branch a
consumer. Historically dies, rolls and shear blades were consumable shop stock, a standing cost every
works carried, and better steel meant fewer re-grinds and fewer changes.

## 3. What it costs

Tooling is a standing operating cost, which the mod has nowhere else today. A die or a roll set stops
being a one-time purchase and becomes something the player re-buys; the forming line therefore has a
reason to keep a pattern shop running past the first build, and a works has a consumables budget.

Lives are sized so re-making tooling is an occasional errand, never a per-heat chore. The failure mode to
design against is the player standing at a bench re-cutting dies instead of running the factory.

## 4. Capability is a separate axis

[boring-machine.md](../machines/boring-machine.md)'s rule - bit material gates the hardest metal it can
machine - is untouched by this page. Grade does two things, and they do not collapse into each other:

| axis | what grade decides |
|---|---|
| **capability** | whether the tooling can work a given metal at all |
| **life** | how long it lasts doing it |

A wrought bit cannot cut steel however new it is; a crucible-steel bit cuts it and keeps cutting it.

## 5. Open

* **The numbers.** Lives per grade, per tooling family. All calibration - none of it changes this rule.
* **Re-make vs re-grind.** Whether a spent tool is destroyed or returns a re-workable blank (the latter is
  kinder to the mass ledger and matches a real shop's re-grinding).
* **Readout.** How a player sees remaining life. Vanilla durability is the obvious carrier for held items;
  a roll set fitted to a stand has no held-item slot to show it on.
* **Whether the wear counter is per-item or per-machine-fitting** - i.e. does a roll set carry its own life, or
  does the stand track the life of what is in it?
