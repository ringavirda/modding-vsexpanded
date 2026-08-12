# Plan coherence — open findings (iwex)

Companion to [`2026-08-05-iwex-plan-audit.md`](2026-08-05-iwex-plan-audit.md), which checks the plan
against the design docs. This file checks the plan for internal coherence: broken dependencies, gaps,
contradictions, ordering, and whether the planned machine is operable from the player's chair.
The findings target the unstarted units (U4.4 onward, U6–U11), plus the forks that still need a
ruling; the shipped units (U2, U3, U4.1–4.3, U5) are not covered. Re-verify each finding against
source at unit start — quoted line numbers and test counts predate the shipped work.

| kind | meaning |
|---|---|
| `BROKEN_DEPENDENCY` | consumes something no earlier task produces |
| `GAP` | must exist; no task makes it |
| `INTERNAL_CONTRADICTION` | two tasks assert incompatible things |
| `ORDERING` | the sequence cannot work as written |
| `UNDERSPECIFIED` | says what, never how |
| `DEAD_TASK` | already done or now pointless |

38 findings (3 blocking, 15 high, 20 medium) and 9 forks needing a ruling.

---

## Blocking (3)

### `BROKEN_DEPENDENCY` — U9's entry condition, the execution order, and U9.10 all name U6.3 as the producer of NaturalDraughtFor and of the per-machine loss virtuals. U6.3 produces neither — it produces a DisruptionMixFloor override. The real producers are U6.5 (draught) and U6.4 (losses).

- **Tasks:** U9 entry condition, Execution order #6, U6.3, U6.4, U6.5, U9.10 Step 2
- **Evidence:** Execution order #6: "U6 (puddling) — serial on the core. Delivers U6.3 NaturalDraughtFor, the only thing gating U9."  |  U9 entry #1: "**U6.3 landed.** `NaturalDraughtFor(courses, damperOpen)` must have replaced the flat read at src/…/BlockEntityFurnaceCore.cs:1229 … and per-machine charge loss must be a virtual over `IwexValues.BfChargeLossFull`"  |  U9.10 Step 2: "The reverberatory transfer term U6.3 introduces for the puddling and reheat furnaces…"  but — U6.3 **Produces:** "`protected override int DisruptionMixFloor` on BlockEntityFireboxFurnace, derived from MinChargeToIgnite; a branch guard…"; U6.4 **Produces:** "`protected virtual float ChargeLossFull => IwexValues.BfChargeLossFull;` and `protected virtual float TransferLoss => 0f;`"; U6.5 **Produces:** "`public static float StackDraught.NaturalDraughtFor(int courses, bool damperOpen, bool venting)` (pure)". Signature also disagrees: 3 params produced, 2 consumed.
- **Consequence:** U9 can be cleared to start with U6.3 landed and none of the three things it actually needs. The crucible furnace then ships lit-and-never-melting (T_process ~1082 °C against MeltingPoint 1600) — which U9's own top trap calls "B15/B2's exact failure mode". Anyone scheduling U6 partially will stop after U6.3 believing U9 is unblocked.

### `INTERNAL_CONTRADICTION` — U7.5 and U8.3 both delete RollSetSpec.Outputs/OutputAt and both build a replacement crop table — two different types with incompatible keys. U8.3's key cannot express what U7.5's does.

- **Tasks:** U7.5, U8.3, U8 entry condition, U8.4 Step 6
- **Evidence:** U7.5 heading: "Move Outputs/OutputAt off RollSetSpec onto a stage-keyed product table"; Step 5: "Delete Outputs from the record (:35), delete OutputAt (:94-101), delete the outputs block from TryParse (:159-183) … Delete the outputs argument from RollSetItemDefinitions.Set (:25-44) and the Out helper (:46)."  U8.3 heading: "The crop table and ShearFeed — the pure decision layer that moves Outputs/OutputAt off RollSetSpec"; Modify: "RollSetSpec.cs:28,35,95-101 (delete Outputs/OutputAt and their parse branch at :159-176)", "RollSetItemDefinitions.cs:46,66,77,90,100 (drop the Out() helper and every `outputs` array)".  Keys differ: U7.5 produces `static Crop? At(string form, SectionClass section, float stage)`; U8.3 produces `public static Crop? At(string form, float thickness)`. U7.5 Step 2 requires "shingledbar grooved 2.0 → 4 × rolledrod @100" and "shingledbar flat 2.0 → 2 × beam @200" — same form, same thickness, different answer. Verified live in src: RollSetSpec.cs:35 `IReadOnlyDictionary<float, string> Outputs,` and :95 `public string? OutputAt(float thickness)`.
- **Consequence:** Whichever unit runs second finds its Modify targets already gone and its deletion steps unexecutable. If both tables get built, the mod carries StockProducts and ShearCrops side by side with different keys and no test comparing them. If only U8.3's is built, the section-blind key silently collapses the flat and square schedules onto one answer — the plate route and the rod route become the same crop.

### `GAP` — `iwex:furnace-firebox` is a required cell in the puddling layout (and, per U9.1, in twelve coke-oven chamber cells) and has no grid recipe anywhere — and no task in the plan adds one.

- **Tasks:** U6.11 Step 5, U9.1 Step 2, U9.4 Step 3, U6 Gate, U9 Gate
- **Evidence:** Required by the layout: `src/IronworkingExpanded/BlockStructures/Furnaces/Blocks/BlockPuddlingFurnaceCore.cs:110` `.Legend('F', IwexBlocks.FurnaceFirebox.Any)` with `:115` `.Role('F', CellRole.Firebox)`. The block exists (`Generated/IwexBlocks.g.cs:392 public static class FurnaceFirebox`) but no `OutputBlock` in any file under `src/IronworkingExpanded/Recipes/Grid/` names `furnace-firebox` (grep of all 12 providers). U9.1 Step 2 makes it the oven's substrate: "the `c` cells hold iwex:furnace-firebox-*-* (IwexBlocks.FurnaceFirebox.Any) marked CellRole.Firebox". U6.11 Step 5 adds recipes only for "`puddlingcore`, `puddlinghearth`, `puddlingchargedoor` or `puddlingchimneycap`". U9.4 Step 3 states the opposite of the truth: "Add the missing chargelid recipe here too. It is the one shipped block in the coke-oven layout with no recipe at all".
- **Consequence:** The puddling furnace, the reheat furnace and the coke oven can never be completed in survival. U6's gate opens "A player builds a puddling furnace from craftable blocks" and U9's gate opens "An iwex-only player ... can run this end to end"; both are unreachable, and no test would notice because StructureRig raises fillers and every layout test is def-level.

---

## High (15)

### `INTERNAL_CONTRADICTION` — U8.6's first test asserts the rivet route yields strictly more fasteners per unit of iron than the nail route; U8's own Gate asserts neither route beats 4 fasteners per 100 u.

- **Tasks:** U8.6 Step 1, U8 Gate
- **Evidence:** U8.6 Step 1: "Write FastenerMassTests first: a rivet bundle's declared units times its count equals the 25 u rod it came from … and the rivets-per-unit-of-iron figure is **strictly better than the nail route's 4-per-100 u**, because that yield advantage is the entire trade the substitution rule sells".  U8 Gate: "both routes conserve mass exactly and **neither beats 4 fasteners per 100 u**."
- **Consequence:** The test U8.6 Step 1 mandates fails U8's gate by construction. Whoever writes FastenerMassTests first fixes the rivet:iron ratio, and the gate then reads as a regression rather than as the disagreement it is — so the ratio gets silently retuned to satisfy whichever text was read last, which decides whether rivets are an upgrade or a sidegrade.

### `GAP` — U4.5 makes local (0,1,0) non-chargeable on the cold furnace, which invalidates `ColdBlastFurnaceScenes.ChargeCells` and four assertions in the burn-out scenario — none of which appear in U4.5's Files or Test lists, and all of which U2's gate promises will survive untouched.

- **Tasks:** U4.5 Step 1, U4.5 Step 3, U4.5 Files, U2 Gate, U2.6 Step 6
- **Evidence:** Plan U4.5 Files lists only `BlockBlastFurnaceCoreCold.cs`, `BlockCupolaFurnaceCore.cs` and two goldens; its Test list is `ChargeableCellsTests`, `ShaftColumnsTests`, `FurnaceRoleCellsTests`, `FurnaceLayoutRig.cs:630-658`. Code: `BlockBlastFurnaceCoreCold.cs:59` `s.Origin(-3, -2)` with Layer 1 row z=0 `. S h h h I` (`:146`) — local (0,1,0) is an `h` crucible cell. `test/IronworkingExpanded.Tests/Fixtures/ColdBlastFurnaceScenes.cs:149` `private static readonly Vec3i[] ChargeCells = [new(0, 1, 0), new(0, 5, 0)];`. `test/IronworkingExpanded.Tests/Scenarios/ColdBlastFurnaceScenarioTests.cs:487-489` asserts a pile block and `Assert.NotNull(scene.PileAtLocal(0, 1, 0))`, and `:494` reads `scene.SalvageAtLocal(0, 1, 0)` as "sitting on the tuyeres". Plan U2 Gate: "Every case in the cold-furnace, cupola and hot-furnace scenario suites passes with no edits to its assertions, the sole exception being `ColdBlastFurnaceScenarioTests.cs:487-488`."
- **Consequence:** U4.5 turns the iwex scenario suite red on a file it does not own, and the salvage-by-height case loses its bottom sample entirely — the assertion that ore and flux come back untouched at the bottom of the column has no cell left to read. The plan's stated U2 invariant is violated by a later task nobody flagged.

