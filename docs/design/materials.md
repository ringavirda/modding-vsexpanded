# Material Catalogue

**Status** timeline + pointers - this page owns the historical timeline and the semi-finished-forms
distinction below, nothing else.

The suite's metals in historical order: which real process each material comes from, when it arrived, and
how the mods map onto that history. See [conventions.md](conventions.md) for the units, R2 (declared
recovery) and R9 (mass is derived from the shape).

This page owns no numbers. Masses are derived, not written: under R9 every mass is `1 vx³ = 2.5 u`
applied to a drawn shape, so the canonical mass table is a generated artifact. Write the shape; the number
follows.

| Looking for | Read |
|---|---|
| the density rule and how to measure a shape | [density-rule](mechanics/density-rule.md) |
| a mass, section or crop point | the owning **item** page - [pig](items/pig.md) · [stock](items/stock.md) · [rolled-parts](items/rolled-parts.md) · [cast-parts](items/cast-parts.md) · [fasteners](items/fasteners.md) · [alloys](items/alloys.md) |
| which metal is for what, what it guarantees, and its target ratio | [alloys](items/alloys.md) |
| mixing by held proportion, off-spec → waste, recarburisation - the ladle mechanic | [ladle](machines/ladle.md) |
| what is live vs designed vs blocked | [STATE.md](../internal/plans/STATE.md) |

---

## Materials

The material catalogue is [items/alloys.md](items/alloys.md)'s metal ladder: every metal, what it
guarantees, which mod owns it and its build status, one row apiece. Target ratios and composition
windows are also that page's. Carbon is set by process while the metallic elements are added by held
proportion, which is [machines/ladle.md](machines/ladle.md)'s mechanic, along with off-spec → waste alloy
and the chill model.

> **Timeline** (correctly ordered): shear steel (piled blister, early 1700s) / crucible steel
> (Huntsman 1740s) / Bessemer steel (1856) / open-hearth / Siemens-Martin steel (1860s–70s) / Hadfield
> (~1882) / reverberatory + electrolytic copper (1870s) + Bessemer-style copper converting (Manhès-David
> ~1880 → Pierce-Smith 1909) / electric arc + HSS (~1900). Cowper hot-blast stoves belong only with the
> hot blast furnace (smex), never cold blast.

### ★★ Crucible steel raises vanilla's tool ceiling, deliberately, and gates nothing *(built 2026-08-21)*

`iiex:cruciblesteel` is the top of the mod's metal ladder and the only metal in it that beats vanilla's own
steel: `MetalToolEmitter`'s preset table tops out at `good` (durability 2600), which is what Bessemer steel
rides, and crucible steel rides the same preset with an explicit **durability 3300** over it. That is a
deliberate raise of the game's tool ceiling, and it is the one thing on this page that changes a number a
player already knew.

★ **Durability alone.** Attack power and mining tier stay at the preset's. Crucible steel's real advantage
was **uniformity** - melted whole in a sealed pot, so no slag stringers and no soft spots - which reads as a
tool that lasts, not one that hits harder or digs deeper. An override that moved mining tier would change
what a player can mine, which is a progression gate; durability is not.

⛔ **It gates nothing.** Wrought-iron and plain steel heads keep working everywhere they worked before, and
nothing in the suite requires a crucible-steel tool. The machine tooling that *would* want it - drill bits,
shear blades, roll sets - has **no durability or wear mechanic at all**
([tooling-wear](mechanics/tooling-wear.md) is ruled and unbuilt), so "crucible-steel heads last longer"
is not expressible yet and was explicitly out of scope.

⛔ **Nothing has been balanced against this.** 3300 is a first number chosen for its ratio to `good`, not a
playtested one.

---

## Semi-finished forms (rolling stock)

The rolling mill's input is a **semi-finished form**, and the line between the tiers is how the form is
made:

- **Wrought bloom = hammer-made.** Wrought iron is never molten, so it cannot be cast - it is consolidated
  by hammering. The helve hammer shingles the puddle balls into a bloom (~180 u; expelled scale → the oxide
  loop) by piling them on the anvil (the vanilla iron-bloom→ingot / stacked-ingots→plate mechanic reused
  wholesale). A 9-pig puddling heat → 18 balls → 9 blooms, a clean batch (see
  [shingling](processes/shingling.md)).
  The bloom is the one generic wrought form: the mill sets the section (flat set → plate, grooved → bar),
  so there is no separate wrought slab/billet. Rolled wrought stock is vanilla iron, so bar off the mill
  is the smith's feedstock - no new material.
- **Steel slab/bloom/billet = cast.** Only molten steel (Bessemer / open-hearth) can be poured into a form,
  via the longcell sand casting - the continuous-casting shortcut, casting the form directly rather than
  rolling a big ingot down. One pour yields 1 slab / 2 blooms / 3 billets (by form size). These are the
  steel line's rolling stock (see [stock](items/stock.md)).

Definitions (textbook), and the rule that makes the form the product gate: the form's cross-section
pre-commits the product family, so a roll set `accepts` only the matching form:

| Form | Section | Rolls into |
|---|---|---|
| **Bloom** | square, large | structural shapes, rails (and → billets) |
| **Billet** | square, small | bars, rods, wire-rod |
| **Slab** | flat, width ≥ 2× thickness | plate, sheet, strip, **skelp** → rolled pipe |

**Rolling stock ≠ cast components.** A `slab` is steel stock to be rolled; a **`castframe`** is a cast-iron
structural member - the I-section machine standard that carries the flywheel, rolling-mill and steam-hammer
frames (poured, never rolled; cast iron's compression strength is what a machine bed wants). The two are
separate shapes. Cast components (castframe, bedplate, cylinder sleeve, cast pipe, grate/door, valve body)
come off the sand-casting stations as near-net parts feeding machine-build recipes; they are not
semi-finished forms.
