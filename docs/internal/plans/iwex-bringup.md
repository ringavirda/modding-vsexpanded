# iwex bring-up

**Status** superseded 2026-09-04 by [2026-09-04-roadmap.md](2026-09-04-roadmap.md), whose Phase 1 is the
playtest walk this file was for; the art queue below is exported except where the roadmap's state table
says "art only". Kept as a record.

**Former status** in progress — Stages 0 and 1 done (re-verified against `src/` on 2026-08-04)   **Mod** iiex
(the art queue below is the iron half of `iiex`; the mod was `iwex` until the 2026-08-14 merge)
**Since** 2026-07-30
**Owns** — the **art queue** and the **playtest gates**. Nothing else. The build *sequence* lives in the
plans; blocker evidence lives in [STATE.md](STATE.md); numbers live on the entity pages.
**Depends on** [STATE.md](STATE.md) · [machines/](../../design/machines) · [processes/](../../design/processes) · [items/](../../design/items)

> ★ **Triaged 2026-08-14.** Still live, and still the only owner of the art queue and the playtest
> gates. Its Stage 2-6 mapping is unchanged, but read the state table in
> [the expansion plan](2026-08-04-iwex-u2-u10-expansion.md) first: of the units this file points at,
> **U1 is built** and U6-U9 are open, so the art queue is what remains of Stage 2. Paths and type names
> were repointed at their live homes the same day.
>
> **This file does not own the build order.** The sequence lives in
> [`docs/internal/plans/2026-08-04-iwex-u2-u10-expansion.md`](../plans/2026-08-04-iwex-u2-u10-expansion.md), which
> merges this stage list with the layered-charge furnace rework; Stages 2–6 below map to units
> **U1 · U6 · U7 · U8 · U9** there. For "what is next, right now", start at
> [`docs/internal/plans/NEXT.md`](NEXT.md).
>
> Parts 1 and 2 are the 2026-07-30 survey, refreshed where noted. Part 3's stage list has been re-verified:
> every Stage 0 and Stage 1 item was checked against source on 2026-08-04, and all are fixed.

> **Why this file exists.** The suite is being brought online **one mod at a time** so a group of players can
> actually test it. That makes "which blocker is worst" the wrong question — the right one is **"what is the
> shortest walk to a build a player can be handed?"** This file answers that and nothing else.

---

## Verified baseline *(2026-07-30)*

| | |
|---|---|
| Build | **green**, 0 warnings |
| iwex tests | **844 passed, 0 failed** |
| iwex source | 137 `.cs` files · 32 registered block classes · 12 def providers |
| iwex runtime shapes | 81 |
| editable shapes on disk | 97 — of which **87 are untracked**, i.e. new or renamed art not yet exported |
| iwex lang keys | 397 × 3 locales (en/ru/uk in sync) |

The mod is much further along than the blocker count suggests. **Nothing on the list below is
architectural** — the frameworks, the three networks, the heat balance and the harness all work. Every item
is either a wrong string, a missing item def, or a process that was never written.

---

## Part 1 — The art queue

**The most important finding for the art side: most of it is already drawn.** The untracked editable shapes
sit in `workbench/shapes/` under the `<family>-<kind>-<name>` naming scheme, and the runtime tree
still carries the old exports. So the dominant art task is **export + wire**, not draw.

### 1a. Already drawn — needs export to `mods/iiex/assets/iiex/shapes/` and a code reference

Nothing to draw here. Listed so it is not re-drawn.