### `BROKEN_DEPENDENCY` — U4.4 puts live molten cells into `PoolCells` on every shaft furnace, but U4.5 deliberately leaves smex's hot furnace with `Pool` and `Chargeable` on the same two cells — and those two cells are the floor of two of its nine charge columns.

- **Tasks:** U4.4 Step 4, U4.5 Step 6, U4 Gate clause 6
- **Evidence:** Plan U4.4 Step 4: "`ConsumeForMelting` … instead SetBlocks the hearth block where a pool cell is free (reuse the free-cell rule from `SolidifyBottomLayer` … empty, or a charge pile that is not holding rejected charge)". Plan U4.5 Step 6: "Do not touch `src/SteelmakingExpanded/.../BlockBlastFurnaceCoreHot.cs:105-106`." Plan U4 Gate: "(6) `Chargeable` and `Pool` are disjoint on both **iwex** shaft furnaces". Code: `BlockBlastFurnaceCoreHot.cs:103-106` `.Role('c', Chargeable)` + `.Role('p', Chargeable)` + `.Role('p', Pool)`; Layer 1 row z=0 is `. . # p p T` (`:132-139`) and Layer 2 row z=0 is `. S c c c #` — the `p` cells sit directly under two `c` columns. `BlockEntityBlastFurnaceHot.cs:21` `public class BlockEntityBlastFurnaceHot : BlockEntityShaftFurnace { }` so it inherits everything U4.4 builds. Free-cell rule at `BlockEntityFurnaceCore.cs:1722-1731`. `SyncChargeBlocks` refuses non-own blocks (`:868-872`).
- **Consequence:** On the hot blast furnace, normal melting SetBlocks hearthmetal over a standing charge pile at the column floor — destroying the world block while the column still counts its units — and `SyncChargeBlocks` then refuses to rebuild it. The two columns permanently draw one block short and the render/model desync is invisible to every test. This is not the cosmetic notch-height deferral U4.6 Step 8 writes down; it is a data-loss path.

### `INTERNAL_CONTRADICTION` — U6.3 adds `protected override int DisruptionMixFloor` to the firebox branch, while U3's gate requires the core's virtual to be gone — and no U3 step deletes it or splits it onto the branch.

- **Tasks:** U3.6, U3.7, U6.3 Step 4, U6.3 Step 7
- **Evidence:** U3 gate clause (8): "`grep -n \"BfMeltStartDelay\\|…\\|ExtinguishThresholdDefault\\|ExtinguishThresholdSevere\\|DisruptionMixFloor\" -r src/` returns nothing on the shaft path".  U3.6 Consumes lists "`DisruptionMixFloor => 144` (:224)" but its Steps 1-9 delete only the five timer fields (Step 4), `TransitionToMelting` and `Extinguish` (Step 5) — nothing deletes or relocates DisruptionMixFloor or the two extinguish thresholds. U3.7's Modify list is config keys only.  U6.3 Step 4: "add `protected override int DisruptionMixFloor => MinChargeToIgnite / 2;` … to `BlockEntityFireboxFurnace`". U6.3 Step 7: "U3.3 deletes `DisruptionMixFloor` outright for the shaft branch. The firebox branch has no columns and therefore keeps an explicit FSM, so this override survives U3."  Source: src/IronworkingExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:225 `protected virtual int DisruptionMixFloor => 144;` — one core virtual, read once at :1282, no branch split exists.  Execution order item 6 runs U6 after U3.
- **Consequence:** Two mutually exclusive outcomes and no task owns either: if U3 leaves the member, its own gate grep cannot pass; if U3 deletes it, U6.3's `override` is a compile error and the puddling/heating hearths keep snuffing at 30 s (B8's fifth cause). Same applies to ExtinguishThresholdDefault/Severe, which U3's gate also greps for and U6.3's arithmetic depends on.

### `BROKEN_DEPENDENCY` — The rod fork U7.3 introduces cannot be entered: no task gives iwex:rolledrod a `stockForm` attribute, and no task adds it to the reheat hearth's stock whitelist.

- **Tasks:** U7.3 Step 4, U7.3 Step 5, U7.4 Step 5, U7.6
- **Evidence:** U7.3 Step 4: "add Rod = 2×2×10 — which is what makes the fork work: a rolledrod re-enters the same flat/grooved sets at their 1.5 and 1.0 gaps." U7.3 Step 5: "flat: family \"flat\", section \"flat\", accepts [\"bloom\",\"rod\"]".  U7.4 Produces: "iwex:rolledrod (100 u), iwex:rod (25 u), iwex:nailplate (100 u), iwex:beam (200 u) — four itemtypes, MaxStackSize 1 where the piece carries heat, each with a runtime shape that resolves" — no step declares a stockForm attribute.  Source: src/IronworkingExpanded/BlockStructures/Forming/WorkPiece.cs:40 `private const string FormKey = "stockForm";`, and the only writer is src/IronworkingExpanded/BlockStructures/Forming/StockItemDefinitions.cs:45 `.Attribute("stockForm", form.Name)`. U7.6 Step 4 extends it to caststock only.  Source: src/IronworkingExpanded/BlockStructures/Furnaces/HeatingHearthLayout.cs:64-79 `StockOf` matches exactly `stock-bloom`, `stock-slab`, `castbillet`, `castbloom`, `castslab`, and the `Stock` enum (:21-28) has only those five members with one art element each.
- **Consequence:** `WorkPiece.FromStack` returns null for a rolledrod, so the mill refuses it; and `BlockEntityHeatingHearth.TryLoad` (HeatingHearthLayout.StockOf at BlockEntityHeatingHearth.cs:63) refuses it too, so it cannot be reheated between rounds. The rod branch of the schedule is dead, which takes U8's rivet route with it — the rivet machine eats a 25 u rod cropped from a rolled rod that can never be produced.

### `GAP` — No task seeds IwexConfig.Migrations, although the plan names U2 as the seeding unit and four units delete config keys.

- **Tasks:** U2.7 Step 6, U2.10 Step 6, U4.4 Step 6, U5.7 Step 11
- **Evidence:** Plan line 121: "IwexConfig.Migrations is `[]` (IwexConfig.cs:27) while U2, U3, U4 and U5 all delete config keys that require an entry in it. The plan mentions this only inside U3's shared-files list. Promote it to a Global Constraint with a named seeding unit (U2) and an append-at-the-end rule."  Source: src/IronworkingExpanded/IwexConfig.cs:26-27 — `/// <summary>Version-driven default resets (none yet).</summary>` / `public static readonly ExConfigMigration[] Migrations = [];`.  Deleting steps with no migration: U2.7 Step 6 "Delete `IwexValues.HopperTallPileCap` (IwexConfig.cs:450) and `FreshPileCeiling` (:284)"; U2.10 Step 6 "Delete `IwexValues.BlastmixBurnTime` (IwexConfig.cs:109)"; U4.4 Step 6 "Delete `IwexConfig.BfMaxMoltenIron` (:274), `BfMaxMoltenSlag` (:277), `CupolaMaxMoltenCastIron` (:333), `CupolaMaxMoltenSlag` (:336)"; U5.7 Step 11 deletes BunkerMaxBurden, MixerMaxRaw, MixerFullMixSecondsSlow/Fast, MixerMinSpeed, MixerMaxSpeed, MixerDrainPerSecond. The only step that adds an entry is U3.7 Step 6.
- **Consequence:** Every existing ex_values.json keeps orphan JSON for eleven-plus deleted keys, and the array U3.7 Step 6 seeds arrives after four units already needed it — so U3.7's single entry is authored against a file it cannot know the shape of. The plan's own "nominate U2 as the seeding unit" instruction has no U2 step to attach to.

### `INTERNAL_CONTRADICTION` — The damper's polarity is inverted between U6.5 and U9.10: one says a shut damper reduces draught, the other requires a shut damper to be the hottest state.

