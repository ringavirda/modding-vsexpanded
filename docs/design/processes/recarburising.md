# Recarburising

**Status** blocked - designed, nothing exists. The step is mandatory under [STATE.md](../../internal/plans/STATE.md) N1
and not one piece of it is built: no blown-iron metal, no ladle, no ferroalloy, no powdered coke, and the
shipped converter still pours a finished tool-capable steel that skips the step entirely.
**Mods** smex (the blow, the ladle) · iiex (the ferroalloy furnace, the cupola that melts it)

**Owns** - the facts this page is canonical for:

* the loop: which baths must be recarburised, which may skip it, and the exact step order;
* the addition arithmetic - how much recarburiser one heat needs, derived from the carbon target, and the
  fact that the reagent's carbon strength sets the mass and the mass sets the route;
* the two-reagent split (settled 2026-08-07): spiegeleisen and high-carbon ferromanganese are two distinct
  items with distinct jobs - the FeMn trim lands mild steel and never needs a cupola, while the 10–15 %
  spiegeleisen dose is the rail-grade route and does;
* the three baths that enter this loop (Bessemer blown iron, over-blown ingot iron, arc ingot iron) and the
  one that does not (an open-hearth heat stopped on carbon);
* the mass balance of the step under R2 - the addition joins the product, so a heat grows;
* the ordering constraint the loop imposes on implementation, and the full gap list.

**Does not own** - cited only, never restated:
[Bessemer](../machines/bessemer.md) - the blow, the carbon bands, the blast draw, capacity, the tilt states ·
[blown iron](../items/blown-iron.md) - the metal itself, its metal def and landing order ·
[ladle](../machines/ladle.md) - the vessel, mixing by held proportion, resolve-on-pour, waste alloy, and the
chill model and its arithmetic ·
[open hearth](../machines/open-hearth.md) - the bath, bath alloying (D6), the rhythm rule ·
[cupola](../machines/cupola.md) - melting ferroalloys, and why fuel contact makes it the right machine ·
[blast furnace (cold)](../machines/blast-furnace-cold.md) - ferroalloys as a burden family, the second act ·
[alloying](alloying.md) - the mixing loop this one is a special case of, D3, and the ferroalloy supply chain ·
[molten network](../mechanics/molten-network.md) / [molten canal](../machines/molten-canal.md) - how the heat
moves · [materials.md](../materials.md) - what each metal is and its target composition ·
[heat balance](../mechanics/heat-balance.md) - the furnace `T_process` law (the ladle has no fire and does not
use it) · [conventions.md](../conventions.md) - R2, R3, R5, R7 · [STATE.md](../../internal/plans/STATE.md) - N1, D4, D6

**Depends on** [Bessemer](../machines/bessemer.md) · [ladle](../machines/ladle.md) ·
[open hearth](../machines/open-hearth.md) · [cupola](../machines/cupola.md) ·
[blast furnace (cold)](../machines/blast-furnace-cold.md) · [materials.md](../materials.md)

---

## What it is

A Bessemer blow does not stop where the player wants it. Air blown through molten pig burns the carbon out
and keeps going - it also burns the manganese and the silicon, and by the time the flame drops the iron is
oxygen-saturated. That metal is **blown iron**: not mild steel, not wrought iron, not usable. It cracks hot.

**Recarburising** is the step that turns it back into a material. A measured addition of a high-carbon
ferroalloy is dropped into the metal after the blow. It puts the carbon back to the grade wanted, and its
manganese scavenges the dissolved oxygen. Mushet's 1856 spiegeleisen addition is what made the Bessemer
process work at all, and the mod treats it that way ([STATE.md](../../internal/plans/STATE.md) N1).

What the mod abstracts away: real practice recarburised by pouring the whole blow onto a measured
spiegeleisen charge in the ladle, judged by eye and by the flame, with silicon, sulfur and phosphorus all
mattering and blows sorted by pig analysis. The mod keeps one axis (carbon), one reagent property (its carbon
strength), and one consequence (chill). There is no oxygen number, no sulfur, no ppm - the deoxidation is
expressed as "this reagent works and plain carbon does not".

---

## The loop

```
                            ┌── open hearth: stop on carbon ──▶ pours ready, loop not entered
                            │
molten pig ──▶ Bessemer ──▶ blow to the end ──▶ blown iron ──┐
                                                              │  (canal)
   cold blast furnace ──▶ ferroalloy burden ──▶ FeMn ─solid─▶ ladle ──▶ steel ──▶ canal / long cell
                                                              │
   Bessemer over-blow ──▶ ingot iron ─────────────────────────┘
   arc furnace (elex)  ──▶ ingot iron ─────────────────────────┘
```

