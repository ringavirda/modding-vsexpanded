# Research snapshot - footprints-shapes-defs

**Written** 2026-09-04 by a read-only research agent, against commit `cf2e8c89` (the tree before the
line-ending renormalisation). **Covers** how megablock defs are authored, machines.txt transcribed to filler cells, the nine mpenergy editables measured, export tooling, goldens, lang guards; a def sketch for the machine-tool family.
**Purpose** grounding for the roadmap's Phase 1 walkthrough and Phase 2 machining-line plan
(`docs/internal/plans/2026-09-04-*.md`).

Line numbers were right on that day and drift; cite by symbol when reusing. "UNVERIFIED" marks are the
agent's own and were not re-checked. Nothing here is a decision - decisions live on the design pages.

---

## 1. How the shear and the fastener bench author their defs today

Both are `BlockNetworkNode, IExBlockDefProvider, IFillerHost, IFillerInteractionTarget` with `NetworkType => "mpenergy"` (`src/IronIndustryExpanded/BlockStructures/Forming/Blocks/BlockShear.cs:29-35`, `BlockFastenerBench.cs:32-38`). Neither is RCC - `Construction(Action<ConstructionStages>)` (`src/ExpandedLib/Definitions/ExBlockDef.cs:873-877`) is not called, despite `docs/design/mechanics/machining-line.md:197-198` saying machines are "built as RCC".

**Shear chain** (`BlockShear.cs:42-64`): `ExBlockDef.Create(domain, "forming", "forming/shear")` -> `.Class<BlockShear>()` -> `.EntityClass<BlockEntityShear>()` -> `.Material(EnumBlockMaterial.Metal)` -> `.Sound("walk","game:walk/metal").Sound("place","game:block/anvil")` -> `.MaxStackSize(1)` -> `.Handbook("forming-shear-*")` -> `.VariantGroup("type","shear")` -> `.VariantGroup("orientation","ns","we")` -> `.NetworkOriented()` -> `.ShapeByType("*-ns","iiex:forming/shear", rotateY: 0)` / `("*-we", ..., rotateY: 90)` -> `.CreativeCommon("*-ns")` -> `.FillerOffsets(Footprint)` -> `.EntityBehavior("Animatable")` -> `.SolidNonOpaque()`. Golden: `test/IronIndustryExpanded.Tests/goldens/iiex/blocktypes/forming/shear.json`.

**Bench chain** (`BlockFastenerBench.cs:53-88`): same, but code shared (`Create(domain,"forming","forming/bench")`), `.VariantGroup("type", NailCutter, Riveter)`, four `ShapeByType($"*-{type}-ns|we", "iiex:forming/{type}", rotateY: 0|90)`, `.FillerOffsetsByType($"*-{NailCutter}-*", NailCutterFootprint)` / `($"*-{Riveter}-*", RiveterFootprint)`, no `Handbook` (reason at `:63-66`). Golden `.../forming/bench.json:54-102` writes `attributesByType.{wc}.fillerOffsets`.

**Signatures** (`ExBlockDef.cs`): `Create(string domain, string code)` :39; `Create(string domain, string code, string assetName)` :46-50; `VariantGroup(string code, params string[] states)` :445; `NetworkOriented()` :498-514 (emits `ExOrientable {mode:"network", scheme}` resolved from the `orientation` states - golden shows `AxisFlat`); `Shape(string baseShape)` :239; `ShapeByType(string wildcard, string baseShape, int? rotateX=null, int? rotateY=null, int? rotateZ=null)` :523-539; `ShapeSpunPerOrientation(string baseShape, int offset=0)` :267-275 (single `shape` + `rotateYByType` for `*-n/e/s/w` via `ExOrientation.AngleFromSide`); `ShapeSelectiveElements(params string[] elements)` :258-261; `FillerOffsets(IEnumerable<FillerCellSpec> cells)` :793-796; `FillerOffsetsByType(string typeWildcard, IEnumerable<FillerCellSpec> cells)` :801-815; serialiser :817-856 emits `{x,y,z}` + `behaviors[{code[,face][,properties]}]` + `collisionBox` (one) / `collisionBoxes` (several) + `allowAttach` + `portFace`/`portNetwork`.