- **Tasks:** U6.5 Step 2, U6.5 Step 8, U9.10 Step 1, U9.10 Step 5
- **Evidence:** U6.5 Step 2: "Pin the curve's shape … **a shut damper and a venting door each reduce it**". U6.5 Step 8: "replace `float natural = IwexValues.BfNaturalDraughtFactor;` (`:1229`) with `float natural = StackDraught.NaturalDraughtFor(StackCourses, DamperOpen, Venting)`". Against that, U9.10 Step 1: "with the drawn stack height and **the damper shut**, ComputeHeatBalance's T_process must clear MeltingPoint = 1600", and U9.10 Step 5: "Damper as preheat: damper **open = damped fire** = the anneal; closing it on cold pots destroys them."
- **Consequence:** One implementation of `NaturalDraughtFor` cannot satisfy both. Whichever polarity is coded, the other unit's headline test is written against the opposite sign — and because both are 'a plausible temperature', the crucible furnace either never melts (U6.5's sign) or the preheat/anneal mechanic inverts and cold pots survive the fierce fire (U9.10's sign). U9's gate ('T_process ≥ 1600 C asserted at the drawn stack height with the damper shut') is unreachable under U6.5's rule.

### `INTERNAL_CONTRADICTION` — U4.5 Step 2 and the U4 trap claim that leaving HearthGlyph in the Chargeable assertion pair is silent; the rig makes it a hard test failure, and U10's own trap says so.

- **Tasks:** U4.5 Step 2, U4 Traps, U10.5 Step 5, U10 Traps
- **Evidence:** U4.5 Step 2: "Leaving both in is not a compile error and not a test failure — it just silently stops proving anything about which glyph carries which role." U4 trap: "Leave both in and the assertion still passes." But U10's trap describes the same method correctly: "`AssertRoleGlyphs`'s second half, which today asserts each listed glyph is used by some cell (FurnaceLayoutRig.cs:713-718)". Source, test/IronworkingExpanded.Tests/Fixtures/FurnaceLayoutRig.cs:713-718: `foreach (string glyph in glyphs) Assert.True(used.Contains(glyph), $"{def.Code}: no {what} cell holds '{glyph}' …")`, reached from the Chargeable call at :639-645 with `[ShaftGlyph, HearthGlyph]`.
- **Consequence:** U4.5 Step 1 (delete `.Role('h', CellRole.Chargeable)`) turns the rig red immediately, on both iwex furnaces and in three assemblies, and the executor has been told to expect silence. The likely reaction to an unexpected red oracle is to weaken it — which is precisely the vacuity the same trap exists to prevent.

### `UNDERSPECIFIED` — U7.3's section-law tolerance of 0.05 vx is unachievable — one of the plan's own three worked examples misses it, and the square-law lengths miss by up to 0.58 vx.

- **Tasks:** U7.3 Step 1, U7.3 Step 2, U7.3 Step 9
- **Evidence:** U7.3 Step 2: "flat => w(t)=V/(L0·t) with L held at L0; square => w=t and L(t)=V/t². Both reproduce the art to within **0.05 vx** (verified: 162/(18·2.75)=3.27 vs drawn 3.25; 162/2.25²=32.0 vs drawn 32; **40/(10·1.5)=2.67 vs drawn 2.6**)." 2.667 − 2.6 = 0.067, which already exceeds the stated tolerance. Measured from assets/editable/shapes/item-shingled-bar.json and item-rolled-rod.json (element from/to deltas): bar V = 162 (ShingledBar1 3×3×9 + SingledBar11 3×3×9), rod V = 40 (RolledRod200 2×2×10). Square law vs drawn: Grooved275 → 162/2.75² = 21.42 vs drawn 11×2 = 22 (Δ 0.58); Grooved250 → 25.92 vs 26 (Δ 0.08); rod Grooved150 → 17.78 vs 9×2 = 18 (Δ 0.22); rod Grooved125 → 25.6 vs 13×2 = 26 (Δ 0.40).
- **Consequence:** SectionLawTests is specified as a [Theory] 'over every measured stage' at 0.05 vx. Written as specified it fails on at least five rows on the first run. The two available repairs are both bad: widen the tolerance to 0.6 vx (at which point the test no longer distinguishes the section law from the exponent model U7.3 Step 2 also requires it to reject), or 'fix' the drawn art, which ruling 1 forbids.

### `BROKEN_DEPENDENCY` — `billet` is required as a StockForm by U7.6 and as a crop-table key by U7.5/U8.3, but U7.3 neither adds it as a StockForm nor keeps it in any set's accepts.

- **Tasks:** U7.3 Step 4, U7.3 Step 5, U7.6 Step 4, U7.5 Step 3, U8.3 Step 4
- **Evidence:** U7.6 Step 4: "extend the byType entry to `new { materialUnits = units, stockForm = form }` so cast stock is rollable at all. The form strings are **billet/bloom/slab** and must match `StockForm.All` keys after U7.3." U7.3 Step 4 adds only Rod: "Bloom becomes the shingled bar at 3 × 3 × 18 …, Slab stays 8×3×20 …, and **add Rod = 2×2×10**". U7.3 Step 5 rewrites both sets to "accepts [\"bloom\",\"rod\"]" and deletes flatwide from iwex. U7.5 Step 3 then requires "the half-step row that proves the point of keying on stage: **castbillet grooved 2.25 → 6 × rolledrod**". Source: src/IronworkingExpanded/BlockStructures/Forming/StockForm.cs — `All` contains only Bloom and Slab; src/IronworkingExpanded/BlockStructures/Forming/RollSetItemDefinitions.cs today has `accepts ["bloom", "billet"]` on both flat and grooved, i.e. `billet` is already a dangling accept the plan only flags for `slitting`.
- **Consequence:** After U7.3, `iwex:caststock-billet` carries `stockForm = "billet"`, `WorkPiece.FromStack` returns null for it (unknown form is 'refused rather than guessed'), and no set accepts it — so U7.6's stated purpose ('so cast stock is rollable at all') is not achieved for two of the three cast forms, and U7.5's castbillet crop row is keyed on a form that cannot reach 2.25.

### `GAP` — `HeatingHearthLayout.StockOf` cannot recognise any of the three cast stock items — the whitelist prefixes never matched the shipped codes — and no task fixes it, while U7's entry condition asserts it does.

- **Tasks:** U7 Entry condition, U7.1, U7 Traps
- **Evidence:** U7 entry condition: "that feed is an item whose code `HeatingHearthLayout.StockOf` (`HeatingHearthLayout.cs:64-79`) recognises — today that whitelist is the literal prefixes `stock-bloom`, `stock-slab`, `castbillet`, `castbloom`, `castslab`." Source, src/IronworkingExpanded/BlockStructures/Furnaces/HeatingHearthLayout.cs:72-77: `if (itemPath.StartsWith("castbillet")) … StartsWith("castbloom") … StartsWith("castslab")`. But the shipped item code is `iwex:caststock-{form}` — src/IronworkingExpanded/Items/CastStockItemDefinitions.cs:99 `.Create(domain, "caststock")` with `.VariantGroup("form", …)` over billet/bloom/slab. `"caststock-billet".StartsWith("castbillet")` is false for all three.
- **Consequence:** Every cast stock piece is silently refused by the reheat hearth. U7.1's soak (the unit's headline deliverable) can never be demonstrated on cast feed, and U7's gate walks only the wrought path, so the hole stays invisible. The plan's own trap names the mechanism ('Rename or re-code any stock item and the hearth silently stops accepting it') but treats it as a future risk rather than a live defect, and no task repairs the whitelist.

### `INTERNAL_CONTRADICTION` — With U6.4's and U6.5's own proposed coefficients the crucible furnace tops out at ~1570 °C — below the 1600 °C melting point U9 gives it — at the draught curve's peak and at every other stack height.

- **Tasks:** U6.4 Produces, U6.5 Step 1, U9.10 Step 3, U9.10 Step 1, U9.8 Step 2
- **Evidence:** The formula is fixed in code: `BlockEntityFurnaceCore.cs:1478-1490` — `tIn = BfCombustionBaseTemp + BfCombustionCokeGain * fuelFactor * airFactor + preheatGain`, `tLoss = BfRadiationLossBase + chargeLoss + ambientLoss`, with `BfCombustionBaseTemp` 950 (IwexConfig.cs:181), `BfCombustionCokeGain` 900 (:184), `BfMaxFuelFactor` 1.25 (:196), `BfRadiationLossBase` 120 (:221). The firebox branch never gets blast (`BlockEntityFireboxFurnace.cs:38` `RequiresBlast => false`, `:50-51` intake/threshold 0f), so `airFactor == natural`. U6.5 Step 1 proposes "`natural(courses) = base + gain·√courses − friction·courses²`, proposed `base 0.5, gain 0.11, friction 0.00102`, peak near 9 courses" → natural(9) = 0.7472. U6.4 Produces: "firebox-branch overrides (reverberatory transfer loss ≈250, firebox charge loss ≈100)"; U9.10 Step 2 overrides transfer loss to ~0. U9.10 Step 3: "override MeltingPoint => a new IwexValues.CrucibleMeltingPoint (1600)".
- **Consequence:** T_in(peak) = 950 + 900×1.25×0.7472 = 1790.6; T_loss = 120 + 100 + 0 = 220; T_process = 1570.6 °C — 29 °C short of 1600, and lower at every stack height other than 9 courses. The player buys stack height, watches the readout rise and fall past the peak, and never melts a pot: exactly the failure U9's own top trap names ("the furnace ships lit-and-never-melting"). Reaching 1600 needs the firebox charge loss ≤ ~70, but that number is U6.4's and U9 has no licence to move it.

