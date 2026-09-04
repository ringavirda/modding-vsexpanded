# Research snapshot - mill-flatwide-cells

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** rolling mill footprint vs machines.txt, gap selection, roll-set spec, the flatwide set, the movable-roller ruling, minimal change set for the i1 cells.
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

## Report: the mill's `i1` (raise/lower) cells

### 1. Shipped footprint vs machines.txt

**Def** - `BlockRollingMill.Definitions` `src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockRollingMill.cs:41-62`: variants `type=rollingmill`, `orientation=ns|we` (:52-53); authored `we` = axle along X, `rotateY:0`; `ns` = `rotateY:90` (:55-57); `StructureAngle => ns ? 90 : 0` (:104). Principal `(0,0,0)` is the **east end** of the drive line; `AxleOffsets = [(-1,0,0),(-2,0,0)]` (:97) become `BlockRollingMillAxle` graph nodes (:160-180).

**Footprint DSL** (:72-92): `f.Solid('-').Origin(-2,-1)`, layer 0 `- - - / . . O / - - -`, layer 1 `. . . / # # # / . . .`. Note `'-'` is registered as a **full solid**, not a slab (:74). Golden `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/forming/rollingmill.json:17-63` pins nine `fillerOffsets`: `(-2..0,0,-1)`, `(-2..0,0,+1)`, `(-2..0,1,0)`; `ExOrientable` mode `network`, scheme `AxisFlat` (:80-88).

**Decks** - `BlockEntityRollingMill.Deck(bool far)` `.../BlockEntities/BlockEntityRollingMill.cs:376-383` = `GlobalPos(Pos,0,0,far?1:-1,angle)`; `InputDeck => Deck(DriveReversed)` (:369), `OutputDeck` opposite (:372); `DriveReversed` reads `MpEnergyNetwork.State.Reversed` (:361-363). `DeckRow(bool far)` yields local x `0 -> -(MillFeed.DeckCells-1)` (:390-394); `IsInputDeck(BlockPos)` accepts the whole row (:405-410). `MillFeed.DeckCells = 3` `.../Forming/MillFeed.cs:74`, `DeckOriginOffset = 2` (:77), `AlongBarrel(double localX)` (:84-85), `GapZone(float, int)` (:59-64), `SideIndex(bool, int)` (:70-71). Block-side `AlongBarrel(BlockPos, BlockPos, BlockSelection)` (BlockRollingMill.cs:355-375) via `ExOrientation.UnrotateXZ`.

**machines.txt** `docs/internal/workbench/machines.txt:187-217`: L1 `_ I1 _ / m O m / - I2 -`, L2 `_ . _ / i1 i2 i1 / - . -`; `I1` south / `I2` north vertical slab (:199-200); `i1` "hold interactions of razing and lowering of rollers if current rollers are flat wide ... this way we can collapse all flat wide rollers into one item" (:201-203); the next line says `i1` "is where player interacts to replace rollers" (:204-206) - almost certainly means `i2` (UNVERIFIED); `-`/`_` are vertical north/south slabs here (:206-207), overriding the file legend (:12-13).

**Deltas** (code frame, `we`):

| machines.txt | Code today | Change needed for i1? |
|---|---|---|
| `O` in the centre column, `m` MP-port fillers either side | `O` at x=0, axle node blocks at x=-1,-2 | No. Moving `O` is a save migration (rolling-mill.md:91-94 keeps the axle block for that reason) |
| `i1 i2 i1` = stand row, y=1 | already fillers `(-2,1,0)`, `(-1,1,0)`, `(0,1,0)` (golden :49-62) | **None added**: i1 = `(0,1,0)` and `(-2,1,0)`, i2 = `(-1,1,0)` - routing only |
| I1/I2 = middle deck cell only; corners slabs | whole row feedable, all nine cells solid | Keep the row for flat/grooved (B17, rolling-mill.md:527); slabs are cosmetic (`FillerLayoutBuilder.Slab(char, BlockFacing)` `src/ExpandedLib/Blocks/Structures/FillerLayoutBuilder.cs:88-92`; shipped use `BlockShear.cs:77-78`) - optional, re-blesses the block golden |
| L2 deck corners slabs | empty | optional, same |
| i2 fits the set | set fits "anywhere" (BlockRollingMill.cs:247-248) | optional narrowing |