`src/ExpandedLib/Blocks/Structures/StructureFootprint.cs`: `record struct FillerBehaviorSpec(string Code, string? Face = null, object? Properties = null)` :17-21, `FillerBehaviorSpec.Of<T>(string? face=null, object? properties=null) where T : BlockEntityBehavior` :29-43; `record struct FillerCellSpec(int X, int Y, int Z, bool AllowAttach = false, IReadOnlyList<FillerBehaviorSpec>? Behaviors = null, IReadOnlyList<Cuboidf>? CollisionBoxes = null, string? PortFace = null, string? PortNetworkType = null)` :56-65; `FillerSlab.Half(BlockFacing)` :75-83 (NORTH -> `z 0..0.5`, DOWN -> `y 0..0.5`, EAST -> `x 0.5..1`); `StructureFootprint.Layout(Action<FillerLayoutBuilder>)` :126-132; `Validate` :148-160.

`FillerLayoutBuilder.cs`: `Origin(int a,int b)` :37; `Solid(char)` :45; `Attach(char)` :52; `Host(char symbol, params FillerBehaviorSpec[] behaviors)` :69-76 (**forces `allowAttach:true`** :73); `Slab(char symbol, BlockFacing half)` :88-92; `Port(char symbol, BlockFacing face, string networkType)` :104-112; `Layer(int y,string)` :116; `Slice(int x,string)` :124; `Face(int z,string)` :133; `Build` guards (one grid kind, `'O'` only at origin, unregistered glyph) :138-186.

**Interaction cells** - no spec field. Every filler forwards to `IFillerInteractionTarget.OnFillerInteractStart/Step/Stop(float secondsUsed, IWorldAccessor, IPlayer, BlockSelection principalSel, BlockPos clickedCell)` + `GetFillerInteractionHelp` (`IFillerInteractionTarget.cs:16-46`); the principal compares `clickedCell` with a code-side `Vec3i` rotated by `StructureAngle` (`BlockShear.cs:92,106-112,199`; bench `Faces` dict `:142-145,162-168,256`). The shear's `Step` returns false (`:285-291`) - hold-to-operate is the unbuilt mechanic (`machining-line.md:372-374`).

**MP cells** - the shipped shear/bench have none: the principal is the node (ns/we faces). Vanilla-MP hosted port: `f.Host('M', FillerBehaviorSpec.Of<BEBehaviorMPFillerPort>("north"))` (`BlockFlywheel.cs:37-40,47`; blower `BlockTwinTubMPBlower.cs:36-37,74`); `BEBehaviorMPFillerPort` (`:20-23`) exposes `IsTurning` :40, `Speed` :52, `IsReversed` :60, `ConfigureFromFiller(BlockPos?, BlockFacing?, JsonObject?)` :62-71. For the exlib `mpenergy` graph, a filler joins as a node only via a hosted `BEBehaviorNetworkMember` (`BEBehaviorNetworkMember.cs:21-25` implements `IFillerHostedBehavior`; `passThrough` :116, `networkType` :197-211); example `multiblock.md:543-547`; connector-vs-node table `:518-526`. **No shipped def hosts an mpenergy membership on a filler** (`grep passThrough src/` -> only Networks/). Passive `Port(...)` is the boiler's pipe arm (`:497-516`).

`StructureAngle => Variant?["orientation"] == "we" ? 90 : 0` (`BlockShear.cs:99`, `BlockFastenerBench.cs:152`); `FillerOffsets => Attributes?["fillerOffsets"]` `:95`; place/remove triad `:114-146`.

## 2. machines.txt -> filler cells

