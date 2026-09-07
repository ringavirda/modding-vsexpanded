# Alloying

**Status** blocked - designed, nothing exists. No mixing block, no alloy metal, no window catalogue, no
ferroalloy, no grade field. The one code hook that could emit alloys (`MetalDef.Alloy`) is inert on purpose
and must stay so.
**Mods** smex (the open-hearth bath, the ladle) · iiex (the cold blast furnace that smelts the ferroalloys, the
cupola that melts them) · hpex (the consumer - the hadfield material gate)

**Owns** - the facts this page is canonical for:

* the loop end to end: ferroalloy → route choice → mixing site → pour → grade;
* the two mixing sites and which base may use which, including the ruling that Bessemer steel can only be
  alloyed in the ladle, and the code reason the converter can never be a mixing site;
* the route rule: the reagent's element strength sets the addition mass, and the mass decides
  solid-and-free vs molten-from-a-cupola - the same rule [recarburising](recarburising.md) states for
  carbon, applied to the alloying element;
* the ferroalloy supply chain as a chain - which machine makes it, which melts it, at what scale, and what
  one alloying run costs in furnace time;
* the addition-mass arithmetic for every alloy in the catalogue, worked at the ferroalloy strengths the
  supply chain actually delivers (never at pure element);
* D3 as a process rule - alloys inherit their base's grade as a continuous pressure penalty - and the
  finding that no code surface in the suite can express it;
* the alloy roster against the release target: which alloys have a customer, which have no element source;
* the order the pieces must land in.

**Does not own** - cited only, never restated:
[ladle](../machines/ladle.md) - the vessel, mixing by held proportion, resolve-on-pour, the waste-alloy
rule, and the chill model and its arithmetic, the pull-don't-join-the-graph constraint ·
[open hearth](../machines/open-hearth.md) - the bath, D6, the regenerators, the rhythm rule ·
[cupola](../machines/cupola.md) - melting ferroalloys, and why fuel contact makes it the right machine ·
[blast furnace (cold)](../machines/blast-furnace-cold.md) - ferroalloys as a burden family, the second act,
the fuel-against-throughput trade · [Bessemer](../machines/bessemer.md) - the blow, its carbon model, capacity ·
[recarburising](recarburising.md) - the mandatory post-blow step and its own addition arithmetic ·
[materials.md](../materials.md) - every alloy's target composition and the waste-alloy recovery routes ·
[molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md) - merge/split are no-ops, `FlowEdge`'s metal-code refusal ·
[bearings](../machines/bearings.md) - chrome steel's consumer · [pipe network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/pipe-network.md) ·
[heat balance](../mechanics/heat-balance.md) · [conventions.md](../conventions.md) - R2, R3, R5, R7 ·
[STATE.md](../../../../docs/superpowers/plans/STATE.md) - D3, D6, D8, N2

**Depends on** [ladle](../machines/ladle.md) · [open hearth](../machines/open-hearth.md) ·
[cupola](../machines/cupola.md) · [blast furnace (cold)](../machines/blast-furnace-cold.md) ·
[materials.md](../materials.md) · [recarburising](recarburising.md)

---

## What it is

Steel becomes alloy steel when a measured proportion of another element is dissolved into it - manganese for
work-hardening toughness, chromium for hardness and bearing races, tungsten for red-hardness. It is a
weighing operation followed by a pour, with one attempt per heat: manganese cannot be taken back out.

Every element arrives as a **ferroalloy**, never as the pure metal, because manganese and chromium boil or
oxidise before they melt and the industry reduced them with iron in a blast furnace instead. The player
holds ferromanganese, not manganese. The mod drops oxidation losses on addition, recovery percentage, ladle
analysis and tapping temperature spec. What survives is **proportion** (get the ratio right or get
slag-value metal back) and **chill** (a cold addition costs the heat).

[recarburising](recarburising.md) is the same mechanic with the element set to carbon, made mandatory. This
page is the general case.

---

## The loop

