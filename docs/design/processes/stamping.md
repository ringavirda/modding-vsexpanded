# Stamping (blanking)

**Status** designed - nothing built. No steam hammer, no die item, no `ItemDie` spec, no `boilerplate`
item, and no recipe of any kind. A repo-wide grep for `hammer` in `src/` returns only vanilla tool items.
**Mods** lpex (the [steam hammer](../machines/steam-hammer.md) and the plate die) · hpex (the
double-action hammer, for work ≥ 2 voxels thick) · iwex / smex supply the plate

## Owns

* the loop and its yield: one `boilerplate` → 3 × `game:metalplate`, and the arithmetic that makes
  that exact by mass and 3 vx³ over by volume;
* the blanking-is-not-shearing argument as a process, stated in geometry rather than as a rule;
* the role stamping plays in the wide route;
* the operations ledger - boilerplate per schedule, stamps per schedule, plates out - for all three
  rolling routes;
* the rule that a die may only re-form metal it is given: no die may mint units, and the
  three-plates-from-one-strip figure is the ceiling every future die is measured against.

## Does not own — cited only, never restated

| Fact | Owner |
|---|---|
| the machine - its footprint, the LP ram, the docked anvil, the four states, the die catalogue, batch size = die cavity count, the LP ≤ 1 vx / HP ≥ 2 vx tier gate, the staged work-item renderer, every steam number | [steam hammer](../machines/steam-hammer.md) |
| the `ItemDie` tooling contract - what a die spec carries and how it is fitted | [heading machine](../machines/heading-machine.md) |
| shearing - the crop station, `Outputs`-on-stage, the cold-cut torque gate, crop-not-convert, and the shear-cuts-across / die-cuts-out split as a rule | [shear](../machines/shear.md) |
| where the `boilerplate` comes from: the schedules, the feed counts, the crop points, and the narrow-vs-wide feed trade | [rolling](rolling.md), [wide hall](../machines/wide-hall.md) |
| `1 vx³ = 2.5 u`, and why `game:metalplate` is 200 u | [density rule](../mechanics/density-rule.md) |
| the ≤ 32 / ≤ 48 handling invariant | [recoverability](../mechanics/recoverability.md) |
| dies for fasteners - nails, bolts, rivets, bearing balls - which are a different bench and a different verb | [nail machine](../machines/nail-machine.md), [heading machine](../machines/heading-machine.md) |
| the HP double-action hammer | [HP hammer](../machines/hp-hammer.md) |
| code-first defs, RCC stages, the cost catalogue | [recipes & config](../mechanics/recipes-config.md) |

**Depends on** [steam hammer](../machines/steam-hammer.md) · [rolling](rolling.md) ·
[shear](../machines/shear.md) · [density rule](../mechanics/density-rule.md) ·
[heading machine](../machines/heading-machine.md)

---

## What it is

Die work under a hammer. A strip of hot plate is laid on the bottom die, the ram falls, and the top die
drives a shaped piece out of it. The die imparts geometry, which is forging work.

What the mod abstracts away:

