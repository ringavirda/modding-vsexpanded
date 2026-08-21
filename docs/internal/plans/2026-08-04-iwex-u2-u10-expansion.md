# iwex U2–U11 — the verified expansion

> **⛔ Reading note, added 2026-08-14.** This plan predates the `iwex`+`lpex` merge. Where it says
> **`iwex`** as a *domain or assembly* it now means **`iiex`** (block codes, lang keys, asset paths,
> namespaces). Where it says `iwex` as a *scope* - "finish iwex", "an iwex-only player" - the phrase no
> longer refers to anything: ruling **M2** retired per-mod closure in favour of per-**loop**, and the
> early loop is the whole of `iiex`. Paths and type names below were repointed at their live homes on
> 2026-08-14; `iwex:` / `lpex:` **code literals** were deliberately left, being historical migration
> sources.
>
> ★★ **Triaged 2026-08-14, verified against `src/` rather than against the checkboxes** (which were
> never ticked — completion is recorded in prose under each unit heading):
>
> | Unit | State |
> |---|---|
> | **U2** cutover to `ChargeColumn` | **DONE**, landed 2026-08-06 |
> | **U3** counter-current furnace | **DONE**, landed 2026-08-06 |
> | **U4** hearth / taps / plug | ✅ **DONE** — U4.1–4.3 landed 2026-08-07, U4.4–U4.9 on 2026-08-20/21. The float pools are gone, the crucible is pool-only, both taps draw their own shape with a clay plug for a closed state, and a shaft is blown in with a flame through an open tap-hole |
> | **U5** burdenmaker | **DONE** |
> | **U6** puddling | **DONE 2026-08-21** — all eleven tasks. B8 closed, the furnace runs a whole heat, the chassis is craftable |
> | **U7** reheat and rolling | ✅ **DONE as far as later rulings allow, 2026-08-21.** The mill, `WorkPiece`'s two-round model, the roll sets, `MillSchedule`, the stage ladders, the rolled products (B3c) and the shear all landed outside this plan; **U7.1 (the reheat soak) landed here** and closed the loop, fixing two live defects on the way — `ShaftCentre` named the wrong cell, and the mill's deck read mirrored at `ns`. **U7.9 is retired** (the mill never refuses on length). **U7.3's section law** moved to the item-piles plan. ⛔ **U7.10 is blocked**: roll sets are lathe-turned under the machining-line ruling, so they stay creative-only until the lathe exists |
> | **U8** fasteners / shop floor | ★★ **DONE 2026-08-21** as far as this plan's tasks go. The shear (U8.3/U8.4) and the stock rack (U8.10) landed outside it; **U8.2, U8.5, U8.6, U8.7 and U8.11** landed in this plan, with recipes, cost rows and lang (U8.8). ⛔ The **gate** is not met and cannot be met here: it asks for a survival walkthrough, and no roll set is craftable, which is **U7.10** on the unbuilt lathe. ⛔⛔ U8.5's *"no die"* step was stale - the machining line ruled the benches take dies, and they do |
> | **U9** coke oven, crucible steel | **OPEN** — neither exists |
> | **U10** connector check | **OPEN** |
> | **U11** ladle | **DESIGN ONLY** — art drawn, no code |
>
> ⛔ So the live queue in this file is **U8, U9, U10** (U6 closed 2026-08-21, U7 on the same day). Everything above U8 is a
> record. Read the unit's own opening paragraph before starting one: several were re-scoped by later
> rulings, and U7's was overtaken almost entirely.
> U11 (the ladle) was appended 2026-08-06 from a user handoff, after the drawn art and the layout
> arrived. It is a leaf — nothing in U2–U10 waits on it and it waits on nothing in them — so the title
> grew but no ordering below changed. The filename still says `u2-u10`; existing references use that
> name, so it stays.
> **Companion to [`2026-08-04-iwex-completion.md`](2026-08-04-iwex-completion.md).** That plan holds U1,
> the Global Constraints and the Commands block; this document is the task-level expansion of U2 onward —
> **87 tasks, 707 steps**, written 2026-08-04 with every claim verified against `src/` and cited by
> file:line.
> Caution: navigate by symbol, not by line number. Line numbers here were correct on 2026-08-04 and rot
> the same way as any other line anchor.

> ## Read first — [`2026-08-05-iwex-plan-audit.md`](2026-08-05-iwex-plan-audit.md)
> Open findings from checking this plan against `docs/design/**`; they target U4.4 onward and U6–U11
> (the shipped units are not covered). The rule it applies, and the rule to
> keep applying: `docs/design/**` is authoritative for **decisions**; this plan is authoritative for
> **sequencing**. When they disagree about a decision, the doc wins. Some findings run the other way
> (`DOC_STALE` — the plan is right and the doc is behind), so this is not a blanket "trust the docs";
> it is "check both, and know which question each answers".
>
> Its blocking finding is still live: `iwex:furnace-firebox` is a required cell in both reverberatory
> layouts and has no grid recipe anywhere — U6.11 must add it, or U6's own gate ("a player builds a
> puddling furnace from craftable blocks") cannot pass. Read the entries for your unit before starting it.

> ## And — [`2026-08-05-iwex-plan-coherence.md`](2026-08-05-iwex-plan-coherence.md)
> The companion check of the plan against itself and against `src/`: broken dependencies, gaps,
> contradictions, ordering, and operability from the player's chair. Two of its findings still change
> the schedule:
>
> 1. **U9 is gated on U6.5 (draught) and U6.4 (losses), not U6.3.** U6.3 produces only a
>    `DisruptionMixFloor` override; anyone scheduling U6 partially and stopping after U6.3 ships a
>    crucible furnace that lights and never melts.
> 2. **U7.5 and U8.3 both delete `RollSetSpec.Outputs`/`OutputAt` and both build a replacement crop
>    table** — with incompatible keys. U7.5's section-aware table is the one that survives (see the
>    settled table below); U8.3 shrinks to `ShearFeed`.
>
> It ends with the forks that still need a ruling before the affected units start. Do not guess at
> one — they are forks precisely because the source cannot settle them.

> ## Four forks ruled 2026-08-05 — settled, do not re-open
>
> **1. A shut damper means less draught and a cooler fire.** U6.5's rule wins; it is the conventional
> stove reading. **U9.10 is the task that must change**, not U6.5 — its Steps 1 and 5 currently say
> "damper open = damped fire" and require T_process ≥ 1600 with the damper *shut*. The crucible furnace
> therefore **melts with the damper open and anneals with it shut**. `CellRole.Damper` keeps meaning the
> chimney damper, so U9.9 Step 6's reuse of `BlockPuddlingChimneyCap` stays justified.
>
> **2. Ignition is a fraction of capacity, not a full shaft.** This means `MinChargeToIgnite` **splits into
> two numbers** — it is currently both the fire threshold *and* the cold-charge heat-penalty denominator (its
> own doc-comment says so). The threshold stays a fraction; the **denominator becomes the full geometric
> capacity**. U2.9's "27 of 36 cells charged" readout stays a genuine status rather than a countdown to one
> event. `HeatBalanceTests`' `FullHearth` literal must be re-pointed at whichever of the two it meant.
>
> **3. The raceway slice spans a whole round** — depth ≥ one coke course + one burden course. `fuelFrac` then
> reads the charged ratio steadily (~3/16 cold) and blast demand sits at ~2.09 atm against the twin-tub's
> 2.2 atm ceiling. The rejected reading (slice inside one course) makes `fuelFrac` alternate ~1.0/~0.0,
> swinging required pressure 1.2 → 3.5 atm against a 2.2 atm blower, so the tuyere gate closes on every burden
> course and the furnace pulses hot and cold. The slice must still be shallow enough that U3.4's per-column
> hang and U3.3's unit-granular melt condition can tell columns apart.
>
> **4. `PuddlingProcessTempC = 1400` — the physically and historically correct number.** *(user: "we've changed
> chimney behavior to allow the puddling furnace to work; we should choose the physically and historically
> correct temperature")*
>
> **The mod's own two melting points bracket it**, and that bracket *is* the puddling process:
> `CupolaCastIronMeltingPoint` **1200** < **1400** < `BfIronMeltingPoint` **1482**. Hot enough to melt pig
> down; too cool for decarburised iron to stay liquid. As carbon leaves the bath the metal's melting point
> climbs from ~1200 toward pure iron's ~1538, crosses the bath temperature partway through, and the iron
> **"comes to nature" — balls up**. So the ball is *emergent from the temperature window*, not a scripted
> stage, which is what makes rabbling a real verb.
>
> Caution: Both previously-proposed numbers are wrong for a reason worth keeping: **1482** (the shipped value, which
> `puddling-furnace.md` already calls *"wrong, and KNOWINGLY left"*) would melt the wrought iron and there
> would be no ball; **1200** would freeze the decarburised metal hard instead of leaving it pasty, and there
> would be no ball either. The verb only exists strictly between the two.
> Note: Reachability at 1400 is U6.5's to demonstrate through `NaturalDraughtFor` — that is what the chimney
> rework was for. If ~1400 needs more courses than a player will build, the **losses** move, not this number.

> ## Four more forks ruled 2026-08-05 — and four that turned out not to be forks
>
> Note: checking all eight candidates against `docs/design/` before offering options answered half of
> them outright. Search the design docs before deciding — that is the standing rule.
>
> **5. The mill's 100 u product is vanilla's rod — `iwex:rolledrod` is never created.** *(U7.3 Step 4b.)*
> Split by **role**: the shear's claimed product is `game:rod-iron`; the re-rollable piece is the auto-emitted
> `iwex:stock-rod`. The precedent is one row above it in the same table — `rolled-parts.md:248-249` settles
> the sibling product toward vanilla, *"which is what makes the two routes comparable"* — and vanilla's rod is
> `2 × 2 × 10 = 100 u` **exactly**. Rolled bar and forged bar were one commodity: the mill sells **labour**,
> not a new material. U7.4 drops to **three** itemtypes.
>
> **6. The blast-furnace yield ships per band — `BfIronPerBurdenBand = 6.375` raw / `6.9` roasted.** *(U3.3
> Step 4.)* **`8.5 u/nugget` and `6.375 u/band` are the same statement**, related by `IronFrac = 0.75` —
> never two competing numbers. Verified: shipped `60 ÷ 16 ÷ 0.75 = 5.000` exactly (bloomery parity confirmed),
> `8.5 × 0.75 × 16 = 102` exactly. Per-band is the reading under which the ruling's own consequence holds —
> the ~3670 u full shaft that bought **bed rotation** is `36 × 16 × 6.375 = 3672` — and it needs no ore-burden
> band content, so it lands without waiting on U2.2c.
>
> **7. The pig re-mass 150 → 375 gets its own task, U2.0, and `PigVoxels` moves 60 → 150 with it.** That keeps
> `UnitsPerVoxel` at **2.5**, so one voxel means the same thing in the bed, the crucible band, the cupola band
> and on the anvil. The "U1.7" that earlier findings kept citing never existed in any plan.
>
> **8. U6.11 Step 5 owns the whole reverberatory chassis** — puddling 4 + firebox + `heatingcore` +
> `heatinghearth` + `chargedoor`. Not scheduling: grid-pattern collision is a property of
> `FurnaceRecipeDefinitions.cs` **as a whole**, and *the loser silently never resolves*. Two units authoring
> the same chassis means the second finds the clash after the first blessed its golden. U9.4 Step 3's
> "the one shipped block with no recipe" was **false** — `chargedoor` had none either.
>
> ### And four that the docs had already settled — closed, not asked
>
> | | Already written down | What was actually left |
> |---|---|---|
> | **Crop table** | U7.5 owns it; section **is** in the key | Bookkeeping: U8.3 shrinks to `ShearFeed`, U8.4 re-points, U7.5 adopts the hundredths key |
> | **smex hot furnace** | no — deferred to the remake | The 4 consequences nobody had listed → U4.5 Step 6, plus a regression test |
> | **Breach** | Splice-and-drop, settled 2026-08-02 | **New U3.5b**, and it must precede U3.6's `Extinguish()` rename |
> | **Cast stock** | 5 hearth forms; the cast 3 are smex's | A stale rename → new U7.6 Step 0; `billet` is **not** an iwex `StockForm` |
>
> Caution: never write `6.375` into a per-material **`UnitsPerBand`** table. That column is what a band
> **weighs**; 6.375 is what a band **yields** — two quantities **~31× apart**. U2.2c's mixed-density test
> draws its expected values from that table, so the error would be baked into the very test meant to catch
> it. Both places that once carried it (plan U2.2c Step 2 and `layered-charge.md`) are corrected.
---
## Amendment 2026-08-04 — the two failure modes, and a correction to "lit lives nowhere"

Added 2026-08-04, after the U2 and U3 maps below were written, so they do not account for it.

**The spec.** A furnace **breached** while lit (walls broken, structure compromised) does **not** extinguish:
its charge keeps burning at natural-draught temperature — `blastSupplyFrac = 0`, which the shipped heat
balance already settles at **~970 °C**, below iron's 1500 — until the coke runs out. Breaking the piles
returns **burden and remaining coke**. A furnace that loses air through the **tuyeres** does the opposite:
sealed and starved, it chokes and extinguishes quickly. See
[`layered-charge.md` § *Two failure modes, and they are opposites*](../../design/layered-charge.md).

**The correction.** "Lit is stored nowhere" was too strong.
`layered-charge.md` has always said **lit is furnace-level**. What dissolves is the *timer-driven FSM* —
`Firing` / `Melting` become derived reads and the `Bf*` cadence keys become emergent. The lit bit stays.

It also **retires the relight trap** the U2 map flags as its first trap. The worry was that with no stored
flag, only "burn-out left no fuel" would stop a furnace re-firing the tick after it died. It cannot: the
ignition gate is `State == Idle && StructureComplete && !IsChoked`, and whatever ended the campaign — a hole
in the wall, or no air — is *still true*. Both failure modes get their correct ending from the gate that
already exists.

### Three further rulings, same day

- **`castwheelsection` is iwex's; `castshell` is lpex's** *(the ownership split, settled 2026-08-04)*. Each part lives with the
  mod that **spends** it: the wheel section feeds iwex's flywheel, the shell is for lpex's water tank, ore
  crusher and engine housings. iwex had no use for a shell at all. `castframe` stays lpex's.
  **U1.5 shipped 2026-08-04** — both items, both patterns (and so both diagrams and both carve recipes,
  which derive), runtime shapes exported, real drawn diagram textures wired, lang in all three languages,
  goldens re-blessed by path in both suites.
  - **lpex got its casting bootstrap, and the cross-mod seam works.** `PatternItemDefinitions.Itemtype`
    and `DiagramItemDefinitions.Itemtype` are now public factories: **iwex owns the pattern system, each mod
    contributes entries.** lpex ships `Items/{CastPart,Pattern,Diagram}ItemDefinitions.cs` +
    `Recipes/Grid/PatternRecipeDefinitions.cs`, and the iwex casting cell reads the mold spec off whichever
    pattern is held, so **neither mod names the other's item codes**. This is the first exercise of the
    indirection the whole spec-on-the-pattern design exists for. It also means lpex's remaining four parts
    (`cylinder`, `axle`, both gear blanks) are now one `Molds` row apiece — `patterns.md` Open 3.
  - **Both are 600 u, not the 540 the density rule computes.** The rule sizes art plausibly; the mass is
    declared so the economy divides. N3 makes cast and fabricated **alternatives, not tiers**, and
    `bending.md` already pins both fabricated halves at 600 u — at 540 the cast route is quietly 10%
    cheaper and the choice stops being one. Reasoning lives in `CastPartItemDefinitions.CastShellUnits`.
  - **New invariant, and it is the general one**: a pattern's `capacity` equals its lane count times the
    cast item's `materialUnits` (`CastMassParityTests`). Nothing connected those two numbers before, and
    drift mints or eats metal with no error, no log and no visibly wrong cast. It holds for all seven
    item-output patterns today.
  - **Both flywheel grids are diagram-led and flat.** Per `docs/design/mechanics/diagram-crafting.md` a
    diagram-crafted recipe is **the plan plus a flat bill of materials** — one cell per distinct ingredient
    carrying its quantity — and explicitly *not* a picture of the product drawn in the grid. An earlier
    version spread the four rim sections across the corners of a 3×3 to look like a wheel; it reads nicely
    and is the wrong idiom, because the grid then encodes the assembly the diagram is responsible for.
    `CastMassParityTests` asserts the diagram and the one-cell-per-ingredient rule together.
  - **Two flywheel diagrams, not one** — `mpenergy-flywheel` and `mpenergy-flywheellarge`. The art says
    so, and it is right: a 5×5×2 wheel is a different drawing even though building it is an upgrade.
  - **48 of the 68 drawn diagram sheets are for units not yet built** — every furnace, the burdenmaker,
    the three mp benches, the crucible hearth, the charge doors, the finished-item family. **Check
    `assets/editable/textures/diagrams/` before planning any diagram work.** Every *declared* type has its
    shipped texture (verified: 0 missing, 0 orphaned), so nothing is broken today.
  - **The editable diagram names have drifted from the shipped type names** — `bfc` ← `furnace-coldblast`,
    `cf` ← `furnace-cupola`, `item-heavyplate` ← `item-castheavyplate`, `item-molddoubleingot` ←
    `item-castingotmold`, `item-castbillet` ← `item-billets`, `item-castbloom` ← `item-blooms`,
    `item-castwheelsection` ← `item-castflywheelsegment`; `item-moldplate` and `molten-barrel` have no
    editable source under any name. Same rename-in-flight as the shapes. A reconciliation, not a defect.
  - The wheel section's *filling shape* is still named `cell-filling-flywheelpart.json`; only the pattern
    type was renamed. Art-side rename, nobody's task yet.
- **`OreProcessing/` holds the burdenmaker alone.** The ore mixer and ore bunker predated the
  burden/furnace rework and were removed (U5.7). An **ore crusher** joins the folder later, with
  **lpex** — not iwex's to plan.
  - The removal was promoted 2026-08-06 (user-reaffirmed) because the mixer's fuel input had become a
    live defect: once carbon comes from fuel bands and nowhere else, a burden's stamped `Mix.Fuel`
    contributed nothing to any furnace, so coke or charcoal put into the mixer was consumed to stamp a
    number nothing read — player fuel destroyed for zero effect. No test saw it: every mixer case
    asserted the stamp arithmetic, never what a furnace did with the stamp. Fuel is charged directly at
    the furnace's hopper, as its own bands — `layered-charge.md` § *The burdenmaker*.
  - **The yield recalibration U5 looked like it needed is not there.** Taking fuel out of the burden makes
    each unit richer in ore but produces fewer units, and `BfIronPerOreUnit` is stated *per unit of ore
    content*, so the two cancel exactly: `16 × 8.5 × 0.75 = 102` and `13 × 8.5 × 0.923 = 102`. That is the
    per-ore-unit expression paying for itself — do not "fix" the ~25 % `IronFrac` move.
- **Open: which systems should become behaviours rather than base classes.** `BlockNetworkNode` is an
  abstract base class with 8 subclasses, and C# gives a block one inheritance slot. The concrete cost is
  already visible — `BlockFlywheel : BlockNetworkNode, IExBlockDefProvider, **IFillerHost**` hand-implements
  the filler interface because it cannot also extend `BlockFilledMegastructure`; `BlockRollingMill` does the
  same; and **zero** megastructures derive from `BlockNetworkNode` because none can. `INetworkConnector`
  (9 implementors, all machines) is the escape hatch for exactly this. Extracting network *orientation* into
  `ExOrientable` is step one (U10); whether network *membership* itself should follow is the larger
question. Check first whether `BlockNetworkNode`'s graph walk depends on `Block` identity in ways a
  behaviour cannot reach — that determines feasibility, and it has not been checked.

**Two code consequences for U2/U3:**

- `BlockEntityFurnaceCore.OnStructureLost` calls `Extinguish()` today. That is the wrong response to a
  breach; it must force `blastSupplyFrac` to zero and keep ticking. The choke path is already right.
- `BlockChargePile` carries `NoDrops`, documented as *"a pile that dropped items would duplicate the charge"*
  — true only while the furnace is authoritative. It must debit the column via the mid-span removal
  § *No unmanaged firing* already calls for, and drop what it removed.

---

## Execution order *(verified, and it differs from the parent plan)*

Position (2026-08-21): Phase 0, U2, U3, U4, U5, U6, U7 and U8 are all done, and **U9.1-U9.4 are done - the coke oven is finished and craftable**. Next is **U9.5-U9.11 (crucible steel)**, then U10. ⛔ U7 carries one blocked residue — U7.10, the roll sets, which the machining-line ruling re-homed on the unbuilt lathe.
The numbered ordering below is kept for the remaining units.
1. Phase 0 — quick wins + baseline, no core edits, run before anything else: several are prerequisites of U2/U3 that are far cheaper to land now than inside a serialised unit (details in § *Phase 0 — quick wins*). **U2.0 (the pig re-mass) belongs here** — it is not a core edit, and **U2.2c reads `ItemPig.PigUnits`**, so running 2.2c first ships 150 while every doc quotes 375.
1b. **U2.0 → U2.2c**, in that order, before the U2.3 cutover. Both were minted from MISSING_TASK findings on 2026-08-05; neither existed when the order below was written.
2. U1 remainder (U1.4 long cell block, U1.5 cast-part items, U1.6 acceptance — *the pig re-mass is **U2.0**, ruled and scoped 2026-08-05*) ∥ U2 (the cutover) ∥ U5.1–U5.7 (burdenmaker + mixer/bunker deletion) — verified disjoint in source: U1 touches Casting/, U2 touches Furnaces/, U5.1–U5.7 touches OreProcessing/ (4 files, none shared with any furnace unit). Their only contention is IiexConfig.cs, the three lang files, IiexBlocks.g.cs and goldens; see collisions for the discipline. If capacity is one thread, run U2 first — it is the critical path.
3. U3 (counter-current) — strictly serial on BlockEntityFurnaceCore.cs. Must open by splitting the firebox branch's cadence off IiexValues.Bf* (BlockEntityFireboxFurnace.cs:197-199) and deciding the cupola's parallel Cupola* triplet (IiexConfig.cs:339/:342/:346) before deleting anything, or four-to-six machines go permanently Idle with a green suite. **And U3.5b (breach) must land before U3.6**, which renames `Extinguish()` → `Shutdown()`; after that rename the design doc's instruction names no symbol that exists, and the wrong breach behaviour gets baked into the derived-state path with a fully green suite — **no test in any suite asserts what a breached lit furnace does.**
4. U4 (hearth / taps / plug / hearthmetal) — serial on the core. U4.3's BEBehaviorMoltenCell change requires U1 fully landed, because the fixed mc_* tree keys (BEBehaviorMoltenCell.cs:348-355) are shared with all five sand-casting consumers.
5. U5.8 + U5.9 (burden identity, cupola direct charge) — small, serial on the core; the only part of U5 that genuinely cannot run in phase 1.
6. U6 (puddling) — serial on the core. **Delivers U6.5 `NaturalDraughtFor` and U6.4's per-machine loss virtuals — those two, together, are what gate U9.** U6.3 produces neither (it produces a `DisruptionMixFloor` override), so stopping after U6.3 believing U9 is unblocked ships a crucible furnace that lights and never melts. Its DisruptionMixFloor fix becomes a firebox-branch override that U3 has already made survivable.
7. U10.4–U10.6 (Connector mark, layout migration, builder refusal) — must follow both U3 (tuyere y=1→y=2) and U4.5 (drop Role('h',Chargeable)), because all three rewrite the same legend blocks in BlockBlastFurnaceCoreCold.cs / BlockCupolaFurnaceCore.cs / BlockBlastFurnaceCoreHot.cs. Slot here, not earlier.
8. U7 (reheat + rolling) — next on the critical path because U8 depends on it. U7.1 edits BlockEntityHeatingFurnace and reads the core's melt cadence, so it must not overlap U6.
9. U8 (fasteners + shop floor) ∥ U9 (coke oven + crucible steel) — the one other defensible parallelisation: U8 lives in Forming/, U9 in Furnaces/ and only reads BlockEntityFurnaceCore. Both need their blockers landed (U9 ← U6.4 + U6.5; U8 ← U7.2 + U7.4 + U7.5). Contention is limited to IiexConfig, lang, IiexRecipeConfig, convert-shape.py, goldens, test-floors.txt. If risk-averse, run U9 first (it is a leaf, nothing depends on it) then U8.
10. U10.7 (delete the pending markers, correct the over-claims the pins left behind) — last, once the pins are actually gone.
11. U11 (the ladle) — **a leaf, and the only unit here that is genuinely unordered.** It touches no furnace file, no `IiexConfig` key and no shared fixture; its contention is `IiexRecipeConfig`, the three lang files, `IiexBlocks.g.cs` and goldens — the ordinary append-only set. So it can run in any window with spare capacity, including phase 1 alongside U1/U2/U5.1–U5.7. Its one prerequisite is **its own U11.1** (the exlib filler collision-box gap), which no other unit provides and which nothing else needs. It reads more cleanly after U4.3 only if the vessel ends up wanting a second molten cell; U11's scope does not force that.

---

## Biggest risks
- the critical path is seven serialised units on one 1951-line file. U2 → U3 → U4 → U5.8 → U6 → U7 → U8, with U9 and U10.4–U10.6 hanging off it, all funnelled through BlockEntityFurnaceCore.cs. There is no way to shorten it, so the only real mitigations are (a) the phase-0 quick wins, (b) genuinely running U1-remainder + U5.1–U5.7 + U10.1–U10.3 in the phase-1 gap, and (c) refusing to branch two furnace units — a merge in that file cannot be resolved by inspection and no golden would catch a bad resolution.
- deletions that fail soft are this plan's signature failure mode, and at least six are verified instances of it: BlockChargePile.PileCode resolving to null so SetBlock is skipped; smex's BlockHopperBell resolving iwex:blastmix via world.GetItem(...) behind a null guard so the drop silently empties; unknown SelectiveElements names dropped without an exception; unknown animation names dropped by the animator; IiexLangCoverageTests covering block codes only; IiexCostSelectorTests checking only for overlapping selectors. In each case the build succeeds, the suite is green and the game is broken. U2.10, U5.7, U7.3 and U8.7 each need a positive existence assertion, not the absence of a failure.
- golden and codegen laundering. EXLIB_WRITE_GOLDENS=1 re-blesses a whole domain and EXLIB_WRITE_BLOCKCODES=1 rewrites IiexBlocks.g.cs (1827 lines) wholesale. The working tree is already large and uncommitted (git status shows ~41 D/?? rows from the editable-shape rename still in flight), so one domain-wide bless during the phase-1 parallel window would absorb another lane's pending def changes into the record with nothing red anywhere. Path-filtered blessing is not a nicety here — it is the only guard.
- the test-floor file stops being evidence if floors move in bulk. iwex is at 1179; U5.7 deletes 39 methods and U2 deletes two reflection-based groups (~18 smex cases). The file exists because a suite that fails to load exits 0 with no summary — if floors are relaxed at the end of a phase rather than per unit, that protection is gone exactly when the tree is most disturbed.
- Cross-mod blast radius is systematically under-modelled. Verified: 49 FSM-reflection sites in 9 files across three assemblies; 40 Nails() call sites across four mods; one hpex-only released-code guard; FurnaceLayoutRig shared by three suites. "The iwex suite is green" is never sufficient evidence for U3, U4, U5.8 or U8.7 — and `./scripts/exmod.sh test 1.21` (never `latest`/1.22, whose ~20 IPlayer failures are upstream VS 1.22.6 noise) is the only meaningful gate for those units.
- the art/export pipeline is a shared single point of failure that no C# test sees. scripts/tools/convert-shape.py's clip-flag rewrite is the only thing keeping a `cycle` clip from vanishing an RCC-suppressed mesh, and it runs only for exports that go through the script — a hand-copied shape passes every test in the repo and is broken only in game. Five units (U5.1, U6.9, U7.4, U8.1, U9.5) export art. Mandate the script, and add the one shape-JSON assertion on emitted onAnimationEnd that currently does not exist anywhere.

---

## Phase 0 — quick wins
Small, verified, low-collision. Several are **prerequisites** of U2/U3 that are far cheaper to land now
than inside a serialised unit.
- QW1 — Done 2026-08-05: `BlockChargePile.PileCode` reads `new("iwex", "furnace-chargepile")` (`BlockChargePile.cs:66`), matching the code the def renders, pinned by a test asserting equality against `IiexBlocks.ChargePile.Any`.
- QW2 — Done 2026-08-04, in a new file `FireboxTickTests.cs` rather than in `FireboxChargeTests.cs` (which is about the hooks, not the tick). **A reverberatory hearth can be completed in the harness** — `StructureRig.Raise` stands in for every empty cell, so the in-game unreachability (U6.1) does not bound what a test can stage; the old "the only way to reach them at all" note in `FireboxChargeTests` was wrong and has been corrected. `A_loaded_hearth_lights_itself_on_its_own_production_tick` is now what stands between U3 and a permanently-Idle pair of hearths, with an empty and a one-unit-short control either side of it. **The fuel clock could not be pinned, and the reason is B8's fifth cause**: a full firebox holds cells × 12 units against the shaft's inherited `DisruptionMixFloor` of **144**, so a lit hearth is simultaneously full enough to light and too empty to stay lit — it snuffs on the disruption grace long before `MaxFuelBurnTime`. That is now asserted as arithmetic *and* as behaviour, and **U6.3 will make this test fail**, which is the notice it should give. Its failure message says what to rewrite it into.
- QW3 — Done: `HeatBalanceTests.cs:83` is `private const int FullHearth = 320;` — a literal, so the six calibration rows are real pins rather than tautologies. Do not re-point it at a config value (see U3.7 Step 7); preserve the `[Collection]` attribute or the scenario suites start racing config mutations.
- QW4 — Run `./scripts/exmod.sh test 1.21` once and record the five per-suite `Total:` numbers as a dated comment line in scripts/test-floors.txt's header. Every unit that later lowers a floor then has an anchor to justify against, and the file's own anti-absorption rule becomes enforceable rather than aspirational.
- QW5 — Done: `convert-shape.py:93-100` HOLD_CLIPS contains `"open"`, so U5.1's burdenmaker gate, U6.9's puddling door and U9.3's coke-oven lid export without shipping a held pose that animates itself shut.
- QW6 — U4.2 in isolation: fix the furnace tap's pick/drop normalisation, which names a code no definition produces. Self-contained inside BlockFurnaceTap.cs; touches no furnace core file, no layout and no shared fixture, so it can land safely during the U2 window.
- QW7 — U10.1 + U10.2 + U10.3 as a standalone change. Verified safe to start today: BlockNetworkNode.cs has no ExOrientable reference so no other unit has the file open, and only 8 classes derive from it (BlockCastIronBevel, BlockCastIronShaft, BlockFlywheel, BlockMoltenCanal, BlockPipe, BlockRollingMill, BlockRollingMillAxle, BlockFluidIntake). Its goldens live under pipes/molten/energy/forming — disjoint from every furnace golden. The largest of these wins, but the best-isolated. Stop at U10.3: U10.4+ needs the layout move.
- QW8 — Finish U1 (U1.4 long cell block, U1.5 cast-part items, U1.6 acceptance). U1.1–U1.3 are already done. Disjoint from every furnace file; the only shared surfaces are IiexBlocks.g.cs and iwex goldens, both handled by regenerating last. The pig re-mass is its own task, U2.0 — do not fold it in as a one-line edit.
- QW9 — Done 2026-08-04 in `IiexCodesHearthCellTests`, widened to both pairs: the rig hand-copies both `HearthGlyph` (from `IiexCodes.HearthCell`) and `ShaftGlyph` (from `IiexCodes.ChargeShaft`), and neither duplication fails to compile when the constant changes — the equality assertions are what stop a rename leaving every furnace layout test passing against the old string.
- QW10 — Done: `IiexCostSelectorTests.cs:39` `Every_block_a_grid_recipe_outputs_has_a_cost_catalogue_row` enforces the cost catalogue, so every "the catalogue is unenforced / 12-15 rows" claim downstream (U5.6 Step 5, U6, U8.8, U9.4 Step 4) is stale — the gate clause is real now.
- QW11 — Done: the parent plan's superseded sequence-table claims are gone with its U2–U10 bodies, and the "navigate by symbol" caution sits at the top of this document.

---

## File collisions
| File | Units | Advice |
|---|---|---|
| `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs` | U2, U3, U4, U5.8, U6, U7.1, U9 (read-only) | 2807 lines (as of 2026-08-07, after U2–U4 landed in it) and the highest-risk file in the repo. Verified edit sites, by symbol not by the plan's line numbers: CollectCharge :1352, EnumerateChargePiles :1416, CollectChargePiles :1425 (coalpile sniff :1438), ExtinguishResidue :1453, SolidifyBottomLayer :1466 (second coalpile sniff :1487 — the plan names neither), BurnOutCharge :1544, ComputeHeatBalance :1211, natural draught :1229, BfChargeLossFull :1243, DisruptionMixFloor :224, ExtinguishThresholdDefault/Severe :242/:245, MaxAwayCatchupSteps :66, chargeHandle :927. Never run two of these units concurrently and never on parallel branches — a merge here cannot be resolved by inspection and no golden would catch a bad resolution. U9 must only read. |
| `src/IronIndustryExpanded/IiexConfig.cs` | U2, U3, U4, U5, U6, U7, U8, U9 | 697 lines, 20 #regions. The `Migrations` array at `:27` is seeded (verified 2026-08-07: it carries the 0.3.0 currency-reset entries, including the five `Bf*` → `Firebox*` cadence renames). U2 (HopperTallPileCap, BlastmixBurnTime), U3 (:106 BlastMixRequiredToFire, :237 BfHeatRatePerSecond, :240 BfCoolRatePerSecond, :280 BfMaxFuelBurnTime, :283 BfMeltStartDelay, :286 BfMeltIntervalSec), U4 (:333/:336 cupola pools) and U5 (:409/:476 ore regions, :636 BurdenProfiles) all deleted keys there; every later unit appends at the end only, never re-sorts. Additive units (U6 draught, U7 :576 rolling, U8 benches, U9) should add a new #region at the file tail rather than edit inside an existing one. |
| `src/IronIndustryExpanded/Generated/IiexBlocks.g.cs` | U1.4, U2.11, U4.1, U5.2, U5.7, U6.1, U8.4, U8.5, U8.6, U9.1, U9.6, U9.9 | 1827 lines, generated by running the definitions. Merge-hostile: two units adding blocks in parallel both rewrite it wholesale. Rule: never hand-edit, never resolve a conflict in it — take one side, then re-run EXLIB_WRITE_BLOCKCODES=1 as the last step of the unit and again after any merge. In the phase-1 parallel window (U1.4 adds the long cell, U5.2 adds the burdenmaker, U5.7 removes two) regenerate once, after both lanes land, by whichever finishes second. |
| `assets/iiex/lang/en.json + ru.json + uk.json` | U2.9, U2.10, U3.6, U4, U5.7, U6.2, U6.5, U6.9, U6.10, U6.11, U7.3, U7.4, U7.9, U8, U9 | en.json is 437 lines and every unit appends. Append-only, never reorder, one contiguous block per unit so a conflict is a clean both-sides-keep. The guard is weaker than it reads: IiexLangCoverageTests uses LangCoverage.MissingNames over block codes only — item rows (U7.4's four, U9's pot/blister) and ingameerror rows (U7.9, U2.9) are entirely unguarded, and a lost row just renders the raw key in game. Add a by-hand three-locale diff to every unit's gate. |
| `test/IronIndustryExpanded.Tests/Fixtures/FurnaceLayoutRig.cs` | U2, U3, U4.1, U4.5, U5, U6, U9, U10.5 | 966 lines, shared oracle for the iwex, lpex and smex furnace suites — one constant change breaks three assemblies. `HearthGlyph` at `:111-112` spells `"*:@(air\|coalpile\|furnace-chargepile\|hearthmetal-.*)"` (verified 2026-08-07; U4.1 landed the rename atomically with `IiexCodes.HearthCell`). The glyph is a hand-written duplicate of the constant and does not fail to compile when the constant changes — QW9's equality assertion is the guard; any future edit to either side must stay atomic. |
| `test/IronIndustryExpanded.Tests/Fixtures/ColdBlastFurnaceScenes.cs · test/IronIndustryExpanded.Tests/Fixtures/CupolaScenes.cs · test/SteelIndustryExpanded.Tests/Fixtures/BlastFurnaceScenes.cs` | U2.6, U3.8, U4, U5.9, U10 | 679 / 374 / 405 lines. Verified 49 reflection sites across 9 test files write State / _secondsAboveMelting / _meltSeconds / _fuelBurnSeconds (CupolaScenes, CupolaScenarioTests, BlastFurnaceScenes, BlastFurnaceTests, BlastFurnaceScenarioTests, SteelPlantScenes, BessemerScenarioTests, BoilerRig, BoilerTickTests). U2.6 rewrites the charge helpers; U3.8 must delete the FSM setters in the same change that kills FurnaceState's setter — if the setter survives so those sites keep compiling, ~27 cases arrange a state the next derived tick overwrites and go green asserting nothing. Cross-mod: none of it is testable from the iwex suite. |
| `src/IronIndustryExpanded/IiexCodes.cs` | U2 (reads ChargeShaft :68), U4.1 (rewrites HearthCell :98) | 102 lines with exactly two public constants, so any two units editing it conflict. U2 only reads ChargeShaft — a non-collision provided U2 does not reformat the file. U4.1's HearthCell rewrite must be atomic with FurnaceLayoutRig.cs:106-107. |
| `src/ExpandedLib/Blocks/Structures/BEBehaviorMoltenCell.cs` | U1 (sand casting route), U4.3 (two molten cells on one BE) | Verified: serialization keys are fixed and not per-instance — mc_amount / mc_type / mc_temp / mc_solid / mc_patcap at :348-355 and :364-368. Two instances on one BE silently overwrite each other and GetBehavior<T>() returns only the first. Six live consumers: BlockEntitySandCastingBed, BlockEntitySandCastingCell, BlockSandCastingBed/Cell/LongCell and BEBehaviorFirebox. U4.3's per-instance prefixing must keep `mc_` as the default or every existing sand-casting-bed save loses its metal. Land U1 completely before U4.3 opens this file. |
| `scripts/test-floors.txt` | U2.6, U2.8, U2.10, U3.8, U5.7, U6, U8, U9, U10.5 | Five rows; iwex currently 1179. Last writer wins silently and the file's own header warns against absorbing deletions. U5.7 alone deletes 39 mixer/bunker methods; U2 deletes two reflection-based groups (ShaftColumnsTests.Piles invoking CollectChargePiles, and BlastFurnaceLifecycleTests' GetBlastMixCount, ~18 smex cases). Rule: each unit reads its own `Total:` from `./scripts/exmod.sh test 1.21` and edits only its own row, in the same change as the deletion. Never batch floor moves at the end of a phase. |
| `src/IronIndustryExpanded/BlockStructures/Furnaces/ChargeColumn.cs` | U2.5, U3.1, U3.5 | 520 lines. U2.5 needs an in-place segment rewrite for burn-out, U3.1 adds RiseGasThrough/Coalesce, U3.5 adds TakeSpan — all three mutate segment spans. Agree the mutation shape once, in U2.1, and write it down; each addition in its own #region so diffs do not interleave. Note ChargeColumn.cs:337's doc-comment already states "Phase 3 places and removes iwex:furnace-chargepile" — U2.2's contract is already written down there. |
| `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs` | U3.7, U6.3, U6.4, U7.1, U9.2, U9.10 | 245 lines. The cadence triplet reads `IiexValues.FireboxMaxFuelBurnTime` / `FireboxMeltStartDelay` / `FireboxMeltIntervalSec` at `:191-193` — the shaft's `Bf*` keys were renamed onto the firebox branch rather than deleted, with the rename and its config reset documented at `IiexConfig.cs:59`. Both reverberatory hearths, the reheat furnace and both U9 machines inherit them. The branch's MinChargeToIgnite is `sealed` (FireboxCellCount * FireboxMixPerCell) precisely to stop leaves reintroducing constants; any replacement must be sealed the same way. |
| `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockBlastFurnaceCoreCold.cs · BlockCupolaFurnaceCore.cs · src/SteelIndustryExpanded/BlockStructures/HotBlastFurnace/Blocks/BlockBlastFurnaceCoreHot.cs` | U3 (tuyere y=1→y=2), U4.5, U5.3, U10.5 | Verified role marks: cold .Role('c',Chargeable):114 + .Role('h',Chargeable):120 + .Role('h',Pool):121; cupola :87/:90/:91; hot .Role('c',Chargeable):103 and .Role('p',Chargeable):105 — the smex 'p' glyph is a Chargeable that U4.5's write-up never mentions. All four units rewrite the same legend blocks and each rewrite re-blesses goldens/{iwex,smex}/blocktypes/furnace/*.json. One legend rewrite per file per unit, in the order U3 → U4.5 → U10.5, never concurrently — the golden would otherwise be blessed twice from two different intents. |
| `src/IronIndustryExpanded/BlockStructures/Forming/{WorkPiece,StockForm,StockItemDefinitions,RollSetSpec,RollSetItemDefinitions}.cs` | U6.11, U7.2, U7.3, U7.4, U7.5, U8.3 | Verified U8's hard blockers are all still true today: WorkPiece.cs:35 is still `record WorkPiece(StockForm Form, float[] Strips, bool[] Turned)` with no Thickness/Mass/Length; RollSetSpec.OutputAt:95 has zero callers in src/ (only its own declaration); StockItemDefinitions.cs:41 still emits the tenths key `(int)(form.BaseThickness * 10)`. U6.11 must change only the two mass numbers and must not do the bloom→bar rename. U8.3 cannot start before U7.2 and U7.5 land. |
| `src/ExpandedLib/Definitions/ExIngredients.cs + ConstructionStages.cs` | U8.7 (sole editor, but cross-mod blast radius) | Verified 40 Nails(...) call sites across four mods; ExIngredients.cs:36 names the ingredient `metal` and ConstructionStages.cs:130 stores storeWildCard:"metal", so which fastener resolves decides what later RCC stages and drops resolve to. No unit collides on the file, but a change breaks lpex/hpex/smex builds. Write the negative test first — lpex's boiler must refuse nails — because nothing in the suite asserts a negative ingredient match today. |
| `scripts/tools/convert-shape.py` | U5.1, U6.9, U7.4, U8.1, U9.5 | One TEXTURES dict (:38) and one HOLD_CLIPS set (:89). `open` is in HOLD_CLIPS (QW5; verified 2026-08-05: `:93-100`), so an exported gate/lid `open` clip keeps its authored ending instead of being rewritten to Repeat and sliding shut by itself — a defect the retired ore mixer once shipped. Later units append TEXTURES entries only, which merge cleanly. |
| `src/ExpandedLib/Blocks/Networks/BlockNetworkNode.cs` | U10.2 only — the claimed U4 collision does not exist | Verified false collision. The file contains no reference to ExOrientable, IsNetworkOriented or mode:"network" at all, and BlockFurnaceTap derives from plain Block with MultiblockStructure + ExOrientable, not from BlockNetworkNode. Only 8 classes derive from it: BlockCastIronBevel, BlockCastIronShaft, BlockFlywheel, BlockMoltenCanal, BlockPipe, BlockRollingMill, BlockRollingMillAxle, BlockFluidIntake. U10.1–U10.3 are therefore safe to run at any time, including concurrently with U2/U3/U4. Remove the stated dependency from the plan's sequence table. |
| `src/IronIndustryExpanded/IiexRecipeConfig.cs` | U5.6, U6.5, U6.11, U8.8, U8.10, U9.4, U9.11 | 78 lines, one Defaults() dictionary at :46 with ~15 rows. Append-only; conflicts are trivial but frequent. **The catalogue is enforced** *(QW10 landed; verified 2026-08-05 — `IiexCostSelectorTests.Every_block_a_grid_recipe_outputs_has_a_cost_catalogue_row`)*, so every unit's gate clause "the recipe exists and is in the catalogue" is now real rather than decorative, and a new craftable block without a row goes **red**. Add the row in the same edit as the recipe. |
| `test/IronIndustryExpanded.Tests/Blocks/Furnaces/HeatBalanceTests.cs` | U3.7, U6.4, U6.6, U9.10 | Shares FurnaceConfigCollection with the scenario suites (it mutates IiexValues), so any edit must preserve the [Collection] attribute or the scenario suites start racing config mutations. Verified trap at :70: `private static int FullHearth => IiexValues.BlastMixRequiredToFire` makes the charge-loss ratio mixCount/requiredMix 1.0 by construction, so the six literal T_process rows (:81-86 → 1420 / 1745.5 / 1577.5 / 1262.5 / 1588 / 970) survive any change to that key. Pin the denominator as literal 320 before U3 touches it (QW3). |
| `test/IronIndustryExpanded.Tests/Blocks/Furnaces/ShaftColumnsTests.cs + FurnaceRoleCellsTests.cs + FurnacePartsTests.cs` | U2.2, U2.3, U2.4, U2.5, U3, U4, U6, U9 | ShaftColumnsTests.cs:943-947 reflects into `CollectChargePiles` and stops compiling the moment U2 deletes it — deleting the test rather than porting it also removes the "a shaft with no box walks for no charge" guard. FurnaceRoleCellsTests:733-758 and FurnacePartsTests:33-51 are the two role/code oracles that both U6 and U9 must extend, and FurnacePartsTests:50's `checkedCodes >= 12` floor was written when iwex shipped four structures — raise it with each new structure or a halving of the codes examined still passes. |

---

# U2 — The cutover: the blast furnace and the cupola read ChargeColumns instead of vanilla coal piles

U2 moved the shaft branch — cold blast furnace, cupola, and smex's hot blast furnace by inheritance —
off `game:coalpile` block entities and onto the `ChargeColumn` set the furnace owns and persists, deleted
the vanilla-pile path, the Harmony side-table, `iwex:blastmix` and the `charge` material role, and landed
band-order charging (coke and burden as separate bands, ambient quantised to 5 °C) plus the hopper's
survival-budget readout. Executed in full; landed 2026-08-06. Record: docs/internal/worklog/2026-08.md and git
history.

### Tasks

#### U2.0 — The pig re-mass, 150 → 375 u — and the voxel follows it

Done 2026-08-05 (`ItemPig.PigUnits = 375`). Record: docs/internal/worklog/2026-08.md and git history.

#### U2.1 — Prerequisites: settle "lit", anchor the relight invariant, correct PileCode

Done 2026-08-05. Record: docs/internal/worklog/2026-08.md and git history.

#### U2.2b — The slag block becomes a placeable, layered slag pile

Done. Record: docs/internal/worklog/2026-08.md and git history.

#### U2.2 — Materialisation: the furnace places and removes iwex:furnace-chargepile to match column height

Done 2026-08-05. Record: docs/internal/worklog/2026-08.md and git history.

#### U2.2c — The charge scale: a band becomes 2 items, and the item-scale constants retire

Done. Record: docs/internal/worklog/2026-08.md and git history.

#### U3.0 — Move the cold blast furnace's tuyeres y=1 → y=2

Done. Record: docs/internal/worklog/2026-08.md and git history.

#### U2.3 — The shaft branch reads columns: CollectCharge, ReadChargeMix, TryIgniteCharge

Done. Record: docs/internal/worklog/2026-08.md and git history.

#### U2.4 — Consumption and descent: SmeltCycle takes from the bottom of each column

Done. Record: docs/internal/worklog/2026-08.md and git history.

#### U2.5 — Extinguish residue on columns: burn-out, the frozen pool, and the rejected-charge guard

Done. Record: docs/internal/worklog/2026-08.md and git history.

#### U2.6 — Rewrite the three shared fixtures onto columns — the seam that carries the whole suite

Done. Record: docs/internal/worklog/2026-08.md and git history.

#### U2.7 — Band-order charging: the tall hopper accepts coke, fills the lowest columns first, at a quantised temperature

Done. Record: docs/internal/worklog/2026-08.md and git history.

#### U2.8 — smex: the bell hopper drips into columns instead of seeding game:coalpile

Done. Record: docs/internal/worklog/2026-08.md and git history.

#### U2.9 — The hopper's survival-budget readout

Done. Record: docs/internal/worklog/2026-08.md and git history.

#### U2.10 — The deletions: the Harmony patch, iwex:blastmix, the charge role, and the last coalpile walk

Done. Record: docs/internal/worklog/2026-08.md and git history.

#### U2.11 — Regenerate the generated artefacts and true up the docs

Done. Record: docs/internal/worklog/2026-08.md and git history.

---

# U3 - The counter-current furnace: raceway combustion, carried segment temperature, the chill

U3 replaced the shaft furnace's flat-rate, timer-driven fire with the counter-current model: coke burns
only at the raceway, rising gas warms every segment above, burden melts if its carried temperature plus
the coke burning with it clears the melting enthalpy at unit granularity, and under-coked burden chills,
hangs and stiffens the blast demand. `FurnaceState` became a derived per-tick read, breach landed as its
own three-gate state, charcoal became a priced second fuel, and the shaft branch's timer and threshold
config keys were deleted. Executed in full; landed 2026-08-06. Record: docs/internal/worklog/2026-08.md and git
history.

### Tasks

#### U3.1 — ChargeColumn learns to be warmed: the counter-current operation, pure and world-free

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U3.2 (+U3.2b) — The raceway

Done 2026-08-06, landed with U3.3. Record: docs/internal/worklog/2026-08.md and git history.

#### U3.3 — The melt condition

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U3.4 — The chill

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U3.5 — Recoverability

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U3.5b — Breach

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U3.6 — FurnaceState derived

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U3.6c — Charcoal is a priced second fuel

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U3.7 — The config deletions

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U3.8 — The cross-mod repair: smex, lpex, the fixtures and the docs

Done 2026-08-06; the handbook and lang text for the retired rules was still owed at landing. Record: docs/internal/worklog/2026-08.md and git history.

---

# U4 — The hearth, the taps, the plug — and the `hearthmetal` item merge

U4 replaces the blast furnace / cupola's two `float` pools (`_moltenIron`, `_moltenSlag` on `BlockEntityShaftFurnace`) with real `BEBehaviorMoltenCell` cells living in the y=1 crucible blocks, and in the same pass merges `iwex:solidifiediron` + `iwex:solidifiedcastiron` into one variant-grouped `iwex:hearthmetal-{pigiron|castiron}` block that is that molten cell — molten while the furnace runs, frozen in place when it dies, chiselled back out with `MoltenChisel`. It then adopts the two already-drawn tap shapes (`assets/editable/shapes/furnace-block-{iron,slag}tap.json`), which carry the notch-height difference as geometry and a top-level `ClayPlug` element and — verified — carry **no `open` animation clip at all**, so adopting them necessarily replaces the tap's `ToggleAnimator` pour pose with a per-BE `SelectiveElements` prune, which is exactly the clay-plug mechanic. Finally it moves the no-canal gate from *cannot open* to *cannot pour* and adds the blow-in verb (open tap → torch → re-plug → blast on).

**Entry condition.** U3 complete: `FurnaceState` is a derived read recomputed each tick (not stored), and `BfMeltStartDelay` / `BfMeltIntervalSec` / `BfMaxFuelBurnTime` / `BfHeatRatePerSecond` / `BfCoolRatePerSecond` / both extinguish thresholds / the disruption floor are gone. U4 depends on this in three concrete places that a fresh engineer will hit on day one: (a) `BlockEntityShaftFurnace.AppendMoltenMetalInfo` (BlockEntityShaftFurnace.cs:473-483) and `AppendMoltenSlagInfo` (:485-495) both gate on `State != FurnaceState.Melting`; (b) `SmeltCycle`/`ConsumeForMelting` (:177-239) is the only writer into the pool today and its cadence is `MeltIntervalSec`; (c) `Extinguish()` → `ExtinguishResidue()` → `SolidifyBottomLayer()` (BlockEntityFurnaceCore.cs:1299-1310, 1453-1508) is the *only* place a solid pool block is ever placed — U4.4 makes that placement continuous, so the extinguish path must already be a derived-state consequence rather than an FSM transition. U4 does not need the tuyere relocation (landed as U3.0) — the tap/crucible half of the layout move is already applied on both iwex furnaces.

**Shared files** (collision risk): `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs — U2 (CollectCharge/EnumerateChargePiles seam), U3 (FSM dissolution), U6 (puddling); U4 removes the SolidProductBlock/DrainedMetalUnits/StampSolidProduct/ClearMoltenPools quartet at :1394-1410 and rewires ExtinguishResidue at :1453-1458`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityShaftFurnace.cs — U2 (ReadChargeMix/SmeltCycle onto columns), U3 (raceway + melt condition), U5 (AcceptedFamilies when the ore/remelt family model dies); U4 rewrites the whole 'Products: capacity, drain, residue' region (:329-443) and the serialization region (:445-464)`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockBlastFurnaceCoreCold.cs — U3 moves the tuyere glyphs to y=2; U4.5 deletes `.Role('h', CellRole.Chargeable)` at :120. Both re-bless goldens/iiex/blocktypes/furnace/blastcore.json`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockCupolaFurnaceCore.cs — U5.3 (cupola charges pig and scrap directly); U4.5 deletes `.Role('h', CellRole.Chargeable)` at :90`, `src/SteelIndustryExpanded/BlockStructures/HotBlastFurnace/Blocks/BlockBlastFurnaceCoreHot.cs — U2/U3 bring it along as a ShaftFurnace leaf; U4.6 either moves its `S` glyph from Layer 2 to Layer 1 or explicitly defers it`, `src/IronIndustryExpanded/IiexConfig.cs — U2 (BlastMixRequiredToFire, HopperTallPileCap), U3 (all the FSM timers), U5 (burden grade bands); U4 deletes :274, :277, :333, :336 and adds the hearth-band + plug-cost constants`, `src/IronIndustryExpanded/IiexCodes.cs — U2 touches `ChargeShaft`; U4.1 rewrites `HearthCell` at :98-99. Only two constants in the file — a merge here is near-certain`, `src/IronIndustryExpanded/Generated/IiexBlocks.g.cs — regenerated by every unit that adds or renames a block; U4.1 collapses two entries into one variant-grouped class. Never hand-edit; always `EXLIB_WRITE_BLOCKCODES=1``, `src/ExpandedLib/Blocks/Structures/BEBehaviorMoltenCell.cs — U1's casting route is its other live consumer; U4.3 changes its serialization keys. The default must stay `mc_` or every sand-casting-bed save breaks`, `test/IronIndustryExpanded.Tests/Fixtures/FurnaceLayoutRig.cs — U2, U3 and U5 all rewrite parts of it; U4 touches `HearthGlyph` (:106-107) and the Chargeable/Pool role-glyph assertions (:630-657)`, `test/IronIndustryExpanded.Tests/Fixtures/ColdBlastFurnaceScenes.cs — U2 rewrites ChargePile/Recharge/PileAtLocal onto columns; U4 rewrites the solid-block registration (:218-231) and the MoltenIron/MoltenSlag accessors (:611-613)`, `test/IronIndustryExpanded.Tests/Fixtures/CupolaScenes.cs — U2 and U5.3 both rewrite it; U4 touches :77 and :334-350`, `test/IronIndustryExpanded.Tests/Scenarios/ColdBlastFurnaceScenarioTests.cs — U2's gate is 'green with no edits to its assertions'; U4 must edit the extinguish case's block-code literal at :429 and the pool-reading assertions at :400-450`, `test/SteelIndustryExpanded.Tests/Blocks/HotBlastFurnace/BlastFurnaceLifecycleTests.cs and BlastFurnaceTests.cs — U2/U3 own most of it; U4 touches the `_moltenIron`/`_moltenSlag` reflection at :114-165, :341, :628 and :88-90, :148-149`, `assets/iiex/lang/{en,ru,uk}.json — every unit adds keys; U4 renames four block keys and adds the plug + blow-in lines. `LangParityTests` fails if ru/uk lag`

### Tasks

#### U4.1 — `hearthmetal-{pigiron|castiron}`: one block, one entity, one alternation entry, plus the migrations

Done 2026-08-07. Record: docs/internal/worklog/2026-08.md and git history.

#### U4.2 — Fix the tap's pick/drop normalisation, which names a code no definition produces

Done 2026-08-07. Record: docs/internal/worklog/2026-08.md and git history.

#### U4.3 — Crucible sizing + make two molten cells coexistable on one block entity

Done 2026-08-07. Record: docs/internal/worklog/2026-08.md and git history.

#### U4.4 — The hearth as live molten cells — delete `_moltenIron` / `_moltenSlag` and the extinguish-only spawn

⛔⛔ **Every line number below U4 is stale — locate by name, never by line** *(verified 2026-08-20)*. The
plan was written against a ~500-line `BlockEntityShaftFurnace.cs`; it is **1103 lines** now. Measured
drift: `_moltenIron`/`_moltenSlag` :41-42 -> **:29-30**; `_maxMolten*` :48-49 -> **:54-55**;
`MaxMoltenProduct`/`MaxMoltenSlagPool` :92/:95 -> **:175/:178**; the pool write in `ConsumeForMelting`
:204-239 -> **:462-463**; `DrainIronTap` :340-377 -> **:960-975**; `DrainSlagTap` :379-414 ->
**:995-1010**; `ClearMoltenPools` :437-441 -> **:1042**; the tree keys :453-461 -> **:1056-1063**; the HUD
appenders :473-495 -> **:1076-1090**; `IiexConfig` :274/:277/:333/:336 -> **:372/:375/:426/:429**.
★ Everything the task *consumes* was re-verified and all of it exists. ⛔ Prose still says `iwex:` in
places; the live codes are `iiex:`.

★★ **Step 1 landed 2026-08-20**: `HearthMetalCellTests` ships six failing cases (the plan's five plus
Step 8's standalone-cooling case). The cold blast furnace has **3 pool cells**. The headless world builds
block entities from `RegisterBlockEntityFactory`, not from a block def, so the test configures the two
cells the way the def's `behaviors` array will.


What U4.3 left for this task: `BEBehaviorMoltenCell` takes a `key` config prop (default `mc_`) that
prefixes its tree keys, with `MoltenCellHost.MoltenCell(key)` / `.MoltenCells()` as the accessors —
`GetBehavior<T>()` returns only the first instance and must not be used on a two-cell host. Capacity is
one shared volume: `IiexConfig.HearthUnitsPerBand` (640) and `HearthSlagSpoutBand` (10, the slag-spout
height in bands); the runtime capacity setter is `SetCapacity` over `_runtimeCapacity`, and its tree key
stays `patcap`.

**DONE 2026-08-20.** Record: docs/internal/worklog/2026-08.md. Three findings the steps did not
predict, all now in the worklog: a cell holds whole units so the melt's fractional yield needed a carry;
`LiquidCapacityReached` read `0 >= 0` as a full crucible on the two hearths; and `HearthSlagSpoutBand` is a
spout height, not a capacity.

**Files**
- Create: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/HearthMetalCellTests.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Products/Blocks/BlockHearthMetal.cs`, `src/IronIndustryExpanded/BlockStructures/Products/BlockEntities/BlockEntityHearthMetal.cs`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityShaftFurnace.cs:41-49,92-95,106-114,204-239,331-414,420-441,447-462,473-495`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityCupolaFurnace.cs:71-73`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:1394-1410,1453-1508`, `src/IronIndustryExpanded/IiexConfig.cs:274,277,333,336`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/hearthmetal.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/HearthMetalCellTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/BlastFurnaceTapTests.cs:212-346`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceHudDistributionTests.cs:150-280`, `test/IronIndustryExpanded.Tests/Fixtures/ColdBlastFurnaceScenes.cs:611-613`, `test/IronIndustryExpanded.Tests/Scenarios/ColdBlastFurnaceScenarioTests.cs:400-450`, `test/IronIndustryExpanded.Tests/Fixtures/CupolaScenes.cs:334-350`, `test/SteelIndustryExpanded.Tests/Blocks/HotBlastFurnace/BlastFurnaceLifecycleTests.cs:114-165,341,628`, `test/SteelIndustryExpanded.Tests/Blocks/HotBlastFurnace/BlastFurnaceTests.cs:88-150`

**Consumes:** BlockEntityFurnaceCore.PoolCells (:373, = CellsWithRole(CellRole.Pool)); .MetalTapPos (:1733); .SlagTapPos (:1737); protected float _internalTemp (:88); protected long _lastTapSoundMs (:101); BlockEntityFurnaceTap.IsPouring; .TryPourMetal(ItemStack, float); BEBehaviorMoltenCell.PushMetalRaw / .DrainMetal / .UpdateThermal / .EnsureMetalStack / .GetRecoveryDrop / .Solidified / .IsHardened / .CellAmount / .MaxUnitCapacity; MetalRegistry.MoltenItemOf(string); MoltenChisel.TryChisel(IWorldAccessor, IPlayer, BlockPos, IChiselableMolten, AssetLocation); IiexValues.TapDrainPerTick / .TapIronStackFactor / .TapSlagStackFactor

**Produces:** BlockEntityHearthMetal implements IChiselableMolten and hosts two BEBehaviorMoltenCell instances (iron + slag); BlockEntityShaftFurnace exposes `protected int PooledMetalUnits` / `PooledSlagUnits` computed by walking PoolCells; `_moltenIron`, `_moltenSlag`, `_maxMoltenIron`, `_maxMoltenSlag`, `MaxMoltenProduct`, `MaxMoltenSlagPool`, `ClearMoltenPools`, `DrainedMetalUnits`, `StampSolidProduct` and the `"moltenIron"`/`"moltenSlag"` tree keys are gone

- [x] **Step 1.** Write `HearthMetalCellTests` first, as five failing cases, none of which may force `StructureComplete`: (1) a furnace that reaches melting SetBlocks `iwex:hearthmetal-pigiron` into every `PoolCells` position and the cells hold metal; (2) a second melt cycle raises `CellAmount` on the same block rather than replacing it; (3) opening the iron tap drains it and the canal below receives; (4) cooling the cells below the melting point latches `Solidified` with the same `CellAmount` — nothing is lost at the freeze; (5) a chisel + hammer on a hardened hearth block returns bits equal to the frozen amount. Build the footprint with `StructureRig.Around(world, furnace, BlockBlastFurnaceCoreCold.Definitions("iwex").Single())` as `BlastFurnaceTapTests.cs:235-239` does.
- [x] **Step 2.** Add the two `BEBehaviorMoltenCell` declarations to the `hearthmetal` def: `.EntityBehavior("exlib.BEBehaviorMoltenCell", new JObject { ["key"] = "hm_iron_", ["solidifies"] = true })` and the same with `"hm_slag_"`. Capacity comes from config at runtime via the (renamed) capacity setter, not from the JSON, because `IiexConfig` is live-editable and a baked JSON number is not.
- [x] **Step 3.** Make `BlockEntityHearthMetal` implement `IChiselableMolten` following `BlockEntityMoltenCanal.cs:352-360` verbatim in shape: `HasChiselableContent => iron.Solidified || slag.Solidified`, `CanChiselOut => … && IsHardened`, `ChiselBlockedError => "iwex-hearthtoohot"`, `ChiselOut()` clearing and returning the recovery. Route the interaction through `MoltenChisel.TryChisel`. Add the lang key in en/ru/uk.
- [x] **Step 4.** In `BlockEntityShaftFurnace`, replace `_moltenIron`/`_moltenSlag` (:41-42) with reads over `PoolCells`. `ConsumeForMelting` (:204-239) stops doing `_moltenIron = Math.Min(_moltenIron + ironProduced, _maxMoltenIron)` and instead SetBlocks the hearth block where a pool cell is free (reuse the free-cell rule from `SolidifyBottomLayer`, BlockEntityFurnaceCore.cs:1477-1493 — empty, or a charge pile that is not holding rejected charge) and calls `PushMetalRaw(units, MetalRegistry.MoltenItemOf(MetalProductCode).ToString(), _internalTemp, Api.World)`. Overflow beyond `MaxUnitCapacity` is what `LiquidCapacityReached` now reports.
- [x] **Step 5.** Rewrite `DrainIronTap` (:340-377) and `DrainSlagTap` (:379-414) to draw from the cells rather than a float. Keep every arithmetic detail: `Math.Min(IiexValues.TapDrainPerTick, pooled)`, then `(int)Math.Ceiling(units * IiexValues.TapIronStackFactor)` / `TapSlagStackFactor`, then `tap.TryPourMetal(stack, _internalTemp)` and subtract only what was `accepted`. `BlastFurnaceTapTests.Retuning_the_tap_rate_moves_the_drain_with_it` (:212-272) and `The_slag_tap_drains_by_its_own_stack_factor_not_the_irons` (:287-346) pin the exact float-representation results 28 and 36 — those two numbers are the regression oracle for the whole rewrite; port the tests onto the cells, not the assertions.
- [x] **Step 6.** Delete `MaxMoltenProduct` (:92) / `MaxMoltenSlagPool` (:95), the `_maxMoltenIron` / `_maxMoltenSlag` cache (:48-49, :112-113), `ClearMoltenPools` (:437-441), the `"moltenIron"` / `"moltenSlag"` tree keys (:453-461), and the cupola's two overrides (BlockEntityCupolaFurnace.cs:71-73). Delete `IiexConfig.BfMaxMoltenIron` (:274), `BfMaxMoltenSlag` (:277), `CupolaMaxMoltenCastIron` (:333), `CupolaMaxMoltenSlag` (:336).
- [x] **Step 7.** On the core: `SolidifyBottomLayer` (BlockEntityFurnaceCore.cs:1466-1508) is now dead for the shaft branch — the pool is already in the world and freezes itself through `BEBehaviorMoltenCell.UpdateThermal` (:284-310). Remove it from `ExtinguishResidue` (:1453-1458) and delete the `SolidProductBlock` / `DrainedMetalUnits` / `StampSolidProduct` / `ClearMoltenPools` virtual quartet (:1394-1410) only after confirming no reverberatory hearth overrides them (grep: today only the shaft branch does). `BurnOutCharge()` must stay in `ExtinguishResidue` — it is the reverberatory hearths' path too.
- [x] **Step 8.** Drive `UpdateThermal` from somewhere: hosted molten cells are not auto-ticked (BEBehaviorMoltenCell.cs:18-25 — 'a hosted cell is not auto-registered in the shared molten network graph'). Give `BlockEntityHearthMetal` its own server tick calling `EnsureMetalStack` then `UpdateThermal`, and a client tick so the glow is not stale (see the `MoltenRenderer temperature staleness` precedent). Add a test that a hearth block with no furnace above it still cools and latches.
- [x] **Step 9.** Rewrite the two HUD appenders (:473-495) against the cell totals. They currently gate on `State != FurnaceState.Melting` — with U3's derived state that read is a per-tick recompute; keep the gate semantics (show while melting, or while a pool is still draining) but source both numbers from the cells and the cap from `MaxUnitCapacity` summed over `PoolCells`. `FurnaceHudDistributionTests.cs:150-280` sets `_moltenIron`/`_moltenSlag` by reflection and must be ported.
- [x] **Step 10.** Port the fixture accessors: `ColdBlastFurnaceScenes.MoltenIron`/`MoltenSlag` (:611-613) and `CupolaScenes` (:334-350) stop reflecting private floats and start summing the cells. Do this by rewriting the fixture, not the ~38 scenario assertions — the same lever U2 used.
- [x] **Step 11.** Port smex's hot furnace: `BlastFurnaceLifecycleTests.cs:114-117,151-164,341,628` and `BlastFurnaceTests.cs:88-90,148-149` reflect the same two fields and one asserts against the deleted `IiexValues.BfMaxMoltenIron`.
- [x] **Step 12.** Re-bless `goldens/iiex/blocktypes/hearthmetal.json` (the def gained two entity behaviours) with `EXLIB_WRITE_GOLDENS=test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/hearthmetal.json`.
- [x] **Step 13.** Run `./scripts/exmod.sh test 1.21` and `1.20`; append a WORKLOG entry.

#### U4.5 — The crucible becomes pool-only — drop `Chargeable` from the hearth glyph on both furnaces

> **DONE 2026-08-21.** All 7 steps. Gate 9/9, **4,136** per version (iiex 1946, siex 314, exlib 1876).
>
> **Line numbers were stale again** (as U4.4's were): `BlockBlastFurnaceCoreCold.cs:115-121` is really
> `:107-112`, `BlockCupolaFurnaceCore.cs:88-91` is `:78-82`, `CellRole.cs:110-116` is `:50-56`,
> `FurnaceLayoutRig.cs:630-658` is `:500-535` and `:657-669`. Locate by name.
>
> ⛔ **Step 5's command does not work as written.** `EXLIB_WRITE_GOLDENS` matches against the def's own
> `domain/path`, so the fragment is `iiex/blocktypes/furnace/blastcore` — a `test/.../goldens/` prefix
> matches nothing and the run passes having written nothing. Same defect in U4.4 Step 12 and U4.6 Step 7.
>
> **Three things the plan did not predict:**
> 1. **The glyph pair could not simply lose `HearthGlyph`** (Step 2's instruction). `AssertFurnaceGeometry`
>    is shared with the smex suite, whose furnace still charges its crucible, so the set had to become a
>    caller parameter — and `ChargeCells(layout)` with it. Hard-coding either would have made one of the
>    two suites vacuous rather than red.
> 2. **The shipped drawings stopped being ragged, which cost four tests their subject.** The uneven-floor
>    region of `ChargeMaterialisationTests`, `ChargePileTests.A_shipped_furnace_indexes_each_column_from_its_own_floor`
>    and the obstruction trio all read `(0,1,0)`. They now run on a shared `FurnaceLayoutRig.SteppedShaftDef`
>    (two columns, floors at y=1 and y=2). `ChargeMaterialisationTests.Both_shipped_shafts_are_uniform_floored`
>    states the premise so the fixture cannot be deleted as redundant later.
> 3. **The cold furnace's capacity moved with it: 1248 → 1152 units** (39 → 36 cells × 32). The heat
>    balance's calibration table is unaffected — every row saturates the charge-loss clamp — but a full
>    hearth is 7.7 % smaller in play. Accepted on the same ground as the cupola's −20 %: the crucible was
>    never a place burden should rest.
>
> **Not done, deliberately (Step 6):** siex's `BlockBlastFurnaceCoreHot` keeps its `Pool`+`Chargeable`
> overlap. New `test/SteelIndustryExpanded.Tests/Blocks/HotBlastFurnace/CrucibleOverlapTests.cs` pins what
> that costs — two of nine columns draw a block short while the bath stands, and the hidden units still
> count toward `ShaftChargeUnits`. It touches no golden and fails the day the remake starts.

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockBlastFurnaceCoreCold.cs:115-121`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockCupolaFurnaceCore.cs:88-91`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/blastcore.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/cupolacore.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/ChargeableCellsTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/ShaftColumnsTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceRoleCellsTests.cs`, `test/IronIndustryExpanded.Tests/Fixtures/FurnaceLayoutRig.cs:630-658`

**Consumes:** CellRole.Chargeable / CellRole.Pool; BlockEntityFurnaceCore.ChargeableCells (:429-430); .PoolCells (:373); FurnaceLayoutRig.AssertRoleGlyphs(def, layout, CellRole, glyphs, label)

**Produces:** Chargeable and Pool become disjoint cell sets on both iwex shaft furnaces; the shaft box is exactly the drawing's Chargeable cells

- [x] **Step 1.** Delete `.Role('h', CellRole.Chargeable)` at BlockBlastFurnaceCoreCold.cs:120 and BlockCupolaFurnaceCore.cs:90. Both files carry a comment predicting exactly this edit (BlockBlastFurnaceCoreCold.cs:115-119, CellRole.cs:110-116) — update those comments to past tense rather than leaving a prediction that already happened.
- [x] **Step 2.** Caution: `FurnaceLayoutRig` asserts `CellRole.Chargeable` against both `[ShaftGlyph, HearthGlyph]` (:630-641) precisely because the hearth row was chargeable. Once it is not, `HearthGlyph` must come out of that pair and the `Pool` assertion (:648-657) keeps it alone. Leaving both in is not a compile error and not a test failure — it just silently stops proving anything about which glyph carries which role, which is the exact vacuity the rig's own doc comment (:587-591) says has shipped seven times.
- [x] **Step 3.** Check `ChargeableCellsTests` and `ShaftColumnsTests`: the cold furnace's chargeable set drops from 39 to 36 cells and the cupola's from **5 to 4** (the cupola has four `c` cells plus the one `h`, and `The_cupola_offers_the_five_cells_of_its_single_column` pins 5 today). The shaft bounding box moves from y=1..5 to y=2..5 on the cold furnace, and y=1..5 to y=2..5 on the cupola. Any hard-coded count must move with it. **The cupola's −20 % is accepted and needs no rebalance** (user, 2026-08-05): its burden is *remelt* — pig, scrap and returns already through a furnace once — so it carries far more metal per unit charged than the blast furnace's ore burden. Four cells is right for what a cupola is; do not add a course to pay it back. Both shafts become **uniform-floored** as a result, so `ColumnFloorY` / `ChargeCellsOf` keep their coverage only through the synthetic fixtures in `ChargeMaterialisationTests` § *A hole in the column* — do not delete those as unused.
- [x] **Step 4.** Caution: Confirm the burden no longer stands on the crucible floor in play: with the crucible pool-only, the lowest charge level is y=2 and the raceway sits there. Verify `ChargeColumnAt` / the descent (U2/U3's) still terminates at y=2 and does not try to descend into a pool cell — add a test that a column's bottom segment does not consume into a `hearthmetal` block.
- [x] **Step 5.** Re-bless the two core goldens with `EXLIB_WRITE_GOLDENS=test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/blastcore.json,test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/cupolacore.json`.
- [x] **Step 6.** **Settled, not a decision to make: no.** Do not touch `src/SteelIndustryExpanded/.../BlockBlastFurnaceCoreHot.cs:105-106`. Its crucible is still the narrow two-cell one and its own comment (:98-102) explicitly defers 'whether the hot furnace should follow' to the smex remake. Say so in the WORKLOG rather than silently copying numbers across. **Re-confirmed 2026-08-05, and the deferral has a price nothing had written down.** After U4.4 the smex hot furnace becomes **the only shipped drawing where a live molten pool cell sits on a charge column's floor** — `Pool` and `Chargeable` on the same two cells, and those cells are the bottom of two of its nine columns. The known consequences, so nobody re-derives them: **two of nine columns render one block short for a whole campaign** while `hearthmetal` stands; the hidden units still count toward `ShaftChargeUnits`, so the HUD and the geometry disagree; `SolidifyBottomLayer` can freeze **over** live charge; and `ColumnFloorY` returns a cell the pool owns. **Why hold anyway:** dropping `Chargeable` alone leaves a **two-cell crucible under a 3×3 shaft** — a bosh no real furnace has — and bakes that shape into a golden, while the hearth's *actual* defect is the **cinder notch a course too high** at y=2. The remake moves crucible, notch and tuyeres in **one** layout change with **one** blessing; a half-fix now means the same golden is blessed twice from two different intents, which the plan's own collision rule forbids. **Land a smex regression test that pins the known-wrong behaviour** — the two short columns and the still-counted hidden units — so the deferral stops being invisible: the day the remake starts, it fails and names exactly what changed. It touches no golden.
- [x] **Step 7.** Run `./scripts/exmod.sh test 1.21` and `1.20`.

#### U4.6 — Adopt the two drawn tap shapes (this necessarily removes the `open` animation pose)

> **DONE 2026-08-21, landed together with U4.7 and U4.8.** Gate 9/9, **4,151** per version.
>
> ⛔ **Step 1's command needs the editable's subfolder**: the converter joins `assets/editable/shapes` with
> the name verbatim, so it is `furnaces/shaft/furnace-block-irontap`, not the bare name. Same for U4.7+.
>
> ⛔ **Step 5's `EXLIB_WRITE_GOLDENS` fragment form is the one U4.5 corrected**:
> `iiex/blocktypes/furnace/irontap`, matched against the def's own `domain/path`.
>
> ★ **Step 3 resolved to "no change": the offset stays 0.** Both new shapes run their launder out past
> z=16 (`TapCanal/Cube8` reaches z=20 on the iron tap and z=24 on the slag tap), which is the same
> direction the old art's runout pointed and the direction a tap declared `-n` pours. The plan's "authored
> spout-out" worry does not apply to the art that actually arrived.
>
> ★ **Step 2's premise verified, and it is why U4.7 came with it**: neither shape has an `animations` key
> and both carry exactly the three top-level elements `Base` / `TapCanal` / `ClayPlug`.
> `FurnaceTapPlugTests.Both_tap_shapes_carry_the_three_elements_the_prune_expects` pins both facts,
> because the render path's keep-lists are written against them.

**Files**
- Create: `assets/iiex/shapes/furnace/irontap.json`, `assets/iiex/shapes/furnace/slagtap.json`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockFurnaceTap.cs:33-39,57-88`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityFurnaceTap.cs:27,57-89,98-116`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/irontap.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/slagtap.json`, `assets/iiex/shapes/furnace/tap.json (delete once nothing references it)`
- Test: `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionGoldenTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/BlastFurnaceTapTests.cs`

**Consumes:** ExBlockDef.ShapeByTypePerOrientation(string baseShape, int offset = 0) (ExBlockDef.cs:308-317); ExBlockDef.ShapeByType(wildcard, base, rotateX?, rotateY?, rotateZ?); scripts/tools/convert-shape.py (texture keys `front1` and `burned` are both already mapped, convert-shape.py:66,72)

**Produces:** iwex:furnace-irontap-{side} draws `iwex:furnace/irontap` and iwex:furnace-slagtap-{side} draws `iwex:furnace/slagtap`; the tap block entity no longer runs an animator

- [x] **Step 1.** Export both shapes: `python scripts/tools/convert-shape.py furnace-block-irontap assets/iiex/shapes/furnace/irontap.json furnace-block-slagtap assets/iiex/shapes/furnace/slagtap.json`. Verify the output keeps the top-level `ClayPlug` element in both.
- [x] **Step 2.** Caution: Establish the fact that couples this task to U4.7 before writing any code: the two new shapes have **no `animations` array at all** (verified by walking both files), while the shipped `assets/iiex/shapes/furnace/tap.json` has one `open` clip and a `Lid` element. So `BlockEntityFurnaceTap.ApplyPourPose` (:98-116) calls `util.StartAnimation(Animation="open")` against a clip that will not exist. Adopting the shapes therefore deletes the pour pose whether or not you intend it. Plan U4.6 and U4.7 as one landing.
- [x] **Step 3.** Replace `.ShapeByTypePerOrientation("iwex:furnace/tap", 0)` (BlockFurnaceTap.cs:76) with a per-type call so each type gets its own shape — `Tap(domain, type)` already takes `type`, so `.ShapeByTypePerOrientation($"iwex:furnace/{type}", 0)` is a one-token change. Keep the offset at 0: the shapes are authored **spout-out** and the 180° belongs at the shape rotation, but the current rotation table (`n`->0, `e`->270, `s`->180, `w`->90) already renders the old spout-in art correctly. Look at each new shape's spout direction in-model before deciding whether `offset` becomes 180 — this is the one place the design doc's 'apply the reversal to the model only, never to TryPourMetal' can be got wrong invisibly.
- [x] **Step 4.** Caution: Do not touch `BlockEntityFurnaceTap.TryPourMetal` (:180-218). It spouts at `Pos.AddCopy(facing.Opposite).DownCopy()` and `BlastFurnaceTapTests.cs:160-186` pins `N→+z, S→−z, E→−x, W→+x` across all four sides. A tap in the east wall is declared `-w`. The cupola's drawing is the mirror of the blast furnaces' (`I` is west on the cold furnace, east on the cupola).
- [x] **Step 5.** Remove `.EntityBehavior("Animatable")` (BlockFurnaceTap.cs:62), the `_toggle` field (BlockEntityFurnaceTap.cs:27), `BuildAnimator` (:66-89) and `ApplyPourPose` (:98-116). `BuildAnimator`'s cache-key comment (:80-82) says 'the two tap types share a shape today but will not always' — that prediction now resolves; delete the comment with the code.
- [x] **Step 6.** Delete `assets/iiex/shapes/furnace/tap.json` and confirm `IiexDefinitionGoldenTests.Every_shape_reference_resolves_to_a_shipped_file` still passes (it is what catches a stale path).
- [x] **Step 7.** Re-bless both tap goldens with `EXLIB_WRITE_GOLDENS=test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/irontap.json,test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/slagtap.json`.
- [x] **Step 8.** Caution: Record the smex conflict explicitly in the WORKLOG: the shapes encode the notch height (iron channel Y 2-3, slag channel Y 10-11, verified in both files), which reads correctly only when both taps sit at y=1. On iwex's two furnaces they already do. On smex's hot furnace they do not — `T` is at y=1 (BlockBlastFurnaceCoreHot.cs:132) and `S` at y=2 (:142). smex therefore double-counts the height from this change onward. Either move `S` down in that drawing (a smex layout change with a golden re-bless) or write down that the hot furnace reads wrong until the smex remake. Do not leave it undecided.
- [x] **Step 9.** Run `./scripts/exmod.sh test 1.21`.

#### U4.7 — The clay plug is the closed state — SelectiveElements, fireclay cost, no animator

> **DONE 2026-08-21, with U4.6 and U4.8.**
>
> ★★ **Step 1's open ruling, settled: breaking the plug refunds nothing.** `TapUnplugClayRefund` exists as
> a knob and ships at **0**, against `CanalUnsealClayRefund`'s 2. Reason written into the config comment:
> ironmaking.md states the cost of a blow-in as *one whole clay plug*, which a refund would halve, and a
> tap plug is knocked through rather than chiselled out whole. One number reverses it.
>
> ⛔ **Step 3's `SelectiveElements` title is wrong and the body is right** - it is `ExShapeElements.Pruned`,
> for the reason the puddling hearth already gives: vanilla's per-segment prefix match can keep or drop the
> wrong subtree silently.
>
> ★ **The legacy save key needed a fallback the plan did not name.** A tap saved before this unit carries
> `isPouring` and no `plugged`, and `GetBool("plugged")` defaults to **false** - so a plain read would have
> reported every existing tap OPEN, not stopped. The read is
> `tree.GetBool("plugged", !tree.GetBool("isPouring"))`, pinned by
> `A_tap_saved_before_the_plug_existed_keeps_the_state_it_had`.
>
> ⛔ **A class doc comment over 16 lines fails `CommentStyleGuards`**, which is how the first pass went red.
> The rationale belongs in docs/design with a citation.

**Files**
- Create: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceTapPlugTests.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityFurnaceTap.cs:24-25,55-62,91-118,120-140,142-172`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockFurnaceTap.cs:95-187`, `src/IronIndustryExpanded/IiexConfig.cs`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceTapPlugTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/BlastFurnaceTapTests.cs:86-114`

**Consumes:** ExShapeElements.Pruned(Shape shape, IReadOnlyCollection<string> keep) (src/ExpandedLib/Helpers/ExShapeElements.cs:59-64); ExShapeElements.Matches(string path, IReadOnlyCollection<string> patterns) (:33-45); ExMesh.RotateByShape(MeshData, Block); the fireclay precedent BlockMoltenCanal.cs:382 (`game:clay-fire`) with IiexValues.CanalSealClayCost / .CanalUnsealClayRefund

**Produces:** BlockEntityFurnaceTap.IsPlugged (per-BE bool, tree key "plugged") drives OnTesselation; `IsPouring` becomes `!IsPlugged`; IiexConfig gains TapPlugClayCost and TapUnplugClayRefund

- [x] **Step 1.** Write `FurnaceTapPlugTests` first: (1) a fresh tap is plugged and pours nothing; (2) breaking the plug with an empty hand opens it and consumes nothing (the plug is destroyed, not recovered) or refunds `TapUnplugClayRefund` — pick per the design ('opening breaks it out and consumes it' — layered-charge.md § The clay plug) and pin whichever; (3) re-plugging with fewer than `TapPlugClayCost` `game:clay-fire` fails with an ingame error and does not close the tap; (4) the plugged flag round-trips through the tree; (5) `TryPourMetal` returns 0 while plugged.
- [x] **Step 2.** Replace `IsPouring` (BlockEntityFurnaceTap.cs:25) with `IsPlugged` and express `IsPouring => !IsPlugged` so `BlockEntityShaftFurnace.DrainIronTap`/`DrainSlagTap` need no edit. Default `IsPlugged = true` — a newly built tap is stopped, which is what makes blowing in cost one plug.
- [x] **Step 3.** Render the plug with `OnTesselation`, copying `BlockEntityPuddlingHearth.OnTesselation` (src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingHearth.cs:107-130) verbatim in structure: load the block's own shape via `Api.Assets.TryGet(Block.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"))`, call `ExShapeElements.Pruned(shape, keep)` with `["Base", "TapCanal", "ClayPlug"]` when plugged and `["Base", "TapCanal"]` when open, `ExMesh.RotateByShape(mesh, Block)`, and return `true`. This is why `ClayPlug` had to be a top-level element in both shapes — verified: both files have exactly three top-level elements, `Base` / `TapCanal` / `ClayPlug`, so the same two keep-lists drive both types with one code path.
- [x] **Step 4.** Add `MarkBlockDirty(Pos)` on the client in `FromTreeAttributes` (the existing `ApplyPourPose` call site, :136-137) so a remote plug/unplug re-tesselates. That is the exact pattern `BlockEntityPuddlingHearth.FromTreeAttributes` uses.
- [x] **Step 5.** Rewrite `BlockFurnaceTap.OnBlockInteractStart` (:97-166): keep the `TryToggleProjection` forward first (:106-113 — it must stay first or Ctrl+Shift+RMB stops previewing); then branch on held item. Empty hand + plugged -> break the plug open. Held `game:clay-fire` >= `TapPlugClayCost` + open -> consume and plug. Anything else -> fall through. Follow `BlockMoltenCanal.OnBlockInteractStart` (:395-440) for the held-stack + cost + `SendIngameError` shape.
- [x] **Step 6.** Add the interaction-help entries alongside the existing `iwex:blockhelp-tap-toggle` (:177-184), one for each verb, each `ShouldApply`-gated on the current plug state and the held item.
- [x] **Step 7.** Add lang keys in en/ru/uk: the two help lines, `iwex:tap-plugged` / `iwex:tap-open` (`tap-state` / `tap-open` / `tap-closed` already exist — reuse rather than duplicating), and the ingame-error code for insufficient clay. `LangParityTests` enforces all three files.
- [x] **Step 8.** Add `IiexConfig.TapPlugClayCost` and `TapUnplugClayRefund` (per the standing rule that new numbers go through config), doc-commented against `CanalSealClayCost` / `CanalUnsealClayRefund` so the two rituals are visibly the same family.
- [x] **Step 9.** Caution: Pin the thing that has no test today and would be silent if deleted: a tap that is open and whose crucible is empty must stay open. The old tap auto-drained while at temperature; with plugs 'an opened tap runs until the crucible empties and the player physically re-plugs it' (layered-charge.md). Assert that a full drain leaves `IsPlugged == false`.
- [x] **Step 10.** Run `./scripts/exmod.sh test 1.21`.

#### U4.8 — Move the no-canal gate from *cannot open* to *cannot pour*

> **DONE 2026-08-21, folded into the U4.6+U4.7 landing.** Not scope creep: U4.7 Step 5 rewrites
> `OnBlockInteractStart` wholesale and its replacement has no canal check in it, so keeping the gate would
> have meant writing code the same landing deletes. The status line went in with it
> (`BlockEntityFurnaceTap.HasCanalBelow`, reusing `iiex:tap-err-nocanal` as a status), and
> `FurnaceTapPlugTests.An_open_tap_with_no_canal_below_still_opens_and_pours_nothing` is Step 1's case.

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockFurnaceTap.cs:124-147`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityFurnaceTap.cs:142-172,180-218`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/BlastFurnaceTapTests.cs:117-155`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceTapPlugTests.cs`

**Consumes:** BlockMoltenCanalStart; ExOrientation.FacingFromSide(string? side) (src/ExpandedLib/Helpers/ExOrientation.cs:202-211); lang key `iwex:tap-err-nocanal`

**Produces:** BlockFurnaceTap.OnBlockInteractStart no longer refuses to open a tap with no canal below; BlockEntityFurnaceTap surfaces the missing canal in GetBlockInfo instead

- [x] **Step 1.** Write the failing test: a tap with no `BlockMoltenCanalStart` beneath its spout can be unplugged (`IsPlugged` goes false) and `TryPourMetal` still returns 0. Today the first half fails — `BlockFurnaceTap.OnBlockInteractStart:124-147` refuses to open and raises `iwex:tap-err-nocanal`, which is exactly what makes the blow-in sequence (open a tap and torch it) impossible.
- [x] **Step 2.** Delete the `isOpening` canal precondition block (:124-147). Keep the `ExOrientation.FacingFromSide` call pattern wherever a facing is still needed — `BlockFacing.FromCode` returns null for a single-letter token and both call sites document that as the trap (BlockFurnaceTap.cs:128-131, BlockEntityFurnaceTap.cs:185-189).
- [x] **Step 3.** `TryPourMetal` (BlockEntityFurnaceTap.cs:196-200) already returns 0 with no canal start — no logic change needed there. What is missing is feedback: add a `GetBlockInfo` line on an open tap with no canal below, reusing `iwex:tap-err-nocanal` as a status rather than an error, so a player who opens a tap and gets nothing is told why. Silently returning 0 forever is the failure the `FacingFromSide` comment already flags as the worst kind.
- [x] **Step 4.** Run `./scripts/exmod.sh test 1.21`.

#### U4.9 — Blow-in: a torch on an open tap lights the lowest charge round

> **Ruled 2026-08-05: torch on the shaft branch, automatic on the firebox branch.**
> Caution: **This rescopes the task.** The automatic "charge-is-full" ignition branch lives on the **shared**
> `BlockEntityFurnaceCore`, and the four firebox machines have no tap to torch — so the branch cannot simply be
> *deleted* as this task assumed. It must be **pushed down onto the two branches**: `BlockEntityShaftFurnace`
> requires the torch, `BlockEntityFireboxFurnace` keeps lighting itself when loaded.
>
> That lands in `BlockEntityFurnaceCore.cs`; U3.6's rewrite of that file landed 2026-08-06, so the
> sequencing constraint is satisfied. U6.6 Step 7 and U9's gate both assume auto-ignition today and stay
> correct, because both are firebox machines.
> The payoff: blowing in costs **one clay plug** and is a deliberate sequence, which is what gives U4.7's
> plug cost a counterpart at the other end of the campaign — *"tapping is a decision rather than a reflex"*.

> **DONE 2026-08-21.** Gate 9/9, **4,160** per version.
>
> ⛔⛔ **The rescoping note above is wrong about the code, and Step 1's caution is why.** It was written
> 2026-08-05, before U3.6 landed. The shared `_cachedIsFull → TryIgniteCharge` branch is **already
> firebox-only**: `BlockEntityShaftFurnace.DerivesState` is `true`, so a shaft never reaches it. Nothing
> needed pushing down. The shaft's auto-ignition was in `DeriveState`, which returned Firing the moment a
> raceway course held carbon.
>
> ★★ **So the real work was adding the one thing the branch was designed not to have: a stored bit.**
> `BlockEntityShaftFurnace.BlownIn` — set by the flame, cleared in `ExtinguishResidue`, serialized as
> `blownIn`. Its own docstring said *"there is no 'was lit' bit either"* and gave the reason that makes one
> safe: burn-out retains no fuel at the raceway, so nothing can relight off a stale flag.
>
> ★ **The torch asks none of `DeriveState`'s questions.** It sets the latch and stops; whether the charge
> takes stays one question asked in one place. A torch on a shaft with no carbon at its tuyeres buys
> nothing, which `A_lit_furnace_with_no_carbon_at_its_raceway_still_does_not_catch` pins.
>
> ★ **The predicate is vanilla's `BlockBehaviorCanIgnite`**, not a torch code — every `*-lit-*` block, and
> deliberately not `ItemFirestarter`, which vanilla itself treats as a separate class of igniter.
>
> ⛔ **The ripple was 58 tests**, every scenario that expected a furnace to light itself. Fixed with the
> fixture lever: `BlowInRig` performs the ritual through the tap block's own interaction, and each rig
> gained a `BlowIn()` step. ⛔⛔ **Two of the three furnace rigs identified as neither side** — `EnumAppSide`
> has no 0 member, so a substitute's default matched neither — which means every `Side == Server` branch in
> the production tick was being skipped in the cupola and smex suites without saying so. Both are
> server-side now.
>
> ★ **Two "on its own" claims were retired, not patched**: smex's
> `A_built_and_blown_furnace_reaches_melting_on_its_own` became
> `..._stays_dark_until_a_torch_reaches_it` and does the gesture, and the hopper's
> `..._FUELLED_and_LIT_through_its_own_hopper` now says the hopper lays the fuel and a player lights it.

**Files**
- Create: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceBlowInTests.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockFurnaceTap.cs:95-187`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityFurnaceTap.cs`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:1020-1036`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceBlowInTests.cs`, `test/IronIndustryExpanded.Tests/Scenarios/ColdBlastFurnaceScenarioTests.cs`

**Consumes:** BlockEntityFurnaceTap._anchor / MultiblockAnchorLink<BlockEntityFurnaceCore>.Resolve() (BlockEntityFurnaceTap.cs:44-53); BlockEntityFurnaceCore.ChargeableCells (:429); .ShaftCentrePos; ExSounds.Ignite ("game:sounds/torch-ignite"); protected virtual float IgnitionTemp => 900f (BlockEntityFurnaceCore.cs:221)

**Produces:** A public ignition entry point on BlockEntityFurnaceCore (e.g. `public bool TryLightFromTap(BlockPos tapPos)`) that the tap calls, replacing the automatic charge-is-full ignition branch as the player-driven route

- [x] **Step 1.** Caution: Establish that there is no existing torch mechanic anywhere in `src/` before writing anything — a repo-wide grep for `torch` finds only sound-asset names (`ExSounds.Ignite`, `ExSounds.TorchUnequip`). This is entirely new surface, and its shape depends on what U3 left of ignition. Read `BlockEntityFurnaceCore.cs:1020-1036` (the current `State == FurnaceState.Idle && _cachedIsFull && !IsChoked -> TryIgniteCharge -> _internalTemp = IgnitionTemp` branch) as it stands after U3 before deciding.
- [x] **Step 2.** Write the failing test: an unplugged tap on a charged, structurally complete furnace, clicked with a lit torch in hand, lights the lowest chargeable round; re-plugging and putting the blast on then takes the furnace to Firing. Do not force `StructureComplete` — build the footprint with `StructureRig`.
- [x] **Step 3.** Route the click through the anchor the tap already resolves (`Anchor.Resolve()`, BlockEntityFurnaceTap.cs:158) — the tap already knows its furnace for the HUD, so no new lookup is needed.
- [x] **Step 4.** Caution: Light the lowest charge round, not the tap's own cell. y=1 is crucible after U4.5, so there is no fuel at tap level; the flame reaches up into the raceway at y=2. Assert the ignited cell is in `ChargeableCells` and is the lowest of them — a test that only checks 'the furnace lit' would pass with the wrong cell.
- [x] **Step 5.** Gate on the tap being open. A plugged tap cannot be lit through, which is what makes the blow-in sequence cost one plug: open -> torch -> re-plug -> blast on.
- [x] **Step 6.** Add the interaction help + lang lines in en/ru/uk.
- [x] **Step 7.** Run `./scripts/exmod.sh test 1.21` and `1.20`; append a WORKLOG entry covering the whole unit.

### Traps — each of these makes a green suite a lie

- Caution: **`FurnaceLayoutRig.HearthGlyph` is a hand-written copy of `IiexCodes.HearthCell`, deliberately (an oracle computed from the thing it pins agrees with any value of it). It will not fail to compile when the constant changes.** Change `IiexCodes.HearthCell` and forget `FurnaceLayoutRig.cs:106-107` and the layout assertions still pass — against the old string. Every furnace layout test then proves nothing about the block the furnace actually places. (IiexCodes.cs:98-99 ↔ FurnaceLayoutRig.cs:106-107.)
- Caution: **Dropping `Role('h', Chargeable)` without pulling `HearthGlyph` out of the `Chargeable` assertion pair is invisible.** `FurnaceLayoutRig` currently asserts Chargeable against `[ShaftGlyph, HearthGlyph]` (:630-641). Leave both in and the assertion still passes — it just stops proving which glyph carries which role, which the rig's own comment (:587-591) calls out as a vacuity that 'has now shipped seven times'.
- Caution: **The two exact-value tap-throughput tests are the only thing pinning the drain arithmetic, and they are written against private floats.** `BlastFurnaceTapTests.cs:212-272` asserts 28 units and `:287-346` asserts 36 — the second exists *solely* because a copy-paste of `TapIronStackFactor` into `DrainSlagTap` would have gone uncaught by every other test in the tree. Port them onto the cells; do not weaken the assertions to `> 0` while refactoring, or the iron/slag factor swap becomes invisible again.
- Caution: **A `hearthmetal` block placed in the world with a molten cell that is never ticked reads as a green suite and a broken game.** Hosted molten cells are not auto-ticked (BEBehaviorMoltenCell.cs:18-25). A unit test that pushes metal and immediately reads it back passes without any tick at all. There must be a test that advances time and asserts the cell cooled, latched `Solidified`, and kept its `CellAmount` — otherwise 'freezes in place with the same content' is untested and a later refactor can delete the tick.
- Caution: **`TryPourMetal`'s facing convention is inverted and the cupola's drawing is mirrored from the blast furnaces'.** A tap in the east wall is declared `-w`; `I` is west on the cold furnace (BlockBlastFurnaceCoreCold.cs:66) and east on the cupola (BlockCupolaFurnaceCore.cs:70). Apply the shape reversal at `ShapeByTypePerOrientation`'s rotation only; touching `TryPourMetal` detaches the mesh from the behaviour and `BlastFurnaceTapTests.cs:160-186` is the only guard.
- Caution: **The slag tap's runout cell is unclaimed on purpose and nothing tests it.** On the cold furnace the `S` tap pours to (-3,0,0), which is why Layer 0's z=0 row opens with `.` rather than `#` (BlockBlastFurnaceCoreCold.cs:75-79). Claim it back — e.g. while 'tidying' the drawing after the crucible change — and the structure still completes, the furnace still lights, and it silently never drains its cinder. Same on smex's hot furnace at (-3,1,0) (BlockBlastFurnaceCoreHot.cs:70-73).
- Caution: **Two `BEBehaviorMoltenCell` instances on one BE do not fail loudly — the second silently overwrites the first's tree keys and `GetBehavior<T>()` silently returns only the first.** A test that writes one and reads one passes. The failing test must write both, round-trip through one tree, and read both back.
- Caution: **Adopting the tap shapes removes the `open` animation without any compile error.** `ApplyPourPose` calls `StartAnimation(Animation="open")`; an animation name that does not exist in the shape is dropped silently by the animator, exactly as an unknown `SelectiveElements` name is (BlockSandCastingBed.cs:102-106 records that same class of invisible-hole failure). If U4.6 lands without U4.7, the tap has no visible open/closed state at all and every test still passes.
- Caution: **Deleting `SolidifyBottomLayer` touches the reverberatory hearths.** `ExtinguishResidue` (BlockEntityFurnaceCore.cs:1453-1458) runs on every furnace; `BurnOutCharge()` inside it is the puddling and heating hearths' path. The zero-default on `DrainedMetalUnits` (:1398-1404) is what makes the freeze a no-op for them today — remove the quartet carelessly and the hearths lose their burn-out salvage with a green suite.
- Caution: **`smex:solidifiediron` is a released code and the only test that can see the whole migration chain lives in the hpex suite.** `test/SteelIndustryExpanded.Tests/Migrations/ReleasedCodeCoverageTests.cs` is the sole assertion that the smex row still reaches a live block — running only the iwex suite after the rename proves nothing about it (`BlockMigrationModSystem` drops an unresolvable pair with a `Logger.Warning`, so the failure in game is a block that just stops loading).
- Caution: **`BlockEntitySolidifiedIron`'s tree key is `"ironCount"`, not `"metalCount"`, deliberately.** Renaming it along with the class orphans every stamped count in every existing world with no error at all — the getter just falls back to the default 2.
- Caution: **The plug's `IsPlugged` must default to true, and nothing forces that.** If the flag defaults to false, every newly built tap is already open, the blow-in sequence costs no plug, and the whole 'tapping is a decision rather than a reflex' point evaporates — with every test still green, because the tests will have been written against whatever the default is.

**Gate.** `./scripts/exmod.sh test 1.21` and `./scripts/exmod.sh test 1.20` green (never `latest` — its ~20 IPlayer failures are upstream), with these specific behaviours pinned by tests that exist afterwards: (1) a furnace that reaches melting has `iwex:hearthmetal-pigiron` standing in every `PoolCells` position, holding metal, placed without the furnace being extinguished; (2) a second melt cycle grows the same cells rather than replacing them; (3) cooling latches `Solidified` with the identical `CellAmount` — nothing lost at the freeze — and a chisel + hammer returns bits equal to what was frozen; (4) a world holding a placed `iwex:solidifiediron` or `smex:solidifiediron` loads as `iwex:hearthmetal-pigiron` with its `ironCount` intact, proven by `HearthMetalMigrationTests` and `ReleasedCodeCoverageTests` (which lives in the hpex suite and must be run); (5) `IiexCodes.HearthCell` admits `hearthmetal-pigiron` and `hearthmetal-castiron` and rejects a bare-prefix lookalike, and `FurnaceLayoutRig.HearthGlyph` was updated with it; (6) `Chargeable` and `Pool` are disjoint on both iwex shaft furnaces and the rig's role-glyph oracle no longer accepts `HearthGlyph` for `Chargeable`; (7) the two tap types resolve to two distinct shapes, `IiexDefinitionGoldenTests.Every_shape_reference_resolves_to_a_shipped_file` passes, and the tap no longer declares `Animatable`; (8) a plugged tap pours nothing, unplugging consumes the plug, re-plugging spends `game:clay-fire`, and the flag round-trips; (9) a tap with no canal below opens but pours nothing, and says why; (10) a lit torch on an open tap ignites the lowest cell in `ChargeableCells` (asserted as that cell, not merely 'the furnace lit'); (11) `BlastFurnaceTapTests`' two exact-value throughput cases still assert 28 and 36 after the rewrite; (12) `git grep -n 'solidifiediron\\|solidifiedcastiron\\|_moltenIron\\|_moltenSlag\\|MaxMoltenProduct\\|MaxMoltenSlagPool\\|ClearMoltenPools'` over src/ and test/ returns only the two migration rows. Plus: goldens re-blessed with an explicit path list (never `=1`), `IiexBlocks.g.cs` regenerated with `EXLIB_WRITE_BLOCKCODES=1`, en/ru/uk lang parity green, and a WORKLOG entry — no commit.

---

# U5 — The burdenmaker (replaces and deletes the ore mixer and ore bunker)

U5 landed `iwex:burdenmaker` — a 9-cell, power-free stock house (two hoppers over a shared bunker basin
with one sliding gate) — and deleted the two machines it replaced together with their block entities, defs,
goldens, shapes, recipes, lang and tests. It collapsed burden's identity to the single `iwex:burden` item
(ore + flux, three flux bands; coke is charged separately at the furnace) and re-pointed the cupola at pig
and scrap charged directly through the material-role registry. Executed in full; landed 2026-08-06/07.
Record: docs/internal/worklog/2026-08.md and git history.

### Tasks

#### U5.1 — Export the burdenmaker shape to assets/iiex/shapes/ore/burdenmaker.json

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.2 — BlockBurdenmaker def: 9-cell footprint, five RCC stages, golden and block codes

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.3 — Rotation-aware cell classifier

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.4 — BlockEntityBurdenmaker: two hoppers, a bunker, one gate, and drops that return everything

Done 2026-08-06, except Step 10: the three OreSurfaceRenderer instances (two hopper interiors, bunker basin) are still open — client-side presentation only. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.5 — Interaction wiring: per-cell right-click, filler forwarding, interaction help and lang

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.6 — Grid recipe for the burdenmaker plus its cost-catalogue row

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.7 — Delete the ore mixer and the ore bunker — code, defs, goldens, shapes, recipes, lang, tests

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.8 — Collapse burden's identity: delete remeltburden, the family model, and the coke grade bands

Done 2026-08-07. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.9 — The cupola charges pig and scrap directly

Done 2026-08-06, ahead of U5.8. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.10 — Handbook and design-doc sync

Done 2026-08-07. Record: docs/internal/worklog/2026-08.md and git history.

---

# U6 — Puddling: the reverberatory furnace process, rabbling, the stack-height draught function

U6 turns the puddling furnace from a shell that cannot complete, cannot light and cannot melt into a machine that runs one full heat: fettle 3 rows → charge 9 pigs → fire the firebox → melt down → rabble through the small door → draw wrought balls → clean the bed back into fettlestock. Three of its four blockers are smaller than the plan says (the layout now declares 6 fillers, not 9; three are orphans, not five) and one is bigger and undocumented: `DisruptionMixFloor => 144` is a shaft number inherited by a 12-unit firebox, so a lit puddling furnace counts a disruption on every tick and snuffs itself after 30 s. U6 also owns `NaturalDraughtFor(courses, damper)` and the counted stack walk that finally gives `CellRole.Flue`, the damper (`IsOpen`) and the doors (`IsVenting`) their first consumer — collected once here for the coke oven and crucible furnace later.

**Entry condition.** U1 landed through at least U1.3 (verified: `src/IronIndustryExpanded/Items/CastStockItemDefinitions.cs` exists with BilletUnits 600 / BloomUnits 1000 / SlabUnits 3000; `iwex:castplate-heavy` exists at `src/IronIndustryExpanded/Items/CastPartItemDefinitions.cs:33`). The pig re-mass landed as U2.0 (2026-08-05): `ItemPig.PigUnits = 375`, matching every yield number below. U2 and U3 are done, so the old constraint against running U6 concurrently with them is satisfied; U3 retired the shaft branch's timer machinery while the firebox branch keeps its explicit FSM, so U6.3's disruption-floor fix lands as a firebox-branch override.

**Shared files** (collision risk): `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs — U2 (the column cutover), U3 (raceway + FSM deletion), U4 (hearth/taps/hearthmetal) and U6.3/U6.4/U6.5/U6.7 all edit it. U3.3 explicitly deletes the disruption floor and both extinguish thresholds U6.3 is overriding. Do not run U6 concurrently with U2 or U3.`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs — shared with the reheat furnace (U7.1). U6.3 and U6.4 add sealed/virtual members here that U7's heat-into-stock work will read.`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockHeatingHearth.cs and BlockHeatingFurnaceCore.cs — U6.1 fixes the heating furnace's six orphan fillers; U7.1 then builds the reheat process on that same hearth.`, `src/IronIndustryExpanded/IiexConfig.cs — every unit adds keys. U6 adds the two loss terms (U6.4), three draught coefficients (U6.5) and the puddling process temperature (U6.6); U3.3 deletes a block of Bf* keys.`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/HeatBalanceTests.cs — U6.4 and U6.6 add calibration rows; U3 rewrites the model these rows describe.`, `src/IronIndustryExpanded/BlockStructures/Forming/StockItemDefinitions.cs and StockForm.cs — U6.11 changes only the two mass numbers; U7.2/U7.3/U7.4 rewrite WorkPiece, the roll sets and the form table (including the bloom→bar rename U6 deliberately does not do) and the stage shape key to hundredths.`, `assets/iiex/lang/{en,ru,uk}.json — U6.2, U6.5, U6.9, U6.10 and U6.11 all add keys; every other unit does too. Merge conflicts here are near-certain; add keys, never reorder.`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/*.json and .../recipes/grid/*.json — U6.1, U6.5, U6.9 and U6.11 re-bless a handful; U2/U4/U5 re-bless overlapping ones. Always use path-fragment filters so two units' blessings do not overwrite each other.`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockPuddlingFurnaceCore.cs — U6.1 (fillers) and U6.5 (the cap/flue position) both edit the layout; keep them in one edit or the golden is re-blessed twice.`, `test/ExpandedLib.Testing/StructureRig.cs — read-only for U6, but its Raise() behaviour is the reason U6.1's test must be def-level; U2.2's fixture rewrite touches this file's consumers.`

### Rulings 2026-08-21 (owner) - two of these change task shape

1. **The puddling furnace's chimney is FIXED, not player-built.** *"Puddling furnace don't work with dynamic
   chimney length. It is fixed in layout because of the cap."* This settles U6.5 Step 6 the other way from
   the plan's own recommendation, and **dissolves U6.5's counted stack walk**: `StackCourses` for a
   reverberatory hearth is the flue its own drawing declares (`CellsWithRole(CellRole.Flue).Count`), which
   needs no walk, no neighbour invalidation and no cache, and never trips
   `ComponentScanBelow`. Steps 4, 7 and 9 fall away with it; the walk arrives with U9, whose coke oven and
   crucible furnace are the machines that actually have player-built stacks. The cap moves from `(-1,7,0)`
   to `(0,7,0)`, over the flue it caps, with its housing filler declared at `(0,7,1)` - which closes U6.1's
   stray. `NaturalDraughtFor` is still built here, pure and shared, because it is what turns a flue height
   into a draught number for every one of them.
   **Consequence for U6.6:** the stack is no longer a dial the player can raise, so if `PuddlingProcessTempC
   = 1400` is out of reach at the drawn height, the losses move - which is what that ruling already said.

2. **The bath is drawn; the balls are not.** *"When hearth smelts pigs those are removed and instead a
   molten metal surface is drawn. When player paddles, they pull ball out as part of paddle shape, no need
   to render ball shapes inside."* U6.8 Step 6's art question is answered smaller than either option it
   offered: the hearth needs **one** new element - a molten surface over the bed, shown in place of
   `Pigs/*` - and **no ball group at all**. R7 is satisfied because the ball appears on the paddle, whose
   drawn `IronBall1` head is exactly that and is why it was drawn holding one.

3. **U6.9 (b): real tools.** `iiex:tool-rabble` and `iiex:tool-paddle` both become items; the rabble
   gathers, the paddle draws out.

4. **U6.10 Step 2: a clean-out returns exactly 3 tap cinder**, so one heat fettles the next and the loop
   closes. The 175 u remainder is absorbed into that fixed 3 rather than divided into it.

### Tasks

#### U6.1 — Filler accounting — make both reverberatory layouts buildable by a player

**Files**
- Create: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceFillerAccountingTests.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockPuddlingHearth.cs:49-55`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockHeatingHearth.cs:46-57`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/puddlinghearth.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/heatinghearth.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceFillerAccountingTests.cs`

**Consumes:** ExBlockDef.ToJson() -> JObject with attributes.multiblockStructure {blockNumbers, offsets} and attributes.fillerOffsets [{x,y,z}]; StructureFootprint.Layout(Action<FillerLayoutBuilder>); FillerLayoutBuilder.Origin(int a, int b).Layer(int y, string grid); ExOrientation.AngleFromSide(string side); ExOrientation.RotateOffset(Vec3i, int angle)

**Produces:** A def-level invariant test asserting, for every iwex furnace core layout: {declared exlib:structurefiller offsets} == {union over part cells of (part cell + part.fillerOffsets rotated by AngleFromSide(part's layout side))}. Two corrected hearth footprints.

- [x] **Step 1.** Write the failing test first, and write it at definition level, not as a scenario. Read the plan's own warning and then read `test/ExpandedLib.Testing/StructureRig.cs:217-234`: `Raise()` synthesises a stand-in block for every empty footprint cell including `exlib:structurefiller`, so any scenario-level puddling test completes a furnace no player can build. A scenario here would be green and wrong.
- [x] **Step 2.** In the new test file, collect the four core defs — `BlockPuddlingFurnaceCore.Definitions("iwex").Single()`, `BlockHeatingFurnaceCore`, `BlockBlastFurnaceCoreCold`, `BlockCupolaFurnaceCore` — and for each, read `attributes.multiblockStructure`, invert `blockNumbers`, and collect every offset whose code is `exlib:structurefiller` (the declared set).
- [x] **Step 3.** For the same layout, walk the offsets whose code names an `iwex:furnace-*` part; for each, load that part's own def, read its `attributes.fillerOffsets`, rotate each by `ExOrientation.AngleFromSide(side)` taken from the legend code's side variant, and add `partCell + rotatedOffset` (the supplied set).
- [x] **Step 4.** Assert `DECLARED == SUPPLIED` set-wise. Both directions matter: an orphan (declared, unsupplied) means the structure can never complete; a stray (supplied, undeclared) means a part drops a filler block outside the footprint.
- [x] **Step 5.** Run it. It must fail with exactly these, verified against the shipped goldens: puddling declares 6 fillers `(-3,0,0) (-3,1,0) (-2,1,0) (-2,2,1) (-1,0,0) (-1,1,0)`, supplies 3 — hearth `(-3,0,0) (-1,0,0)` and charge door `(-2,2,1)`; orphans are `(-3,1,0) (-2,1,0) (-1,1,0)`. Heating declares 12, supplies 6 (hearth 5 + door 1); orphans are `(-3,1,-1) (-3,1,0) (-2,1,-1) (-2,1,0) (-1,1,-1) (-1,1,0)`. Puddling also has one stray: the chimney cap at `(-1,7,0)` produces a filler at `(-1,7,1)` that the layout does not declare — leave that failing here; it is U6.5's to resolve with the cap's position.
- [x] **Step 6.** Fix the puddling hearth: add `.Layer(1, "###")` to the existing `f.Origin(-1, 0).Layer(0, "#0#")` block at `BlockPuddlingHearth.cs:50-55`. Arithmetic checked: local `(-1,1,0) (0,1,0) (1,1,0)` + hearth cell `(-2,0,0)` = exactly the three orphans. The hearth is a bed with a low roof over it; the second course is the roof, which is why the drawing wanted those cells.
- [x] **Step 7.** Fix the heating hearth: add `.Layer(1, "###\n###")` to `f.Origin(-1, -1).Layer(0, "###\n#0#")` at `BlockHeatingHearth.cs:48-56`. Local `(-1,1,-1) (0,1,-1) (1,1,-1) (-1,1,0) (0,1,0) (1,1,0)` + `(-2,0,0)` = exactly the six orphans.
- [x] **Step 8.** Re-bless only the two changed goldens: `EXLIB_WRITE_GOLDENS=iwex/blocktypes/furnace/puddlinghearth,iwex/blocktypes/furnace/heatinghearth ./scripts/exmod.sh test 1.21`. Never `=1` — it re-blesses the whole domain unread.
- [x] **Step 9.** Read both diffs by hand before committing them; the only change should be added `fillerOffsets` entries.
- [x] **Step 10.** `./scripts/exmod.sh test 1.21`. The blast furnace and cupola arms of the new test must pass unchanged — if either fails, a fourth layout has the same defect and it is in scope.

#### U6.2 — Kill the shipped raw-key HUD bug: migrate puddling Lang.Get calls to the generated IiexLang constants

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingHearth.cs:170-183`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityChargeDoor.cs:128-142`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingChimneyCap.cs:60-67`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityHeatingFurnace.cs:69`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Definitions/IiexLangCoverageTests.cs`

**Consumes:** ExLangKeyGenerator (src/ExpandedLib.Generators/ExLangKeyGenerator.cs) — emits `IiexLang.<PascalKey>` consts from assets/iiex/lang/en.json; a missing key becomes a compile error

**Produces:** Every puddling-side `Lang.Get("iwex:...")` replaced by an `IiexLang.*` constant, so the key set is compiler-enforced

- [x] **Step 1.** Verify the bug first, because it ships today: `BlockEntityPuddlingHearth.cs:174` calls `Lang.Get("iwex:furnace-puddlinghearth-charge", …)` and `:181` calls `"iwex:furnace-puddlinghearth-needsfettle"`, but `assets/iiex/lang/en.json` defines `puddlinghearth-charge` / `puddlinghearth-needsfettle` with no `furnace-` prefix. Both render the raw key in game. `BlockEntityHeatingFurnace.cs:69` has the same shape: `iwex:heatingfurnace-ready` is not in en.json at all.
- [x] **Step 2.** Understand why nothing caught it: `test/ExpandedLib.Testing/LangCoverage.cs` only checks that every registered block code resolves to a `block-*` name key. It never looks at `Lang.Get` call sites. This is a permanent hole and the migration below is the only structural fix.
- [x] **Step 3.** Replace every hand-typed `Lang.Get("iwex:…")` in the four puddling-side block entities with the generated constant (`IiexLang.PuddlinghearthCharge`, `IiexLang.ChargedoorState`, `IiexLang.ChimneycapState`, …). The build now fails if a key is absent — that failure is the test for this task.
- [x] **Step 4.** Where a constant does not exist because the key is genuinely missing (`heatingfurnace-ready`), add the key to `assets/iiex/lang/en.json` and its ru/uk translations, following the RU/UK conventions (single `-`, no em-dash), then use the constant.
- [x] **Step 5.** Do not rename the existing `puddlinghearth-*` keys to `furnace-puddlinghearth-*`. The shorter key is the one the three locales already carry; moving it costs three files for no gain.
- [x] **Step 6.** `./scripts/exmod.sh test 1.21`. `IiexLangCoverageTests` and the locale-parity tests must stay green.

#### U6.3 — B8's fifth, undocumented cause — a firebox cannot hold a shaft's disruption floor, so a lit hearth snuffs itself in 30 s

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs:193-243`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:224`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FireboxChargeTests.cs`, `test/IronIndustryExpanded.Tests/Invariants/FurnaceBranchGuards.cs`

**Consumes:** BlockEntityFurnaceCore.DisruptionMixFloor (protected virtual int, :224); BlockEntityFireboxFurnace.MinChargeToIgnite (protected sealed override int => FireboxCellCount * IiexValues.FireboxMixPerCell, :241-242); IiexValues.FireboxMixPerCell = 12 (IiexConfig.cs:389); BEBehaviorFirebox.CellCapacity

**Produces:** `protected override int DisruptionMixFloor` on BlockEntityFireboxFurnace, derived from MinChargeToIgnite; a branch guard forbidding any firebox leaf from carrying a floor above its own capacity

- [x] **Step 1.** Write the failing test in `FireboxChargeTests.cs` (there is already a `#region B8, first cause` there that owns exactly this shape of defect). Assert: for every `BlockEntityFireboxFurnace` leaf, `DisruptionMixFloor <= MinChargeToIgnite`. Read it reflectively the way `FireboxChargeTests.cs:185` already reads `FireboxCellCount`.
- [x] **Step 2.** Confirm it fails and confirm the arithmetic by hand: `BlockEntityFurnaceCore.cs:224` is `protected virtual int DisruptionMixFloor => 144;` — a hard literal, never overridden anywhere in `src/`. The puddling firebox is one cell (`CellRole.Firebox` marks exactly `(-5,1,0)` in `goldens/iiex/blocktypes/furnace/puddlingcore.json`), so `MinChargeToIgnite = 1 × 12 = 12`. At `BlockEntityFurnaceCore.cs:1041`, `mixCount (max 12) < DisruptionMixFloor (144)` is true on every tick of a lit hearth, so `disruptionCount >= 1` always, `_extinguishSeconds` accrues (:1054) and `Extinguish()` fires at `ExtinguishThresholdDefault = 30` seconds (:1063-1067). The heating furnace's two-cell firebox (24 units) has the same fate.
- [x] **Step 3.** This is not in the plan, not in `docs/design/machines/puddling-furnace.md` § Gotchas and not in `docs/design/processes/puddling.md` § Open. Add it to the machine page's Gotchas as B8's fifth cause when the fix lands.
- [x] **Step 4.** Fix on the branch, not the leaf: add `protected override int DisruptionMixFloor => MinChargeToIgnite / 2;` (or whatever fraction the user prefers) to `BlockEntityFireboxFurnace`'s `#region Tunables` at `:193-243`, beside `MinChargeToIgnite`. Derived, never a literal — the whole reason `MinChargeToIgnite` was made derived (`:231-242`) applies verbatim here.
- [x] **Step 5.** Consider sealing it too, with the same argument the branch already uses for `ShaftHoldsLayeredCharge` / `AcceptedFamilies` / `MinChargeToIgnite`. A leaf re-introducing a hand-picked floor is exactly how this was born.
- [x] **Step 6.** Add the invariant to `test/IronIndustryExpanded.Tests/Invariants/FurnaceBranchGuards.cs` as a Law beside `NoFireboxAsksForMoreThanItsCellsCanHold` (:157) — that file already scans every `BlockEntityFireboxFurnace` leaf, so a third hearth added later inherits the guard.
- [x] **Step 7.** U3 retired the shaft branch's use of the disruption machinery; the firebox branch has no columns and keeps an explicit FSM, so this override is firebox-only. Say so in the code comment.
- [x] **Step 8.** `./scripts/exmod.sh test 1.21`.

#### U6.4 — Per-machine heat loss — charge loss and reverberatory transfer loss become virtuals

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:1211-1270`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs:193-243`, `src/IronIndustryExpanded/IiexConfig.cs:220-230`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/HeatBalanceTests.cs`

**Consumes:** BlockEntityFurnaceCore.ComputeHeatBalance(BurdenMix charge, float blastSupplyFrac, float blastTemp, int mixCount) (:1211); IiexValues.BfChargeLossFull = 310 (IiexConfig.cs:225); IiexValues.BfRadiationLossBase = 120 (:221); HeatBalance.Compute(...) in src/ExpandedLib/Heat/HeatBalance.cs

**Produces:** `protected virtual float ChargeLossFull => IiexValues.BfChargeLossFull;` and `protected virtual float TransferLoss => 0f;` on BlockEntityFurnaceCore; firebox-branch overrides (reverberatory transfer loss ≈250, firebox charge loss ≈100)

- [x] **Step 1.** Read `docs/design/machines/crucible-furnace.md:53-146` before touching anything. It settles the shape (per-machine charge loss + a reverberatory transfer loss that is a virtual with a branch default, not a branch constant) and states plainly that all coefficients are proposals to calibrate in play.
- [x] **Step 2.** Write the failing test in `HeatBalanceTests.cs`'s `#region Calibration`. Its existing `[InlineData]` rows pin the shaft furnace at 1420/1740/970 °C — those must not move. Add rows for a pure-fuel firebox (`BurdenMix(0f, 0f, count)`, so `FuelFrac == 1.0` and `fuelFactor` clamps at `BfMaxFuelFactor = 1.25`) asserting the new firebox T_process.
- [x] **Step 3.** Confirm the current number by hand so the test is written against arithmetic, not against whatever the code returns: `T_in = 950 + 900 × 1.25 × 0.5 = 1512.5`; `T_loss = 120 + 310 × clamp(12/12) + 0 = 430`; `T_process = 1082.5 °C`. `docs/design/machines/puddling-furnace.md` § Gotchas says ~1392.5 — that is the empty-firebox figure and an empty firebox cannot be lit. 1082.5 is the real ceiling and it is the number `crucible-furnace.md:387-400` uses.
- [x] **Step 4.** In `ComputeHeatBalance` at `:1245-1252`, replace the direct `IiexValues.BfChargeLossFull` read with `ChargeLossFull`, and add `+ TransferLoss` into `tLoss`. Both new members `protected virtual` on the core, defaulting to today's behaviour (`BfChargeLossFull` and `0f`), so the shaft furnaces are bit-identical.
- [x] **Step 5.** Override both on `BlockEntityFireboxFurnace`: `ChargeLossFull` to the firebox's real thermal sink (a bed charge, not a 320-unit descending column) and `TransferLoss` to the reverberatory bridge loss. Back both with new `IiexConfig` keys beside `BfChargeLossFull` (`IiexConfig.cs:223-225`) so they are tunable via `/exmod config iwex`; add XML doc comments naming what they mean.
- [x] **Step 6.** Caution: Do not make `TransferLoss` a branch constant. `crucible-furnace.md:118-130` is explicit: the crucible furnace is on the firebox branch but its pots sit in the coke bed with no bridge, so it must override transfer loss to ≈0. A constant here makes that machine unbuildable later.
- [x] **Step 7.** Re-derive every asserted temperature in the new `[InlineData]` rows from the changed formula by hand. Do not paste the number the run produced — that is how a calibration silently moves.
- [x] **Step 8.** `./scripts/exmod.sh test 1.21`. The three pre-existing shaft calibration rows must be untouched and green.

#### U6.5 — NaturalDraughtFor(courses, damper) — the counted stack walk, and the chimney cap that terminates it

**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Furnaces/StackDraught.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/StackDraughtTests.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:1226-1231`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockPuddlingFurnaceCore.cs:110-181`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockPuddlingChimneyCap.cs:49-60`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingChimneyCap.cs:19-34`, `src/IronIndustryExpanded/IiexConfig.cs:206-208`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/puddlingcore.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/puddlingchimneycap.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/StackDraughtTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceRoleCellsTests.cs:733-759`

**Consumes:** CellRole.Flue (src/ExpandedLib/Blocks/Structures/CellRole.cs:124); BlockEntityMultiblockStructure.CellsWithRole(CellRole) (used at BlockEntityFurnaceCore.cs:373,390,429); BlockEntityPuddlingChimneyCap.IsOpen (:22); BlockEntityChargeDoor.IsVenting (:44); IiexValues.BfNaturalDraughtFactor = 0.5f (IiexConfig.cs:207), read at BlockEntityFurnaceCore.cs:1229

**Produces:** `public static float StackDraught.NaturalDraughtFor(int courses, bool damperOpen, bool venting)` (pure); `protected virtual int StackCourses` on BlockEntityFurnaceCore, cached and invalidated on neighbour change; the natural-draught term in ComputeHeatBalance becomes a function of the two

- [x] **Step 1.** Read `docs/design/machines/crucible-furnace.md:74-107` (the curve peaks, it does not clamp: `natural(courses) = base + gain·√courses − friction·courses²`, proposed `base 0.5, gain 0.11, friction 0.00102`, peak near 9 courses) and `:249-278` (the walk rules: the core owns the walk, the cap terminates it, a gap breaks the count, cache it, and the chimney is outside `StructureComplete`).
- [x] **Step 2.** Write `StackDraughtTests.cs` first, against the pure function only. Pin the curve's shape, not its coefficients: `natural(0) == BfNaturalDraughtFactor`; strictly increasing to the peak; strictly decreasing after; `natural(30) < natural(0)`; a shut damper and a venting door each reduce it; the value is finite and positive for `courses` in 0..64.
- [x] **Step 3.** Create `src/IronIndustryExpanded/BlockStructures/Furnaces/StackDraught.cs` as a pure static class beside `HearthRows.cs` / `PuddlingHearthLayout.cs` — those two are the precedent for 'pure, testable without a world'. Back `base`/`gain`/`friction` with three new `IiexConfig` keys next to `BfNaturalDraughtFactor` (`IiexConfig.cs:206-207`).
- [x] **Step 4.** Now the walk. `CellRole.Flue` today marks `(0,3,0) (0,4,0) (0,5,0) (0,6,0)` on the puddling core and `(0,3,0) (0,4,0)` on the heating core (read off the goldens' `multiblockRoles`). The walk starts one cell above the highest flue cell — `docs/design/mechanics/multiblock.md:228` — and steps up while the ring `. b . / b a b / . b .` is valid, using the same brick-family alternation `smex:smokestack` accepts (`BlockSmokeStackIntake.cs:51-54`).
- [x] **Step 5.** Caution: Fix the cap's position, which U6.1 surfaced as a stray filler. Verified from `goldens/iiex/blocktypes/furnace/puddlingcore.json`: the flue column is at x=0 topping out at `(0,6,0)`, but `M` (`iwex:furnace-puddlingchimneycap-n`) sits at `(-1,7,0)` — one cell west, on top of the brick at `(-1,6,0)`. The cap does not cap the flue, and `(0,7,0)` is not in the layout at all. Nothing catches this: `FurnaceRoleCellsTests.cs:733-759` asserts only that the Flue role is non-empty.
- [x] **Step 6.** Decide and record: per the settled design the stack above the drawn flue is player-built and outside `StructureComplete`, so `M` should leave the core layout entirely and the cap becomes a block the player sets at the top of whatever they built. If the user prefers to keep a minimum cap in the drawing, move `M` to layer 7 column 6 (x=0) and declare its housing filler at `(0,7,1)` so U6.1's accounting balances. Either way the current position is wrong.
- [x] **Step 7.** Caution: The cap must stop resolving its core through `MultiblockAnchorLink`. `BlockEntityPuddlingChimneyCap` extends `BlockEntityFurnacePart`, whose link uses `BlockEntityFurnaceCore.ComponentScanBelow = 8` (`BlockEntityFurnaceCore.cs:1712`). A cap three courses higher than today is 10 above the core and silently loses it. Invert the direction: the core finds the cap by walking, and the cap reads its state from whatever the walk hands it (or tolerates a null core in `GetBlockInfo`).
- [x] **Step 8.** Wire the walk into `ComputeHeatBalance`: replace `float natural = IiexValues.BfNaturalDraughtFactor;` (`:1229`) with `float natural = StackDraught.NaturalDraughtFor(StackCourses, DamperOpen, Venting);`. `StackCourses` is a `protected virtual int` on the core defaulting to 0, cached; the firebox branch overrides it with the walk. `DamperOpen`/`Venting` read the cap and the door — the first consumers `IsOpen` and `IsVenting` have ever had.
- [x] **Step 9.** Cache the walk and invalidate it on neighbour change, the way the rest of the mod does. A per-tick block-accessor walk up a 30-block column is invisible in a headless test and fatal in play.
- [x] **Step 10.** R7 is not optional here: add a block-info line naming the count and the verdict — `stack: 11 courses -> draught 0.74 (peak at 9)`. Without it a decline past the peak is indistinguishable from a bug. Use `IiexLang` constants (U6.2), and add the en/ru/uk keys.
- [x] **Step 11.** Re-bless only the changed core/cap goldens by path fragment; read both diffs by hand.
- [x] **Step 12.** `./scripts/exmod.sh test 1.21`. `FurnaceRoleCellsTests.A_hearth_declares_none_of_the_five_roles_at_all` asserts the exact role set `["Firebox", "Flue"]` — it must still hold.

#### U6.6 — Close B8 — the puddling furnace gets its own process temperature instead of iron's melting point

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingFurnace.cs:55-65`, `src/IronIndustryExpanded/IiexConfig.cs:268-290`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/HeatBalanceTests.cs`, `test/IronIndustryExpanded.Tests/Invariants/FurnaceBranchGuards.cs`

**Consumes:** BlockEntityPuddlingFurnace.MeltingPoint (protected override float => IiexValues.BfIronMeltingPoint, :63); IiexValues.BfIronMeltingPoint = 1482f (IiexConfig.cs:271); the outputs of U6.4 and U6.5

**Produces:** `IiexValues.PuddlingProcessTempC` + `protected override float MeltingPoint => IiexValues.PuddlingProcessTempC;`, and an invariant that every firebox leaf's process temperature is reachable at its own buildable stack height

- [x] **Step 1.** this task carries an open decision and must not guess it. Two settled documents disagree. `docs/design/machines/puddling-furnace.md` Open #2 says 'give the furnace its own melt point below the natural-draught ceiling' and its Numbers section calls 1482 'wrong, and KNOWINGLY left' (echoed verbatim in the source comment at `BlockEntityPuddlingFurnace.cs:57-62`). `docs/design/machines/crucible-furnace.md:60-66` instead computes puddling's target at 1482 and lands it at ≈3 courses. Take the question to the user before writing code.
- [x] **Step 2.** **Ruled 2026-08-05: `PuddlingProcessTempC = 1400`** — the physically and historically correct number, chosen over a gameplay-convenient one at the user's instruction. **The mod's own two melting points bracket it, and that bracket is the process:** `CupolaCastIronMeltingPoint` **1200** < **1400** < `BfIronMeltingPoint` **1482**. Hot enough to melt pig down; too cool for decarburised iron to stay liquid. As carbon leaves the bath the metal's melting point climbs from ~1200 toward pure iron's ~1538, crosses the bath temperature partway through, and the iron **"comes to nature" — it balls up**. So the ball is *emergent from the window*, not a scripted stage, which is what makes rabbling a verb. Both alternatives break it symmetrically: **1482** melts the wrought iron (no ball); **1200** freezes it hard instead of pasty (no ball). And 1400 sits **7.5 °C above** the flat-draught ceiling of 1392.5 °C this task's own analysis computes — so the **chimney is load-bearing**, and the two fixes `puddling-furnace.md` lists as *alternatives* (own melt point **or** a real draught model) turn out to be the same fix. If 1400 needs more courses than a player will build, **the losses move, not this number.** The supporting argument: introduce `PuddlingProcessTempC` rather than reuse `BfIronMeltingPoint`. Puddling works pig in the pasty state — that is the entire process (`docs/design/processes/puddling.md` § What it is) — and `crucible-furnace.md:107-117` builds a whole mechanic on 'a reverberatory furnace physically cannot melt iron'. A puddling furnace whose threshold is iron's melting point contradicts both. `CupolaCastIronMeltingPoint = 1200` (`IiexConfig.cs:316`) is the natural reference: the charge is pig, and pig melts there.
- [x] **Step 3.** Write the failing test as an invariant, not a literal: for every `BlockEntityFireboxFurnace` leaf, its `MeltingPoint` must be clear of the T_process its own machine reaches at some buildable stack height, using U6.4's loss terms and U6.5's draught curve. That is the assertion that stays true when the coefficients are recalibrated in play; a `[InlineData(1200f)]` is not.
- [x] **Step 4.** Add the config key with an XML doc comment saying it is a process temperature, not a melting point, and why. Put it beside `BfIronMeltingPoint` at `IiexConfig.cs:271`.
- [x] **Step 5.** Replace `MeltingPoint` at `BlockEntityPuddlingFurnace.cs:63` and delete the six-line 'wrong, and KNOWINGLY left' comment above it — that comment explicitly says it moves with the draught work, which is this.
- [x] **Step 6.** Correct `docs/design/machines/puddling-furnace.md` § Gotchas: B8's second half now cites the real ceiling (1082.5 °C at a full one-cell firebox, not ~1392.5), and B8 has five causes, not four.
- [x] **Step 7.** `./scripts/exmod.sh test 1.21`. Then, in game or via a scenario, confirm a completed puddling furnace with a full firebox and a 3-course stack actually crosses into Melting — that is the first time in the mod's history it can.

#### U6.7 — The core resolves its own hearth

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingFurnace.cs:33-53`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:1335-1342`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceRoleCellsTests.cs`

**Consumes:** BlockEntityFurnaceCore.ShaftCentre (protected virtual Vec3i, :273; overridden to (-2,0,0) at BlockEntityPuddlingFurnace.cs:43); GlobalOf(Vec3i) (:433); ShaftCentrePos (:464); ScanForOutlets() (protected virtual, :1335); OnStructureCompleted() => ScanForOutlets() (:823)

**Produces:** `protected BlockEntityPuddlingHearth? Hearth` on BlockEntityPuddlingFurnace, resolved at ShaftCentrePos, refreshed by ScanForOutlets and null-safe

- [x] **Step 1.** Write the failing test: stand a puddling furnace up through `StructureRig` (copy the pattern from `test/IronIndustryExpanded.Tests/Fixtures/ColdBlastFurnaceScenes.cs:251` — `StructureRig.Around(world, anchor, def, angle)` then `.Complete()`), place a real `BlockPuddlingHearth` + `BlockEntityPuddlingHearth` at the hearth cell via `rig.Occupy(rig.Cell(-2,0,0), …)` before `Raise()`, and assert the core's `Hearth` is that block entity. Repeat at all four facings — `rig.Cell` does the rotation for you.
- [x] **Step 2.** Caution: never force `StructureComplete`. `Complete()` runs the machine's own monitor tick; a false there means the rig and the machine disagree, which is the honest outcome.
- [x] **Step 3.** Implement: override `ScanForOutlets()` on `BlockEntityPuddlingFurnace`, call `base.ScanForOutlets()` first, then resolve `Api.World.BlockAccessor.GetBlockEntity(ShaftCentrePos) as BlockEntityPuddlingHearth` into a field. `ScanForOutlets` is already called from `OnStructureCompleted` (`:823`), from `Initialize` when complete (`:846`) and from two lit-tick recovery paths (`:914`, `:995`), so the hearth is refreshed on exactly the same schedule the taps are.
- [x] **Step 4.** Make every consumer null-tolerant. A furnace whose hearth block was broken mid-heat must not throw on the production tick; it should read as 'no hearth' in the HUD and do nothing.
- [x] **Step 5.** Note the rotation is free: `ShaftCentrePos` is `GlobalOf(ShaftCentre)` and `GlobalOf` applies `_currentAngle`. Do not hand-rotate.
- [x] **Step 6.** `./scripts/exmod.sh test 1.21`.

### Traps — each of these makes a green suite a lie

- Caution: **`FurnaceLayoutRig.HearthGlyph` is a hand-written copy of `IiexCodes.HearthCell`, deliberately (an oracle computed from the thing it pins agrees with any value of it). It will not fail to compile when the constant changes.** Change `IiexCodes.HearthCell` and forget `FurnaceLayoutRig.cs:106-107` and the layout assertions still pass — against the old string. Every furnace layout test then proves nothing about the block the furnace actually places. (IiexCodes.cs:98-99 ↔ FurnaceLayoutRig.cs:106-107.)
- Caution: **Dropping `Role('h', Chargeable)` without pulling `HearthGlyph` out of the `Chargeable` assertion pair is invisible.** `FurnaceLayoutRig` currently asserts Chargeable against `[ShaftGlyph, HearthGlyph]` (:630-641). Leave both in and the assertion still passes — it just stops proving which glyph carries which role, which the rig's own comment (:587-591) calls out as a vacuity that 'has now shipped seven times'.
- Caution: **The two exact-value tap-throughput tests are the only thing pinning the drain arithmetic, and they are written against private floats.** `BlastFurnaceTapTests.cs:212-272` asserts 28 units and `:287-346` asserts 36 — the second exists *solely* because a copy-paste of `TapIronStackFactor` into `DrainSlagTap` would have gone uncaught by every other test in the tree. Port them onto the cells; do not weaken the assertions to `> 0` while refactoring, or the iron/slag factor swap becomes invisible again.
- Caution: **A `hearthmetal` block placed in the world with a molten cell that is never ticked reads as a green suite and a broken game.** Hosted molten cells are not auto-ticked (BEBehaviorMoltenCell.cs:18-25). A unit test that pushes metal and immediately reads it back passes without any tick at all. There must be a test that advances time and asserts the cell cooled, latched `Solidified`, and kept its `CellAmount` — otherwise 'freezes in place with the same content' is untested and a later refactor can delete the tick.
- Caution: **`TryPourMetal`'s facing convention is inverted and the cupola's drawing is mirrored from the blast furnaces'.** A tap in the east wall is declared `-w`; `I` is west on the cold furnace (BlockBlastFurnaceCoreCold.cs:66) and east on the cupola (BlockCupolaFurnaceCore.cs:70). Apply the shape reversal at `ShapeByTypePerOrientation`'s rotation only; touching `TryPourMetal` detaches the mesh from the behaviour and `BlastFurnaceTapTests.cs:160-186` is the only guard.
- Caution: **The slag tap's runout cell is unclaimed on purpose and nothing tests it.** On the cold furnace the `S` tap pours to (-3,0,0), which is why Layer 0's z=0 row opens with `.` rather than `#` (BlockBlastFurnaceCoreCold.cs:75-79). Claim it back — e.g. while 'tidying' the drawing after the crucible change — and the structure still completes, the furnace still lights, and it silently never drains its cinder. Same on smex's hot furnace at (-3,1,0) (BlockBlastFurnaceCoreHot.cs:70-73).
- Caution: **Two `BEBehaviorMoltenCell` instances on one BE do not fail loudly — the second silently overwrites the first's tree keys and `GetBehavior<T>()` silently returns only the first.** A test that writes one and reads one passes. The failing test must write both, round-trip through one tree, and read both back.
- Caution: **Adopting the tap shapes removes the `open` animation without any compile error.** `ApplyPourPose` calls `StartAnimation(Animation="open")`; an animation name that does not exist in the shape is dropped silently by the animator, exactly as an unknown `SelectiveElements` name is (BlockSandCastingBed.cs:102-106 records that same class of invisible-hole failure). If U4.6 lands without U4.7, the tap has no visible open/closed state at all and every test still passes.
- Caution: **Deleting `SolidifyBottomLayer` touches the reverberatory hearths.** `ExtinguishResidue` (BlockEntityFurnaceCore.cs:1453-1458) runs on every furnace; `BurnOutCharge()` inside it is the puddling and heating hearths' path. The zero-default on `DrainedMetalUnits` (:1398-1404) is what makes the freeze a no-op for them today — remove the quartet carelessly and the hearths lose their burn-out salvage with a green suite.
- Caution: **`smex:solidifiediron` is a released code and the only test that can see the whole migration chain lives in the hpex suite.** `test/SteelIndustryExpanded.Tests/Migrations/ReleasedCodeCoverageTests.cs` is the sole assertion that the smex row still reaches a live block — running only the iwex suite after the rename proves nothing about it (`BlockMigrationModSystem` drops an unresolvable pair with a `Logger.Warning`, so the failure in game is a block that just stops loading).
- Caution: **`BlockEntitySolidifiedIron`'s tree key is `"ironCount"`, not `"metalCount"`, deliberately.** Renaming it along with the class orphans every stamped count in every existing world with no error at all — the getter just falls back to the default 2.
- Caution: **The plug's `IsPlugged` must default to true, and nothing forces that.** If the flag defaults to false, every newly built tap is already open, the blow-in sequence costs no plug, and the whole 'tapping is a decision rather than a reflex' point evaporates — with every test still green, because the tests will have been written against whatever the default is.

**Gate.** `./scripts/exmod.sh test 1.21` and `./scripts/exmod.sh test 1.20` green (never `latest` — its ~20 IPlayer failures are upstream), with these specific behaviours pinned by tests that exist afterwards: (1) a furnace that reaches melting has `iwex:hearthmetal-pigiron` standing in every `PoolCells` position, holding metal, placed without the furnace being extinguished; (2) a second melt cycle grows the same cells rather than replacing them; (3) cooling latches `Solidified` with the identical `CellAmount` — nothing lost at the freeze — and a chisel + hammer returns bits equal to what was frozen; (4) a world holding a placed `iwex:solidifiediron` or `smex:solidifiediron` loads as `iwex:hearthmetal-pigiron` with its `ironCount` intact, proven by `HearthMetalMigrationTests` and `ReleasedCodeCoverageTests` (which lives in the hpex suite and must be run); (5) `IiexCodes.HearthCell` admits `hearthmetal-pigiron` and `hearthmetal-castiron` and rejects a bare-prefix lookalike, and `FurnaceLayoutRig.HearthGlyph` was updated with it; (6) `Chargeable` and `Pool` are disjoint on both iwex shaft furnaces and the rig's role-glyph oracle no longer accepts `HearthGlyph` for `Chargeable`; (7) the two tap types resolve to two distinct shapes, `IiexDefinitionGoldenTests.Every_shape_reference_resolves_to_a_shipped_file` passes, and the tap no longer declares `Animatable`; (8) a plugged tap pours nothing, unplugging consumes the plug, re-plugging spends `game:clay-fire`, and the flag round-trips; (9) a tap with no canal below opens but pours nothing, and says why; (10) a lit torch on an open tap ignites the lowest cell in `ChargeableCells` (asserted as that cell, not merely 'the furnace lit'); (11) `BlastFurnaceTapTests`' two exact-value throughput cases still assert 28 and 36 after the rewrite; (12) `git grep -n 'solidifiediron\\|solidifiedcastiron\\|_moltenIron\\|_moltenSlag\\|MaxMoltenProduct\\|MaxMoltenSlagPool\\|ClearMoltenPools'` over src/ and test/ returns only the two migration rows. Plus: goldens re-blessed with an explicit path list (never `=1`), `IiexBlocks.g.cs` regenerated with `EXLIB_WRITE_BLOCKCODES=1`, en/ru/uk lang parity green, and a WORKLOG entry — no commit.

---

# U5 — The burdenmaker (replaces and deletes the ore mixer and ore bunker)

U5 landed `iwex:burdenmaker` — a 9-cell, power-free stock house (two hoppers over a shared bunker basin
with one sliding gate) — and deleted the two machines it replaced together with their block entities, defs,
goldens, shapes, recipes, lang and tests. It collapsed burden's identity to the single `iwex:burden` item
(ore + flux, three flux bands; coke is charged separately at the furnace) and re-pointed the cupola at pig
and scrap charged directly through the material-role registry. Executed in full; landed 2026-08-06/07.
Record: docs/internal/worklog/2026-08.md and git history.

### Tasks

#### U5.1 — Export the burdenmaker shape to assets/iiex/shapes/ore/burdenmaker.json

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.2 — BlockBurdenmaker def: 9-cell footprint, five RCC stages, golden and block codes

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.3 — Rotation-aware cell classifier

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.4 — BlockEntityBurdenmaker: two hoppers, a bunker, one gate, and drops that return everything

Done 2026-08-06, except Step 10: the three OreSurfaceRenderer instances (two hopper interiors, bunker basin) are still open — client-side presentation only. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.5 — Interaction wiring: per-cell right-click, filler forwarding, interaction help and lang

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.6 — Grid recipe for the burdenmaker plus its cost-catalogue row

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.7 — Delete the ore mixer and the ore bunker — code, defs, goldens, shapes, recipes, lang, tests

Done 2026-08-06. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.8 — Collapse burden's identity: delete remeltburden, the family model, and the coke grade bands

Done 2026-08-07. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.9 — The cupola charges pig and scrap directly

Done 2026-08-06, ahead of U5.8. Record: docs/internal/worklog/2026-08.md and git history.

#### U5.10 — Handbook and design-doc sync

Done 2026-08-07. Record: docs/internal/worklog/2026-08.md and git history.

---

# U6 — Puddling: the reverberatory furnace process, rabbling, the stack-height draught function

U6 turns the puddling furnace from a shell that cannot complete, cannot light and cannot melt into a machine that runs one full heat: fettle 3 rows → charge 9 pigs → fire the firebox → melt down → rabble through the small door → draw wrought balls → clean the bed back into fettlestock. Three of its four blockers are smaller than the plan says (the layout now declares 6 fillers, not 9; three are orphans, not five) and one is bigger and undocumented: `DisruptionMixFloor => 144` is a shaft number inherited by a 12-unit firebox, so a lit puddling furnace counts a disruption on every tick and snuffs itself after 30 s. U6 also owns `NaturalDraughtFor(courses, damper)` and the counted stack walk that finally gives `CellRole.Flue`, the damper (`IsOpen`) and the doors (`IsVenting`) their first consumer — collected once here for the coke oven and crucible furnace later.

**Entry condition.** U1 landed through at least U1.3 (verified: `src/IronIndustryExpanded/Items/CastStockItemDefinitions.cs` exists with BilletUnits 600 / BloomUnits 1000 / SlabUnits 3000; `iwex:castplate-heavy` exists at `src/IronIndustryExpanded/Items/CastPartItemDefinitions.cs:33`). The pig re-mass landed as U2.0 (2026-08-05): `ItemPig.PigUnits = 375`, matching every yield number below. U2 and U3 are done, so the old constraint against running U6 concurrently with them is satisfied; U3 retired the shaft branch's timer machinery while the firebox branch keeps its explicit FSM, so U6.3's disruption-floor fix lands as a firebox-branch override.

**Shared files** (collision risk): `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs — U2 (the column cutover), U3 (raceway + FSM deletion), U4 (hearth/taps/hearthmetal) and U6.3/U6.4/U6.5/U6.7 all edit it. U3.3 explicitly deletes the disruption floor and both extinguish thresholds U6.3 is overriding. Do not run U6 concurrently with U2 or U3.`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs — shared with the reheat furnace (U7.1). U6.3 and U6.4 add sealed/virtual members here that U7's heat-into-stock work will read.`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockHeatingHearth.cs and BlockHeatingFurnaceCore.cs — U6.1 fixes the heating furnace's six orphan fillers; U7.1 then builds the reheat process on that same hearth.`, `src/IronIndustryExpanded/IiexConfig.cs — every unit adds keys. U6 adds the two loss terms (U6.4), three draught coefficients (U6.5) and the puddling process temperature (U6.6); U3.3 deletes a block of Bf* keys.`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/HeatBalanceTests.cs — U6.4 and U6.6 add calibration rows; U3 rewrites the model these rows describe.`, `src/IronIndustryExpanded/BlockStructures/Forming/StockItemDefinitions.cs and StockForm.cs — U6.11 changes only the two mass numbers; U7.2/U7.3/U7.4 rewrite WorkPiece, the roll sets and the form table (including the bloom→bar rename U6 deliberately does not do) and the stage shape key to hundredths.`, `assets/iiex/lang/{en,ru,uk}.json — U6.2, U6.5, U6.9, U6.10 and U6.11 all add keys; every other unit does too. Merge conflicts here are near-certain; add keys, never reorder.`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/*.json and .../recipes/grid/*.json — U6.1, U6.5, U6.9 and U6.11 re-bless a handful; U2/U4/U5 re-bless overlapping ones. Always use path-fragment filters so two units' blessings do not overwrite each other.`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockPuddlingFurnaceCore.cs — U6.1 (fillers) and U6.5 (the cap/flue position) both edit the layout; keep them in one edit or the golden is re-blessed twice.`, `test/ExpandedLib.Testing/StructureRig.cs — read-only for U6, but its Raise() behaviour is the reason U6.1's test must be def-level; U2.2's fixture rewrite touches this file's consumers.`

### Rulings 2026-08-21 (owner) - two of these change task shape

1. **The puddling furnace's chimney is FIXED, not player-built.** *"Puddling furnace don't work with dynamic
   chimney length. It is fixed in layout because of the cap."* This settles U6.5 Step 6 the other way from
   the plan's own recommendation, and **dissolves U6.5's counted stack walk**: `StackCourses` for a
   reverberatory hearth is the flue its own drawing declares (`CellsWithRole(CellRole.Flue).Count`), which
   needs no walk, no neighbour invalidation and no cache, and never trips
   `ComponentScanBelow`. Steps 4, 7 and 9 fall away with it; the walk arrives with U9, whose coke oven and
   crucible furnace are the machines that actually have player-built stacks. The cap moves from `(-1,7,0)`
   to `(0,7,0)`, over the flue it caps, with its housing filler declared at `(0,7,1)` - which closes U6.1's
   stray. `NaturalDraughtFor` is still built here, pure and shared, because it is what turns a flue height
   into a draught number for every one of them.
   **Consequence for U6.6:** the stack is no longer a dial the player can raise, so if `PuddlingProcessTempC
   = 1400` is out of reach at the drawn height, the losses move - which is what that ruling already said.

2. **The bath is drawn; the balls are not.** *"When hearth smelts pigs those are removed and instead a
   molten metal surface is drawn. When player paddles, they pull ball out as part of paddle shape, no need
   to render ball shapes inside."* U6.8 Step 6's art question is answered smaller than either option it
   offered: the hearth needs **one** new element - a molten surface over the bed, shown in place of
   `Pigs/*` - and **no ball group at all**. R7 is satisfied because the ball appears on the paddle, whose
   drawn `IronBall1` head is exactly that and is why it was drawn holding one.

3. **U6.9 (b): real tools.** `iiex:tool-rabble` and `iiex:tool-paddle` both become items; the rabble
   gathers, the paddle draws out.

4. **U6.10 Step 2: a clean-out returns exactly 3 tap cinder**, so one heat fettles the next and the loop
   closes. The 175 u remainder is absorbed into that fixed 3 rather than divided into it.

### Tasks

#### U6.1 — Filler accounting — make both reverberatory layouts buildable by a player

**Files**
- Create: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceFillerAccountingTests.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockPuddlingHearth.cs:49-55`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockHeatingHearth.cs:46-57`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/puddlinghearth.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/heatinghearth.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceFillerAccountingTests.cs`

**Consumes:** ExBlockDef.ToJson() -> JObject with attributes.multiblockStructure {blockNumbers, offsets} and attributes.fillerOffsets [{x,y,z}]; StructureFootprint.Layout(Action<FillerLayoutBuilder>); FillerLayoutBuilder.Origin(int a, int b).Layer(int y, string grid); ExOrientation.AngleFromSide(string side); ExOrientation.RotateOffset(Vec3i, int angle)

**Produces:** A def-level invariant test asserting, for every iwex furnace core layout: {declared exlib:structurefiller offsets} == {union over part cells of (part cell + part.fillerOffsets rotated by AngleFromSide(part's layout side))}. Two corrected hearth footprints.

- [x] **Step 1.** Write the failing test first, and write it at definition level, not as a scenario. Read the plan's own warning and then read `test/ExpandedLib.Testing/StructureRig.cs:217-234`: `Raise()` synthesises a stand-in block for every empty footprint cell including `exlib:structurefiller`, so any scenario-level puddling test completes a furnace no player can build. A scenario here would be green and wrong.
- [x] **Step 2.** In the new test file, collect the four core defs — `BlockPuddlingFurnaceCore.Definitions("iwex").Single()`, `BlockHeatingFurnaceCore`, `BlockBlastFurnaceCoreCold`, `BlockCupolaFurnaceCore` — and for each, read `attributes.multiblockStructure`, invert `blockNumbers`, and collect every offset whose code is `exlib:structurefiller` (the declared set).
- [x] **Step 3.** For the same layout, walk the offsets whose code names an `iwex:furnace-*` part; for each, load that part's own def, read its `attributes.fillerOffsets`, rotate each by `ExOrientation.AngleFromSide(side)` taken from the legend code's side variant, and add `partCell + rotatedOffset` (the supplied set).
- [x] **Step 4.** Assert `DECLARED == SUPPLIED` set-wise. Both directions matter: an orphan (declared, unsupplied) means the structure can never complete; a stray (supplied, undeclared) means a part drops a filler block outside the footprint.
- [x] **Step 5.** Run it. It must fail with exactly these, verified against the shipped goldens: puddling declares 6 fillers `(-3,0,0) (-3,1,0) (-2,1,0) (-2,2,1) (-1,0,0) (-1,1,0)`, supplies 3 — hearth `(-3,0,0) (-1,0,0)` and charge door `(-2,2,1)`; orphans are `(-3,1,0) (-2,1,0) (-1,1,0)`. Heating declares 12, supplies 6 (hearth 5 + door 1); orphans are `(-3,1,-1) (-3,1,0) (-2,1,-1) (-2,1,0) (-1,1,-1) (-1,1,0)`. Puddling also has one stray: the chimney cap at `(-1,7,0)` produces a filler at `(-1,7,1)` that the layout does not declare — leave that failing here; it is U6.5's to resolve with the cap's position.
- [x] **Step 6.** Fix the puddling hearth: add `.Layer(1, "###")` to the existing `f.Origin(-1, 0).Layer(0, "#0#")` block at `BlockPuddlingHearth.cs:50-55`. Arithmetic checked: local `(-1,1,0) (0,1,0) (1,1,0)` + hearth cell `(-2,0,0)` = exactly the three orphans. The hearth is a bed with a low roof over it; the second course is the roof, which is why the drawing wanted those cells.
- [x] **Step 7.** Fix the heating hearth: add `.Layer(1, "###\n###")` to `f.Origin(-1, -1).Layer(0, "###\n#0#")` at `BlockHeatingHearth.cs:48-56`. Local `(-1,1,-1) (0,1,-1) (1,1,-1) (-1,1,0) (0,1,0) (1,1,0)` + `(-2,0,0)` = exactly the six orphans.
- [x] **Step 8.** Re-bless only the two changed goldens: `EXLIB_WRITE_GOLDENS=iwex/blocktypes/furnace/puddlinghearth,iwex/blocktypes/furnace/heatinghearth ./scripts/exmod.sh test 1.21`. Never `=1` — it re-blesses the whole domain unread.
- [x] **Step 9.** Read both diffs by hand before committing them; the only change should be added `fillerOffsets` entries.
- [x] **Step 10.** `./scripts/exmod.sh test 1.21`. The blast furnace and cupola arms of the new test must pass unchanged — if either fails, a fourth layout has the same defect and it is in scope.

#### U6.2 — Kill the shipped raw-key HUD bug: migrate puddling Lang.Get calls to the generated IiexLang constants

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingHearth.cs:170-183`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityChargeDoor.cs:128-142`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingChimneyCap.cs:60-67`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityHeatingFurnace.cs:69`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Definitions/IiexLangCoverageTests.cs`

**Consumes:** ExLangKeyGenerator (src/ExpandedLib.Generators/ExLangKeyGenerator.cs) — emits `IiexLang.<PascalKey>` consts from assets/iiex/lang/en.json; a missing key becomes a compile error

**Produces:** Every puddling-side `Lang.Get("iwex:...")` replaced by an `IiexLang.*` constant, so the key set is compiler-enforced

- [x] **Step 1.** Verify the bug first, because it ships today: `BlockEntityPuddlingHearth.cs:174` calls `Lang.Get("iwex:furnace-puddlinghearth-charge", …)` and `:181` calls `"iwex:furnace-puddlinghearth-needsfettle"`, but `assets/iiex/lang/en.json` defines `puddlinghearth-charge` / `puddlinghearth-needsfettle` with no `furnace-` prefix. Both render the raw key in game. `BlockEntityHeatingFurnace.cs:69` has the same shape: `iwex:heatingfurnace-ready` is not in en.json at all.
- [x] **Step 2.** Understand why nothing caught it: `test/ExpandedLib.Testing/LangCoverage.cs` only checks that every registered block code resolves to a `block-*` name key. It never looks at `Lang.Get` call sites. This is a permanent hole and the migration below is the only structural fix.
- [x] **Step 3.** Replace every hand-typed `Lang.Get("iwex:…")` in the four puddling-side block entities with the generated constant (`IiexLang.PuddlinghearthCharge`, `IiexLang.ChargedoorState`, `IiexLang.ChimneycapState`, …). The build now fails if a key is absent — that failure is the test for this task.
- [x] **Step 4.** Where a constant does not exist because the key is genuinely missing (`heatingfurnace-ready`), add the key to `assets/iiex/lang/en.json` and its ru/uk translations, following the RU/UK conventions (single `-`, no em-dash), then use the constant.
- [x] **Step 5.** Do not rename the existing `puddlinghearth-*` keys to `furnace-puddlinghearth-*`. The shorter key is the one the three locales already carry; moving it costs three files for no gain.
- [x] **Step 6.** `./scripts/exmod.sh test 1.21`. `IiexLangCoverageTests` and the locale-parity tests must stay green.

#### U6.3 — B8's fifth, undocumented cause — a firebox cannot hold a shaft's disruption floor, so a lit hearth snuffs itself in 30 s

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs:193-243`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:224`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FireboxChargeTests.cs`, `test/IronIndustryExpanded.Tests/Invariants/FurnaceBranchGuards.cs`

**Consumes:** BlockEntityFurnaceCore.DisruptionMixFloor (protected virtual int, :224); BlockEntityFireboxFurnace.MinChargeToIgnite (protected sealed override int => FireboxCellCount * IiexValues.FireboxMixPerCell, :241-242); IiexValues.FireboxMixPerCell = 12 (IiexConfig.cs:389); BEBehaviorFirebox.CellCapacity

**Produces:** `protected override int DisruptionMixFloor` on BlockEntityFireboxFurnace, derived from MinChargeToIgnite; a branch guard forbidding any firebox leaf from carrying a floor above its own capacity

- [x] **Step 1.** Write the failing test in `FireboxChargeTests.cs` (there is already a `#region B8, first cause` there that owns exactly this shape of defect). Assert: for every `BlockEntityFireboxFurnace` leaf, `DisruptionMixFloor <= MinChargeToIgnite`. Read it reflectively the way `FireboxChargeTests.cs:185` already reads `FireboxCellCount`.
- [x] **Step 2.** Confirm it fails and confirm the arithmetic by hand: `BlockEntityFurnaceCore.cs:224` is `protected virtual int DisruptionMixFloor => 144;` — a hard literal, never overridden anywhere in `src/`. The puddling firebox is one cell (`CellRole.Firebox` marks exactly `(-5,1,0)` in `goldens/iiex/blocktypes/furnace/puddlingcore.json`), so `MinChargeToIgnite = 1 × 12 = 12`. At `BlockEntityFurnaceCore.cs:1041`, `mixCount (max 12) < DisruptionMixFloor (144)` is true on every tick of a lit hearth, so `disruptionCount >= 1` always, `_extinguishSeconds` accrues (:1054) and `Extinguish()` fires at `ExtinguishThresholdDefault = 30` seconds (:1063-1067). The heating furnace's two-cell firebox (24 units) has the same fate.
- [x] **Step 3.** This is not in the plan, not in `docs/design/machines/puddling-furnace.md` § Gotchas and not in `docs/design/processes/puddling.md` § Open. Add it to the machine page's Gotchas as B8's fifth cause when the fix lands.
- [x] **Step 4.** Fix on the branch, not the leaf: add `protected override int DisruptionMixFloor => MinChargeToIgnite / 2;` (or whatever fraction the user prefers) to `BlockEntityFireboxFurnace`'s `#region Tunables` at `:193-243`, beside `MinChargeToIgnite`. Derived, never a literal — the whole reason `MinChargeToIgnite` was made derived (`:231-242`) applies verbatim here.
- [x] **Step 5.** Consider sealing it too, with the same argument the branch already uses for `ShaftHoldsLayeredCharge` / `AcceptedFamilies` / `MinChargeToIgnite`. A leaf re-introducing a hand-picked floor is exactly how this was born.
- [x] **Step 6.** Add the invariant to `test/IronIndustryExpanded.Tests/Invariants/FurnaceBranchGuards.cs` as a Law beside `NoFireboxAsksForMoreThanItsCellsCanHold` (:157) — that file already scans every `BlockEntityFireboxFurnace` leaf, so a third hearth added later inherits the guard.
- [x] **Step 7.** U3 retired the shaft branch's use of the disruption machinery; the firebox branch has no columns and keeps an explicit FSM, so this override is firebox-only. Say so in the code comment.
- [x] **Step 8.** `./scripts/exmod.sh test 1.21`.

#### U6.4 — Per-machine heat loss — charge loss and reverberatory transfer loss become virtuals

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:1211-1270`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs:193-243`, `src/IronIndustryExpanded/IiexConfig.cs:220-230`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/HeatBalanceTests.cs`

**Consumes:** BlockEntityFurnaceCore.ComputeHeatBalance(BurdenMix charge, float blastSupplyFrac, float blastTemp, int mixCount) (:1211); IiexValues.BfChargeLossFull = 310 (IiexConfig.cs:225); IiexValues.BfRadiationLossBase = 120 (:221); HeatBalance.Compute(...) in src/ExpandedLib/Heat/HeatBalance.cs

**Produces:** `protected virtual float ChargeLossFull => IiexValues.BfChargeLossFull;` and `protected virtual float TransferLoss => 0f;` on BlockEntityFurnaceCore; firebox-branch overrides (reverberatory transfer loss ≈250, firebox charge loss ≈100)

- [x] **Step 1.** Read `docs/design/machines/crucible-furnace.md:53-146` before touching anything. It settles the shape (per-machine charge loss + a reverberatory transfer loss that is a virtual with a branch default, not a branch constant) and states plainly that all coefficients are proposals to calibrate in play.
- [x] **Step 2.** Write the failing test in `HeatBalanceTests.cs`'s `#region Calibration`. Its existing `[InlineData]` rows pin the shaft furnace at 1420/1740/970 °C — those must not move. Add rows for a pure-fuel firebox (`BurdenMix(0f, 0f, count)`, so `FuelFrac == 1.0` and `fuelFactor` clamps at `BfMaxFuelFactor = 1.25`) asserting the new firebox T_process.
- [x] **Step 3.** Confirm the current number by hand so the test is written against arithmetic, not against whatever the code returns: `T_in = 950 + 900 × 1.25 × 0.5 = 1512.5`; `T_loss = 120 + 310 × clamp(12/12) + 0 = 430`; `T_process = 1082.5 °C`. `docs/design/machines/puddling-furnace.md` § Gotchas says ~1392.5 — that is the empty-firebox figure and an empty firebox cannot be lit. 1082.5 is the real ceiling and it is the number `crucible-furnace.md:387-400` uses.
- [x] **Step 4.** In `ComputeHeatBalance` at `:1245-1252`, replace the direct `IiexValues.BfChargeLossFull` read with `ChargeLossFull`, and add `+ TransferLoss` into `tLoss`. Both new members `protected virtual` on the core, defaulting to today's behaviour (`BfChargeLossFull` and `0f`), so the shaft furnaces are bit-identical.
- [x] **Step 5.** Override both on `BlockEntityFireboxFurnace`: `ChargeLossFull` to the firebox's real thermal sink (a bed charge, not a 320-unit descending column) and `TransferLoss` to the reverberatory bridge loss. Back both with new `IiexConfig` keys beside `BfChargeLossFull` (`IiexConfig.cs:223-225`) so they are tunable via `/exmod config iwex`; add XML doc comments naming what they mean.
- [x] **Step 6.** Caution: Do not make `TransferLoss` a branch constant. `crucible-furnace.md:118-130` is explicit: the crucible furnace is on the firebox branch but its pots sit in the coke bed with no bridge, so it must override transfer loss to ≈0. A constant here makes that machine unbuildable later.
- [x] **Step 7.** Re-derive every asserted temperature in the new `[InlineData]` rows from the changed formula by hand. Do not paste the number the run produced — that is how a calibration silently moves.
- [x] **Step 8.** `./scripts/exmod.sh test 1.21`. The three pre-existing shaft calibration rows must be untouched and green.

#### U6.5 — NaturalDraughtFor(courses, damper) — the counted stack walk, and the chimney cap that terminates it

**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Furnaces/StackDraught.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/StackDraughtTests.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:1226-1231`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockPuddlingFurnaceCore.cs:110-181`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockPuddlingChimneyCap.cs:49-60`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingChimneyCap.cs:19-34`, `src/IronIndustryExpanded/IiexConfig.cs:206-208`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/puddlingcore.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/puddlingchimneycap.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/StackDraughtTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceRoleCellsTests.cs:733-759`

**Consumes:** CellRole.Flue (src/ExpandedLib/Blocks/Structures/CellRole.cs:124); BlockEntityMultiblockStructure.CellsWithRole(CellRole) (used at BlockEntityFurnaceCore.cs:373,390,429); BlockEntityPuddlingChimneyCap.IsOpen (:22); BlockEntityChargeDoor.IsVenting (:44); IiexValues.BfNaturalDraughtFactor = 0.5f (IiexConfig.cs:207), read at BlockEntityFurnaceCore.cs:1229

**Produces:** `public static float StackDraught.NaturalDraughtFor(int courses, bool damperOpen, bool venting)` (pure); `protected virtual int StackCourses` on BlockEntityFurnaceCore, cached and invalidated on neighbour change; the natural-draught term in ComputeHeatBalance becomes a function of the two

- [x] **Step 1.** Read `docs/design/machines/crucible-furnace.md:74-107` (the curve peaks, it does not clamp: `natural(courses) = base + gain·√courses − friction·courses²`, proposed `base 0.5, gain 0.11, friction 0.00102`, peak near 9 courses) and `:249-278` (the walk rules: the core owns the walk, the cap terminates it, a gap breaks the count, cache it, and the chimney is outside `StructureComplete`).
- [x] **Step 2.** Write `StackDraughtTests.cs` first, against the pure function only. Pin the curve's shape, not its coefficients: `natural(0) == BfNaturalDraughtFactor`; strictly increasing to the peak; strictly decreasing after; `natural(30) < natural(0)`; a shut damper and a venting door each reduce it; the value is finite and positive for `courses` in 0..64.
- [x] **Step 3.** Create `src/IronIndustryExpanded/BlockStructures/Furnaces/StackDraught.cs` as a pure static class beside `HearthRows.cs` / `PuddlingHearthLayout.cs` — those two are the precedent for 'pure, testable without a world'. Back `base`/`gain`/`friction` with three new `IiexConfig` keys next to `BfNaturalDraughtFactor` (`IiexConfig.cs:206-207`).
- [x] **Step 4.** Now the walk. `CellRole.Flue` today marks `(0,3,0) (0,4,0) (0,5,0) (0,6,0)` on the puddling core and `(0,3,0) (0,4,0)` on the heating core (read off the goldens' `multiblockRoles`). The walk starts one cell above the highest flue cell — `docs/design/mechanics/multiblock.md:228` — and steps up while the ring `. b . / b a b / . b .` is valid, using the same brick-family alternation `smex:smokestack` accepts (`BlockSmokeStackIntake.cs:51-54`).
- [x] **Step 5.** Caution: Fix the cap's position, which U6.1 surfaced as a stray filler. Verified from `goldens/iiex/blocktypes/furnace/puddlingcore.json`: the flue column is at x=0 topping out at `(0,6,0)`, but `M` (`iwex:furnace-puddlingchimneycap-n`) sits at `(-1,7,0)` — one cell west, on top of the brick at `(-1,6,0)`. The cap does not cap the flue, and `(0,7,0)` is not in the layout at all. Nothing catches this: `FurnaceRoleCellsTests.cs:733-759` asserts only that the Flue role is non-empty.
- [x] **Step 6.** Decide and record: per the settled design the stack above the drawn flue is player-built and outside `StructureComplete`, so `M` should leave the core layout entirely and the cap becomes a block the player sets at the top of whatever they built. If the user prefers to keep a minimum cap in the drawing, move `M` to layer 7 column 6 (x=0) and declare its housing filler at `(0,7,1)` so U6.1's accounting balances. Either way the current position is wrong.
- [x] **Step 7.** Caution: The cap must stop resolving its core through `MultiblockAnchorLink`. `BlockEntityPuddlingChimneyCap` extends `BlockEntityFurnacePart`, whose link uses `BlockEntityFurnaceCore.ComponentScanBelow = 8` (`BlockEntityFurnaceCore.cs:1712`). A cap three courses higher than today is 10 above the core and silently loses it. Invert the direction: the core finds the cap by walking, and the cap reads its state from whatever the walk hands it (or tolerates a null core in `GetBlockInfo`).
- [x] **Step 8.** Wire the walk into `ComputeHeatBalance`: replace `float natural = IiexValues.BfNaturalDraughtFactor;` (`:1229`) with `float natural = StackDraught.NaturalDraughtFor(StackCourses, DamperOpen, Venting);`. `StackCourses` is a `protected virtual int` on the core defaulting to 0, cached; the firebox branch overrides it with the walk. `DamperOpen`/`Venting` read the cap and the door — the first consumers `IsOpen` and `IsVenting` have ever had.
- [x] **Step 9.** Cache the walk and invalidate it on neighbour change, the way the rest of the mod does. A per-tick block-accessor walk up a 30-block column is invisible in a headless test and fatal in play.
- [x] **Step 10.** R7 is not optional here: add a block-info line naming the count and the verdict — `stack: 11 courses -> draught 0.74 (peak at 9)`. Without it a decline past the peak is indistinguishable from a bug. Use `IiexLang` constants (U6.2), and add the en/ru/uk keys.
- [x] **Step 11.** Re-bless only the changed core/cap goldens by path fragment; read both diffs by hand.
- [x] **Step 12.** `./scripts/exmod.sh test 1.21`. `FurnaceRoleCellsTests.A_hearth_declares_none_of_the_five_roles_at_all` asserts the exact role set `["Firebox", "Flue"]` — it must still hold.

#### U6.6 — Close B8 — the puddling furnace gets its own process temperature instead of iron's melting point

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingFurnace.cs:55-65`, `src/IronIndustryExpanded/IiexConfig.cs:268-290`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/HeatBalanceTests.cs`, `test/IronIndustryExpanded.Tests/Invariants/FurnaceBranchGuards.cs`

**Consumes:** BlockEntityPuddlingFurnace.MeltingPoint (protected override float => IiexValues.BfIronMeltingPoint, :63); IiexValues.BfIronMeltingPoint = 1482f (IiexConfig.cs:271); the outputs of U6.4 and U6.5

**Produces:** `IiexValues.PuddlingProcessTempC` + `protected override float MeltingPoint => IiexValues.PuddlingProcessTempC;`, and an invariant that every firebox leaf's process temperature is reachable at its own buildable stack height

- [x] **Step 1.** this task carries an open decision and must not guess it. Two settled documents disagree. `docs/design/machines/puddling-furnace.md` Open #2 says 'give the furnace its own melt point below the natural-draught ceiling' and its Numbers section calls 1482 'wrong, and KNOWINGLY left' (echoed verbatim in the source comment at `BlockEntityPuddlingFurnace.cs:57-62`). `docs/design/machines/crucible-furnace.md:60-66` instead computes puddling's target at 1482 and lands it at ≈3 courses. Take the question to the user before writing code.
- [x] **Step 2.** **Ruled 2026-08-05: `PuddlingProcessTempC = 1400`** — the physically and historically correct number, chosen over a gameplay-convenient one at the user's instruction. **The mod's own two melting points bracket it, and that bracket is the process:** `CupolaCastIronMeltingPoint` **1200** < **1400** < `BfIronMeltingPoint` **1482**. Hot enough to melt pig down; too cool for decarburised iron to stay liquid. As carbon leaves the bath the metal's melting point climbs from ~1200 toward pure iron's ~1538, crosses the bath temperature partway through, and the iron **"comes to nature" — it balls up**. So the ball is *emergent from the window*, not a scripted stage, which is what makes rabbling a verb. Both alternatives break it symmetrically: **1482** melts the wrought iron (no ball); **1200** freezes it hard instead of pasty (no ball). And 1400 sits **7.5 °C above** the flat-draught ceiling of 1392.5 °C this task's own analysis computes — so the **chimney is load-bearing**, and the two fixes `puddling-furnace.md` lists as *alternatives* (own melt point **or** a real draught model) turn out to be the same fix. If 1400 needs more courses than a player will build, **the losses move, not this number.** The supporting argument: introduce `PuddlingProcessTempC` rather than reuse `BfIronMeltingPoint`. Puddling works pig in the pasty state — that is the entire process (`docs/design/processes/puddling.md` § What it is) — and `crucible-furnace.md:107-117` builds a whole mechanic on 'a reverberatory furnace physically cannot melt iron'. A puddling furnace whose threshold is iron's melting point contradicts both. `CupolaCastIronMeltingPoint = 1200` (`IiexConfig.cs:316`) is the natural reference: the charge is pig, and pig melts there.
- [x] **Step 3.** Write the failing test as an invariant, not a literal: for every `BlockEntityFireboxFurnace` leaf, its `MeltingPoint` must be clear of the T_process its own machine reaches at some buildable stack height, using U6.4's loss terms and U6.5's draught curve. That is the assertion that stays true when the coefficients are recalibrated in play; a `[InlineData(1200f)]` is not.
- [x] **Step 4.** Add the config key with an XML doc comment saying it is a process temperature, not a melting point, and why. Put it beside `BfIronMeltingPoint` at `IiexConfig.cs:271`.
- [x] **Step 5.** Replace `MeltingPoint` at `BlockEntityPuddlingFurnace.cs:63` and delete the six-line 'wrong, and KNOWINGLY left' comment above it — that comment explicitly says it moves with the draught work, which is this.
- [x] **Step 6.** Correct `docs/design/machines/puddling-furnace.md` § Gotchas: B8's second half now cites the real ceiling (1082.5 °C at a full one-cell firebox, not ~1392.5), and B8 has five causes, not four.
- [x] **Step 7.** `./scripts/exmod.sh test 1.21`. Then, in game or via a scenario, confirm a completed puddling furnace with a full firebox and a 3-course stack actually crosses into Melting — that is the first time in the mod's history it can.

#### U6.7 — The core resolves its own hearth

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingFurnace.cs:33-53`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:1335-1342`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceRoleCellsTests.cs`

**Consumes:** BlockEntityFurnaceCore.ShaftCentre (protected virtual Vec3i, :273; overridden to (-2,0,0) at BlockEntityPuddlingFurnace.cs:43); GlobalOf(Vec3i) (:433); ShaftCentrePos (:464); ScanForOutlets() (protected virtual, :1335); OnStructureCompleted() => ScanForOutlets() (:823)

**Produces:** `protected BlockEntityPuddlingHearth? Hearth` on BlockEntityPuddlingFurnace, resolved at ShaftCentrePos, refreshed by ScanForOutlets and null-safe

- [x] **Step 1.** Write the failing test: stand a puddling furnace up through `StructureRig` (copy the pattern from `test/IronIndustryExpanded.Tests/Fixtures/ColdBlastFurnaceScenes.cs:251` — `StructureRig.Around(world, anchor, def, angle)` then `.Complete()`), place a real `BlockPuddlingHearth` + `BlockEntityPuddlingHearth` at the hearth cell via `rig.Occupy(rig.Cell(-2,0,0), …)` before `Raise()`, and assert the core's `Hearth` is that block entity. Repeat at all four facings — `rig.Cell` does the rotation for you.
- [x] **Step 2.** Caution: never force `StructureComplete`. `Complete()` runs the machine's own monitor tick; a false there means the rig and the machine disagree, which is the honest outcome.
- [x] **Step 3.** Implement: override `ScanForOutlets()` on `BlockEntityPuddlingFurnace`, call `base.ScanForOutlets()` first, then resolve `Api.World.BlockAccessor.GetBlockEntity(ShaftCentrePos) as BlockEntityPuddlingHearth` into a field. `ScanForOutlets` is already called from `OnStructureCompleted` (`:823`), from `Initialize` when complete (`:846`) and from two lit-tick recovery paths (`:914`, `:995`), so the hearth is refreshed on exactly the same schedule the taps are.
- [x] **Step 4.** Make every consumer null-tolerant. A furnace whose hearth block was broken mid-heat must not throw on the production tick; it should read as 'no hearth' in the HUD and do nothing.
- [x] **Step 5.** Note the rotation is free: `ShaftCentrePos` is `GlobalOf(ShaftCentre)` and `GlobalOf` applies `_currentAngle`. Do not hand-rotate.
- [x] **Step 6.** `./scripts/exmod.sh test 1.21`.

#### U6.8 — Melt down — the 9-pig charge becomes a bath on the core's melt cadence

**Files**
- Create: `test/IronIndustryExpanded.Tests/Fixtures/PuddlingFurnaceScenes.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/PuddlingHearthTests.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingHearth.cs:42-100`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingFurnace.cs:67-76`, `src/IronIndustryExpanded/BlockStructures/Furnaces/PuddlingHearthLayout.cs:50-65`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/PuddlingHearthTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnacePartsTests.cs:55-120`

**Consumes:** BlockEntityPuddlingFurnace.SmeltCycle(object chargeHandle) (protected override void, :74 — empty today); BlockEntityPuddlingHearth.PigCount (:34), IsFullyCharged (:37), TryFettle(HearthRows.Row) (:48), TryChargePig(HearthRows.Row) (:63); PuddlingHearthLayout.PigCapacity = 9 (:22), PigsPerRow = 3 (:19), ElementsFor(IReadOnlyList<int>, IReadOnlyList<bool>) (:54); the Hearth accessor from U6.7

**Produces:** A per-heat melt state on BlockEntityPuddlingHearth (melt progress + a bath flag), driven only from SmeltCycle; PuddlingHearthLayout.ElementsFor extended to draw the melted state

- [x] **Step 1.** first, close the coverage hole this task sits in. `grep` proves it: nothing under `test/` references `BlockEntityPuddlingHearth`, `TryFettle`, `TryChargePig` or `ClearBed`. The design docs' claim that 'the charge half is live and tested' covers only the pure helpers (`FurnacePartsTests.cs:61,94,105,125` test `PuddlingHearthLayout` and `HearthRows`). Create `PuddlingHearthTests.cs` and pin the existing behaviour before adding to it: fettle-before-pig is refused (`:67`), re-fettling a loaded row is refused (`:52-53`), a loaded centre blocks both flanks but never itself (`:50`, `:65` + `HearthRows.cs:75-76`), `_pigs[i]` is clamped on read from the tree (`:154`), and a round-trip through `ToTreeAttributes`/`FromTreeAttributes` preserves the charge. Every one of these is a live mechanic a refactor could delete silently today.
- [x] **Step 2.** Create `PuddlingFurnaceScenes.cs` modelled on `ColdBlastFurnaceScenes.cs`: a `StructureRig`-raised puddling furnace with a real hearth and a real `BEBehaviorFirebox` bed, driven on the clock. Do not add a `StructureComplete` shortcut; the fixture's job is to prove the machine completes itself.
- [x] **Step 3.** Write the failing melt test: a fettled, fully-charged hearth in a lit furnace at process temperature loses its pigs to a bath over the melt cadence, and a hearth that is not fully charged still melts what it has (nine is the bed's capacity, not a gate).
- [x] **Step 4.** Implement the melt on `BlockEntityPuddlingHearth` as methods the core calls, and call them only from `BlockEntityPuddlingFurnace.SmeltCycle(chargeHandle)` (`:74`). The core reaches `SmeltCycle` at `BlockEntityFurnaceCore.cs:1181`, inside the Melting branch, on the `_meltIntervalSec / MeltSpeedFactor()` cadence.
- [x] **Step 5.** Caution: The hearth must not register its own tick listener. The core already carries bounded away-catch-up (`MaxAwayCatchupSteps => 600` at `BlockEntityFurnaceCore.cs:66`, driven by `BlockEntityProductionMachine.cs:114-128`), and `SmeltCycle` rides it for free. A `RegisterGameTickListener` on the hearth looks identical while the chunk is loaded and teleports the melt when it is not — which is the plan's away-catch-up trap, correctly stated but wrongly aimed at the core (the core already has it; the hearth is where it can be lost).
- [x] **Step 6.** Rendering: the shipped hearth shape has `Base`, `BaseExtension`, `Bed`, `Fettle/Cube11-13`, `Pigs/Pig1-9` and nothing for a bath or for balls (verified against `assets/iiex/shapes/furnace/puddlinghearth.json`). There are 16 balls per heat and only 9 pig elements, so balls cannot be drawn on the bed with today's art. **Ruled 2026-08-05, following the balling ruling.** U6.9 Step 4 settled **one ball per rabble**, so bath-in-the-mesh with the ball count only in block info (the fallback below) would leave the hearth looking **identical across 16 consecutive gestures** on the mod's single most hands-on machine. That is precisely the failure R7 (*"nothing is hidden"*) exists to prevent, so the ball group now has to be **drawn**: a bath element plus a ball group rendered as they form, which also makes the centre-first draw order read straight off the mesh. Cost is one editable shape edit + a `convert-shape` export + a golden re-bless — an art task this unit did not previously carry, so **raise it with the user before U6.8 starts** rather than shipping the invisible version. `ExShapeElements.Pruned` drops an unknown element name **silently**, so an invented name is an invisible hole in the mesh, not an error — the elements must exist in the shape before anything references them. Fallback if the art slips: while melting, `ElementsFor` drops the `Pigs/*` and keeps the `Fettle/*` cubes (the bath), and the ball count lives in the block info.
- [x] **Step 7.** Caution: Whatever element names the new state uses, pin them against the shipped shape in `FurnacePartsTests.cs`'s existing `#region Hearth element names match the shipped art` (`:55-120`). `ExShapeElements.Pruned` drops an unknown name without raising anything (`BlockEntityPuddlingHearth.cs:113-114` says so), so a typo is an invisible hole in the mesh.
- [x] **Step 8.** Serialize the melt state in `ToTreeAttributes`/`FromTreeAttributes` (`:137-159`) and clamp on read, exactly as `_pigs` is.
- [x] **Step 9.** `./scripts/exmod.sh test 1.21`.

#### U6.9 — Rabbling — the verb, the small door, the tools, and the two clips that have been waiting

**Files**
- Create: `assets/iiex/shapes/item/tool-rabble.json`, `assets/iiex/shapes/item/tool-paddle.json`, `src/IronIndustryExpanded/Items/PuddlingToolItemDefinitions.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockChargeDoor.cs:46-57`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityChargeDoor.cs:31-44`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingHearth.cs`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/PuddlingHearthTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionGoldenTests.cs`

**Consumes:** BlockEntityChargeDoor.SmallOpen (:26), HasSmallDoor (:29), IsVenting (:44), the `doorClips` block attribute (BlockChargeDoor.cs:48-56, read at BlockEntityChargeDoor.cs:34-37); BlockEntityFurnacePart.PoseOneOf(string?, params string[]) (:119) and Pose(Action<BlockEntityAnimationUtil>) (:102)

**Produces:** `iwex:tool-rabble` / `iwex:tool-paddle` items; a rabble verb on the hearth gated on the small door; `rabbling` and `paddle` added to the `doorClips` attribute so the block entity can play them

- [x] **Step 1.** Verify what already exists before scoping any art. The runtime shape `assets/iiex/shapes/furnace/puddlingchargedoor.json` already carries all five clips — `closed-main`, `open-main`, `open-small`, `rabbling` (4 keyframes, `onAnimationEnd: Repeat`) and `paddle` (5 keyframes, `EaseOut`) — plus the `Tools` element group with `Rabble` and `Paddle`. `docs/design/processes/puddling.md` § Gotchas says both tool clips are authored `EaseOut` and warns a looped clip must be `Repeat`; that is stale for the runtime asset — `rabbling` is already `Repeat`. `paddle` staying `EaseOut` is correct: a paddle stroke is one-shot.
- [x] **Step 2.** The item art is drawn but not exported: `assets/editable/shapes/item-tool-rabble.json` and `item-tool-paddle.json` exist; `assets/iiex/shapes/item/` holds only casting shapes. Export both with `scripts/tools/convert-shape.py` (the same route every other exported shape took). `item-tool-paddle.json` carries an `IronBall1` group on the paddle head — the paddle is drawn holding a ball, which is the draw-out verb, not the rabbling one.
- [x] **Step 3.** Write the failing test: rabbling with `iwex:tool-rabble` on the hearth is refused while the small door is shut and accepted while it is open; each accepted rabble advances the bath toward balled-up; and rabbling a bath that is not yet molten is refused with its own error code. Use the `SendIngameError(code)`-only convention — codes like `iwex-hearth-cannotrabble`, no free text.
- [x] **Step 4.** **(c) ruled 2026-08-05: one ball per rabble.** 16 gathering gestures per heat, each one producing a ball. **It is the only option under which the `PuddlingProcessTempC = 1400` ruling's own justifying sentence is true in code** — *"which is what makes rabbling a verb the player is actually performing rather than watching"*. "All 16 at once" makes rabbling a progress bar with a wait, which that ruling explicitly argues against, and it thins the one hands-on machine in the whole wrought route. "One per row" would reuse `HearthRows`/`CanReach` as the *production* structure, but **16 does not divide by 3** — the last group is short and the count stops being forced by division — and the bed was worked as one bath; the rows are this mod's own abstraction, not the hearth's. Historically exact: the puddler gathered the stiffening metal into balls **one at a time** with the bar and drew each white-hot ball out **separately** (`puddling.md:52-53`), which is also how `iwex.md:191` already describes the verb and what the drawn `rabbling` Repeat clip is for. Budget **32 gestures per heat** — 16 rabbles plus 16 draw-outs. **(a) is calibration, not a ruling:** space the rabbles by a **cooldown**, not by a count, so the pacing can move without changing the model. **(b) is still open** — whether a rabbling bar is a real item or rabbling is bare-handed against the door; two item shapes are drawn for a thing that does not exist. **And this ruling forces U6.8 Step 6's art question**: 16 invisible gestures on the mod's most hands-on machine is exactly the failure R7 ("nothing is hidden") exists to prevent — see that step.
- [x] **Step 5.** Add `rabbling` and `paddle` to the puddling door's `doorClips` attribute at `BlockChargeDoor.cs:48-56`. The clip names stay data, not code — that is why the attribute exists (`BlockEntityChargeDoor.cs:31-33`).
- [x] **Step 6.** Play the clip from the door's block entity on each rabble, one-shot, and stop it on the last. Route through `Pose`/`PoseOneOf` so the null-animator guard on `ToggleAnimator` still holds — `BlockEntityFurnacePart.cs:101-102`.
- [x] **Step 7.** Define the two tool items in a new `PuddlingToolItemDefinitions.cs` (follow `FettleItemDefinitions.cs` for shape). Add name + description keys in all three locales; `IiexLangCoverageTests` will demand the block/item name keys.
- [x] **Step 8.** Caution: Match the held item on domain and path. The hearth currently matches on `Code.Path` alone (`BlockPuddlingHearth.cs:106`, `:115`), so any mod shipping an item pathed `pig` charges this hearth. Do not extend that defect to the new tools; fix it for the existing two while you are in the method.
- [x] **Step 9.** Re-bless the new item goldens by path fragment.
- [x] **Step 10.** `./scripts/exmod.sh test 1.21`.

#### U6.10 — Draw the balls, clean the bed — and close the fettle loop

**Files**
- Create: `src/IronIndustryExpanded/Items/WroughtBallItemDefinitions.cs`, `assets/iiex/shapes/item/puddled-ironball.json`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingHearth.cs:77-100`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockPuddlingHearth.cs:83-127`, `src/IronIndustryExpanded/Items/FettleItemDefinitions.cs:71-79`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/PuddlingHearthTests.cs`, `test/IronIndustryExpanded.Tests/Items/PuddlingYieldTests.cs`

**Consumes:** BlockEntityPuddlingHearth.ClearBed() (public void, :83 — no caller anywhere, verified); HearthRows.CanReach(Row, bool) (:75); ItemPig.PigUnits (Items/ItemPig.cs:33); FettleItemDefinitions.StockTag = "fettlestock" (:39), TapCinder def (:77-78)

**Produces:** `iwex:puddled-ironball` at 200 u; a draw-out verb on the hearth honouring the centre-first reach rule; a clean-out verb that calls ClearBed and returns spent fettle + tap cinder

- [x] **Step 1.** Write the yield test first, and write it as arithmetic, not as a literal. `docs/design/processes/puddling.md` § Numbers derives the whole yield by integer division: balls = ⌊(9 × pigMass) / ballMass⌋ and cinder = the remainder. At the settled 375 u pig that is 16 balls + 175 u; at the 150 u the code still ships (`ItemPig.cs:33`) it is 6 balls + 150 u. Assert `balls * BallUnits + cinderUnits == 9 * ItemPig.PigUnits` — that stays true through U1's re-mass and catches a mass leak; a hardcoded `Assert.Equal(16, …)` would pass at whatever the constant happens to be.
- [x] **Step 2.** Settle `tapcinder`'s unit value with the user before writing the clean-out. `docs/design/processes/puddling.md` § Open #1 is explicit that at exactly 3 cinder the fettle loop closes perfectly (3 cinder → 3 fettle → the next heat's 3 rows) and at anything else it does not. `iwex:tapcinder` today is a plain `Recovered` item with no `materialUnits` at all (`FettleItemDefinitions.cs:62-70`, `:77-78`).
- [x] **Step 3.** The ball art is drawn and untracked: `assets/editable/shapes/item-puddled-ironball.json` (an `IronBall1` group with seven children). Export it to `assets/iiex/shapes/item/`. Define the item in a new `WroughtBallItemDefinitions.cs` with `materialUnits = 200`, hot-workable, carrying vanilla's `temperature` attribute so it cools in the hand — copy the `combustibleProps`/`temperatureDamage` shape from `BlockStructures/Forming/StockItemDefinitions.cs:46-55`, since the ball goes straight to the hammer and the cooling is the carry cost.
- [x] **Step 4.** Implement the draw-out verb on `BlockPuddlingHearth.HandleInteract` (`:83-127`): empty-handed RMB (or the paddle from U6.9 — the paddle's drawn geometry, a shaft with a ball on the head, argues for the paddle) takes one ball from the clicked row. Route it through `RowAt` so the fillers keep being the interface.
- [x] **Step 5.** Caution: The reach rule must apply on the way out as well as in. `HearthRows.CanReach(row, centreLoaded)` (`HearthRows.cs:75-76`) already says a loaded centre blocks both flanks — so a full hearth is unloaded centre-first, which is the order a puddler actually drew balls. Test it explicitly; it is currently exercised only as a pure function.
- [x] **Step 6.** Give `ClearBed()` its caller. It has none anywhere in `src/` or `test/` — it is the design's answer to where the cinder goes and it has been dead code since it was written. The verb is `docs/design/processes/puddling.md` § Open #2 and is unchosen; the cheap answer is empty-handed RMB on a bed with no balls left. It must return the spent fettle and the tap cinder together, which is the whole 'cleaned, not tapped' decision.
- [x] **Step 7.** Add a HUD line for the heat's state (bath / balls remaining) through `IiexLang` constants, and en/ru/uk keys.
- [x] **Step 8.** Serialize the ball state and clamp on read.
- [x] **Step 9.** `./scripts/exmod.sh test 1.21`.

#### U6.11 — The gate's tail — ball → helve → shingled bar, and a furnace the player can actually build

**Files**
- Create: `src/IronIndustryExpanded/Recipes/Smithing/ShinglingRecipeDefinitions.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Forming/StockItemDefinitions.cs:21-27`, `src/IronIndustryExpanded/Recipes/Grid/FurnaceRecipeDefinitions.cs:16-20`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Definitions/IiexRecipeOutputTests.cs`, `test/IronIndustryExpanded.Tests/Items/PuddlingYieldTests.cs`

**Consumes:** PigRecipeDefinitions (src/IronIndustryExpanded/Recipes/Smithing/PigRecipeDefinitions.cs) — the only existing iwex smithing recipe and the exact template for a helve recipe (ExRecipeDef.Create(domain, "smithing", name).Body(new { ingredient, name, pattern, code, output })); StockItemDefinitions.Units (private static Dictionary<string,int>, :24-27 — bloom 180, slab 400); the ball item from U6.10; iwex:castplate-heavy (Items/CastPartItemDefinitions.cs:33)

**Produces:** One helve smithing recipe: piled wrought balls → wrought stock; stock masses conserved across the step; grid recipes for the four puddling blocks

- [x] **Step 1.** **Scope confirmed 2026-08-05: U6 owns this.** The unit's Gate is *'9 pigs → puddling furnace → rabble → wrought balls → helve → a shingled bar'*, and without the ball item, the helve recipe and conserved stock masses **U6 would ship a furnace whose product nothing can consume** — and U7's entry condition would be false. It is absent from the deliverables table (which stops at U6.4); the table is what is stale, not the task.
- [x] **Step 2.** Mass reconciliation, and keep it minimal so U7's roll-set redesign is not half-done here. `docs/design/processes/shingling.md` § Inputs: 2 balls (400 u) → bar, 6 balls (1200 u) → slab, mass conserved exactly. Today `StockItemDefinitions.cs:25-26` ships `bloom = 180` and `slab = 400`. Change those two numbers to 400 and 1200 and stop there. Do not rename `StockForm.Bloom` to `Bar` — `accepts: ["bloom"]` runs through the roll sets, `HeatingHearthLayout.Stock.ShingledBloom` and U1's `caststock-bloom`; that rename belongs to U7.3/U7.4 with the rest of the form table.
- [x] **Step 3.** Write the failing test: `2 × BallUnits == Units["bloom"]` and `6 × BallUnits == Units["slab"]`. Mass conservation across shingling is R6 and it is the reason these numbers are not free.
- [x] **Step 4.** **Ruled 2026-08-05: voxel accumulation — pile the balls, the way a player stacks ingots for a vanilla plate.** `PigRecipeDefinitions` is a **1:1** anvil route that accumulates nothing — `shingling.md` § Open 4 says so outright — so copying it verbatim *cannot express this*. The loop is that page's § The loop step 2: **RMB one ball at a time onto the anvil** (200 u, then 400 u), then the shingling blow yields one **400 u** bar with mass conserved exactly. The work item must accept a **second input mid-build**, which has no precedent in the mod — budget for it rather than discovering it halfway. The recipe itself goes in a new `ShinglingRecipeDefinitions.cs`, structured like `PigRecipeDefinitions.cs` (voxel pattern via the `.Body` escape hatch — there is no smithing builder) but extended for the mid-build input. The helve needs no UI and no mode switch because it has exactly one recipe: whatever is piled, a bar is the only thing it can make. That absence is the mechanic (`shingling.md` § Why it is like this).
- [x] **Step 4b.** Caution: **`iwex:furnace-firebox` has no recipe, and it is a required cell in both reverberatory layouts** *(blocking finding, 2026-08-05)*. Step 5's "four puddling blocks" is a count inherited from a doc written **before the firebox became a block** — the `Firebox` role moved off `@(air|coalpile)` onto `iwex:furnace-firebox`, and the shipped goldens confirm it: `puddlingcore.json` requires one at (-5,1,0) and `heatingcore.json` requires two. Verified: nothing under `src/IronIndustryExpanded/Recipes/` outputs it. **Without this step U6's own Gate — "a player builds a puddling furnace from craftable blocks" — cannot pass**, and the reheat furnace is unbuildable too. Composition is settled in `machines/firebox.md`: cast or wrought iron rods + refractory brick + a diagram; quantities are not. U9.4 Step 3 does exactly this diligence for the coke-oven lid — copy its shape.
- [x] **Step 5.** **Ruled 2026-08-05 — this step owns the whole reverberatory chassis, not just puddling: the four puddling blocks + the firebox (Step 4b) + `heatingcore` + `heatinghearth` + `chargedoor`. Eight, not four.** **The reason is not scheduling, it is a silent failure mode.** Grid-recipe pattern collision is a property of `FurnaceRecipeDefinitions.cs` **as a whole** — its own comment at `:34-38` records that two recipes sharing a pattern with overlapping ingredients collide and **the loser silently never resolves**. The puddling core and the reheat core are *the same chassis one row apart*. Author them in two different units and the second discovers the clash **after the first has already blessed its golden, with nothing red in between**. Everything else about splitting this (which unit's gate needs it, critical-path length) is schedulable; a silent recipe collision is not. This closes U7's construction gap too — `reheat-furnace.md` § Construction and § Open #6 record it and assign no owner. **`iwex:furnace-chargedoor` is shared by the reheat furnace and the coke oven**, which is why it lands here rather than in either machine's own unit — see the U9.4 correction. `furnace-chargelid` stays with U9.4. Context: `FurnaceRecipeDefinitions.cs:16-20` defines exactly two groups (blast furnace, cupola) and none of the 20-odd grid goldens outputs `puddlingcore`, `puddlinghearth`, `puddlingchargedoor` or `puddlingchimneycap` — the machine is creative-only. The hearth is the one that consumes `iwex:castplate-heavy`, which is why U1 is this unit's entry condition. Unlike the blast furnace nothing here needs a pipe, so B1 does not gate it. **Not RCC, and the docs close it:** `puddling-furnace.md` § Construction — *"There are **no** RCC construction stages either — every part is a plain placed block"* — and iwex ships no RCC catalogue entries at all. The multiblock layout already makes the player place 76 refractory bricks and 10 slabs one at a time with a projection shopping list, so the brick-by-brick fiction is delivered by the **layout**, not by RCC; the grid recipe makes only the iron fittings and the anchor core, which is exactly the shop-made part a founder would have bought in.
- [x] **Step 6.** Watch for recipe conflicts: grid recipes clash on the same pattern with overlapping ingredients, and the output must use the creative-inventory default variant (`*-n` for every furnace part here).
- [x] **Step 7.** Re-bless the new recipe goldens by path fragment; read each diff.
- [x] **Step 8.** `./scripts/exmod.sh test 1.21`, then walk the gate by hand in game.

### Traps — each of these makes a green suite a lie

- StructureRig.Raise() synthesises a stand-in for every empty footprint cell including exlib:structurefiller (StructureRig.cs:217-234). So any scenario-level puddling test completes a furnace no player can build, and U6.1's whole subject would be invisible. The filler-accounting test must be at definition level, comparing declared filler offsets against the parts' emitted fillerOffsets.
- BlockEntityPuddlingHearth, BlockEntityChargeDoor and BlockEntityPuddlingChimneyCap have zero test coverage today. The fettle-before-pig rule, the centre-blocks-flanks reach rule as the block entity applies it, the _pigs tree clamp and both door toggles are live mechanics a refactor could delete with a fully green suite. Pin the existing behaviour before adding to it.
- ClearBed(), IsVenting and IsOpen have no readers anywhere including tests. Adding a reader without a test that drives it through the real player verb leaves all three exactly as re-deletable as they are now — and ClearBed is the only thing closing the fettle loop.
- LangCoverage checks only that block codes resolve to block-* name keys; it never inspects Lang.Get call sites. Two puddling HUD keys already render raw in game and no test notices. Migrating to the generated IiexLang constants is the only structural fix — and until it happens, every HUD line U6 adds can ship broken and green.
- ExShapeElements.Pruned / SelectiveElements drop an unknown element name without raising anything, and a name that exists but belongs to the wrong cell draws the charge one row over. Any element the melt or ball state names must be pinned against the shipped shape the way FurnacePartsTests.cs:61 and :105 pin the pigs and the deliberately out-of-order Fettle cubes.
- If the hearth registers its own game tick instead of being driven from SmeltCycle, away-catch-up silently stops applying to the melt. It looks identical while the chunk is loaded — which is every headless test — and teleports the heat on reload.
- The counted stack walk must be cached and invalidated on neighbour change. A per-tick BlockAccessor walk up a 30-block column costs nothing in a headless test and is a frame-rate bug in play; no test will ever see it.
- Once the chimney is player-built, the cap must not resolve its core through MultiblockAnchorLink: ComponentScanBelow = 8 silently caps the chimney at eight courses forever, and the failure mode is a cap that simply reports 'not part of a furnace' with no error.
- HeatBalanceTests pins T_process with literal [InlineData] values. Changing charge loss, transfer loss or the draught term will fail those rows loudly — good — but 'fixing' them by pasting the number the run produced is how the calibration silently moves. Re-derive every asserted temperature by hand from the changed formula.
- Pig ships at 150 u against the settled 375 (ItemPig.cs:33). Every U6 yield number is stated at 375. A test asserting a literal 16 balls will pass at whatever the constant happens to be; assert balls*BallUnits + cinderUnits == 9*ItemPig.PigUnits instead, so the invariant survives U1's re-mass.
- EXLIB_WRITE_GOLDENS=1 re-blesses the whole domain unread, which accepts every unrelated drift as the new record. Always pass path fragments (e.g. iwex/blocktypes/furnace/puddlinghearth) and read each diff by hand.
- Ruling 2's 'FurnaceState dissolves' is a shaft-branch consequence (burning is per column, governed by raceway coke). A firebox has no columns, so the firebox branch keeps an explicit FSM. Do not delete timers or thresholds on BlockEntityFireboxFurnace in sympathy with U3 — and do not let U6's DisruptionMixFloor override be read as blessing the shaft's floor.
- ./scripts/exmod.sh test latest / 1.22 reports ~20 upstream IPlayer-mocking failures from VS 1.22.6 that are not real. Use 1.21.

**Gate.** A player builds a puddling furnace from craftable blocks, fettles three rows, charges nine pigs, loads the firebox with coke, shuts the big door and sets the damper — and the furnace lights, holds (it no longer extinguishes at 30 s), reaches its process temperature at a three-course stack, melts the charge, is rabbled through the small door, yields wrought balls drawn centre-row-first, and cleans out to spent fettle plus tap cinder that grid-craft straight back into the next heat's fettling. Two balls under the helve make a shingled bar, and mass balances end to end: 9 × PigUnits in equals balls × 200 plus the cinder remainder. Verifiable without a game: the filler-accounting invariant passes for all four furnace layouts in both directions; the firebox-branch guard proves no hearth carries a disruption floor above its own capacity; StackDraughtTests pins a curve that rises, peaks and declines; a StructureRig-raised puddling furnace completes itself with nothing forced, resolves its own hearth at all four facings, and runs a full heat on the clock; the ball/bar mass conservation test passes; and `./scripts/exmod.sh test 1.21` is green across all three suites.

---

# U7 — Reheat and rolling: heat-into-stock, the two-round WorkPiece rewrite, roll sets, rolled products

⛔ **Five of these tasks were done outside this plan, under the forming-line ruling of 2026-08-12** (see
[NEXT.md](NEXT.md) and the worklog). Read this before picking any U7 task up:

| Task | State |
|---|---|
| **U7.6** (B3 - the fallback line) | **done** - `WorkPiece.FromStack` reads the itemtype attribute when the stack carries nothing |
| **U7.7** (B17 - the whole deck row) | **done** - `IsInputDeck` walks `DeckRow`. ⛔ the `ns` gap-band sign inversion it names is **not** verified fixed; check `AlongBarrel` before assuming |
| **U7.2** (the two-round `WorkPiece`) | **done 2026-08-12** - one `Thickness`, the `Gap` it is half way through, a flag per side; `Length`/`LengthAt` landed with it. **`Mass` did not** |
| **U7.5** (`Outputs`/`OutputAt` off `RollSetSpec`) | **done** - the states are the stock's stage ladder and the set names no product; the *shear's* half of it is still unbuilt |
| the `bloom` → `bar` rename this unit defers to U7.3/U7.4 | **done 2026-08-12** - `shingledbar` / `shingledslab`, with the masses, `FormerNames` and an item-code migration |

What is genuinely left in U7: **U7.1** (the reheat cycle - `SmeltCycle` is still an empty body), the
**section law** (U7.3's real content), the **roll-set schedule** re-cut, **U7.4**'s product items, **U7.9**'s
48-voxel refusal (a declared stage property since the 2026-08-13 ruling, not `WorkPiece.Mass`), and U7.10.

U7 turns a reduction simulator that nothing can enter and nothing can leave into a working forming line. Four things are genuinely missing and one is genuinely wrong: the reheat furnace puts no heat into stock (`SmeltCycle` is an empty body at `BlockEntityHeatingFurnace.cs:54`); `WorkPiece` still carries the retired per-strip `Strips[]`/`Turned[]` model instead of one `Thickness` + a per-side fed-this-round flag; the four shipped roll sets encode a schedule the 2026-07-29 decision replaced (and one of them, `slitting`, accepts a form that does not exist); and no rolled product item exists at all. The wrong thing is the spread model: `RollingPass.SpreadWidth`'s per-form exponent cannot reproduce the drawn stage art, which encodes the **section law** (flat holds length and puts everything into width; square keeps w=t and puts everything into length) — I verified that law reproduces every drawn stage element to within the drawing's own rounding, and the shipped exponent model does not. Two tiny unblockers (B3's one-line attribute fallback, B17's deck row) come first because nothing downstream is reachable without them, and B17's neighbourhood hides a second, untested bug: `BlockRollingMill.AlongBarrel` uses `localX = dz` for the `ns` orientation where `RotateOffset(…, 90)` demands `-dz`, so on a north-south mill the gap bands run backwards along the barrel.

**Entry condition.** U6 complete: the puddling furnace + helve produce the wrought feed the mill eats, and that feed is an item whose code `HeatingHearthLayout.StockOf` (`HeatingHearthLayout.cs:64-79`) recognises — today that whitelist is the literal prefixes `stock-bloom`, `stock-slab`, `castbillet`, `castbloom`, `castslab`. U1 complete (done: `CastStockItemDefinitions` ships `caststock-{billet,bloom,slab}` at 600/1000/3000 u; `PatternItemDefinitions.Molds` carries the three `longcell` patterns). U7 must not run concurrently with U2/U3/U6: U7.1 edits `BlockEntityHeatingFurnace` and reads `BlockEntityFurnaceCore`'s melt cadence, and all three of those units rewrite that cadence (ruling 2 makes `BfMeltIntervalSec` / `BfMeltStartDelay` / `BfMaxFuelBurnTime` emergent — the same three keys `BlockEntityFireboxFurnace.cs:197-199` binds the reheat furnace to).

**Shared files** (collision risk): `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityHeatingFurnace.cs — U7.1 fills SmeltCycle; U2/U3 rewrite the cadence that calls it`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs — U7.1 needs a hearth handle at OnStructureCompleted and possibly a public InternalTempC; U2, U3, U5 and U6 all edit this file (the plan already warns U6 must not overlap U2/U3 for the same reason)`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityHeatingHearth.cs — U7.1 adds the soak; U6 shares HeatingHearthLayout.Rows through BlockEntityPuddlingHearth`, `src/IronIndustryExpanded/IiexConfig.cs — U7.1 adds ReheatRateK and edits the Rolling region (:576-636); U2/U3 delete or re-derive the Bf* keys (:237,:280,:283,:286); U5/U6 touch the burden and draught regions`, `src/IronIndustryExpanded/BlockStructures/Casting/PatternItemDefinitions.cs — U7.10 adds the `rollers` pattern; U1.3 just added the three longcell patterns and U9's crucible work touches it again`, `src/IronIndustryExpanded/Recipes/Grid/FormingRecipeDefinitions.cs — U7.10 adds roll-set recipes; U8 adds shear / nail machine / rivet machine recipes to the same forming provider`, `assets/iiex/lang/{en,ru,uk}.json — U7.3 deletes two set rows, U7.4 adds four item rows, U7.9 adds one ingameerror row; every other unit adds rows to the same three files, and no test catches a lost row for item codes`, `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/ — U7.3/U7.5 re-bless rollset.json, U7.6 re-blesses caststock.json, U7.4 adds four files; a domain-wide EXLIB_WRITE_GOLDENS=1 from any unit would absorb the others' pending changes`, `scripts/tools/convert-shape.py — U7.4 extends it with element extraction; every unit that exports drawn art (U6's paddle/rabble, U8's three machines) calls it`, `src/IronIndustryExpanded/BlockStructures/Forming/StockForm.cs — U7.2 and U7.3 both rewrite it, and smex's cast forms and lpex's wide forms are settled to extend the same table`

### Tasks

#### U7.6 — B3 — a fresh stock item off the shelf is a WorkPiece (one fallback line, plus the cast forms)

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Forming/WorkPiece.cs:162-180`, `src/IronIndustryExpanded/Items/CastStockItemDefinitions.cs:79-88`, `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/caststock.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/WorkPieceTests.cs`

**Consumes:** public static WorkPiece? FromStack(ItemStack? stack) — WorkPiece.cs:162; public ExItemDef Attribute(string key, object value) — ExpandedLib/Definitions/ExItemDef.cs:207 (writes attributes.{key} on the item def, i.e. stack.Collectible.Attributes)

**Produces:** FromStack falls back to stack.Collectible.Attributes["stockForm"] when the per-stack tree carries none; caststock-{form} declares stockForm alongside materialUnits

- [ ] **Step 0.** Caution: **First — repair `HeatingHearthLayout.StockOf`'s whitelist, or U7's entry condition is a lie and U7.1 has nothing to soak.** *(new 2026-08-05 — a GAP finding with no owner; it is a stale rename, not a design question.)* `StockOf` (`HeatingHearthLayout.cs:64-79`) still tests the pre-rename prefixes `castbillet` / `castbloom` / `castslab`, but U1 shipped the items as **`caststock-{billet,bloom,slab}`** on 2026-08-04. **So no cast stock can enter the reheat hearth at all — and `caststock-*` is the only stock item the game can currently produce**, since nothing makes wrought stock until U6. `FurnacePartsTests.cs:158-160` pins those same non-existent codes, which is why **nothing is red**. Match on `caststock-` and demultiplex on the form suffix; keep the five-member `Stock` enum and all 15 art groups. Re-point the three `InlineData` rows to the shipped codes. **Reheating is not rolling** — a reverberatory hearth holds anything that fits the bed, which is exactly why the billet's 48-voxel crosswise seating anchors the recoverability invariant.
- [ ] **Step 1.** Add a failing test in WorkPieceTests.cs region 'Stack round-trip': build an ItemStack over an item whose Collectible.Attributes carries {"stockForm":"bloom"} and no stack-level attributes, assert WorkPiece.FromStack returns a Fresh piece. Use TestWorld.RegisterItem plus ExpandedLib.Testing helpers (TestBlocks.Configure / ReflectionHelpers) to set CollectibleObject.Attributes, the way the casting tests configure collectible attributes.
- [ ] **Step 2.** In WorkPiece.FromStack (WorkPiece.cs:162-180) read the form name as: stack?.Attributes?.GetString(FormKey) ?? stack?.Collectible?.Attributes?[FormKey]?.AsString(null). Keep the existing early-out shape: a null/unknown name still returns null, so 'An_unknown_form_is_refused_rather_than_guessed' (WorkPieceTests.cs:138) still passes.
- [ ] **Step 3.** Guard the strips read so a stack with a collectible-level form but no per-stack tree returns Fresh(form) rather than throwing on a null tree.
- [ ] **Step 4.** In CastStockItemDefinitions.Definitions (CastStockItemDefinitions.cs:79-88) extend the byType entry to `new { materialUnits = units, stockForm = form }` so cast stock declares its form. **Strike the clause "and must match `StockForm.All` keys after U7.3" — it is false and actively dangerous** *(corrected 2026-08-05)*. **`billet` is not an iwex `StockForm`, and neither are cast bloom or cast slab**: `iwex.md:1120` gives iwex build item 6 as the **wrought two** (`shingledbar`, `shingledslab`) plus the rod, and assigns the three cast forms to **smex build item 22**. The reason is physical and the design states it outright — `iwex.md:1266`: **cast iron cannot be rolled**; it shatters, the mill is a hot-*wrought* mill, and the rolled cast ladder is **steel**, behind the converter. Obeying the struck clause would alias **`caststock-bloom` (1000 u, 4×4×25 cast)** onto the wrought **`bloom` (400 u, 3×3×18)** — the same key naming two different pieces, with U7.3's rename being the only thing that removes the collision. So U7.3 Step 5's `accepts ["bloom","rod"]` is already correct and needs no change, and U7.5's `castbillet` crop rows stay as **forward-declared data** the shear consumes the day smex lands the form.
- [ ] **Step 5.** Re-bless exactly one golden: EXLIB_WRITE_GOLDENS=iwex/itemtypes/caststock ./scripts/exmod.sh test 1.21 — never =1.
- [ ] **Step 6.** Re-run ./scripts/exmod.sh test 1.21 clean.

#### U7.7 — B17 — the whole deck row is an input, and fix the ns gap-band sign inversion it was hiding

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Forming/BlockEntities/BlockEntityRollingMill.cs:244-261`, `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockRollingMill.cs:368-375`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/RollingMillFeedTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/MillFeedTests.cs`

**Consumes:** public BlockPos InputDeck / OutputDeck — BlockEntityRollingMill.cs:244,:247; private BlockPos Deck(bool far) — :250-257; public bool IsInputDeck(BlockPos cell) — :260; public const int DeckCells = 3 / DeckOriginOffset = 2 — MillFeed.cs:78,:81; public static Vec3i RotateOffset(int x,int y,int z,int angle) — ExpandedLib/Helpers/ExOrientation.cs:36 (90 => (z,-x))

**Produces:** IEnumerable<BlockPos> InputDeckCells / OutputDeckCells; IsInputDeck becomes a membership test over the three-cell row; InputDeck/OutputDeck keep meaning 'the centre cell' for ejection; BlockRollingMill.AlongBarrel is orientation-correct

- [ ] **Step 1.** Write the failing deck test in RollingMillFeedTests.cs: for a `we` mill at origin, assert mill.IsInputDeck is true for all three cells of the input row — ExOrientation.GlobalPos(pos, x, 0, ±1, angle) for x in {-2,-1,0} — and false for every cell of the output row. Today only x=0 passes.
- [ ] **Step 2.** Write the failing orientation test: place an `ns` mill (the Mill("ns") overload already exists at RollingMillFeedTests.cs:25), then assert that a click on the deck cell whose local x is -2 maps to gap zone 0 (the widest gap) and the cell at local x=0 maps to the last zone. Drive it through BlockRollingMill's own path — expose or reflect into the private AlongBarrel(BlockPos, BlockPos, BlockSelection) at BlockRollingMill.cs:368 — because MillFeed.AlongBarrel alone cannot see the bug.
- [ ] **Step 3.** Change Deck(bool far) to return the row: ExOrientation.GlobalPos(Pos, x, 0, far ? 1 : -1, StructureAngle) for x = -2,-1,0 (the layout at BlockRollingMill.cs:74-94 authors Origin(-2,-1) and marks all three cells of both z=±1 rows '-'). Keep a single-cell Deck for the ejection point (x=0).
- [ ] **Step 4.** Make IsInputDeck(cell) => InputDeckCells.Any(c => c.Equals(cell)). Leave EjectPiece (BlockEntityRollingMill.cs:290-303) spawning on the centre cell.
- [ ] **Step 5.** Fix BlockRollingMill.AlongBarrel (:373): for StructureAngle == 90 the inverse of RotateOffset's (z,-x) is localX = -dz, not dz. Write `double localX = StructureAngle == 90 ? -dz : dx;`.
- [ ] **Step 6.** Verify GetFillerInteractionHelp (BlockRollingMill.cs:393-427) now offers help on all three input cells, since it gates on mill.IsInputDeck(clickedCell).
- [ ] **Step 7.** ./scripts/exmod.sh test 1.21.

#### U7.2 — Rewrite WorkPiece to the settled two-round model, add Length/Mass, and delete the lopsided-mesh machinery

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Forming/WorkPiece.cs`, `src/IronIndustryExpanded/BlockStructures/Forming/StockMesh.cs`, `src/IronIndustryExpanded/BlockStructures/Forming/Items/ItemStockPiece.cs:26-68`, `src/IronIndustryExpanded/BlockStructures/Forming/MillFeed.cs:95-128`, `src/IronIndustryExpanded/BlockStructures/Forming/BlockEntities/BlockEntityRollingMill.cs:167-211`, `src/IronIndustryExpanded/BlockStructures/Forming/BlockEntities/BlockEntityRollingMill.cs:282-288`, `src/IronIndustryExpanded/BlockStructures/Forming/StockItemDefinitions.cs:41`, `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockRollingMill.cs:336-339`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/WorkPieceTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/StockMeshTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/RollingMillFeedTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/MillFeedTests.cs`

**Consumes:** public sealed record WorkPiece(StockForm Form, float[] Strips, bool[] Turned) — WorkPiece.cs:35; WithStrip/IsTurned/Fed — :126,:137,:145; Resplit — :67; StripWidth/StripLength — :95,:115; Thickest/Thinnest/IsEven — :81,:84,:88; SidesFor/PassesForGap — :49,:53; StockMesh.SideOf/CacheKey — StockMesh.cs:36,:67; MillFeed.Decide(set,piece,gapIndex,strip,tempC,rollRadius,rollingTempC) — MillFeed.cs:95

**Produces:** public sealed record WorkPiece(StockForm Form, float Thickness, bool[] FedThisRound) with float Width, float Length, int Mass, int Sides(float barrelWidth), bool RoundComplete, WorkPiece Fed(int side, float target), WorkPiece EnterBarrel(float barrelWidth), static WorkPiece Fresh(StockForm), FromStack/ToStack; plus static string StageKey(float t) => ((int)MathF.Round(t*100)).ToString()

- [ ] **Step 1.** Read the settled model before touching anything: docs/design/processes/rolling.md § 'Feed arithmetic' and the settled forming-line design. A gap costs two rounds; a round is one feed per side; sides = ceil(entryWidth / barrelWidth) computed on entry width and reset when the barrel changes; round 1 lands the half-step (2.0 → 1.75) and round 2 lands the gap (1.75 → 1.5). The whole piece sits on the half-step after round 1 — there is no lopsided state and no per-side thickness.
- [ ] **Step 2.** Rewrite WorkPieceTests.cs region by region. Delete the 'Strips reconstruct the whole piece' region (:32-100) and replace with: a fresh piece is at Form.BaseThickness with no side fed; feeding side 0 of a 1-sided piece at gap 1.5 from 2.0 lands 1.75 and completes the round; feeding again lands 1.5; on a 2-sided piece round 1 is not complete until both sides are fed; changing barrel width resets FedThisRound; the stack round-trip carries Thickness plus the flag array.
- [ ] **Step 3.** Replace the record with `public sealed record WorkPiece(StockForm Form, float Thickness, bool[] FedThisRound)`. Tree keys: keep "stockForm", replace "stripThickness"/"stripTurned" with "stockThickness" (float) and "stockFed" (BoolArrayAttribute). Old saves carrying the array keys read as a fresh piece — acceptable (nothing has ever shipped survival stock) but state it in the doc comment.
- [ ] **Step 4.** Add Width/Length/Mass. Width and Length come from the section law, which needs the roll set's section class from U7.3 — for this task compute them from StockForm alone and let U7.3 move the law behind a section parameter. Mass is the item def's materialUnits read off the collectible, not re-derived from voxels (ruling 1: the density rule is a sizing guide, mass is declared).
- [ ] **Step 5.** Delete StockMesh.cs and StockMeshTests.cs entirely — SidePlacement, SideOf and CacheKey exist only to draw the lopsided piece the settled model deletes. Do not leave a stub.
- [ ] **Step 6.** Rewrite ItemStockPiece.OnBeforeRender (ItemStockPiece.cs:26-48) to select the stage shape instead of composing one: load the family shape, prune to the current stage's element with ExpandedLib.Helpers.ExShapeElements.Pruned(shape, [element]) (ExShapeElements.cs:59 — the same call BlockEntityHeatingHearth.OnTesselation uses at BlockEntityHeatingHearth.cs:116-119), tesselate, cache by StageKey. Keep the OnUnloaded dispose loop.
- [ ] **Step 7.** Change the stage-shape key from tenths to hundredths (ruling 4): StockItemDefinitions.cs:41 emits `iwex:forming/stock-{name}-{(int)(BaseThickness*10)}` → use StageKey, giving stock-bloom-300. Update RolledStockStagesTests.StagePath (RolledStockStagesTests.cs:29) to match. The drawn art already uses this convention (Grooved275, Flattened125, RolledRod200).
- [ ] **Step 8.** Simplify the mill's feed path: TryFeed (BlockEntityRollingMill.cs:167-211) calls piece.Resplit(...) and writes it back before MillFeed.Decide runs (:178-183), so a refused offer mutates the piece. Replace with EnterBarrel(barrelWidth) applied only after the decision is accepted, and add a test that a refused feed leaves the stack byte-identical.
- [ ] **Step 9.** Update CompletePass (:282-288) to piece.Fed(_pendingStrip, _pendingTarget) where _pendingTarget is the half-step on round 1 and the gap on round 2; if you rename the persisted keys rmPendingGap/rmPendingStrip, update FromTreeAttributes (:436-437) in the same edit.
- [ ] **Step 10.** Update BlockRollingMill.Feed's sides computation (:336-339) to WorkPiece.Sides(barrelWidth).
- [ ] **Step 11.** ./scripts/exmod.sh test 1.21.

#### U7.3 — Redesign the roll sets to the settled schedules and build the section law the drawn art already encodes

**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Forming/SectionClass.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/SectionLawTests.cs`, `scripts/export-stock-stages.py`
- Modify: `src/IronIndustryExpanded/BlockStructures/Forming/RollSetItemDefinitions.cs:56-108`, `src/IronIndustryExpanded/BlockStructures/Forming/RollSetSpec.cs:31-38`, `src/IronIndustryExpanded/BlockStructures/Forming/RollSetSpec.cs:108-201`, `src/IronIndustryExpanded/BlockStructures/Forming/StockForm.cs:28-64`, `src/IronIndustryExpanded/BlockStructures/Forming/RollingPass.cs:146-176`, `assets/iiex/lang/en.json:118-121`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/rollset.json`, `scripts/tools/generate-rolled-stock.py`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/SectionLawTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/RollSetSpecTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/RolledStockStagesTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/MillFeedTests.cs`

**Consumes:** public sealed record RollSetSpec(string Family, string[] Accepts, float[] Gaps, IReadOnlyDictionary<float,string> Outputs, float BarrelWidth, float MinTorque) — RollSetSpec.cs:31; TryParse — :108; public sealed record StockForm(string Name, float BaseWidth, float BaseThickness, float MaxWidth, float BaseLength, float SpreadExponent) — StockForm.cs:28; RollingPass.SpreadWidth — :146; RollingPass.LengthMultiplier — :168; private static object Set(string family, string[] accepts, double[] gaps, object[] outputs, double barrelWidth, double minTorque) — RollSetItemDefinitions.cs:25

**Produces:** RollSetSpec gains `SectionClass Section` parsed from a required `section` key ("flat" | "square"); StockForm loses SpreadExponent and gains float Volume; two pure statics SectionLaw.WidthAt(form, section, thickness) and SectionLaw.LengthAt(form, section, thickness); the shipped set list becomes exactly {flat, grooved}

- [ ] **Step 1.** Measure the art first — it is the oracle. assets/editable/shapes/item-shingled-bar.json: ShingledBar1 3×3×9 with a 9-long child (18 long, V=162), Grooved275 2.75²×11(×2=22), Grooved250 2.5²×13(×2=26), Grooved225 2.25²×16(×2=32), CutRod1..4 2×2×10, Flattened275 3.25×2.75×9, Flattened250 3.6×2.5×9, Flattened225 4.0×2.25×9, Beam 4.5×2×9. item-rolled-beam.json continues the flat branch: Flattened175 5.1×1.75×9, Flattened150 6.0×1.5×9, Flattened125 7.2×1.25×9, CutPlate1/2 9×1×9. item-rolled-rod.json is the rod's own schedule off RolledRod200 2×2×10 (V=40): Grooved175 1.75²×13, Grooved150 1.5²×9(×2=18), Grooved125 1.25²×13(×2=26), CutRivetRod1..4 1×1×10, Flattened175 2.3×1.75×10, Flattened150 2.6×1.5×10, Flattened125 3.2×1.25×10, NailPlate 4×1×10.
- [ ] **Step 2.** Write SectionLawTests.cs before the implementation, as a [Theory] over every measured stage: flat => w(t)=V/(L0·t) with L held at L0; square => w=t and L(t)=V/t². Both reproduce the art to within 0.05 vx (verified: 162/(18·2.75)=3.27 vs drawn 3.25; 162/2.25²=32.0 vs drawn 32; 40/(10·1.5)=2.67 vs drawn 2.6). Also assert the shipped exponent model fails the same table, so the test proves the change is necessary rather than cosmetic.
- [ ] **Step 3.** Add SectionClass.cs: `public enum SectionClass { Flat, Square }` plus a static SectionLaw with WidthAt/LengthAt. Delete StockForm.SpreadExponent and add `float Volume` (BaseWidth·BaseThickness·BaseLength for the drawn base). **Do not assume "no narrow schedule reaches `MaxWidth`, so U7 does not have to settle rolling.md Open #1" — it is false, and the load-bearing kind of false.** The shipped cap is **8** (`StockForm.cs:48`, `Bloom = new("bloom", 3f, 3f, 8f, 16f, …)`) and the bar's terminal flat stage is **9.0 × 1.0** — so today it **silently clamps to 8.0** and **U7's own gate** ("reproduces `CutPlate` 9 × 1 × 18") fails. **Ruled 2026-08-05: `MaxWidth` lives in both places, and the effective cap is `min(roll-set barrel, stock-form cap)`.** It is the only answer satisfying all three demands `rolling.md:418` names at once: the **barrel becomes physical** (so `wide-hall.md` and iwex build item 4 get their way — a billet has no intrinsic maximum width, the rolls do), the **bar carries a form cap of 9** so its plate stage is legal, and **`skelp` stays capped at 8** — which is not optional, because skelp is the only feed for the conical roller → rolled pipe, and that is hpex's 12 atm requirement. A roll-set-only cap kills skelp outright; a form-only cap makes the barrel decorative. `RollSetSpec` gains the barrel-width field and every shipped roll-set golden re-blesses. **`min()` does not close the 50-voxel hole** — `min(15, 8)` is still 8, so castbloom's 1.0 stage stays 50 long, past U7.9's 48-voxel refusal. It needs a **declared mandatory crop mid-gap**, which is machinery **U7.5's crop table already has to express** for the billet's 2.25 half-step — so it is a new row, not a new mechanism. Sanity note for whoever implements it: the narrow flat schedule already rolls a **9-wide piece on a 4-wide barrel** by taking it a side at a time (`rolling.md:214`), so "barrel = max width" is not literally how the mod's own law works — the barrel caps a *single pass's* bite, which is why `min()` and not "the barrel wins".
- [ ] **Step 4.** Correct the StockForms to the drawn art and add the rod: Bloom becomes the shingled bar at 3 × 3 × 18 (BaseLength is 16 today — StockForm.cs:48), Slab stays 8×3×20 (:54), and add Rod = 2×2×10 — which is what makes the fork work: a rolledrod re-enters the same flat/grooved sets at their 1.5 and 1.0 gaps.
- [ ] **Step 4b.** **Ruled 2026-08-05: the rod re-enters the mill, and rod-sized stock also heats in a vanilla furnace.** Add a `Rod` member to `HeatingHearthLayout.Stock` plus a `stockForm` attribute on the rod item; a stand-in element on the hearth art is acceptable until it is drawn. **Small stock must be heatable in vanilla's own firepit/forge, not only in the reheat hearth** — the reheat furnace is for *big* stock, and gating a rod behind a megablock puts the early rivet route out of reach. **Ruled 2026-08-05 — split by role, and the finished product is vanilla's rod.** `iwex:rolledrod` **is not created.** The shear's claimed product is **`game:rod-iron`**; the re-rollable work piece is the auto-emitted **`iwex:stock-rod`**. Two objects with two jobs, not one object with two contradictory ones — today's text asks a single 100 u item to be both a `MaxStackSize 1` stage-rendered work piece *and* a spendable part **32 recipe sites already ask for by the code `game:rod-*`**. **The precedent is one row above it in the same table**: `rolled-parts.md:248-249` settles the sibling product toward vanilla — *"`game:metalplate` is a **vanilla** item and stays one. Nothing here re-skins it - the mill route simply reaches the same object the anvil does, at the same 200 u, **which is what makes the two routes comparable**."* The rod's case is *stronger*, because vanilla's rod is `2 × 2 × 10 = 100 u` **exactly** rather than approximately (`fasteners.md:83`), and because `game:rod-iron` is already the mill's only output code that resolves (`RollSetItemDefinitions.cs:90`). **Historically it is the same fact:** Cort's grooved rolls and a hammerman's bar both produced *merchant bar* — rolling changed the **labour**, not the product. Two items for hand-forged and rolled rod is the anachronism. **What this task must now do:** add the `Units["rod"] = 100` row this step currently omits — that missing key is what makes `StockItemDefinitions` throw, since it enumerates `StockForm.All` against the unit table — and give the mill a **code→StockForm admission for `game:rod-iron`**, so a vanilla anvil rod converts to `iwex:stock-rod` on entry. That admission is what makes "drop-in" *real* without touching `ExIngredients.cs:45`, `ConstructionStages.cs:115`'s `storeWildCard`, or smex's hardcoded literal at `BlockEntityConverterControl.cs:1059` — and it hands the mill a genuine early-game on-ramp: **an anvil-made rod can be rolled to nail plate before you own a puddling furnace.** The drawn `item-rolled-rod.json` is a *rolling-stage* shape and belongs to `iwex:stock-rod`, so the art needs no change.
- [ ] **Step 5.** Rewrite RollSetItemDefinitions.Sets (:56-104) to exactly two sets. flat: family "flat", section "flat", accepts ["bloom","rod"], gaps [2.5,2.0,1.5,1.0], barrelWidth 4.0 (the drawn barrel segments in item-finished-rollers-flat.json are 4 voxels each), minTorque 0.2. grooved: family "grooved", section "square", accepts ["bloom","rod"], gaps [2.5,2.0,1.5,1.0], barrelWidth 16.0 (a never-overhangs sentinel — the groove constrains spread), minTorque 0.3. Delete slitting outright (accepts ["plate"], no such StockForm — RollSetItemDefinitions.cs:96-103) and delete flatwide from iwex (wide sets are lpex's six single-gap items per docs/design/machines/wide-hall.md). Keep authoring gaps as double[], not float[]: RollSetItemDefinitions.cs:22-24 explains why (a widened float leaks binary error into the golden).
- [ ] **Step 6.** This fixes B4 as a side effect: the old grooved set opened at a 1.0 gap against 3.0 stock — a 2.0 draft against δ_max = μ²R = 1.0 — so it could never bite. With a 2.5 first gap the draft is 0.5 and each round's draft is 0.25.
- [ ] **Step 7.** Add `section` to the RollSetSpec record (:31-38) and to TryParse (:108-201) as a required field with a human-readable error, matching the existing validation idiom. Update RollSetSpecTests.cs's parsing region accordingly.
- [ ] **Step 8.** Replace stage generation with stage export. scripts/tools/generate-rolled-stock.py is stale twice over: its forms table names item-shingledbloom / item-shingledslab, both of which git reports as ' D' (renamed to item-shingled-bar.json / item-shingled-slab.json), and it derives stages from the exponent model. Since the stages are now drawn (ruling 1: no redraws), write scripts/export-stock-stages.py to lift each named element out of the editable family file, remap the texture the way scripts/tools/convert-shape.py's TEXTURES table does, and emit assets/iiex/shapes/forming/stock-{form}-{key}.json for keys 300/275/250/225/200/175/150/125/100. Delete the ten existing generated files — stock-bloom-30 measures 3×3×16 and stock-bloom-10 is 7.6 wide against the settled 9.0, so every one is wrong.
- [ ] **Step 9.** Rewrite RolledStockStagesTests.cs against the section law and the new key: Gaps becomes [3.0,2.75,2.5,2.25,2.0,1.75,1.5,1.25,1.0]; drop the exponent-derived assertions at :72-95 and assert width/length against SectionLaw; drop 'A_bloom_lands_on_plate_geometry_at_the_one_voxel_gap' (:113) or restate it as V/L0 = 9 for this form only (rolling.md Gotcha: the claim at StockForm.cs:44-46 is true of one form and reads as general).
- [ ] **Step 10.** Delete the lang rows for the removed sets in all three locales (en.json:119 flatwide, :121 slitting, plus ru/uk siblings) and fix en.json:110 while you are there — item-stock-slab reads "Cast Slab", which is wrong and duplicates item-caststock-slab (en.json:355).
- [ ] **Step 11.** Re-bless one golden: EXLIB_WRITE_GOLDENS=iwex/itemtypes/rollset ./scripts/exmod.sh test 1.21, then run clean.

#### U7.9 — The 48-voxel refusal — make the recoverability invariant something code enforces, not something a document asserts

**Files**
- Create: `test/IronIndustryExpanded.Tests/Invariants/RecoverabilityTests.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Forming/MillFeed.cs:5-37`, `src/IronIndustryExpanded/BlockStructures/Forming/MillFeed.cs:95-128`, `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockRollingMill.cs:350-359`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Invariants/RecoverabilityTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/MillFeedTests.cs`

**Consumes:** public enum FeedVerdict — MillFeed.cs:5-29; public static FeedDecision Decide(...) — MillFeed.cs:95; WorkPiece.Length (from U7.2); SectionLaw.LengthAt (from U7.3)

**Produces:** FeedVerdict.TooLong; const int WorkPiece.MaxLengthVoxels = 48; MillFeed.Decide refuses any feed whose resulting length would exceed 48; ingameerror key iwex-rollingmill-toolong

- [ ] **Step 1.** Read docs/design/mechanics/recoverability.md § '48 is the mill's refusal; 32 is only a mode switch'. 48 is the hard limit (the longest piece that can legally exist); 32 is the hearth's lengthwise/crosswise seating switch and is not a refusal. Do not implement 32 as a limit.
- [ ] **Step 2.** Write the failing tests in MillFeedTests.cs: a bloom at the 2.0 gap on the grooved set is 40.5 long and must be accepted (legal crosswise); a stage that would land past 48 must return FeedVerdict.TooLong with Draft 0.
- [ ] **Step 3.** Add TooLong to the FeedVerdict enum with a doc comment saying why it exists (a piece past 48 fits no hearth seating and is stranded — the silent soft-lock the invariant forbids), and add the check in Decide after the bite check.
- [ ] **Step 4.** Map the verdict in BlockRollingMill.Feed's switch (:350-359) to "iwex-rollingmill-toolong" and add game:ingameerror-iwex-rollingmill-toolong to all three locale files alongside the five at en.json:113-117.
- [ ] **Step 5.** Write the schedule-walk invariant the design has asked for twice and nobody has built (recoverability.md § Open, rolling.md Open #2): in RecoverabilityTests.cs, for every (StockForm, RollSetSpec) pair the mod ships, walk every gap and every half-step and assert each reachable stage is ≤ 48 or is a declared mandatory crop point. This is the test that mechanically catches the castbloom-at-50 hole recoverability.md flags as its highest-value open item.
- [ ] **Step 6.** ./scripts/exmod.sh test 1.21.

#### U7.5 — Move Outputs/OutputAt off RollSetSpec onto a stage-keyed product table (the shear's input, not the mill's)

> Caution: **collision resolved 2026-08-05 — U7.5 owns the crop table; U8.3 consumes it.**
> Both tasks deleted `RollSetSpec.Outputs`/`OutputAt` and both built a replacement, with **incompatible keys**:
> U7.5's `(Form, SectionClass, stage)` against U8.3's `(Form, thickness)`. Whichever ran second would find its
> delete targets already gone.
>
> **U7.5's key wins because it is strictly more expressive, and its own first two rows prove the difference is
> real:** `shingledbar grooved 2.0 → 4 × rolledrod` and `shingledbar flat 2.0 → 2 × beam` are the same form at
> the same thickness with different answers. A section-blind key collapses them — the plate route and the rod
> route become one crop.
>
> Caution: **U8.3 therefore shrinks to `ShearFeed`** (the torque/feed decision layer) and must **delete its
> `RollSetSpec.cs` Modify targets and its `Out()`-helper deletion steps**; U8.4 Step 6 re-points at
> `StockProducts`. Adopt ruling 4's hundredths **stage key** inside U7.5's record so `2.25` cannot collide
> with `2.2` — that half of U8.3 was right and is the only part worth carrying over.

**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Forming/StockProducts.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/StockProductsTests.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Forming/RollSetSpec.cs:28`, `src/IronIndustryExpanded/BlockStructures/Forming/RollSetSpec.cs:35`, `src/IronIndustryExpanded/BlockStructures/Forming/RollSetSpec.cs:94-101`, `src/IronIndustryExpanded/BlockStructures/Forming/RollSetSpec.cs:159-183`, `src/IronIndustryExpanded/BlockStructures/Forming/RollSetItemDefinitions.cs:25-46`, `src/IronIndustryExpanded/BlockStructures/Forming/RollSetItemDefinitions.cs:56-104`, `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/rollset.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/StockProductsTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/RollSetSpecTests.cs`

**Consumes:** public string? OutputAt(float thickness) — RollSetSpec.cs:95 (compares floats with ==, has no production caller); IReadOnlyDictionary<float,string> Outputs — :35; the outputs array validation — :159-183; private static object Out(double gap, string code) — RollSetItemDefinitions.cs:46

**Produces:** public static class StockProducts with `readonly record struct Crop(string Form, SectionClass Section, int StageKey, string ProductCode, int Count)` and `static Crop? At(string form, SectionClass section, float stage)`, keyed on stage so a half-step is a legal product point. **Corrected 2026-08-05 — `int StageKey`, in hundredths (`(int)MathF.Round(stage * 100)`), matched exactly.** The earlier `float Stage` + `1e-3` tolerance came from this task's pre-collision text; the collision ruling above adopts ruling 4's hundredths key, which is the one half of U8.3 worth carrying over. It is also what keeps the crop key and the **shape-path** key the same encoding — `StockItemDefinitions.cs:41` still emits tenths, `(int)(2.25f * 10) == 22`, which collides 2.25 with 2.2 and is exactly the bug U7.3 Step 8 moves the art keys to hundredths to kill. Two near-identical stage encodings in one line is how that bug comes back.

- [ ] **Step 1.** Caution: Read the conflict before starting. The plan's U7.5 says to wire RollSetSpec.OutputAt so 'rolling a piece to 1.0 yields a plate'. Three design pages settled the opposite on 2026-07-29: docs/design/machines/shear.md § Owns, docs/design/processes/rolling.md § The loop step 8, and docs/design/items/roll-sets.md § Open all state that the mill only ever makes stock and the shear turns a stage into a product. Build the table; do not give the mill a claim gesture.
- [ ] **Step 2.** Write StockProductsTests.cs first from docs/design/items/rolled-parts.md § 'Every crop point, and whether it is exact': shingledbar grooved 2.0 → 4 × rolledrod @100; shingledbar flat 2.0 → 2 × beam @200; shingledbar flat 1.0 → 2 × game:metalplate @200; rolledrod grooved 1.0 → 4 × rod @25; rolledrod flat 1.0 → 1 × nailplate @100 (whole piece, no crop). Assert conservation: sum(product mass × count) equals the stock's declared mass on every row, so no route mints metal.
- [ ] **Step 3.** Add the half-step row that proves the point of keying on stage: castbillet grooved 2.25 → 6 × rolledrod. 2.25 is not one of the barrel's gaps and is therefore unexpressible under the current TryParse rule at RollSetSpec.cs:172-175 ('output gap {gap} is not one of the barrel's gaps').
- [ ] **Step 4.** Create StockProducts.cs as a pure static table. **Corrected 2026-08-05:** key on `int StageKey = (int)MathF.Round(stage * 100)` and match it **exactly**. Do **not** use the `MathF.Abs(a-b) < 1e-3f` tolerance the original text specified — the point is not to avoid float `==` (`OutputAt`'s `:98` comparison), it is to stop storing a float key at all, so 2.25 and 2.2 become 225 and 220 and cannot collide by construction. Convert once at the boundary, in `At(...)`, so callers keep passing a float thickness.
- [ ] **Step 5.** Delete Outputs from the record (:35), delete OutputAt (:94-101), delete the outputs block from TryParse (:159-183) and make `outputs` ignored (or explicitly rejected). Delete the outputs argument from RollSetItemDefinitions.Set (:25-44) and the Out helper (:46). Remove RollSetSpecTests cases 'An_output_must_sit_on_a_real_gap' (:80) and 'The_product_is_the_thickness_you_stop_at' (:126), moving their intent into StockProductsTests.
- [ ] **Step 6.** This deletes the four dangling output codes in one stroke — iwex:rolledplate-iron (:66), iwex:rolledsheet-iron (:66,:77), iwex:wirerod-iron (:90), iwex:nailrod-iron (:100) — none of which resolves to any item in src/.
- [ ] **Step 7.** Re-bless: EXLIB_WRITE_GOLDENS=iwex/itemtypes/rollset ./scripts/exmod.sh test 1.21, then run clean.

#### U7.4 — Define the rolled product items and export their runtime shapes from the drawn elements

> Caution: **Re-scoped 2026-08-05: Three itemtypes, not four — `iwex:rolledrod` is not created.** *(U7.3 Step 4b's
> ruling; read it first.)* The mill's 100 u grooved-2.0 product is **vanilla `game:rod-iron`**, which is
> `2 × 2 × 10 = 100 u` **exactly** — the same object measured twice. The re-rollable work piece at that mass is
> the auto-emitted **`iwex:stock-rod`** from `StockItemDefinitions`, not an item this task mints.
> Caution: **Everywhere below and in U7.5/U8.3 that reads "rolledrod" as a product now means `game:rod-iron`**; where
> it means a piece going back through the stand, it means `iwex:stock-rod`. Leaving both would have shipped
> **three identities for one object** — two mod items plus vanilla's — with two handbook entries, two crop-table
> rows and a golden for a product nothing produces; the coherence findings already file that as a
> BROKEN_DEPENDENCY.

**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Forming/RolledProductItemDefinitions.cs`, `assets/iiex/shapes/forming/rod.json`, `assets/iiex/shapes/forming/nailplate.json`, `assets/iiex/shapes/forming/beam.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/rod.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/nailplate.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/beam.json`, `test/IronIndustryExpanded.Tests/Items/RolledProductMassTests.cs`
- Modify: `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`, `scripts/tools/convert-shape.py`
- Test: `test/IronIndustryExpanded.Tests/Items/RolledProductMassTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionGoldenTests.cs`

**Consumes:** IExItemDefProvider plus ExItemDef.Create/Shape/MaxStackSize/MaterialDensity/Attribute/CreativeCommon — ExpandedLib/Definitions/ExItemDef.cs:107,:207; CastStockItemDefinitions as the worked shapeByType/attributesByType example — src/IronIndustryExpanded/Items/CastStockItemDefinitions.cs:74-104; StockProducts (U7.5)

**Produces:** iwex:rod (25 u), iwex:nailplate (100 u), iwex:beam (200 u) — **three** itemtypes, MaxStackSize 1 where the piece carries heat, each with a runtime shape that resolves. The fourth product of the line is **`game:rod-iron`**, which already exists and is not defined here.

- [ ] **Step 1.** Confirm the element→product mapping and do not redraw anything (ruling 1): `RolledRod200` in item-rolled-rod.json (2×2×10 = 40 vx³) is a **rolling-stage** element and belongs to `iwex:stock-rod`, which `StockItemDefinitions` emits — **not** to an item this task creates; rod = one of RivetRod1..4 in item-rolled-rivetrod.json (1×1×10 = 10 vx³) — the filename still says rivetrod because the rename to `rod` came after the drawing; nailplate = NailPlate1 in item-rolled-nailplate.json (4×1×10 = 40 vx³); beam = Beam in item-rolled-beam.json (4.5×2×9 = 81 vx³). The plate is vanilla game:metalplate and stays vanilla — nothing here re-skins it.
- [ ] **Step 2.** Caution: The nailplate double-route question the plan flags as open is already settled by the art: NailPlate is 40 vx³, exactly half of CutPlate1's 81 vx³, so 'flat-rolled rod' and 'half a plate' land on the same 100 u. Make that an assertion in RolledProductMassTests rather than a review note.
- [ ] **Step 3.** Export the four runtime shapes. Two routes — pick one and say why in a comment. (a) Extend scripts/tools/convert-shape.py with an --element flag that lifts a named element subtree into its own runtime file. This is the safe route: every existing selectiveElements use in the repo is on a blocktype (goldens hits: iwex mpenergy/transmission; hpex boiler/lancashire, engine/cornish; lpex boiler/cornish, engine/watt; smex converter/bessemer) and none is on an itemtype. (b) Point item defs at one family shape with a selectiveElements array — needs a new ExItemDef.ShapeSelectiveElements (ExBlockDef has one at ExBlockDef.cs:285; ExItemDef has none) and needs verifying that VS honours CompositeShape.SelectiveElements on the item render path. Route (a) requires no engine assumption.
- [ ] **Step 4.** Whichever route: the exported shape must not carry the editable absolute texture path (item-rolled-rod.json:12 points at F:/repos/... which resolves to nothing in game and fails silently). Remap via convert-shape.py's TEXTURES table.
- [ ] **Step 5.** Write RolledProductItemDefinitions.cs as an IExItemDefProvider. Masses go on materialUnits and are declared (rolled-parts.md's catalogue: 100/25/100/200), not derived from voxels. Add MaterialDensity(7800) and the combustibleProps/temperatureDamage shape StockItemDefinitions.cs:48-57 uses for anything meant to be reheatable; a 25 u rod that is only ever cold-headed does not need it.
- [ ] **Step 6.** Write RolledProductMassTests.cs: every StockProducts row's ProductCode resolves to a def this provider emits (or to game:metalplate / game:metalnailsandstrips); every mass matches the catalogue; conservation holds per schedule.
- [ ] **Step 7.** Add lang rows for the four items in en/ru/uk, following the RU/UK conventions (single '-', no em-dash).
- [ ] **Step 8.** Bless the **three** new goldens by naming them explicitly: `EXLIB_WRITE_GOLDENS=iwex/itemtypes/rod,iwex/itemtypes/nailplate,iwex/itemtypes/beam ./scripts/exmod.sh test 1.21`. `IiexDefinitionGoldenTests.Goldens_exactly_cover_the_defs` otherwise fails with 'defs with no golden file' — and it fails **in the other direction** too, so do not create a `rolledrod.json` golden for a def that no longer exists.
- [ ] **Step 9.** Confirm IiexDefinitionGoldenTests.Every_shape_reference_resolves_to_a_shipped_file passes — this is the test that catches a def naming a shape that was never exported.
- [ ] **Step 10.** ./scripts/exmod.sh test 1.21.

#### U7.1 — Heat into stock — the reheat furnace's soak, on V/A at the vanilla forge rate with no ×2

> ★★ **DONE 2026-08-21.** Built as designed, with three corrections to the plan's own reading:
> **Step 6's re-homing question is closed** - `SmeltCycle` survived U2/U3 with an honest `dt`, so it is the
> host and the away-catch-up comes free; `InternalTemperature` was made public in U6 anyway.
> **Step 9 kept `RollingCoolRate`** as the `k` that now multiplies `A/V`, and added `ReheatRateK` for the
> soak - one law, two coefficients, because furnace radiation and mill-floor convection are not the same
> number. **Step 3's "same mass" case moved to `RollingPassTests`**: no two hearth-admissible forms share a
> mass, so the machine-level case is bar vs slab and the equal-mass claim is pinned on the pure law.
> ⛔ Two defects found: `ShaftCentre` named a cell one row out (this furnace's bed handle had always
> resolved to a fire slab), and `AlongBarrel` read the deck mirrored at `ns`. Both fixed and guarded.


**Files**
- Create: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/ReheatSoakTests.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityHeatingFurnace.cs:42-56`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityHeatingHearth.cs`, `src/IronIndustryExpanded/IiexConfig.cs:576-636`, `src/IronIndustryExpanded/BlockStructures/Forming/RollingPass.cs:178-196`, `src/IronIndustryExpanded/BlockStructures/Forming/BlockEntities/BlockEntityRollingMill.cs:333-346`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/ReheatSoakTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/RollingPassTests.cs`

**Consumes:** protected override void SmeltCycle(object chargeHandle) { } — BlockEntityHeatingFurnace.cs:54 (the only empty body on the branch); protected override float MeltingPoint => IiexValues.RollingTempC — :62; the melt cadence at BlockEntityFurnaceCore.cs:1176-1183 (SmeltCycle fires every _meltIntervalSec / MeltSpeedFactor() once _internalTemp ≥ _ironMeltingPoint); protected BlockEntityFurnaceCore? Core — BlockEntityFurnacePart.cs:37; public ItemStack? StockIn(HearthRows.Row row) — BlockEntityHeatingHearth.cs:33; public static float Cool(float tempC, float ambientC, float ratePerSecond, float dt) — RollingPass.cs:190

**Produces:** BlockEntityHeatingHearth.SoakTick(float dt) raising each loaded row's vanilla temperature attribute toward the furnace's process temperature at k·A/V; IiexValues.ReheatRateK (the one pacing constant, used in both directions); RollingPass.Cool called with k·A/V instead of the flat RollingCoolRate

- [x] **Step 1.** Establish the vanilla forge rate empirically, not from memory: the provisioned assemblies are at .game/1.21/Mods/VSSurvivalMod.dll and .game/1.21/VintagestoryAPI.dll. Read BlockEntityForge's tick and its temperature-change helper (the firepit's changeTemperature(fromTemp, toTemp, dt) is the shared shape) and cite the member in a code comment. Do not invent a number.
- [x] **Step 2.** Read docs/design/machines/reheat-furnace.md § 'Reheat rate (proposed)' and the settled forming-line design: soak time ∝ V/A — t/2 for a plate, t/4 for a square bar, so thickness alone carries it and no new per-piece state is needed. No ×2 furnace multiplier: under an area law it puts every piece below a vanilla ingot's forge time and the reheat stops being a beat. The furnace's argument was never speed — it is that a forge cannot hold a slab and the hearth soaks three pieces at once.
- [x] **Step 3.** Write ReheatSoakTests.cs first. Build the furnace the harness way — never force StructureComplete; build the real footprint and let the machine complete itself (see test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnacePartsTests.cs for the idiom). Assert: a cold piece on a lit hearth rises; the rise is slower for a thick piece than a thin one at the same mass (this is the whole point of the area law — a mass law makes them equal); the piece never exceeds the furnace's process temperature; an unlit furnace soaks nothing; all three loaded rows soak.
- [x] **Step 4.** Add IiexValues.ReheatRateK to the Rolling region of IiexConfig.cs (near :629, next to RollingCoolRate) with an [ExConfigRange] and a comment that it is the one constant pacing the loop in both directions and is unchosen until tuned in play.
- [x] **Step 5.** Implement the soak. Preferred host: fill BlockEntityHeatingFurnace.SmeltCycle (:54) — the core already calls it on the melt cadence once _internalTemp clears MeltingPoint (= RollingTempC 900), which is exactly 'the furnace is hot enough to reheat'. Resolve the hearth once at OnStructureCompleted via GlobalOf(ShaftCentre) (ShaftCentre = (-2,0,1) at :38) and cache the BlockEntityHeatingHearth handle; the core does not resolve it today.
- [x] **Step 6.** Record why you did or did not take the alternative host: _internalTemp is protected (BlockEntityFurnaceCore.cs:88) with no public read, so having the hearth tick itself needs one added. `public float InternalTempC => _internalTemp;` is a one-line change and decouples U7.1 from the melt cadence — which matters, because ruling 2 makes BfMeltIntervalSec/BfMeltStartDelay/BfMaxFuelBurnTime emergent and BlockEntityFireboxFurnace.cs:197-199 binds this furnace to all three.
- [x] **Step 7.** Raise the stack's temperature with ItemStack.Collectible.SetTemperature (the API the mill already reads through at BlockEntityRollingMill.cs:175) — the stock items declare temperatureDamage and combustibleProps (StockItemDefinitions.cs:48-57), so vanilla's cooling applies during the carry-back for free.
- [x] **Step 8.** Adopt the away-catch-up model (bounded catch-up on reload, as the production machines do) or a reload teleports the soak.
- [x] **Step 9.** Close the loop: change RollingPass.Cool's caller (BlockEntityRollingMill.cs:341-346) to pass ReheatRateK·A/V instead of the flat IiexValues.RollingCoolRate, and add a RollingPassTests case that a 1.0-thick piece sheds heat faster than a 3.0-thick one. Either keep RollingCoolRate as the k it now multiplies, or delete it and say so.
- [x] **Step 10.** Caution: While in BlockEntityHeatingHearth: neither it nor BlockHeatingHearth overrides GetDrops or OnBlockBroken, so breaking a loaded bed silently destroys up to three unique unstackable pieces. Fix it here or file it — do not leave it undocumented.
- [x] **Step 11.** ./scripts/exmod.sh test 1.21.

#### U7.10 — Make the two roll sets survival-reachable, or the gate cannot be demonstrated

> ⛔⛔ **BLOCKED 2026-08-21, by a ruling later than this task, not by work.** This task says: cast a roll
> blank from a `rollers` pattern, then a grid recipe finishes it into a set. **The machining line settled
> otherwise** - [machining-line.md](../../design/machines/../mechanics/machining-line.md) § Lathe: *"its
> `rollerslathe` clip is the intended route for the mill's roll blanks … so the lathe is the mill's missing
> supplier"*. The art agrees: `item-lathed-rollers-{flat,grooved,flatwide*}.json` and
> `item-sandcast-rollerblanks.json` are both drawn, and the blank's own diagram texture exists.
>
> So a grid recipe built now would contradict a settled design and be deleted the day the lathe lands.
> ⛔ **Nor is "build the cast blank half now" right**: an item nothing consumes is the `ClearBed` trap in
> advance. The real content of this task is **the lathe**, which is machining-line scope and needs an owner
> ruling on the machine budget.
>
> ⛔ What it costs meanwhile: every roll set stays `CreativeCommon` with no recipe in any tier, so the
> forming line cannot be demonstrated in survival - the one thing between it and a complete loop.


**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Casting/PatternItemDefinitions.cs:79-175`, `src/IronIndustryExpanded/Recipes/Grid/FormingRecipeDefinitions.cs:26-52`, `src/IronIndustryExpanded/IiexRecipeConfig.cs`, `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/pattern.json`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Definitions/IiexRecipeOutputTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionGoldenTests.cs`

**Consumes:** private static readonly Dictionary<string, object> Molds — PatternItemDefinitions.cs:86; public static readonly string[] PatternTypes — :204; ExRecipeDef.Create(domain,"grid",name).Grid(...) — the worked example at src/IronIndustryExpanded/Recipes/Grid/FormingRecipeDefinitions.cs:39-52; the drawn blank at assets/editable/shapes/item-sandcast-rollers-blank.json

**Produces:** a `rollers` casting pattern (Cell size) plus a grid recipe turning a cast roll blank into iwex:rollset-{flat,grooved}

- [ ] **Step 1.** Caution: The plan omits this and the gate depends on it: RollSetItemDefinitions.cs:127 marks every set CreativeCommon and there is no recipe for any roll set in any tier. FormingRecipeDefinitions.cs:12-16 says so explicitly and defers them to 'the casting patterns once their blank pattern lands'. Without this task the gate is a creative-mode demonstration.
- [ ] **Step 2.** Add a `rollers` entry to PatternItemDefinitions.Molds keyed on the drawn blank (item-sandcast-rollers-blank.json holds Blank1/Blank2 → Blank11/Blank12, two plain 16-long roll blanks, no grooves). Size is `cell`, not longcell — a roll blank is not bulk stock. Carry its mass on `capacity` (ruling 1: capacity is what a pour measures against).
- [ ] **Step 3.** Add two grid recipes: cast roll blank + hammer + a file/chisel → iwex:rollset-flat and → iwex:rollset-grooved. The rolls are chilled cast iron in every tier (RollSetItemDefinitions.cs:11-14), so the route is cast-then-finished, never forged.
- [ ] **Step 4.** Add a `rollset-grid` key to IiexRecipeConfig.DefaultCatalogue so the cost rescales with RecipeLevel, matching docs/design/mechanics/recipes-config.md.
- [ ] **Step 5.** Re-bless by path (EXLIB_WRITE_GOLDENS=iwex/itemtypes/pattern,iwex/recipes/grid/...), add the lang rows, and run ./scripts/exmod.sh test 1.21.

### Traps — each of these makes a green suite a lie

- The forming suite's 123 test methods build every WorkPiece by hand. RollingMillFeedTests.Piece (test/.../RollingMillFeedTests.cs:63-67) does `WorkPiece.Fresh(StockForm.Bloom).ToStack(stack)` and MillFeedTests constructs pieces directly — so B3, the defect that makes every real stock item unrollable, is invisible to a fully green suite and has been for the machine's whole life. The U7.6 test must start from a stack carrying only collectible-level attributes, or it re-creates the blind spot.
- Nothing measures a piece's length anywhere in code. recoverability.md is explicit: none of 32, 48, 2.25 or 2.0 exists in src/, WorkPiece has no Length and no Mass, and StripLength(t) is a per-strip figure nothing compares to a limit. Delete the U7.9 refusal in a refactor and every test still passes while the cast tier silently soft-locks. The schedule-walk invariant test is the only thing that would notice.
- The ns gap-band sign inversion (BlockRollingMill.cs:373) is dormant only because B17 pins AlongBarrel to [0.667,1). Fix B17 without fixing the sign and a dormant bug becomes a live one that no test covers — MillFeedTests only exercises MillFeed.AlongBarrel(double), never the block's rotation. Write the ns test in the same commit as the Deck fix.
- Caution: RollSetSpec.MinTorque, IsWide, PassesAt, OverhangsBarrel, NextGap, NextDraft and Family have no production caller — only five members of the record are reached from src/. PassesAt is referenced solely by a <see cref> in RollingPass.cs's doc comment, which is exactly how it reads as wired when it is not. 'It is tested' does not mean 'it is used'; deleting any of them breaks nothing.
- Caution: RolledStockStagesTests asserts the art against the simulation's own model. If U7.3 changes the model and regenerates the shapes from the same changed model, the test becomes a tautology and passes for any law you invent. Pin the law against the measured editable elements, not against regenerated output.
- Caution: HeatingHearthLayout.StockOf (HeatingHearthLayout.cs:64-79) is a hard-coded prefix whitelist and the element map (:35-58) is fifteen hard-coded strings aimed at art. Rename or re-code any stock item and the hearth silently stops accepting it — selective-element matching drops an unknown name without an exception. Worse: Items1/Items3/Items2 are not in positional order (Items3 is the centre), so a wrongly-known name draws in the wrong place, also silently.
- Caution: BlockEntityPuddlingHearth sizes its own arrays off HeatingHearthLayout.Rows, so touching the reheat hearth's contents model reaches into a U6 file. Do not restructure the rows in U7 — the crosswise seating is explicitly out of scope.
- Caution: SmeltCycle only fires while _internalTemp ≥ _ironMeltingPoint and the FSM is in Melting (BlockEntityFurnaceCore.cs:1129-1183). Ruling 2 dissolves that FSM into a derived HUD label. A soak hosted inside SmeltCycle therefore has a cadence U2/U3 are about to delete. Either host it on the hearth's own tick behind a public InternalTempC, or accept that U7.1 must be re-homed after U3 — and say which, in the code.
- Caution: Re-blessing with EXLIB_WRITE_GOLDENS=1 rewrites the whole iwex domain and would silently absorb other in-flight def changes (this branch has a large uncommitted tree). Always name paths: EXLIB_WRITE_GOLDENS=iwex/itemtypes/rollset,iwex/itemtypes/caststock.
- Caution: Run the suite with `./scripts/exmod.sh test 1.21`. `latest`/1.22 reports ~20 IPlayer-mocking failures from VS 1.22.6 that are upstream and not real — and this unit's tests are exactly the kind (BlockRollingMill.HandleInteract, BlockHeatingHearth.HandleInteract) that take IPlayer, so the noise will read as your own breakage.
- Caution: U7.4's four itemtypes will fail IiexDefinitionGoldenTests.Goldens_exactly_cover_the_defs ('defs with no golden file') and Every_shape_reference_resolves_to_a_shipped_file until both the goldens and the exported runtime shapes exist. Neither is a code bug; do not chase it.
- Caution: IiexLangCoverageTests only covers block codes (LangCoverage.MissingNames over block codes). Nothing fails if U7.4's four item lang rows are forgotten or if U7.3 leaves the deleted sets' rows behind — the raw key just renders in game. Check the three locale files by hand.
- Note: U7.8 (roasting) is deferred and should stay deferred: it is designed to live on the reheat furnace 'and the ore mixer', and U5 deleted the mixer, so its host must be re-decided before anything is built. No item, no furnace mode, no recipe and no config key exist today.

**Gate.** Honest, achievable-inside-U7 gate — demonstrate in one integration test plus one in-game session: a shingled bar item, laid on a lit reheat furnace's hearth, rises above RollingTempC at a rate that scales on V/A (thin faster than thick) with no ×2 multiplier; carried to a mill fitted with the `flat` set it is accepted on any of the three input-deck cells, in both orientations, with the gap band picked by where along the deck you click; four feeds later it is at 2.0 and its width/length reproduce the drawn Beam element (4.5 × 2 × 18) under the section law; eight feeds later it is at 1.0 and reproduces CutPlate (9 × 1 × 18); `StockProducts.At("bloom", Flat, 1.0f)` answers `2 × game:metalplate @200` and `StockProducts.At("bloom", Square, 2.0f)` answers `4 × rolledrod @100`, with the four product items existing, shaped, lang-keyed and golden-pinned; a feed whose output would exceed 48 voxels is refused with FeedVerdict.TooLong; and the roll sets are craftable rather than creative-only. `./scripts/exmod.sh test 1.21` green with StockMesh.cs and StockMeshTests.cs deleted rather than stubbed. The plan's stated gate — "rod, plate and beam" in hand — additionally requires U8's shear, which owns every crop; U7 ends one gesture short of a product.

---

# U8 — Fasteners and the shop floor: MP shear (cutter), nail machine, rivet machine, stock rack

U8 turns the mechanical-energy graph from a one-consumer curiosity into a real network by adding three MP-driven benches — the shear/cutter (every crop in the ladder), the nail machine (nailplate → 4 nails, sheared and headed in one pass) and the rivet machine (25 u rod → rivets) — plus the stock rack, a mechanism-free 1×1×3 display megablock. All three machines are already fully drawn as megablocks with idle+cycle clips (contradicting both the plan and the three design pages, which all say "nothing drawn, 1×1×1"), so U8 is genuinely layouts, block entities, drive geometry, items and recipes — no machine art. Dies and bolts are dropped per state.md § Fasteners (settled 2026-07-30); the fastener rule becomes substitution (iwex accepts nails or rivets, lpex's boiler accepts rivets only) rather than a tier gate. The stock rack is the one genuinely blocked deliverable: neither its art nor the shared `StockPile.Place` pile-composition code exists anywhere.

> ## Ruled 2026-08-05 — the three benches get footprints, and the user drew them
>
> Caution: **Neither option the findings offered was taken.** Not 1 × 1 × 1 (which leaves an 82-voxel riveter straddling
> five cells a player can build into), and not the art's measured `3×2×2 / 2×3×3 / 6×3×2`. The user supplied
> **compact layouts** — 6, 4 and 6 cells — reproduced **verbatim** below. They are the authority; the drawn art
> and the four design pages that say "1 × 1" both defer to them.
>
> **MP shear** *(xy slice)* — 6 cells
> ```
> # # i
> I O #
> ```
> **Nail machine** *(zy slice)* — 4 cells
> ```
> I M
> # O
> ```
> **Rivet machine** *(xy slice)* — 6 cells
> ```
> # M #
> I O #
> ```
>
> | glyph | meaning |
> |---|---|
> | `#` | full-block filler |
> | `i` | **down-slab** filler *(shear only)* |
> | `O` | the **principal** — and on the **shear** it is also the MP input, a **north-south** shaft |
> | `M` | full-block filler **carrying the shaft / MP input** — **west-east** on the nail machine, **north-south** on the riveter |
> | `I` | full-block **interaction** filler — the cell the player clicks |
>
> **Every mechanism this needs already ships, and the plan's cost estimate was wrong.**
> `FillerLayoutBuilder.Host(char, params FillerBehaviorSpec[])` exists and its own doc-comment uses **`M`** for
> exactly this case: `f.Host('M', new FillerBehaviorSpec("exlib.BEBehaviorMPFillerPort", "west"))`. So a
> footprint cell **can** be the drive input — `BEBehaviorMPFillerPort` "joins the MP network at that position
> and exposes a connector", and **`BlockFlywheel` already drives through one**. Therefore **U8.4 Step 2's
> "generalise `BlockRollingMillAxle`'s drive bus" is not the route** — the axle blocktype is for a mill's
> *through* axle, and the findings' "a filler can never be a graph node" describes the axle, not the port.
> `I` is `IFillerInteractionTarget` (shipped, and `BlockStructureFiller` already routes the clicked cell to
> it); `i` is an oriented part, which layouts can already demand.
> Caution: **Builder note, not a design question:** `Slice(x, …)` is the **zy** elevation (rows run down in −Y,
> columns +Z), so the nail machine's grid transcribes directly. The two **xy** slices are either stacked
> `Layer(y, …)` grids or a north-orientation chosen so the long axis runs +Z — decide at implementation and
> say which in a comment; do not silently transpose a layout.

**Entry condition.** U7 must be complete and is not today — verified against src/. Hard prerequisites U8 cannot be started without: (1) U7.2 — `WorkPiece` (src/IronIndustryExpanded/BlockStructures/Forming/WorkPiece.cs:34) is still the per-strip `Strips[]`/`Turned[]` record with no `Thickness`, no `Mass` and no `Length`, so a crop cannot be expressed at all; (2) U7.4 — no rolled product item exists (src/IronIndustryExpanded/Items/ has no rolledrod/nailplate/beam; the codes live only as string literals at RollSetItemDefinitions.cs:66,77,90,100, four of five naming items that do not resolve); (3) U7.5 — `RollSetSpec.OutputAt` (RollSetSpec.cs:95) still has zero callers in src/, so rolling produces no product to crop; (4) ruling 4 — `StockItemDefinitions.cs:44` still emits the tenths shape key `(int)(form.BaseThickness*10)` and `scripts/tools/generate-rolled-stock.py` still writes `stock-bloom-30`; the hundredths key must land in U7 before U8.3 writes a stage-keyed crop table against it. Soft prerequisite: `RollSetItemDefinitions.Sets["grooved"]` opens at a 1.0 gap against 3.0 stock (RollSetItemDefinitions.cs:89), blocker B4, so the 25 u rod the rivet machine eats has no legal entry until U7.3 rewrites it to 1.5 → 1.0.

**Shared files** (collision risk): `src/IronIndustryExpanded/IiexConfig.cs — U2, U3 and U7 all add config regions; U8 adds a Forming benches region after the Rolling mill region at :576-633`, `src/IronIndustryExpanded/IiexRecipeConfig.cs:45-78 — every unit shipping a craftable block adds catalogue rows to the same dictionary`, `src/IronIndustryExpanded/Generated/IiexBlocks.g.cs — regenerated wholesale by EXLIB_WRITE_BLOCKCODES=1; two units adding blocks in parallel will both rewrite this file`, `test/IronIndustryExpanded.Tests/goldens/iiex/** — one file per def; the completeness test (Goldens_exactly_cover_the_defs) fails if any unit adds a def without its golden`, `assets/iiex/lang/en.json, ru.json, uk.json — every unit adding a block or an error code appends here; IiexLangCoverageTests fails on any missing locale`, `src/IronIndustryExpanded/BlockStructures/Forming/RollSetSpec.cs — U7.3 rewrites the gap tables while U8.3 deletes Outputs/OutputAt and its parse branch at :159-176`, `src/IronIndustryExpanded/BlockStructures/Forming/RollSetItemDefinitions.cs — U7.3 redesigns the sets; U8.3 strips every `outputs` array and the Out() helper at :46`, `src/IronIndustryExpanded/BlockStructures/Forming/WorkPiece.cs — U7.2 rewrites it to the two-round model; U8.3 and U8.4 read Thickness/Mass/Length off the result`, `src/IronIndustryExpanded/BlockStructures/Forming/StockItemDefinitions.cs:44 — U7.4 changes the shape key from tenths to hundredths; U8.3's crop table keys on the same integer`, `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockRollingMillAxle.cs:75-121 — U8.4 generalises its three BlockRollingMill casts so the bus serves four machines; anything else touching the mill's placement touches these lines`, `src/ExpandedLib/Definitions/ExIngredients.cs and ConstructionStages.cs — cross-mod files; U8.7's Fastener/RequireFastener helpers change how 36 nail sites and 27 rod sites across all four mods resolve`, `src/IronIndustryExpanded/Recipes/Grid/FormingRecipeDefinitions.cs — U8.8 extends it to four recipes and U8.10 adds a fifth`, `src/IronIndustryExpanded/Recipes/Grid/PipeRecipeDefinitions.cs:25,34,43,52 — U8.7 retargets all four pipe segments off Nails(1)`, `docs/internal/plans/STATE.md — U8.11 fixes the placement table at :596-599; other units edit the same file's decision log`, `docs/iiex/handbook/*.html plus the NN- prefixed keys in assets/iiex/lang/en.json — the sync pipeline joins them and drift fails a test, so any unit adding a handbook page collides here`

### Tasks

#### U8.1 — Export the three drawn machine shapes to runtime and track the editables

**Files**
- Create: `assets/iiex/shapes/forming/cutter.json`, `assets/iiex/shapes/forming/nailcutter.json`, `assets/iiex/shapes/forming/riveter.json`, `test/IronIndustryExpanded.Tests/Blocks/Forming/FormingShapeExportTests.cs`
- Modify: `scripts/tools/convert-shape.py:36-88 (only if --check reports an unmapped texture key)`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/FormingShapeExportTests.cs`

**Consumes:** scripts/tools/convert-shape.py — `python scripts/tools/convert-shape.py <editable-name> <runtime-path> [...]` (:100-125), `--check` mode (:139-158); TEXTURES map (:36-88) already maps cast-iron1 -> iwex:block/metal/castiron, iron3 -> game:block/metal/riveted/iron3, iron5 -> game:block/metal/sheet-plain/iron5; clip rewrite rules HOLD_CLIPS (:88) and ONESHOT_CLIPS (:96); DefinitionAssets.MissingShapes(domain, assembly) enforced by IiexDefinitionGoldenTests.Every_shape_reference_resolves_to_a_shipped_file (:52-60)

**Produces:** Three runtime shape assets addressable as `iwex:forming/cutter`, `iwex:forming/nailcutter`, `iwex:forming/riveter`, each carrying an `idle` and a `cycle` clip with `"onAnimationEnd": "Repeat"` and no absolute filesystem texture path

- [ ] **Step 1.** Run `python scripts/tools/convert-shape.py --check` from the repo root and confirm it prints 'Every texture key in assets/editable/shapes is mapped' — the three machine files use only cast-iron1, iron3 and iron5, all already mapped
- [ ] **Step 2.** Run `python scripts/tools/convert-shape.py machine-mp-megablock-cutter assets/iiex/shapes/forming/cutter.json machine-mp-megablock-nailcutter assets/iiex/shapes/forming/nailcutter.json machine-mp-megablock-riveter assets/iiex/shapes/forming/riveter.json`
- [ ] **Step 3.** Open each emitted file and confirm `textures` no longer contains any value starting with `F:/` (the riveter's `iron5` and all three files' `cast-iron1` are absolute authoring paths in the editable source)
- [ ] **Step 4.** Confirm each emitted file's `animations` array still holds both `idle` and `cycle`, and that both now read `"onAnimationEnd": "Repeat"` (the editables carry Blockbench's `EaseOut`, which makes a running mesh vanish)
- [ ] **Step 5.** Write FormingShapeExportTests with one theory over the three runtime paths asserting: the file exists; `animations` is non-empty; every clip's `onAnimationEnd` equals `Repeat`; no `textures` value matches `^[A-Za-z]:` — nothing in the suite tests any of this today
- [ ] **Step 6.** Measure the `cycle` clip's rotation keyframes on each ShaftGroup and confirm the wheel turns exactly one revolution across the clip's 60 frames; EnergyAnim.SpinSpeed (src/IronIndustryExpanded/BlockNetworkEnergy/EnergyAnim.cs:23-24) divides omega by 2*pi on the assumption of one revolution per clip, and a two-revolution clip animates at half speed with no error anywhere
- [ ] **Step 7.** `git add assets/editable/shapes/machine-mp-megablock-{cutter,nailcutter,riveter}.json` — all three are currently untracked (`??`)
- [ ] **Step 8.** Run `./scripts/exmod.sh test 1.21`

#### U8.2 — Shared bench base: BlockEntityMpBench (mpenergy consumer, stroke cycle, input piece, output tray)

> ★★ **DONE 2026-08-21 as `BlockEntityMpBench`.** Extracted from the shear rather than written first,
> which is why it carries exactly what two machines turned out to share: the mpenergy membership, the
> 250 ms stroke clock, the speed/torque reads, ejection, and the stroke's own persistence. ⛔ The stroke
> key moved `shearRemaining` -> `benchRemaining`, so the base reads the legacy key as a fallback - a shear
> caught mid-stroke by the upgrade would otherwise strand its piece with no clock to clear it.


**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Forming/BlockEntities/BlockEntityMpBench.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/MpBenchTests.cs`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/MpBenchTests.cs`

**Consumes:** IMpEnergyConsumer.LoadTorque(float speed) — src/ExpandedLib/Networks/MpEnergyNodes.cs:35-41; BlockEntityNetworkNode (src/ExpandedLib/Blocks/Networks/BlockEntityNetworkNode.cs) with `public override string NetworkType { get; set; }`; live speed read `(NetworkSystem?.GetNetworkAt(Pos) as MpEnergyNetwork)?.State?.Speed ?? 0f` — BlockEntityRollingMill.cs:79-81; RollingPass.CanCarry(float loadTorque, float availableTorque, float speed) — RollingPass.cs:126-127 (currently zero callers in src/); persistence idiom `tree.SetItemstack` + `stack.ResolveBlockOrItem(worldForResolving)` — BlockEntityRollingMill.cs:409-443

**Produces:** `public abstract class BlockEntityMpBench : BlockEntityNetworkNode, IMpEnergyConsumer` exposing: `public override string NetworkType { get => "mpenergy"; set {} }`; `protected abstract float StrokeTorque { get; }`; `protected abstract int StrokesPerPiece { get; }`; `protected abstract bool Convert(ItemStack input, out ItemStack[] products, out string? errorCode)`; `public bool IsWorking { get; }`; `public bool TryLoad(ItemStack? stack, out string? errorCode)`; `public ItemStack? TakeFromTray()`; `public IReadOnlyList<ItemStack> Tray { get; }`; `public bool AdvanceStroke(float dt, float speed)`; `public float LoadTorque(float speed)`; `public ItemStack? ReleaseWorkPiece()`

- [x] **Step 1.** Write the failing tests first in MpBenchTests against a private `TestBench : BlockEntityMpBench` double that converts one fake stack to two: an idle bench returns LoadTorque 0; a loaded bench returns StrokeTorque > 0; a bench cannot be loaded twice; AdvanceStroke at speed 0 makes no progress and does not lose the piece; AdvanceStroke completes after exactly StrokesPerPiece advances and moves products to the tray; TakeFromTray returns products newest-first and empties; ReleaseWorkPiece hands the input back unchanged mid-cycle
- [x] **Step 2.** Add the torque-gate tests: below StrokeTorque the run cannot carry, so AdvanceStroke returns false and the piece is untouched; above it the stroke advances — route the check through RollingPass.CanCarry so that function finally has a caller in src/
- [x] **Step 3.** Implement BlockEntityMpBench: fields `_input`, `_tray` (List<ItemStack>), `_strokesDone`; register the stroke tick server-side in Initialize with `RegisterGameTickListener` mirroring BlockEntityRollingMill.cs:64-71, but read the tick interval from a config key rather than repeating the mill's `private const int PassTickMs = 250` (BlockEntityRollingMill.cs:41)
- [x] **Step 4.** Implement LoadTorque(speed) => IsWorking ? StrokeTorque : 0f — deliberately speed-independent, matching BlockEntityRollingMill.cs:314-326 and its stated reason
- [x] **Step 5.** Implement OnBlockBroken(IPlayer?) spawning the input piece and every tray stack before calling base, modelled on BlockEntityRollingMill.cs:374-383
- [x] **Step 6.** Implement ToTreeAttributes / FromTreeAttributes for `_input`, `_tray` and `_strokesDone`, calling `ResolveBlockOrItem(worldForResolving)` on every stack read back (without it the loaded stack has no Collectible and silently fails)
- [x] **Step 7.** Add a reload test: load a bench, advance one stroke, round-trip the tree via TestWorld.Reload(pos), and assert `_strokesDone`, the input and the tray all survive
- [x] **Step 8.** Run `./scripts/exmod.sh test 1.21`

#### U8.3 — **ShearFeed** — the pure decision layer *(re-scoped 2026-08-05: the crop table is U7.5's)*

> Caution: **collision resolved 2026-08-05 — U7.5 owns the crop table; this task consumes it.** U7.5 and U8.3 were
> the same task twice, building two incompatible tables and both deleting `RollSetSpec.Outputs`/`OutputAt`.
> **U7.5's key wins because it is strictly more expressive, and its own first two rows prove the difference
> is real:** `shingledbar grooved 2.0 → 4 × rod` and `shingledbar flat 2.0 → 2 × beam` are the **same form at
> the same thickness with different answers**. A section-blind key collapses them — the plate route and the rod
> route become one crop. This is why `rolled-parts.md`'s crop table has a **Set** column at all, and why
> `rolled-parts.md:132` calls the rod "the sharpest decision point in the whole forming line".
> Caution: **`SectionClass` is the piece's geometry** (`{Flat, Square}`, parsed from a required `section` key), **not
> the roll set's `Family`** (`flat`/`grooved`, which is item identity and handbook grouping). Keying on
> geometry is what lets the shear read the **piece** rather than ask which mill made it.
> **What this task keeps from its old self:** the hundredths integer stage key (adopted into U7.5), the
> **mass-ledger** test and the **2.25-vs-2.2 collision** test. Those three were right.
> Caution: **What it loses:** `ShearCrops.cs`, `ShearCropsTests.cs`, both `RollSetSpec.cs` / `RollSetItemDefinitions.cs`
> Modify targets and the old Step 8 — U7.5 Step 5 already performs every one of those deletions, and U7 runs
> first. Nothing has been consumed yet: `grep -rn "SectionClass\|StockProducts\|ShearCrops" src/ test/`
> returns **zero** hits, so this re-scope is free.

**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Forming/ShearFeed.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/ShearFeedTests.cs`
- Modify: — *(none; U7.5 owns every `RollSetSpec` / `RollSetItemDefinitions` deletion)*
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/ShearFeedTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/StockProductsTests.cs` *(add the two tests carried over)*

**Consumes:** FeedDecision / FeedVerdict and the pure-decision house style — src/IronIndustryExpanded/BlockStructures/Forming/MillFeed.cs:6-29 and MillFeed.Decide(...) at :95-128; StockForm.All (StockForm.cs:57-64) and StockForm.WidthAt(float) (:39-40); the U7 WorkPiece with a scalar `Thickness`; **`StockProducts.At(form, section, stage)` and its `int StageKey` from U7.5**

**Produces:** `public static class ShearFeed` with `public static FeedDecision Decide(WorkPiece? piece, float availableTorque, float speed, float minTorqueHot, float coldMultiplier, float tempC, float rollingTempC)`; plus two tests **added to `StockProductsTests`** — the mass ledger and the key-collision guard

- [ ] **Step 1.** Note: *(carried into U7.5 Step 2 — the crop rows are authored there, off `rolled-parts.md` § 'Every crop point'. Do not re-author them here.)* Where those rows read `rolledrod`, the product is **`game:rod-iron`** and the re-rollable piece is **`iwex:stock-rod`** — see U7.3 Step 4b.
- [ ] **Step 2.** Add the mass-ledger test **to `StockProductsTests`**: for every entry, input units == Count × output units. This is the only thing standing between a balance tweak and silently minting metal — it is the most valuable thing this task ever carried, so it moves rather than dies.
- [ ] **Step 3.** Add the key-collision test **to `StockProductsTests`**: assert the table is keyed on an integer hundredths stage and that 2.25 and 2.2 produce distinct keys — `(int)(t*10)` collides them, and `RollSetSpec.OutputAt` compared floats with `==` (`RollSetSpec.cs:98`), which is why the table moved at all
- [ ] **Step 4.** Note: *(the 2.25 half-step row is U7.5 Step 3 — `RollSetSpec.TryParse` rejected any output gap that is not one of the barrel's gaps at `:172-176`, which is precisely why the billet's product could not be expressed before.)*
- [ ] **Step 5.** Note: *(dropped — `StockProducts` is U7.5's to implement, including the invariant `PathFor`-style formatter that never emits `2,5`.)*
- [ ] **Step 6.** Write ShearFeedTests: no piece -> a distinct verdict; a piece at a stage with no crop entry -> a distinct verdict; a hot piece over minTorqueHot -> Accepted; the same piece cold needs coldMultiplier x minTorqueHot and is refused below it; a hot cut has no temperature floor at all (unlike RollingPass.CanBite at RollingPass.cs:52-57 — shearing is force, not friction)
- [ ] **Step 7.** Implement ShearFeed.Decide as a pure function returning FeedDecision, routing the torque comparison through RollingPass.CanCarry
- [ ] **Step 8.** Note: *(dropped 2026-08-05 — U7.5 Step 5 deletes `Outputs`/`OutputAt` and every `outputs` array, and U7.5 Step 7 re-blesses `iwex/itemtypes/rollset`. U7 runs first, so by the time this task starts there is nothing left to delete and nothing left to bless.)*
- [ ] **Step 9.** Run `./scripts/exmod.sh test 1.21`

#### U8.4 — The shear (cutter): BlockShear + BlockEntityShear + 3x2x2 footprint + drive bus + def

**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockShear.cs`, `src/IronIndustryExpanded/BlockStructures/Forming/BlockEntities/BlockEntityShear.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/ShearTests.cs`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/forming/shear.json`
- Modify: `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockRollingMillAxle.cs:75-121 (generalise the three `is BlockRollingMill mill` casts so the axle bus can serve any forming principal)`, `src/IronIndustryExpanded/IiexConfig.cs:576-633 (add a Forming benches region after the Rolling mill region)`, `assets/iiex/lang/en.json:105-124`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`, `src/IronIndustryExpanded/Generated/IiexBlocks.g.cs (regenerated, never hand-edited)`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/ShearTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionGoldenTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexLangCoverageTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexNetworkNodeContractTests.cs`

**Consumes:** BlockRollingMill as the end-to-end template — `BlockNetworkNode, IExBlockDefProvider, IFillerHost, IFillerInteractionTarget` (BlockRollingMill.cs:31-36), `Definitions(string domain)` (:44-64), `StructureFootprint.Layout(f => ...)` (:74-94), `AxleOffsets`/`AxleCells(BlockPos)` (:99, :113-120), the CanPlaceBlock/OnBlockPlaced/OnBlockBroken triad (:122-215), `StructureAngle => Variant?["orientation"] == "ns" ? 90 : 0` (:106), `.SolidNonOpaque()` for an overhanging shape (:62-63); StructureFillers.FootprintCells/CanPlace/PlaceFillers/RemoveFillers (src/ExpandedLib/Blocks/Structures/StructureFillers.cs:150,203,221,258); BlockEntityMpBench from U8.2; ShearFeed.Decide and ShearCrops.At from U8.3

**Produces:** `public partial class BlockShear : BlockNetworkNode, IExBlockDefProvider, IFillerHost, IFillerInteractionTarget` with `public static IEnumerable<ExBlockDef> Definitions(string domain)` emitting code `iwex:forming-shear-{ns|we}`, `public int StructureAngle`, `public IEnumerable<BlockPos> AxleCells(BlockPos)`; `public class BlockEntityShear : BlockEntityMpBench` with `public FeedDecision TryCrop(ItemStack? stack, float speed)`

- [ ] **Step 1.** Write ShearTests first, modelled on RollingMillTests.cs:32-149: assert the fillerOffsets JSON straight off the def (`BlockShear.Definitions("iwex").Single().ToJson()["attributes"]["fillerOffsets"]`) has exactly the expected cell count for a 3x2x2 body minus the principal and minus the drive-bus cells; assert the principal (0,0,0) and every bus cell are excluded; assert AxleCells swings from -X to +Z between the `we` and `ns` variants; assert StructureAngle is 0 for `we` and 90 for `ns`
- [ ] **Step 2.** Decide and record the drive geometry before writing the footprint: the art is 3 cells along X (measured x -16..30 = 46 vx across the Base and CutterGroup groups), so the shaft crosses two neighbour cells. A filler can never be a graph node (BlockRollingMillAxle.cs:17), so those cells must be real BlockNetworkNode cells or the bench is only drivable at its principal and cannot sit mid-line-shaft
- [ ] **Step 3.** Generalise BlockRollingMillAxle: change the three `world.BlockAccessor.GetBlock(principal) is BlockRollingMill mill` casts at :78, :104 and :118 to test an interface or plain `Block` so the same `iwex:forming-millaxle-*` blocktype serves the shear. Do not rename the block code — it is shipped and a rename needs a BlockMigrations entry
- [ ] **Step 4.** Author the footprint with StructureFootprint.Layout in the `we` frame, principal at (0,0,0), leaving the bus row as `.` cells exactly as BlockRollingMill.cs:74-94 does
- [ ] **Step 5.** Write BlockShear.Definitions: `ExBlockDef.Create(domain, "forming", "forming/shear").Class<BlockShear>().EntityClass<BlockEntityShear>().Material(EnumBlockMaterial.Metal).MaxStackSize(1).Handbook("forming-shear-*").VariantGroup("type", "shear").VariantGroup("orientation", "ns", "we").ShapeByType("*-we", "iwex:forming/cutter", rotateY: 0).ShapeByType("*-ns", "iwex:forming/cutter", rotateY: 90).CreativeCommon("*-we").FillerOffsets(Footprint).SolidNonOpaque()` — the `type` group must have at least one state or the block silently becomes unplaceable (IiexNetworkNodeContractTests)
- [ ] **Step 6.** Implement BlockEntityShear on BlockEntityMpBench: `Convert()` looks up **`StockProducts.At(form, section, stage)`** *(re-pointed 2026-08-05 — `ShearCrops` no longer exists; U7.5 owns the table and its key carries the piece's `SectionClass`, which is what makes a grooved 2.0 crop differ from a flat one)*, emits `Count` copies of the crop's `ProductCode` and leaves the remainder on the deck as stock (crop-not-convert)
- [ ] **Step 7.** Implement BlockShear's interaction routing: OnFillerInteractStart + OnBlockInteractStart both funnelling into one HandleInteract, sneak+RMB to take the piece back, wrench to free a jammed piece — copy the shape of BlockRollingMill.cs:232-291 but do not implement IMpEnergyDirection (direction is last-writer-wins across the whole run, MpEnergyNetwork.cs:74-77, and a second writer silently swaps the mill's decks)
- [ ] **Step 8.** Add lang keys `block-forming-shear-*` plus every ingameerror and help key to all three of en.json, ru.json and uk.json — IiexLangCoverageTests fails on any locale
- [ ] **Step 9.** Regenerate the block code table with `EXLIB_WRITE_BLOCKCODES=1` on the iwex test project and bless the new golden with `EXLIB_WRITE_GOLDENS=test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/forming/shear.json`
- [ ] **Step 10.** Add an integration test that places a shear next to a shaft and a flywheel, builds the real footprint via TestWorld.Place + AddNode (never force StructureComplete), and asserts the shear appears in the same MpEnergyNetwork as the flywheel and that its LoadTorque reaches the network sum while cropping
- [ ] **Step 11.** Run `./scripts/exmod.sh test 1.21`

#### U8.5 — The nail machine: nailplate -> 4 nails, sheared and headed in one pass

> ★★ **DONE 2026-08-21, and not as written.** ⛔⛔ **Step 5 is stale and was not followed**: it says
> *"there is no die and no tooling slot - state.md drops the ItemDie family entirely"*. That was true on
> 2026-07-30 and false by 2026-08-12, when [machining-line.md](../../design/mechanics/machining-line.md)
> § Tooling ruled *"the heading, nail and rivet benches take dies"* and `ItemDie` shipped in exlib. The
> bench takes a die, and it is that contract's first production consumer.
>
> Built as **one blocktype with a `type` variant** (`BlockFastenerBench`, `BlockEntityFastenerBench`)
> shared with the riveter, per the owner's ruling that the machining machines are one machine - so there
> is no `BlockNailMachine`. The footprint is machines.txt's own 4 cells, not the plan's measured 2x3x3.
> Step 6's two-cell tray verb is not built: the drawn bench has one working face and the shear's
> single-click verb is what both benches use, pending the station window.


**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockNailMachine.cs`, `src/IronIndustryExpanded/BlockStructures/Forming/BlockEntities/BlockEntityNailMachine.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/NailMachineTests.cs`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/forming/nailmachine.json`
- Modify: `src/IronIndustryExpanded/IiexConfig.cs (nail bench keys)`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`, `src/IronIndustryExpanded/Generated/IiexBlocks.g.cs`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/NailMachineTests.cs`

**Consumes:** BlockEntityMpBench (U8.2); BlockShear's footprint/def/axle pattern (U8.4); the `nailplate` item from U7.4; ExIngredients.Nails resolves `game:metalnailsandstrips-*` (src/ExpandedLib/Definitions/ExIngredients.cs:36-37) — the output code is the vanilla item

**Produces:** `public partial class BlockNailMachine : BlockNetworkNode, IExBlockDefProvider, IFillerHost, IFillerInteractionTarget` emitting `iwex:forming-nailmachine-{ns|we}` over a 2x3x3 footprint; `public class BlockEntityNailMachine : BlockEntityMpBench` converting 1 nailplate to 4 `game:metalnailsandstrips-{metal}`

- [x] **Step 1.** Write NailMachineTests first: one nailplate in yields exactly 4 nail stacks and nothing else; the conversion takes StrokesPerPiece strokes, not one; a non-nailplate stack is refused with a distinct error code; the metal of the output follows the metal of the input plate
- [x] **Step 2.** Add the anchor test that must never be relaxed: 4 nails per 100 u nailplate, asserted as an arithmetic identity against the item's materialUnits, not as a literal 4. Vanilla's own rate is 25 u per bundle from two independent readings, and a machine that beats it invalidates the whole no-minting argument the forming line is balanced on
- [x] **Step 3.** Author the 2x3x3 footprint from the measured art (Base x 0..16 z 0..27, MachineCasing y -6..27, NailTray z 0..29 — the tray pokes into the +Z neighbour cell) with the principal at (0,0,0) in the `we` frame
- [x] **Step 4.** Write BlockNailMachine.Definitions naming `iwex:forming/nailcutter` per orientation, with the same `type`/`orientation` variant grammar as the shear
- [ ] **Step 5.** Implement BlockEntityNailMachine.Convert; there is no die and no tooling slot — state.md § Fasteners (settled 2026-07-30) drops the ItemDie family entirely, and a cut-nail machine sheared and headed in one pass, which is why no separate heading machine ever existed
- [ ] **Step 6.** Route the tray interaction to the NailTray cell specifically so RMB-empty on the tray collects and RMB-with-plate on the feed table loads — two different cells, two different verbs
- [x] **Step 7.** Add lang keys in all three locales; regenerate block codes; bless the new golden by explicit path
- [x] **Step 8.** Run `./scripts/exmod.sh test 1.21`

#### U8.6 — The rivet item and the rivet machine: rod @ 25 u -> rivets

> ★★ **DONE 2026-08-21.** `iiex:rivet` at **12.5 u**, two per rivet rod, so the ruling's 8-per-100 u
> against nails' 4 holds exactly. No metal variant: the rivet rod that feeds it has none either, and a
> steel rivet is siex's the day it has a route. It borrows vanilla's nails-and-strips geometry outright,
> as Step 2 allows.
>
> ⛔ Steps 7 and 8 (the consumer census and the four-at-once integration test) are **not built** - the
> census would pin a count that is about to move again when the station family lands, and the
> load test wants a drive rig this suite has no fixture for. Filed rather than skipped silently.


> **Ruled 2026-08-05: the rivet route yields more than the nail route** — a bundle at **12.5 u**, i.e.
> **8 rivets per 100 u against nails' 4**. U8.6 Step 1 and U8's Gate asserted opposite requirements and the
> deciding number was never given; this is it.
>
> **That yield advantage is the trade U8.7's substitution rule sells** — the rivet machine earns its extra
> mill schedule and its second bench, rather than being an lpex prerequisite dressed as an iwex bench.
> Caution: **Consequences to carry, not to discover later:** nails become **dominated** everywhere iwex accepts either
> fastener, so the nail machine now survives on **build cost alone** — check that it is meaningfully cheaper, or
> it has no reason to exist. And **U8's Gate clause asserting equal yield must be struck**, not reinterpreted.

**Files**
- Create: `src/IronIndustryExpanded/Items/FastenerItemDefinitions.cs`, `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockRivetMachine.cs`, `src/IronIndustryExpanded/BlockStructures/Forming/BlockEntities/BlockEntityRivetMachine.cs`, `test/IronIndustryExpanded.Tests/Items/FastenerMassTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/RivetMachineTests.cs`, `test/IronIndustryExpanded.Tests/goldens/iiex/itemtypes/rivet.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/forming/rivetmachine.json`
- Modify: `src/IronIndustryExpanded/IiexConfig.cs (rivet bench keys)`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`, `src/IronIndustryExpanded/Generated/IiexBlocks.g.cs`
- Test: `test/IronIndustryExpanded.Tests/Items/FastenerMassTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/RivetMachineTests.cs`

**Consumes:** BlockEntityMpBench (U8.2); the 25 u rod product of ShearCrops (U8.3, drawn as item-rolled-rod.json's CutRivetRod1..4 at 1x1x10 vx each); the bundle convention set by `game:metalnailsandstrips` (one stack entry worth 25 u of iron and an unstated number of pieces); ExItemDef + IExItemDefProvider (src/ExpandedLib/Definitions/), StockItemDefinitions.cs:36-56 as the nearest item-def template with MaterialDensity/materialUnits/combustibleProps

**Produces:** `public class FastenerItemDefinitions : IExItemDefProvider` emitting `iwex:rivet-{metal}` as a bundle at MaxStackSize > 1 with a `smeltedRatio` that closes the ledger; `public partial class BlockRivetMachine : BlockNetworkNode, IExBlockDefProvider, IFillerHost, IFillerInteractionTarget` emitting `iwex:forming-rivetmachine-{ns|we}` over a 6x3x2 footprint; `public class BlockEntityRivetMachine : BlockEntityMpBench`

- [x] **Step 1.** Write FastenerMassTests first: a rivet bundle's declared units times its count equals the 25 u rod it came from (mass-neutral — a rivet is the rod plus a head, nothing added or lost); and the rivets-per-unit-of-iron figure is strictly better than the nail route's 4-per-100 u, because that yield advantage is the entire trade the substitution rule sells
- [x] **Step 2.** Define the rivet item. It has no art anywhere — reuse `game:item/rod` or `game:item/resource/metalnailsandstrips` geometry as a deliberate drop-in, exactly as nails and rod already do, and record that choice in the def's doc comment
- [x] **Step 3.** Write RivetMachineTests: one 25 u rod in yields one rivet bundle; a 100 u rolledrod is refused with a distinct error (it must be cropped on the shear first — the mill hands nothing to a bench directly); the stroke is torque-gated above the nail bench's gate, which is how the drawn gear train earns its keep without inventing a second ratio mechanism (the mpenergy network has exactly one ratio device, BlockEntityTransmission.cs:59-64)
- [x] **Step 4.** Author the 6x3x2 footprint from the measured art (Base spans x -45..37 = 82 vx, i.e. six cells along the shaft axis; y -9..34.2 = three cells; z -6..16 = two cells). Six cells along the shaft means five drive-bus cells, so confirm the generalised axle from U8.4 handles a bus longer than two
- [x] **Step 5.** Write BlockRivetMachine.Definitions naming `iwex:forming/riveter` per orientation; no tooling slot, no die
- [x] **Step 6.** Implement BlockEntityRivetMachine.Convert
- [ ] **Step 7.** Add the network census test that gives U8 its headline claim teeth: assert that exactly four types in the iwex assembly implement IMpEnergyConsumer (BlockEntityRollingMill plus the three benches) by reflection, so deleting one is a red test rather than a silent regression
- [ ] **Step 8.** Add the four-consumers-at-once integration test: one flywheel, a shaft run, all four machines working simultaneously, and assert the summed LoadTorque stalls the run while any three of them do not. Do not write this test with idle machines — idle friction is per-network, not per-node (MpEnergyNetworkState.cs:84), so four idle benches cost exactly what one costs and the test would pass proving nothing
- [x] **Step 9.** Add lang keys in all three locales; regenerate block codes; bless both new goldens by explicit path
- [x] **Step 10.** Run `./scripts/exmod.sh test 1.21`

#### U8.7 — The substitution rule: iwex machines accept nails or rivets, lpex's boiler accepts rivets only

> ★★ **DONE 2026-08-21.** ⛔⛔ **Step 5 is struck**: it says *"do not create any die item - state.md settled
> 2026-07-30 drops both"*. The owner amended that ruling on 2026-08-21 - dies stay, because the steam
> hammer's stamping wants them too - so the dies shipped with the benches. The **bolt** stays struck.
>
> ⛔ **Steps 2 and 3's `Fastener(qty)` helper does not exist and cannot**: Vintage Story has no OR across
> item codes, in a grid ingredient or in an RCC `requireStacks` (an AND list). Substitution is one recipe
> per fastener, the way the two gear routes already are, so what shipped is
> `RecipeIngredients.Fasteners(domain)` + a loop. Owner scoped it to **six sites** rather than every nail
> site. `ConstructionStages.RequireFastener` is likewise not possible; `RequireRivets` shipped instead,
> for the boiler's rivets-only gate.
>
> ⛔ The helper also could not live in exlib as written - it would have to name `iiex:rivet`, inverting the
> dependency. The list is iiex's; only the rivets-only RCC helper is exlib's, and it takes the rivet code
> as a parameter because the mod shipping the rivet is not always the mod building with it.


**Files**
- Create: `test/ExpandedLib.Tests/Definitions/FastenerSubstitutionTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/BoilerFastenerGateTests.cs`
- Modify: `src/ExpandedLib/Definitions/ExIngredients.cs:35-41 (add a Fastener(qty) factory beside Nails/NailsSteel)`, `src/ExpandedLib/Definitions/ConstructionStages.cs:107-115 and the shared body at :120-132 (add RequireFastener beside RequireMetalNails)`, `src/IronIndustryExpanded/Recipes/Grid/PipeRecipeDefinitions.cs:25,34,43,52`, `src/IronIndustryExpanded/Recipes/Grid/FurnaceRecipeDefinitions.cs:60`, `src/IronIndustryExpanded/Recipes/Grid/MoltenRecipeDefinitions.cs:34`
- Test: `test/ExpandedLib.Tests/Definitions/FastenerSubstitutionTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/BoilerFastenerGateTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionGoldenTests.cs`

**Consumes:** ExIngredients.Nails(qty) => `i.Item("game:metalnailsandstrips-*").Metal().Quantity(qty)` — ExIngredients.cs:36-37; ConstructionStages.RequireMetalNails(domain, qty) => `metalnailsandstrips-*` with `storeWildCard: "metal"` and `allowedVariants: ["iron","steel"]` — ConstructionStages.cs:109-110 -> :120-132; the rivet item from U8.6; the census in docs/design/items/fasteners.md § Consumer census (21 grid + 15 RCC nail sites, 13 + 14 rod sites)

**Produces:** `ExIngredients.Fastener(int qty)` emitting one recipe variant per accepted fastener code with the `metal` wildcard capture preserved; `ConstructionStages.RequireFastener(string domain, int qty)` doing the same for RCC stages; every iwex bill retargeted onto it while lpex's boiler stages stay on the rivet-only helper

- [x] **Step 1.** Write BoilerFastenerGateTests first and make it the anchor: assert that no stage of lpex's Cornish boiler (BlockBoilerCornish.cs:132-145) and no stage of hpex's Lancashire boiler (BlockBoilerLancashire.cs:146,153,158) accepts `metalnailsandstrips` under any wildcard. Nothing in the suite asserts a negative match today, so a careless widening would pass everything
- [x] **Step 2.** Write FastenerSubstitutionTests: Fastener(1) emits recipe variants matching both the nail code and the rivet code; every variant carries the same ingredient name so the `metal` capture survives; RequireFastener stores `storeWildCard: "metal"` exactly as RequireMetalNails does at ConstructionStages.cs:130
- [x] **Step 3.** Implement ExIngredients.Fastener and ConstructionStages.RequireFastener. Note the trap this walks into: recipes clash on the same pattern with overlapping ingredients, so emitting two variants of one recipe is only safe if their ingredient sets are disjoint — verify against the existing recipe-conflict guard
- [x] **Step 4.** Retarget the four iwex pipe segments (PipeRecipeDefinitions.cs:25,34,43,52), the tall hopper (FurnaceRecipeDefinitions.cs:60) and the plated molten barrel (MoltenRecipeDefinitions.cs:34) from Nails(n) to Fastener(n). `plated` stays the tier name for flanged joints — it is not a claim about the fastener
- [x] **Step 5.** Do not create a bolt item and do not create any die item. State.md § Fasteners settled 2026-07-30 drops both; docs/design/machines/heading-machine.md and docs/design/items/dies.md still describe them at length and are superseded
- [x] **Step 6.** Re-bless every affected recipe golden by explicit comma-separated path list, never EXLIB_WRITE_GOLDENS=1
- [x] **Step 7.** Run `./scripts/exmod.sh test 1.21` for iwex, lpex, hpex and exlib

#### U8.8 — Recipes and cost-catalogue rows for the three benches

**Files**
- Create: `test/IronIndustryExpanded.Tests/goldens/iiex/recipes/grid/shear.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/recipes/grid/nailmachine.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/recipes/grid/rivetmachine.json`
- Modify: `src/IronIndustryExpanded/Recipes/Grid/FormingRecipeDefinitions.cs:26 (extend Definitions to four recipes)`, `src/IronIndustryExpanded/IiexRecipeConfig.cs:47-78 (add three catalogue rows)`
- Test: `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionGoldenTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexRecipeOutputTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexCostSelectorTests.cs`

**Consumes:** FormingRecipeDefinitions.RollingMill(domain) as the exact template — ExRecipeDef.Create(domain, "grid", name).Grid(r => r.Name(..).Pattern(..).Size(3,3).Ingredient(..).OutputBlock($"{domain}:forming-rollingmill-we", 1)) at FormingRecipeDefinitions.cs:39-52; ExIngredients.Hammer / Plate(qty) / Fastener(qty); IiexRecipeConfig.Grid(match) helper at IiexRecipeConfig.cs:39-40 and the DefaultCatalogue dictionary at :45-78

**Produces:** Three new ExRecipeDef entries and three cost-catalogue keys `shear-grid`, `nailmachine-grid`, `rivetmachine-grid` matching `iwex:forming-shear-*`, `iwex:forming-nailmachine-*`, `iwex:forming-rivetmachine-*`

- [ ] **Step 0.** Do not spend `iwex:castshell` here. `castshell` is **lpex's** (water tank, ore crusher, engines), so iwex may not spend it — an iwex bench requiring an lpex item would make an optional mod mandatory. The honest fix for an orphan part is to put it where something already wants it, not to invent a consumer; nothing in iwex needs a shell.
- [ ] **Step 1.** Cost the shear as the heaviest of the three (cast bed and C-frame): castplate-heavy plus plate, Fastener(1), Hammer — it is the crop station for the whole ladder
- [ ] **Step 2.** Cost the nail machine as deliberately the cheapest: planks for the timber trestle, one castplate for the head, plate for the shears, Fastener(1). The player is meant to build six of them in a row on one shaft, so the cost must not compete with the mill
- [ ] **Step 3.** Cost the rivet machine between the two, with the SpurGearItemDefinitions gear the mill already uses (FormingRecipeDefinitions.cs:48)
- [ ] **Step 4.** Every output must name the creative-inventory default variant (`-we`, the authored unrotated orientation) — a recipe naming a non-default variant is a known repo failure mode
- [ ] **Step 5.** Add the three cost rows with the trailing `-*` wildcard; a bare code matches nothing once a variant group exists and the row goes silently inert, which has already happened once to `moltenbarrel-grid` (IiexRecipeConfig.cs:58-63)
- [ ] **Step 6.** Bless the three new recipe goldens by explicit path
- [ ] **Step 7.** Run `./scripts/exmod.sh test 1.21`

#### U8.9 — StockPile.Place in exlib — the pile-composition code the rack and the reheat hearth share

**Files**
- Create: `src/ExpandedLib/Blocks/Structures/StockPile.cs`, `test/ExpandedLib.Tests/Blocks/StockPileTests.cs`
- Test: `test/ExpandedLib.Tests/Blocks/StockPileTests.cs`

**Consumes:** The pure-function house style (StockMesh.SideOf at src/IronIndustryExpanded/BlockStructures/Forming/StockMesh.cs:36, StockMesh.CacheKey at :67-73); GameMath.MurmurHash3 for reproducible jitter; ExMesh.RotateByShape(mesh, block) as the single final rotation — BlockEntityHeatingHearth.cs:121

**Produces:** `public static class StockPile` with `public enum PileMode { Pyramid, Flat }` and `public static (Vec3f Offset, float Yaw) Place(PileMode mode, float pieceWidth, float pieceHeight, int layer, int index, int seed)`, plus `public static int PerLayer(float rackWidth, float pieceWidth)` and `public static int Capacity(float rackWidth, float pieceWidth, int layers)`

- [ ] **Step 1.** Write StockPileTests first — it is a pure function, so the whole rule set can be pinned headless with no world
- [ ] **Step 2.** Assert PerLayer = floor(rackWidth / pieceWidth) and that the worked capacity table in docs/design/machines/stock-rack.md § Numbers reproduces: 3-wide bar -> 5 per layer, 4-wide bloom -> 4, 8-wide slab -> 2, 12-wide castslab -> 1
- [ ] **Step 3.** Assert pyramid mode steps x by half a piece width per layer and y by 0.866 (sqrt(3)/2) times piece height, and that layer n holds one fewer than layer n-1 — the half-width offset is what makes the pile read as nested rather than as a grid with a gap
- [ ] **Step 4.** Assert flat mode steps y by a full piece height with one piece per layer plus yaw and xz jitter
- [ ] **Step 5.** Assert the jitter is reproducible: the same (seed, layer, index) gives the same offset every call, and two different seeds differ. An RNG here makes the pile jump on every re-tesselation
- [ ] **Step 6.** Implement StockPile as pure static maths with no VS world dependency
- [ ] **Step 7.** Run `./scripts/exmod.sh test 1.21` for exlib

#### U8.10 — The stock rack: 1x1x3 display megablock (blocked on art)

**Files**
- Create: `assets/editable/shapes/storage-megablock-stockrack.json`, `assets/iiex/shapes/forming/stockrack.json`, `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockStockRack.cs`, `src/IronIndustryExpanded/BlockStructures/Forming/BlockEntities/BlockEntityStockRack.cs`, `test/IronIndustryExpanded.Tests/Blocks/Forming/StockRackTests.cs`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/forming/stockrack.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/recipes/grid/stockrack.json`
- Modify: `src/IronIndustryExpanded/Recipes/Grid/FormingRecipeDefinitions.cs`, `src/IronIndustryExpanded/IiexRecipeConfig.cs:45-78`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`, `src/IronIndustryExpanded/Generated/IiexBlocks.g.cs`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Forming/StockRackTests.cs`

**Consumes:** StockPile.Place / PerLayer / Capacity (U8.9); BlockFilledMegastructure — `abstract class BlockFilledMegastructure : Block, IFillerHost` with the place/break triad folded in and `public abstract int StructureAngle` (src/ExpandedLib/Blocks/Structures/BlockFilledMegastructure.cs:31-96) — usable here because the rack is not a network node; StructureFootprint.Rectangle(halfWidth, depth) (StructureFootprint.cs:51-69); BlockEntityHeatingHearth.cs:26-174 end to end as the BE template (slot array :30, TryLoad :57, TryTake :73, OnTesselation :99-126, per-slot SetItemstack + ResolveBlockOrItem :132-154, block-info readout :160-171)

**Produces:** `public partial class BlockStockRack : BlockFilledMegastructure, IExBlockDefProvider, IFillerInteractionTarget` emitting `iwex:forming-stockrack-{n|e|s|w}` over `StructureFootprint.Rectangle(0, 3)`; `public class BlockEntityStockRack : BlockEntity` with `public bool TryLoad(ItemStack)`, `public ItemStack? TryTake()`, `public int TotalUnits { get; }`

- [ ] **Step 1.** Caution: blocked first step: draw the rack. Nothing under assets/editable/shapes/ is a rack — this is the one genuinely undrawn deliverable in U8 (the plan's 'art needed: shear bench, rivet machine, stock rack' is right about the rack and wrong about the other two). It is a plank frame only; the pile is composed at runtime from each stored item's own shape, so no pile art is authored
- [ ] **Step 2.** Export with convert-shape.py once drawn — IiexDefinitionGoldenTests.Every_shape_reference_resolves_to_a_shipped_file will fail on a def naming a shape that does not ship
- [ ] **Step 3.** Write StockRackTests first: RMB with stock on any of the three cells loads; RMB empty takes the top piece (LIFO); a non-stock item is refused; the block-info readout reports the running unit total
- [ ] **Step 4.** Write the drop test and treat it as the highest-value test in the task: breaking any cell must spawn every stored piece before base.OnBlockBroken. A rack that eats 15 000 u of slabs on a misclick is the worst bug this block can have and it is one missing line (the mill's precedent is BlockEntityRollingMill.cs:374-383)
- [ ] **Step 5.** Decide `allowAttach` on the two filler cells before writing the footprint. StructureFootprint's fillers default to AllowAttach: false (StructureFootprint.cs:33) and Rectangle only opts in flanking columns (:64) — with halfWidth 0 there are none, so as written the rack cannot be stacked vertically, which contradicts 'racks stack vertically, so shelving is just racks'
- [ ] **Step 6.** Implement OnTesselation composing the pile from each item's own shape via StockPile.Place, translating before the single final ExMesh.RotateByShape, and returning true so the default block mesh is not also drawn
- [ ] **Step 7.** Use the five-arg ShapeTextureSource, not ITesselatorAPI.GetTextureSource(Item): the latter returns item-atlas UVs and a mesh handed to ITerrainMeshPool needs block-atlas ones. BlockMoltenBarrel.cs:190 is the in-repo precedent for runtime texture insertion plus a cached base mesh
- [ ] **Step 8.** Cost the recipe at four planks for two racks — no metal, no tool, no fastener. A rack returns no operating efficiency so it has nothing to sell, and it must never compete with a machine for materials
- [ ] **Step 9.** Add lang keys in all three locales; regenerate block codes; bless both goldens by explicit path
- [ ] **Step 10.** Run `./scripts/exmod.sh test 1.21`

#### U8.11 — Doc surgery: strike the die family, the bolt and the 1x1x1 bench claims

> ★★ **DONE 2026-08-21. Steps 1-2 are inverted by the owner's die ruling** - dies stay, so nothing is
> struck from `dies.md` and `heading-machine.md` is not deleted; it records the resolution and shrinks
> to bearing balls, the one headed fastener neither built bench makes. Everything else landed: a new
> `rivet-machine.md` (Step 1's intent without the die deletion), `nail-machine.md` (Steps 4-5),
> STATE.md's own contradiction (Step 6), `shear.md` (Step 3, and five further sections that had gone
> stale around it), and the handbook page (Step 7) as **one page for the whole shop**,
> `docs/iiex/handbook/10-formingshop.html`, in three locales.


**Files**
- Create: `docs/design/machines/rivet-machine.md`
- Modify: `docs/design/machines/heading-machine.md (delete — content superseded and renamed)`, `docs/design/items/dies.md (strike the iwex/lpex/hpex die rows and the ItemDie contract)`, `docs/design/items/fasteners.md (strike the bolt, the die column, and the stale asset table naming item-rod-rolled.json / item-rod-nail.json)`, `docs/design/machines/shear.md § Assets and § Structure`, `docs/design/machines/nail-machine.md § Assets and § Structure`, `docs/internal/plans/STATE.md:596-599 (the placement table still lists 'heading machine (bolts) | iwex' and 'rivet die | lpex', contradicting :627 in the same file)`
- Test: `test/ExpandedLib.Tests/Localization/HandbookParityTests.cs`

**Consumes:** docs/internal/plans/STATE.md:620-649 § 'Fasteners — settled 2026-07-30: two machines, two routes, no dies' as the authoritative text; the handbook sync pipeline joining docs/<mod>/handbook/*.html to lang/en.json on the NN- prefix (test/ExpandedLib.Testing/HandbookSync.cs)

**Produces:** A machines/rivet-machine.md that owns the rivet bench with no die catalogue; dies.md and fasteners.md with the die family and the bolt struck; shear.md and nail-machine.md whose Assets and Structure sections match the drawn art

- [ ] **Step 1.** Rename heading-machine.md to rivet-machine.md and rewrite it against state.md:627 — the machine is a rivet header, rod @ 25 u in, rivets out, iwex, no tooling slot
- [ ] **Step 2.** Strike every die row from dies.md and fasteners.md, and strike the bolt item, the bolt die and the ball die. Record that `plated` survives as the tier name for flanged joints only
- [x] **Step 3.** Correct shear.md § Assets. ⛔ The Assets claim had already been corrected on 2026-08-13; what was still false on 2026-08-21 was everything the *build* invalidated - the runtime shape, the lang keys, an orphaned handbook row, the art brief, § Construction (*'there is no recipe'*), § Numbers (*'no config section, no keys and no code'*), § Code (*'nothing exists'*), § Drops (*'there are no fillers'* - there are five), the status header's B3c claim, and § Role's *'the mill has no product stage'*, which moved to `MillSchedule` and has a caller
- [x] **Step 4.** Correct nail-machine.md § Assets: the file is machine-mp-megablock-nailcutter.json (not machine-megablock-nailcutter.json), it now carries idle and cycle animations (the page says 'none in the file'), and its groups are Base / MachineCasing / ShaftGroup / CutterGroup / NailTray (the page says Base / Machine / ShaftGroup)
- [x] **Step 5.** Correct all three § Structure sections (done for the three *built* benches; heading-machine.md's 1 × 1 × 1 is now marked unverified rather than corrected, because that bench has neither art nor a layout). Original text: Correct all three § Structure sections: every one specifies '1 x 1 x 1, no fillers, no multiblock, no projection'. The drawn art is 3x2x2, 2x3x3 and 6x3x2 and the filenames say megablock. Record the drive-bus consequence — a bench wider than one cell along the shaft needs real BlockNetworkNode cells because a filler can never be a graph node
- [x] **Step 6.** Fix state.md's placement table at :596-599 so it stops contradicting its own settled section 28 lines later
- [x] **Step 7.** Add the handbook page for the forming shop and its NN- prefixed lang keys. Shipped as page **10**, after the steam-power pages rather than beside the ironworking ones, because the shop cannot run before the drive that turns it. ⛔ The sync test would **not** have caught the absence: `Problems()` only reports an orphan on one side, and a page with neither a source nor a descriptor is invisible to it
- [x] **Step 8.** Run `./scripts/exmod.sh test 1.21`

### Traps — each of these makes a green suite a lie

- The torque gate is U8's only reason to exist mechanically, and the repo already has the exact failure precedent. RollSetSpec.MinTorque is parsed and stored with zero readers (RollSetSpec.cs:37), and RollingPass.CanCarry has four passing assertions and zero callers (RollingPass.cs:126 vs RollingPassTests.cs:147-155). If U8 tests the gate only as a pure function, a refactor can delete the call site and the suite stays green. Every bench needs an integration test on a placed machine wired to a real flywheel run that refuses to stroke below the gate.
- A `cycle` clip authored with onAnimationEnd EaseOut makes the running mesh vanish in game. All three editables carry EaseOut; scripts/tools/convert-shape.py:117-121 rewrites it, but only for exports that go through the script. A hand-copied shape passes every C# test in the repo and is broken only in game. Nothing today asserts anything about an emitted clip's onAnimationEnd.
- EnergyAnim.SpinSpeed (EnergyAnim.cs:23-24) divides omega by 2*pi on the assumption of exactly one revolution per clip. The three new cycle clips are 60 frames where the mill's is 30. A wheel authored as two revolutions animates at half the true shaft speed with no error, no log line and no failing test — the animation silently stops being a readout of the run.
- The mass ledger is the whole no-minting argument the forming line is balanced on, and nothing enforces it. 4 nails per 100 u nailplate, 4 rods per 100 u rolledrod, one mass-neutral rivet per 25 u rod. Without a headless invariant summing input units against output units across every bench conversion, a one-line balance tweak mints iron and every test still passes.
- Widening a fastener wildcard is never cosmetic. ExIngredients.Nails names its ingredient `metal` (ExIngredients.cs:37) and ConstructionStages stores `storeWildCard: "metal"` (:130), so which fastener goes in decides what later RCC stages and drops resolve to. A substitution helper that drops the capture breaks stages nobody thought to test.
- Nothing in the suite asserts a negative ingredient match. The substitution rule is only real if lpex's boiler refuses nails; a widening that accidentally lets `metalnailsandstrips` satisfy a rivet slot passes every existing test in every mod. The refusal test must be written before the helper.
- A megablock that eats its contents on break is the worst bug in this family and it is one missing line. The three benches have output trays and the rack holds up to 15 000 u of slabs; breaking any cell reroutes to the principal, so the principal's OnBlockBroken must spawn everything before base. The mill's precedent (BlockEntityRollingMill.cs:374-383) covers a jammed piece and a roll set; nothing covers a tray, because no machine has had one.
- Dropping the drive bus reads as a harmless simplification and passes everything. A bench whose only graph node is its principal still places, still ticks, still converts, still passes every unit test — and simply cannot be chained on a line shaft, which is the entire 'hall of identical machines on one shaft' design. Only a placement-plus-network-walk integration test catches it (a filler can never bridge the graph — BlockRollingMillAxle.cs:17).
- Never force StructureComplete in a bench or rack test. Build the real footprint with TestWorld.Place + AddNode and let the machine complete itself; a forced flag hides exactly the placement and filler-spawn bugs these tasks are most likely to introduce.
- Four idle consumers cost exactly what one costs (MpEnergyNetworkState.cs:84 computes friction once per run from the shaft speed). A 'the network is now real' test built on four idling benches passes trivially and proves nothing. Put all four in a working state.
- Goldens and the generated block table must be regenerated deliberately. EXLIB_WRITE_GOLDENS takes a comma-separated path list — never =1, which re-blesses the whole repo and launders unrelated drift into the commit. IiexBlocks.g.cs is regenerated with EXLIB_WRITE_BLOCKCODES=1 and must never be hand-edited.
- A def can name a shape that resolves but is the wrong shape. Every_shape_reference_resolves_to_a_shipped_file only checks existence, so a copy-pasted `iwex:forming/rollingmill` on the shear passes. Verify each def names its own export by eye.
- `side` and `orientation` variant groups spell single letters. The mill uses orientation `ns`/`we` (two letters = two connector faces) and the rack will use side `n|e|s|w`. Anything spelling words breaks the connector derivation and the golden at once.
- Use `./scripts/exmod.sh test 1.21`. `latest`/1.22 reports about twenty IPlayer-mocking failures from VS 1.22.6 that are upstream, not real, and they will mask or invent U8 regressions.

**Gate.** An iwex-only player can run the full fastener loop with no creative mode and no anvil: shingled bar → mill (grooved) → rolledrod at 100 u → shear crops it into four 25 u rods → rivet machine heads them into rivets; and rolledrod → mill (flat) → nailplate at 100 u → nail machine cuts and heads exactly four `game:metalnailsandstrips`; both routes conserve mass exactly and neither beats 4 fasteners per 100 u. All four machines sit on one line shaft driven by one flywheel, and running all four simultaneously stalls the run while running any three does not. A wall of stock racks displays bar, bloom, slab and castslab with visibly different silhouettes, and breaking any rack cell returns every piece. `./scripts/exmod.sh test 1.21` is green with no golden re-blessed by wildcard, `IiexBlocks.g.cs` regenerated rather than hand-edited, and lang keys present in en, ru and uk. Reflection confirms exactly four `IMpEnergyConsumer` implementations in the iwex assembly.

---

# U9 - Coke oven and crucible steel

U9 ships the two natural-draught machines that bracket the iron tier: the beehive coke oven (bulk `game:coke` from bituminous coal, two sealed chambers, lid-gated) and the Huntsman crucible furnace (4 fireclay pots x 100 u crucible steel per heat, coke-fired, stack-height sets temperature). Everything either machine needs from the framework already exists except the stack-height draught function, which is **U6.5's** — together with U6.4's per-machine loss virtuals — and those two are the hard entry gate (not U6.3, which produces neither — see entry condition #1) - `BlockEntityFurnaceCore.cs:1229` still reads the flat `IiexValues.BfNaturalDraughtFactor = 0.5f`, which caps a firebox at T_process ~1082 C against the ~1600 C this process needs. The art position is much better than the plan says: the crucible hearth, the pot, the crushed blister chunk and the cast-iron ingot mould are all drawn, and the coke oven needs no model art at all (its core is a plain cube; lid, door and hopper are shipped blocks). The real content work is two blocktypes + layouts per machine, two block entities, one new metal def, one clayformed pot block, one anvil route, recipes/costs/lang, and the tests that stop a green suite lying about any of it.

**Entry condition.** Three things, all outside U9:

1. **U6.4 and U6.5 landed — not U6.3**, which produces neither of the two things this condition actually requires. `NaturalDraughtFor(int courses, bool damperOpen, bool venting)` — **three** parameters, not the two this condition used to state — must have replaced the flat read at `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs:1229` (`float natural = IiexValues.BfNaturalDraughtFactor;`, default `0.5f` at `src/IronIndustryExpanded/IiexConfig.cs:207`), the counted stack walk must exist and must consume `CellRole.Flue` (`src/ExpandedLib/Blocks/Structures/CellRole.cs:124`) and `CellRole.Damper` (`:131`) - **both roles exist and neither has a single consumer in `src/` today** - and per-machine charge loss must be a virtual over `IiexValues.BfChargeLossFull` (read at `BlockEntityFurnaceCore.cs:1241-1244`). Without all three the crucible furnace ships lit-and-never-melting.

2. **U2/U3's ruling-2 rework settled on the firebox branch.** `BlockEntityFireboxFurnace.cs:197-199` overrides `MaxFuelBurnTime`/`MeltStartDelay`/`MeltIntervalSec` from `IiexValues.Bf*`. Ruling 2 makes those emergent on the shaft branch; U9's two machines inherit those three lines, so whatever replaces them must be decided before either block entity is written, or both get rewritten immediately.

3. **U1 complete** for U9.11 only (the cast-iron ingot mould is a sand-casting pattern; `PatternItemDefinitions.Molds` at `src/IronIndustryExpanded/BlockStructures/Casting/PatternItemDefinitions.cs:86`).

U9.1-U9.4 (the coke oven) only strictly need #1 and #2. U9.5-U9.11 need all three.

**Shared files** (collision risk): `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnaceCore.cs - U2, U3, U4 and U6 all rewrite this file; U9 only reads it (ComputeHeatBalance :1211, natural :1229, cell-role accessors :373-429, ComponentScan* :1712-1713). Do not overlap.`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs - U6 owns the branch; both U9 machines derive from it and depend on its sealed members (:62, :71, :241-242) and its tunables passthrough (:197-199).`, `src/IronIndustryExpanded/IiexConfig.cs - every unit adds keys; U6.3 rewrites BfNaturalDraughtFactor (:207) and BfChargeLossFull (:225) out from under U9.`, `src/IronIndustryExpanded/Generated/IiexBlocks.g.cs - generated. Every unit that adds a blocktype regenerates it; two units regenerating in parallel will produce a conflicting file. Regenerate last, never hand-edit.`, `src/IronIndustryExpanded/Recipes/Grid/FurnaceRecipeDefinitions.cs:16-17 - U6 adds the puddling/hearth recipes here too; U9 adds two more groups to the same array.`, `src/IronIndustryExpanded/IiexRecipeConfig.cs:46-77 - one Defaults() dictionary, every unit appends rows.`, `src/IronIndustryExpanded/BlockStructures/Casting/PatternItemDefinitions.cs:72-171 - U1 owns the casting route and has already added castbillet/castbloom/castslab; U9.11 adds one more Molds row for the ingot mould.`, `assets/iiex/lang/{en,ru,uk}.json - every unit appends keys; three files, all three required by IiexLangCoverageTests.`, `scripts/tools/convert-shape.py - U7 (rolled stock) and U8 (the three MP machines) also need TEXTURES entries; U9.5 adds blistersteel and slag1. One dict, three units.`, `scripts/test-floors.txt - one row per suite; every unit that adds tests should ratchet it, and two units editing it in parallel conflict.`, `test/IronIndustryExpanded.Tests/Fixtures/FurnaceLayoutRig.cs - the shared oracle for every furnace in the line, including smex's; U6 and U9 both add glyph constants to it.`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceRoleCellsTests.cs:733-758 and FurnacePartsTests.cs:33-51 - the two role/code oracles both U6 and U9 must extend and whose floors both must raise.`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockChargeDoor.cs - the coke oven consumes its lid and door defs; U6 consumes the puddling door. Read-only for U9.`, `docs/internal/workbench/layouts.md - the two U9 drafts sit at :518-655; U6 edits the puddling/heating drafts in the same file.`, `docs/design/machines/{coke-oven.md,crucible-furnace.md} and docs/design/layered-charge.md - U2/U3 rewrite layered-charge's state-machine section under U9's feet.`

### Tasks

#### U9.1 — Coke-oven core blocktype + multiblock layout (no cycle yet)

> ★★ **DONE 2026-08-21.** The blocktype, the layout, `BlockEntityCokeOven` (charge and ignition only,
> `SmeltCycle` empty), the `co` face label, three locales, the golden, the regenerated block table and 14
> tests all ship; 9/9 green at 4,442 per version. ⛔⛔ **Three of the plan's own instructions were wrong**,
> each recorded on the step. ⛔ **No tier group**: the oven is fire brick throughout, so Step 3's
> `"tier1","tier2","tier3"` would have minted three indistinguishable blocks.


**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockCokeOvenCore.cs`, `assets/iiex/textures/block/furnace/co.png`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/cokeovencore.json`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/CokeOvenLayoutTests.cs`
- Modify: `src/IronIndustryExpanded/Generated/IiexBlocks.g.cs`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`, `docs/internal/workbench/layouts.md:590-660`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/CokeOvenLayoutTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnacePartsTests.cs:33-51`, `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionGoldenTests.cs`

**Consumes:** BlockFurnaceCoreBase.Core(string domain, string code, string? type, string path, params string[] brickTiers) - src/IronIndustryExpanded/BlockStructures/Furnaces/BlockFurnaceCoreBase.cs:58-64; BlockFurnaceCoreBase.FurnaceCode = "furnace" :43; IiexBlocks.FurnaceChargedoor.WithSide(BlockFacing) / FurnaceChargelid.WithSide(BlockFacing) / HopperTall.WithSide(BlockFacing) - src/IronIndustryExpanded/Generated/IiexBlocks.g.cs:252,279,704; VanillaCodes.FireBricks :91, FireSlab(BlockFacing) :170, Air :50; ExCodes.Filler :27

**Produces:** iwex:furnace-cokeovencore-{tier}-{side} + IiexBlocks.FurnaceCokeovencore.{Code,Any,WithSide}; the two-chamber layout with CellRole.Firebox on the chamber cells and CellRole.Flue on the crown void

- [x] **Step 1.** Read the draft at docs/internal/workbench/layouts.md:590-655 and treat every legend in it as wrong - its legends predate the firebox and charge-shaft cutovers. Rewrite: 'iwex:beehiveovencore-north*' -> IiexBlocks.FurnaceCokeovencore.Any, 'iwex:chargedoor-north*' -> IiexBlocks.FurnaceChargedoor.WithSide(BlockFacing.SOUTH), 'iwex:chargelid-south*' -> IiexBlocks.FurnaceChargelid.WithSide(...), IiexCodes.HopperTall(BlockFacing.SOUTH) -> IiexBlocks.HopperTall.WithSide(BlockFacing.SOUTH) (IiexCodes.HopperTall was deleted, IiexCodes.cs:37-44), ExCodes.FireBricks/FireSlab/Filler/Air -> VanillaCodes.* / ExCodes.Filler. Delete the dead 'K' legend.
- [~] **Step 1b.** ⛔ **Deferred to U9.2**, where the block entity exists. The accept list is a property of the *charge*, and `BEBehaviorFirebox.IsFuel` is `static` and reached from `BlockFirebox.OnBlockInteractStart`, so the filter needs a seam through the owning core - which U9.1 has no cycle to hang it on. Original text: **Ruled 2026-08-05: only bituminous coal cokes.** Lignite and anthracite do not. **So the chamber cannot inherit `BEBehaviorFirebox`'s fuel filter** — that filter refuses lignite but *accepts* anthracite, which is now wrong in the other direction, and it is a **reverberatory-fuel** list never designed for a retort. Give the chamber its own accept list. The firebox keeps refusing lignite where that is correct: as **fuel**. This makes coal type a real prospecting constraint on entering the iron tier — chosen deliberately as the historically sharp reading; `coking.md`'s "all three vanilla coal grades feed the same oven" row is updated to match.
- [x] **Step 2.** ★★ **Confirmed by the owner 2026-08-21 against `coke-oven.md`, which said the opposite.** The design page's 2026-08-03 settlement made the charge `@(air|coalpile)`; this step's file-reference line made it `FurnaceFirebox` with `CellRole.Firebox`. The plan won, on a fact neither document had: `BlockEntityCoalPile.TestCokable` needs a closed `game:cokeovendoor` beside each pile **and** twelve coke-oven-viable blocks in that pile's own 3x3x3, so vanilla's conversion could never have fired in a bulk chamber and the pile would have been storage only. Original text: **Ruled 2026-08-05 — neither earlier option stands.** **The oven is two sealed chambers, charged with coal piles and lit without access to air.** It is **vanilla's own coking process**, scaled up — not a new simulation. The mod's contribution is **efficiency and scale, not a different mechanism**: the **arched ceiling** makes it more efficient, and the chamber holds **many piles per charge** instead of vanilla's one block. The player gets coke **faster and in larger quantities**; that is the whole value proposition and it needs no new physics. **Rejected: the "self-heated, air-regulated beehive"** where a door aperture trades yield against burn-off — do not build an air-regulation control. **Rejected: gating the cycle on a coking temperature off the shared heat balance** — riding vanilla's process means there is no `CokeOvenTempC` to invent and no transfer-loss override to derive, which is what made that option expensive. **And no regenerator.** Regenerative practice is **much later historically**; if it is ever built it is a **separate regenerative coke oven belonging to smex**, not a mode of this one. Do not reason from "a beehive oven burns part of its own charge, so Firebox is not a lie" — the chambers are sealed and air-free. If `BEBehaviorFirebox` is still the right host it is because it is a fuel-bed abstraction, **not** because anything is burning. File references: the `c` cells hold iwex:furnace-firebox-*-* (IiexBlocks.FurnaceFirebox.Any) marked CellRole.Firebox, not ExCodes.CoalBed; BEBehaviorFirebox accepts bituminous/anthracite/charcoal/coke and refuses lignite (BEBehaviorFirebox.cs:55-82). Step 1b rules **only bituminous cokes**, so the chamber needs its own accept list regardless.
- [x] **Step 3.** ⛔ **Built with no `tier` group**, against this step's `"tier1","tier2","tier3"`: the oven is fire brick throughout (vanilla's own coke-oven masonry, and it must stay cheap because it gates everything after it), so there is no tiered brick in the drawing for a tier variant to match and three tiers would be three indistinguishable blocks. Original text: Write BlockCokeOvenCore.cs modelled line-for-line on src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockHeatingFurnaceCore.cs:35-141: Core(domain, FurnaceCode, "cokeovencore", "furnace/cokeovencore", "tier1","tier2","tier3") then .Class/.EntityClass/.Texture(all|north|south)/.MultiblockLayout.
- [x] **Step 4.** Built rather than drawn: the `c` is lifted verbatim from `cf.png` and the `o` is that same `c` unioned with its own mirror, so both letters carry the shipped set's exact stroke weight, palette and anti-aliasing instead of a newly-drawn approximation. Original text: Draw assets/iiex/textures/block/furnace/co.png as a 2-letter label to match the shipped set (bc, bfc, bfh, cf, cfd, cs, hf, n, pf, ss in assets/iiex/textures/block/furnace/) and wire it as the south face.
- [x] **Step 5.** Already enforced - `FurnaceFillerAccountingTests` walks every furnace layout at definition level, which is where the puddling and reheat mismatches were caught. 2 declared, 2 produced. Original text: Re-check the filler accounting the design page claims balances: each hopper-tall emits one filler and each chargedoor emits one via BlockChargeDoor.UpperHalf (BlockChargeDoor.cs:80-92); the chargelid emits none (Lid(...) at :100-105 deliberately never calls FillerOffsets). Count declared `f` cells against produced ones and fail the layout if they differ - U6.1 exists because two shipped layouts got this wrong.
- [x] **Step 6.** Add the lang keys block-furnace-cokeovencore-* and blockdesc-furnace-cokeovencore-* in all three locales (IiexLangCoverageTests requires every code to resolve in every locale).
- [x] **Step 7.** ⛔ Bootstrap needed: the layout names `IiexBlocks.FurnaceCokeovencore.Any` and the table is generated from the def that names it, so the legend goes in as a literal, the table regenerates, then the helper replaces the literal. Original text: Regenerate the block-code table: EXLIB_WRITE_BLOCKCODES=1 dotnet test test/IronIndustryExpanded.Tests
- [x] **Step 8.** Bless the golden: EXLIB_WRITE_GOLDENS=test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/cokeovencore.json ./scripts/exmod.sh test 1.21
- [x] **Step 9.** In CokeOvenLayoutTests, stand the structure up with FurnaceLayoutRig.Stand (test/IronIndustryExpanded.Tests/Fixtures/FurnaceLayoutRig.cs:199) at all four sides and assert it completes without forcing StructureComplete; assert the two chambers are 6 cells each and carry CellRole.Firebox via FurnaceLayoutRig.RoleCellsOf (:364).
- [x] **Step 10.** Raised to 15. Original text: Raise the `checkedCodes >= 12` floor in FurnacePartsTests.cs:50 to reflect the fifth structure.

#### U9.2 — BlockEntityCokeOven - the coking cycle on firebox beds

> ★★ **DONE 2026-08-21.** The bake, the per-chamber clocks, the accept list (U9.1's deferred Step 1b),
> `CokeOvenCycleSec` / `CokeOvenYieldFrac` / `CokeOvenCokingCoals` and 21 tests. 9/9 green at **4,463** per
> version. ⛔ **Away-catch-up needed no work** (Step 6): `BEBehaviorProductionMachine` already replays the
> unloaded gap through `GameTime.CatchUp` before the first real tick, and `SmeltCycle`'s `dt` is the
> interval it stands for, so a caught-up bake integrates identically. ⛔ **`BurnOutCharge` had to be
> overridden** - the branch's version keeps only a fraction of the bed as salvage, which would have eaten
> the charge of any oven that went out before reaching its light temperature.


**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityCokeOven.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/CokeOvenCycleTests.cs`
- Modify: `src/IronIndustryExpanded/IiexConfig.cs`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockCokeOvenCore.cs`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/CokeOvenCycleTests.cs`

**Consumes:** BlockEntityFireboxFurnace.CollectCharge() -> List<(BlockPos,BEBehaviorFirebox)> :90-100; ReadChargeMix(object, out bool, out BurdenMix, out int, out string?) :120-140; TryIgniteCharge(object) :155-164; BurnOutCharge() :177-189; sealed MinChargeToIgnite => FireboxCellCount * IiexValues.FireboxMixPerCell :241-242; BEBehaviorFirebox.Units :109, FuelCode :114, IsFull :129, TryAdd(ItemStack,int) :167, Consume(int) :202, Clear() :220, CellCapacity :102

**Produces:** BlockEntityCokeOven : BlockEntityFireboxFurnace with a per-chamber coking cycle; IiexValues.CokeOvenCycleSec, CokeOvenYieldFrac, CokeOvenTempC

- [x] **Step 1.** Write the failing tests first. Minimum set: (a) a chamber full of game:ore-bituminouscoal at the coking temperature for CokeOvenCycleSec ends with every bed's FuelCode == the coke code and Units == round(before * CokeOvenYieldFrac); (b) a chamber that is not full does not start; (c) a chamber of charcoal or coke does not convert (already coke / never coal); (d) mass conservation - the yield fraction is applied exactly once per chamber per cycle, asserted over two consecutive cycles, not one (the dirty-precondition rule).
- [x] **Step 2.** Derive BlockEntityCokeOven from BlockEntityFireboxFurnace (not from BlockEntityFurnaceCore directly): the branch seals ShaftHoldsLayeredCharge=false :62, AcceptedFamilies=null :71 and MinChargeToIgnite :241, which are the three invariants a retort also wants.
- [x] **Step 3.** Caution: **Re-scoped 2026-08-05 by the U9.1 Step 2 ruling — there is no coking temperature.** The oven rides **vanilla's coking process**, so `CokeOvenTempC` is **not created** and `MeltingPoint` is **not** overridden to it. That is the whole saving of the ruling: a temperature gate would have needed a stated coking temperature *and* a ≈0 transfer-loss override, and without both the oven strands at ~1042 °C on flat draught and blocks every coke-fired machine downstream. Still do: override `ShaftCentre` (cf. `BlockEntityHeatingFurnace.cs:38`) to the shared wall, and implement `SmeltCycle(object chargeHandle)` — the hook `BlockEntityHeatingFurnace.cs:54` leaves empty — to walk the beds and convert on the **cycle timer plus the sealed gate**, not on a temperature.
- [x] **Step 4.** Built as a flood fill on face adjacency over `CellRole.Firebox` cells, ordered by their lowest cell so an index survives a reload. ⛔ The obvious test (charge the west, only the west cokes) **does not discriminate** - an oven with one shared clock passes it too, since an empty chamber converts nothing either way. The case that bites charges the east **late**, mid-bake, and checks the west finishes while the east does not; mutation-checked. Original text: Handle the two-chamber question the design page leaves open (coke-oven.md Open #7): implement per-chamber state by grouping FireboxCells (BlockEntityFurnaceCore.cs:390) into connected components rather than adding a second FSM. One cycle per component; assert in a test that filling only the west chamber cokes only the west chamber.
- [x] **Step 5.** ★★ **Vanilla's own numbers, read from `.game/`**: bituminous cokes at **0.75** and lignite at **0.5** (`itemtypes/resource/ore-ungraded.json`, `cokeConversionRateByType`); anthracite has no entry and cokes in neither. Ours is **0.9**, truncated as vanilla truncates, so a 12-unit cell yields 10 - an effective 10/12 against vanilla's 0.75, and never a mint. Scale: **144 units per oven against a vanilla pile's 16**. Both pinned. ⛔ The **cycle length** is not vanilla's 12 game hours: while a chunk is loaded the production tick counts real seconds and only the away-catch-up counts game ones, so a literal 12 game hours is a twelve-hour real wait. Sized against the mod's own clock instead (3600 s, three firebox charges) and left untuned - the case against vanilla rests on yield and scale, which need no clock. Original text: **Set the yield and the rate against vanilla — and after the 2026-08-05 ruling this is the task's centre, not a footnote.** The oven's entire value is **efficiency × scale over vanilla's one-block oven**, so both must be measurably better and both must be justified in a doc-comment: `CokeOvenYieldFrac` ≥ vanilla's coal→coke ratio *(the **arched ceiling**, stated as the reason)*, and piles-per-charge ≫ 1 *(the **scale**)*. The design's own bar — *"the bulk oven must be a better rate, not merely a bigger box"* — is now the acceptance test rather than an aspiration: pin **both** against vanilla in `CokeOvenCycleTests`, because a bigger box at the same rate is exactly the failure this ruling says the machine must not be. Add the keys to IiexConfig.cs beside the Bf*/Cupola* blocks with the same doc-comment density.
- [x] **Step 6.** Already inherited - `BEBehaviorProductionMachine.RunAwayCatchup` replays the unloaded gap through `GameTime.CatchUp` before the first real tick, and the bake integrates `dt`, so nothing teleports. Original text: Adopt the away-catch-up model the furnaces use on reload, or a chamber teleports through its cycle; pin it with a test that advances the game clock across a save/load.
- [x] **Step 7.** ./scripts/exmod.sh test 1.21

#### U9.3 — The lid gate, the drops, and the HUD - IsVenting gets its first consumer

> ★★ **DONE 2026-08-21.** The seal gate, per-chamber closure resolution, the per-chamber HUD lines in
> three locales, the drops case and 12 tests. 9/9 green at **4,475** per version. `IsVenting` has its first
> consumer in `src/`. ⛔ **The gate broke every U9.2 cycle test**, correctly - their rig had no lids, and a
> missing closure reads as open - which is the clearest possible evidence the gate is real; the closure
> placer moved into `FurnaceLayoutRig` so both suites share one definition of where the lids are.
> ⛔ This step's `IsVenting => MainOpen || SmallOpen` is stale: the shipped property is `MainOpen` alone,
> deliberately, since not dumping the heat is the whole reason the small door exists.


**Files**
- Create: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/CokeOvenLidGateTests.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityCokeOven.cs`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityChargeDoor.cs`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/CokeOvenLidGateTests.cs`

**Consumes:** BlockEntityChargeDoor.MainOpen :23, SmallOpen :26, HasSmallDoor :29, IsVenting => MainOpen || SmallOpen :44, ToggleMain() :49; BlockEntityFurnacePart.Core / ResolveOwningAnchor() (src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntityFurnacePart.cs:37,50); BlockEntityFurnaceCore.GetBlockInfo :1781 and AppendReadyInfo :1948

**Produces:** a sealed-chamber predicate on BlockEntityCokeOven that reads the lid and door of each chamber; the R7 block-info readout naming which chamber is unsealed

- [x] **Step 1.** Both halves pinned, plus the drawing door - a gate that read only the lid would let a player bake with the door standing wide - and a chamber whose lid was never placed, since treating a missing closure as shut would let the gate be demolished rather than obeyed. Mutation-checked: deleting the gate fails four. Original text: Write the failing test first: charge and light a chamber, toggle its lid open (BlockEntityChargeDoor.ToggleMain :49), advance CokeOvenCycleSec, assert the bed is still bituminous. Then shut it and assert it converts. Both halves are required - a test that only asserts 'sealed cokes' leaves the gate free to be deleted, and IsVenting has zero consumers in src/ today (BlockEntityChargeDoor.cs:44).
- [x] **Step 2.** Resolved by proximity to each chamber rather than by a table per facing, so the pairing survives rotation and an oven of three chambers would need no code. The warning was right and is now pinned by a test: a lid stands **three** courses above the anchor against `ComponentScanAbove = 1`, so the core must find the lid and never the reverse. Original text: Resolve the lid and drawing door per chamber by position from the layout rather than by scanning: the core already knows its own cells (BlockEntityFurnaceCore.GlobalOf / CellsWithRole). Do not let the lid search for the core - it is a BlockEntityFurnacePart and ComponentScanAbove = 1 (BlockEntityFurnaceCore.cs:1713), so a lid three cells above the core cannot resolve it.
- [x] **Step 3.** The per-chamber lines went in `AppendHeatExtras`, which is the lit block, rather than `AppendReadyInfo`, which is only the full-but-unlit line: an oven refusing to coke is lit, so the ready line would never have shown it. Original text: Implement AppendReadyInfo (the abstract at BlockEntityFurnaceCore.cs:1948) to name the unsealed chamber, and add a per-chamber progress line. R7 is 'nothing is hidden': an oven that silently refuses to cok is indistinguishable from a bug.
- [x] **Step 4.** Drops: assert the chamber contents survive the core being broken. The firebox blocks are ordinary world blocks and drop themselves, but a test must pin it - the puddling and reheat hearths' silent-destruction hole is the mistake this page names explicitly.
- [x] **Step 5.** Add lang keys for the new HUD lines in all three locales.
- [x] **Step 6.** ./scripts/exmod.sh test 1.21

#### U9.4 — Coke-oven construction: recipe, cost row, handbook

> ★★ **DONE 2026-08-21, and the coke oven is craftable.** The core recipe, the chargelid recipe, two cost
> rows, the golden and handbook page 11 in three locales. 9/9 green at **4,479** per version.
> ⛔ **This step's own correction was itself out of date**: it says `iiex:furnace-chargedoor` has no recipe
> either and belongs to U6.11 - it *had* one already (`FurnaceRecipeDefinitions.cs:219`, cost row
> `chargedoor-grid`), which U6.11 shipped. Only the **chargelid** was missing, exactly as the original
> wording said. Verified by grep, as the step asked.


**Files**
- Create: `docs/iiex/handbook/NN-cokeoven.html`
- Modify: `src/IronIndustryExpanded/Recipes/Grid/FurnaceRecipeDefinitions.cs:16-17`, `src/IronIndustryExpanded/IiexRecipeConfig.cs:46-77`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/recipes/grid/cokeoven.json`
- Test: `test/IronIndustryExpanded.Tests/Definitions/IiexRecipeOutputTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexCostSelectorTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionGoldenTests.cs`

**Consumes:** FurnaceRecipeDefinitions.Definitions(domain) => [BlastFurnace(domain), Cupola(domain)] :16-17; the private helpers Refractory(int) :122-123 and RefractoryTiered(int) :128-135; ExIngredients.FireClay/Nails/Plate/Hammer/Chisel (used at :43-47)

**Produces:** a third ExRecipeDef group CokeOven(domain); cost rows cokeovencore-grid and chargelid-grid in IiexRecipeConfig.Defaults()

- [x] **Step 1.** A *fourth* entry - `Reverberatory(domain)` landed with U6 - output `iiex:furnace-cokeovencore-n`, the letter and not the word. Original text: Add CokeOven(domain) as a third entry in FurnaceRecipeDefinitions.Definitions and register the grid recipe for the core. Output must be the creative-inventory default variant - the shipped cores emit "iwex:furnace-blastcore-{tier}-n" / "iwex:furnace-cupolacore-{tier}-n" (:32, :118), i.e. the letter n, not 'north'.
- [x] **Step 2.** `BBB,BHB,BBB` - a beehive of brick round a void, sharing no pattern with any of the sixteen already in the file. Eight `game:burnedbrick-fire` and a hammer: the cheapest core in the file on purpose, since coke is the fuel half of every shaft charge and an oven priced against what it unlocks would gate the iron tier behind the tier it opens. Original text: Caution: Give the recipe a pattern that collides with neither the blast-furnace core ("BRP,BN_,BRP" :26) nor the cupola core ("BRB,PCP,BRB" :115) - two grid recipes sharing a pattern with overlapping ingredients collide and the loser silently never resolves (comment at :34-38). Keep it cheap: fire brick, not refractory - this oven gates everything after it.
- [x] **Step 3.** ⛔ **The correction in this step is itself stale.** It says `iiex:furnace-chargedoor` has no recipe either and is U6.11's - but U6.11 has shipped, and the door has both a recipe (`FurnaceRecipeDefinitions.cs:219`) and a cost row. Only the **chargelid** was missing, which is what the original wording said. Grepped rather than believed, as the step asks. Original text: Add the missing **chargelid** recipe here. **Corrected 2026-08-05: "the one shipped block in the coke-oven layout with no recipe" is false.** `iwex:furnace-chargedoor` has none either, and it is **shared with the reheat furnace** — which is exactly why it is not this unit's to author: **U6.11 Step 5** now owns the whole reverberatory chassis *plus* the chargedoor, precisely so that one task checks `FurnaceRecipeDefinitions`' pattern-collision surface once. U9's gate was blocked by this too, and the old wording hid it by asserting the gap was already closed. The **chargelid** genuinely is coke-oven-only, so it stays here. Verify by grep before you write it, not from this sentence.
- [x] **Step 4.** `cokeoven-grid` and `chargelid-grid` added; ⛔ the cupola core's missing cost row is confirmed and **flagged in place rather than fixed**, as the step asks. Original text: Add cost rows to IiexRecipeConfig.Defaults() (:46-77) using the wildcard form the tier/side groups need: Grid("iwex:furnace-cokeovencore-*"), Grid("iwex:furnace-chargelid-*"). Note while there: the shipped cupola core recipe has no cost row either - flag it, do not silently fix it in this unit.
- [x] **Step 5.** Page **11**, in all three locales - the second handbook page this session, after the forming shop at 10. Original text: Write the handbook page under docs/iiex/handbook/ with the NN- prefix convention and mirror its keys into assets/iiex/lang/en.json (the handbook sync pipeline joins on the prefix).
- [x] **Step 6.** Bless the recipe golden with EXLIB_WRITE_GOLDENS=<the one path>, never =1.
- [x] **Step 7.** ./scripts/exmod.sh test 1.21

#### U9.5 — Export the crucible art and extend the shape converter

**Files**
- Create: `assets/iiex/shapes/furnace/cruciblehearth.json`, `assets/iiex/shapes/item/steelcrucible.json`, `assets/iiex/shapes/item/metalchunk.json`, `assets/iiex/shapes/item/ingotmold.json`, `assets/iiex/shapes/casting/cell-filling-ingotmold.json`
- Modify: `scripts/tools/convert-shape.py`
- Test: `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionGoldenTests.cs`

**Consumes:** scripts/tools/convert-shape.py TEXTURES map; the drawn editables assets/editable/shapes/{furnace-block-draftcruciblehearth,item-steelcrucible,item-shingled-metalchunk,item-sandcast-ingotmold,molten-sandcellfilling-castingotmold}.json

**Produces:** runtime shapes under assets/iiex/shapes/{furnace,item,casting}/ that IiexDefinitionGoldenTests.Every_shape_reference_resolves_to_a_shipped_file can find

- [ ] **Step 1.** Run `python scripts/tools/convert-shape.py --check`. It reports blistersteel and slag1 as unmapped, each used by exactly one editable - the crucible hearth. Add both to TEXTURES: blistersteel -> game:block/metal/ingot/blistersteel (the file exists at .game/1.20/assets/survival/textures/block/metal/ingot/blistersteel.png), slag1 -> iwex:block/slag/slag (assets/iiex/textures/block/slag/slag.png). Follow the file's own rule: cite a shipped sibling in the comment.
- [ ] **Step 2.** Caution: The hearth shape hardcodes the refractory face at tier3 via the front1 key. That is correct and must not be 'fixed': convert-shape.py's own comment (the front1 entry) explains the blocktype's textures map overrides by key, so a tiered block wears {tier}. Declare .Texture("front1", "game:block/clay/refractory/{tier}/front1") on the blocktype in U9.9.
- [ ] **Step 3.** Convert each editable: python scripts/tools/convert-shape.py furnace-block-draftcruciblehearth assets/iiex/shapes/furnace/cruciblehearth.json item-steelcrucible assets/iiex/shapes/item/steelcrucible.json item-shingled-metalchunk assets/iiex/shapes/item/metalchunk.json item-sandcast-ingotmold assets/iiex/shapes/item/ingotmold.json molten-sandcellfilling-castingotmold assets/iiex/shapes/casting/cell-filling-ingotmold.json
- [ ] **Step 4.** Verify the converted hearth still carries the element tree the code will name: Base/{Masonry,Firebar1-3,ClayStand1-4}, Crucibles/Crucible1-4, FillingBlisterSteel/BlisterSteel1-4, FillingSlag/Slag1-4, Covers/Cover1-4, Coke/CokeL1-6. The Coke/CokeL{n} names are exactly what BlockFirebox.ElementsFor(int layers) emits (src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockFirebox.cs:127-134), so the hearth's bed can reuse the firebox bed mesher unchanged.
- [ ] **Step 5.** Assert the hearth's extent stays one cell: bbox x -1..16, y -1..14, z 0..16. It is a single block holding all four pots, not a row of holes.
- [ ] **Step 6.** ./scripts/exmod.sh test 1.21

#### U9.6 — The refractory pot - a two-variant block on vanilla's crucible chassis

**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockSteelCrucible.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/SteelCrucibleTests.cs`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/steelcrucible.json`
- Modify: `src/IronIndustryExpanded/BlockNetworkMolten/Blocks/ClayHeatGate.cs`, `src/IronIndustryExpanded/Generated/IiexBlocks.g.cs`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/SteelCrucibleTests.cs`, `test/IronIndustryExpanded.Tests/Casting/ClayHeatGateTests.cs`

**Consumes:** vanilla .game/1.20/assets/survival/blocktypes/clay/crucible.json - classByType {crucible-burned: BlockSmeltingContainer, crucible-smelted: BlockSmeltedContainer}, behaviors GroundStorable/Unplaceable/RightClickPickup, attributes maxHeatableTemp:1200 + onTongTransform + tongOpening; ClayHeatGate.WouldShatter(Block?, float) :32-33; MoldKinds.FitsPedestal(Block?) :26-28

**Produces:** iwex:steelcrucible-{burned|smelted} as a BlockSmeltingContainer/BlockSmeltedContainer pair with a firing counter; the documented fireclay exception on ClayHeatGate

- [ ] **Step 1.** Caution: It is a block, not an item. docs/design/machines/crucible-furnace.md's Code table sends you to .../Items/ modelled on StockItemDefinitions - that is wrong. Vanilla's crucible is a block precisely so it can be a BlockSmeltedContainer, which is what the mod's existing pour path accepts (BlockMoltenCanalStart.cs:77). An item cannot pour.
- [ ] **Step 2.** Write the failing tests first: (a) a smelted pot pours into a canal start; (b) a pot at 3 recorded firings breaks after the pour and the metal survives; (c) ClayHeatGate.WouldShatter(pot, 1600f) is false - MoldKinds.FitsPedestal is `block is BlockToolMold` (:27), so the pot never satisfies it; pin that so a future 'tidy' of FitsPedestal cannot silently start shattering pots.
- [ ] **Step 3.** Define the two variants with classByType exactly as vanilla does, shape iwex:item/steelcrucible (from U9.5), and maxHeatableTemp raised past 1600. MaxStackSize must be 1 - the firing counter lives on the stack and two pots of different ages must never merge.
- [ ] **Step 4.** Write the fireclay exception into ClayHeatGate.cs as a doc-comment paragraph naming this block and the 3-firing life, per the design's own instruction ('a documented exception is a rule, an undocumented one is a bug'). Do not add a branch - there is nothing to branch on.
- [ ] **Step 5.** Caution: Decide the code path deliberately. BlockMoltenCanalStart.cs:66 and BlockMoltenBarrel.cs:84 both cache pour-source stacks by `block.Code.Path.StartsWith("crucible-")`. A pot coded iwex:steelcrucible-* will not appear in the interaction help even if the pour works. Either name it so the prefix matches, or extend both caches - and pin whichever you choose with a test, because help text and pour capability are two independent things behind one string.
- [ ] **Step 6.** Add the clayforming recipe from clay-fire (vanilla clayforming accepts only clay-* in blue|fire|red). Author it via ExRecipeDef.Body - there is no clayforming builder (src/ExpandedLib/Definitions/ExRecipeDef.cs:11-16 documents the escape hatch; PigRecipeDefinitions.cs:22-34 is the worked example).
- [ ] **Step 7.** Regenerate block codes, bless the golden, add lang keys in all three locales, ./scripts/exmod.sh test 1.21

#### U9.7 — Blister steel cold-crushes to 100 u of charge

**Files**
- Create: `src/IronIndustryExpanded/Items/BlisterBreaking.cs`, `src/IronIndustryExpanded/Recipes/Smithing/BlisterRecipeDefinitions.cs`, `src/IronIndustryExpanded/Patches/AnvilBlisterBreakingPatches.cs`, `test/IronIndustryExpanded.Tests/Items/BlisterBreakingTests.cs`, `test/IronIndustryExpanded.Tests/goldens/iiex/recipes/smithing/blister.json`
- Modify: `src/IronIndustryExpanded/Items/CastPartItemDefinitions.cs`
- Test: `test/IronIndustryExpanded.Tests/Items/BlisterBreakingTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionGoldenTests.cs`

**Consumes:** PigBreaking.PigVoxels :16, WorkItemCode :21, MarkerKey :25, UnitsPerVoxel :29, Emit(int voxelsRemoved, ref float remainder) -> (int Chunks, int Bits) :38-48 - all in src/IronIndustryExpanded/Items/PigBreaking.cs; PigRecipeDefinitions.Definitions :20-35 as the smithing-recipe shape; vanilla game:ingot-blistersteel

**Produces:** the cold branch of the blister fork: 3 chunks (25 u) + 5 bits (5 u) = 100 u exactly, plus the chunk item wearing assets/iiex/shapes/item/metalchunk.json

- [ ] **Step 1.** Write the conservation tests first, mirroring test/IronIndustryExpanded.Tests/Items/PigBreakingTests.cs: over a full break the payout equals the removed voxels' units to within one sub-bit crumb, and the total is exactly 3x25 + 5x5 = 100. This is the trap PigBreaking already guards - do not re-open it.
- [ ] **Step 2.** Copy the PigBreaking arithmetic rather than generalising it in this unit: pick a voxel count that makes units-per-voxel exact for 110 u in / 100 u out, and state the chosen number in a doc-comment the way PigBreaking.cs:14-17 does.
- [ ] **Step 3.** Add the smithing recipe via ExRecipeDef.Body keyed on a new marker code (not 'iwexpigbreak'), so the helve patch acts only on blister work items and never on ordinary steel smithing.
- [ ] **Step 4.** Caution: Gate on cold. The whole point of the fork is that hot-worked blister keeps vanilla behaviour and becomes shear steel via survival/recipes/smithing/steel.json. Write a test that a hot blister ingot does not enter the crushing route - otherwise the mod silently deletes vanilla's shear-steel path.
- [ ] **Step 5.** Define the crushed chunk item (shape iwex:item/metalchunk from U9.5) beside CastPartItemDefinitions, with materialUnits carrying 25.
- [ ] **Step 6.** Bless the smithing-recipe golden with a single-path EXLIB_WRITE_GOLDENS, ./scripts/exmod.sh test 1.21

#### U9.8 — cruciblesteel as a metal def

**Files**
- Create: `assets/iiex/config/metals/cruciblesteel.json`, `test/IronIndustryExpanded.Tests/Definitions/CrucibleSteelMetalTests.cs`
- Modify: `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Definitions/CrucibleSteelMetalTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/PigIronMetalTests.cs`

**Consumes:** the shipped metal-def schema as written in assets/iiex/config/metals/castiron.json (code, moltenItem, solidDrop, displayLangKey, castDomain, generateItemFamily, itemForms, texturePath, density, meltingPoint, tools.preset) and assets/siex/config/metals/bessemersteel.json (isAlloy); MetalRegistry + MetalDef in src/ExpandedLib/Metals/

**Produces:** iwex:ingot-cruciblesteel and the emitted tool family; meltingPoint 1600

- [ ] **Step 1.** Write the failing test first, modelled on test/IronIndustryExpanded.Tests/Definitions/PigIronMetalTests.cs: the registry resolves cruciblesteel, its meltingPoint is the highest in the iwex catalogue, and its emitted item forms exist.
- [ ] **Step 2.** Author the JSON with isAlloy:true (it is a made steel, like bessemersteel), density ~7820, meltingPoint 1600, tools.preset above bessemersteel's 'good'. MetalDef.Durability + MetalToolEmitter already emit the whole tool family from this one entry - no per-tool work.
- [ ] **Step 3.** Caution: Record in docs/design/materials.md that this raises vanilla's tool ceiling deliberately and that it gates nothing - wrought-iron heads still work. That is the settled ruling and it needs a home outside the machine page.
- [ ] **Step 4.** Caution: Do not attempt the 'crucible-steel machine heads last longer' half. Drill bits, shear blades and roll sets have no durability or wear mechanic at all; that is a new system and is explicitly out of U9.
- [ ] **Step 5.** Add lang keys iwex:metal-cruciblesteel in all three locales, ./scripts/exmod.sh test 1.21

#### U9.9 — Crucible-furnace core + hearth blocktypes and the layout

**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockCrucibleFurnaceCore.cs`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockCrucibleHearth.cs`, `src/IronIndustryExpanded/BlockStructures/Furnaces/CrucibleHearthLayout.cs`, `assets/iiex/textures/block/furnace/csf.png`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/cruciblecore.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/cruciblehearth.json`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/CrucibleFurnaceLayoutTests.cs`
- Modify: `src/IronIndustryExpanded/Generated/IiexBlocks.g.cs`, `docs/internal/workbench/layouts.md:518-589`, `docs/design/machines/crucible-furnace.md`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/CrucibleFurnaceLayoutTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnacePartsTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceRoleCellsTests.cs`

**Consumes:** BlockFurnaceCoreBase.Core(domain, code, type, path, tiers) :58-64; BlockHeatingHearth.Definitions :28-64 as the hearth-block shape (VariantGroup type + SideVariant + ShapeByTypePerOrientation + FillerOffsets + StructureAngle => ExOrientation.AngleFromSide(Variant["side"]) :69); IiexBlocks.FurnacePuddlingchimneycap.WithSide for the damper; VanillaCodes.Refractory :87, AnyBricks :127, Air :50; CellRole.Firebox :83, Flue :124, Damper :131

**Produces:** iwex:furnace-cruciblecore-{tier}-{side} and iwex:furnace-cruciblehearth-{side}; a layout whose stack base is inside the footprint and whose courses are not

- [ ] **Step 1.** Take the drawn geometry as authoritative: assets/editable/shapes/furnace-block-draftcruciblehearth.json is one 16x16x16 block (measured bbox x -1..16, y -1..14, z 0..16) containing four clay stands in a 2x2, four pots, and a six-course coke bed. So the layout gets one hearth cell, not four holes. This settles crucible-furnace.md Open 'whether the four pot cells are parts or fillers' - neither.
- [ ] **Step 2.** Give BlockCrucibleHearth a BEBehaviorFirebox (as BlockFirebox does at BlockFirebox.cs:52) so the coke bed is the same substrate every other fuel bed uses, and mark the hearth cell CellRole.Firebox in the layout. MinChargeToIgnite is sealed to FireboxCellCount * FireboxMixPerCell (BlockEntityFireboxFurnace.cs:241-242), so a one-cell hearth ignites at 12 units - do not try to introduce a CrucibleMixRequiredToFire constant, the branch forbids it.
- [ ] **Step 3.** Rewrite the draft at docs/internal/workbench/layouts.md:518-585 against reality: 'iwex:draftcruciblefurnacehearth*' / 'iwex:draftcruciblefurnacecore-*' / 'iwex:chargelid-north*' -> the generated IiexBlocks accessors; ExCodes.Refractory/AnyBricks/Air/CokeOvenDoor -> VanillaCodes.* (CokeOvenDoor is a method taking a BlockFacing, VanillaCodes.cs:280, and VanillaCodes.Sealing(wall) :289 is the intended call because vanilla's spelling is inverted).
- [ ] **Step 4.** Fix the three gaps layouts.md itself flags: add the firebox/fuel cell (now the hearth block), give the flue column its own glyph on the air code so CellRole.Flue does not also claim the open cells at rows 0-1 (idiom 1 in layouts.md:192), and draw the damper at the flue base with CellRole.Damper. This is the first production use of CellRole.Damper anywhere.
- [ ] **Step 5.** Author only the minimum stack in the layout - the base course - and leave the rest to the player, per the settled 'the core owns the walk' rule. A course is `. b . / b a b / . b .`, the exact predicate smex:smokestack already draws (BlockSmokeStackIntake.cs:80-148) with VanillaCodes.AnyBricks for b and VanillaCodes.Air for a.
- [ ] **Step 6.** Reuse the puddling chimney cap as the damper block (BlockPuddlingChimneyCap.cs:30-68, BlockEntityPuddlingChimneyCap.IsOpen :22 / Toggle() :25-30) rather than defining a second one, per the design's 'one implementation should serve both'.
- [ ] **Step 7.** Draw assets/iiex/textures/block/furnace/csf.png as the core's south-face label, matching the shipped 2-letter set.
- [ ] **Step 8.** Tests: stand the structure at all four sides with FurnaceLayoutRig.Stand and assert it completes without forcing StructureComplete; assert the hearth cell carries Firebox, the column carries Flue, the base carries Damper; assert RoleNamesOf returns exactly ["Firebox","Flue","Damper"]. Also add both new machines to the loop in FurnaceRoleCellsTests.cs:733-758, which today pins the exact role set for the two hearths only.
- [ ] **Step 9.** Regenerate codes, bless both goldens by explicit path, raise FurnacePartsTests.cs:50's floor again, ./scripts/exmod.sh test 1.21

#### U9.10 — BlockEntityCrucibleFurnace - seat, preheat, melt, crack, pull

**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityCrucibleFurnace.cs`, `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityCrucibleHearth.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/CrucibleFurnaceTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/CrucibleHearthSlotTests.cs`
- Modify: `src/IronIndustryExpanded/IiexConfig.cs`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/CrucibleFurnaceTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/CrucibleHearthSlotTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/HeatBalanceTests.cs`

**Consumes:** BlockEntityFireboxFurnace (the whole branch, esp. RequiresBlast => false :38, TuyereIntakeVolume/BlastPressureThreshold => 0f :50-51); BlockEntityFurnaceCore.ComputeHeatBalance(BurdenMix, float, float, int) :1211-1263 and its natural line :1229 (post-U6.3: NaturalDraughtFor(courses, damperOpen)); MeltSpeedFactor() :1268-1283; GetBlockInfo :1781 / AppendHeatBalanceInfo :1858 / abstract AppendReadyInfo :1948; BlockEntityPuddlingChimneyCap.IsOpen :22; BlockFirebox.ElementsFor(int) :127-134 for the bed mesh; ExShapeElements.Retextured

**Produces:** the per-pot melt cycle, the damper-as-preheat two-phase rhythm, the cracked-pot cellar spill, and the R7 draught readout

- [ ] **Step 1.** Write the temperature test first and make it fail: with the drawn stack height and the damper shut, ComputeHeatBalance's T_process must clear MeltingPoint = 1600. This is B15/B2's exact failure mode and the only thing that decides whether the machine works at all. Pin it in HeatBalanceTests beside the shaft numbers, and pin the per-machine charge-loss override too (BfChargeLossFull's full 310 is wrong for a firebox - BlockEntityFurnaceCore.cs:1241-1244).
- [ ] **Step 1b.** Caution: **Ruled 2026-08-05: a shut damper means less draught and a cooler fire** (U6.5's rule; the conventional stove reading). **This task is the one that must change, not U6.5 — and it was backwards for its own machine, not merely inconsistent with the puddling furnace.** A Huntsman crucible furnace is a deep hole: pots on a grate, coke packed around, ash pit below, flue above, air drawn up through the grate by chimney draught alone. Melting steel at ~1600 °C is at the limit of what coke and natural draught reach — which is *why* these furnaces had famously tall stacks. **Open flue damper = full draught = fiercest.** Steps 1 and 5 below say "damper open = damped fire" and require T_process ≥ 1600 with the damper *shut*; invert both.
> 
> **The pot-cracking mechanic survives — only its polarity flips**, and the historical sequence is a better rhythm than the one U9.10 drafted:
> 
> | phase | damper | why |
> |---|---|---|
> | **preheat** | **shut / partial** | gentle heat; a cold pot brought up fast cracks — this is where the crack check belongs |
> | **melt** | **open** | full draught, the 1600 °C gate, ~3–4 h |
> | **hold / anneal** | **shut** | kill the melt and steady it before teeming |
> 
> So the damper is **opened once, in the middle** — one deliberate act per heat rather than a state the player leaves set. `CellRole.Damper` keeps meaning the chimney damper, so U9.9 Step 6's reuse of `BlockPuddlingChimneyCap` stays justified.
- [ ] **Step 2.** Caution: Override the transfer loss to ~0. The reverberatory transfer term **U6.4** introduces for the puddling and reheat furnaces is what correctly stops a reverberatory melting iron; this furnace's pots sit in the coke bed with no bridge, so it must not pay it. If it is a branch constant rather than an overridable virtual, this machine cannot work - raise that with **U6.4** rather than working round it.
- [ ] **Step 3.** Derive BlockEntityCrucibleFurnace from BlockEntityFireboxFurnace and override MeltingPoint => a new IiexValues.CrucibleMeltingPoint (1600), plus the melt cadence keys ruling 2 leaves standing. Implement SmeltCycle(object) to act on each seated pot, not on a burden column - the same override shape BlockEntityHeatingFurnace.cs:54 leaves empty.
- [ ] **Step 4.** Pot slots are sub-block: the hearth is one cell with four stands, so the 'which cell you click picks which hole' idiom (BlockHeatingHearth.RowAt :72-78) does not apply. Implement four selection boxes or a cycle-to-next-free rule on BlockEntityCrucibleHearth, and pin slot->element mapping (Crucibles/Crucible1..4, Covers/Cover1..4, FillingBlisterSteel/BlisterSteel1..4, FillingSlag/Slag1..4) with a test that every named element exists in the shipped shape - the pattern FurnacePartsTests.cs:57-91 already uses for the two hearths.
- [ ] **Step 5.** Damper as preheat: damper open = damped fire = the anneal; closing it on cold pots destroys them. Tests must cover both directions - a cold pot cracks when the damper is shut on it, and a preheated one does not.
- [ ] **Step 6.** Cracked pot spills its charge one block down as items. Test that the metal survives; the pot is lost, the steel is not. A silent void here is the puddling-hearth mistake repeated.
- [ ] **Step 7.** 110 u blister -> 100 u crucible steel per pot (~9% melt loss). Assert the loss is applied once per heat over two consecutive heats, and that four pots is four independent heats.
- [ ] **Step 8.** Slag: confirm the inherited BfMaxMoltenSlag path is genuinely unused rather than quietly accumulating (crucible-furnace.md Open). One assertion.
- [ ] **Step 9.** R7 readout: block info must show `stack: N courses -> draught X (peak at P)` with the direction of travel. A declining draught past the peak is indistinguishable from a bug without it. Add lang keys in all three locales.
- [ ] **Step 10.** ./scripts/exmod.sh test 1.21

#### U9.11 — The pour target, construction, and the end-to-end gate

**Files**
- Create: `test/IronIndustryExpanded.Tests/Scenarios/CrucibleSteelScenarioTests.cs`, `docs/iiex/handbook/NN-cruciblefurnace.html`, `test/IronIndustryExpanded.Tests/goldens/iiex/recipes/grid/cruciblefurnace.json`
- Modify: `src/IronIndustryExpanded/BlockStructures/Casting/PatternItemDefinitions.cs:72-171`, `src/IronIndustryExpanded/Items/CastPartItemDefinitions.cs`, `src/IronIndustryExpanded/Recipes/Grid/FurnaceRecipeDefinitions.cs`, `src/IronIndustryExpanded/IiexRecipeConfig.cs:46-77`, `assets/iiex/lang/en.json`, `assets/iiex/lang/ru.json`, `assets/iiex/lang/uk.json`
- Test: `test/IronIndustryExpanded.Tests/Scenarios/CrucibleSteelScenarioTests.cs`, `test/IronIndustryExpanded.Tests/Casting/PatternValidationTests.cs`, `test/IronIndustryExpanded.Tests/Casting/MoldSpecTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexCostSelectorTests.cs`

**Consumes:** PatternItemDefinitions.PatternShapes :72-82 and Molds :86-171; Mold(shape, capacity, cavityBoxes, output, outputType:, size:) as used at :143-172; MoldSpec/MoldSize.Cell (src/IronIndustryExpanded/BlockStructures/Casting/MoldSpec.cs:9-16); BlockEntitySandCastingCell.Imprint's iwex-castingcell-wrongsize refusal

**Produces:** a castingotmold pattern + the cast-iron ingot mould, the crucible-furnace grid recipes and cost rows, and the U9 gate scenario

- [ ] **Step 1.** Add one row to PatternItemDefinitions.PatternShapes and one to Molds for castingotmold: shape iwex:casting/cell-filling-ingotmold (from U9.5), the cavity box measured off the drawn art, output the mould block, size defaulting to MoldSize.Cell (it is a 1x1 cast, unlike the LongCell stock). The file's own comment at :83-84 says adding a castable part is one line here plus the output item and the filling shape - hold it to that.
- [ ] **Step 2.** Caution: Capacity is the mass, not the drawn volume. Ruling 1: the sand cavities are illustrative; `capacity` on the pattern is what a pour measures against (PatternItemDefinitions.cs comment block above :143). Do not derive it from voxels and do not propose a redraw.
- [ ] **Step 3.** Add crucible-furnace core and hearth grid recipes to FurnaceRecipeDefinitions with patterns that collide with none of blastfurnace/cupola/cokeoven, and cost rows cruciblefurnacecore-grid + cruciblehearth-grid to IiexRecipeConfig.Defaults(). crucible-furnace.md claims those two keys already exist at IiexRecipeConfig.cs:47-70 - they do not; the shipped catalogue has 12 rows and none of them is a crucible or a cupola.
- [ ] **Step 4.** Write the gate scenario end to end in one test class, modelled on test/IronIndustryExpanded.Tests/Scenarios/ColdBlastFurnaceScenarioTests.cs: bituminous coal -> coke oven -> coke; vanilla ingot-blistersteel -> cold helve -> 3 chunks + 5 bits; charge a fireclay pot; build the crucible furnace and its stack; preheat on the open damper; shut it; melt; pull with tongs; pour into a cast-iron ingot mould; get iwex:ingot-cruciblesteel. Never force StructureComplete anywhere in it - build the real footprint.
- [ ] **Step 5.** Assert in that scenario that the furnace completes and runs cold with no stack built, and gets hotter as courses are added. That is the intended, legible behaviour (the chimney is deliberately outside StructureComplete) and it must be pinned, not 'fixed'.
- [ ] **Step 6.** Write the handbook page with the NN- prefix and mirror the keys into en.json; add every new lang key in ru.json and uk.json too (single hyphen, no em-dash).
- [ ] **Step 7.** Bless the recipe golden by explicit path; raise scripts/test-floors.txt IronworkingExpanded.Tests above its current 1132 by ~95% of the new count; ./scripts/exmod.sh test 1.21

### Traps — each of these makes a green suite a lie

- the draught number is the whole machine, and nothing tests it today. If BlockEntityCrucibleFurnace sets MeltingPoint = 1600 and no test asserts T_process >= MeltingPoint at the drawn stack height with the damper shut, the furnace ships lit-and-never-melting - B2's and B8's exact failure mode on a third furnace. HeatBalanceTests currently pins the shaft-furnace numbers only. The assertion must also cover the per-machine charge-loss override (BfChargeLossFull's full 310 is a burden-column penalty a firebox has no business paying, BlockEntityFurnaceCore.cs:1241-1244) and the reverberatory transfer-loss override to ~0 - get the second one wrong and the same term that correctly stops a reheat furnace melting iron also stops this furnace ever working.
- the coking conversion has no natural observable. Its only visible effect is 'the bed's contents changed identity'. A test that asserts a bool ('cycle ran') rather than 'FuelCode moved from bituminous to coke and Units dropped by exactly the yield fraction, once, over two consecutive cycles' leaves the entire conversion deletable by a refactor with a green suite. Same class of trap PigBreaking's conservation tests already guard for the pig chain.
- IsVenting has zero consumers (BlockEntityChargeDoor.cs:44). The lid gate is the coke oven's only reason to exist as a machine rather than a timer. A test that only asserts 'sealed chamber cokes' and never 'open chamber does not' means deleting the gate keeps every test green - and the oven silently becomes a box that cokes with the lid off.
- Caution: CellRole.Damper will be its first production use, and FurnaceRoleCellsTests.A_hearth_declares_none_of_the_five_roles_at_all (test/.../FurnaceRoleCellsTests.cs:733-758) pins the exact role set for the puddling and heating cores only. A new machine's role marks are entirely unpinned unless it is added to that loop; a Role() hung on the wrong glyph is invisible to the DSL and to every other test.
- Caution: FurnacePartsTests.cs:50 asserts `checkedCodes >= 12` - a floor written when iwex shipped four structures. Adding two more roughly doubles the real count while the floor stays put, so a break in layout-code collection that halved the codes examined would still pass. Raise it with each structure.
- Caution: scripts/test-floors.txt (IronworkingExpanded.Tests = 1132) is the guard against a suite that fails to load - which exits 0 with no summary and reports as a pass. A single static field of a game-typed value type in a new fixture (a ValueTuple of Vec3i, say) resolved during xUnit discovery kills the whole assembly. Both new test fixtures are at risk; keep them to plain concrete helpers with no generic constrained on a game type, as FurnaceLayoutRig.cs:35-40 warns.
- Caution: the stack is outside StructureComplete by design, so a rig that builds only the footprint gets a furnace that completes and runs cold. That is correct behaviour and the R7 readout is what makes it legible - but a test author who reads it as a bug will 'fix' it by pulling the courses into the layout, which permanently destroys the build-complexity-buys-capability trade the machine exists for. Assert the cold-complete state explicitly so the intent is written down. And never force StructureComplete - build the real footprint.
- Caution: the draught curve peaks and then declines. A test that only checks 'more courses = hotter' will pass on a curve that is monotonic, silently deleting the optimum. Pin the peak and at least one point past it (the design's own table: 9 courses is the peak, 20 is worse than 3, 30 is worse than none).
- Caution: the heat balance is cached into the save tree (hbAirFactor at BlockEntityFurnaceCore.cs:1642/:1664) because GetBlockInfo runs client-side. A stack rebuilt after ignition must invalidate the cached course count. A test that computes the balance only at ignition never observes staleness - add a course mid-heat and assert the readout moves.
- Caution: pot life (3 firings) lives on the stack. A test that seats a fresh pot every time never observes the counter at all, and a MaxStackSize above 1 would merge two pots of different ages into one. Pin both: the counter advances across seat/pull cycles, and two pots never stack.
- Caution: the cracked pot must not eat the charge. It spills one block down as recoverable items. A silent void here repeats the puddling/reheat hearths' destruction hole, and nothing in the current suite would notice - assert the metal's existence after the crack, not just the pot's absence.
- Caution: two independent things behind one string: BlockMoltenCanalStart.cs:66 and BlockMoltenBarrel.cs:84 both build their interaction-help caches from `block.Code.Path.StartsWith("crucible-")`, while the actual pour is gated on the BlockSmeltedContainer type at :77. A pot can therefore pour perfectly while never appearing in the help, or appear in the help and not pour. Test them separately.
- Caution: mass conservation across the blister fork. 110 u in / 100 u out per pot, and 3 chunks + 5 bits = 100 u exactly on the anvil. If the crushing route and the melt loss are tested in isolation, an off-by-one in either is invisible; the end-to-end scenario has to carry a single mass assertion from ingot to poured ingot.
- Caution: Ruling 2 lands underneath both machines. BlockEntityFireboxFurnace.cs:197-199 hands MaxFuelBurnTime / MeltStartDelay / MeltIntervalSec straight through from IiexValues.Bf*. If U2/U3 make those emergent on the shaft branch and nothing replaces them on the firebox branch, both U9 block entities inherit dead constants and their cadence tests pass against a clock nothing drives any more.
- Caution: the coke oven'S output must beat vanilla'S, and that is a comparison, not a constant. Vanilla already cokes a 3x3x3 chamber. If the yield and cycle time are tuned in isolation the bulk oven can ship as a strictly worse, much more expensive box, with every test green. Write the comparison into the config keys' doc-comments so the next person can see what the number is measured against.

**Gate.** An iwex-only player, with no steam and no MP anywhere in the chain, can run this end to end and a scenario test proves it: bituminous coal charged through the crown hoppers into a sealed two-chamber beehive oven yields bulk `game:coke`; that coke fires a crucible furnace whose temperature the player buys by building the chimney taller (and can overbuild past the peak and see it get worse); vanilla `game:ingot-blistersteel` cold-crushed on the anvil under a helve gives exactly 3 chunks + 5 bits = 100 u; four fireclay pots each melt 110 u down to 100 u of `iwex:ingot-cruciblesteel`, poured by hand with tongs into a sand-cast cast-iron ingot mould; a pot survives three heats and then breaks after a pour without eating its charge. Concretely: `./scripts/exmod.sh test 1.21` green with the iwex floor raised, both new structures completing at all four facings without any test forcing `StructureComplete`, goldens blessed by explicit path for four new blocktypes and two recipe files, and `T_process >= 1600 C` asserted at the drawn stack height with the damper shut.

---

# U10 — The connector check, and BlockBehaviorExOrientable's network half

U10 closes the one hole nothing in the engine catches: a network node (tuyere, passthrough, outlet) fitted with its connector pointing into the structure it is embedded in. Today the only thing catching it is a hard orientation *pin* in three shipped layouts — which is itself the trap the unit is supposed to make unrepresentable, so the four deliverables cannot land in the plan's order. The real sequence is: give every BlockNetworkNode def `mode:"network"` + a named scheme so `IsNetworkOriented` becomes a real answer (41 goldens move, no behaviour changes); route the two genuine code-rewrite sites in `BlockNetworkNode` through `ApplyOrientation`; build a `Connector` layout mark + completion-time subset check ("the connector faces out of the structure"); migrate the three furnace layouts off the pins onto it; only then can the builder refuse a pinned self-orienting node; finally delete the three ` ` doc markers. Two of the plan's "four sites" cannot or must not move, and saying so is part of the deliverable.

**Entry condition.** U0 is done (`OnPickBlock` override + `ExOrientableTests` + corrected doc markers all present — verified). U10.1–U10.3 (behaviour metadata + `BlockNetworkNode` wiring) touch no layout and can start immediately. **The layout-move wait is satisfied (2026-08-05)** — the tuyere/layout move U10.4–U10.6 had to wait for landed as **U3.0**: both tuyeres now sit at **y=2** in `BlockBlastFurnaceCoreCold`'s `Layer(2)`, so the three drawings U10.5 edits are in their final shape and the `.Connector` cells it authors are coordinates that no longer move. **U10.4–U10.6 are startable.** (Until 2026-08-07 this condition read "must wait for U2/U3's layered-charge layout move to land", citing docs/design/layered-charge.md's y=1→y=2 relocation at (0,2,-2)/(0,2,2).) U4 is not a hard prerequisite — it opens `BlockNetworkNode` for the tap/plug work, so running U10.2 concurrently with U4 risks a merge in the same file, but nothing in U10 consumes a U4 output.

**Shared files** (collision risk): `src/ExpandedLib/Blocks/Networks/BlockNetworkNode.cs — U4's tap/plug work has this file open for the same reason U10 does; two units editing Rotate/Recalculate concurrently will collide`, `src/ExpandedLib/Blocks/Structures/BlockEntityMultiblockStructure.cs — U2, U3 and U6 all reshape furnace completion/tick behaviour through this base; U10.4 adds a branch inside IncompleteBlockCount`, `src/ExpandedLib/Definitions/MultiblockLayoutBuilder.cs — U2 (layered-charge layouts), U5 (burdenmaker), U8 (three new megablocks) all author through it; U10.4 adds a DSL verb and U10.6 adds a refusal`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockBlastFurnaceCoreCold.cs — U2/U3 redraw this layout wholesale (tuyeres move y=1 → y=2, layer 2 becomes a full 3×3 raceway); U10.5 edits the same legend block`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockCupolaFurnaceCore.cs — same, plus U5's direct pig+scrap charging`, `src/SteelIndustryExpanded/BlockStructures/HotBlastFurnace/Blocks/BlockBlastFurnaceCoreHot.cs — mirrors the cold furnace's drawing; every cold-furnace layout change lands here too`, `test/IronIndustryExpanded.Tests/Fixtures/FurnaceLayoutRig.cs — the shared oracle for iwex, lpex and smex furnace suites; a constant change here breaks three suites at once, and U2–U5 all touch it`, `test/IronIndustryExpanded.Tests/Fixtures/ColdBlastFurnaceScenes.cs and test/SteelIndustryExpanded.Tests/Fixtures/BlastFurnaceScenes.cs and test/IronIndustryExpanded.Tests/Fixtures/CupolaScenes.cs — the three scene fixtures that place real tuyeres; U2/U3/U4 rewrite their melt and tap paths`, `test/ExpandedLib.Testing/StructureRig.cs — every megablock suite in the repo goes through it; U10.4 changes what `Missing` counts`, `test/ExpandedLib.Testing/NetworkNodeContract.cs and MultiblockCodes.cs — the shared per-mod guards, consumed by all five `{Mod}NetworkNodeContractTests``, `test/{ExpandedLib,IronworkingExpanded,LowPressureExpanded,HighPressureExpanded,SteelmakingExpanded}.Tests/goldens — 41 node blocktypes (U10.1) plus the three furnace cores (U10.5); any other unit blessing goldens in the same window will produce a confusing merged diff`, `scripts/test-floors.txt — one row per suite, read by both runners; must move in the same change as any net test-count drop`

### Tasks

#### U10.1 — Declare mode:"network" + a named scheme on every BlockNetworkNode def, guarded by a scheme-parity contract

Path update (2026-08-07): the pipe base classes moved to exlib. BlockPipe.cs, BlockPipePassthrough.cs,
BlockEntityPipe.cs, BlockEntityPipePassthrough.cs and ChimneyVent.cs now live in
`src/ExpandedLib/Blocks/Networks/` (namespace `ExpandedLib.Blocks.Networks`); registered class keys are
`exlib.*` and iwex's plated-tier defs come from `src/IronIndustryExpanded/BlockNetworkPipe/PlatedPipeDefinitions.cs`.
Symbols are unchanged — navigate by symbol; the file list below predates the move.

**Files**
- Modify: `src/ExpandedLib/Blocks/Behaviors/BlockBehaviorExOrientable.cs:104-125`, `test/ExpandedLib.Testing/NetworkNodeContract.cs:37-67`, `src/ExpandedLib/Blocks/Networks/BlockPipe.cs:82-160 (straight/bend/tjunction/xjunction)`, `src/ExpandedLib/Blocks/Networks/BlockPipePassthrough.cs:120`, `src/IronIndustryExpanded/BlockNetworkMolten/Blocks/BlockMoltenCanal.cs:194-228 (CanalFamilyDef, per CanalTypeSpec)`, `src/IronIndustryExpanded/BlockNetworkMolten/Blocks/BlockMoltenCanalTap.cs:56`, `src/IronIndustryExpanded/BlockNetworkEnergy/Blocks/BlockCastIronShaft.cs:40`, `src/IronIndustryExpanded/BlockNetworkEnergy/Blocks/BlockCastIronBevel.cs:49`, `src/IronIndustryExpanded/BlockNetworkEnergy/Blocks/BlockFlywheel.cs:118`, `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockRollingMill.cs:56`, `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockRollingMillAxle.cs:47`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockTuyere.cs:32`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockTwinTubMPBlower.cs:59`, `src/IronIndustryExpanded/BlockNetworkPipe/Blocks/BlockFluidIntake.cs:27`, `src/IronIndustryExpanded/BlockNetworkPipe/Blocks/BlockPipeOutlet.cs:51`, `src/IronIndustryExpanded/BlockNetworkPipe/Blocks/BlockValve.cs:39`, `src/IronIndustryExpanded/BlockNetworkPipe/Blocks/BlockPressureValve.cs:39`, `src/SteelIndustryExpanded/BlockStructures/SmokeStack/Blocks/BlockSmokeStackIntake.cs:154`, `test/*/goldens/**/blocktypes (the 41 files carrying a `"code": "orientation"` variant group)`
- Test: `test/ExpandedLib.Tests/Blocks/Behaviors/ExOrientableTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexNetworkNodeContractTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexNetworkNodeContractTests.cs`, `test/SteelIndustryExpanded.Tests/Definitions/SiexNetworkNodeContractTests.cs`, `test/SteelIndustryExpanded.Tests/Definitions/SiexNetworkNodeContractTests.cs`

**Consumes:** BlockBehaviorExOrientable.Initialize(JsonObject) reading properties["mode"]/["scheme"] (BlockBehaviorExOrientable.cs:104-125); ExOrientations.All / ExOrientations.Resolve(IEnumerable<string>?) -> ExOrientationScheme? (ExOrientations.cs:188, :213); ExOrientationScheme.Name/.Tokens (ExOrientations.cs:43, :48); ExBlockDef.Behavior(string name, object properties) (ExBlockDef.cs:609); ExBlockDef.VariantStates(string) -> string[] (ExBlockDef.cs:987); ExDefinitions.DefinitionsOf(Type, string) (ExDefinitions.cs:139)

**Produces:** NetworkNodeContract.SchemeViolations(string domain, Assembly asm, out int defsChecked) -> IReadOnlyList<string>; every concrete BlockNetworkNode def carries {"name":"ExOrientable","properties":{"mode":"network","scheme":"<SchemeName>"}}, so BlockBehaviorExOrientable.IsNetworkOriented is true and .Scheme is the block's real vocabulary at runtime

- [ ] **Step 1.** Add `NetworkNodeContract.SchemeViolations(domain, asm, out int defsChecked)` beside the existing `Violations` (NetworkNodeContract.cs:43): for each concrete `BlockNetworkNode` subtype from `ReflectionScan.GetCandidateTypes`, for each `ExDefinitions.DefinitionsOf(type, domain)` def, read `def.ToJson()["behaviors"]`, require exactly one entry named `ExOrientable` with `properties.mode == "network"`, and require `ExOrientations.Resolve(def.VariantStates("orientation"))?.Name` to equal `properties.scheme`. Increment `defsChecked` per def examined.
- [ ] **Step 2.** Add to each of the five `{Mod}NetworkNodeContractTests.cs` a fact calling `SchemeViolations` and asserting both `Assert.Empty(violations)` and `Assert.True(defsChecked > 0)` — the non-zero assert is mandatory, the sibling `MultiblockCodes.Unresolvable` (MultiblockCodes.cs:42-47) exists precisely because a checker that examines nothing passes forever.
- [ ] **Step 3.** Run `./scripts/exmod.sh test 1.21` and watch every node def listed as a violation (~41 across three mods). This is the failing state.
- [ ] **Step 4.** Add `.Behavior("ExOrientable", new { mode = "network", scheme = "<Name>" })` to each def, taking `<Name>` from `ExOrientations.Resolve(states)!.Name`: pipe straight/passthrough/shaft/bevel = `Axis`; flywheel/mill/millaxle/canal straight+pass = `AxisFlat`; valve/pressurevalve = `DirectedAxis`; canal start = `DirectedAxisFlat`; tuyere/blower/fluidintake/canal tap/smokestack intake = `Face`; pipe outlet = `FaceAll`; pipe bend = `PipeBend`; pipe tjunction = `PipeTee`; pipe xjunction = `PipeCross`; canal bend/tee/cross = `CanalBend`/`CanalTee`/`CanalCross`.
- [ ] **Step 5.** Harden BlockBehaviorExOrientable.cs:118-121: today an unrecognised `scheme` string silently becomes `ExOrientations.Axis`, which would make `ApplyOrientation` refuse every real token on a bend. Log a `world`-less `Logger`-free failure is not available in `Initialize`, so instead keep the fallback but record the unresolved name and add an ExOrientableTests case asserting the fallback is reached only for a null/absent scheme; the typo case is what U10.1's contract test catches at build.
- [ ] **Step 6.** Re-bless the 41 goldens with a scoped list, e.g. `EXLIB_WRITE_GOLDENS=iwex/blocktypes/pipe,iwex/blocktypes/molten,iwex/blocktypes/mpenergy,iwex/blocktypes/forming,iwex/blocktypes/furnace/tuyere,iwex/blocktypes/furnace/twintubblower,lpex/blocktypes/pipe,hpex/blocktypes/pipe,smex/blocktypes/smokestack` — never `=1`.
- [ ] **Step 7.** Diff the blessed goldens and confirm the only change in each is a new entry in the `behaviors` array. Any other movement means a def was edited by accident.
- [ ] **Step 8.** Run `./scripts/exmod.sh test 1.21` green on all three suites.

#### U10.2 — Route BlockNetworkNode's two real exchange sites through ApplyOrientation

**Files**
- Create: `test/ExpandedLib.Tests/Blocks/Networks/NetworkNodeOrientationTests.cs`
- Modify: `src/ExpandedLib/Blocks/Networks/BlockNetworkNode.cs:364-409 (Rotate)`, `src/ExpandedLib/Blocks/Networks/BlockNetworkNode.cs:808-857 (RecalculateAndSyncOrientations)`, `src/ExpandedLib/Blocks/Behaviors/BlockBehaviorExOrientable.cs:139-152 (ApplyOrientation)`
- Test: `test/ExpandedLib.Tests/Blocks/Networks/NetworkNodeOrientationTests.cs`, `test/ExpandedLib.Tests/Blocks/Behaviors/ExOrientableTests.cs`, `test/IronIndustryExpanded.Tests/Invariants/PipeInvariantTests.cs`

**Consumes:** BlockBehaviorExOrientable.ApplyOrientation(IWorldAccessor world, BlockPos pos, string token) -> bool (BlockBehaviorExOrientable.cs:139); BlockBehaviorExOrientable.VariantKey => "orientation" when IsNetworkOriented (:98); BlockNetworkModSystem.RemoveNode/AddNode as used at BlockNetworkNode.cs:397, :407; TestWorld's wired BlockAccessor.GetBlock(AssetLocation) and ExchangeBlock (TestWorld.cs:427, :439)

**Produces:** BlockNetworkNode.Rotate and BlockNetworkNode.RecalculateAndSyncOrientations contain no `CodeWithVariant("orientation", …)` + `ExchangeBlock` pair; both call `blockAtPos.GetBehavior<BlockBehaviorExOrientable>()?.ApplyOrientation(world, pos, token)` and branch on its bool

- [ ] **Step 1.** Write the failing test file first. Case 1 (wrench): stand a straight-pipe family (`ns`/`we`/`ud`, scheme `Axis`, behaviour attached) in a TestWorld, call `Rotate(entity, sel, +1)`, assert the block at pos wears the next declared token, that `World.Networks` re-registered the node (query `NetworkAt(pos)` connector faces), and that `MarkBlockDirty` was observed for that pos.
- [ ] **Step 2.** Case 2 (recalculate): place a node wearing `we` with a compatible neighbour that forces `ns`, call `RecalculateAndSyncOrientations`, assert the exchange happened and `BlockEntityNetworkNode.PossibleOrientations` was updated. Case 3: call it again with nothing changed and assert no second exchange (ApplyOrientation returns false when the block already wears the token — pinned by ExOrientableTests.cs:204-214, and load-bearing because this runs on every neighbour notification).
- [ ] **Step 3.** Case 4 (negative): a node whose block carries no `ExOrientable` behaviour must still not crash — decide and pin the contract (either fall back to the old inline rewrite, or refuse and log). U10.1 makes the behaviour universal, so "refuse and log" is defensible and is the stronger statement.
- [ ] **Step 4.** Run the new tests and watch Case 1's MarkBlockDirty assertion fail: `ApplyOrientation` (BlockBehaviorExOrientable.cs:144-151) does `GetBlock` + `ExchangeBlock` and nothing else, while the sites it replaces also call `MarkBlockDirty` (BlockNetworkNode.cs:400, :854).
- [ ] **Step 5.** Add `world.BlockAccessor.MarkBlockDirty(pos)` to `ApplyOrientation` after the exchange, and delete the now-duplicate calls at the two sites. Note in the XML doc that headless tests cannot see a missing mark — the test above is the only thing that will.
- [ ] **Step 6.** Replace BlockNetworkNode.cs:388-399 with `bool swapped = GetBehavior<BlockBehaviorExOrientable>()?.ApplyOrientation(world, pos, choices[nextIndex]) ?? false;` inside the existing `netManager.RemoveNode(...)` / `netManager.AddNode(...)` sandwich, keeping `be?.MarkDirty(true)` and the neighbour `RecalculateAndSyncOrientations` loop (:404-405) exactly where they are.
- [ ] **Step 7.** Replace BlockNetworkNode.cs:846-855 with `netBlock.GetBehavior<BlockBehaviorExOrientable>()?.ApplyOrientation(world, pos, finalChoices[0]);` — call it on `netBlock` (the block at pos), never on `this`, because `this` may be a different variant instance.
- [ ] **Step 8.** Add an XML remark at BlockNetworkNode.cs:70 (TryPlaceBlock) and :675 (GetDrops) naming why those two sites do not move — see U10.3.
- [ ] **Step 9.** Run `./scripts/exmod.sh test 1.21` green, paying attention to the iwex pipe/molten invariant suites.

#### U10.3 — Pin the two sites that must not move, so a later tidy-up cannot silently change drops

**Files**
- Modify: `src/ExpandedLib/Blocks/Networks/BlockNetworkNode.cs:671-688 (GetDrops/OnPickBlock XML docs)`, `src/ExpandedLib/Blocks/Behaviors/BlockBehaviorExOrientable.cs:242-251 (CanonicalStack XML doc)`
- Test: `test/ExpandedLib.Tests/Blocks/Networks/NetworkNodeOrientationTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionBehaviorTests.cs`

**Consumes:** BlockNetworkNode.GetFallbackOrientation(string? type) -> string, defaulting to AllowedOrientations[type][0] (BlockNetworkNode.cs:721-726); BlockNetworkNode.AllowedOrientations derived from the def's variant order via ExDefinitions.OrientationMap (BlockNetworkNode.cs:712-715); BlockBehaviorExOrientable.CanonicalStack using _scheme.Tokens[0] (BlockBehaviorExOrientable.cs:244-251)

**Produces:** A regression test asserting `iwex:furnace-tuyere-*` drops and picks the `-s` variant (its def's first-listed state) and that this deliberately differs from `ExOrientations.Face.Tokens[0]` == "n"; a second test asserting BlockNetworkNode.GetDrops/OnPickBlock never delegate to the behaviour

- [ ] **Step 1.** Add a test asserting `new BlockTuyere{…}.GetDrops(world, pos, null)[0].Collectible.Code` is `iwex:furnace-tuyere-s`. IiexDefinitionBehaviorTests.cs:18-27 already pins that the tuyere's orientation table is `[s,n,w,e]`; this states the consequence.
- [ ] **Step 2.** Add an assertion in the same test that `ExOrientations.Face.Tokens[0]` is `"n"` — so the divergence is written down, not discovered. Replacing site :683 with `CanonicalStack` would silently change the tuyere's drop from `-s` to `-n`, and the same for `lpex:pipe-outlet` (declares `s,n,w,e,u,d`; FaceAll.Tokens[0] == "n"), `lpex:pipe-fluidintake` (overrides GetFallbackOrientation to "s", BlockFluidIntake.cs:39) and `iwex:molten-canal-start` (overrides to "s", BlockMoltenCanalStart.cs:55).
- [ ] **Step 3.** Add a test proving adding the behaviour is drop-neutral: place a node block whose `BlockBehaviors` includes a `BlockBehaviorExOrientable`, call the block's `GetDrops`/`OnPickBlock`, and assert the behaviour's `CanonicalStack` was not the answer — BlockNetworkNode.cs:675 and :687 override `Block.GetDrops`/`Block.OnPickBlock` without calling base, so the behaviour's overrides never run.
- [ ] **Step 4.** Record in the XML doc at BlockNetworkNode.cs:70 that TryPlaceBlock's `:121` rewrite is not an ApplyOrientation call site: `ApplyOrientation` exchanges the block at a position, and at :121 no block has been placed yet — it resolves a code and hands it to `DoPlaceBlock`. The shared part is code resolution, not the swap.
- [ ] **Step 5.** Run `./scripts/exmod.sh test 1.21` green.

#### U10.4 — The connector check: a Connector layout mark, a multiblockConnectors attribute, and a completion-time subset test

**Files**
- Create: `src/ExpandedLib/Blocks/Structures/MultiblockConnectors.cs`, `test/ExpandedLib.Tests/Structures/MultiblockConnectorsTests.cs`
- Modify: `src/ExpandedLib/Definitions/MultiblockLayoutBuilder.cs:85-117 (Role/AddLegend siblings), :196-256 (Build), :292-309 (ValidateRoles)`, `src/ExpandedLib/Definitions/ExBlockDef.cs:918-932 (MultiblockLayout emits the third sibling)`, `src/ExpandedLib/Blocks/Structures/BlockEntityMultiblockStructure.cs:126-152 (SetStructureAngle loads it), :459-482 (IncompleteBlockCount enforces it)`, `test/ExpandedLib.Testing/StructureRig.cs:100-181, :244-254 (Missing must agree with the machine)`
- Test: `test/ExpandedLib.Tests/Structures/MultiblockConnectorsTests.cs`, `test/ExpandedLib.Tests/Structures/StructureRigTests.cs`, `test/ExpandedLib.Tests/Definitions/MultiblockFacingsTests.cs`

**Consumes:** MultiblockCellRoles.FromAttributes(JsonObject?) as the total, never-throwing reader to mirror (MultiblockCellRoles.cs:71-127); the authored↔transformed index alignment CellsWithRole relies on (BlockEntityMultiblockStructure.cs:283-307); _structureInitAngle, not _currentAngle (BlockEntityMultiblockStructure.cs:143, :496); INetworkConnector.HasConnectorAt(IBlockAccessor, BlockPos, BlockFacing) (INetworkConnector.cs:35); ExOrientation.RotateSideWord / FacingFromSide (ExOrientation.cs:176-210)

**Produces:** MultiblockLayoutBuilder.Connector(char symbol, params BlockFacing[] outward) -> MultiblockLayoutBuilder; attributes.multiblockConnectors as `{ "<letter>": [ {x,y,z}, … ] }`; MultiblockConnectors.FromAttributes(JsonObject?) -> MultiblockConnectors, .IsEmpty, .OutwardFacesAt((int X,int Y,int Z) authoredOffset) -> IReadOnlyList<string>

- [ ] **Step 1.** Write the failing test first, on a probe layout, not on a furnace: a 1×3×1 drawing whose middle cell glyph is legended `exlib:probe-node-*` and marked `.Connector('Y', BlockFacing.NORTH)`. Place a node exposing a connector only on `south`; assert the structure never completes and that `Interact`'s missing report names the cell.
- [ ] **Step 2.** Second case in the same file: place a node exposing `north`; assert it completes. Third: a node exposing `ns` (a superset) also completes — this is the subset test the orientation-schemes page argues for (orientation-schemes.md:149-166), and it is what makes the check immune to a legitimate re-pick.
- [ ] **Step 3.** Fourth: raise the same structure at angle 90 and assert the demand becomes `west`, derived from `_structureInitAngle`. Fifth: a cell with a connector mark whose occupant is not an `INetworkConnector` at all counts missing (so the mark cannot be satisfied by a plain brick).
- [ ] **Step 4.** Add `MultiblockLayoutBuilder.Connector(char symbol, params BlockFacing[] outward)`, accumulating into a `Dictionary<char, List<string>>` of letters. Validate it in `ValidateRoles`/`Build` exactly as `Role` is: the glyph must have a `Legend` entry (MultiblockLayoutBuilder.cs:294-299) and must be drawn in some `Layer` (:245-250) — an undrawn connector glyph resolves to nothing at runtime, the silent-empty-set failure roles already guard.
- [ ] **Step 5.** Emit `attributes.multiblockConnectors` from `ExBlockDef.MultiblockLayout` as a third sibling beside `multiblockFacings` and `multiblockRoles` (ExBlockDef.cs:925-930), omitted entirely when nothing is marked — the additive guarantee that let facings and roles ship without a migration.
- [ ] **Step 6.** Write `MultiblockConnectors` as a near-copy of `MultiblockCellRoles`: total, type-checked with the same `Coord(JToken?)` pattern (MultiblockCellRoles.cs:119-127), never throwing, because it is re-read on the server monitor tick and on client `GetBlockInfo`.
- [ ] **Step 7.** Load it in `SetStructureAngle` on the line after `_roles` (BlockEntityMultiblockStructure.cs:145), so it is dropped and reloaded with every angle change alongside `_codeByNumber`/`_cellsAccepting`/`_cellsWithRole`.
- [ ] **Step 8.** In `IncompleteBlockCount` (BlockEntityMultiblockStructure.cs:459-482), after `WildcardUtil.Match(wanted, actual.Code)` succeeds, look the authored offset up (same `_structure.Offsets`/`TransformedOffsets` index alignment `CellsWithRole` uses at :288-302), and for each declared outward letter rotate it by `_structureInitAngle` and require `actual is INetworkConnector c && c.HasConnectorAt(Api.World.BlockAccessor, worldPos, face)`. Otherwise `missing++` and invoke `onMissing`.
- [ ] **Step 9.** Mirror the same rule in `StructureRig.Missing` and `UnsatisfiedReport` (StructureRig.cs:244-308). If you do not, `Complete()` throws `"0 of N cells unsatisfied"` — the rig counts by code, the machine counts by code and connector, and the two disagree.
- [ ] **Step 10.** Run `./scripts/exmod.sh test 1.21` green; the exlib suite must gain the five new cases and every existing megablock suite must stay green (no shipped layout marks a connector yet).

#### U10.5 — Migrate the three furnace layouts off the orientation pins onto Connector

**Files**
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockBlastFurnaceCoreCold.cs:81-96`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockCupolaFurnaceCore.cs:74-76`, `src/SteelIndustryExpanded/BlockStructures/HotBlastFurnace/Blocks/BlockBlastFurnaceCoreHot.cs:79-84`, `test/IronIndustryExpanded.Tests/Fixtures/FurnaceLayoutRig.cs:45-55`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceRoleCellsTests.cs:60-82`, `test/SteelIndustryExpanded.Tests/Blocks/HotBlastFurnace/FurnaceGeometryTests.cs:300-315`, `test/IronIndustryExpanded.Tests/Fixtures/ColdBlastFurnaceScenes.cs:345-365`, `test/IronIndustryExpanded.Tests/Fixtures/CupolaScenes.cs:150-160`, `test/SteelIndustryExpanded.Tests/Fixtures/BlastFurnaceScenes.cs:165-175`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/blastcore.json`, `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/furnace/cupolacore.json`, `test/SteelIndustryExpanded.Tests/goldens/siex/blocktypes/blastfurnace/core.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceGeometryTests.cs`, `test/IronIndustryExpanded.Tests/Blocks/Furnaces/FurnaceOrientationMatrixTests.cs`, `test/IronIndustryExpanded.Tests/Scenarios (cold furnace scenarios)`, `test/IronIndustryExpanded.Tests (cupola scenarios)`, `test/SteelIndustryExpanded.Tests/Blocks/HotBlastFurnace`

**Consumes:** MultiblockLayoutBuilder.Connector(char, params BlockFacing[]) from U10.4; IiexBlocks.FurnaceTuyere.Any == "iwex:furnace-tuyere-*" (Generated/IiexBlocks.g.cs:701-702); IiexBlocks.PipePassthrough.Any; PipeTestWorld.MakeTuyere(int id, string orientation) (test/IronIndustryExpanded.Tests/Fixtures/PipeTestWorld.cs:101-112)

**Produces:** No shipped layout pins a network node's orientation; blastcore.json / cupolacore.json / smex core.json lose the `iwex:furnace-tuyere-n|-s` and `iwex:pipe-passthrough-*-ns` keys from `multiblockFacings` and gain `multiblockConnectors`

- [ ] **Step 1.** Write the failing test first, before touching any legend: add a knob to `ColdBlastFurnaceScenes` that fits one tuyere backwards (the `Tuyere(pos, id, orientation)` helper at ColdBlastFurnaceScenes.cs:355 already takes the letter), and a test asserting the furnace never completes. Run it now — it passes today, because the pin catches it. That green run is the baseline the migration must not lose.
- [ ] **Step 2.** In BlockBlastFurnaceCoreCold.cs:94-95, replace `.Legend('Y', IiexBlocks.FurnaceTuyere.WithOrientation("n"))` / `('T', …"s")` with `.Legend('Y', IiexBlocks.FurnaceTuyere.Any)` / `.Legend('T', IiexBlocks.FurnaceTuyere.Any)` plus `.Connector('Y', BlockFacing.NORTH)` and `.Connector('T', BlockFacing.SOUTH)`. Two glyphs on one code is already supported and shares one block number (MultiblockLayoutBuilder.cs:205-214), which is exactly what makes two connector directions on one code representable.
- [ ] **Step 3.** Do the same for the passthrough at :96: `.Legend('P', IiexBlocks.PipePassthrough.Any)` + `.Connector('P', BlockFacing.NORTH, BlockFacing.SOUTH)` — the passthrough must connect both ways through the wall, and the multi-face form is why `Connector` takes `params`.
- [ ] **Step 4.** Repeat for BlockCupolaFurnaceCore.cs:76 (one tuyere, `NORTH`) and BlockBlastFurnaceCoreHot.cs:82-83 (`Y`→NORTH, `y`→SOUTH).
- [ ] **Step 5.** Update FurnaceLayoutRig.cs:54-55: `NorthTuyereGlyph`/`SouthTuyereGlyph` collapse to one `TuyereGlyph = "iwex:furnace-tuyere-*"`. This is a real weakening of the `AssertRoleGlyphs` several-codes oracle (FurnaceLayoutRig.cs:690-719), whose second half asserts each listed glyph is used by some cell. Replace it with a connector-face oracle read off `multiblockConnectors` — 'the cell marked Tuyere in the north wall demands an outward `n` connector' — or the migration trades a strong assertion for a weak one.
- [ ] **Step 6.** Update FurnaceRoleCellsTests.cs:70-82's `Tuyere(wall, side)` helper and smex FurnaceGeometryTests.cs:305-315 the same way: they currently rotate the authored letter through `ExOrientation.RotateOrientationToken` because the code was pinned; with a wildcard code the rotation moves to the connector face instead.
- [ ] **Step 7.** Re-bless the three core goldens: `EXLIB_WRITE_GOLDENS=iwex/blocktypes/furnace/blastcore,iwex/blocktypes/furnace/cupolacore,smex/blocktypes/blastfurnace/core`. Diff and confirm `multiblockFacings` keeps exactly the player-oriented codes (`furnace-blastcore-*-n`, `furnace-irontap-w`, `furnace-slagtap-e`, `hopper-tall-e`) and nothing else.
- [ ] **Step 8.** Re-run the backwards-tuyere test from step 1 and confirm it still fails to complete — now because of the connector check rather than the pin. If it passes (i.e. the furnace completes), the check is not wired into the path the scenes use.
- [ ] **Step 9.** Run `./scripts/exmod.sh test 1.21` across all three suites — smex and lpex fixtures reach into iwex's rig through the test-project chain, so a rig constant change breaks three suites at once.

#### U10.6 — Make a pinned self-orienting node unrepresentable — a builder guard plus a cross-mod def check

**Files**
- Create: `test/ExpandedLib.Testing/PinnedNetworkNodes.cs`
- Modify: `src/ExpandedLib/Definitions/MultiblockLayoutBuilder.cs:94-117 (AddLegend), :119-154 (FindOrientationSegments)`, `test/ExpandedLib.Tests/Definitions/MultiblockFacingsTests.cs`, `test/IronIndustryExpanded.Tests/Definitions/IiexDefinitionBehaviorTests.cs`, `test/IronIndustryExpanded.Tests/Definitions`, `test/SteelIndustryExpanded.Tests/Definitions`, `test/SteelIndustryExpanded.Tests/Definitions`
- Test: `test/ExpandedLib.Tests/Definitions/MultiblockFacingsTests.cs`, `test/ExpandedLib.Tests/Definitions/StructureLayoutTests.cs`

**Consumes:** MultiblockLayoutBuilder._facingSegment keyed by the full domained code (MultiblockLayoutBuilder.cs:108-115); MultiblockCodes.Unresolvable(out int codesChecked, params (string,Assembly)[]) as the shape to copy (MultiblockCodes.cs:48-92); DefinitionGoldens.Collect(domain, asm) for cross-mod def enumeration (MultiblockCodes.cs:62)

**Produces:** MultiblockLayoutBuilder.Legend throws InvalidOperationException naming the glyph and the code when the code's orientation segment is a multi-letter network token; PinnedNetworkNodes.Violations(out int codesChecked, params (string Domain, Assembly Assembly)[] sources) -> IReadOnlyList<string> for the single-letter cases the builder cannot judge

- [ ] **Step 1.** Understand the limit before writing anything: `MultiblockLayoutBuilder` holds only a string (Legend at :48, AddLegend at :94), and `Build()` runs inside a def's static `Definitions(domain)` factory, so the block does not exist and cross-mod defs may not be registered yet. It cannot ask a behaviour anything. The guard therefore splits in two.
- [ ] **Step 2.** Builder half, which is exact: a multi-letter orientation token (`ns`, `we`, `ud`, `nswe`, `uns`, …) is spelled only by a network node — no player-oriented block spells one — so `AddLegend` can refuse it outright. Add the check where `FindOrientationSegments` already runs (:110), throwing when any recorded segment is longer than one letter and `oriented` is true. `LegendAnyFacing` (:56) stays lax and is the documented opt-out.
- [ ] **Step 3.** Failing test: a probe layout calling `.Legend('p', "iwex:pipe-plated-straight-ns")` must throw with a message naming the glyph, the code and the reason ("a network node takes its orientation from its neighbours; mark the cell with Connector instead"). Add a companion test that `.LegendAnyFacing('p', "iwex:pipe-plated-straight-ns")` still succeeds.
- [ ] **Step 4.** Cross-mod half, for the single-letter cases the builder cannot tell apart (`iwex:furnace-tuyere-n` looks exactly like `iwex:hopper-tall-e`): write `PinnedNetworkNodes.Violations` modelled on `MultiblockCodes.Unresolvable`. For every def in the sources, read `attributes.multiblockFacings`; for each key, find the def that provides that code (reuse `MultiblockCodes.AnyProvides`'s whole-segment matching, MultiblockCodes.cs:122-138) and fail when that def's `behaviors` array declares `ExOrientable` with `mode == "network"`.
- [ ] **Step 5.** Wire `PinnedNetworkNodes` into each mod's definition test with the same `Assert.True(codesChecked > 0)` pattern — a checker that examines nothing is exactly the failure `MultiblockCodes` documents at :42-47.
- [ ] **Step 6.** Run `./scripts/exmod.sh test 1.21`. Nothing must trip: U10.5 removed the last three pins. If anything trips, U10.5 is incomplete — do not relax the guard.
- [ ] **Step 7.** Caution: Do not attempt this task before U10.5. Three shipped layouts pin network-node orientations today (visible in blastcore.json's `multiblockFacings`: `iwex:furnace-tuyere-n`, `-s`, `iwex:pipe-passthrough-*-ns`), so a guard added first fails the build of iwex, lpex, hpex and smex simultaneously.

#### U10.7 — Delete the pending markers, and correct the over-claims the pins left behind

**Files**
- Modify: `src/ExpandedLib/Blocks/Behaviors/BlockBehaviorExOrientable.cs:33-38, :47-64, :127-138`, `src/ExpandedLib/Helpers/ExOrientations.cs:110-126`, `src/IronIndustryExpanded/BlockStructures/Furnaces/Blocks/BlockBlastFurnaceCoreCold.cs:81-93`, `test/IronIndustryExpanded.Tests/Fixtures/FurnaceLayoutRig.cs:45-55`, `docs/design/mechanics/orientation-schemes.md:85-168`, `docs/design/mechanics/multiblock.md:131-168`, `WORKLOG.md`

**Consumes:** nothing — this is the record-keeping half, and every claim it makes true must already be green

**Produces:** No pending marker remains in BlockBehaviorExOrientable.cs; orientation-schemes.md's "What exists today" and "Two ways to express it" sections name the built mechanism

- [ ] **Step 1.** Replace BlockBehaviorExOrientable.cs:33-38 (the "Only horizontal is in production use" pending note): `network` is now declared by every BlockNetworkNode def. Keep an accurate note that `omni` is still declared by no block but is exercised by ExOrientableTests.cs:145-170, so deleting the branch breaks tests.
- [ ] **Step 2.** Replace :47-64 (the "NOT BUILT, and this is the dangerous one" pending paragraph) with the built statement and where it is enforced: the builder's multi-letter refusal plus `PinnedNetworkNodes` per mod, and the `Connector` mark as the sanctioned way to state what the layout actually wants.
- [ ] **Step 3.** Replace :127-138's "intended, not yet wired" pending note on `ApplyOrientation` with the truth from U10.2/U10.3: two of `BlockNetworkNode`'s four rewrite sites now call it; `TryPlaceBlock`'s cannot (no block at the position yet) and `GetDrops`'s must not (the per-type fallback is not the scheme's first token).
- [ ] **Step 4.** Correct the over-claim at BlockBlastFurnaceCoreCold.cs:88-93 and its copy at FurnaceLayoutRig.cs:49-53. Both assert that a backwards tuyere is "unbuildable" because a pipe neighbour makes that face required and the node is exchanged onto it. Traced: `ComputeValidOrientations` uses the `connectsAny` relaxation for single-axis shapes (BlockNetworkNode.cs:247-256) and the solid-brick branch re-adds the current letter as required (:228-240), so a tuyere already facing `s` stays inside `finalChoices` and `RecalculateAndSyncOrientations` never exchanges it (:841-844). The layout was the only thing catching it; now the connector check is.
- [ ] **Step 5.** Update orientation-schemes.md:85-97 ("What exists today"): the declared-scheme registry the page proposes at :172-193 is built (`src/ExpandedLib/Helpers/ExOrientations.cs`, twelve schemes + the two-step `Rotate`), and step 2 of its cost table — pointing ~18 blocks' `VariantGroup` at it — is what U10.1 effectively completed via the `scheme` property. Mark the "Require connector faces out" row of the :149-156 table as the chosen and built form.
- [ ] **Step 6.** Update multiblock.md:141-145: it still names `FindSideSegment` and says "the last such segment wins"; the code is `MultiblockLayoutBuilder.FindOrientationSegments` (:144-154) and it records all rotating segments. Add the `multiblockConnectors` sibling to the emission list at :561.
- [ ] **Step 7.** Append a WORKLOG.md entry describing what landed and why the plan's task order was inverted. Do not commit — the user owns git history.

### Traps — each of these makes a green suite a lie

- The connector check tested by forcing StructureComplete proves nothing. Build the real footprint with StructureRig and let the machine complete itself — the whole rig exists because `ReflectionHelpers.SetProperty(be, "StructureComplete", true)` asserts the conclusion (StructureRig.cs:22-27).
- Removing the layout pins in U10.5 without the check from U10.4 opens the hole for the first time in the mod's life, and every existing furnace test still passes, because every fixture places a correctly-oriented tuyere. The only thing that can catch it is a negative test — a deliberately backwards tuyere that must leave the furnace incomplete. Write it before the legend change and confirm it is green on the pin, then confirm it is still green on the check.
- The rig-side mirror is the silent half. `StructureRig.Missing` counts by code only (StructureRig.cs:244-254). Add the connector rule to `IncompleteBlockCount` and not to the rig and `Complete()` throws "0 of 160 cells unsatisfied" — a confusing failure that invites someone to "fix" it by loosening the production check.
- Adding `mode:"network"` to 41 defs moves 41 goldens and changes no runtime behaviour on its own. A green golden re-bless is not evidence the wiring works; only the U10.2 node-level tests are. Do not let the golden diff stand in for them.
- A misspelled `scheme` silently becomes `ExOrientations.Axis` (BlockBehaviorExOrientable.cs:118-121). On a bend that makes `ApplyOrientation` refuse every real token, and the node simply stops re-orienting — no exception, no log line. The per-def scheme-parity contract is the only guard, and it must assert a non-zero examined count or it passes while examining nothing (the failure MultiblockCodes.cs:42-47 documents).
- `ApplyOrientation` does not `MarkBlockDirty`. A headless suite cannot see a missing client mesh update, so routing Rotate and RecalculateAndSyncOrientations through it and dropping the existing `MarkBlockDirty` calls (BlockNetworkNode.cs:400, :854) is invisible to every test in the repo. Pin it explicitly with a substitute assertion on the accessor.
- `Rotate`'s exchange sits between `RemoveNode` and `AddNode`. Move the exchange out of that sandwich and the network graph keeps the old connector faces while the block wears the new ones — a run that reads as connected and moves nothing. Assert the graph, not just the block code.
- `ExOrientableRig` drives the behaviour in isolation and never through a `BlockNetworkNode` (ExOrientableRig.cs:18-22). Passing ExOrientableTests says nothing about whether the node path works; U10.2 needs its own node-level test file.
- Collapsing `NorthTuyereGlyph`/`SouthTuyereGlyph` to one wildcard weakens `AssertRoleGlyphs`'s second half, which today asserts each listed glyph is used by some cell (FurnaceLayoutRig.cs:713-718) — the thing that stops one tuyere code covering both cells. Unless a connector-face oracle replaces it, a drawing that marked both tuyeres outward-north would pass.
- `MultiblockConnectors.FromAttributes` runs on the server monitor tick and on client `GetBlockInfo`. A throw there is a repeating exception on a live block entity, not a refusal to load. Copy `MultiblockCellRoles`' total, type-checked `Coord(JToken?)` pattern (MultiblockCellRoles.cs:119-127) rather than casting.
- `MultiblockFacings` still rotates through the old `ExOrientation.RotateOrientationToken`, which does not recognise bend/tee/cross tokens at all — so a layout pinning `pipe-bend-nw` gets no facings entry and is silently unchecked at every angle. That is a second, still-open silent hole in the same feature; do not assume the pin mechanism covers a code just because it carries an orientation segment.
- test-floors.txt gates each suite on a minimum discovered count. If U10.5's oracle rewrite consolidates per-facing theory cases, the count can fall without anyone noticing — and the whole reason the file exists is that a suite that fails to load reports zero failures and exit 0.

**Gate.** A cold blast furnace raised with one tuyere fitted backwards never completes, and the missing-cell report names that cell and the outward face it wants; the same furnace with both tuyeres correct completes at all four facings, and a tuyere wearing a superset token still satisfies it. Every concrete `BlockNetworkNode` def declares `mode:\"network\"` with a `scheme` whose tokens set-equal its own `orientation` states, proved by a per-mod contract that reports a non-zero examined count. `BlockNetworkNode.Rotate` and `RecalculateAndSyncOrientations` contain no `CodeWithVariant` + `ExchangeBlock` pair, and a node-level test proves the wrench swap still re-registers the graph and marks the block dirty. A probe layout that pins `iwex:pipe-plated-straight-ns` fails at build with a named reason, and no shipped layout trips the guard. `iwex:furnace-tuyere-*` still drops `-s`. No pending marker remains in BlockBehaviorExOrientable.cs. `./scripts/exmod.sh test 1.21` green on all three suites with every floor met.

---

# U11 — The ladle: a canal-merging vessel on a laid multiblock

*Appended 2026-08-06 from a user handoff. The art is **drawn** (`assets/editable/shapes/molten-megablock-laddle.json`,
`assets/editable/shapes/molten-block-laddlecore.json`) and the **layout is specified by the user**, reproduced
verbatim below — this section is the only copy of it, so do not paraphrase the grids.*

U11 builds the ladle as a **structure**, not as a process: the two blocks, their two layouts, the shape export, the
RCC construction, placement/breaking, and the tests. **The metallurgy is explicitly not in this unit** — no
alloy windows, no ferroalloy metal defs, no powdered coke, no `blowniron`, no composition resolver. Those are
smex-tier and are gated on decisions [ladle.md](../../design/machines/ladle.md) § *Open* still lists as open (#3
capacity, #4 where the windows live). This unit ends with a ladle that stands up in the world, holds a bath, pulls
from its canals and pours; what it pours is one metal, and the merge arrives with the alloy catalogue.

**Home: iwex.** Ruled 2026-08-05 ([ladle.md](../../design/machines/ladle.md):16-20) — *"`iwex`, not smex. Its
alloying role is smex-tier, but its pouring role gates iwex casting, and the block is the same object in both
eras."* The page's own front-matter still says **Mod smex** and § *Code* still points at
`src/SteelIndustryExpanded/BlockStructures/Ladle/`; both are stale against the ruling five lines below them, and
U11.7 fixes them.

**Entry condition.** Everything this unit stands on exists today: the molten canal family (`BlockMoltenCanal`,
`BlockMoltenCanalStart`, `BlockMoltenCanalTap` in `src/IronIndustryExpanded/BlockNetworkMolten/`), `MoltenCharge`,
`BEBehaviorMoltenCell`, the megablock filler system, `ExRightClickConstructable`, and `MultiblockLayoutBuilder`
with its oriented-legend support. **The one thing that does not exist is U11.1's**, and it is a genuine framework
gap rather than a missing convenience — see below. Nothing in U2–U10 is a prerequisite, and nothing in U2–U10
waits on this; U11 is a **leaf**. It reads more cleanly *after* U4.3 (two molten cells on one block entity), but
only if the vessel ends up wanting a second cell, which this unit's scope does not force.

**Shared files** (collision risk): `src/ExpandedLib/Blocks/Structures/StructureFootprint.cs` +
`src/ExpandedLib/Definitions/ExBlockDef.cs` — U11.1 widens `FillerCellSpec`; no other unit touches either ·
`src/ExpandedLib/Definitions/VanillaCodes.cs` — U11.4 adds one rung (`AnyBricksOrAir`); U10.5 reads the file but
edits no constant · `src/IronIndustryExpanded/IiexRecipeConfig.cs` — U11.6 appends the `ladle-grid` row alongside
U5.6/U6/U8.8/U9.4 · `assets/iiex/lang/{en,ru,uk}.json` — append-only, one contiguous block ·
`scripts/tools/convert-shape.py` — U11.2 appends TEXTURES entries only (all five keys the two shapes use are already
mapped, verified 2026-08-06 via `--check`) · `src/IronIndustryExpanded/Generated/IiexBlocks.g.cs` + iwex goldens —
regenerate last.

---

## The layout, as specified

Caution: **Two grids, and they interlock exactly.** The ladle megablock's own fillers are what satisfy every `f`
(structure filler) cell in the multiblock — the same trick `BlockCupolaFurnaceCore` already uses for the tall
hopper's filler at its layer 5. This was cross-checked cell by cell and the counts match on all three shared
layers, which is the evidence the two drawings are aligned:

| megablock layer | multiblock layer | filler cells | `f` cells | match |
|---|---|---|---|---|
| L1 (`y = 0`, principal) | L2 | 8 | 8 | yes |
| L2 (`y = +1`, + crank) | L3 | 10 | 10 | yes |
| L3 (`y = +2`, bottom slabs) | L4 | 6 | 6 | yes |

So the **ladle megablock principal sits at multiblock `(0, +1, 0)`** and the **ladle core at `(0, 0, 0)`**.

### Filler layout for the ladle megablock itself (horizontal layers)

```
L1:
# # #
# C #
# # #

L2:
# # #
# # # I
# # #

L3:
_ . _
. _ _
_ . _
```

* `#` — full block filler
* `C` — the principal (the megablock's own origin; `FillerLayoutBuilder` skips it)
* `I` — **vertical slab filler, slab is west.** It holds the hand rotating control and therefore the
  interactions. **The crank geometry really is in this cell** — the `Controls` group spans **X 14…40**, and
  a west slab in cell +2 occupies x 32…40, so the slab ends exactly where the crank does. It was drawn to
  fit.

> ### A shape's child `from`/`to` are relative to the parent's `from`, not absolute
>
> This cost a wrong answer to the user on 2026-08-06 and is the single easiest way to misread any shape in
> this repo. Reading the raw JSON as absolute put the ladle's crank at `x ≤ 32` — the *west face* of the `I`
> cell — and produced the confident, wrong conclusion that the slab held no geometry. The user checked in
> Model Creator and corrected it.
>
> **The proof, from the burdenmaker:** `Lids/Lid`'s child `Cube53` reads `from [29,0,0]`. Read as absolute
> that is nonsense — a lid segment 29 px east of a lid that ends at x = 4. Added to its parent's
> `from [-12,17,-12]` it is **X 17…28**, which is precisely the small hopper's extent in
> `burdenmaker.md` § *Assets*. The design doc's numbers were computed correctly; a naive read is not.
>
> ```python
> def walk(e, ox, oy, oz):            # accumulate the parent's `from` down the tree
>     a = [e['from'][i] + (ox, oy, oz)[i] for i in range(3)]
>     ...
>     for c in e.get('children', []): walk(c, a[0], a[1], a[2])
> ```
>
> Caution: Group nodes with `from == to == [0,0,0]` (every `Root`, and the ladle's four top-level groups) make
> relative and absolute agree **for their direct children only**, which is exactly why the mistake survives
> a spot check. It applied to U5.2's burdenmaker facing check as well, which verified the facing
> "against the exported JSON extents".
* `_` — **bottom slab filler**
* `.` — nothing

### The overall multiblock structure (horizontal layers)

```
L1:
. b N b .
# n n n #
# d C d #
# s s s #
. b S b .

L2:
. b . b .
b f f f .
b f L f .
b f f f .
. b . b .

L3:
. b . b .
b f f f .
b f f f f
b f f f .
. b . b .

L4:
. b A b .
b f C f .
B D f f .
b f E f .
. b A b .
```

* `#` — any brick
* `b` — any brick **or air**
* `N` — north-oriented molten canal start · `S` — south-oriented molten canal start
* `C` *(L1)* — the **ladle core** block, down slab · `d` — down slab, any brick
* `n` — north-down stair, any brick · `s` — south-down stair, any brick
* `f` — structure filler · `L` — the **ladle** block (the megablock principal)
* `A` — any straight **ns** molten canal · `B` — **we** straight molten canal
* `C` *(L4)* — any **n** molten canal tap · `D` — any **w** tap · `E` — any **s** tap
* `.` — nothing

> ### ruled by the user 2026-08-06 — the `b` cells **and every canal cell** are optional
>
> *"All `b` bricks and canal blocks are meant to be optional, so players can add them in position depending
> on the composition they need in game. Their absence should not block the ladle working."*
>
> **So `N`, `S`, `A`, `B` and the three taps are sockets, not requirements.** The drawing says *where a
> canal may go*, not what must be there. **A ladle with no plumbing at all completes and works** — it simply
> has nothing to pull from and nowhere to pour, which the molten code already handles (a blocked destination
> does not plug; `PushMetal` refusal falls through to `SoakHeat`).
>
> **What that means in the builder:** every canal glyph is an `@(air|…)` alternation exactly as `b` is, and
> it is declared with **`LegendAnyFacing`, never `Legend`**.
>
> Caution: **The facing must not be pinned, and this is a correctness matter rather than a preference.**
> `BlockMoltenCanal` is a `BlockNetworkNode`: it **re-derives its own orientation from its neighbours** and
> exchanges itself (`RecalculateAndSyncOrientations`). A cell pinned to `straight-*-ns` therefore breaks the
> structure the moment the player routes their plumbing differently and the canal legitimately re-orients to
> `we` or to a bend — with no error, no message, and a ladle that stops working because of a pipe two blocks
> away. `Legend` **auto-detects** orientation segments and pins them, so this is opt-out, not opt-in.
> Note: This is the same hazard U10.6 exists to make unrepresentable ("a pinned self-orienting node").
>
> Caution: **Optional is not unchecked.** `@(air|canal…)` still refuses a *wall* in the spout channel, which is
> wanted: those cells have to stay clear for the pour. Only the structural cells — `#`, `d`, `n`, `s`, `C`,
> `L`, `f` — are genuinely required.

Caution: **`C` is overloaded across layers** — the ladle core at L1, a north canal tap at L4. `MultiblockLayoutBuilder`
refuses a glyph declared twice (`"a glyph maps to one code"`), so **one of them must be re-lettered in the C#**.
Keep the user's spelling in this document and pick the new letter in the builder; say which in the code comment.

**Reading of the plumbing** (derived, and it is what the two pour clips confirm): three **taps** at L4 pour
*down* into the vessel from canals arriving on three sides, and the two **canal starts** at L1 are what the
vessel tilts *into* — `poursouth` toward `S`, `pournorth` toward `N`, with the `n`/`s` stair courses forming the
spout channel. That is the design's *"call `DrainMetal` on two neighbours instead of one"* made literal, one
layer up.

---

### Tasks

#### U11.1 — exlib: let a filler cell declare its own collision boxes

**Files**
- Modify: `src/ExpandedLib/Blocks/Structures/StructureFootprint.cs` (`FillerCellSpec`),
  `src/ExpandedLib/Definitions/ExBlockDef.cs` (`SerializeFillerCells`),
  `src/ExpandedLib/Blocks/Structures/FillerLayoutBuilder.cs` (a glyph→boxes registration)
- Test: `test/ExpandedLib.Tests/Blocks/Structures/` (beside the existing footprint tests)

**Consumes:** nothing · **Produces:** `FillerCellSpec` carries `Cuboidf[]? CollisionBoxes`; `FillerLayoutBuilder`
can register a glyph as a partial-fill cell; the emitted `fillerOffsets` entry carries `collisionBoxes`

Caution: **This is a real gap, not a nicety, and it fails silently.** `StructureFillers.ReadOffsets` has read
`collisionBox` / `collisionBoxes` per cell since the partial-fill feature landed, and
`BlockEntityStructureFiller.CollisionBoxes` already drives both `GetCollisionBoxes` and `GetSelectionBoxes`
(`BlockStructureFiller.cs:146-166`). But the **C# authoring path cannot express it**: `FillerCellSpec` has no
such field and `SerializeFillerCells` (`ExBlockDef.cs:859-892`) emits only `x`/`y`/`z`/`behaviors`/`allowAttach`.
Every megablock shipped so far is full-cube, so nothing has needed it — the ladle needs it **twice** (`I` and the
six `_` cells). Authored without this, the layout still loads and still places fillers; they are simply
**full cubes**, so the ladle would wall off its own crank cell and stand on an invisible solid block above its
rim. Nothing anywhere goes red.

- [ ] **Step 1.** Write the failing test: a footprint whose glyph is registered with a bottom-slab box emits an
      entry carrying `collisionBoxes`, and `StructureFillers.ReadOffsets` round-trips it back to the same cuboid.
      Assert the round-trip, not just the emission — the two halves are in different files and the JSON key is
      the only thing joining them.
- [ ] **Step 2.** Run it; expect a compile error on the missing member.
- [ ] **Step 3.** Add `Cuboidf[]? CollisionBoxes = null` to `FillerCellSpec` (last, so every existing positional
      construction still compiles) and emit `collisionBoxes` from `SerializeFillerCells` when non-empty.
      Emit the **array** form, not the singular — `ReadBoxes` prefers `collisionBoxes` and one code path is
      cheaper than two.
- [ ] **Step 4.** Add `FillerLayoutBuilder.Partial(char symbol, params Cuboidf[] boxes)` beside `Solid`/`Attach`,
      so the ladle's `_` and `I` read off the diagram rather than being hand-listed after it — the whole reason
      the layout DSL exists.
- [ ] **Step 5.** Run the exlib suite; move `scripts/test-floors.txt`'s exlib row in the same change.

#### U11.2 — Export both shapes to runtime

**Files**
- Create: `assets/iiex/shapes/molten/laddle.json`, `assets/iiex/shapes/molten/laddlecore.json`
- Modify: `scripts/tools/convert-shape.py` only if a texture key turns out unmapped

**Produces:** two runtime shapes, animations intact

- [ ] **Step 1.** `python scripts/tools/convert-shape.py molten-megablock-laddle assets/iiex/shapes/molten/laddle.json
      molten-block-laddlecore assets/iiex/shapes/molten/laddlecore.json`. **Mandate the script** — a
      hand-copied shape passes every test in the repo and is broken only in game (see Biggest risks).
- [ ] **Step 2.** Confirm the three clips survive: `idle`, `poursouth`, `nournorth`. **`nournorth` is a typo
      for `pournorth`.** Decide once and write it down: renaming it in the editable is the honest fix, but the
      name must then match whatever `ToggleAnimator`/`ConstructedAnimator` is asked for — an unknown animation
      name is **dropped by the animator with no exception and no log line** (Biggest risks, "deletions that fail
      soft"). The user drew it; ask before renaming their file.
- [ ] **Step 3.** Check the emitted `onAnimationEnd` on all three. All are **held poses**, not cycles, so they
      must keep `EaseOut` — they belong in `HOLD_CLIPS`, and `poursouth`/`pournorth`/`idle` are not in the set
      today. Add them, or a pour tilt eases itself back upright.

#### U11.3 — `BlockLadleCore`: the multiblock principal

**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Ladle/Blocks/BlockLadleCore.cs`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Ladle/LadleLayoutTests.cs`

**Produces:** `iwex:molten-laddlecore-{brick}` (or the settled code), a down-slab block that anchors the structure

**Ruled by the user 2026-08-06: use the generic down-slab shape, craftable from any brick (texture
variants)** — so the core disappears into the masonry beside the `d` slabs flanking it. That leaves
`molten-block-laddlecore.json` (a slab with raised north/south cradle rails) **unused**; it is exported by U11.2
anyway so the decision stays reversible, and its rails are the only thing given up. The core still has to be
*our* block rather than a vanilla slab, because it is the multiblock principal and holds the block entity.

- [ ] **Step 1.** Def with a `brick` variant group over vanilla's brick colours, a down-slab shape, and the
      multiblock layout of U11.4 attached.
- [ ] **Step 2.** Caution: Readability check, and record the answer: three visually identical brick slabs sit in a row
      at L1 (`d C d`). The furnace cores solve exactly this by texturing the north face with an orientation
      marker and the south with a type label (`BlockCupolaFurnaceCore.cs:52-62`). Decide whether the ladle core
      wants the same, or whether disappearing into the wall is the point.

#### U11.4 — The 5×5×4 multiblock layout

**Files**
- Modify: `BlockLadleCore.cs` (the `.MultiblockLayout(...)` block),
  `src/ExpandedLib/Definitions/VanillaCodes.cs` (one new rung)
- Test: `LadleLayoutTests.cs`

**Produces:** the layout above, in the builder, buildable at all four facings

- [ ] **Step 1.** Add `VanillaCodes.AnyBricksOrAir` — `AnyBricks` with `air` folded into the alternation, for
      `b`. Precedent and its warning both exist: `CoalBed` is `@(air|coalpile)`, and its doc-comment already
      records that a cell admitting air **completes with nothing built there**. That is exactly what `b` is
      for — but say so on the constant, or the next reader files it as a bug.
- [ ] **Step 2.** Add a second rung for the optional canal sockets. One helper per canal role rather than a
      single loose one, so the drawing still states *what kind* of fitting belongs in each cell: air-or-start,
      air-or-straight, air-or-tap. Each is `@(air|<the canal wildcard with its orientation group left wild>)`.
- [ ] **Step 3.** Draw the four layers with `Origin(-2, -2)`. **Required** cells: `#` → `AnyBricks`,
      `d` → `AnySlab(down)`, `n` → `AnyStairs(down, NORTH)`, `s` → `AnyStairs(down, SOUTH)`,
      `f` → `ExCodes.Filler`, `L` → the ladle megablock, and the re-lettered core. **Optional** cells:
      `b` → `AnyBricksOrAir`, and `N`/`S`/`A`/`B` + the three taps → the air-or-canal rungs from Step 2.
      Re-letter the duplicate `C` (see the layout note).
- [ ] **Step 4.** Caution: Declare **every** canal glyph with **`LegendAnyFacing`, never `Legend`** — see the
      ruling box above. `Legend` auto-detects a rotating orientation segment and pins it, and a pinned
      self-orienting node breaks the structure the moment the player's plumbing makes the canal re-orient.
      The slabs and stairs **do** stay on `Legend`: they are inert vanilla masonry that only a player moves,
      and their facing is the spout channel's shape.
- [ ] **Step 5.** Build it with `StructureRig`, not by forcing `StructureComplete`. The rig exists precisely
      because `SetProperty(be, "StructureComplete", true)` asserts the conclusion. Assert completion at all
      four angles.
- [ ] **Step 6.** Write the test the ruling demands, and write it first because it is the one a
      requirement-shaped legend would fail: **a ladle raised with no canal in any of the seven sockets
      completes and works.** Then a second case adds a canal to one socket and asserts the structure is
      *still* complete — optional means both directions, and a legend that admitted air but not the fitting
      would pass the first case alone.
- [ ] **Step 7.** Caution: The remaining **negative** test is about masonry, not facing: a brick dropped into a canal
      socket leaves the structure incomplete, because those cells must stay clear for the pour. That is the
      only thing the `@(air|canal)` alternation is still enforcing, so it is the only negative left to write.
      Do **not** write "a tap fitted the wrong way round leaves it incomplete" — that asserts the opposite
      of the ruling.

#### U11.5 — `BlockLadle`: the megablock, its footprint and its construction

**Files**
- Create: `src/IronIndustryExpanded/BlockStructures/Ladle/Blocks/BlockLadle.cs`,
  `.../Ladle/BlockEntities/BlockEntityLadle.cs`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Ladle/LadleFootprintTests.cs`

**Produces:** the megablock, RCC-raised, with the filler footprint above

**Lifecycle — precedent, not invention.** Grid-craft the ladle → place it → RCC **shell** then **lining**. That
is what `lpex:boilercornish-n` and `lpex:enginewatt-n` do (both grid-crafted *and* `.Construction(...)`), and it
matches [ladle.md](../../design/machines/ladle.md)'s own two-stage table and its `ladle-grid` cost key. The
Bessemer is the *other* pattern — control-spawned — and it is the wrong one here: there is no control block in
the layout, and the vessel is the thing the player places.

- [ ] **Step 1.** `FillerOffsets(StructureFootprint.Layout(...))` from the megablock grid, using U11.1's
      `Partial` for `_` (bottom slab) and `I` (west vertical slab).
- [ ] **Step 2.** Stage 1 **shell**: `8 plate + 8 nails + 4 rod` (`ExIngredients`). Stage 2 **lining**:
      **`ExCodes.RefractoryTier(2)`**. **The tier2 pin is a user ruling on the historical route and it buys
      authenticity, not a mechanic** — `conventions.md` makes tier a gate only where the lining reacts with the
      slag, and nothing in this ladle does. That argument was heard and overruled; do not "fix" it back to
      any-tier on the grounds that it gates nothing. And **fire clay is disqualified outright**: it caps at
      1200 °C and this vessel carries pig iron at 1482 °C.
- [ ] **Step 3.** `StructureAngle`, `GetDrops` → `[]`, and the filler triad. Prefer deriving from
      `BlockFilledMegastructure` over re-implementing the triad — it exists to fold exactly this copy-paste.
- [ ] **Step 4.** Break behaviour: fillers cleared **before** the base call, and the RCC drop path wrapped, per
      `BlockConverterBessemer.OnBlockBroken`'s scar (a wildcard with no `storeWildCard` throws to the client and
      crashes the game).

#### U11.6 — Recipe, cost row, lang, handbook

**Files**
- Modify: `src/IronIndustryExpanded/Recipes/Grid/MoltenRecipeDefinitions.cs`,
  `src/IronIndustryExpanded/IiexRecipeConfig.cs`, `assets/iiex/lang/{en,ru,uk}.json`, `docs/iiex/handbook/`

- [ ] **Step 1.** Grid recipe for the ladle frame; row **`ladle-grid`** in the cost catalogue. The catalogue is
      **enforced** since QW10 — a craftable block with no row goes red, so this is a real gate.
- [ ] **Step 2.** Lang in all three locales, one contiguous block, EN first. RU/UK: single `-`, never an
      em-dash. The coverage test walks **block codes only** — item and `ingameerror` rows are unguarded, so
      diff the three locales by hand.

#### U11.7 — Docs sync

**Files**
- Modify: `docs/design/machines/ladle.md`, `docs/design/mechanics/molten-network.md` (R3), `WORKLOG.md`

- [ ] **Step 1.** Fix `ladle.md`'s front-matter **Mod smex** → iwex and § *Code*'s
      `src/SteelIndustryExpanded/BlockStructures/Ladle/` paths, both stale against the ruling on the same page.
- [ ] **Step 2.** Replace § *Assets* (*"Nothing exists"*) and § *Structure* (*"Nothing is drawn yet, and there is
      no cell legend to lay out"*) — both are now false, and the second is doubly so: **there is a cell legend**,
      because the ladle turns out to be a megablock *inside* a multiblock rather than a bare megablock.
- [ ] **Step 3.** State plainly what U11 did **not** build (the merge, the windows, the ferroalloys) so the next
      reader does not mistake a standing ladle for a working one. R3 stays a **reservation**: nothing in code
      enforces "only the ladle merges canals", and no test would notice another block starting to.
- [ ] **Step 4.** Append a `WORKLOG.md` entry. Do not commit — the user owns git history.

### Traps — each of these makes a green suite a lie

- Caution: **Authoring the footprint without U11.1 is invisible.** Full-cube fillers place, link and pass every
  assertion a footprint test makes; the only symptom is in-game geometry. Write U11.1's round-trip assertion
  first, and assert the ladle's own emitted entries carry boxes.
- Caution: **A stub `Block` in the fixture hides all of it.** `TestBlocks.Configure(new Block(), …)` gives the machine
  no layout and no overridden methods, so every geometry answer is 0 and the shipped path is never reached —
  this bit twice in one day on 2026-08-06. Register the real `BlockLadle`/`BlockLadleCore` types, and write the
  premise as an assertion (`Assert.True(cells.Count > 1, …)`).
- Caution: **The duplicate `C` glyph throws at load, not at compile.** `MultiblockLayoutBuilder` refuses it with
  *"a glyph maps to one code"* — which is the good case. The bad case is re-lettering it and forgetting the
  role/legend pair, which resolves to nothing silently.
- Caution: **`b` cells accept air, so the structure completes with the whole outer ring unbuilt.** That is intended, but
  it means the completion test proves less than it looks: a case that builds only the inner cells passes. Assert
  a *wrong* block in a `b` cell fails, or the rung is untested.
- Caution: **The optional-canal ruling makes the completion test nearly vacuous, and that is the trap.** Seven of the
  layout's cells now accept air, on top of the twelve `b` cells — so a "the structure completes" test can pass
  while proving almost nothing about the plumbing half of the drawing. The load-bearing cases are the two
  *directions* of optional (no canal completes; a canal added later still completes) plus the masonry negative.
  A single happy-path completion test here is worse than none, because it reads as coverage.
- Caution: **A pinned self-orienting node is a delayed-action break, and `Legend` pins by default.**
  `BlockMoltenCanal` re-derives its orientation from its neighbours and exchanges itself, so a cell pinned to
  one facing is satisfied at build time and fails later, when the player extends their plumbing and the canal
  legitimately re-orients. No error, no message, and the cause is two blocks away from the symptom. **Every
  canal glyph must use `LegendAnyFacing`.** U10.6 exists to make this unrepresentable; until it lands, this
  layout is the one that would prove why.
- Caution: **The megablock's fillers satisfying `f` is load-bearing and order-sensitive.** If the player raises the
  masonry before placing the ladle, every `f` cell is empty and the structure reads incomplete until the vessel
  goes in — correct, but it must be *tested in both orders*, because a rig that always places the ladle first
  would never see the other one.
- Caution: **`StructureFillers.CanPlace` refuses a footprint that is not clear**, so a ladle cannot be placed into
  already-built masonry. Check that the `f` cells are genuinely empty in the intended build order, or the machine
  is unplaceable in the very structure it belongs to.
- Caution: **An unknown animation name is dropped by the animator with no exception.** `nournorth` vs `pournorth` is
  exactly that shape. Whichever name wins, one test must assert the shape actually contains the clip the code
  asks for.
- Caution: **`GetDrops` → `[]` is not always honoured for a variant block** — the per-side variant can still be handed
  its own code as a fallback drop at registration. Override it, as the Bessemer does, and assert emptiness.

**Gate.** A player can grid-craft the ladle, place it on a laid core, raise it through both RCC stages, and the
5×5×4 structure completes — at **all four facings**, and **with no canal fitted in any of the seven sockets**;
fitting one afterwards, in any orientation the network gives it, leaves the structure complete. A brick dropped
into a canal socket does *not*. The megablock reserves 24 cells with the crank
cell a west slab and the six rim cells bottom slabs, proved by reading the emitted `fillerOffsets` rather than by
placing them. Breaking the ladle clears every filler and returns both stages' materials. `ladle-grid` is in the
cost catalogue. `docs/design/machines/ladle.md` no longer says the ladle lives in smex, has no shape, or has no
cell legend. `./scripts/exmod.sh test 1.21` **and** `1.20` green on all three suites with every floor met.