| Editable source | Wants runtime path | Blocks what |
|---|---|---|
| `item-finished-rollers-{flat,grooved}` | `iwex:forming/rollers-*` | roll sets ship on a **placeholder** — `RollSetItemDefinitions.cs` sets `.Shape("game:item/ingot")` |
| `item-finished-rollers-flatwide{5,10,15}`, `item-rollers-flatwide20` | same | the lpex wide train |
| `item-sandcast-rollers-blank` | `iwex:item/rollers-castblank` | casting a roll set (its own pattern) |
| `item-rolled-rod`, `item-rolled-rivetrod`, `item-rolled-nailplate`, `item-rolled-beam` | `iwex:forming/*` | **every rolled product** — none exist as items |
| `item-shingled-bar`, `item-shingled-slab` | `iwex:forming/*` | the mill's only legal input |
| `item-tool-paddle`, `item-tool-rabble` | `iwex:item/tool-*` | the puddling verbs |
| `item-sandcast-heavyplate`, `item-sandcast-ingotmold`, `molten-sandcellfilling-castplatemold` | `iwex:item/*` | D2's plate redraw (castplate 500 u) |
| `item-sandcast-frame`, `item-sandcast-shell`, `item-sandcast-wheelsegment` | `iwex:item/*` | N3 cast parts |
| `item-sandcast-cylinderblank`, `item-finished-cylinder`, `item-finished-pipesegment` | `iwex:item/*` | boring-machine stock |
| `item-finished-gear{spur,pinion,bevel}`, `item-sandcast-gearblank{large,small}` | `iwex:item/gear-*` | iwex must ship its own gear |
| `molten-megablock-sandlongcell` + 5 `molten-sandlongcellfilling-*` | `iwex:casting/longcell-*` | **the long-cell block, which does not exist at all** |
| 11 × `molten-sandcellfilling-*` | `iwex:casting/cell-filling-*` | the redesigned cast-part set |
| `machine-mp-megablock-nailcutter` | `iwex:forming/nailcutter` | the nail machine |
| `machine-mp-megablock-cutter` | `iwex:forming/*` | the shear |
| `machine-mp-megablock-riveter` | `iwex:forming/*` | the rivet machine |
| `machine-mp-megablock-rollingmill` | `iwex:forming/rollingmill` | redraw of a live block |
| `machine-megablock-designtable` | `iwex:crafting/designtable` | redraw of a live block |
| `furnace-block-hopperreinforced`, `furnace-megablock-{chargedoor,heatinghearth,puddlingchargedoor,puddlingchimneycap,puddlinghearth}` | `iwex:furnaces/*` | redraws of live blocks |
| `furnace-block-draftcruciblehearth`, `furnace-block-chargelid`, `item-steelcrucible` | `iwex:furnaces/*`, `iwex:item/*` | the draft crucible furnace and its refractory pot |

Ruled 2026-08-03: the old `item-rod-nail` split into two items — `item-rolled-rivetrod` (feeds the rivet
machine, rolled on grooved rollers) and `item-rolled-nailplate` (feeds the nail machine, made by **two
routes**: flat rollers on rod, *or* MP shears halving a plate).

The second route is the more interesting half. Until now the shear only *cropped* — it removed material to
make stock legal. Halving a plate into nailplate makes it a **converging** path: two different upstream
products reach the same downstream machine, so a player with a plate surplus and a player with a rod
surplus both have a way into nails. That is the first place in the ladder where the forming line stops
being a single chain, and it is what makes the shear worth building for a reason other than "the recipe
says so".

It also means `nailplate`'s mass must come out the same on both routes, or one becomes strictly better.
The density rule (`1 voxel³ = 2.5 u`) settles it: half a rolled plate and a flat-rolled rod must be drawn
to the same volume. Check this when the shapes are wired, not after the recipes exist.

### 1b. Genuinely not drawn — the actual art queue

Ordered by when the plan needs them. Nothing in `workbench/` matches any of these. The shear, the
rivet machine and the crucible-furnace parts have left this list — their shapes are drawn (see 1a) — and
the bolt and die items left it by ruling (fasteners, 2026-07-30: no dies, no bolts).

| # | Needs | Kind | Wanted by |
|---|---|---|---|
| **A3** | **Stock rack** | 1×1×3 megablock, planks + stacked stock | Stage 5. Pure player-facing joy; no mechanism |
| **A4** | **Coke oven** (beehive) | megablock, natural draught | Stage 6 |

