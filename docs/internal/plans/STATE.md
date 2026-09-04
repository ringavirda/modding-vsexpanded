# STATE — status and open decisions

Last updated 2026-09-04. **This file owns two things and nothing else: *status* and *open decisions*.**
It is the answer to "what is actually true right now". It does not own numbers, mechanics or lore — those
live on the entity pages — and it does not own sequencing, which lives in `docs/internal/plans/` with
[NEXT.md](NEXT.md) as the entry point.

---

## Blockers — the game does not work without these

The list keeps its original B-numbers because other pages cite them. Numbers absent from the table are
fixed and no longer carried; line citations are kept only where re-verified — where the source has moved,
the class name is the anchor.

| # | Blocker | Evidence |
|---|---|---|
| ~~B3a~~ | ~~**The mill's feed path rejects fresh stock.**~~ **Fixed 2026-08-11.** `WorkPiece.FromStack` now falls back to `stack.Collectible.Attributes` when the stack tree carries no `stockForm`, so fresh stock off the grid is a work piece. Stack state still wins once a piece has been rolled | `WorkPiece.FromStack` |
| ~~B3b~~ | ~~**The mill never produces its product.**~~ **Mechanism built 2026-08-12.** `ClaimFinishedPiece` calls `OutputAt` from `CompletePass`: an even piece whose stage names an output is swapped for that item, heat carried across; otherwise it ejects stock for the shear. All four stale codes deleted (two contradicted settled rulings). ⛔ **The remaining gap is the key, not the wiring**: `Outputs` is keyed on gap alone, and flat 1.0 yields `nailplate` from a `rolledrod` but plate from a bloom — the key must become **(form, gap)** before the catalogue can be written | `BlockEntityRollingMill.ClaimFinishedPiece`, `RollSetSpec.Outputs` |
| ~~B3c~~ | ~~**No rolled product item exists.**~~ **Closed 2026-08-14, and all six are obtainable.** `RolledItemDefinitions` ships `rod`, `nailplate`, `beam`, `blank`, `skelp` and `boilerplate`; two crop tables plus one mill-claimed stage make **ten** routes work: bar → rod ×4, beam ×2 or plate ×2; slab → boilerplate ×2; billet → beam ×3 or plate ×3; bloom → blank ×5 or skelp ×5; cast slab → boilerplate ×5; and the fork — a vanilla `game:rod-iron` admitted at the deck becomes 4 × `rivetrod` grooved or 1 × `nailplate` flat. ★★ **Both tiers of the forming line produce a finished product, and both benches downstream have their input.** ⛔ Two declared rows stay unreachable on two blockers — the billet's grooved 2.25 and the bloom's wide 3.0 both need the **mid-gap crop** rule, since each yields pieces that still want a pass; `heavyplate` waits on M.8. See rolled-parts.md § *What is reachable today* | `docs/design/items/rolled-parts.md` |
| ~~B4~~ | ~~**`grooved` cannot bite fresh stock.**~~ **Fixed 2026-08-12.** Gaps are `[2.5, 2.0, 1.5, 1.0]` (owner's numbers; the groove bottoms at 1.0), every step a 0.5 draft inside δ_max 1.0. Outputs are now `[]` — both stages are shear crops. Guarded behaviourally by `ShippedRollSetTests` — every shipped ladder must be walkable from its accepted form's fresh thickness, which a golden alone never checked | `RollSetItemDefinitions` |
| **B5** | **siex's rolled-pipe tier is uncraftable.** Four live blocktypes, four shapes, zero recipes — re-confirmed 2026-08-14 by enumerating every recipe output across the three mods: not one names `pipe-rolled-*` | `siex/Recipes/` carries machine recipes only |
| **B6** | **The "mandatory" pressure valve cannot be installed on an HP line.** It is cast-tier (flanged, `BlockPressureValve.cs:39`), so welded rolled pipe refuses to couple it. Since M4 the tier is a variant, so a rolled valve is now expressible — it needs a shape, a recipe and a name, not a framework change; and its gate clamps below the Cornish engine's engage pressures. The shape exists since 2026-08-24 (`assets/editable/shapes/networks/pipe/pipe-block-rolled-pressurevalve.json`, derived from the owner's cast one); the block def, recipe and lift trigger are still owed | `BlockEntityPressureValve`, `SiexConfig` |
| ~~B15~~ | ~~**The crucible furnace (designed) cannot reach crucible-steel heat**~~ **Fixed 2026-08-22 (U9.9).** The shortfall was never in the draught curve: two per-machine losses close it — transfer loss 0 (the pots stand in the fire) and charge loss 50 (the charge is walled off from it). Measured 1604 °C at six courses, 1621 at the nine-course peak, pinned by `EveryFireboxReachesItsOwnProcessTemperature` | `BlockEntityFurnaceCore`, `CrucibleFurnaceLayout` |
| ~~B17~~ | ~~**The mill's deck can only reach 2 of 4 gap zones.**~~ **Fixed 2026-08-12.** `IsInputDeck` now accepts the whole `MillFeed.DeckCells` row rather than the single cell beside the stand, so a click reaches the full barrel. It was worse than recorded: for **any** gap count above one the widest gap — the only one fresh stock can enter at — was unreachable, since the reachable span was the last third | `BlockEntityRollingMill.IsInputDeck`/`DeckRow` |
| **B18** | **A refused pipe joint does not leak.** `ClassifyOpenings` counts an open face as a leak only when the neighbour block is air, so a welded segment butted against a cast one produces no leak, no warning and no signal — B6 is silent in play | `PipeNetwork.ClassifyOpenings` (exlib) *(re-verified 2026-08-07)* |
| **B19** | **The cast pipe segments have no recipe at all** — the cast tier is creative-only, and the source comment admits it. Sharpened 2026-08-14: it is the four **plain segments** (straight, bend, tjunction, xjunction) that have none. The cast tier's *fittings* — passthrough, passthroughbend, valve, pressure valve — are all craftable, so the tier reads as half-built rather than absent, and B23's stage 3 now names a real block a player still cannot make. The designed route is the horizontal bore finishing cast pipe-parts, i.e. the machining line ([roadmap](2026-09-04-roadmap.md) Phase 2) | `iiex MachineRecipeDefinitions` |
| **B20** | **The Watt engine costs no gear.** Its pattern `_H_,PRP,PIP` has no `G` cell while the def declares a gear ingredient, so the gear is free | `lpex MachineRecipeDefinitions.WattEngine` *(re-verified 2026-08-07)* |
| **B21** | **Sub-machine → engine lookup is off by 90° in every orientation**, so every sub-machine binds through a fallback loop that never verifies the engine points back — two engines two cells apart can cross-bind | `BlockEntityEngineSubmachine` |
| ~~B23~~ | ~~**The Bessemer vessel cannot be built at all — in survival or creative.**~~ **Fixed 2026-08-14** by M.6's resolution guard, which found it on its first run. Stage 3 now requires `iiex:pipe-cast-straight*` — the trailing-star wildcard the three gas-intake recipes already use, not one orientation, so a segment placed the other way round still counts. ⛔ Only the hard wall is gone: B19 still leaves that segment uncraftable, so the vessel is completable in creative-instant and not in survival | `BlockConverterBessemer.cs:107-117`, `ExConstruction` |
| **B24** | **The rolled-joint test is a tautology.** It asserts `OpeningsCount > 0`, which comes from the rig's unsealed free end, not from the refused joint — so it passes identically whether the joint couples or not. B18 is exactly the bug it was written to catch | `RolledJointTests` |