### 2. How the gap is chosen today

`RollSetSpec(int Schema, string Family, string[] Accepts, float BarrelWidth, float MinTorque)` `.../Forming/RollSetSpec.cs:24-30`; **no `Gaps` field** - dropped 2026-08-12 (docs/design/items/roll-sets.md:160-162). `TryParse` (:61-110): `family` required (:77-82), `accepts` >=1 (:84-93), `barrelWidth` >0 (:95-100), `minTorque` optional default 0 (:107); `AcceptsForm` (:55-56), `PassesAt` (:41-45), `OverhangsBarrel` (:49-50).

Gaps live on `MillSchedule.Gaps` `.../Forming/MillSchedule.cs:40` = `route.RungsFor(set.Family)` thicknesses (:27, :63); `For(RollSetSpec?, string? form, ProcessRouteRegistry?)` (:51-65); `IsWide => Stages.Count == 1` (:44); `NextGap` (:70-75). `ProcessRoute.RungsFor` excludes `HalfStep` stages, descending (`src/ExpandedLib/Processes/ProcessRoute.cs:43-46`); `SameThickness` tolerance `1e-4` (:37, :57-58); `ProcessStage(... bool HalfStep=false)`, `IsRung => !HalfStep` (`ProcessStage.cs:21-28`, :39).

**Ladders** (`assets/*/config/processroutes/`): `shingledslab.json:5-23` rungs 2.5/2.0 (`flatwide` only); `heavyplate.json:5-25` 1.5/1.0 -> `iiex:boilerplate`; `shingledbar.json:28-51` flat+flatwide 2.5/2.0 -> `iiex:beam`; `rod.json:21-44` and `beam.json:5-27` 1.5/1.0; siex `castbloom.json:4-41`, `castslab.json:4-41` **3.5/3.0/2.5/2.0/1.5/1.0**, all `flatwide`, no codes. So the flatwide "ladder" is per stock family, not per set; on cast stock `Gaps.Length == 6` and the deck splits into six bands over three cells - wide-hall.md:175-183's "GapZone returns 0 on every wide set" is stale.

**Feed path** `BlockRollingMill.Feed` (:303-349): `offered = WorkPiece.FromStack(mill.Admit(held))` (:317), `schedule = mill.ScheduleFor(offered)` (:318), `gap = GapZone(AlongBarrel(...), schedule?.Gaps.Length ?? 1)` (:319-323), `sides = WorkPiece.SidesFor(offered.Width, mill.RollSet.BarrelWidth)` (:324-327), `mill.TryFeed(held, gap, side)` (:330); verdict->error key (:338-347). This is "the deck's gap mapping" (rolling-mill.md:207-209). `TryFeed(ItemStack?, int gapIndex, int side)` (BE:258-304) -> `MillFeed.Decide(RollSet, schedule, piece, gapIndex, side, tempC, IiexValues.RollingRollRadius, IiexValues.RollingTempC)` (:277-286), `_pendingGap = schedule.Gaps[gapIndex]` (:296).

**`MillFeed.Decide`** (:103-143) order: NoRollSet -> WrongForm -> index range -> PartCropped -> `gap >= Thickness` NoReduction -> `IsFed(side)` NoReduction -> TooCold -> `draft = Thickness - RoundTarget(gap)` (:139) -> `RollingPass.CanBite ? Ok : WontBite` (:140-142).

**Friction bound** `.../Forming/RollingPass.cs`: `HotFriction = 0.3f` (:21), `ColdFriction = 0.055f` (:25), `MaxDraft = mu^2R` (:31-32), `CanBite(draft, R, tempC, rollingTempC)` (:39-46). With `RollingRollRadius = 4` (`src/IronIndustryExpanded/IiexConfig.cs:883`) delta_max = 0.36: one round's 0.25 bites, a skipped gap's 0.5 skids (rolling-mill.md:263-268). `WorkPiece.RoundTarget` `.../Forming/WorkPiece.cs:89-92`, `FeedsPerSide = 2` (:34). Rolling keys: `RollingTempC` 900 (:868), `RollingColdStressMultiplier` 10 (:873), `RollingColdSpanC` 400 (:878), `RollingLoadTorque` 0.34 (:893), `RollingCoolRate` 0.005 (:909), `RollingAmbientC` 20 (:930); `IiexValues` is source-generated from `IiexConfig` (`src/ExpandedLib.Generators/ExConfigGenerator.cs:13-16`, :243-244), so a new property is an accessor for free.