**Textures.** iwex's texture set is in good shape (furnace faces, cast iron, slag). The one recurring need is
**diagram icons**: `mods/iiex/assets/iiex/textures/item/diagram/diag-{type}.png`, one per structure diagram and one per
casting pattern (`diag-item-{pattern}.png`), derived automatically from
`PatternItemDefinitions.PatternTypes`. So each new pattern in Stage 2 and each new diagram-crafted block
needs exactly one 32×32 icon. Machines built by grid/RCC (mill, burdenmaker, blower, hearths) need
**none** — only the structure diagrams and the patterns do.

`twintubmpblower.json` has a runtime shape but no editable source. It predates the convention. Not
blocking; worth back-filling so the source of truth is uniform.

---

## Part 2 — Code survey

### Live and working — leave alone

Tall hopper · burden · burdenmaker · **cold blast furnace** (heat
balance, blast, melt cycle, tap) · **cupola** · molten canal network (start / straight / bend / T / X / tap /
mold pedestal / barrel) · sand casting **cell** · cast molds · plated **pipe** tier + its recipes · mp-energy
(flywheel + vanilla-MP bridge, shaft, bevel, transmission) · design table + diagram crafting · pig items +
helve breaking · slag products · **rolling-mill machinery** (pass physics, feed decision, gap zones, strips,
load torque, deck rotation, stall/release)

### Shells — the block stands, the process was never written

| Block | What exists | What is absent |
|---|---|---|
| **Puddling hearth** | fettle, charge pigs (9), render, save/load | **The entire process.** Zero temperature code, no melt, no rabbling verb, no ball output |
| **Heating hearth** (reheat) | 3-row rack, load/unload, per-row render | **All of the heating.** Zero temperature code. The rack is a shelf |

These two are the mod's spine and they are the largest single piece of remaining work. Everything the
forming line needs — hot stock — comes out of them.

### Absent entirely

| Missing | Art | Note |
|---|---|---|
| **long cell** block | drawn | The whole cast-stock route |
| **every rolled product item** | drawn | `rolledplate`, `rolledsheet`, `wirerod`, `nailrod` exist **only as string literals** in `RollSetItemDefinitions.cs`. Nothing defines them |
| **the mill's product-extraction path** | n/a | `RollSetSpec.OutputAt` is written, tested and **never called by any block or BE**. Rolling a piece to 1.0 yields a `stock-bloom` at 1.0, not a plate |
| shear · nail machine · rivet machine · stock rack | all but the rack drawn | Stage 5 |
| coke oven · crucible furnace | oven not drawn; crucible hearth + lid + pot drawn | Stage 6 |
| iwex's own **gear** | drawn | shipped as defs (`SpurGearItemDefinitions`); the drawn shapes still want wiring |

### Blocker diagnosis, verified against source

- **B3's stated cause is narrower than STATE has it.** `StockItemDefinitions` does set `stockForm`; the
  real cause is that `WorkPiece.FromStack` reads `stockForm` from **`stack.Attributes`** (the per-stack
  tree) and never falls back to `stack.Collectible.Attributes` (the item def). A fresh piece off the shelf
  has an empty tree → `null` → `WrongForm`. **One fallback line fixes the feed path.** The missing output
  items are the separate half.
- **B17 is a three-line fix.** The footprint is already correct — `BlockRollingMill.Footprint` authors a
  full 3×3, so **both feed decks are 3 filler cells wide**. The bug is only that
  `BlockEntityRollingMill.Deck()` returns a **single** cell at x=0, so `AlongBarrel` is pinned to
  `[0.667, 1)` and a click can only ever select gap zones 2–3. Make `Deck` return the row and
  `IsInputDeck` a membership test.

---

## Part 3 — The plan

Six stages. **Each ends at a state a player can be handed**, which is the whole point of doing one mod at a
time. Stage gates are written as things a tester *does*, not as features.