| # | Machine | Player verb | What comes out |
|---|---|---|---|
| 1 | [cold blast furnace](../machines/blast-furnace-cold.md) | charge a ferroalloy burden and run it, as a campaign rather than continuously | solid high-Mn ferromanganese - *no burden family, no metal, no product code exists* |
| 2 | [Bessemer](../machines/bessemer.md) | blow past the carbon target, to the end | blown iron - *the shipped machine retypes to finished steel at 0.2 % C instead* |
| 3 | [molten canal](../machines/molten-canal.md) | tilt the vessel; the deep pour is a held commit | the heat, in a canal |
| 4 | [ladle](../machines/ladle.md) | passive - the ladle pulls from the adjacent canal cell | the heat, in the ladle, with a live readout (R7) |
| 5 | [ladle](../machines/ladle.md) | RMB with solid ferromanganese in hand, once or a few times | carbon and manganese in the tally; the bath cools by the chill term ([ladle](../machines/ladle.md)) |
| 6 | [ladle](../machines/ladle.md) | RMB, empty hand - tilt-pour | steel, into a canal start, mold pedestal or [long cell](../machines/long-cell.md) |

Step 1 is a campaign, not a supply line. One blow's worth of recarburiser is minutes of furnace time (a
reference campaign is ≈ 11½ minutes - [ironmaking](ironmaking.md) § Derived), so the cold furnace runs
occasionally and idles between, which is why it gets a family of ferroalloys rather than one product.

### The fork: who has to do this and who does not

| Bath | Enters the loop? | Why |
|---|---|---|
| **Bessemer blown iron** | mandatory | the blow has no stopping point that leaves a usable metal ([STATE.md](../../internal/plans/STATE.md) N1) |
| **Over-blown ingot iron** | yes | [alloys](../items/alloys.md) already routes it - *"not wrought iron — recarburise or remelt"* |
| **Arc-furnace ingot iron** | yes, when elex exists | same row; deferred with elex ([STATE.md](../../internal/plans/STATE.md) D8) |
| **Open-hearth heat** | no | a long bath can be held and corrected, so it is stopped on the carbon it wants and taps ready ([open hearth](../machines/open-hearth.md)) |
| **Puddled wrought iron** | never | it is never molten; carbon leaves as a pasty ball, not as a bath ([materials](../materials.md) § Semi-finished forms) |

The fork is the second half of the Bessemer-vs-open-hearth argument: the
[open hearth](../machines/open-hearth.md) sells guarantees over volume, and the converter's product needs a
downstream step the hearth's does not. The converter is still faster; it is not finished when it stops.

### Powdered coke is not a substitute

| Additive | Adds | Where it is used | Exists? |
|---|---|---|---|
| **powdered coke** | carbon only | open-hearth carbon trim; carburising arc ingot iron | no item — `grep -rni "powderedcoke\|powdered coke" src/ assets/` returns nothing |
| **spiegeleisen** *(~3.8 % C, low Mn — settled 2026-08-07)* | carbon and deoxidises | a mandatory post-blow addition — dosed at 10–15 % it is the rail-grade route | no metal def; `assets/*/config/metals/` holds only `castiron`, `pigiron`, `slag`, `bessemersteel` |
| **high-carbon ferromanganese** *(~80 % Mn — settled 2026-08-07)* | manganese, carbon, and deoxidises | the mild-steel trim additive, and the ladle's alloying reagent | same — no metal def |

Carbon alone cannot fix burnt Bessemer metal: it is the manganese that takes the oxygen out. The three
additives therefore coexist with no redundancy; the coke rule is the [ladle](../machines/ladle.md)'s, and the
two-reagent split is this page's (§ Numbers, settled 2026-08-07).

---

## Inputs and outputs