### 3. The flatwide set today

`iiex:rollset-flatwide` - `RollSetItemDefinitions.Sets["flatwide"]` `.../Forming/RollSetItemDefinitions.cs:59-68`: family `flatwide`, accepts `["shingledslab","shingledbar","castbloom","castslab","heavyplate"]` (:61), `barrelWidth 16.0` (:66), `minTorque 0.5` (:67); the comment (:62-65) already records the movable-roller ruling. Golden `goldens/iiex/itemtypes/rollset.json:37-51`; lang `item-rollset-flatwide` "Wide Flat Roll Set" `assets/iiex/lang/en.json:109`. Accepting ladders: all seven above.

Wider than the barrel: `WorkPiece.SidesFor(width, barrelWidth) = max(1, ceil(width/barrelWidth))` (WorkPiece.cs:50-53); `ForSides(sides)` resets `Fed = new bool[sides]` (:72-77); `IsFed` (:81-82); `Feed(index, gap)` moves the gauge only when every side is fed (:99-117). At barrel 16 against `MaxWidth` 14 (`StockForm.cs:67`) / 15 (:114) / cast 15 (siex `CastStockForms.cs`, UNVERIFIED value) `sides` is always 1.

**Ruling** `docs/internal/worklog/2026-08.md:2006-2013`: "** **The wide roll sets are cancelled, not deferred** *(owner ruling)*. steel-roll-sets.md proposed `rollset-flatwide35` and `-flatwide30` as smex items so 4-thick cast stock could be bitten. It should not: the wide stand's top roller is the movable one and the player sets the gap on the mill itself ([machines.txt]'s `i1` cells), so **one wide set covers every wide gap** and 3.5 / 3.0 are ordinary rungs on the two cast ladders. !! The cost is exactly what that page's own open question warned of: one item carries one `MinTorque`, so the wide route has **no torque gate at all** and cast stock rolls behind the iron-tier 0.5." Restated at `docs/design/machines/steel-roll-sets.md:3-8`, :180-183, :301 and `rolling-mill.md:58-66` ("holds RMB on the mill's raise/lower cell ... Friction is unchanged ... a screwed-down skip still skids"). `NEXT.md` carries nothing (grep empty); roadmap row `docs/internal/plans/2026-09-04-roadmap.md:73` and `STATE.md:110` list it "not built".

### 4. The pass-through family

machines.txt section Mp bending machine (:16-40): "Which cell of two is currently intractable is based on input shaft rotation direction" (:22-24); "'i' is the cell that has interaction about setting the height (gap) of the movable roller. Player pushes it down to achieve bigger bending curve" (:25-27). The mill note cites it as "the same mechanic" (:202). The mill already has the direction half: `InputDeck => Deck(DriveReversed)` (BE:369). `docs/design/machines/bending-roller.md`: shares consumer/tooling/BE base, not the block (:66-69, :126-128); verbs "mirror `BlockRollingMill.HandleInteract` exactly" (:194-203); never a roll set (:284-286); nothing built (:3-6). Both are the only "sequence" machines (`docs/design/mechanics/process-extension.md:112-120`).

### 5. Storing a per-block gap; block info

Persistence is an `ExBlockState` chain (BE:622-630): `.Float("rmTemp")`, `.Float("rmRemaining")`, `.Bool("rmStalled")`, `.Float("rmPendingGap")`, `.Int("rmPendingSide")`; `ToTreeAttributes` -> `State.ToTree(tree)` (:632-635), `FromTreeAttributes` -> `State.FromTree(tree, world)` + `MigrateLooseStacks` (:637-644). API: `ExBlockState.Float(string key, Func<float> get, Action<float> set)` `src/ExpandedLib/Blocks/ExBlockState.cs:65` (`Bool` :56, `Int` :59). Stacks are container slots `RollSetSlot = 0`, `PieceSlot = 1` (:62-65, :85-93). How an absent key reads back: UNVERIFIED (check `ExBlockState.FromTree` :162).