Fixed and dropped from the table: B1/B7 (iwex recipes reaching into lpex — iwex ships its own pipe and
gear), B2 (withdrawn — the 1420 °C stall is the taught coke trade, named by the furnace every tick; the
residual is band tuning), B8 (closed on all five counts 2026-08-21 with U6 — the puddling furnace runs a
whole heat and its structure completes; the fifth count was a firebox inheriting the shaft's flat
disruption floor), B9–B11 (molten flow and blast-draw order), B13 (the hearth parts ship their own filler
footprints), B14 (the casting bed carves on the server), B16 (hopper columns rotate with the structure),
B22 (the hpex migration names its two extracted paths literally, with a coverage guard test), B23 (M.6,
2026-08-14).

**Not blockers, but the largest open fact (2026-09-04):** nothing built since U4.4 — the hearth cells,
taps and plug, blow-in, puddling, reheat, the mill's rolled catalogue, the shear, the fastener benches,
the storage rack, the workbench, the coke oven, the crucible furnace and the Cornish boiler megablock —
has been seen in game. Every one of them is green in the harness and unwalked. Roadmap Phase 1.

**B12 is now measured rather than carried as a question** — cast stock landed 2026-08-14, and the length
is exactly what the design predicted: `castbloom` at its 1.0 rung is **8 × 1 × 50**, past the 48-voxel
crosswise seating, so a bloom rolled straight there cannot be reheated. It is a cost rather than a lock,
because the shear does not need heat — the piece still crops into five `skelp` at extra drive — and the
clean escape, the wide 3.0 crop into five, is one of the two rows the mid-gap crop rule blocks. `castslab`
has the same shape of hole at 2.0 pending M.8. Owned by
[recoverability](../../design/mechanics/recoverability.md).