**Frame, verified**: `StructureLayout.Parse` rows +Z / cols +X (`StructureLayout.cs:31-41`), `ParseVertical` (Slice) rows -Y / cols **+Z** (`:63-73`), `ParseFrontal` (Face) rows -Y / cols +X (`:95-105`). VS `NORTH = (0,0,-1)` (`.compat/Vintagestory/vsapi/Math/BlockFacing.cs:46`). So the owner's "XZ layers, north is up" and "XY slice, north is forward/front" transcribe literally; **"YZ slice, north is right" does not** - the code's `Slice` puts north on the *left*. The shipped bench transcribed the nail cutter literally, so its `m`/`#` sit at z=+1 (south) (`BlockFastenerBench.cs:109-115`, golden `bench.json:62-71`). Rotation: n 0 / w 90 / s 180 / e 270, 90 deg maps `(x,z)->(z,-x)` (`ExOrientation.cs:19-25,32-44`). Origin = negation of `O`'s (col,row). Cells below are the literal transcription (`_`/`-` slab meanings per each machine's Note, `machines.txt` lines cited):

| Machine (grid) | Origin | Cells (glyph(x,y,z)) | Kinds / drive / I / i |
|---|---|---|---|
| cutter `:43-59` (Face 0) | (-1,1) | `_`(-1,1,0) `_`(0,1,0) `_`(1,1,0) `I`(-1,0,0) `#`(1,0,0) | `_` bottom slab; O = N-S drive (principal node); I window. = shear golden |
| drill press `:62-77` (Slice 0) | (-1,2) | `#`(0,2,-1) `#`(0,2,0) `#`(0,2,1) `I`(0,1,-1) `#`(0,1,0) `#`(0,1,1) `I`(0,0,-1) `M`(0,0,1) | I = **north vertical slab** x2 (window+bit); M = N-S drive on filler. Literal puts I north (z=-1), M south - mirror of the text |
| horizontal bore `:80-101` (Layer) | (-1,0) | y0: `m`(-1,0,0) `#`(1,0,0) `i`(0,0,1) . y1: `#`(-1,1,0) `I`(0,1,0) `#`(1,1,0) | m = E-W drive on filler; I window above O; i = hold, north vertical slab |
| lathe `:104-125` (Layer) | (-1,-1) | y0: `_`(-1,0,-1) `_`(0,0,-1) `_`(1,0,-1) `#`(-1,0,0) `m`(1,0,0) . y1: `i`(0,1,-1) `I`(-1,1,0) `I`(0,1,0) `#`(1,1,0) | `_` = **south** vertical slab (redefined); i = hold + south vertical slab; two I windows |
| nail cutter `:128-141` (Slice 0) | (0,1) | `I`(0,1,0) `m`(0,1,1) `#`(0,0,1) | I = window + hold; m = E-W. Shipped as plain `Solid` |
| planer `:144-169` (Layer) | (-2,-1) | y0: `#`(-1,0,-1) `#`(0,0,-1) `M`(1,0,-1) `I`(-2,0,0) `I`(-1,0,0) `#`(1,0,0) `#`(2,0,0) `-`(1,0,1) . y1: `i`(0,1,-1) `_`(1,1,-1) `i`(0,1,0) `#`(1,1,0) `i`(0,1,1) `-`(1,1,1) . y2: `_`(1,2,-1) `#`(1,2,0) `-`(1,2,1) | `-` north / `_` south vertical slabs; `i` = hold + **east** vertical slab; O has I's interactions; M = N-S |
| riveter `:172-184` (Face 0) | (-1,1) | `#`(-1,1,0) `M`(0,1,0) `#`(1,1,0) `I`(-1,0,0) `#`(1,0,0) | I = window + hold; M = N-S above O. = bench golden |
| shaper `:220-232` (Slice 0) | (0,1) | `I`(0,1,0) `#`(0,1,1) `#`(0,1,2) `m`(0,0,1) | I = window + hold; m = E-W. Literal puts `# #` south; shape extends north (section 3) |

**Ambiguities**: (a) Slice N/S mirror (drill, nail, shaper) - the drill's "north vertical slab" I only makes sense as the half nearest the body if I is *south* of O; (b) which arm carries `M`/`m` - vanilla-MP port, mpenergy membership, or plain filler as the bench ships; the shaper must take both drives (`machining-line.md:83-87`); on bore/lathe/shaper/nail cutter only the filler has a connection, so the principal cannot be the sole node; (c) `Host` makes M/m attachable; (d) the cutter's window vs `BlockShear`'s click verb (`:379-381`); (e) `_`/`-`/`I`/`i` are redefined per Note (lathe `_` = south vertical; drill `I` = slab); (f) planer "O has the same interactions as I".