### `UNDERSPECIFIED` — U4.9 says the torch-on-tap verb replaces the automatic charge-is-full ignition branch, but that branch lives on the shared core and the four firebox machines carry no tap to torch.

- **Tasks:** U4.9 Produces, U4.9 Step 1, U6.6 Step 7, U9.10, U9.2
- **Evidence:** U4.9 Produces: "A public ignition entry point on BlockEntityFurnaceCore (e.g. `public bool TryLightFromTap(BlockPos tapPos)`) that the tap calls, **replacing the automatic charge-is-full ignition branch** as the player-driven route". That branch is on the shared core, not the shaft branch: `BlockEntityFurnaceCore.cs:1261-1266`. The puddling furnace has no tap — `BlockPuddlingFurnaceCore.cs:115-116` marks only `.Role('F', CellRole.Firebox)` and `.Role('A', CellRole.Flue)`, and U6's own notes record "MetalTapPos/SlagTapPos are both null". U6.6 Step 7 depends on the automatic route: "confirm a completed puddling furnace with a full firebox and a 3-course stack actually crosses into Melting - that is the first time in the mod's history it can."
- **Consequence:** If the branch is removed, the puddling furnace, the reheat furnace, the coke oven and the crucible furnace lose their only ignition route and U6's and U9's gates become unreachable. If the branch stays, it fires the instant `_cachedIsFull` goes true, so the torch never gets a chance and the clay plug the whole blow-in ritual is priced on (U4.7) costs nothing. One of the two is dead either way and the plan does not say which.

### `GAP` — The reheat furnace — the entry point of the whole forming line — has no grid recipe, and no task in U6, U7 or U8 adds one.

- **Tasks:** U7.1, U7 Gate, U6.11 Step 5, U8 Gate
- **Evidence:** `src/IronworkingExpanded/Recipes/Grid/FurnaceRecipeDefinitions.cs:16-17`: `public static IEnumerable<ExRecipeDef> Definitions(string domain) => [BlastFurnace(domain), Cupola(domain)];`. No `OutputBlock` in any file under `Recipes/Grid/` names `furnace-heatingcore`, `furnace-heatinghearth` or `furnace-chargedoor`, though all three blocks ship (`Generated/IwexBlocks.g.cs:435`, `:478`, `:285`). U6.11 Step 5 adds only "the four puddling blocks". U7's gate opens "a shingled bar item, laid on a lit reheat furnace's hearth, rises above RollingTempC"; U8's entry is U7 complete.
- **Consequence:** Everything downstream of the reheat furnace — rolling, the section-law schedules, the shear, both fastener benches — is demonstrable only in creative mode. U7's gate and U8's "An iwex-only player can run the full fastener loop with no creative mode" cannot be walked.

### `BROKEN_DEPENDENCY` — U7.3 adds `Rod` to `StockForm.All`, which is the enumeration `StockItemDefinitions` builds items from against a two-row unit table — it throws on the missing key, and if the row is added it mints a duplicate rod item alongside U7.4's `iwex:rolledrod`.

- **Tasks:** U7.3 Step 4, U7.4 Produces, U6.11 Step 2
- **Evidence:** U7.3 Step 4: "add Rod = 2×2×10 — which is what makes the fork work: a rolledrod re-enters the same flat/grooved sets at their 1.5 and 1.0 gaps." `StockForm.cs:55-63`: `All` is exactly `{bloom, slab}`. `StockItemDefinitions.cs:30`: `StockForm.All.Values.Select(form => Stock(domain, form))`, and `:44` `.Attribute("materialUnits", Units[form.Name])` over the two-row dictionary at `:23-27` (`["bloom"] = 180, ["slab"] = 400`). U6.11 Step 2 forbids touching it further: "Change those two numbers to 400 and 1200 and stop there." U7.4 separately Produces "iwex:rolledrod (100 u)".
- **Consequence:** Adding Rod throws `KeyNotFoundException` while enumerating the defs, which takes down every iwex definition test. Adding the missing row instead silently ships a third creative stock item `iwex:stock-rod` at shape `iwex:forming/stock-rod-200` that duplicates `iwex:rolledrod` — two items for one thing in the handbook, the crop table and the reheat hearth, and a golden for a product nothing produces.

---

## Medium (20)

### `BROKEN_DEPENDENCY` — U8.9's Consumes block cites StockMesh.SideOf and StockMesh.CacheKey as its house-style reference, but U7.2 deletes StockMesh.cs outright and U7 runs before U8.

- **Tasks:** U8.9, U7.2 Step 5
- **Evidence:** U8.9 **Consumes:** "The pure-function house style (StockMesh.SideOf at src/IronworkingExpanded/BlockStructures/Forming/StockMesh.cs:36, StockMesh.CacheKey at :67-73); GameMath.MurmurHash3 …".  U7.2 Step 5: "Delete StockMesh.cs and StockMeshTests.cs entirely — SidePlacement, SideOf and CacheKey exist only to draw the lopsided piece the settled model deletes. **Do not leave a stub.**"  Execution order #9 places U8 after U7. StockMesh.cs verified present in src today.
- **Consequence:** By the time U8.9 is written its only named precedent is gone, and the jitter/caching conventions the task is meant to copy (reproducible seeded offsets, a stable cache key) have to be reinvented — precisely the conventions its Step 5 says must not drift ("An RNG here makes the pile jump on every re-tesselation").

### `ORDERING` — U7.9's schedule-walk invariant needs the declared mandatory crop points, which only U7.5's StockProducts supplies — but U7.9 is placed before U7.5 in the plan's own task ordering and does not list it in Consumes.

- **Tasks:** U7.9 Step 5, U7.5
- **Evidence:** U7's tasks appear in the order U7.6, U7.7, U7.2, U7.3, **U7.9**, **U7.5**, U7.4, U7.1, U7.10.  U7.9 Step 5: "in RecoverabilityTests.cs, for every (StockForm, RollSetSpec) pair the mod ships, walk every gap and every half-step and assert each reachable stage is ≤ 48 or **is a declared mandatory crop point**."  U7.9 **Consumes:** "public enum FeedVerdict — MillFeed.cs:5-29; public static FeedDecision Decide(...) — MillFeed.cs:95; WorkPiece.Length (from U7.2); SectionLaw.LengthAt (from U7.3)" — no crop table.  U7.5 **Produces:** "public static class StockProducts with `record Crop(...)` and `static Crop? At(string form, SectionClass section, float stage)`".
- **Consequence:** The invariant test — which U7's own trap calls "the only thing that would notice" a stranded 50-voxel piece — is written with no source of truth for legal crop points, so it either hard-codes them (and drifts from StockProducts the moment U7.5 lands) or silently degrades to a length-only check that passes on the castbloom hole it exists to catch.

### `INTERNAL_CONTRADICTION` — U9.7 tells the implementer to size the anvil crush for a lossy 110 u in / 100 u out, while the same task's Produces and Step 1 require exact conservation to 100 u. The 10 u loss belongs to the pot, not the anvil.

- **Tasks:** U9.7 Step 1, U9.7 Step 2, U9.10 Step 7
- **Evidence:** U9.7 **Produces:** "the cold branch of the blister fork: 3 chunks (25 u) + 5 bits (5 u) = **100 u exactly**".  U9.7 Step 1: "over a full break the payout equals the removed voxels' units to within one sub-bit crumb, and the total is exactly 3x25 + 5x5 = 100."  U9.7 Step 2: "pick a voxel count that makes units-per-voxel exact for **110 u in / 100 u out**".  U9.10 Step 7: "110 u blister -> 100 u crucible steel per pot (~9% melt loss)."  U9's own trap agrees the loss is the pot's: "Mass conservation across the blister fork. 110 u in / 100 u out **per pot**, and 3 chunks + 5 bits = 100 u exactly on the anvil."
- **Consequence:** The crushing arithmetic is derived against the wrong ratio, so the anvil route loses 10 u a second time and the end-to-end ledger the gate asserts ("a single mass assertion from ingot to poured ingot") is off by ~9% with the per-step tests all green — the exact failure the PigBreaking conservation tests were written to prevent.

### `DEAD_TASK` — U2.5 Step 6 rewrites `SolidifyBottomLayer`'s free-cell test and replaces `PileHoldsRejectedCharge`; U4.4 Step 7 then deletes `SolidifyBottomLayer` from the residue path while simultaneously telling you to reuse its free-cell rule. Nothing ever deletes `PileHoldsRejectedCharge` or `SolidifyBottomLayer` itself.