```
  cold blast furnace ──▶ ferroalloy burden ──▶ solid ferromanganese / ferrochrome
                                                        │
                            ┌───── small addition ──────┤─── large addition ─────┐
                            │        (solid, free)      │    (must be molten)    │
                            ▼                           │                        ▼
                    hand-drop into the ──────────────┐  │                     cupola
                    mixing site                      │  │                        │ canal
                                                     ▼  ▼                        ▼
    open-hearth heat ─────────────▶  ┌──────────────────────────────┐ ◀──────────┘
                                     │   mixing site                │
    Bessemer heat ──▶ [recarburise] ─▶│   bath (D6) │ ladle (R3)    │
                                     └──────────────┬───────────────┘
                                                    │ pour, held proportions resolve
                                     ┌──────────────┴──────────────┐
                                     ▼                             ▼
                              in every window                 outside any window
                              alloy (grade = base's, D3)      waste alloy (keeps base mass)
                                     │                             │
                              long cell / canal / mold      cupola · Bessemer scrap · arc
```

| # | Machine | Player verb | What comes out |
|---|---|---|---|
| 1 | [cold blast furnace](../machines/blast-furnace-cold.md) | charge a ferroalloy burden; a very high-coke grade, run as a campaign | solid ferromanganese / ferrochrome - no burden family, no metal, no product code exists |
| 2 | — | decide the route by the mass needed (see Numbers) | small → step 4 · large → step 3 |
| 3 | [cupola](../machines/cupola.md) | charge the ferroalloy as remelt charge, tap into a canal | molten ferroalloy on a canal run - no ferroalloy charge variant exists |
| 4a | [open hearth](../machines/open-hearth.md) | RMB a charge door with the ferroalloy, during the heat | the tally moves; the flame holds the bath while the player corrects (D6) |
| 4b | [ladle](../machines/ladle.md) | RMB with the solid ferroalloy, or pull the molten one off the second canal | the tally moves; the bath takes the chill |
| 5 | either site | read the live composition (R7) and top up | — |
| 6 | either site | pour | the alloy, or waste alloy - the window rule is [ladle](../machines/ladle.md)'s |
| 7 | [cupola](../machines/cupola.md) / [Bessemer](../machines/bessemer.md) | recover a botched mix | cast iron / Bessemer steel - routes are [materials.md](../materials.md)'s |

### Two mixing sites, and which base may use which

D6 settles that the bath is primary and the ladle is the second option
([open hearth](../machines/open-hearth.md)). The choice is not always free:

| Base | Bath ([open hearth](../machines/open-hearth.md)) | Ladle ([ladle](../machines/ladle.md)) |
|---|---|---|
| open-hearth steel | primary - a long heat is a bath that can be held and corrected | available |
| Bessemer steel | impossible - see below | the only route |
| cast iron | no - the cupola has no holdable bath either | available (and waste recovery is a remelt, not an alloying run) |
| copper / bronze | no | available - deferred with all non-ferrous ([STATE.md](../../../../docs/superpowers/plans/STATE.md) D8) |

The converter cannot be a mixing site, and the shipped code says so twice.

1. It refuses solid additions into a finished heat. `TryChargeScrap` accepts cold charge only onto an empty
   vessel or a raw pig heat, never into finished steel (`BlockEntityConverterControl.cs:631-635`). The one
   hand-drop port the converter has is closed exactly when an alloying addition would be made.