### Other known defects

* **The ×3 literal is in both sub-machines**: it makes the fluid pump **15 L/s** (documented 5) and smex's
  blower **43.2 L/s** (documented 14.4). It survived because no test asserts any pump's rate as a number —
  they all check only "water moved / did not move".
* **The steam hammer's drawn mesh does not fit its declared footprint**: 57 voxels tall against 48 for
  three cells, and the `HandLeaver` reaches into the west neighbour. All four clips are authored `EaseOut`
  where `hammerhit` must `Repeat`.
* **The density rule is not yet applied to casting.** The shipped mold cavities use inconsistent implicit
  densities, and the `materialUnits` JSON attribute is written on every item and never read by any mod
  code — machines read the C# constants. The settled masses land as one batch
  ([economy-landing](../../design/items/economy-landing.md)).
* **Molten throughput is still not one number.** The taps read `TapDrainPerTick = 50` and the canal edge
  is 50, but the bed/cell pull is a hard-coded 25 and the furnace hand-down a hard-coded `min(20, pool)`,
  and the mold pedestal has no rate cap at all. D5b wants 50 u/s everywhere, from config.
* **N1 is contradicted by shipped data.** `bessemersteel.json` ships `generateItemFamily: true` **and**
  `tools: {preset: "good"}` *(re-verified 2026-08-07)* — so Bessemer steel currently makes pickaxes and
  knives, against the blown-iron ruling.

---

## The ladder — what a player actually does

**live** = built and green · **walked** = seen in game · **shell** = block stands, logic missing ·
**designed** = spec only · **art** = drawn, not wired. A station marked *live* but not *walked* has
never been seen by a player.

### Tier 1 — iiex (iron, water + cast iron)

```
coal ──▶ beehive coke oven [live] ──▶ coke
ore ──▶ crusher ──▶ burdenmaker [live, walked] ──▶ burden ───┐
                    (lime in, no power)                      ├──▶ tall hopper [live, walked]
coke ────────────────────────────────────────────────────────┘
                                                                            │
   twin-tub MP blower [live, walked] ──blast──▶ COLD BLAST FURNACE [live] ◀──┘
                                            │ molten canal [live, walked]
                                            │ hearth cells · taps · clay plug · blow-in [live, U4.4–4.9 unwalked]
              ┌─────────────────────────────┼──────────────────────────────┐
              ▼                             ▼                              ▼
      pig beds [live, walked]      cupola [live, walked]           (siex: converter)
      pigs 375u                    remelt ──▶ cast iron
              │                             │
              │                    sand cells + long cell [live]
              │                             ▼
              │                    CAST PARTS — castplate-heavy [live]; frames / cylinder /
              │                    gear blanks / axle / flywheel part [ART ONLY, no consumer]
              ▼
      puddling furnace [live] ──▶ wrought balls ──▶ helve shingling [live] ──▶ shingledbar
                                                                            │
      reheat furnace [live] ──soak──▶ ROLLING MILL [live, creative-walked] ◀┘
                                            │   roll sets ship but are NOT CRAFTABLE (lathe-turned by ruling)
              ┌──────────────┬──────────────┼───────────────┐
              ▼              ▼              ▼               ▼
        rod (vanilla)      beam          plate      wide: flatwide movable roller [not built]
              │
        ┌─────┴─────┐          ← the rod fork: same 4 feeds, same 100 u, either way
        ▼           ▼
   grooved 1.0   flat 1.0
   rivetrod      nailplate
        │           │
   riveter       nail cutter   [live]   ──▶ rivets · nails-and-strips
                                        shear [live] owns every crop · storage rack [live, renderer unverified]
                                        workbench [live] · crucible furnace ──▶ crucible steel [live]

   MACHINING LINE — lathe · horizontal bore · shaper · planer · drill press [DESIGNED; nine shapes drawn]
   The missing supplier: roll sets, cast pipe segments, gears beyond the hand-assembled one, cylinders.
```