## 3. The editable shapes (`assets/editable/shapes/machines/mpenergy/`)

Extents are axis-aligned, rotation ignored (cells = voxels/16; principal cell 0..1).

| Shape | Top-level (after `Root` lift) | Clips code->name (frames, end) | x / y / z (cells) | vs footprint |
|---|---|---|---|---|
| cutter | Root{Base,ShaftGroup,CutterGroup} | idle(30,Hold) cycle(60,Repeat) | -1.00..1.94 / 0..1.62 / 0..1.00 | fits |
| drillpress | Root{Base,DrillHousing,Bed,Belt} + **Items**{GearBlankLarge2} | idle, cycle(30), **drilldown->"drill"**(60,EaseOut) | -0.25..1.60 / 0..3.62 / -1.00..2.69 | needs x+/-1, y=3, z=2; footprint is one column |
| horizontalboring | Root{Base,ShaftGroup,SlidingBed,ControlGroup} + Items{Cylinder} | idle, cycle, **bedmove->"cylinderbore"**(120) | -0.56..2.00 / 0..1.94 / -0.31..1.52 | 5 vox into unreserved z=-1 |
| lathe | Root{Base,GearGroup,SpindleGroup,TurretGroup} + Items{Cylinder1,CylinderHeavy2,Shaft1,RollerBlank11} | idle, cycle, cylinderlathe, cylinderheavylathe, shaftlathe, rollerslathe (60) | -1.25..2.38 / 0..2.50 / -0.72..1.25 | 4/6 vox over x; 8 over y; 3.5 past the south-slab half; 4 into z=+1 |
| nailcutter | Root **from [0,0,-16]**{Base,MachineCasing,ShaftGroup,CutterGroup,NailTray} | idle, cycle | -0.19..1.04 / 0..1.91 / **-1.00..0.81** | body is north (z=-1); shipped footprint is z=+1 |
| planer | Root{Base,ToolSupport,Machinery,MovingBed,MovableTurret} | idle, cycle | -2.00..2.56 / -0.07..4.47 / -0.62..2.00 | 1.5 cells above y=2; 9 vox past x=2 (doc says 73x64x46, `machining-line.md:396` - UNVERIFIED which) |
| riveter | Root{Base,ShaftGroup,RiviterBottom,RiviterTop,Levers} | idle, cycle | -1.00..2.25 / 0..2.26 / 0..1.00 | 4 vox over x and y |
| shaper | Root{Base,ItemBed,WorkPart,Items} | idle, cycle, **gearblanksmall->"gearblank"**(30) | -0.12..1.22 / 0..2.12 / -1.38..1.22 | extends north; 6 vox into z=-2 |
| rollingmill | Root + RollersFlat/Grooved/FlatWide20/15/10/5 | idle, cycle | -1.56..1.17 / 0..1.94 / -0.38..3.50 | shipped |
| bending | Root{Base,StaticRollers,Support,MovableRoller} | idle, cycle, rollergap10..60(Hold) | -1.19..3.88 / -0.12..2.88 / -1.00..1.62 | not in scope |

Textures: every file has `cast-iron1` as `F:/repos/.../editable/textures/materials/cast-iron1`; cutter/bore/lathe/nail/riveter also point `iron*` at `F:/.../.game/1.22/...`; drill/planer/shaper use bare `block/metal/...`. All keys (`cast-iron1, iron, iron2, iron3, iron4, iron5, plain`) are in `TEXTURES` (`scripts/tools/convert-shape.py:40-55,76`). ! The remap is **by key** (`:249-252`): drill/planer/shaper/lathe author `iron` = `plate/iron` but ship `tarnished/iron` (`:41`); bore's `iron2` (plate/iron) ships `sheet-plain/iron2` (`:46`). Clip rewrite keys on `name` (`:256-263`): `drill` is neither in `HOLD_CLIPS` (`:115-121`) nor `ONESHOT_CLIPS` (`:141`) -> exported as Repeat; add it to `ONESHOT_CLIPS` if it is a one-shot.