### Stage 0 — Make it buildable — done *(verified against source 2026-08-04)*

Nothing in iwex could be constructed without lpex installed, which contradicted the placement rule.

| # | Item | Where it landed |
|---|---|---|
| 1 | `lpex:pipe-straight*` → **`iwex:pipe-plated-straight*`** | `FurnaceRecipeDefinitions.cs:66`, with the reason at `:63-65` |
| 2 | `lpex:gear-iron` → an **iwex cast gear** | `SpurGearItemDefinitions`; consumers rewritten (`BlockTransmission.cs:70`) |
| 3 | **Twin-tub blower** recipe | `FurnaceRecipeDefinitions.cs:102` |
| 4 | **Rolling mill** recipe | `FormingRecipeDefinitions.cs:51` |

**Gate met:** with only exlib + iwex a player can build the burdenmaker, the blower, the blast furnace and
the mill.

### Stage 1 — Make the live half actually run — done *(verified against source 2026-08-04)*

| # | Item | Where it landed |
|---|---|---|
| 1 | **B14** casting bed could never be carved | `BlockEntitySandCastingBed.cs` — the animator is built on **both** sides now |
| 2 | **B2** | withdrawn, not fixed — see [STATE.md](STATE.md): the stall is the *taught* coke trade and the furnace names it every tick. The residual is band tuning, and it is a play-test number |
| 3 | **B16** rotated cupola charged outside itself | `BlockEntityHopperTall.cs` — columns rotate by `DripAngle`; the diagonals were added too, so the drip is a 3×3 and not a plus |
| 4 | **B9 / B10** molten flow | `MoltenNetwork.cs` — vertical edges are driven `downhillOnly`, each undirected edge once |
| 5 | **B11** air drawn before the pressure test | `BlockEntityFurnaceCore.cs` — order inverted |
| 6 | **D5b** throughput | `IiexValues.TapDrainPerTick = 50`, read by both taps |

**Gate met:** ore → burden → blast furnace → canal → pig bed → pigs → helve → anvil.

### Stage 2 — Close the casting route

The art is entirely done; this is item defs and pattern entries.

1. Export the new cell fillings + the long-cell set.
2. **Build the long-cell block** (1×1×2) — `BlockSandCastingLongCell` + BE, reusing the cell's
   `CastingCellLogic` and the `MoldSpec` idiom.
3. Add the cast-part items and their patterns: `castplate` (D2, 10×2×10 = 500 u), `castframe`, `castshell`,
   `castwheelsection`, `castbillet`, `castbloom`, `castslab`, cylinder blank, gear blanks, shafts.
4. **Apply the density rule while doing it.** The shipped cavities use inconsistent implicit densities;
   the settled masses land as one batch ([economy-landing](../../design/items/economy-landing.md)). Fixing them
   retroactively later means touching every recipe that consumes them.

**Gate:** cupola → long cell → a cast slab, and cupola → cell → a cast frame. The cast half of the fork
works, and `castbillet`/`castbloom`/`castslab` stop being names the heating hearth references but which do
not exist.

### Stage 3 — Write the puddling process

The first stage with real new logic.

1. Confirm both reverberatory layouts can complete: the hearths, doors and chimney cap now ship their own
   filler footprints, so the old declared-but-unsupplied filler cells should be gone — verify counts on
   both furnaces before building on them.
2. **B8's remainder** — the ignition threshold is now derived from the firebox's own cell count
   (`FireboxCellCount × FireboxMixPerCell`), so the furnace can light; what remains is the process itself
   and the draught ceiling against the melt line.
3. **Make the natural-draught factor scale with stack height** rather than the flat constant. This one
   change retro-fits *every* natural-draught furnace — puddling, coke oven, crucible — and turns the tall
   chimneys already drawn throughout the mod into something that **does** rather than decorates. Do it
   here, collect it three times.