- **Tasks:** U2.5 Step 0, U2.5 Step 6, U4.4 Step 4, U4.4 Step 7
- **Evidence:** Plan U2.5 Step 0: "**U4.4 Step 7 deletes `SolidifyBottomLayer` from `ExtinguishResidue` outright**". Plan U2.5 Step 6: "*(May be moot — see Step 0. …)* Change `SolidifyBottomLayer`'s free-cell test … Replace `PileHoldsRejectedCharge(BlockPos)` with a column-based equivalent". Plan U4.4 Step 4: "SetBlocks the hearth block where a pool cell is free (reuse the free-cell rule from `SolidifyBottomLayer`, BlockEntityFurnaceCore.cs:1477-1493 …)". Plan U4.4 Step 7: "Remove it from `ExtinguishResidue` … and delete the `SolidProductBlock` / `DrainedMetalUnits` / `StampSolidProduct` / `ClearMoltenPools` virtual quartet". Code: `SolidifyBottomLayer` at `BlockEntityFurnaceCore.cs:1707`, `PileHoldsRejectedCharge` at `:1753`, both `private`.
- **Consequence:** Work is done in U2.5 on a method U4.4 orphans, and after U4.4 two private methods remain with no caller and no owner — including the wrong-family salvage guard, whose reason ("freezing the pool over wrong-family charge destroys salvage the player is owed") the plan insists must be kept verbatim. It will be kept as dead code, or deleted along with the rule.

### `GAP` — Three new defs are created with no golden file and no bless step, and Goldens_exactly_cover_the_defs fails on a def with no golden.

- **Tasks:** U6.10, U9.7 Step 5, U9.8
- **Evidence:** Verified guard: test/IronworkingExpanded.Tests/Definitions/IwexDefinitionGoldenTests.cs:40-45 — `public void Goldens_exactly_cover_the_defs()` … `Assert.True(missing.Count == 0, "defs with no golden file (unmigrated?): " + …)`. Goldens are one file per def: `ls test/IronworkingExpanded.Tests/goldens/iwex/itemtypes/` returns 28 entries including per-metal families `castiron/` (axe, chisel, hammer, ingot, knife, metalbit, metalnailsandstrips, metalplate, pickaxe, rod …) and `pigiron/`.  U6.10 Files·Create: `src/IronworkingExpanded/Items/WroughtBallItemDefinitions.cs`, `assets/iwex/shapes/item/puddled-ironball.json` — no golden, and Steps 1-9 contain no bless step.  U9.7 Step 5: "Define the crushed chunk item (shape iwex:item/metalchunk from U9.5) beside CastPartItemDefinitions, with materialUnits carrying 25" — Files·Create lists only `goldens/iwex/recipes/smithing/blister.json`, and Step 6 blesses only that recipe.  U9.8 Files·Create: `assets/iwex/config/metals/cruciblesteel.json` + a test; Step 2 notes "MetalDef.Durability + MetalToolEmitter already emit the whole tool family from this one entry" — no golden directory, no bless step in its five steps.
- **Consequence:** Each of those three units ends with its own gate red on a test it never mentions. U9.8 is the worst case: one metal def emits a ten-file golden family (matching castiron/), so a single unlisted step blocks the unit.

### `BROKEN_DEPENDENCY` — U9's entry condition requires the stack walk to consume CellRole.Damper, but U6.5 (the task that builds the walk) never marks or reads it — and U9.9 then adds the mark with no reader.

- **Tasks:** U9 Entry condition #1, U6.5 Step 8, U9.9 Step 4
- **Evidence:** U9 entry condition #1 (line 1983): "the counted stack walk must exist and must consume `CellRole.Flue` (`src/ExpandedLib/Blocks/Structures/CellRole.cs:124`) and `CellRole.Damper` (`:131`) - **both roles exist and neither has a single consumer in `src/` today**".  U6.5 Consumes names only "CellRole.Flue … BlockEntityPuddlingChimneyCap.IsOpen (:22); BlockEntityChargeDoor.IsVenting (:44)"; Step 8 reads the blocks directly: "`DamperOpen`/`Venting` read the cap and the door — the first consumers `IsOpen` and `IsVenting` have ever had." CellRole.Damper appears nowhere in U6.  U9.9 Step 4: "draw the damper at the flue base with CellRole.Damper. This is the first production use of CellRole.Damper anywhere."  Source: src/ExpandedLib/Blocks/Structures/CellRole.cs:132 `Damper,` — present, unread.
- **Consequence:** U9's stated entry is never satisfied by U6, and U9.9 ships a Role() mark nothing reads. The plan's own U9 trap says exactly why that is invisible: "a Role() hung on the wrong glyph is invisible to the DSL and to every other test."

### `INTERNAL_CONTRADICTION` — U9 attributes NaturalDraughtFor and the transfer-loss virtual to "U6.3", which in this document is the DisruptionMixFloor task; the arity it cites is also wrong.

- **Tasks:** U9 Entry condition #1, U9.10 Step 2, Execution order item 6, U6.4, U6.5
- **Evidence:** Execution order item 6: "U6 (puddling) — serial on the core. Delivers U6.3 NaturalDraughtFor, the only thing gating U9."  U9 entry #1: "**U6.3 landed.** `NaturalDraughtFor(courses, damperOpen)` must have replaced the flat read at … BlockEntityFurnaceCore.cs:1229".  U9.10 Step 2: "The reverberatory transfer term U6.3 introduces for the puddling and reheat furnaces …".  In this document U6.3 is titled "B8's fifth, undocumented cause — a firebox cannot hold a shaft's disruption floor, so a lit hearth snuffs itself in 30 s"; NaturalDraughtFor is U6.5's Produces ("`public static float StackDraught.NaturalDraughtFor(int courses, bool damperOpen, bool venting)`") and TransferLoss is U6.4's Produces ("`protected virtual float TransferLoss => 0f;`"). The same drift appears in U5's shared-files line ("U6.3 adds NaturalDraughtFor") and in U2's ("U2.5 deletes HopperTallPileCap", which is U2.7 Step 6).
- **Consequence:** U9's blocker resolves to the wrong task. Scheduling U6.3 alone — a one-line override — satisfies U9's stated entry while leaving the flat 0.5 draught in place, which is precisely U9's own headline trap: "The draught number is the whole machine … the furnace ships lit-and-never-melting." The two-argument signature U9 codes against also does not match the three-argument one U6.5 produces.

### `BROKEN_DEPENDENCY` — Three blocktype tasks declare `.EntityClass<T>()` for block entities that a later task in the same unit creates, and then gate on standing the structure up.

- **Tasks:** U5.2 Step 4, U9.1 Step 3, U9.9
- **Evidence:** U5.2 Step 4: "Author the def: ExBlockDef.Create(domain, \"burdenmaker\", \"ore/burdenmaker\").Class<BlockBurdenmaker>().EntityClass<BlockEntityBurdenmaker>()…" — `BlockEntityBurdenmaker.cs` is in U5.4's Files·Create. U5.2 Step 12 then requires the golden and shape tests green.  U9.1 Step 3: "Write BlockCokeOvenCore.cs modelled line-for-line on … BlockHeatingFurnaceCore.cs:35-141: Core(…) then .Class/.EntityClass/.Texture(all|north|south)/.MultiblockLayout." — `BlockEntityCokeOven.cs` is U9.2's Files·Create; U9.1 Step 9 requires the structure to "complete without forcing StructureComplete", which needs the be.  Same shape for U9.9 (BlockCrucibleFurnaceCore/BlockCrucibleHearth) vs U9.10 (BlockEntityCrucibleFurnace/BlockEntityCrucibleHearth).  Verified precedent: src/IronworkingExpanded/BlockStructures/Furnaces/Blocks/BlockHeatingFurnaceCore.cs:46-47 `.Class<BlockHeatingFurnaceCore>()` / `.EntityClass<BlockEntityHeatingFurnace>()`.
- **Consequence:** Each of those tasks does not compile as written until the following task lands, so their own "run the suite" gates cannot be met in the stated order. Either the task order inverts or each def task must be told to ship a stub be — neither is written down.

### `ORDERING` — U4.5 moves the heat-balance charge-loss denominator on both iwex shaft furnaces, but no step re-derives the calibration literals U3.7 Step 1 just pinned.

- **Tasks:** U3.7 Step 1, U3.7 Step 2, U4.5 Step 3
- **Evidence:** U3.7 Step 1: "removing it moves every number in HeatBalanceTests' calibration table (HeatBalanceTests.cs:79-104) … Pin the shipped numbers as literals in a new test before touching the denominator."  U3.7 Step 2: "Replace the shaft's MinChargeToIgnite with derived shaft capacity: `ChargeableCells.Count * ChargeColumn.BandsPerBlock * IwexValues.ChargeUnitsPerBand` … Assert the count in the test so the number cannot drift silently when U4 makes the crucible pool-only."  U4.5 Step 3: "the cold furnace's chargeable set drops from 39 to 36 cells and the cupola's from **5 to 4**".  Verified the cupola arithmetic is right today: BlockCupolaFurnaceCore.cs marks `.Role('c', CellRole.Chargeable)` and `.Role('h', CellRole.Chargeable)` with one `c` in each of Layers 2, 3, 4, 5 and one `h` in Layer 1 = 5.  U4.5's seven steps re-bless two goldens and add a descent test; none touches HeatBalanceTests or the pinned literals.
- **Consequence:** U4.5 changes `MinChargeToIgnite` by 3/39 on the blast furnace and 1/5 on the cupola, which is the denominator of `chargeLoss = BfChargeLossFull * clamp(mixCount / requiredMix)`. The literals U3.7 pinned two units earlier go red inside a unit whose gate never mentions them, and the honest fix (re-derive six T_process rows by hand) is unassigned — inviting exactly the "paste the number the run produced" failure U6.4 Step 7 warns against.