## 4. How the shipped shapes were exported

Runtime: `assets/iiex/shapes/forming/{shear,nailcutter,riveter}.json`, referenced as `iiex:forming/shear` (`BlockShear.cs:57-58`) and `iiex:forming/nailcutter|riveter` (`BlockFastenerBench.cs:70-81`). Invocation: `python scripts/tools/convert-shape.py <editable-name> <runtime-path> [pairs...]` where the name is relative to `assets/editable/shapes` without `.json` (`:19-26,239-241`; e.g. `machines/steam/machine-pipe-megablock-boiler-cornish-new assets/iiex/shapes/boiler/cornish.json`, `docs/internal/plans/2026-08-23-cornish-boiler-megablock.md:242-244`). `--check` walks the editable tree recursively and lists texture keys with no mapping (`:290-318`); `convert` refuses to write on an unmapped key (`:266-275`). `unwrap_root` lifts children of a top-level group named `Root`, folding the group's `from` into each child; other top-level groups (`Items`) are left alone (`:210-236`). Non-hold, non-oneshot clips become `Repeat` and get `close_loop` (`:151-195,255-263`).

History: shear.json exported 2026-08-15 (`a01d7333`) before `unwrap_root` (`bdfd14fb`, 2026-08-21) - it **still carries `Root/...`**, so selective paths must be `Root/CutterGroup/*`. nailcutter.json (2026-08-21, `2dcb4d2b`) is lifted but predates the `from` fold (`f9389b0a`, 2026-08-23): shipped bbox z 0..29 vs a fresh export's -16..13. A re-export moves it one cell north and off the shipped z=+1 footprint. UNVERIFIED which renders correctly in game.

Selective elements: def-level `.ShapeSelectiveElements("MasonryBase/*")` (`BlockBoilerCornish.cs:47`), `"Root/Cylinder/*"` (`BlockEngine.cs:232`), `"Base/*"` (`BlockTransmission.cs:87`), `ProcessItemEmitter.cs:109`; runtime per-BE: `ConstructedAnimator.Rebuild(string[]? selectiveElements)` (`ConstructedAnimator.cs:121-145`), `ExShapeElements.Pruned(Shape, IReadOnlyCollection<string>)` / `Matches` (`ExShapeElements.cs:55,28`), and the hearth's `ExMeshCache` per-state mesh (`BlockEntityHeatingHearth.cs:180-206`). The design contract (`Elements` vs `Anchor`, `Animation` defaults to `cycle`) is `machining-line.md:284-288`; `RenderSpec` is not built (`:254-255`). Clip driving precedent: `BlockEntityFlywheel.cs:150-161` (`StartAnimation`/`StopAnimation("cycle")`). No forming BE starts `cycle` today (grep) - the shear's `Animatable` has no driver (UNVERIFIED in game).

## 5. Goldens and tests

`test/ExpandedLib.Testing/DefinitionGoldens.cs`: `Collect(domain, asm)` :30, `Cases` :76, `CheckGolden(domain, asm, relativePath, goldenRoot)` :85, `CheckCompleteness` :112, `WriteAll` :150, `WriteRequested` :172. `EXLIB_WRITE_GOLDENS=1` rewrites all; a comma list filters by `domain/path` fragment (`:144-147,178-195`). Golden path = `{domain}/{Location.Path}` (`:72-73`) -> `goldens/iiex/blocktypes/machining/machinetool.json` for `Create(domain, code, "machining/machinetool")`. Suite: `IiexDefinitionGoldenTests.cs` `Def_reproduces_its_golden` :32, `Goldens_exactly_cover_the_defs` :43, `Every_shape_reference_resolves_to_a_shipped_file` :64 (`DefinitionAssets.cs:66-72` reads `shape` and `shapebytype`), `Variant_grouped_blocks_never_carry_a_bare_name_key` :86, `Regenerate_goldens_when_requested` :116. Parity is semantic; filler `behaviors` are identity (`IiexDefinitionBehaviorTests.cs:100`).