4. Write the process on `BlockEntityPuddlingHearth`: melt the 9-pig charge → rabble (the verb) → **200 u
   wrought balls**. Paddle and rabble art is drawn.

**Gate:** 9 pigs → puddling furnace → rabble → wrought balls → helve → a shingled bar. The wrought half of
the fork works.

### Stage 4 — Reheat and rolling

The stage the design spent the most words on and code the least.

1. Write **heat-into-stock** on `BlockEntityHeatingHearth`. Soak time ∝ **V/A** (area, not mass — heat
   exchange is a surface process), at the vanilla forge rate with **no ×2 multiplier**.
2. **Rewrite `WorkPiece` to the settled two-round model.** Today it is per-strip `Strips[]` + `Turned[]`
   with lopsided pieces; the settled model is **one `Thickness` + a per-side fed-this-round flag**, where a
   gap costs 2 rounds, round 1 landing the `.75`/`.25` **half-step**. This *deletes* code and deletes the
   lopsided art. Note the stage-path scheme `(int)(t*10)` cannot express a half-step (2.25 → `22`) — the
   shape naming needs one more digit or a different key.
3. **Redesign the roll sets.** Currently: the retired 0.5 gap in three sets, `rolledsheet` and `wirerod`
   outputs that are retired by decision, a `slitting` set whose `accepts: ["plate"]` matches no `StockForm`
   so it can never fire, and `flatwide` in iwex when wide sets belong to lpex. **B4** — `grooved` opens at a
   1.0 gap against 3.0 stock, a 2.0 draft against `δ_max` 1.0, so the rod route has no legal entry.
4. Define the **rolled product items** — `rolledrod`, `nailplate`, `beam`, rolled plate. Art drawn.
5. **Wire the extraction path.** `RollSetSpec.OutputAt` exists and is called by nothing; the mill must convert
   a piece at a product gap into its product.
6. **B3** (the `FromStack` fallback) and **B17** (the 3-cell deck).

**Gate:** **shingled bar → reheat → mill → rod, plate and beam.** This is the mod's thesis made playable
— the moment build complexity starts paying out as operating efficiency.

### Stage 5 — Fasteners and the shop floor

Where the remaining art queue lands. Per the fasteners ruling (2026-07-30): two machines, two routes, no
dies, and bolts are dropped.

1. **Shear** — owns every crop in the ladder. Torque-gated, cold. Art drawn.
2. **Rivet machine** — cuts rod @ 25 u and upsets a head. Art drawn.
3. **Nail machine** — shears and heads nailplate in one pass. Art drawn.
4. **Stock rack** — 1×1×3, no mechanism, pure display.

These machines take the mp-energy network from **one consumer to four**, which is what makes the
flywheel self-evident instead of asserted: one waterwheel cannot run four machines at once, but it can charge
a flywheel that serves them one at a time.

**Gate:** rod → rivets, plate → nails, and a warehouse wall of stock to look at. **iwex is complete** —
cast, puddle, roll, fasten, with no dangling end pointing at steam.

### Stage 6 — iwex's own rewards

1. **Coke oven** (A4) — the fuel the whole tier assumes.
2. **Crucible furnace** — Huntsman is **1740**, a century before Bessemer: coke-fired, natural
   draught, no steam and no MP. It belongs here, and it is the one thing that pays an iwex-only player in
   **gear** rather than in materials. Its gate is batch size, not tier. The hearth block, charge lid and
   refractory pot are drawn.

**Gate:** an iwex-only player can make the best steel in the game, one pot at a time.

---

## Sequencing notes

**Stage 1 is the release.** Stages 2–6 are depth on a working loop, and each can ship to the same
playtest group as an update. Do not hold Stage 1 for Stage 4.

**Deliberately not in this plan:** the hot-blast rework, the bell hopper, the skip hoist, everything
non-ferrous, and all of lpex. They are correctly deferred and iwex does not need them.