### `INTERNAL_CONTRADICTION` — U6's entry condition says U3 removes U6.3's DisruptionMixFloor override; U6.3 Step 7 and U6's own trap say it must survive U3.

- **Tasks:** U6 Entry condition, U6.3 Step 7, U6 Traps, Execution order step 6
- **Evidence:** U6 entry condition: "U6 before U3 is fine (U6.3 becomes a firebox-branch override **U3.3 later removes with the rest**)". U6.3 Step 7: "Note for U3: `U3.3` deletes `DisruptionMixFloor` outright for the shaft branch. The firebox branch has no columns and therefore keeps an explicit FSM, so **this override survives U3** unless U3 explicitly retires it." U6 trap: "Do not delete timers or thresholds on `BlockEntityFireboxFurnace` in sympathy with U3." Source: src/IronworkingExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:225 `protected virtual int DisruptionMixFloor => 144;` with no override anywhere in src/.
- **Consequence:** If U6 runs first and U3 obeys the entry condition, the override is deleted and both reverberatory hearths go back to snuffing after 30 s — the exact defect U6.3 exists to fix — with the iwex suite green except for the one FireboxTickTests case, whose failure message will point at the draught work (U6.5), not at this.

### `BROKEN_DEPENDENCY` — U9, U5, U6 and U7 cite parent-plan task numbers that address different tasks in this document: 'U6.3' is used for two U6.5/U6.4 deliverables, 'U3.3' for U3.7's config deletions, 'U5.3' for U5.9.

- **Tasks:** U9 Entry condition, U9.10 Step 2, Execution order step 6, U6 Shared files, U7 Entry condition, U4 Shared files
- **Evidence:** In this document U6.3 is 'B8's fifth cause — DisruptionMixFloor', U6.4 is the loss virtuals and U6.5 is 'NaturalDraughtFor'. Yet U9 entry: "1. **U6.3 landed.** `NaturalDraughtFor(courses, damperOpen)` must have replaced the flat read"; execution order: "Delivers U6.3 NaturalDraughtFor, the only thing gating U9"; U9.10 Step 2: "The reverberatory transfer term **U6.3** introduces" (that is U6.4 Step 5). Likewise U3.3 here is 'The melt condition at unit granularity' and U3.7 owns the config deletions, but U6 shared files says "U3.3 explicitly deletes the disruption floor and both extinguish thresholds" and U2.10 Step 6 says "Keep BlastMixRequiredToFire — U3.3 owns that one". U4 shared files says "BlockCupolaFurnaceCore.cs — U5.3 (cupola charges pig and scrap directly)" — that is U5.9; U5.3 is the burdenmaker cell classifier. The arity also disagrees: U6.5 Produces `NaturalDraughtFor(int courses, bool damperOpen, bool venting)`, U9 cites a two-argument form.
- **Consequence:** An executor scheduling U9 blocks on the wrong U6 task (a 20-minute DisruptionMixFloor override) and starts U9.5–U9.11 with no draught function at all — which is exactly the 'ships lit-and-never-melting' failure U9's own top trap names. The same mis-numbering makes 'confirm U3.3 is not in flight' checks look at the wrong file.

### `INTERNAL_CONTRADICTION` — The iwex test floor is quoted as 1179 in three places and 1132 in two; the file says 1179.

- **Tasks:** File collisions (test-floors row), U5.7 Step 13, U5 Traps, U9.11 Step 7, U9 Traps
- **Evidence:** Collisions table: "`scripts/test-floors.txt` … Five rows; iwex currently **1179**." U5.7 Step 13: "Lower IronworkingExpanded.Tests in scripts/test-floors.txt (**currently 1179**)". Against that, U9.11 Step 7: "raise scripts/test-floors.txt IronworkingExpanded.Tests above its **current 1132**" and U9 trap: "scripts/test-floors.txt (IronworkingExpanded.Tests = **1132**)". Source: scripts/test-floors.txt — `IronworkingExpanded.Tests = 1179`, with the dated baseline header `IronworkingExpanded.Tests 1243`.
- **Consequence:** U9 is the last unit to touch the file and would 'raise' the floor against a number 47 below the real one — i.e. it can set a floor that is lower than today's and record it as a raise. The file exists precisely because a suite that fails to load exits 0 with no summary; a floor set from a stale anchor is the one failure mode it cannot survive.

### `INTERNAL_CONTRADICTION` — U9.9 Step 8 instructs adding the two new machines to a test loop whose assertion is an exact-set equality that the crucible furnace's own required role set violates.

- **Tasks:** U9.9 Step 8, U6.5 Step 12
- **Evidence:** U9.9 Step 8: "assert `RoleNamesOf` returns exactly [\"Firebox\",\"Flue\",\"Damper\"]. Also **add both new machines to the loop in FurnaceRoleCellsTests.cs:733-758**, which today pins the exact role set for the two hearths only." Source, test/IronworkingExpanded.Tests/Blocks/Furnaces/FurnaceRoleCellsTests.cs:748-753: `foreach (ExBlockDef def in new[] { PuddlingDef(), HeatingDef() }) { List<string> names = RoleNamesOf(def); Assert.Equal(["Firebox", "Flue"], names); … }` — with the method's own comment at :746-747: "this exact-set assertion is the only thing standing behind the marks, so **do not relax it to a subset**." U6.5 Step 12 independently requires that same assertion to keep holding.
- **Consequence:** Adding the crucible core to that loop fails on `Assert.Equal(["Firebox","Flue"], names)`. The obvious repair — relaxing the equality to a subset check — is explicitly forbidden by the test's own comment and would silently un-pin the two hearths' role sets, which U6.5 Step 12 states as a gate.

### `INTERNAL_CONTRADICTION` — U6.4 states the shaft calibration table has three rows pinning 1420/1740/970; it has six, and 1740 is not one of them.

- **Tasks:** U6.4 Step 2, U6.4 Step 8, File collisions (HeatBalanceTests row), QW3
- **Evidence:** U6.4 Step 2: "Its existing `[InlineData]` rows pin the shaft furnace at **1420/1740/970 °C** - those must not move." U6.4 Step 8: "The **three** pre-existing shaft calibration rows must be untouched and green." The plan's own collisions table says six: "the six literal T_process rows (:81-86 → 1420 / 1745.5 / 1577.5 / 1262.5 / 1588 / 970)", and QW3 says "converts six calibration rows". Source, test/IronworkingExpanded.Tests/Blocks/Furnaces/HeatBalanceTests.cs:94-99: six `[InlineData]` rows asserting 1420f, 1745.5f, 1577.5f, 1262.5f, 1588f, 970f. 1740 appears only in the source comment as the retired config constant.
- **Consequence:** U6.4 Step 7 requires re-deriving 'every asserted temperature in the new [InlineData] rows … by hand'. An executor working from Step 2's three-number list leaves 1577.5/1262.5/1588 unexamined, which are exactly the melt/stall boundary rows the firebox loss overrides move.

### `INTERNAL_CONTRADICTION` — U7.3 Step 3 keeps StockForm.MaxWidth on the argument that no narrow schedule reaches it, while stating the bar reaches 9.0 — above the shipped MaxWidth of 8.0.

- **Tasks:** U7.3 Step 3, U7.3 Step 4, U7.3 Step 9, U7 Gate
- **Evidence:** U7.3 Step 3: "Keep MaxWidth: **no narrow schedule reaches it** (the bar tops out at 9.0 wide, the rod at 4.0), so U7 does not have to settle rolling.md Open #1". Source, src/IronworkingExpanded/BlockStructures/Forming/StockForm.cs: `public static readonly StockForm Bloom = new("bloom", 3f, 3f, 8f, 16f, 0.846f);` — the third argument is MaxWidth = 8f. U7.3 Step 4 changes only BaseLength ("Bloom becomes the shingled bar at 3 × 3 × 18"), and U7.3 Step 9 concedes the endpoint: "restate it as V/L0 = **9** for this form only". U7's gate requires the same: "eight feeds later it is at 1.0 and reproduces CutPlate (**9** × 1 × 18)". Measured art agrees: item-rolled-beam.json CutPlate1/CutPlate2 are 9.0 × 1.0 × 9.0.
- **Consequence:** Either MaxWidth is consulted — and the final flat stage clamps at 8.0, so the gate's CutPlate assertion and the U7.5 crop row 'shingledbar flat 1.0 → 2 × game:metalplate' both fail — or MaxWidth is not consulted, in which case the field is dead and the stated reason for keeping it is fiction. The step decides neither.