**Side choices at this tier:** cast parts vs wrought parts · rivets vs nails per rod · which burden grade.

### Tier 2 — iiex (steam)

```
Cornish boiler megablock [live] ──▶ Watt engine [live, walked on the OLD art] ──▶ MP + cast pipes [live; segments uncraftable, B19]
   normal-plate 4 atm variant [ART]     fluid pump · air blower · planetary [live; polished art NOT wired]
   gauges, safety valve [ART]           horizontal engine · jet condenser · injector · indicator [ART, no code]
                                        MP pump / MP blower, wooden and cast tiers [ART sketches v2]
              ┌─────────────────────┼──────────────────┐
              ▼                     ▼                  ▼
      STEAM HAMMER [designed;   WIDE HALL          boring machine ──▶ folded into the machining line
      owner's shape]            4 mills [needs the
      6 balls → shingledslab    flatwide roller]
      + STAMPING
              │                     │
              ▼                     ▼
      boilerplate ──stamp──▶ 3 × metalplate    heavyplate [M.8 parked]
```

### Tier 3 — siex (steel)

```
hot blast furnace [live, walked] + cowpers [live, walked; gas-fired remake ruled] ──▶ molten pig
                                   │
                    ┌──────────────┴──────────────┐
                    ▼                             ▼
          BESSEMER [live, walked;        OPEN HEARTH [designed] ◀── gas producer [designed; four gaps]
          creative-only until B19]
                    └──────────────┬──────────────┘
                                   ▼
                            ladle [designed] ── alloying · FeMn · hadfield [designed; no metal in code]
                                   ▼
                        long cell ──▶ castbillet · castbloom · castslab [live] ──▶ iiex mill + shear [live]
                                   ▼
                        bending roller [designed] ──▶ skelp ──▶ rolled pipe [blocks live; no recipe, B5]
```

**Side choices:** Bessemer vs open hearth (by what the product must guarantee) · direct-charge vs pig beds.

### Tier 4 — siex, high pressure · Tier 5 — elex

The Lancashire boiler and the Cornish engine are live and walked (published as hpex), the four rolled
pipe blocktypes are live, and the tier is **blocked by B5 and B6**; its material gate (hadfield) **does
not exist in code**. The Cornish engine's pumping-only redesign and the tandem Corliss are ruled and their
sketches shelved. elex is **deferred**; [scope.md](../../design/scope.md) owns the deferral ruling and
its carve-outs — this file keeps only the status word.

---

## Settled decisions

Short dated statements; the reasoning lives on the owner pages.