`GetBlockInfo` (:596-612) prints only `iiex:rollingmill-info-idle` / `-rolling` / `-stalled` with `ExMeasure.Temperature` (en.json:111-113; ru.json:149-151; uk.json:149-151). **The fitted set is not shown** - rolling-mill.md:135 overstates. Help keys `iiex:rollingmill-help-feed/-feed-far/-free` (BlockRollingMill.cs:406-425; en.json:87, :100-101). Guards: `LangKeyResolutionTests` (every literal `Lang.Get("domain:key")` must exist, `test/ExpandedLib.Tests/Localization/LangKeyResolutionTests.cs:11-60`), `LangParityTests` (ru/uk key and placeholder parity, `LangParityTests.cs:11-19`); `ExLangKeyGenerator` emits `IiexLang` constants for every en.json key (`src/ExpandedLib.Generators/ExLangKeyGenerator.cs:12-19`).

**Hold mechanics**: `IFillerInteractionTarget.OnFillerInteractStep/Stop` (`src/ExpandedLib/Blocks/Structures/IFillerInteractionTarget.cs:24-38`), forwarded per clicked cell by `BlockStructureFiller.OnBlockInteractStep/Stop` (`BlockStructureFiller.cs:261-302`). The mill's are inert (:377-391). The one shipped filler-cell hold is `BlockBoiler.HandleInteractStep` (`.../Boiler/BlockBoiler.cs:327-370`): empty hand only (:346-348), act once past `HatchHoldSeconds`, keep returning `true` until release (:350-369), mutate server-side only (:358). Roadmap Phase 2 item 6 makes hold-to-operate an exlib feature (roadmap:177-178); :60-61 says it is the only missing framework piece. Plan decision: local boiler-style hold now, or wait.

### 6. Tests to copy

| File | Fixture |
|---|---|
| `test/IronIndustryExpanded.Tests/Blocks/Forming/RollingMillFeedTests.cs:23-50` | `Mill(orientation)`: `TestWorld`, `RegisterNetwork("mpenergy", ...)`, `TestBlocks.Configure(new BlockRollingMill(), $"iiex:forming-rollingmill-{o}", 1, ("type","rollingmill"), ("orientation",o))`, `world.Place`, `world.Attach`, `world.AddNode(pos,"mpenergy")`, `ReflectionHelpers.SetProperty(mill,"NetworkSystem",world.Networks)`; `Fitted()` :251-274 (inline `rollset` JSON + `TryFitRollSet`); `FittedWithOutput` injects `Routes` (:321); `RunPass` (:54-63) |
| `RollingMillStationTests.cs:272-325` | drives private `BlockRollingMill.AlongBarrel` via `ReflectionHelpers.Invoke` at both facings - the pattern for i1 cell routing; `LegacyTree` :134-140 for tree round-trips |
| `MillFeedTests.cs:19-95` | pure `MillFeed.Decide` with `FlatSet` + `BloomRoute()` |
| `ShippedRollSetTests.cs:23-200` | shipped sets from `RollSetItemDefinitions.Definitions("iiex")`, routes from `ProcessRouteSeeds.Shipped()`; walkability round-by-round (:75-120), skip refused (:157-180), dead sets empty (:137-154) |
| `RollingMillLoadTests.cs:23-110` | real BE on `MpEnergyNetwork` with `DriveNode`/`FlywheelNode` |

Goldens: `EXLIB_WRITE_GOLDENS=<path fragment>` scoped bless (`test/ExpandedLib.Testing/DefinitionGoldens.cs:144-147`, :169-181). `ShippedSpecSchemaGuards` pins `schema` on shipped specs (`test/IronIndustryExpanded.Tests/Invariants/ShippedSpecSchemaGuards.cs:27-31`, :82-98); an added optional field at schema 1 is permitted (process-extension.md:317-321).

### Minimal change set for the i1 cells