### `UNDERSPECIFIED` — U6.4's proposed firebox charge loss makes U9.10's hard 1600 °C requirement unreachable at any stack height.

- **Tasks:** U6.4 Step 5, U6.5 Step 1, U9.10 Step 1, U9 Gate
- **Evidence:** U6.4 Step 5 proposes "firebox-branch overrides (reverberatory transfer loss ≈250, **firebox charge loss ≈100**)". U6.5 Step 1 fixes the curve: "`natural(courses) = base + gain·√courses − friction·courses²`, proposed base 0.5, gain 0.11, friction 0.00102, peak near 9 courses". U9.10 Step 1: "with the drawn stack height and the damper shut, ComputeHeatBalance's T_process must **clear MeltingPoint = 1600**", with the transfer loss overridden to ≈0 (Step 2). Arithmetic on the shipped formula (verified against IwexConfig.cs:181-225 — BfCombustionBaseTemp 950, BfCombustionCokeGain 900, BfMaxFuelFactor 1.25, BfRadiationLossBase 120, and reproduced by HeatBalanceTests' 970 row): natural(9) = 0.5 + 0.33 − 0.0826 = 0.7474; T_in = 950 + 900 × 1.25 × 0.7474 = 1790.8; T_loss = 120 + 100 + 0 = 220; T_process = **1570.8** — 29 °C short, at the peak of the curve, i.e. the best any stack height can do.
- **Consequence:** U9's headline gate assertion cannot pass with U6.4's numbers. The crucible furnace's charge loss has to be driven to ≈0 (T_process 1670.8) or BfRadiationLossBase overridden as well — neither of which any task authorises, and U6.4 Step 5 explicitly backs its number with a new config key and a doc comment, so it reads as chosen rather than provisional.

### `GAP` — Nothing teaches the reheat hearth about the rolled products, so anything cropped on the shear can never be reheated — and the rod branch is the longest schedule in the mod.

- **Tasks:** U7.4 Produces, U7.1, U7.3 Step 4, U8.6 Step 3
- **Evidence:** `HeatingHearthLayout.cs:63-78` `StockOf` is a closed prefix list: `stock-bloom`, `stock-slab`, `castbillet`, `castbloom`, `castslab`, else `null`. U7.4 Produces four new codes — "iwex:rolledrod (100 u), iwex:rod (25 u), iwex:nailplate (100 u), iwex:beam (200 u)" — and no U7 task edits `StockOf`; U7's own trap flags the mechanism ("HeatingHearthLayout.StockOf ... is a hard-coded prefix whitelist ... Rename or re-code any stock item and the hearth silently stops accepting it") without assigning the fix.
- **Consequence:** A rolledrod being taken from 2.0 down to 1.0 on the grooved set is four gaps × two rounds of passes, cooling through `RollingPass.Cool` the whole way; once it drops below `RollingTempC` it can neither bite nor be reheated, and the rivet route (rolledrod → 1.5 → 1.0 → shear → 4 rods) soft-locks. The refusal is silent — the hearth just does not accept the click.

### `UNDERSPECIFIED` — The reverberatory transfer loss is put on the firebox branch and only the crucible is told to override it, so the coke oven inherits a bridge loss for a bridge it does not have — and its coking temperature is never stated.

- **Tasks:** U6.4 Step 5, U6.4 Step 6, U9.2 Step 3, U9.2 Step 5
- **Evidence:** U6.4 Step 5: "Override both on `BlockEntityFireboxFurnace`: `ChargeLossFull` to the firebox's real thermal sink ... and `TransferLoss` to the reverberatory bridge loss." U6.4 Step 6 names exactly one exemption: "the crucible furnace is on the firebox branch but its pots sit in the coke bed with no bridge, so it must override transfer loss to ≈0." U9.2 Step 3 derives the oven from the same branch and sets "MeltingPoint to the coking temperature (a new IwexValues key - do not reuse BfIronMeltingPoint)" without a value, and U9.2 never mentions transfer loss.
- **Consequence:** With the branch default (≈250) and the firebox charge loss (≈100), a coke oven on flat natural draught tops out at 950 + 900×1.25×0.5 − 470 = 1042.5 °C. Real coking wants ~1000–1100, so whether the oven works at all is decided by an unstated constant and an unstated override — and the oven gates every coke-fired machine downstream of it.

### `INTERNAL_CONTRADICTION` — The fettle loop closes on exactly 3 tap cinder per heat, but at the settled 375 u pig the puddling remainder is 175 u, which no integer cinder value divides into three.

- **Tasks:** U6.10 Step 1, U6.10 Step 2, U6.10 Step 6
- **Evidence:** U6.10 Step 1: "balls = ⌊(9 × pigMass) / ballMass⌋ and cinder = the remainder. At the settled 375 u pig that is 16 balls + 175 u; at the 150 u the code still ships (`ItemPig.cs:33`) it is 6 balls + 150 u." U6.10 Step 2: "`docs/design/processes/puddling.md` § Open #1 is explicit that at exactly 3 cinder the fettle loop closes perfectly (3 cinder → 3 fettle → the next heat's 3 rows)." Confirmed in source: `src/IronworkingExpanded/Items/ItemPig.cs:33` `public const int PigUnits = 150;`.
- **Consequence:** 175 ÷ 3 = 58.33: at the settled mass the closing condition is not expressible with a whole-unit cinder, while it closes exactly at the mass the code still ships (150 ÷ 3 = 50). The player's puddling loop either leaks fettlestock every heat or gains it, and the decision is spread across three numbers (pig mass, ball mass 200, cinder value) that no single task owns — the pig re-mass in particular is only ever assigned to a "U1.7" that this document does not contain.

### `UNDERSPECIFIED` — Two of the grooved roll set's four gap bands are unreachable for the only wrought stock the mod ships, because the section law drives the bar past U7.9's 48-voxel refusal.

- **Tasks:** U7.3 Step 5, U7.3 Produces, U7.9 Produces, U7.9 Step 5
- **Evidence:** U7.3 Step 5: "grooved: family \"grooved\", section \"square\", accepts [\"bloom\",\"rod\"], gaps [2.5,2.0,1.5,1.0]". U7.3 Produces the law: "square => w=t and L(t)=V/t²", and U7.3 Step 1 measures the bar at "ShingledBar1 3×3×9 with a 9-long child (18 long, V=162)". U7.9 Produces: "const int WorkPiece.MaxLengthVoxels = 48; MillFeed.Decide refuses any feed whose resulting length would exceed 48".
- **Consequence:** For the bar, L(1.5) = 162/2.25 = 72 and L(1.0) = 162 — both refused. Half the grooved barrel is dead for wrought stock, and a player who clicks those bands gets `iwex-rollingmill-toolong` with nothing saying the bands exist for the rod. U7.9 Step 5's invariant as written ("walk every gap and every half-step and assert each reachable stage is ≤ 48 or is a declared mandatory crop point") fails on the shipped sets unless "reachable" is redefined to mean "not already refused".

---

# Forks needing a ruling (9)

Cross-check each fork against the expansion plan's ruled-forks boxes ("Four forks ruled 2026-08-05" and
"Four more forks ruled 2026-08-05") before treating it as open — several of these were ruled there and
are settled; do not re-open a ruled fork.

## Q1. Which crop table survives — U7.5's StockProducts keyed on (form, section, stage), or U8.3's ShearCrops keyed on (form, thickness)?

**Why the plan cannot settle it:** Both tasks delete the same members of RollSetSpec and build a replacement, and U8's entry condition lists U7.5 as a prerequisite of the unit that re-does it. The section-blind key cannot express U7.5's own worked rows, so this is not a naming choice.

- **U7.5's StockProducts is the table; U8.3 drops its ShearCrops and consumes StockProducts.At(form, section, stage)** — One table, section-aware, half-steps expressible; U8.3 shrinks to ShearFeed plus the mass-ledger tests. The mill still makes no product (the settled 2026-07-29 position), and U8.4 Step 6 must be re-pointed.
- **U8.3's ShearCrops is the table; U7.5 shrinks to deleting Outputs/OutputAt with no replacement** — The crop table lands with the shear that owns it, but keyed on form+thickness it cannot distinguish flat 2.0 (2 beams) from square 2.0 (4 rolledrods). The two branches of the schedule collapse onto one answer unless the section is folded into the form name.

## Q2. Is U6.11 (ball → helve → shingled bar, plus the four puddling grid recipes) in scope for U6?

**Why the plan cannot settle it:** U6.11's own Step 1 says it "is not in the plan's U6 deliverables table … Confirm the scope with the host rather than assuming", while U6's Gate and U7's entry condition both depend on its output. The plan flags the conflict and declines to resolve it.