| # | Settled | Ruling |
|---|---|---|
| **D1** | 2026-07-29 | Pig mass **375 u** (5 × 3 × 10 vx³ under R9). Full bed **7500 u**; puddling charge = 9 pigs = **3375 u**. Shipped (`ItemPig.PigUnits`) — [pig](../../design/items/pig.md) |
| **D2** | 2026-07-29 · **amended 2026-08-15** | ~~Cast plate **`castplate` 10 × 2 × 10 = 500 u**~~ — ⛔ **struck by the owner: `castplate` is simply the old name of `castplate-heavy`, and there was never a second item.** No 500 u plate exists or should; the cast plate is **600 u** at 10 × 2 × 12, which the redrawn art now carries and which matches the rolled one exactly. What survives of D2 is its real clause: every cast structural part gets a fabricated substitute (see N3) — [cast-parts](../../design/items/cast-parts.md). Its other clause — "the rolled plate is a separate item" — is superseded by M5 |
| **D3** | 2026-07-29 | **Alloys inherit their base's properties** as a *continuous penalty*, never a lockout: critical machinery built from lesser steel gets a lower max pressure — [alloys](../../design/items/alloys.md) |
| **D4** | 2026-07-29 | Converter capacity **6000 u** — see Open for the pig-vs-steel accounting |
| **D5b** | 2026-07-29 | **One number for the whole molten network: 50 u/s** — tap, bed pull and canal, because it is the same canal. Two sites are still hard-coded (see defects) |
| **D6** | 2026-07-29 | Alloying happens **in the open-hearth bath, primarily**; the ladle can do it too — a second option, not a replacement |
| **D7** | 2026-07-29 | **Wire exists, in elex.** The iron-tier rejection stands |
| **D8** | 2026-07-29 | **Non-ferrous later.** The release target is the complete ferrous line — owned by [scope.md](../../design/scope.md) |
| **D9** | 2026-07-29 | **Crucible steel is the tool/weapon reward** — Huntsman 1740, iwex, one pot at a time, built in banks. Vanilla `game:steel` (shear steel) becomes crucible feedstock — [crucible-furnace](../../design/machines/crucible-furnace.md) |
| **N1** | 2026-07-29 | **Blown iron**: Bessemer steel is structural, never tool-grade — [blown-iron](../../design/items/blown-iron.md) |
| **N2** | 2026-07-29 | **Bearings are in**, hpex tier only, required by HP machines: chrome-steel balls + a rolled ring race. Earlier machinery abstracts journal bearings into build cost — [bearings](../../design/machines/bearings.md) |
| **N3** | 2026-07-29 | **All cast parts get fabricated substitutes** — beam + plate + rivets for frames, bent shells on the bending roller for shells/barrels/rims. The HP hammer's frame is in tension and is the first that **must** be fabricated — [fabrication](../../design/processes/fabrication.md) |
| **E1–E4** | 2026-08-12 | **Extensibility is a product target.** Other mods must be able to add tooling to diagram crafting, sand casting, rolling, the steam hammer and the machining line. Both routes (JSON attributes primary, public C# registration API alongside); the die carries its job spec; **every spec attribute is versioned**; and this layer lands **before** the tier spine — the schemas are a public contract, so breaking changes go first. Plan: [extensibility](2026-08-12-extensibility.md) |
| **F1** | 2026-08-12 | **Framework composition A0–A4 complete.** Form / process / membership are three independent axes; the block-entity base slot belongs to form. [staging](2026-08-10-framework-composition-staging.md) · [design](../../design/mechanics/framework-composition.md) |
| **M1** | 2026-08-13 · **BUILT 2026-08-14** | **Two content mods, split on the tier line.** Both merges are done: `{iwex + lpex}` → `iiex` and `{smex + hpex}` → `siex`, one assembly and one domain each, gate **9 targets / 3,990** green. The mod set is now final - `exlib`, `iiex`, `siex`. `{iwex + lpex}` → **Iron Industry Expanded (`iiex`)**, the early-industrial loop: cast and wrought iron build the machines, from the basic blast furnace with tub blowers through to a steam-powered workshop. `{smex + hpex}` → **Steel Industry Expanded (`siex`)**, the steel loop: alloys and stock only steel can supply, fuel-gas power, larger and more efficient machines. `exlib` is unchanged. Supersedes the per-mod closure rule below |
| **M2** | 2026-08-13 | **Closure is per LOOP, not per mod.** The old rule — *"an iwex-only player gets a complete early-19th-century loop … and iwex recipes never reach into lpex"* — is retired. About 15 entity pages cite it to justify a placement; each is re-justified against its loop instead. ⛔ The loops are **nested, not parallel**: the steel loop extends the early loop's machinery and cannot close on its own (the gas producer is fed by an early-loop boiler) |
| **M3** | 2026-08-13 | ⛔⛔ **A merge collapses no tier.** The plated pipe tier, the iron gears and the rest of the early-loop parts are the **bootstrap rung** — deliberate progression, not duplication to be deduplicated. A player builds the plated tier before steam and upgrades to cast; both survive the merge intact. This is the constraint every merge task is measured against |
| **M4** | 2026-08-13 · **built 2026-08-14** | **Full domain consolidation, and the pipe tier becomes a variant group.** One asset domain per merged mod, so every block code moves. Because tier can then no longer be `Code.Domain` — and M3 forbids losing it — the tier moves onto the block. ⛔ The code is **`pipe-{tier}-{type}-{orient}`**, tier **first**, not the `pipe-straight-ns-{tier}` this row first wrote: declared last it moves every code out from under the `*-straight-ns` shape selectors and the blocks load with no shape and no error. **Supersedes [rolled-pipe](../../design/machines/rolled-pipe.md)'s and [cast-pipes](../../design/machines/cast-pipes.md)'s "the tier is the mod, not a variant axis"**, whose premise (tier == mod) is exactly what M1 removes; both pages are rewritten. Resolves the ~150-code collision by construction, and the three tiers now have three distinct display names. The variant covers the four segments and both valves; the bricks (passthrough, outlet) bear no pressure, name no tier and take the defaults |
| **M5** | 2026-08-13 | **`heavyplate` is one item with a metal axis**, absorbing `castplate`. Three routes, three metal states and no others: sand-cast in cast iron, rolled from a wrought or steel slab, or planed down from a larger plate. **Supersedes D2's "the rolled plate is a separate item"** — [rolled-parts](../../design/items/rolled-parts.md) · [cast-parts](../../design/items/cast-parts.md) |
| **M6** | 2026-08-13 | **The code-first definition builders are supported public API**, not a private convenience. `ExBlockDef`/`ExItemDef`/`ExRecipeDef` get a wiki page, a sidebar entry, a Getting-Started route and the deprecation rule. Carries three consequences: finish `ExItemDef` (30 methods against the block builder's 82), rename `Raw` → `RootKey` (it silently writes data the game cannot read), and move the block-code emitter out of the test assembly into the generator package — [framework hardening](2026-08-13-framework-hardening.md) F4.2, F8 |

D5a is retired: the pile-cap question dissolved when charge capacity became furnace geometry
(`ChargeCapacityUnits`, 2026-08-06) and the fire-threshold constants were deleted with it.

Later rulings, one line each:

* **Ferroalloys (2026-07-29)** — the cold blast furnace's permanent job: FeMn, FeCr and later FeSi as
  burden-family campaigns on the existing block. Run it cold and pay in coke, or hot and pay in
  throughput. Large additions (spiegel, hadfield-scale Mn) are melted in the **cupola** and poured — a
  cold charge that size freezes the heat; small ones are thrown in solid. Chill is the mechanic, no gate.
  Powdered coke is not a substitute: carbon alone cannot deoxidise burnt Bessemer metal — the manganese
  does, which is why FeMn is mandatory after a blow — [recarburising](../../design/processes/recarburising.md),
  [alloying](../../design/processes/alloying.md).
* **Cupola vs crucible (2026-07-29)** — fuel contact decides: the cupola carburises (right for cast iron
  and ferroalloys), the crucible's sealed pot stays clean (right for tool steel, later non-ferrous).
  Two crucible machines: the ferrous **draft furnace** (natural draught, tall stack, ~1600 °C, tongs) and
  the deferred non-ferrous **tilting crucible**. Hole count sets throughput; chimney height sets
  temperature — [crucible-furnace](../../design/machines/crucible-furnace.md),
  [tilting-crucible](../../design/deferred/non-ferrous/tilting-crucible.md).
* **Bending (2026-07-29)** — cold and multi-pass: the mill walks thickness *down* in gaps, the bender
  walks curvature *up* in passes, so `WorkPiece` gains a curvature axis. A separate block, because three
  rolls bend and two reduce. Welding is implied by placement, not a verb —
  [bending-roller](../../design/machines/bending-roller.md), [bending](../../design/processes/bending.md).
* **Fasteners (2026-07-30, ~~reaffirmed 2026-08-15~~, amended 2026-08-21)** — two machines, two routes,
  **no bolts**: the nail machine shears and heads nailplate in one pass; the **rivet machine** cuts and
  upsets rod @ 25 u; both **iwex**. iwex machines accept nails or rivets (merely structural =
  substitutable); the boiler requires rivets (must-hold-pressure = not) —
  [fasteners](../../design/items/fasteners.md).
  ⛔⛔ **"No dies" is struck (owner, 2026-08-21): the benches take dies after all.** This row and
  [machining-line](../../design/mechanics/machining-line.md) § Tooling contradicted each other for nine
  days - `ItemDie` shipped in exlib on the machining line's reading while this row said the family was
  dropped. The owner's reason settles it and is new information: **dies are wanted for the steam hammer's
  stamping as well**, so the contract has a second consumer and is not the nail bench's alone. What the
  original ruling actually rejected was the **die-fed bolt route** and a heading bench standing in for two
  machines; both stay rejected.
  ★★ **BUILT 2026-08-21**: both benches, die-fed, as one blocktype with a `type` variant. The
  substitution rule is built at **six sites** (four plated pipe segments, the tall hopper, the plated
  molten barrel) rather than at every nail site - ⛔ Vintage Story has no OR across item codes, in a grid
  ingredient or in an RCC `requireStacks` (which is an AND list), so substitution costs one duplicated
  recipe per site and the scope was chosen to stop it multiplying against the two gear routes. Both
  boilers moved to rivets **mass-neutrally**: Cornish 16 nail bundles → 32 rivets, Lancashire 24 → 48, the
  same metal either way, because the gate is the change and not the price.
  ★ The 25 u blank ships as **`iiex:rivetrod`** (2026-08-14, named 2026-08-15) — named for the bench
  because it is a *quarter* of vanilla's rod by section and by mass, so a bare `rod` would ship two items a
  player cannot tell apart. ⛔ [heading-machine.md](../../design/machines/heading-machine.md) had
  overwritten this row with a die-fed bolt route; its premise is now open and the row stands.
* **Placement (2026-07-29)** — a machine lives with the content it **feeds**, not what it is made of. The
  test: an iwex-only player gets a complete early-19th-century loop — cast, puddle, roll, fasten — with no
  dangling ends pointing at steam, and iwex recipes never reach into lpex.
* **Power progression (2026-07-29)** — the producer changes, the network does not: lpex swaps the
  waterwheel for a steam engine at the flywheel's hub and everything downstream is untouched. Steam's
  advantage is siting and reliability, not raw power, and the flywheel is never obsoleted —
  [mp-energy](../../design/mechanics/mp-energy.md).
* **The open hearth is a blown furnace (2026-07-29)** — natural draught settles ~1082 °C; blown it reaches
  the bath's working range, with the regenerator feeding the intake and buying speed, not possibility
  (R5) — [open-hearth](../../design/machines/open-hearth.md).
* **The ladle pulls by code (2026-07-29)** — it never joins the molten graph, so R3's merge mechanic needs
  no exlib change — [ladle](../../design/machines/ladle.md).

---

## Open

* **The cycle rate** stays deliberately loose for play-testing.
* **D4's accounting.** `CapacityUnits` gates the **pig** charge and the blow sheds 10 %, so 6000 u of pig
  yields 5400 u of steel = 1.8 slab pours, not 2. Either the capacity becomes 6667 u of pig, or D4 is
  restated as a *steel* capacity and the fill gate changes with it.
* **Ladle sizing.** 5400 u of steel + 12.5 % Mn = 6171 u, over a 6000 u ladle. And ferroalloy additions
  move manganese and carbon **together** (FeMn is high-carbon by definition), so a single-element model
  silently misses hadfield's ~1.2 % C target.
* **Producer gas has no path through any existing system** — four independent gaps the open hearth depends
  on: the cowper soaks *sensible* heat only and cannot burn a fuel gas; `Medium == "Air"` is tested
  literally, so a fourth gas is silently inert at every furnace (violating R7); two gases always mix; and
  the heat balance has no gas-fuel term. Also "producer gas is never stored" must be a **rate rule at the
  machines** — a pipe run *is* storage. Owned by [gas-system](../../design/mechanics/gas-system.md) and
  [gas-producer](../../design/machines/gas-producer.md); recorded here as status.
* **`BridgeDriveTorque` tuning** — how much a single vanilla waterwheel actually buys decides whether the
  iron tier feels powered or starved, and nothing on paper fixes it.
* **hpex's material gate (hadfield) does not exist in code.**
* **The full-shaft anchor** (a full shaft ≈ one casting bed) has not been re-checked against the
  geometry-derived capacity — see [burden](../../design/items/burden.md) § Open.

---

## What "complete" looks like

The release target — a player walks `exlib → iiex → siex` without leaving the spine — is owned by
[scope.md](../../design/scope.md). Today the walk breaks in three places: the forming line cannot be
reached in survival (roll sets are lathe-turned and there is no lathe), the Bessemer cannot be raised in
survival (the cast pipe segments have no recipe, B19), and the steel loop's second half — open hearth,
ladle, alloying, hadfield, high pressure — is designed and unbuilt (B5, B6). The first two close on one
unit, the machining line; the sequencing is in [the roadmap](2026-09-04-roadmap.md).

Everything else is polish. The frameworks are mature, the networks work, the heat balance is calibrated,
and the test harness is real. **The gap is not capability — it is that the last mile of each tier was
never wired to the next, and that five weeks of built stations have not been seen in game.**