`WorkbenchTests.cs:60-74`: *"The_mesh_and_the_footprint_turn_together(side): `int shapeAngle = (int)Def().ToJson()["shape"]!["rotateYByType"]![$"*-{side}"]!; int footprintAngle = Normalise(BlockFor(side).StructureAngle); ... Assert.Equal(shapeAngle, footprintAngle);`"* - the machine tools' analogue reads `shapebytype["*-{type}-{o}"]["rotateY"]`. Shear footprint pattern: `ShearStationTests.cs:110-138` (`StructureFillers.ReadOffsets(new JsonObject(offsets))`, slab `Y1 0 / Y2 0.5`) and `:141-160` (`StructureAngle` 90, nest moves). `FurnaceFillerAccountingTests.cs:16-24,141-168,197-238` reconciles a *multiblock* layout's `exlib:structurefiller` cells with parts' `fillerOffsets` - not applicable to a footprint-only megablock; `StructureRig.Around` throws without `multiblockStructure` (`StructureRig.cs:95-98`), so stand footprints up as `ShearStationTests` does, not with the rig.

## 6. Lang keys and guards

- Names: `LangCoverage.MissingNames` requires every concrete variant code to resolve via exact or trailing-`*` key (`LangCoverage.cs:20-27,35-72`); bare `block-{code}` is refused for variant blocks. Precedent `"block-forming-shear-*": "Crop Shear"` (`assets/iiex/lang/en.json:114`) -> one `block-machinetool-{type}-*` per type.
- Call sites: `LangCallSites` scans literal `Lang.Get*`, `ActionLangCode = "..."`, one-arg `SendIngameError("iiex-x-y")` (-> `game:ingameerror-iiex-x-y`) and demands them in every locale (`LangCallSites.cs:27-49,128-171,195-232`; test `IiexLangCoverageTests.cs:41`). Precedent keys `en.json:117-128,134-143`. Generated `IiexLang` consts come from `en.json` (`ExLangKeyGenerator.cs:12-20`, `src/Directory.Build.props:266`; used `BlockFastenerBench.cs:377`) - note consts are invisible to the literal scan, so other locales are not checked for them.
- `ReferencedCodes` checks recipe outputs/ingredients and `{type:item|block, code}` stacks in defs, RCC `requireStacks` included (`ReferencedCodes.cs:73-142,185-200,323-329`; `IiexDefinitionBehaviorTests.cs:80`). `config/processjobs/*.json` (`ProcessJobRegistry.Jobs(string?)` `:49`, `Job` `:61`; `BlockEntityShear.MachineKey` `:76`) is not scanned by it - UNVERIFIED whether another guard covers job codes.

## Recommended def shape for the family

! `forming-shear`, `forming-nailcutter`, `forming-riveter` already ship (class strings live in saves); folding them into `machinetool-*` is a migration decision, not a def edit.