- **In scope — U6.11 lands with U6** — U6's gate is walkable end to end and U7 has its wrought feed. U6 grows a mass-reconciliation edit (bloom 180→400, slab 400→1200) that U7.3 also touches, and a smithing recipe provider that did not exist.
- **Out of scope — defer the tail** — U6 ends at wrought balls in the hearth with nothing to do with them, and U7's stated entry condition ("the mill's wrought feed is a shingled bar") is unmet. U7 would have to start from cast stock only, which drops the whole puddling→rolling join out of the critical path.

## Q3. Does U4.5 also drop `Role('p', Chargeable)` on smex's hot blast furnace?

**Why the plan cannot settle it:** U4.5 Step 6 explicitly refuses to touch `BlockBlastFurnaceCoreHot.cs:105-106`, deferring to 'the smex remake'. But U4.4 makes `PoolCells` hold live molten cells on every `BlockEntityShaftFurnace`, and the hot furnace's two `p` cells are the floor of two of its nine charge columns (Layer 1 `. . # p p T`, Layer 2 `. S c c c #`). The deferral was written about crucible size, not about the pool/charge collision U4.4 creates.

- **Extend U4.5 to smex: drop `Role('p', Chargeable)`, re-bless the smex golden** — One extra legend edit and one golden, and all three shaft furnaces stay coherent. It does pre-empt part of the smex remake's layout decisions, and the hot furnace's crucible stays the narrow two-cell one so its capacity numbers diverge from iwex's three.
- **Leave it: hot furnace ships with charge and molten metal on the same two cells** — Hearthmetal overwrites standing charge piles during normal melting; `SyncChargeBlocks` then refuses to rebuild them, so two columns permanently render one block short while still counting their units. No test in any suite can see it. If this is accepted, it needs to be written into the WORKLOG as a known data-loss path, not left implicit in a size deferral.

## Q4. Is the rolled rod meant to re-enter the mill at all, and if so where does it get reheated?

**Why the plan cannot settle it:** U7.3 Step 4 makes the rod a StockForm specifically so it can re-enter the flat/grooved sets, but no task gives it a stockForm attribute and HeatingHearthLayout.StockOf plus the Stock enum (five members, five art elements) have no rod entry. The reheat hearth's art has no rod element to draw.

- **Add a Rod member to HeatingHearthLayout.Stock plus a stockForm attribute on iwex:rolledrod, and accept that the hearth draws a rod with a stand-in element** — The fork works end to end and U8's rivet route becomes reachable; costs an art decision on the hearth shape that no U6/U7 task currently owns.
- **Drop the rod StockForm; a rolledrod is a finished product that only ever goes to the shear** — U7.3's 'accepts ["bloom","rod"]' collapses to ["bloom"], the 1.5/1.0 gaps lose their second consumer, and the rod schedule drawn in item-rolled-rod.json (Grooved175/150/125, Flattened175/150/125) has no producer.

## Q5. Does closing the damper make the fire fiercer or weaker?

**Why the plan cannot settle it:** U6.5 Step 2 pins 'a shut damper and a venting door each reduce it' as a property of the pure draught function; U9.10 Steps 1 and 5 require the opposite ('damper open = damped fire = the anneal', and T_process ≥ 1600 with the damper shut). Both are asserted as settled, and one function signature serves both machines. No design page is cited on both sides.

- **Shut damper = less draught = cooler (U6.5's rule)** — Physically conventional and matches a chimney damper. U9's crucible furnace must then run its melt with the damper open and its anneal with it shut, inverting U9.10 Step 5's preheat mechanic and the cold-pot-cracking test.
- **Shut damper = fierce fire (U9.10's rule)** — Matches the crucible furnace's 'preheat on the open damper, shut it to melt' ritual and its 1600 °C gate. U6.5's StackDraughtTests must then pin the reverse, and the puddling furnace's damper reads backwards to anyone who has used a stove.
- **The damper gates the fire, not the stack: open = bypass/damped, shut = full draught through the bed** — Reconciles both by redefining what the mark means, but CellRole.Damper then names a different thing from the chimney damper the puddling cap models, and U9.9 Step 6's reuse of BlockPuddlingChimneyCap as the damper block stops being justified.

## Q6. Who owns the crop table — the mill-side `StockProducts` (U7.5) or the shear-side `ShearCrops` (U8.3) — and is the roll set's section class part of its key?

**Why the plan cannot settle it:** Both tasks build it, both delete `RollSetSpec.Outputs`/`OutputAt` and the `Out()` helper, and the two record shapes differ: U7.5 keys on `(Form, SectionClass, float Stage)` and U8.3 on `(Form, int StageKey)` in hundredths. U7.4 validates against the first; U8.4 calls the second. The plan gives no rule for which survives.

- **U7.5's StockProducts, section-keyed, float stage matched with a 1e-3 tolerance** — Flat and grooved schedules can declare different products at the same thickness (2.0 → 2 beams flat, 4 rolledrod grooved), which is what U7.5 Step 2's table actually needs. U8.3 collapses to a thin wrapper and its Files list must stop naming RollSetSpec.
- **U8.3's ShearCrops, hundredths-integer keyed, no section** — Matches ruling 4's stage key exactly and cannot collide 2.25 with 2.2, but cannot express the flat/grooved fork at one thickness — U7.5 Step 2's own first two rows both sit at stage 2.0.
- **One table with both: (Form, SectionClass, int StageKey)** — Neither task as written produces it; U7.5 must be respecified and U8.3 reduced to consuming it. Cleanest, but it changes both units' Produces blocks and U7.4/U8.4's call sites.

## Q7. Does the puddling furnace get its own process temperature (`PuddlingProcessTempC`), or does it keep iron's 1482?

**Why the plan cannot settle it:** U6.6 Step 1 flags this explicitly as an open decision the task 'must not guess', with two settled documents disagreeing — puddling-furnace.md Open #2 calls 1482 'WRONG, AND KNOWINGLY LEFT', crucible-furnace.md computes puddling's target at 1482 and lands it at ≈3 courses. The verified arithmetic makes it load-bearing: a full one-cell firebox reaches T_process 1082.5 °C flat, and even at the draught curve's peak (9 courses) it reaches ~1790 − losses.

- **PuddlingProcessTempC ≈ 1200 (the CupolaCastIronMeltingPoint reference)** — Reachable at ~3 courses with U6.4's proposed losses, and consistent with 'puddling works pig in the pasty state'. Requires deleting the 'WRONG, AND KNOWINGLY LEFT' comment and accepts that a reverberatory furnace never melts iron — which is the premise crucible-furnace.md builds a mechanic on.
- **Keep BfIronMeltingPoint 1482** — Needs ~9 courses and the loss overrides tuned low; the puddling furnace then physically melts iron, contradicting the 'a reverberatory furnace cannot melt iron' rule that U6.4 Step 6 and U9 both lean on.
- **Derive it as an invariant rather than a constant (U6.6 Step 3's shape) and let calibration decide** — The test survives retuning, but the number ships as whatever the first implementer picked, and U9.10's 1600 assertion inherits the same unpinned loss coefficients.

## Q8. Does the rivet route yield more fasteners per unit of iron than the nail route, or exactly the same?

**Why the plan cannot settle it:** U8.6 Step 1 and U8's Gate state opposite requirements in the same unit, and the deciding number — a rivet bundle's declared `materialUnits` — is never given. It is a pure balance decision the source cannot settle.

- **Rivets yield more per unit (e.g. a bundle at 12.5 u → 8 per 100 u)** — The rivet machine earns its extra mill schedule and second bench; but nails become dominated everywhere iwex accepts either fastener under U8.7's substitution rule, so the nail machine survives only on build cost, and U8's Gate clause must be struck.
- **Both routes yield 4 per 100 u; rivets are a materials gate, not a yield gate** — U8's Gate stands and mass conservation is simple, but the rivet machine sells nothing to an iwex-only player — its only consumer is lpex's boiler, which makes it an lpex prerequisite dressed as an iwex bench. U8.6 Step 1's stated rationale ("that yield advantage is the entire trade the substitution rule sells") has to be replaced with a different reason to build it.

## Q9. Is ignition player-driven (torch on an open tap) or automatic (charge-is-full)?

**Why the plan cannot settle it:** U4.9 says the torch route replaces the automatic branch, but that branch is on the shared `BlockEntityFurnaceCore` and the four firebox machines have no tap. Whichever survives, the other is dead code, and U6.6 Step 7 and U9's gate both assume the automatic one.

- **Torch on the shaft branch, automatic on the firebox branch** — Blowing in costs a clay plug and is a deliberate ceremony on the blast furnace and cupola, while hearths and ovens light themselves when loaded. Needs the ignition branch pushed down onto the two branches rather than deleted from the core — a change U4.9 does not scope, and one that lands in the same file U3.6 is already rewriting.
- **Automatic everywhere; the torch is flavour** — U6 and U9 keep working unchanged, but U4.7's plug cost and U4.9's whole blow-in sequence stop being a decision — the furnace lights itself before the player reaches the tap, and 'tapping is a decision rather than a reflex' loses its counterpart at the other end of the campaign.