| Real practice | Here | Why |
|---|---|---|
| a separate blanking press, a punching machine, a drop stamp and a hammer | one machine, many dies | the die is the extension point; more uses for the wide route means more dies, not another megablock |
| scrap skeleton left behind by a blanking die | nothing left over - the yield is exact by mass | crop-not-convert already covers remainders at the shear |
| draw depth, blank-holder force, springback | one blow, one product | thickness is the only geometric axis the work item has |
| die wear and re-sinking | no durability | undesigned; see [Open](#open) |
| hot vs cold blanking | not decided | see [Open](#open) - the design names no temperature gate on this machine |

### Why it is blanking and not shearing

The verb split belongs to the [shear](../machines/shear.md):

> The shear cuts *across*, the die cuts *out*.

The arithmetic that forces it here:

| Quantity | Value |
|---|---|
| `boilerplate` | 15 × 1 × 16 = 240 vx³ |
| `game:metalplate` | 9 × 1 × 9 = 81 vx³ |
| three of them | 243 vx³ |

1. 15 × 16 does not partition into 9 × 9. Two 9-wide strips do not fit across a 15-wide plate, so no
   sequence of straight cuts across the piece can produce even two plates, let alone three.
2. The three plates do not fit by area either - 243 > 240. So this is not cutting squares out of a
   sheet: the die re-forms the strip, spreading it into the cavity, which is what a blanking-and-forming
   die does and what a shear cannot.

The hammer therefore never shears.

---

## The loop

```
wide rolling ──▶ boilerplate 600 u ──▶ reheat? ──▶ steam hammer + plate die
                                                        │  hold the lever
                                                        ▼
                                              3 × game:metalplate @200
```

| # | Where | Player verb | What comes out |
|---|---|---|---|
| 1 | [wide hall](../machines/wide-hall.md) | roll a slab to the 1.0 stand | the slab at 15 × 1, 32 long (wrought) or 16 long per cropped fifth (cast) |
| 2 | [shear](../machines/shear.md) | RMB on the throat | one `boilerplate` 15 × 1 × 16 @600, and the remainder still stock |
| 3 | [steam hammer](../machines/steam-hammer.md) | fit the plate die (RMB holding it, in the `rollset` / `pattern` idiom) | the die-set is seated in the ram and the docked anvil |
| 4 | the anvil cell | RMB with the `boilerplate` | the strip is laid on the bottom die |
| 5 | the lever cell | hold RMB | the blow loop runs; the work item advances through its forge stages |
| 6 | — | release | 3 × `game:metalplate` |

Same machine, same gesture, different die. Shingling - 6 wrought balls read off the anvil at the first
lever pull - is the hammer's other job and shares every verb above; the die is the whole difference
([steam hammer](../machines/steam-hammer.md) § Job 1).

---

## Inputs and outputs

| In | Mass | Out | Mass | Balance |
|---|---|---|---|---|
| 1 × `boilerplate` 15 × 1 × 16 | 600 u | 3 × `game:metalplate` 9 × 1 × 9 | 3 × 200 = 600 u | exact by mass; 243 vx³ against 240 drawn |

Neither item is an item. `boilerplate` does not exist in `src/` and `game:metalplate` is vanilla's,
reachable today only by hand-forging a 9 × 9 smithing pattern from two ingots - the rate this stamp
reproduces exactly ([density rule](../mechanics/density-rule.md) § Derivation).

### The operations ledger

One stamp = one held lever-pull on one `boilerplate`. Feed counts are [rolling](rolling.md)'s and are cited,
not restated.

| Route | Stock | `boilerplate` per schedule | Stamps | Plates | Metal |
|---|---|---|---|---|---|
| narrow `flat` | `shingledbar` | — (the bar is cropped straight to plate at the 1.0 gap) | 0 | 2 | 400 u |
| wide, lpex | `shingledslab` 1200 u | 2 | 2 | 6 | 1200 u |
| wide, smex | `castslab` 3000 u | 5 | 5 | 15 | 3000 u |

The stamp is what turns the wide route's volume into something the game consumes. Rolled to 1.0 and
cropped, a 3000 u cast slab is five boilerplates, and boilerplate's only other consumers are boiler shells
and, later, the [bending roller](../machines/bending-roller.md).

---

## Numbers

Nothing on this page is in config or in code. Every value below is either derived from the
[density rule](../mechanics/density-rule.md) or is a stated design constant with no implementation.

| Quantity | Value | Status | Note |
|---|---|---|---|
| stamp yield | 3 plates per `boilerplate` | design constant | derived: 600 / 200. Do not retune - it is the only figure that makes the wide route's headline true |
| mass balance | exact | derived | no yield is minted; the same rule that closed the nail route's exploit question |
| volume balance | 243 vx³ out of 240 | 1.25 % over | the die re-forms rather than cuts, so it is not a partition - but the art must not draw three 9 × 9 squares inside a 15 × 16 rectangle, because they do not fit |
| batch size | = die cavity count, read off the die mesh | design constant | owned by [steam hammer](../machines/steam-hammer.md); a 3-cavity plate die is what makes the yield above legible without a tooltip |
| output-thickness tier gate | LP ≤ 1 voxel · HP ≥ 2 voxels | design constant | [steam hammer](../machines/steam-hammer.md), [HP hammer](../machines/hp-hammer.md). A 1-voxel `boilerplate` sits exactly at the LP ceiling |
| blows per stamp · steam per blow · minimum inlet pressure | unchosen | open | [steam hammer](../machines/steam-hammer.md) § Unchosen |
| work temperature required | unchosen | open | nothing anywhere names a threshold; the mill's `RollingTempC` (`IwexConfig.cs:490`) is an iwex key the hammer would be reaching upward into |

### The ceiling every future die inherits

> A die may re-form the metal it is given and nothing else.

`boilerplate` → 3 plates is 600 u → 600 u. The nail route sits on the same ceiling: vanilla's own
36-voxel anvil pattern gives 4 nails-and-strips per 100 u ingot, so 25 u a nail is a hard anchor and no
machine may beat it ([nail machine](../machines/nail-machine.md)). A die that produced four plates from a
`boilerplate` would mint 200 u.

---

## Why it is like this

* Industrialisation buys labour, never material: 200 u per plate on the narrow route, the wrought wide
  route and the cast wide route alike; what changes is the handling ([rolling](rolling.md) § The trade).
* The die is the extension point and it is data. `RollSetSpec` and `MoldSpec` established the idiom -
  the tooling owns the data, the machine reads it, no machine code names a product. A future die for shell
  segments or thick blanks costs one item definition.
* One verb per station: the hammer never shears and the shear never forms, so neither needs to ask what
  the player meant.

---

## Gotchas

* Three plates do not fit inside the strip by area (243 vx³ against 240). The mass balance is exact and
  the geometry is not, which is fine for a forming die and would be a defect for a cutting one - so the
  art and the handbook text must both describe it as forming.
* `boilerplate` does not exist, so the stamp has no input; the plate die does not exist, so it has no
  tooling; the hammer does not exist, so it has no machine. This process sits downstream of three separate
  unbuilt things.
* The `ItemDie` spec does not exist either. It is the [heading machine](../machines/heading-machine.md)'s
  to define, and this machine is its second consumer - so the hammer must not invent a parallel die
  format.
* A stamp is not counted as a rolling feed anywhere. [rolling](rolling.md)'s trade table lists feeds
  and stamps in separate columns for exactly that reason; do not fold them together when tuning.

---

## Open

| # | Question | Weight |
|---|---|---|
| 1 | Nothing is built - no machine, no die, no die spec, no input item | high |
| 2 | Does stamping need heat? No threshold is named anywhere. Blanking a cold 1-voxel strip is physically reasonable; forming one is not. If it needs heat, the `boilerplate` acquires a heat budget between the shear and the hammer, and the reheat furnace gains a fourth customer | medium |
| 3 | Die durability - none is designed. The boring machine's bit tiers and the wooden pattern's `WoodenPatternDurability = 24` (`PatternItemDefinitions.cs`) are the two precedents, and they disagree with each other | low |
| 4 | What else gets a die? Named but unspecified: blanks, shell segments, fasteners. The rule that constrains them is on this page (a die may not mint); the catalogue is [steam hammer](../machines/steam-hammer.md)'s | low |
| 5 | What happens on a blow with nothing under the die? Undesigned - presumably a sound, but the die/anvil pair should not damage itself | — |
| 6 | Does the HP hammer re-do this operation, or only thicker ones? The gate is stated as output thickness ≥ 2 voxels, which excludes plate - so the HP machine is additive, not an upgrade. Confirm before hpex's die list is written | low |