```csharp
[BlockRegister]
public partial class BlockMachineTool : BlockNetworkNode, IExBlockDefProvider, IFillerHost, IFillerInteractionTarget {
  public override string NetworkType => "mpenergy";
  private static readonly FillerBehaviorSpec MpN = FillerBehaviorSpec.Of<BEBehaviorMPFillerPort>("north"),
    MpS = ...("south"), MpE = ...("east"), MpW = ...("west");   // or a hosted mpenergy membership - decide (section 2b)
  private sealed record Machine(string Type, string Shape, IReadOnlyList<FillerCellSpec> Footprint, Vec3i[] Windows, Vec3i[] Holds);
  private static readonly Machine[] Machines = [
    new("drillpress", "iiex:machining/drillpress", DrillPress, [new(0,1,1), new(0,0,1)], []),
    new("horizontalbore", "iiex:machining/horizontalbore", Bore, [new(0,1,0)], [new(0,0,1)]),
    new("lathe", "iiex:machining/lathe", Lathe, [new(-1,1,0), new(0,1,0)], [new(0,1,-1)]),
    new("planer", "iiex:machining/planer", Planer, [new(-2,0,0), new(-1,0,0), new(0,0,0)], [new(0,1,-1), new(0,1,0), new(0,1,1)]),
    new("shaper", "iiex:machining/shaper", Shaper, [new(0,1,0)], [new(0,1,0)]),
    // cutter / nailcutter / riveter: only if forming-* is retired
  ];
  public static IEnumerable<ExBlockDef> Definitions(string domain) {
    ExBlockDef def = ExBlockDef.Create(domain, "machinetool", "machining/machinetool")
      .Class<BlockMachineTool>().EntityClass<BlockEntityMachineTool>()
      .Material(EnumBlockMaterial.Metal).Sound("walk", "game:walk/metal").Sound("place", "game:block/anvil")
      .MaxStackSize(1)
      .VariantGroup("type", [.. Machines.Select(m => m.Type)])
      .VariantGroup("orientation", "ns", "we").NetworkOriented()
      .CreativeCommon("*-ns").EntityBehavior("Animatable").SolidNonOpaque();
    foreach (Machine m in Machines)
      def.ShapeByType($"*-{m.Type}-ns", m.Shape, rotateY: 0)
         .ShapeByType($"*-{m.Type}-we", m.Shape, rotateY: 90)
         .FillerOffsetsByType($"*-{m.Type}-*", m.Footprint);
    return [def];
  }
  public JsonObject? FillerOffsets => Attributes?["fillerOffsets"];
  public int StructureAngle => Variant?["orientation"] == "we" ? 90 : 0;
  public string MachineKey => Variant?["type"] ?? Machines[0].Type;
}
```

Footprints (mirror Slice rows if the owner's compass is kept - section 2a):

```csharp
static readonly IReadOnlyList<FillerCellSpec> DrillPress = StructureFootprint.Layout(f =>
  f.Slab('I', BlockFacing.NORTH).Host('M', MpN, MpS).Origin(-1, 2)
   .Slice(0, """
              # # #
              I # #
              I O M
              """));
static readonly IReadOnlyList<FillerCellSpec> Bore = StructureFootprint.Layout(f =>
  f.Host('m', MpE, MpW).Slab('i', BlockFacing.NORTH).Solid('I').Origin(-1, 0)
   .Layer(0, """
              m O #
              . i .
              """)
   .Layer(1, """
              # I #
              . . .
              """));
static readonly IReadOnlyList<FillerCellSpec> Lathe = StructureFootprint.Layout(f =>
  f.Slab('_', BlockFacing.SOUTH).Slab('i', BlockFacing.SOUTH).Host('m', MpE, MpW).Solid('I').Origin(-1, -1)
   .Layer(0, """
              _ _ _
              # O m
              """)
   .Layer(1, """
              . i .
              I I #
              """));
static readonly IReadOnlyList<FillerCellSpec> Planer = StructureFootprint.Layout(f =>
  f.Solid('I').Slab('i', BlockFacing.EAST).Slab('_', BlockFacing.SOUTH).Slab('-', BlockFacing.NORTH)
   .Host('M', MpN, MpS).Origin(-2, -1)
   .Layer(0, """
              . # # M .
              I I O # #
              . . . - .
              """)
   .Layer(1, """
              . . i _ .
              . . i # .
              . . i - .
              """)
   .Layer(2, """
              . . . _ .
              . . . # .
              . . . - .
              """));
static readonly IReadOnlyList<FillerCellSpec> Shaper = StructureFootprint.Layout(f =>
  f.Solid('I').Host('m', MpE, MpW).Origin(0, 1)
   .Slice(0, """
              I # #
              O m .
              """));
```

Then: export each editable with `convert-shape.py machines/mpenergy/machine-mp-megablock-<x> assets/iiex/shapes/machining/<type>.json` (after fixing the section 3 extents/`drill` clip), add `block-machinetool-{type}-*`, `iiex:machinetool-help-*` and `game:ingameerror-iiex-machinetool-*` to every locale, bless goldens with `EXLIB_WRITE_GOLDENS=iiex/blocktypes/machining/machinetool`, and pin per-type footprint + `StructureAngle`/`rotateY` tests in the `ShearStationTests`/`WorkbenchTests` pattern.