Sizes are one settled converter heat: a 6000 u pig charge yielding 5400 u of metal (`BessemerSteelYield`
0.90, `SiexConfig.cs:221`; the derivation and the 4800-vs-6000 dispute are
[bessemer § Numbers](../machines/bessemer.md#numbers) and [§ Open #2](../machines/bessemer.md#open)).

| In | Mass | Out | Mass |
|---|---|---|---|
| blown iron, molten, ~0 % C | 5400 u | mild steel, molten, ~0.2 % C | 5559–5586 u *(base + trim)* |
| solid high-C ferromanganese, cold — the mild trim | 159–186 u (see Numbers) | — | — |
| *or:* spiegeleisen at 10–15 % — the rail-grade route, molten from a [cupola](../machines/cupola.md) | 540–810 u | rail-grade steel, 0.45–0.65 % C | 5940–6210 u |
| — | — | heat lost to the chill | *not mass* — [ladle](../machines/ladle.md) owns the temperature term |

Nothing is consumed and nothing is lost. Recarburising is a 1:1 transport-and-forming step under R2
([conventions](../conventions.md)) - no yield fraction, no by-product, no slag. Every unit of reagent ends up
in the product, which is why the heat grows. The only refining loss in the chain is the converter's own 6 %
slag + 4 % gas ([bessemer](../machines/bessemer.md#numbers)), spent before this step begins.

---

## Numbers

Everything here is derived or proposed - no config key, no code. Each derivation names the cited value it
starts from.

### Settled 2026-08-07 — two reagents, two distinct items

Solving the addition equation backwards for the two figures the design had written down:

| Stated figure | Source | Implied composition | Which reagent that is |
|---|---|---|---|
| *"perhaps 300 u of recarburiser"* per blow | settled design figure | `11.4 / 300` = 3.8 % C | spiegeleisen |
| *"6000 u of hadfield needs ~940 u of FeMn"* | settled design figure | `750 / 940` = 80 % Mn | high-carbon ferromanganese |

They are not interchangeable - roughly 10× apart in Mn strength - and each keeps the job its composition
implies:

| Reagent | Composition | Job |
|---|---|---|
| **spiegeleisen** | ~3.8 % C, low Mn (roughly a tenth of FeMn's strength) | the historical Bessemer additive - dosed at 10–15 % it is the rail-grade route (below) |
| **high-carbon ferromanganese** | ~80 % Mn, high-C | the mild-steel trim additive, and the [ladle](../machines/ladle.md)'s alloying reagent |

**The reagent's strength sets the mass, and the mass sets the route.** That rule is this page's, and the
two-reagent split is what makes "which ferroalloy" a real choice.

### How much recarburiser one heat needs — the **mild** trim

The mild target is Bessemer steel at ~0.2 % C ([alloys](../items/alloys.md)), which is also the number the
shipped converter already uses as its retype threshold (`BessemerSteelCarbonTarget` 0.002,
`SiexConfig.cs:162`). For an addition of mass `m` and carbon fraction `c` into a base of `M` units at ~0 % C:

```
m · c = 0.002 · (M + m)        ⇒        m = 0.002 · M / (c − 0.002)
```

At `M` = 5400 u:

| Reagent | `c` | `m` | as % of the heat | Route |
|---|---|---|---|---|
| high-carbon ferromanganese, `c` band low | 0.060 | 186 u | 3.4 % | solid |
| high-carbon ferromanganese, `c` band high | 0.070 | 159 u | 2.9 % | solid |
| *(spiegeleisen at its settled 0.038, for reference)* | 0.038 | 300 u | 5.6 % | solid — and it lands exactly on the settled *"perhaps 300 u"* figure |

Mild steel is a small FeMn trim, solid, hand-dropped and free: the [ladle](../machines/ladle.md)'s chill table
shows a 300 u solid addition landing free on a 5400 u bath from either 1800 °C or 1700 °C, and the FeMn trim
is half that size.

Spiegeleisen's ~3.8 % C is pinned (2026-08-07, the table above); FeMn's own carbon fraction is still a design
lever - it decides whether hadfield is one operation or two ([alloying](alloying.md) § Numbers), and nothing
has chosen it.

### The 10–15 % spiegeleisen dose is the **rail-grade route** *(a feature, settled 2026-08-07)*

The 10–15 % band, taken on a 5400 u heat at spiegeleisen's settled `c` = 0.038:

| Addition | Mass | Carbon in | Final mass | Final % C |
|---|---|---|---|---|
| 10 % of charge | 540 u | 20.5 u | 5940 u | 0.35 % |
| 15 % of charge | 810 u | 30.8 u | 6210 u | 0.50 % |

At the historical spiegel band (`c` up to ~0.05) the same doses land 0.45–0.65 % C - rail/spring carbon. The
converter makes both products: a small FeMn trim lands mild at ~0.2 % C, a 10–15 % spiegeleisen dose lands
rail-grade at 0.45–0.65 % C. One blow, two products, chosen by reagent and dose.

The large dose is why the cupola keeps its third job. A 540–810 u cold charge is past what a bath absorbs for
free ([ladle](../machines/ladle.md) § chill), so the rail-grade route wants its spiegeleisen molten from a
cupola - which is why *"Bessemer plants kept a cupola melting spiegeleisen"*
([cupola](../machines/cupola.md)). The mild trim alone never needs a cupola; the rail-grade dose and
[alloying](alloying.md)'s hadfield charge both do.

### The step that follows the blow, in shipped constants

| Quantity | Arithmetic | Result |
|---|---|---|
| carbon the blow must remove, settled | `BessemerPigCarbonStart` 0.04 → ~0 (`SiexConfig.cs:159`) | the whole 4 % |
| carbon the blow removes today | 0.04 → `BessemerSteelCarbonTarget` 0.002 (`:162`) | 3.8 % — it stops at the finished grade |
| carbon recarburising puts back | 0 → 0.002 | 10.8 u on a 5400 u heat |
| the shipped terminal that N1 wants | `BessemerOverblowCarbon` 0.0005 (`:167`), `IsOverblownIron()` (`BlockEntityConverterControl.cs:872`) | already implemented, under the name *over-blow* |

N1 does not add a state to the converter, it deletes one. The shipped machine already blows to a fully
decarburised bath and already has a name and a retype for it (`IronCode`, `:98`). The change is: make that
terminal the only terminal, rename the product, and let the ladle put the 10.8 u back.

It also deletes the machine's worst gotcha. The 11.7-second over-blow window that
[bessemer § Gotchas #1](../machines/bessemer.md#gotchas) calls the whole difficulty of the converter exists
only because there is a grade to overshoot. Blow to the end every time and there is nothing to time.

---

## Why it is like this

1. The process did not work without it. Bessemer's 1856 patent produced brittle, unforgeable metal; Mushet's
   spiegeleisen addition is what made it an industry. A mod that models the blow and not the addition models
   the failed version of the process.

2. It is the cost of the fast machine, paid in a different currency. The converter's advantage is rhythm - a
   heat every ~9 minutes against the open hearth's ~60
   ([open hearth § the rhythm arithmetic](../machines/open-hearth.md#numbers)). Charging it a downstream
   station and a ferroalloy supply rather than a slower blow keeps that advantage while making it conditional
   on infrastructure. R5: nothing is blocked, the cheap route is punished.

3. It gives the iron-tier machines a permanent job. The recarburiser is a cold blast furnace product
   ([blast furnace § second act](../machines/blast-furnace-cold.md)), so the machine a player builds first is
   still running in the steel era - the same pattern as the helve surviving the steam hammer and the pig beds
   surviving direct charging.

4. Two additives instead of one, because the physics differ. Powdered coke is the obvious "add carbon back"
   item and is wrong here, for a reason the player can be told in one sentence. That asymmetry carries the
   whole chemistry of deoxidation without a new number.

5. Recarburising is the only mandatory step in the suite that exists purely to finish another machine's
   product, which is what makes the Bessemer read as half a process.

---

## Gotchas

1. **Nothing in this loop exists.** Six independent gaps: a `blowniron` metal def
   ([blown iron](../items/blown-iron.md)), a ferroalloy metal def, a ferroalloy burden family, the
   [ladle](../machines/ladle.md) block, a `Roles.Ferroalloy` token beside the five in
   `MaterialRoleDef.cs:49-65`, and the converter retype at `BlockEntityConverterControl.cs:95-99`. The
   shipped tier works today only because it skips the step.

2. **Ordering is load-bearing.** Retyping the blow to blown iron must not land before the ladle does, or the
   steel tier has no product at all - [ladle § Open #2](../machines/ladle.md#open) and
   [blown iron](../items/blown-iron.md) say the same. The safe order is: ferroalloy metal +
   `Roles.Ferroalloy` → ladle → `blowniron` metal → converter retype → strip `bessemersteel`'s tool preset
   (`mods/siex/assets/siex/config/metals/bessemersteel.json`).

3. **A ferroalloy addition moves two numbers.** Ferromanganese is high-carbon by definition
   ([cupola](../machines/cupola.md):61-67), so every recarburising addition is also a manganese addition. A
   model that tracks carbon alone silently accumulates 2–3 % Mn in what it calls mild steel; a model that
   tracks Mn alone (as an [alloying](alloying.md) window would) silently misses the carbon target. The tally
   must carry both from day one.

4. **Blown iron and ingot iron may or may not be the same metal, and nothing says.**
   [alloys](../items/alloys.md) defines ingot iron as *"slag-free, ~0 % C — not wrought iron"* from the
   Bessemer over-blow (later the arc furnace) and routes it through *"recarburise or remelt"*. N1 defines
   blown iron as oxygen-saturated and unusable. Physically the shipped over-blow product is blown iron.
   Either one metal gains a defect, or the catalogue gains a near-duplicate row - decide before either is
   authored ([blown iron](../items/blown-iron.md) carries the same question).

5. **The bath cools while the player fetches the reagent.** A ladle with no cooldown coefficient is a ladle
   that freezes during a walk to the chest. The knob exists on the converter
   (`BessemerCooldownCoefficient` 0.5, `SiexConfig.cs:240`, on `MoltenCooldownSpeed` 24, `IiexConfig.cs:31`);
   the ladle needs its own, and its value is the working window. That number is
   [ladle](../machines/ladle.md)'s to set.

6. **`Roles.Scrap` is the template, and it is a two-line change.** `MaterialRoleRegistry.IsRole(role, stack)`
   (`MaterialRoleRegistry.cs:89`) plus one JSON row in `mods/iiex/assets/iiex/config/materialroles.json` is the entire
   mechanism a hand-dropped ferroalloy needs - the same path `TryChargeScrap` uses
   (`BlockEntityConverterControl.cs:615-661`). No new system.

7. **A converter that pours ready is currently a better machine than the design wants.**
   `bessemersteel.json` ships `generateItemFamily: true`, `itemForms: [ingot, plate, rod, nails]` and
   `tools: { preset: "good" }`. Until that is stripped, adding the recarburising step is a pure nerf with no
   compensating unlock, and it will read as one.

8. **There is no in-game teaching path.** A mandatory step with no handbook page is a wall, and this one has
   the worst possible failure mode: the player pours what looks like steel and it is not.
   `mods/siex/docs/handbook/` ↔ `mods/siex/assets/siex/lang/en.json` is where it goes.

---

## Open

1. **The whole loop.** See Gotchas #1 and the ordering in #2.

2. Settled 2026-08-07: **both reagents, as two distinct items.** Spiegeleisen (~3.8 % C, low Mn) and
   high-carbon ferromanganese (~80 % Mn) - two reagents, two masses, two routes, one arithmetic
   (§ Numbers). The compositions are pinned as identity rows in [alloys](../items/alloys.md); FeMn's own
   carbon fraction is the one number still open ([alloying](alloying.md)).

3. **Is the carbon target 0.2 % or is it a band?** Partly answered 2026-08-07: the reagent split already
   gives one blow two products - mild at ~0.2 % C off the FeMn trim, rail-grade at 0.45–0.65 % C off the
   10–15 % spiegeleisen dose (§ Numbers). What remains open is the identity side:
   [materials.md](../materials.md) has exactly one Bessemer steel row, and a rail-grade product needs its own
   row (or a band on the one row) before the route can retype anything.

4. **Does the step have a failure mode?** Today, as designed, no: the addition is free, small and always
   works. [alloying](alloying.md)'s off-spec → waste-alloy rule ([ladle](../machines/ladle.md)) is the obvious
   candidate - under-recarburise and pour blown iron that is still blown - but nothing says whether the same
   window mechanic applies to carbon.

5. **What recarburises ingot iron when there is no ladle nearby?** [alloys](../items/alloys.md) offers
   *"recarburise or remelt"*, and re-melting is the [cupola](../machines/cupola.md), which produces cast
   iron. That is a downgrade, not a recovery - worth confirming it is intended as the escape hatch.

6. **`MaxAwayCatchupSteps` is unanswered here too.** A ladle holding an un-recarburised heat in an unloaded
   chunk still cools (temperature is vanilla time-based) but does not tick. The
   [Bessemer](../machines/bessemer.md#gotchas) replays nothing at all; a step whose whole cost is a working
   window cannot inherit that silently.

7. **No test would notice any of this.** [bessemer § Tests](../machines/bessemer.md#code) records that no
   converter test asserts a rate as a number - which is how `BessemerPourRate` moved 16 → 44 unnoticed. A
   mandatory chemistry step needs a mass-and-carbon conservation test on day one, modelled on
   `ConverterControlProcessTests.cs:237`.
