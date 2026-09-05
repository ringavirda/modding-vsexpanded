# Blown iron

**Status** designed - nothing exists. No metal def, no item, no lang key, no texture, no code path.
`grep -rn -i "blowniron\|blown iron" src/ assets/` returns zero hits; the term appears only in
[STATE.md § N1](../../internal/plans/STATE.md), [Bessemer § Open 1](../machines/bessemer.md#open) and
[ladle § Open 2](../machines/ladle.md#open).   **Mod** smex (`SteelmakingExpanded`)

> Caution: this is a sequencing problem, not a modelling one. Everything it needs already exists except
> the one machine that consumes it. Shipping it before the [ladle](../machines/ladle.md) leaves the steel
> tier with no product at all.

**Owns** - the facts this page is canonical for:

* what blown iron is: the Bessemer converter's true product, and the ladder's second metal that
  guarantees nothing;
* why the blow leaves it unusable, and why manganese and not carbon fixes it, stated as a property of the
  material (the additive rules and the chill arithmetic are the [ladle](../machines/ladle.md)'s);
* the metal def it must ship as - every field, with the value and the reason - and the ruling that its
  `itemForms` is `["ingot"]` and it makes no tools;
* the melting-point decision and its derived consequence for the converter's freeze/stall ordering;
* the landing order: the exact set of changes that must go in together, and what breaks if they do not;
* the open question N1 creates and nobody has answered: what the over-blow band produces once the blow's
  own product is already burnt metal.

**Does not own** - cited only, never restated:

| Fact | Owner |
|---|---|
| the blow, the carbon bands, the retype sites, the mass balance, capacity, the cold-scrap gate | [Bessemer](../machines/bessemer.md) |
| recarburising as a verb, the chill model, mixing by held proportion, waste alloy, the ladle's numbers | [ladle](../machines/ladle.md) |
| the metal ladder, D3, `MetalDef` field semantics, the generated family and its presets | [alloys](alloys.md) |
| where ferromanganese is smelted / melted | [cold blast furnace](../machines/blast-furnace-cold.md), [cupola](../machines/cupola.md) |
| `IMoltenCell`, `FlowEdge`, the metal-type refusal, push/drain/soak | [molten network](../mechanics/molten-network.md), [molten canal](../machines/molten-canal.md) |
| `T_process = T_in − T_loss` and `HeatBalance.Compute` | [heat balance](../mechanics/heat-balance.md) |
| melt-back granularity and the declared-recovery rule (R2) | [recoverability](../mechanics/recoverability.md) |
| code-first item defs, goldens, config | [recipes & config](../mechanics/recipes-config.md) |
| status of everything, N1, D4 | [STATE.md](../../internal/plans/STATE.md) |

**Depends on** [Bessemer](../machines/bessemer.md) · [ladle](../machines/ladle.md) · [alloys](alloys.md) ·
[molten network](../mechanics/molten-network.md) · [recoverability](../mechanics/recoverability.md) ·
[STATE.md § N1](../../internal/plans/STATE.md)

---

## Role

Blown iron exists to make the ladle mandatory: without it the Bessemer converter is a one-block steel
factory, and the suite's only alloying mechanic has no customer.

The blow burns out the carbon and the manganese together, leaving iron saturated with dissolved oxygen:
red-short, unforgeable, useless as a casting. Bessemer's 1856 process produced unusable metal until
Mushet's spiegeleisen addition.

The settled chain ([STATE.md § N1](../../internal/plans/STATE.md)):

```
molten pig ──▶ BESSEMER (blow) ──▶ blown iron ──▶ LADLE (+ FeMn / spiegel) ──▶ steel
                                        │
                                        └── unusable: no forms, no tools, no recipes accept it
```

### Why carbon cannot fix it, and manganese can

This is why the suite carries two additives that would otherwise be redundant:

| Additive | What it adds | Fixes a blow? | Why |
|---|---|---|---|
| powdered coke | carbon, and only carbon | no | the metal's problem is dissolved oxygen, not missing carbon |
| ferromanganese / spiegeleisen | carbon and manganese | yes | manganese is a stronger deoxidiser than iron - it scavenges the oxygen into slag, then the carbon it carries does the recarburising |

Carbon reacts with dissolved oxygen but leaves as CO gas, giving a boiling, blowholed ingot; manganese
takes the oxygen out as a floating oxide. The rule: coke trims carbon, only a ferroalloy repairs a blow.

The material carries the rule, so no gate is needed. Blown iron is a different metal code, so no recipe
accepts it, no mold takes it, and it has no forms to accept anything with; the
[ladle](../machines/ladle.md) needs no check that says "you must recarburise".

---

## The catalogue

A metal has no section and no `vx³` - see [alloys § why no geometry columns](alloys.md#role). What it
has is a form list, and blown iron's is the shortest on the ladder.

| Form | Code | Exists as | Mass | Made by | Consumed by |
|---|---|---|---|---|---|
| molten charge | `siex:ingot-blowniron` carried in a `MoltenCharge` | the normal state - it should almost never be an inventory item | the converter's charge units ([Bessemer](../machines/bessemer.md#numbers)) | [Bessemer](../machines/bessemer.md) blow | [ladle](../machines/ladle.md), and nothing else |
| ingot | `siex:ingot-blowniron` | the generated `ingot` form; exists only so a mold/canal has something to freeze into | vanilla ingot template, 1 → 1 smelt-back (`MetalFamilyEmitter.cs:145`, `:154`) | solidifying a pour | remelt in the [cupola](../machines/cupola.md) |
| bits | `game:metalbit-iron` (as `solidDrop`) | chisel-out / break recovery from a frozen vessel | 5 u per bit, the shared granularity ([recoverability](../mechanics/recoverability.md)) | a lost heat | scrap charge, remelt |
| plate / rod / nails | - | must not exist | - | - | - |
| tools | - | must not exist - no `tools` block at all | - | - | - |

### The metal def it must ship as

`pigiron.json` is the template, not `bessemersteel.json` - pig iron is the ladder's other metal that
guarantees nothing, and its def already says exactly that: `itemForms: ["ingot"]` and no `tools` key
(`mods/iiex/assets/iiex/config/metals/pigiron.json:9-13`).

```jsonc
// mods/siex/assets/siex/config/metals/blowniron.json
{
  "code": "blowniron",
  "moltenItem": "siex:ingot-blowniron",
  "solidDrop": "game:metalbit-iron",     // recoverable as ordinary iron scrap
  "displayLangKey": "siex:metal-blowniron",
  "castDomain": "smex",
  "generateItemFamily": true,
  "itemForms": ["ingot"],                // ← the whole ruling, in one line
  "texturePath": "game:block/metal/tarnished/iron",
  "density": 7870,
  "meltingPoint": 1482
  // no "tools" key - cf. pigiron.json, which omits it for the same reason
  // no "isAlloy" - it is burnt iron, not a mixture
}
```

Every field's semantics and its convention fallback are [alloys](alloys.md#numbers)'s; the values above
and their reasons are this page's.

---

## Numbers

### Owned — the def's values and why each one

| Field | Value | Reason |
|---|---|---|
| `density` | 7870 kg/m³ | the emitter's own iron default (`MetalFamilyEmitter.cs:42`) - blown iron is nearly pure iron, so it takes the iron figure, not pig's 7000 or Bessemer steel's 7820 |
| `meltingPoint` | 1482 °C | the emitter's iron default (`:43`) and the value the whole iron line already uses. The physical figure for pure iron is ~1538, and choosing it would break the converter - see the derivation below |
| `solidDrop` | `game:metalbit-iron` | it is iron; a frozen heat returns ordinary iron scrap, as pig and cast iron do (`pigiron.json:4`, `castiron.json:4`) |
| `itemForms` | `["ingot"]` | the ingot exists only so a pour has something to become; every build form is a promise blown iron cannot make |
| `tools` | absent | `preset: "none"` would also work (`MetalToolEmitter.cs:80-84`), but absence is what pig iron does |
| `liquidThreshold` | absent → 0.8 | pig and cast iron override to 0.75 because they are high-carbon; blown iron is not |

### Derived — the melting point decides whether the converter has a warning band

The converter freezes a charge when it falls below its own melting point, and stalls refining below
`BessemerRefineTemperature`. Both thresholds are compared against the same `T_process`, and cold scrap is
the only thing that moves it. Using the Bessemer's shipped terms - `T_process` = 1800 °C at full blast
with no scrap, refine floor 1500 °C, scrap loss 0.35 °C/u
([Bessemer § Numbers](../machines/bessemer.md#numbers)) - the two limits land at:

| Blown iron's `meltingPoint` | Freeze at | Refine stall at | Which comes first |
|---|---|---|---|
| 1482 °C (recommended) | `(1800−1482)/0.35` = 909 u scrap | 857 u scrap | stall first - the player gets a warning band before losing the heat |
| 1500 °C (= Bessemer steel's) | 857 u | 857 u | exactly coincident - the "stall" is a freeze, which is [Bessemer Gotcha #2](../machines/bessemer.md#gotchas) |
| 1530–1538 °C (physical) | 771–749 u | 857 u | freeze first - the heat is lost before any stall message can appear |

The physically correct number is the wrong one: it would make blown iron the only metal that can freeze
while the converter still believes it is refining. 1482 keeps the ordering the messages assume.

### Cited — owned elsewhere

| Quantity | Value | Owner |
|---|---|---|
| what a blow yields per 100 u pig | 90 u metal + 6 u slag + 4 u gas | [Bessemer](../machines/bessemer.md#numbers) |
| carbon at which the bath retypes | `BessemerSteelCarbonTarget` 0.002 | [Bessemer](../machines/bessemer.md#numbers) |
| the over-blow band and its ~11.7 s window | 0.002 → 0.0005 | [Bessemer](../machines/bessemer.md#gotchas) |
| product of a settled 6000 u charge | 5400 u | [Bessemer](../machines/bessemer.md#numbers), [STATE.md § D4](../../internal/plans/STATE.md) |
| recarburiser demanded per heat | ~300 u of FeMn | [STATE.md § the ferroalloy furnace](../../internal/plans/STATE.md) |
| ladle capacity, chill, additive routing | proposed | [ladle](../machines/ladle.md#numbers) |
| melt-back at 5 u per bit | shared | [recoverability](../mechanics/recoverability.md) |

---

## Assets

Nothing exists. What it will need:

| Asset | Path | State |
|---|---|---|
| metal def | `mods/siex/assets/siex/config/metals/blowniron.json` | missing - smex's `config/metals/` holds exactly one file, `bessemersteel.json` |
| shape | - | none needed. The generated `ingot` form paints `game:item/ingot` ([alloys § assets](alloys.md#assets)) |
| texture | `game:block/metal/tarnished/iron` | vanilla, already used by pig iron (`pigiron.json:10`) - verified present. Dull by intent: this is not a metal to be proud of |
| lang | `siex:metal-blowniron`, `siex:item-ingot-blowniron`, `siex:itemdesc-ingot-blowniron*` | missing. The description is the only place the player learns why the ingot in their hand is worthless. Model on `iiex:itemdesc-ingot-pigiron*` (`mods/iiex/assets/iiex/lang/en.json:39`) |
| status lines | `smex-bessemer-status-*` | existing converter status text says "pour it, or blow on for soft iron" ([Bessemer Gotcha #1](../machines/bessemer.md#gotchas)); under N1 that sentence is wrong and has to change with the retype |
| goldens | `mods/siex/tests/goldens/siex/itemtypes/blowniron/ingot.json` | missing - one file, exactly as `pigiron/` has one |
| handbook | `mods/siex/docs/handbook/04-bessemer.html` | already stale for the shipped machine ([Bessemer Gotcha #11](../machines/bessemer.md#gotchas)); N1 makes it wrong a second time |

---

## Code

Nothing exists. No `blowniron` token, no def, no test.

### The landing order — four changes, and they go in together

| # | Change | Where | Blocked by |
|---|---|---|---|
| 0 | the [ladle](../machines/ladle.md) exists - block, BE, additions, pour | `mods/siex/src/BlockStructures/Ladle/` | nothing built |
| 0b | ferromanganese exists as a metal and as something the player can obtain | `assets/*/config/metals/`, [cold blast furnace](../machines/blast-furnace-cold.md), [cupola](../machines/cupola.md) | nothing built |
| 1 | the metal def | `mods/siex/assets/siex/config/metals/blowniron.json` | (1 file) |
| 2 | the retype target - the converter's `SteelCode` becomes blown iron | the four metal tokens at `BlockEntityConverterControl.cs:95-99`; the call site is `RetypeToSteel` (`:374-383`) - both owned by [Bessemer](../machines/bessemer.md#code) | needs 1 |
| 3 | the tool preset - `bessemersteel.json`'s `tools: {preset:"good"}` goes, and its `itemForms` shrink to what a ladle product should have | `mods/siex/assets/siex/config/metals/bessemersteel.json:13-15` ([alloys Gotcha #1](alloys.md#gotchas)) | needs 0 |
| 4 | lang + status text + handbook | `mods/siex/assets/siex/lang/en.json`, `mods/siex/docs/handbook/04-bessemer.html` | needs 2 |

Caution: change 2 alone is a regression, not a fix. With the ladle absent, retyping the blow's product to
blown iron gives the steel tier no product at all - the converter would pour a metal with one item form,
no recipes and no consumer. [ladle § Open 2](../machines/ladle.md#open) states this.

Change 3 without change 2 is safe and worth doing first. Dropping Bessemer steel's tool preset removes 8
items and 8 lang keys from the drift surface, and the content it deletes contradicts two settled rulings.

### Where a caller hooks in

* The item side is one JSON file - no C#. See [alloys § where a caller hooks in](alloys.md#code).
* The machine side is four string constants already indirected through `MetalRegistry`
  (`BlockEntityConverterControl.cs:95-99`).
* Tests: `mods/siex/tests/Definitions/BessemerSteelMetalTests.cs:30`/`:44` is the template -
  it asserts a metal is smex-owned and that its generated family matches its preset. A `BlownIronMetalTests`
  asserting the absence of plate/rod/nails/tools guards the ruling against being quietly undone.

---

## Gotchas

1. The one-line fix is not a one-line fix. Every summary of N1 reduces to "retype the blow's output", and
   doing only that empties the steel tier. The dependency is on a machine that does not exist
   ([ladle](../machines/ladle.md)), not on a token.

2. N1 changes what over-blowing means, and nobody has said what to. Today the converter has two products:
   Bessemer steel at `carbon ≤ 0.002` and soft `game:ingot-iron` at `≤ 0.0005`
   (`BlockEntityConverterControl.cs:328-383`). If the first becomes blown iron, the over-blow band is
   "burnt metal, more so". Three candidates, none chosen: (a) delete the band and let the blow finish;
   (b) keep it as irrecoverable scrap-only metal, making the timing trap harsher; (c) keep
   `game:ingot-iron` and accept that two adjacent bands both need the ladle. See [Open #1](#open).

3. N1 softens the suite's harshest timing trap. The 11.7-second over-blow window
   ([Bessemer Gotcha #1](../machines/bessemer.md#gotchas)) is punishing because stopping late turns usable
   material into worse; once both sides of that boundary need the ladle, overshooting costs nothing.

4. "Blown iron" and "ingot iron" are two names for nearly the same physical thing, and
   [alloys § Compositions](alloys.md) already carries an Ingot iron row describing the over-blow product
   as "slag-free, ~0 % C - not wrought iron". Shipping blown iron without reconciling that row leaves the
   ladder with two burnt-iron entries whose difference is a carbon fraction the player never sees.

5. Blown iron is the second metal that guarantees nothing, and the pair should feel the same: pig iron and
   blown iron share a dull texture, a single ingot form and a description shape.

6. The metal-type refusal makes a mistake permanent. `FlowEdge` will not move metal into a cell holding a
   different code and the graph's merge is a no-op ([molten network](../mechanics/molten-network.md)), so
   blown iron poured into a canal that already carries steel will not go, and a blown-iron run cannot be
   topped up. The ladle must pull it by code, which is
   [ladle § it must not be a molten-graph node](../machines/ladle.md#structure).

7. A frozen blown-iron heat recovers as plain iron bits under the proposed `solidDrop`, so the punishment
   for losing a heat is losing the blow, not the metal - consistent with R2 and with how pig and cast iron
   behave. It also makes scrapping blown iron a legitimate way to avoid ever building a ladle, at 90 % of
   the value; that should be a decision, not an accident.

---

## Open

1. What does the over-blow band produce? (Gotcha #2.) Neither
   [Bessemer § Open 1](../machines/bessemer.md#open) nor [ladle § Open 2](../machines/ladle.md#open) raises
   it. It has to be answered before change 2 lands, because the answer decides whether `IronCode` stays in
   the converter's token list at all.

2. The whole chain is blocked on two unbuilt things - the [ladle](../machines/ladle.md) and a
   ferromanganese the player can actually obtain ([cold blast furnace](../machines/blast-furnace-cold.md) §
   second act, melted in the [cupola](../machines/cupola.md)). The second additionally needs manganese to
   exist as a metal at all ([alloys § Open 5](alloys.md#open)).

3. How much ferromanganese per heat, and does it change the mass balance? ~300 u is
   [STATE.md](../../internal/plans/STATE.md)'s estimate against a 5400 u heat (≈ 5.5 %). FeMn is high-carbon by
   definition, so the addition moves manganese and carbon together - a single-element model would miss the
   product's carbon target. The arithmetic is the [ladle](../machines/ladle.md#open)'s; the demand is this
   material's, and nothing has been sized.

4. Does blown iron need a `MoltenCharge` display of its own? R7 ("nothing is hidden") means the player
   should see that what they are pouring is unusable before they pour it. The converter's readout prints
   the metal's display name ([alloys](alloys.md#numbers), `MetalRegistry.DisplayName`), so "Blown Iron" may
   be sufficient - but a name is not an explanation, and there is no handbook page.

5. `bessemersteel.json` should probably lose more than its tools. Once Bessemer steel is a ladle product,
   its `itemForms` list is worth re-deriving from what actually consumes it - which under N3 is fabricated
   substitutes built from beam + plate + rivets ([cast parts](cast-parts.md),
   [rolled parts](rolled-parts.md)), not a generated `rod` and `metalnailsandstrips`. Not this page's call,
   but this page's change (3) is when it becomes cheap.

6. Should blown iron be castable at all? `castDomain: "smex"` above lets it reach a mold, and the
   [long cell](../machines/long-cell.md) would take it. Arguably it should not be, but refusing it needs a
   mechanism the mold system does not have, and the cheap version (no matching recipes downstream) is
   probably enough.