2. There is no cool half of its heat. `BessemerRefineTemperature` is 1500 °C (`SiexConfig.cs:194`) and that
   is numerically equal to Bessemer steel's own melting point
   (`mods/siex/assets/siex/config/metals/bessemersteel.json`), so the converter has no "hold and correct" band at all;
   the moment the blast stops it is in the freeze window
   ([bessemer § Gotchas #2](../machines/bessemer.md#gotchas)).

> Bessemer steel must therefore be [recarburised](recarburising.md) downstream and alloyed downstream. Both
> in the same ladle, on the same heat, in that order.

### The route rule - strength sets the mass, mass sets the route

| Addition | Route | Consequence |
|---|---|---|
| small - a few per cent of the heat | solid lumps, hand-dropped | free; the heat absorbs the chill |
| large - ~10 % and up | molten, poured from a [cupola](../machines/cupola.md) down a second canal | needs a cupola, a canal run and timing |

It needs no gate and no check: the arithmetic that draws the line is the chill term, and it belongs to
[ladle](../machines/ladle.md). This page owns only the consequence:

> Recarburising is solid and free. Hadfield needs a cupola. The difference between making steel and making
> alloy steel is an infrastructure requirement, never an unlock - R5 exactly, since the cold route is
> punished rather than blocked.

The cupola is the right machine for a physical reason: in a cupola the fuel and the metal touch, so the
charge carburises - ruinous for tool steel, and free for ferroalloys that are high-carbon by definition
([cupola](../machines/cupola.md)).

---

## Inputs and outputs

One settled converter heat: 6000 u of pig → 5400 u of metal (`BessemerSteelYield` 0.90, `SiexConfig.cs:221`;
derivation at [bessemer § Numbers](../machines/bessemer.md#numbers)). Reagent strengths are the settled
identity rows - see Numbers for where each comes from.

| Run | In | Mass | Out | Mass |
|---|---|---|---|---|
| hadfield, Bessemer base | blown iron | 5400 u | — | — |
| | ferromanganese (80 % Mn, high-C) | ~1000 u | hadfield steel | ~6400 u |
| | (separate [recarburiser](recarburising.md), if the FeMn's carbon does not cover it) | 0–284 u | (pushes the run to ~6740 u - see Numbers) | |
| hadfield, open-hearth base | open-hearth steel in the bath | 5400 u | hadfield steel | ~6400 u - and a better grade (D3) |
| any, off-spec | base + additions | as above | waste alloy | keeps the full base-metal mass |

Alloying is 1:1 under R2 ([conventions](../conventions.md)) - a forming step, not a refining one. Nothing is
burnt off and nothing is declared as loss; the additions join the product, so every alloy heat is bigger
than the base heat that started it. A botched run costs the additions and the time, never the underlying
iron ([ladle](../machines/ladle.md), the waste-alloy rule).

---

## Numbers

Everything here is derived or proposed - there is no alloying config section, no key and no code. Each
derivation names the cited value it starts from.

### The addition-mass equation

For a target window fraction `w` (of the finished alloy) and a reagent carrying the element at fraction `r`,
added to a base of `M` units carrying none of it:

```
m · r = w · (M + m)        ⇒        m = w · M / (r − w)
```

Hadfield's `w` is 0.125 ([alloys](../items/alloys.md)), `M` = 5400 u:

| Reagent | `r` | `m` | Final heat | as % of the base |
|---|---|---|---|---|
| ferromanganese, the settled strength (2026-08-07) | 0.80 | 1000 u | 6400 u | 18.5 % |
| ferromanganese, low grade | 0.70 | 1174 u | 6574 u | 21.7 % |
| pure manganese - a control row; the supply chain never delivers it | 1.00 | 771 u | 6171 u | 14.3 % |

Where 80 % Mn comes from: the settled design figure is "6000 u of hadfield needs ~940 u of FeMn". Solving on
the product basis, `6000 × 0.125 = 750 u` of Mn, and `750 / 940 = 79.8 %` - a real high-carbon
ferromanganese grade. Settled 2026-08-07: the composition is pinned as an identity row in
[alloys § Pinned identities](../items/alloys.md), alongside spiegeleisen's ~3.8 % C, and the two are
distinct items, ~10× apart in Mn strength ([recarburising](recarburising.md)).

### The chill arithmetic must be worked at ferroalloy strength, never at pure element

The blast furnace makes ferromanganese, not manganese
([blast furnace § second act](../machines/blast-furnace-cold.md)), so the hadfield addition is 1000 u - 30 %
heavier than a pure-element figure would suggest. Through the ladle's own formula and constants (`L_f/c_p` =
550 °C):

| Row | `m` | `T_mixed` | chill | `T_new` | vs. 1500 °C melting point |
|---|---|---|---|---|---|
| pure Mn (the control), bath at 1800 | 771 u | 1577.5 | 68.8 | 1509 °C | survives by 9 °C - an artefact of an input the chain cannot deliver |
| at the settled `r` = 0.80 | 1000 u | 1521.9 | 85.9 | 1436 °C | frozen |

"Hadfield needs a cupola" is therefore unconditional, not merely a matter of speed: the solid route freezes
even a 1800 °C bath, and the bath a 1000 u cold charge could survive (~1876 °C) is above the converter's own
`T_process` ceiling. The [ladle](../machines/ladle.md)'s worked table carries the same rows. A fast-hands
feature has to be bought explicitly, by lowering `LadleSolidChillC`, not by working the table at pure
element.

### On a hadfield run the ferroalloy is also the recarburiser

High-carbon ferromanganese carries carbon as well as manganese, so a 1000 u addition into 6400 u delivers:

| FeMn carbon fraction | Carbon in | Final % C | vs. hadfield's ~1.2 % target ([alloys](../items/alloys.md)) |
|---|---|---|---|
| 0.05 | 50 u | 0.78 % | short - a top-up is needed |
| 0.07 | 70 u | 1.09 % | within a whisker |
| 0.08 | 80 u | 1.25 % | slightly over |

For hadfield the two steps collapse into one addition, which is what high-carbon FeMn was for. That holds at
only one end of the reagent band: at 5 % C the run needs a separate [recarburising](recarburising.md)
addition as well. The reagent's carbon fraction is therefore a design lever that decides whether hadfield is
one operation or two, and nothing has chosen it.

### What one heat does to the ladle's capacity

| Stage | Mass | Against the proposed `LadleCapacity` 6000 u ([ladle](../machines/ladle.md#numbers)) |
|---|---|---|
| blown iron off one settled heat | 5400 u | fits |
| + [recarburiser](recarburising.md), if separate | 5559–5684 u | fits |
| + hadfield's ferromanganese | ~6590–6740 u | over by 10–12 % |

[ladle § Open #3](../machines/ladle.md#open) flags the sizing question but computes 6171 u - from pure
manganese and with no recarburising step. The real figure is ~500 u higher. Either the ladle is sized for
base + both additions (≈ 6800 u), or a hadfield heat is mixed in two goes.

### Ferroalloy supply - what one alloy run costs upstream

| Quantity | Arithmetic | Result |
|---|---|---|
| FeMn per plain steel heat (recarburising) | [recarburising § Numbers](recarburising.md#numbers) | 159–284 u |
| FeMn per hadfield heat | `0.125 × 5400 / 0.675` | ~1000 u - 3.5–6× a plain heat |
| does one addition fit one cupola pool? | 1000 u vs the cupola's molten-cast-iron ceiling ([cupola § Numbers](../machines/cupola.md#numbers)) | yes - one pool, one addition, a clean unit |
| time to melt it | 1000 u ÷ the cupola's nominal rate ([cupola § Rates](../machines/cupola.md#rates)) | ≈ 5½ minutes |
| blast-furnace time | minutes of furnace time, not hours - a reference campaign is ≈ 11½ minutes ([ironmaking](ironmaking.md) § Derived) | a campaign, not a supply line |

### D3 has no code surface - pressure is a property of the mod, not the metal

> D3 ([STATE.md](../../../../docs/superpowers/plans/STATE.md)): alloys inherit their base's properties as a continuous penalty, not
> a lockout - critical machinery built from lesser steel gets a lower max pressure.

Every pressure ceiling that exists in the suite:

| Rating | Value | file:line | Keyed by |
|---|---|---|---|
| `BlockPipe.BurstPressure` | resolved at runtime | `exlib/src/Blocks/Networks/BlockPipe.cs:193-194` | the block's `Code.Domain`, via `_burstByDomain` (`:180`) |
| default, if a mod registers none | 5 atm | `BlockPipe.cs:182` | — |
| iiex plated | 2.5 atm | `IiexConfig.cs:163` | domain `iiex` |
| iiex cast | 5.0 atm | `IiexConfig.cs:50` | domain `iiex` |
| hpex rolled | 12 atm | `SiexConfig.cs:115` | domain `hpex` |

That is the whole list - `grep -n "MaxPressure\|BurstPressure\|SafetyPressure"` over `IiexConfig.cs` and
`SiexConfig.cs` returns those two burst keys and nothing else. No boiler, cylinder or vessel has a
material-keyed pressure limit at all, and `MetalDef` carries `IsAlloy` (`MetalDef.cs:57`) and `Alloy`
(`:71`) but no grade field of any kind.

D3 is therefore currently inexpressible. A pipe's rating comes from which mod's block was placed, not from
what metal it is made of. A hadfield pipe mixed on a Bessemer base and one mixed on an open-hearth base are
the same block with the same rating.

The narrow fix keeps the shape of the rule: give `MetalDef` a grade scalar, let a mixed alloy inherit
`min(base.grade, …)`, and make `BurstPressure` read the segment's metal with the domain value as the
fallback. `BlockPipe.RegisterBurst` (`BlockPipe.cs:185`) is already an indirection, so the seam exists.

### The alloy roster against the release target

| Alloy | Base | Element(s) | Element source | In the ferrous target? |
|---|---|---|---|---|
| hadfield | mild steel | ~12.5 % Mn | ferromanganese - cold blast furnace | yes - it is hpex's material gate |
| chrome steel | per D3 - any steel base, graded | ~1 % C, ~1.5 % Cr (defined 2026-08-07) | ferrochrome - cold blast furnace; one route, via the [ladle](../machines/ladle.md) | yes ([bearings](../machines/bearings.md), N2) - its identity row is [alloys § Pinned identities](../items/alloys.md) |
| HSS | open-hearth | ~18 % W + ~4 % Cr | tungsten has no source anywhere - the ferroalloy family is FeMn / FeCr / FeSi ([STATE.md](../../../../docs/superpowers/plans/STATE.md)) | no - elex, deferred (D8) |
| tin bronze / brass / bismuth / black bronze | copper | Sn / Zn / Bi / Au+Ag | tilting crucible (designed) | no - non-ferrous, deferred (D8) |

Inside the current release target, alloying has exactly two customers: hadfield and chrome steel, both
defined. Chrome steel's minimal row landed 2026-08-07 so [bearings](../machines/bearings.md) and N2 stop
waiting on it.

---

## Why it is like this

1. Off-ratio yields waste, and never snaps. Vanilla's `AlloyRecipe` maps a ratio band onto a product, so
   a sloppy mix quietly becomes the nearest alloy; here it becomes waste that keeps the base mass. A botched
   run costs the additions and the clock, never the iron. The rule, the recovery routes and the mechanic are
   [ladle](../machines/ladle.md)'s; the reason `MetalDef.Alloy` must stay inert is
   [alloys](../items/alloys.md)'s and the ladle's.

2. Chill instead of a gate. Nothing forbids a large cold addition; it takes the bath under its melting
   point and the pour is lost - R5, with the model producing the ruling. It also needs no new mechanism: a
   cupola, a canal and a ladle already exist, so melting the ferromanganese in a cupola and running it to
   the ladle is zero new code and historically exact.

3. Two sites, because the two steelmakers differ in rhythm and not in quality. A long open-hearth heat is
   a bath that can be held, watched and corrected. The converter's heat is over in five minutes and cannot
   be held, so its product is corrected downstream - the historical division: Bessemer plants ladled,
   open-hearth plants alloyed in the bath.

4. D3 is a penalty, not a lockout, because a lockout would delete the choice. If hadfield needed an
   open-hearth base, the [open hearth](../machines/open-hearth.md) would simply be required and the
   [Bessemer](../machines/bessemer.md) a stepping stone. As a continuous penalty, hadfield-on-Bessemer works
   and only holds less pressure, so a player with only a converter still reaches hpex, and a player who
   builds the hearth gets a better machine out of the same recipe. One rule instead of a table.

5. The ferroalloys come from the machine the player already built. No new smelter and no new mechanism: a
   new burden family through the [cold blast furnace](../machines/blast-furnace-cold.md), the same pattern
   that already produces the [cupola](../machines/cupola.md)'s charge. It keeps the tier-1 machine alive
   permanently, as the helve survives the steam hammer and the pig beds survive direct charging.

---

## Gotchas

1. Nothing exists. The mixing block, the ferroalloy metals, the ferroalloy burden family, the window
   catalogue, the `Roles.Ferroalloy` token (there are five roles, `MaterialRoleDef.cs:49-65`), the grade field,
   and every alloy metal def. `assets/*/config/metals/` holds four metals in total.

2. A ladle that joins the molten graph cannot alloy. Merge and split are no-ops and `FlowEdge` refuses a
   transfer into a cell holding a different metal code
   ([molten network](https://github.com/ringavirda/exlib/blob/main/docs/design/mechanics/molten-network.md)), so a graph-joined ladle would be refused its own
   second input by the network it exists to merge. It must pull from neighbours by code. This is
   [ladle](../machines/ladle.md)'s constraint and it is the one mistake that takes the feature back to zero.

3. Every ferroalloy addition moves two numbers. FeMn is high-carbon by definition, so a manganese
   addition is a carbon addition. A window model that tracks only the alloying element will let a player hit
   12.5 % Mn while silently landing anywhere between 0.78 % and 1.25 % C - see Numbers. The tally must be
   per-element from the first line of code.

4. Two mixing sites must not become two alloy systems. [open hearth § Open #3](../machines/open-hearth.md#open)
   and [ladle § Gotchas #4](../machines/ladle.md#gotchas) both ask for this and neither owns it, so it is
   stated here: one window catalogue, one readout format, one resolver, two call sites.

5. Where the windows live is undecided and it is a source-of-truth question. A JSON catalogue beside
   `config/metals/` matches how metals already load; but the targets currently live in
   [alloys](../items/alloys.md), a doc rather than data. Both cannot be canonical.
   ([ladle § Open #4](../machines/ladle.md#open) raises the same.)

6. `LadleAlloyTolerance` as one number for the whole table is a real design choice, not a placeholder.
   [ladle](../machines/ladle.md) proposes ±0.02 mass fraction for every alloy. At hadfield's 12.5 % that is a
   ±16 % relative band and forgiving; at HSS's 4 % Cr it is ±50 % and meaningless. A single absolute
   tolerance does not scale across the catalogue.

7. The chill has a second clock the arithmetic does not show. The bath cools on its own while the player
   fetches the addition, and for a large addition the cupola melt itself takes ~5½ minutes. The real question
   is not "does 1000 u of cold FeMn freeze the heat" but "is the cupola already tapped when the converter
   pours" - a plant-layout problem.

---

## Open

1. The whole loop. See Gotchas #1. Order: ferroalloy metal + burden family → `Roles.Ferroalloy` → the
   window catalogue and resolver → the [ladle](../machines/ladle.md) → the bath call site → the alloy metals →
   the grade field.

2. Decide how D3 is expressed, or drop it. As written it needs a per-material grade and a
   material-aware pressure read (Numbers). The cheap version is one scalar on `MetalDef` and one change in
   `BlockPipe.BurstPressure` (`exlib/src/Blocks/Networks/BlockPipe.cs:193-194`); the expensive version
   is a grade on every pressure vessel in iiex and hpex. Nothing else in the suite is waiting on this.

3. Size the ladle for base + both additions. ~6800 u, not 6000 (Numbers). This must be settled together
   with [bessemer § Open #2](../machines/bessemer.md#open) and [ladle § Open #3](../machines/ladle.md#open),
   not after either.

4. Pin the ferroalloy compositions - partly settled 2026-08-07. The element fractions are pinned as
   identity rows in [alloys § Pinned identities](../items/alloys.md): spiegeleisen ~3.8 % C, low Mn;
   FeMn ~80 % Mn. Still open: FeMn's own carbon fraction, which decides the residual carbon and whether
   hadfield is one operation or two (§ Numbers).

5. Does bath alloying use the same chill model? A fired bath is being heated, so a cold addition there
   should be a melt-speed cost rather than a lost pour ([open hearth](../machines/open-hearth.md) already
   argues scrap that way). If so the open hearth's advantage is not only D3's grade - it is that the chill
   route rule does not apply to it at all, a bigger asymmetry than D6 currently states.

6. Nothing rewards a second cupola or a second ladle, the same gap
   [cupola § Open #6](../machines/cupola.md#open) records. A hadfield heat needing a full cupola pool is the
   first thing in the suite that could make a bank mean something - one cupola per addition.

7. Is the alloying element ever recovered? Waste alloy recovers the base ([materials.md](../materials.md)
   :111-117); the manganese in it is written off. For a 1000 u addition that is a large, invisible loss, and R2
   asks for shortfalls to become something the player can see.

8. No handbook page and no teaching path. Hadfield is the suite's introduction to the alloying
   mechanic; an introduction that nothing explains is a wall - and this mechanic's failure mode (waste
   alloy) is silent until the pour.