**Modify**
- `RollSetSpec.cs`: add `bool Adjustable` (JSON `adjustable`, default false, parsed beside `minTorque` :107) - tooling declares its own screw-down (process-extension.md:149-151) rather than the mill testing `Family == "flatwide"`. Optionally `float GapMin/GapMax/GapStep` on the spec instead of config.
- `RollSetItemDefinitions.cs:24-38, :59-68`: `Set(..., bool adjustable = false)`; flatwide `adjustable: true`.
- `BlockEntityRollingMill.cs`: `private float _wideGap;` + `.Float("rmWideGap", ...)` (:624-630); `public float WideGap`; `public bool HasAdjustableSet => RollSet?.Adjustable == true`; `public bool TryStepWideGap(int direction)` (refuse `IsRolling`, clamp/step, `MarkDirty(true)`); `public int? WideGapIndex(MillSchedule? schedule)` (first `i` with `ProcessRoute.SameThickness(schedule.Gaps[i], _wideGap)`); reset `_wideGap` to max in `TryFitRollSet` (:228-247) when the set changes; `GetBlockInfo` adds set name + gap lines.
- `BlockRollingMill.cs`: `private static readonly Vec3i[] RaiseLowerOffsets = [new(0,1,0), new(-2,1,0)]`; `public bool IsRaiseLowerCell(BlockPos principal, BlockPos cell)` (rotate via `ExOrientation.RotateOffset`, cf. `AxleCells` :111-116); `HandleInteract` (:229-253): before the `held == null` return (:249), empty-hand click on an i1 cell with an adjustable set starts the hold, locked set -> error; `OnFillerInteractStep` (:377-383) boiler-pattern: every `RollingWideGapHoldSeconds` call `TryStepWideGap(sneak ? +1 : -1)` (mapping UNVERIFIED - machines.txt only says "pushes it down"); `Feed` (:319-323): `int gap = mill.WideGapIndex(schedule) ?? MillFeed.GapZone(...)`, and an off-ladder gap gets its own verdict; `GetFillerInteractionHelp` (:393-426) adds two entries for i1.
- `MillFeed.cs`: `FeedVerdict.GapOffLadder` (:6-33) + mapping in `Feed`'s switch (:338-347); optionally `public static int? GapIndexFor(MillSchedule schedule, float gap)` as the pure half.
- `IiexConfig.cs` Rolling region (:860-931): `RollingWideGapMin` 1.0f `[ExConfigRange(0.25,16)]`, `RollingWideGapMax` 3.5f, `RollingWideGapStep` 0.5f, `RollingWideGapHoldSeconds` 0.5f `[ExConfigRange(0.05,10)]`.
- Lang en/ru/uk: `iiex:rollingmill-help-lower`, `iiex:rollingmill-help-raise`, `rollingmill-info-rollset` ("Fitted: {0}"), `rollingmill-info-gap` ("Roll gap: {0}"), `game:ingameerror-iiex-rollingmill-gaplocked`, `game:ingameerror-iiex-rollingmill-gapoffladder`; revise the handbook clause en.json:617 ("rightmouse the near or the far side ... to choose which gap") for the wide set (handbook parity guard - which test, UNVERIFIED).
- Docs: rolling-mill.md:58-66 status, wide-hall.md:175-186 (stale), roadmap:73, STATE.md:110.

**New tests** `RollingMillWideGapTests.cs` (copy `RollingMillFeedTests.Mill()` + an `adjustable` flatwide JSON + a six-rung route via `Routes`): step clamps to `[Min,Max]`; refused while `IsRolling`; `rmWideGap` survives `ToTreeAttributes`/`FromTreeAttributes`; feed uses the stored gap regardless of deck position; fresh 4.0 at 3.0 -> `WontBite` (0.5 > 0.36); off-ladder gap -> `GapOffLadder`; locked set on i1 -> error; i1 cells resolve at `we` and `ns` (StationTests pattern). Add to `ShippedRollSetTests`: every rung accepted by an adjustable set lies within `[RollingWideGapMin, RollingWideGapMax]` on the step grid.

**Goldens to re-bless**: `goldens/iiex/itemtypes/rollset.json` (new spec field; `EXLIB_WRITE_GOLDENS=iiex/itemtypes/rollset`); `goldens/iiex/blocktypes/forming/rollingmill.json` only if slab cells or the principal move (not needed for i1).
