# Cornish boiler — the self-contained megablock

> **For agentic workers:** REQUIRED SUB-SKILL: use `superpowers:subagent-driven-development`
> (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Status** BUILT 2026-08-23. CB0-CB9 are all done and the gate is 9 targets / 4,700; what remains is
CB10, which is a follow-through list rather than a unit of work. The record of what each unit found is
[the worklog](../worklog/2026-08.md); the execution ledger is
`.superpowers/sdd/2026-08-23-cornish-boiler-megablock/progress.md`.

**Goal:** Replace the Cornish boiler's player-built masonry firebox with a self-contained 3 × 6 × 3
megablock that carries its own brickwork, burner and two hatches in its shape, burns fuel through
`BEBehaviorFirebox` rather than a vanilla coal pile, and derives its steam output from the fuel's own
`combustibleProps`.

**Architecture:** The boiler stops being a multiblock and becomes a pure megablock. Every role the
player used to build — the coal bed, the exhaust outlet, the feedwater passthrough, the firebrick shell —
becomes either a footprint filler cell or part of the boiler's own mesh. Three network ports move onto
the structure: feedwater onto the principal's own SOUTH face, steam onto an up-port filler, exhaust onto
an east-port filler. Fuel state moves inside the block entity as a hosted `BEBehaviorFirebox`, and the
steam rate becomes a function of the burning fuel's vanilla burn temperature, saturating at a design
point so that duration — not flame temperature — is what distinguishes one coal from another.

**Tech stack:** C# (net7.0/net8.0, multi-targeted 1.20/1.21/1.22), Vintage Story API, xUnit, code-first
`ExBlockDef` definitions, Python 3 for the shape tooling.

**Spec:** `workbench/machines.txt` § *Pipe steam cornish boiler* (the owner's layout and
legend, revised 2026-08-23). Decisions cited from `docs/design/machines/boiler-cornish.md`,
`docs/design/machines/firebox.md`, `docs/design/items/fuels.md`,
`docs/design/mechanics/multiblock.md`, `docs/design/mechanics/gas-system.md`.

---

## Global constraints

Every task's requirements implicitly include this section. Each line was verified against `src/` on
2026-08-23; navigate by symbol, not by line number.

- **Nothing has shipped.** `mods/exlib/testing/ReleasedCodes.cs` states "no iiex, siex or hpex
  build has been released". `iiex:boilercornish` needs no save migration and the footprint may change
  freely. `PpexRenameMigration` maps released `ppex:boilercornish` → `iiex:boilercornish`; per the
  owner's ruling of 2026-08-23, **iiex is a separate mod with reimplementations and owes ppex worlds
  nothing** — do not build a footprint migrator.
- **The JSON schema is the API.** Anything a third-party mod must be able to supply — a coal, a bed
  element path, a layer count — is declared data, never a hardcoded string or a `const`.
- **Comments describe, never narrate.** No "changed from", no "new", no dated prose in code comments.
  Neutral repo voice throughout; the worklog records history, not the source.
- **Never commit.** Leave every change in the working tree. The repo owner owns git history.
- **Test command:** `./scripts/exmod.sh test 1.21`. Use 1.21, not 1.22 — VS 1.22.6 broke `IPlayer`
  mocking. A full green run is 9 targets.
- **Goldens** rewrite with `EXLIB_WRITE_GOLDENS=1`; the **handbook** with `EXLIB_WRITE_HANDBOOK=1`,
  which writes `mods/iiex/assets/iiex/lang/en.json` only — `ru.json` and `uk.json` are hand work.
- **Design pages own decisions.** If a number here disagrees with `docs/design/**`, the design page
  wins and this plan is the thing to fix.

### The agreed numbers

Rate model, ruled 2026-08-23. A boiler is heating-surface limited, not flame limited: every coal here is
7–16× hotter than the water, so flame temperature is close to a pass/fail gate and **burn duration is
what distinguishes fuels**.

```
mult   = min(1, (burnTemperature - Tsat) / (BoilerFuelDesignTemp - Tsat))
rate   = CornishBoilerSteamPerSecond * mult
heatUp = BoilerHeatUpSeconds / mult
burn   = fireboxUnits * burnDuration        (seconds per unit = vanilla burnDuration)
```

`Tsat` is the existing `BlockEntityBoiler.SteamTemperature()`. `BoilerFuelDesignTemp` = **1200 °C**.

| Fuel | vanilla burn °C | duration | mult | L/s | Watt engines | bed lasts |
|---|---|---|---|---|---|---|
| `game:ore-anthracite` | 1200 | 196 | 1.00 | 64.0 | 2.13 | 52.3 min |
| `game:ore-bituminouscoal` | 1200 | 84 | 1.00 | 64.0 | 2.13 | 22.4 min |
| `game:ore-lignite` | 1100 | 77 | 0.91 | 58.2 | 1.94 | 20.5 min |
| `game:coke` | 1340 | 40 | 1.00 | 64.0 | 2.13 | 10.7 min |
| `game:charcoal` | 1300 | 40 | 1.00 | 64.0 | 2.13 | 10.7 min |

Verified in `.game/1.22/assets/survival/itemtypes/resource/{coke,charcoal,ore-ungraded}.json`.

Config changes (`IiexConfig.cs`):

| Key | Now | Becomes |
|---|---|---|
| `CornishBoilerSteamPerSecond` | 32 | **64** |
| `CornishBoilerCapacity` | 800 | **1600** |
| `CornishBoilerMaxBoilWater` | 500 | **1000** |
| `CornishBoilerMinBoilWater` | 150 | **300** |
| `BoilerWaterIntakeRate` | 10 | **20** |
| `BoilerFuelDesignTemp` | — | **1200** (new) |
| `BoilerFuelMinTemp` | — | **1000** (new — the floor below which an item is not boiler fuel) |
| `CornishBoilerMaxOutputPressure` | 5.0 | 5.0 (unchanged) |
| `FireboxLayersPerCell` / `FireboxUnitsPerLayer` | 6 / 2 | 6 / 2 (unchanged — these stay the *defaults*) |

### The footprint

`W` is the principal at `(0,0,0)`. Grids are `Origin(-1, -5)`, rows Z −5 (north) .. 0 (south),
columns X −1 .. +1. **40 fillers + 1 principal.**

```
L1 (y=0)          L2 (y=1)          L3 (y=2)
# # E             # # #             . . .
# # #             # # #             . _ .
# # #             # # #             . S .
# # #             # # #             . _ .
# # #             # # #             . M .
# W #             # I #             . _ .
```

| Glyph | Cell | Meaning |
|---|---|---|
| `W` | `(0,0,0)` | **principal**; feedwater in on its own SOUTH face |
| `I` | `(0,1,0)` | main hatch — open, charge, ignite, close |
| `S` | `(0,2,-3)` | steam out, pipe port UP |
| `M` | `(0,2,-1)` | man hatch — bucket fill, emergency vent; bottom slab |
| `E` | `(1,0,-5)` | exhaust out, pipe port EAST |
| `_` | `(0,2,-4)`, `(0,2,-2)`, `(0,2,0)` | bottom slab, no interaction |
| `#` | the rest | plain filler |

### The geometry offsets

Re-derived for the new footprint. **Deliberately distinct** — Gotcha 12 in
`docs/design/machines/boiler-cornish.md` records that the old geometry pinned the steam port, the blast
centre and the light sample to one cell by coincidence, so moving one silently moved the others.

| Attribute | Value | Why |
|---|---|---|
| `fuelOffset` | `(0,1,0)` | the `I` cell — where fire sound and particles play |
| `mainHatchOffset` | `(0,1,0)` | the `I` cell |
| `manHatchOffset` | `(0,2,-1)` | the `M` cell |
| `steamConnectorOffset` | `(0,2,-3)` | the `S` cell; the pipe attaches at `(0,3,-3)` |
| `exhaustOutletOffset` | `(1,0,-5)` | the `E` cell; the pipe attaches at `(2,0,-5)` |
| `explosionCenterOffset` | `(0,1,-2)` | geometric centre of the barrel |
| `lightSampleOffset` | `(0,1,-3)` | mid-body, away from the fire |
| `waterRendererBox` | see CB6.4 | derived, then confirmed visually |

### The new shape's element paths

After conversion, `Root` is unwrapped and the top level is:

```
MasonryBase   BoilerCasing   Flues   BoilerEnds   MasonryTop   CasingSegment5   CoalLayers
```

Old paths that no longer exist anywhere: `Root/Base`, `Root/BaseExtension`, `Root/Casing`,
`Root/Flues`, and the `lidopen` animation. Animations are now `idle` (Repeat), `mainhatchopen` (Hold),
`manhatchopen` (Hold).

### Commands

```bash
./scripts/exmod.sh test 1.21                       # full suite, 9 targets
./scripts/exmod.sh test 1.21 -Filter Boiler        # one area
./scripts/exmod.sh format -Check                   # formatting gate
python infra/tools/convert-shape.py --check      # unmapped textures across all editables
EXLIB_WRITE_GOLDENS=1 ./scripts/exmod.sh test 1.21 -Filter DefinitionGoldens
EXLIB_WRITE_HANDBOOK=1 ./scripts/exmod.sh test 1.21 -Filter HandbookSync
```

---

## CB0 — the shape tooling ✅ DONE 2026-08-23

Landed before the plan was written, because CB1 cannot convert the shape without it. Recorded here so
the sequence reads whole.

- `infra/tools/convert-shape.py`
  - `TEXTURES["iron4"]` → `game:block/metal/sheet/iron4`. The map had it as `sheet-plain`, and because
    the remap overwrites by key it beat the editable: `iiex/shapes/furnace/tuyere.json` and
    `iiex/shapes/ore/burdenmaker.json` both ship `sheet-plain` while their editables author `sheet`.
  - Added `bituminous` and `anthracite` so a fuel bed can be authored against the coal it holds. The
    conversion refusal is gone.
  - `unwrap_root` now folds the wrapper's own `from` into every lifted child (`from`, `to` and
    `rotationOrigin`, which share the parent-relative space), via a new `_translate` helper. The
    reworked boiler wraps at `[16,0,16]`; lifting bare moved the machine one cell west and one north,
    silently.
  - Held poses became a suffix rule: `HOLD_SUFFIXES = ("open",)` and a `holds()` predicate, so `open`,
    `lidopen`, `mainhatchopen` and `manhatchopen` all end on Hold. The exact-match set had already let
    this scar happen twice.
- Swept 11 editables off `sheet-plain/iron4` and normalised 12 to the bare game-relative form.

**Verified:** the converter runs clean on the new editable, and the converted file's per-cell occupancy
is identical to the editable's — X −1..1, Y 0..2, Z −5..0, same per-group extents.

**Left open, deliberately:**

- Four editables use `iron4` as a *second slot* pointing at `sheet-plain/steel4` or `sheet-plain/iron5`
  — `machines/pipe/machine-pipe-megablock-manualpump.json`,
  `machines/pipe/machine-pipe-megablock-mppump.json`,
  `networks/pipe/pipe-block-cast-pressurevalve.json`, `networks/pipe/pipe-block-cast-valve.json`.
  They were already being clobbered to `sheet-plain/iron4`; they now get `sheet/iron4`. Both are wrong
  for them. They want the `iron42` treatment — a distinct key, or a hand fix after conversion.
- 17 shipped shapes would flip `iron4` on their next conversion. Do **not** bulk re-convert:
  `unwrap_root` now also strips `Root`, which breaks any code naming `Root/...` paths. Per shape, with
  the code check.

---

## CB1 — convert the shape ✅ DONE 2026-08-23

**Files:**
- Create: `mods/iiex/assets/iiex/shapes/boiler/cornish.json` (overwrite)
- Read: `workbench/shapes/machines/steam/machine-pipe-megablock-boiler-cornish-new.json`

**Interfaces:**
- Produces: the shipped shape whose top-level element names every later task references —
  `MasonryBase`, `BoilerCasing`, `Flues`, `BoilerEnds`, `MasonryTop`, `CasingSegment5`, `CoalLayers`.

- [ ] **Step 1: Confirm the editable is clean**

```bash
python infra/tools/convert-shape.py --check
```

Expected: no unmapped key attributed to `machines/steam/machine-pipe-megablock-boiler-cornish-new`.

- [ ] **Step 2: Confirm no non-ASCII element names survive**

```bash
python - <<'EOF'
import json
p="workbench/shapes/machines/steam/machine-pipe-megablock-boiler-cornish-new.json"
d=json.load(open(p,encoding='utf-8-sig'))
bad=[]
def walk(e,path=""):
    n=e.get("name","");pp=path+"/"+n if path else n
    if any(ord(c)>127 for c in n): bad.append(pp)
    for c in e.get("children",[]) or []: walk(c,pp)
for e in d["elements"]: walk(e)
print("non-ASCII:", bad or "NONE")
EOF
```

Expected: `non-ASCII: NONE`. The Cyrillic `С` in `BoilerСasing` was fixed by the owner on 2026-08-23; if
this reports anything, stop and fix the shape before converting — `ExShapeElements.Pruned` drops an
unmatched path without raising, so the barrel would render as nothing with every test green.

- [ ] **Step 3: Convert**

```bash
python infra/tools/convert-shape.py \
  machines/steam/machine-pipe-megablock-boiler-cornish-new \
  mods/iiex/assets/iiex/shapes/boiler/cornish.json
```

Expected output names all seven top-level elements and `anims=['idle', 'mainhatchopen', 'manhatchopen']`.

- [ ] **Step 4: Verify the conversion preserved geometry and intent**

```bash
python - <<'EOF'
import json
d=json.load(open("mods/iiex/assets/iiex/shapes/boiler/cornish.json",encoding='utf-8'))
assert "editor" not in d and "textureSizes" not in d
assert [e["name"] for e in d["elements"]] == ["MasonryBase","BoilerCasing","Flues",
        "BoilerEnds","MasonryTop","CasingSegment5","CoalLayers"], [e["name"] for e in d["elements"]]
assert d["textures"]["iron4"] == "game:block/metal/sheet/iron4"
assert d["textures"]["bituminous"] == "game:block/coal/bituminous"
ends = {a["name"]: a["onAnimationEnd"] for a in d["animations"]}
assert ends == {"idle":"Repeat","mainhatchopen":"Hold","manhatchopen":"Hold"}, ends
mb = next(e for e in d["elements"] if e["name"]=="MasonryBase")
assert mb["from"] == [16.0,0.0,16.0], mb["from"]   # Root's translation folded in
print("shape OK")
EOF
```

Expected: `shape OK`.

- [ ] **Step 5: Verify the shipped-asset guards still pass**

```bash
./scripts/exmod.sh test 1.21 -Filter ShippedAsset
```

Expected: PASS. (Definition goldens and boiler tests will fail from CB6 onward until CB8 re-blesses
them; that is expected and handled there.)

---

## CB2 — declarative filler ports (exlib) ✅ DONE 2026-08-23

The spec puts pipe connections on two filler cells. Today the only way to do that is
`BlockBoiler.MarkSteamPort`, which reaches into a filler BE after placement and sets `PortFace` /
`PortNetworkType` by hand — the sole `PortFace` writer in the codebase. `StructureFillers.ReadOffsets`
reads only `x/y/z/allowAttach/collisionBox(es)/behaviors`, so there is nothing to declare.

> **Ruling.** Use a **passive port**, not a hosted `BEBehaviorNetworkMember`. A hosted membership makes
> the cell a real graph node that joins the run and adds its volume to it
> (`docs/design/mechanics/multiblock.md` § *A filler cell is a graph node when it declares one*), which
> changes the pipe pool. `docs/design/machines/boiler-cornish.md` records that the boiler's port is
> deliberately a connector and not a node. Keep it that way and make it declarable.

**Files:**
- Modify: `mods/exlib/src/Blocks/Structures/StructureFootprint.cs` (`FillerCellSpec`)
- Modify: `mods/exlib/src/Blocks/Structures/FillerLayoutBuilder.cs`
- Modify: `mods/exlib/src/Blocks/Structures/StructureFillers.cs` (`ReadOffsets`, `FootprintCells`, `PlaceFillers`)
- Modify: `mods/exlib/src/Definitions/ExBlockDef.cs` (`FillerOffsets` serialization)
- Test: `mods/exlib/tests/Structures/FillerPortTests.cs` (create)

**Interfaces:**
- Produces:
  - `FillerCellSpec(int X, int Y, int Z, bool AllowAttach, IReadOnlyList<FillerBehaviorSpec>? Behaviors,
    IReadOnlyList<Cuboidf>? CollisionBoxes, string? PortFace, string? PortNetworkType)` — two new
    trailing optional members.
  - `FillerLayoutBuilder.Port(char symbol, BlockFacing face, string networkType)` — registers a glyph as
    a plain (non-attach) filler carrying a north-orientation port face, rotated with the footprint.
  - Serialized as `"portFace"` / `"portNetwork"` on the `fillerOffsets` cell object.

- [ ] **Step 1: Write the failing test**

Create `mods/exlib/tests/Structures/FillerPortTests.cs`:

```csharp
using ExpandedLib.Blocks.Structures;
using Vintagestory.API.MathTools;
using Xunit;

namespace ExpandedLib.Tests.Structures;

public class FillerPortTests {
  [Fact]
  public void PortGlyphCarriesFaceAndNetwork() {
    var cells = StructureFootprint.Layout(f =>
      f.Origin(-1, 0)
        .Port('S', BlockFacing.UP, "pipe")
        .Layer(0, """
                  # O #
                  # S #
                  """)
    );

    FillerCellSpec port = Assert.Single(cells, c => c.PortFace != null);
    Assert.Equal(0, port.X);
    Assert.Equal(1, port.Z);
    Assert.Equal("u", port.PortFace);
    Assert.Equal("pipe", port.PortNetworkType);
    Assert.False(port.AllowAttach);
  }

  [Fact]
  public void PlainGlyphCarriesNoPort() {
    var cells = StructureFootprint.Layout(f =>
      f.Origin(-1, 0).Layer(0, """
                              # O #
                              """)
    );
    Assert.All(cells, c => Assert.Null(c.PortFace));
  }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
./scripts/exmod.sh test 1.21 -Filter FillerPortTests
```

Expected: FAIL — `FillerCellSpec` has no `PortFace`, `FillerLayoutBuilder` has no `Port`.

- [ ] **Step 3: Widen `FillerCellSpec`**

In `StructureFootprint.cs`, append two members to the record and document them in the existing style:

```csharp
public readonly record struct FillerCellSpec(
  int X,
  int Y,
  int Z,
  bool AllowAttach = false,
  IReadOnlyList<FillerBehaviorSpec>? Behaviors = null,
  IReadOnlyList<Cuboidf>? CollisionBoxes = null,
  string? PortFace = null,
  string? PortNetworkType = null
);
```

Extend the type's summary to say that a cell may carry a passive network port: the face code a network
reads back through `BlockStructureFiller.HasConnectorAt`, which answers for the principal without
making the cell a graph node.

- [ ] **Step 4: Add `Port` to the builder**

In `FillerLayoutBuilder.cs`, add a `_ports` dictionary beside `_hosted` and `_boxes`, and:

```csharp
  /// <summary>
  /// Registers a character as a filler carrying a passive network port on <paramref name="face"/>: the
  /// cell answers <see cref="BlockStructureFiller.HasConnectorAt"/> for the principal without joining
  /// the graph, which is what lets a pipe couple two cells away from the block that owns the machine.
  /// The face is authored in the north orientation and rotated with the rest of the footprint.
  /// <para>
  /// Attachment stays off, as it is for a plain filler: a port is the machine's own coupling, not a
  /// shelf.
  /// </para>
  /// </summary>
  public FillerLayoutBuilder Port(
    char symbol,
    BlockFacing face,
    string networkType
  ) {
    _symbols[symbol] = false;
    _ports[symbol] = (face.Code[0].ToString(), networkType);
    return this;
  }
```

and in `Build()`, pass them through:

```csharp
      cells.Add(
        new FillerCellSpec(
          cell.X,
          cell.Y,
          cell.Z,
          attach,
          _hosted.TryGetValue(cell.Symbol, out var hosted) ? hosted : null,
          _boxes.TryGetValue(cell.Symbol, out var boxes) ? boxes : null,
          _ports.TryGetValue(cell.Symbol, out var port) ? port.Face : null,
          _ports.TryGetValue(cell.Symbol, out var net) ? net.Network : null
        )
      );
```

- [ ] **Step 5: Run the test and watch it pass**

```bash
./scripts/exmod.sh test 1.21 -Filter FillerPortTests
```

Expected: PASS.

- [ ] **Step 6: Serialize and read back the port**

In `ExBlockDef.FillerOffsets`, emit `portFace` / `portNetwork` when the spec carries them. In
`StructureFillers.ReadOffsets`, read them back onto the parsed cell. In `StructureFillers.FootprintCells`,
rotate the face by the structure angle exactly as `FillerBehavior.ConnectorFace` is rotated. In
`PlaceFillers`, set `be.PortFace` / `be.PortNetworkType` from the cell.

- [ ] **Step 7: Write the round-trip test**

Append to `FillerPortTests.cs`:

```csharp
  [Fact]
  public void PortSurvivesDefinitionRoundTrip() {
    var def = ExBlockDef
      .Create("exlib", "porttest", "porttest")
      .FillerOffsets(StructureFootprint.Layout(f =>
        f.Origin(-1, 0)
          .Port('S', BlockFacing.UP, "pipe")
          .Layer(0, """
                    # O #
                    # S #
                    """)
      ));

    var cells = StructureFillers.ReadOffsets(def.Build()["fillerOffsets"]);
    var port = Assert.Single(cells, c => c.PortFace == "u");
    Assert.Equal("pipe", port.PortNetworkType);
  }
```

Adjust `def.Build()` to whatever the definition's actual materialisation call is — read
`mods/exlib/tests/` for the established pattern before writing this, and match it.

- [ ] **Step 8: Run the suite**

```bash
./scripts/exmod.sh test 1.21 -Filter Filler
```

Expected: PASS, including the pre-existing `StructureFillerBoxesTests`.

---

## CB3 — a port read at an arbitrary cell (exlib) ✅ DONE 2026-08-23

`MachinePorts.ConnectedNetwork` is anchored to `be.Pos`. With the ports on filler cells, the boiler must
read across a face from a cell that is not its own.

**Files:**
- Modify: `mods/exlib/src/Blocks/Machines/MachinePorts.cs`
- Test: `mods/exlib/tests/Machines/MachinePortsTests.cs` (create or extend)

**Interfaces:**
- Consumes: nothing from CB2.
- Produces: `TNet? ConnectedNetworkAt<TNet>(this BlockEntity be, BlockPos at, BlockFacing connectorFace)`.

- [ ] **Step 1: Write the failing test**

Model it on the existing pipe fixtures. Read `mods/exlib/testing/TestWorld.cs` and
`mods/exlib/tests/Networks/FillerNodeTests.cs` first — `FillerNodeTests` already stands a
footprint cell up against a live network and is the closest working rig.

The assertion: with a pipe placed east of cell `C`, `ConnectedNetworkAt<PipeNetwork>(C, EAST)` returns
that pipe's network, and `ConnectedNetworkAt<PipeNetwork>(C, WEST)` returns null.

- [ ] **Step 2: Run it and watch it fail**

```bash
./scripts/exmod.sh test 1.21 -Filter MachinePorts
```

Expected: FAIL — no such method.

- [ ] **Step 3: Add the overload**

```csharp
  /// <summary>
  /// The network of type <typeparamref name="TNet"/> across <paramref name="connectorFace"/> from
  /// <paramref name="at"/>, which need not be this machine's own cell: a mega-block couples on a
  /// footprint port two cells from the block that owns it, and the reciprocal-connector test has to run
  /// from the port rather than from the principal.
  /// </summary>
  public static TNet? ConnectedNetworkAt<TNet>(
    this BlockEntity be,
    BlockPos at,
    BlockFacing connectorFace
  )
    where TNet : BlockNetwork =>
    be.NetworkSystem()
      ?.GetConnectedNetworkAcross(
        be.Api.World.BlockAccessor,
        at,
        connectorFace
      ) as TNet;
```

Then re-express the existing `ConnectedNetwork` as `ConnectedNetworkAt(be.Pos, connectorFace)` so there
is one implementation of the rule, which is what the type's summary already promises.

- [ ] **Step 4: Run the test and watch it pass**

```bash
./scripts/exmod.sh test 1.21 -Filter MachinePorts
```

Expected: PASS.

- [ ] **Step 5: Run every consumer of the old method**

```bash
./scripts/exmod.sh test 1.21 -Filter "Pipe|Engine|Boiler|Converter|Cowper"
```

Expected: PASS — the re-expression must be behaviour-neutral.

---

## CB4 — the firebox becomes configurable (iiex) ✅ DONE 2026-08-23

Three hardcoded things block a boiler bed: the layer count is a process-wide static, the bed's element
path is a `const` on `BlockFirebox`, and the fuel list is a substring array with `lignite` excluded by
name inside the shared static `IsFuel`.

> **Ruling, 2026-08-23.** Canonical element names are not worth enforcing — other modders will not
> follow them. The bed element path and layer prefix become per-block data. And **lignite is admissible
> in a boiler**: the exclusion's stated reason is that it "will not carry a metallurgical heat", which
> is furnace reasoning. A boiler needs to beat a ~157 °C saturation temperature; lignite at 1100 °C
> clears it sevenfold, and brown coal is a mainstream steam-raising fuel. The metallurgical exclusion
> moves to where the architecture already puts machine-level refusals —
> `BlockEntityFireboxFurnace.AcceptsFireboxFuel`, which `BlockEntityCokeOven` already overrides.

**Files:**
- Modify: `mods/iiex/src/BlockStructures/Furnaces/BEBehaviorFirebox.cs`
- Modify: `mods/iiex/src/BlockStructures/Furnaces/Blocks/BlockFirebox.cs`
- Modify: `mods/iiex/src/BlockStructures/Furnaces/BlockEntities/BlockEntityFireboxFurnace.cs`
- Modify: `mods/iiex/src/IiexConfig.cs`
- Test: `mods/iiex/tests/Blocks/Furnaces/FireboxChargeTests.cs`
- Test: `mods/iiex/tests/Blocks/Furnaces/FireboxConfigTests.cs` (create)

**Interfaces:**
- Produces:
  - `BEBehaviorFirebox.LayersPerCell` / `.UnitsPerLayer` / `.CellCapacity` become **instance** members.
    The statics keep their names as `DefaultLayersPerCell` / `DefaultUnitsPerLayer` /
    `DefaultCellCapacity` and keep reading `IiexValues`.
  - `BEBehaviorFirebox.BedElement` (string, default `"Coke"`) and `.LayerPrefix` (string, default
    `"CokeL"`) — instance, read from behaviour properties.
  - `BEBehaviorFirebox.ElementsFor(int layers)` — instance, returns `[BaseElement?] + prefix paths`.
  - `BEBehaviorFirebox.IsFuel(ItemStack?)` becomes "has `combustibleProps` at or above
    `IiexValues.BoilerFuelMinTemp`", replacing the substring list and the `lignite` const.
  - `BlockEntityFireboxFurnace.AcceptsFireboxFuel` gains the metallurgical refusal.

- [ ] **Step 1: Write the failing tests**

Create `mods/iiex/tests/Blocks/Furnaces/FireboxConfigTests.cs` with three facts:

1. `DefaultsMatchConfig` — a behaviour with no properties reports `LayersPerCell == 6`,
   `UnitsPerLayer == 2`, `CellCapacity == 12`, `BedElement == "Coke"`, `LayerPrefix == "CokeL"`.
2. `PropertiesOverrideDefaults` — a behaviour initialised with
   `{ "layers": 4, "unitsPerLayer": 4, "bedElement": "CoalLayers", "layerPrefix": "L" }` reports
   `LayersPerCell == 4`, `UnitsPerLayer == 4`, `CellCapacity == 16`, and
   `ElementsFor(2)` returns `["CoalLayers/L1", "CoalLayers/L2"]`.
3. `LigniteIsFuelAtTheBedButNotAtAFurnace` — `BEBehaviorFirebox.IsFuel(ligniteStack)` is **true**, while
   a `BlockEntityFireboxFurnace`'s `AcceptsFireboxFuel(ligniteStack)` is **false**.

Read `FireboxChargeTests.cs` for how a bare `BEBehaviorFirebox` is stood up in this suite and follow it
exactly; do not invent a new fixture.

- [ ] **Step 2: Run them and watch them fail**

```bash
./scripts/exmod.sh test 1.21 -Filter FireboxConfigTests
```

Expected: FAIL on all three.

- [ ] **Step 3: Add the two config keys**

In `IiexConfig.cs`, beside the existing firebox keys:

```csharp
  /// <summary>
  /// Flame temperature (°C) at which a boiler's grate saturates its heating surface. At or above it a
  /// fuel raises steam at the vessel's full rate; below it the rate falls off linearly toward the
  /// saturation temperature. Every coal vanilla ships except lignite clears it, so fuel choice is felt
  /// mainly as burn duration - see docs/design/machines/boiler-cornish.md.
  /// </summary>
  public float BoilerFuelDesignTemp { get; set; } = 1200f;

  /// <summary>
  /// Lowest burn temperature (°C) an item may declare and still be firebox fuel. The floor is what keeps
  /// firewood, peat and every other low-grade combustible out of a fuel bed now that admission is read
  /// from <c>combustibleProps</c> rather than from a list of coal names.
  /// </summary>
  public float BoilerFuelMinTemp { get; set; } = 1000f;
```

- [ ] **Step 4: Make the geometry instance members**

In `BEBehaviorFirebox.cs`, replace the `static` geometry region. Keep the statics under `Default*`
names — `FurnaceBranchGuards` and roughly thirty test call sites read them, and the furnaces must not
move.

```csharp
  /// <summary>Fuel units one drawn layer holds by default. Config-driven, at least 1.</summary>
  public static int DefaultUnitsPerLayer =>
    Math.Max(1, IiexValues.FireboxUnitsPerLayer);

  /// <summary>Drawn layers in one cell by default.</summary>
  public static int DefaultLayersPerCell =>
    Math.Max(1, IiexValues.FireboxLayersPerCell);

  /// <summary>Units one firebox cell holds when full, at the defaults.</summary>
  public static int DefaultCellCapacity =>
    DefaultLayersPerCell * DefaultUnitsPerLayer;

  private int _layersPerCell = DefaultLayersPerCell;
  private int _unitsPerLayer = DefaultUnitsPerLayer;

  /// <summary>Drawn layers in this bed, from the behaviour's <c>layers</c> property.</summary>
  public int LayersPerCell => _layersPerCell;

  /// <summary>Fuel units one drawn layer of this bed holds, from <c>unitsPerLayer</c>.</summary>
  public int UnitsPerLayer => _unitsPerLayer;

  /// <summary>Units this bed holds when full.</summary>
  public int CellCapacity => LayersPerCell * UnitsPerLayer;
```

Add `_bedElement` / `_layerPrefix` fields with the `"Coke"` / `"CokeL"` defaults and public getters, and
read all four in `Initialize(ICoreAPI api, JsonObject properties)`:

```csharp
  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
    _layersPerCell = Math.Max(1, properties["layers"].AsInt(DefaultLayersPerCell));
    _unitsPerLayer = Math.Max(1, properties["unitsPerLayer"].AsInt(DefaultUnitsPerLayer));
    _bedElement = properties["bedElement"].AsString(_bedElement);
    _layerPrefix = properties["layerPrefix"].AsString(_layerPrefix);
  }
```

`LayerCount`, `Free`, `IsFull` and `TryTakeLayer` already read the members by name and need no edit
once the statics become instance properties.

- [ ] **Step 5: Move `ElementsFor` onto the behaviour**

Add to `BEBehaviorFirebox`:

```csharp
  /// <summary>The element paths a bed of <paramref name="layers"/> courses draws, bottom-first.</summary>
  public List<string> ElementsFor(int layers) {
    var keep = new List<string>();
    for (int i = 1; i <= layers; i++)
      keep.Add(_bedElement + "/" + _layerPrefix + i);
    return keep;
  }
```

In `BlockFirebox.cs`, drop the `BedElement` const and re-express `BlockFirebox.ElementsFor` as
`[BaseElement, ..bed.ElementsFor(layers)]` so the standalone block keeps prepending its always-drawn
rim. `BlockEntityFirebox.RenderElements(int layers)` calls through to the bed's behaviour.

- [ ] **Step 6: Read fuel admission off `combustibleProps`**

Replace the `Fuels` array, the `Excluded` const and `IsFuel`:

```csharp
  /// <summary>
  /// Whether <paramref name="stack"/> burns hot enough to be firebox fuel, read from the item's own
  /// <c>combustibleProps</c> against <see cref="IiexValues.BoilerFuelMinTemp"/>. Read rather than
  /// listed so a fuel another mod ships is admitted by declaring what it already declares - vanilla
  /// gives every coal a burn temperature and a duration, and nothing in this suite read either before.
  /// What a given machine ACCEPTS is narrower and is the machine's own rule; see
  /// <see cref="BlockEntities.BlockEntityFireboxFurnace.AcceptsFireboxFuel"/>.
  /// </summary>
  public static bool IsFuel(ItemStack? stack) =>
    BurnTemperatureOf(stack) >= IiexValues.BoilerFuelMinTemp;

  /// <summary>Burn temperature (°C) of <paramref name="stack"/>, or 0 when it does not burn.</summary>
  public static float BurnTemperatureOf(ItemStack? stack) =>
    stack?.Collectible?.CombustibleProps?.BurnTemperature ?? 0f;

  /// <summary>Burn duration (seconds per unit) of <paramref name="stack"/>, or 0 when it does not burn.</summary>
  public static float BurnDurationOf(ItemStack? stack) =>
    stack?.Collectible?.CombustibleProps?.BurnDuration ?? 0f;
```

- [ ] **Step 7: Move the metallurgical refusal to the furnace branch**

In `BlockEntityFireboxFurnace.cs`, narrow the virtual:

```csharp
  /// <summary>
  /// Whether this furnace takes <paramref name="stack"/> as firebox fuel. Narrower than
  /// <see cref="BEBehaviorFirebox.IsFuel"/>: a reverberatory hearth has to reach a metallurgical heat,
  /// which a low-rank, high-moisture, high-ash coal will not carry however well it raises steam. Declared
  /// here rather than on the bed, whose test is shared with every host - a boiler burns what this
  /// refuses.
  /// </summary>
  public virtual bool AcceptsFireboxFuel(ItemStack? stack) =>
    BEBehaviorFirebox.IsFuel(stack)
    && !IsLowRank(stack?.Collectible?.Code?.Path);

  /// <summary>Coals that will not carry a metallurgical heat, by code fragment.</summary>
  private static bool IsLowRank(string? path) =>
    path != null && path.Contains("lignite", StringComparison.Ordinal);
```

- [ ] **Step 8: Run the firebox suite**

```bash
./scripts/exmod.sh test 1.21 -Filter "Firebox|CokeOven|Crucible|Puddling|Reheat"
```

Expected: PASS. `FireboxChargeTests` pins the literals 6 / 2 / 12 — repoint those three assertions at
`BEBehaviorFirebox.DefaultLayersPerCell` etc. so they keep asserting the shipped defaults rather than
the old static names. `FurnaceBranchGuards.NoFireboxAsksForMoreThanItsCellsCanHold` reads
`CellCapacity`; repoint it at `DefaultCellCapacity`.

- [ ] **Step 9: Run the whole suite**

```bash
./scripts/exmod.sh test 1.21
```

Expected: PASS on all 9 targets. Fuel admission changed shape, so watch `FuelRoleGrantTests` and the
cowper's substring taxonomy in particular.

---

## CB5 — the fuel texture comes off the atlas (iiex) ✅ DONE 2026-08-23

The boiler blocktype declares no textures at all — `BlockBoiler.BoilerShell` has no `.Texture(...)` call
and relies wholly on the shape's map. `BlockFirebox` declares all four fuel keys precisely so
`ExShapeElements.Retextured` has valid targets, and an undeclared key draws missing-texture pink rather
than raising. That approach cannot work for a coal another mod adds: code-first defs are built at mod
load and the fuel is not known until then.

**Files:**
- Modify: `mods/iiex/src/BlockStructures/Boiler/BlockEntityBoiler.Client.cs`
- Reference: `mods/iiex/src/BlockStructures/Storage/BlockEntities/BlockEntityStorageRack.cs`

**Interfaces:**
- Produces: `BlockEntityBoiler : ITexPositionSource` with `AtlasSize` and
  `TextureAtlasPosition this[string textureCode]`, resolving the burning fuel's own item texture.

- [ ] **Step 1: Read the working precedent**

Open `BlockEntityStorageRack.cs` § *ITexPositionSource* (`AtlasSize`, the indexer). It resolves an
arbitrary stored item's texture from the block atlas at tesselation time; that is exactly the shape of
what the boiler needs. Copy the pattern, not the specifics.

- [ ] **Step 2: Write the failing test**

Assert that for a bed holding `game:ore-lignite` — a coal the boiler's blocktype does not and cannot
declare — the resolved texture path is the lignite item's own, not a fallback. Follow whatever
client-side rig the storage-rack tests use; if there is none, assert on the resolver method directly
rather than standing up a client.

- [ ] **Step 3: Run it and watch it fail**

```bash
./scripts/exmod.sh test 1.21 -Filter Boiler
```

- [ ] **Step 4: Implement the resolver**

Implement `ITexPositionSource` on `BlockEntityBoiler`, keyed on the bed's `FuelCode`. Resolve the item,
take its first texture, and look it up in `capi.BlockTextureAtlas` (falling back to
`capi.ItemTextureAtlas` for an item-only texture). Return the block's own default position when the fuel
does not resolve — a bed must never draw pink.

- [ ] **Step 5: Run the test and watch it pass**

```bash
./scripts/exmod.sh test 1.21 -Filter Boiler
```

---

## CB6 — the boiler becomes a megablock (iiex) ✅ DONE 2026-08-23

The largest unit. Split into six tasks that each leave the tree compiling.

**Files:**
- Modify: `mods/iiex/src/BlockStructures/Boiler/Blocks/BlockBoilerCornish.cs`
- Modify: `mods/iiex/src/BlockStructures/Boiler/BlockBoiler.cs`
- Modify: `mods/iiex/src/BlockStructures/Boiler/BlockEntityBoiler.cs`
- Modify: `mods/iiex/src/BlockStructures/Boiler/BlockEntityBoiler.Client.cs`
- Modify: `mods/iiex/src/BlockStructures/Boiler/BlockEntities/BlockEntityBoilerCornish.cs`
- Modify: `mods/iiex/src/BlockStructures/Boiler/IBoilerGeometry.cs`
- Modify: `mods/iiex/src/IiexConfig.cs`

### CB6.1 — drop the multiblock

**Interfaces:**
- Produces: `BlockEntityBoiler : BlockEntityProductionMachine`; `BlockBoilerCornish` with no
  `MultiblockLayout` and no `"MultiblockStructure"` behavior.

- [ ] **Step 1: Confirm the gate does not come from the multiblock**

`BlockEntityBoiler.IsConstructed` is `_animator?.IsConstructed ?? false` — it reads the RCC through
`ConstructedAnimator`, not the structure. Dropping structure verification does not ungate the machine.
Confirm by reading `mods/exlib/src/Blocks/Construction/ConstructedAnimator.cs` before editing.

- [ ] **Step 2: Swap the base class**

`BlockEntityMultiblockMachine` and `BlockEntityProductionMachine` are documented as the same tick host
for the multiblock and non-multiblock cases. Change the declaration to
`public abstract partial class BlockEntityBoiler : BlockEntityProductionMachine`, and move the
`CanRunProduction` gate override across — it lives on `BlockEntityMultiblockStructure` today and on
`IProductionReadiness` in the new base.

- [ ] **Step 3: Delete the structure map**

Remove the whole `.MultiblockLayout(...)` call from `BlockBoilerCornish.Cornish`, and
`.Behavior("MultiblockStructure")` from `BlockBoiler.BoilerShell`. Do the same in
`mods/siex/src/BlockStructures/Boiler/Blocks/BlockBoilerLancashire.cs` — it shares the shell
and would otherwise verify a structure its base no longer supports.

- [ ] **Step 4: Build**

```bash
./scripts/exmod.sh test 1.21 -Filter Boiler
```

Expected: compiles. Boiler tests fail — the fixtures still build the old multiblock scene. CB8 fixes
them; do not patch them here.

### CB6.2 — the new footprint

**Interfaces:**
- Consumes: `FillerLayoutBuilder.Port` from CB2, `Slab` from exlib as shipped.
- Produces: the 40-cell footprint; `BlockBoilerCornish` names `IiexCodes` no more.

- [ ] **Step 1: Replace `FillerOffsets`**

```csharp
The `W` cell is drawn as `O` — the DSL's principal glyph — because that is what
`FillerLayoutBuilder` recognises and skips. Everything else keeps the spec's letters.

```csharp
      .FillerOffsets(
        StructureFootprint.Layout(f =>
          f.Origin(-1, -5)
            .Slab('_', BlockFacing.DOWN)
            .Slab('M', BlockFacing.DOWN)
            .Solid('I')
            .Port('S', BlockFacing.UP, "pipe")
            .Port('E', BlockFacing.EAST, "pipe")
            .Layer(
              0,
              """
              # # E
              # # #
              # # #
              # # #
              # # #
              # O #
              """
            )
            .Layer(
              1,
              """
              # # #
              # # #
              # # #
              # # #
              # # #
              # I #
              """
            )
            .Layer(
              2,
              """
              . . .
              . _ .
              . S .
              . _ .
              . M .
              . _ .
              """
            )
        )
      )
```

`I` is a **full** filler — the main hatch stands at head height on L2 — so it is registered with
`Solid`, not `Slab`. `M` is a bottom slab per the spec, and a slab cell's selection box is only the half
it fills: the man hatch is clickable on the lower 8/16 of its cell. That is a deliberate consequence of
choosing a slab for an interaction cell; record it in `docs/design/machines/boiler-cornish.md` at CB9
rather than silently living with it.

Interactions still forward from a slab. `BlockStructureFiller.OnBlockInteractStart` hands the click to
the principal **before** it consults `AllowAttach`, which only decides the unhandled fallthrough — so a
non-attach slab is a perfectly good interaction cell.

- [ ] **Step 2: Replace the geometry attributes**

Use the table in *The geometry offsets* above verbatim. Rename `lidOffset` to `mainHatchOffset` and add
`manHatchOffset`. Leave a comment on `explosionCenterOffset` and `lightSampleOffset` recording that they
are deliberately distinct cells, so the next geometry change does not silently re-collide them.

- [ ] **Step 3: Delete the port marking that the footprint now declares**

`BlockBoiler.MarkSteamPort` and the `OnFootprintPlaced` override go away — CB2 places both ports
declaratively. `HasConnectorAt` changes from `DOWN` to `SOUTH`: the principal *is* the `W` cell and takes
feedwater on its own south face.

- [ ] **Step 4: Repoint the three network reads**

Each port moved, and one of them changed *kind*.

**Feedwater** — `BlockEntityBoiler.OnProductionTick` reads
`this.ConnectedNetwork<PipeNetwork>(BlockFacing.DOWN)`. The principal is the `W` cell now and takes
water on its own south face, so this becomes `BlockFacing.SOUTH`, matching the `HasConnectorAt` change
in Step 3.

**Exhaust** — the old outlet was a real player-placed `iiex:pipe-outlet-fire-u` block that was itself a
graph node, so the boiler read `NetworkAt<PipeNetwork>(ExhaustOutletWorldPos(Pos))`. The `E` cell is a
**passive port**, not a node, so `NetworkAt` on it finds nothing. Read across its face instead, using
CB3's overload:

```csharp
    PipeNetwork? exhaustNet =
      BoilerBlock != null
        ? this.ConnectedNetworkAt<PipeNetwork>(
            BoilerBlock.ExhaustOutletWorldPos(Pos),
            BlockFacing.EAST
          )
        : null;
```

⛔ The face is the **north-orientation** east. `StructureFillers.FootprintCells` rotates the declared
port face into the placed orientation, so the *cell* rotates with the machine — but this read passes a
literal facing. Rotate it the same way the offsets are rotated (`ExOrientation`, `StructureAngle`), or a
boiler facing anything but north reads the wrong neighbour and never vents.

**Steam** — no change. `PushSteam` looks one cell above the connector and verifies a `BlockNetworkNode`
with a DOWN connector; the pipe is the node and the filler is the connector, exactly as before. Do not
"fix" it.

- [ ] **Step 5: Build and check the footprint validates**

```bash
./scripts/exmod.sh test 1.21 -Filter "Definition|Filler"
```

Expected: `StructureFootprint.Validate` does not throw. If it reports a duplicate cell, the `Origin` is
wrong — it must be `(-1, -5)` so that the `O` glyph lands on `(0,0,0)`.

### CB6.3 — construction stages

**Interfaces:**
- Consumes: CB1's element paths.
- Produces: four stages naming only paths that exist.

- [ ] **Step 1: Repoint `ShapeSelectiveElements`**

`"Root/Base/*"` no longer exists. The pre-construction mesh is the brick setting:
`.ShapeSelectiveElements("MasonryBase/*")`.

- [ ] **Step 2: Rewrite the four stages**

Map the new element tree onto the same four-stage shape as before — setting, shell, flues and burner,
then crown:

```csharp
      .Construction(c =>
        c.Stage(s => s.AddElements("MasonryBase"))
          .Stage(s =>
            s.RequireMetalPlate(domain, 6)
              .RequireRivets(domain, RivetCode, 8)
              .Require("game:burnedbrick-fire", 8)
              .AddElements("BoilerCasing", "CasingSegment5")
          )
          .Stage(s =>
            s.RequireMetalPlate(domain, 8)
              .RequireMetalRod(domain, 4)
              .RequireRivets(domain, RivetCode, 8)
              .AddElements("Flues", "CoalLayers")
          )
          .Stage(s =>
            s.RequireMetalPlate(domain, 8)
              .RequireRivets(domain, RivetCode, 16)
              .RequireMetalRod(domain, 4)
              .Require("game:burnedbrick-fire", 36)
              .AddElements("BoilerEnds", "MasonryTop")
          )
      );
```

`CasingSegment5` is a sibling of `BoilerCasing` in the art rather than a child, so it must be named
explicitly or the rear barrel course never appears.

- [ ] **Step 3: Verify every named path exists in the shipped shape**

```bash
python - <<'EOF'
import json
d=json.load(open("mods/iiex/assets/iiex/shapes/boiler/cornish.json",encoding='utf-8'))
top={e["name"] for e in d["elements"]}
for p in ["MasonryBase","BoilerCasing","CasingSegment5","Flues","CoalLayers","BoilerEnds","MasonryTop"]:
    assert p in top, f"MISSING {p}"
print("all stage paths resolve")
EOF
```

Expected: `all stage paths resolve`.

- [ ] **Step 4: Build**

```bash
./scripts/exmod.sh test 1.21 -Filter Boiler
```

### CB6.4 — the water renderer box

- [ ] **Step 1: Compute a first approximation**

The barrel (`BoilerCasing` + `CasingSegment5`) spans principal-relative voxels x −11..17, y 0..51,
z −64..16; `Flues` occupies the middle at x −1..17, y 9..39. Water sits inside the shell and below the
flue centreline, so start from:

```csharp
          waterRendererBox = new {
            x1 = -8,
            y1 = 4,
            z1 = -60,
            x2 = 14,
            y2 = 30,
            z2 = 12,
          },
```

- [ ] **Step 2: Confirm it visually**

Place a boiler in a creative world, fill it past `MinBoilWater`, and check the surface sits inside the
barrel with no clipping through the shell or the flue. Adjust and repeat. `BoilerWaterRenderer` reads
this box directly; there is no test that can catch a wrong one.

- [ ] **Step 3: Record the confirmed value**

Put the final numbers in `docs/design/machines/boiler-cornish.md` alongside the other offsets, so the
next geometry change starts from a measured figure rather than re-deriving it.

### CB6.5 — the internal firebox

**Interfaces:**
- Consumes: CB4's configurable behaviour, CB5's texture resolver.
- Produces: `BlockEntityBoiler.Bed => GetBehavior<BEBehaviorFirebox>()`; the `BlockEntityCoalPile` path
  deleted.

- [ ] **Step 1: Declare the behaviour on the boiler**

In `BlockBoiler.BoilerShell`, add the hosted bed with the boiler's own geometry — four layers of four,
drawn from the art's own element names:

```csharp
      .EntityBehavior<BEBehaviorFirebox>(
        new JObject {
          ["layers"] = 4,
          ["unitsPerLayer"] = 4,
          ["bedElement"] = "CoalLayers",
          ["layerPrefix"] = "L",
        }
      )
```

- [ ] **Step 2: Replace the fuel read**

In `BlockEntityBoiler.OnProductionTick`, delete the `BlockEntityCoalPile` lookup and the
`pile.IsBurning` / `pile.inventory` test. `fireOn` becomes the boiler's own lit bit AND a non-empty bed:

```csharp
    bool fireOn = _lit && Bed is { Units: > 0 };
```

Add `_lit` as a serialized bool beside `_burning` — the firebox model has no lit/unlit state of its own,
and the boiler needs one because the spec's `I` cell ignites.

- [ ] **Step 3: Replace the choke extinguish**

`pile?.Extinguish()` becomes `_lit = false;` plus the existing sound at `FuelWorldPos`.

- [ ] **Step 4: Burn the bed down**

The furnace branch runs a fixed clock and never draws fuel down; a boiler must, or stoking is
meaningless. `BEBehaviorFirebox.Consume(int units)` exists and is tested. Accumulate fractional units
against the burning fuel's duration and consume whole units:

```csharp
    _fuelSeconds += dt;
    float perUnit = Math.Max(1f, BurnSecondsPerUnit);
    while (_fuelSeconds >= perUnit && Bed is { Units: > 0 }) {
      Bed.Consume(1);
      _fuelSeconds -= perUnit;
    }
    if (Bed is { Units: <= 0 })
      _lit = false;
```

Both of the fuel's own figures come off the charged stack, resolved from the bed's `FuelCode`. A bed
holds one fuel, so each is stable for the whole bed:

```csharp
  /// <summary>The stack the bed is holding, or null when it is empty.</summary>
  private ItemStack? BedStack =>
    Bed?.FuelCode is { } code && Api != null
      ? new ItemStack(
          Api.World.GetItem(new AssetLocation(code))
            ?? (CollectibleObject?)Api.World.GetBlock(new AssetLocation(code))
        )
      : null;

  /// <summary>Seconds one unit of the charged fuel burns for, from its own combustibleProps.</summary>
  private float BurnSecondsPerUnit =>
    BEBehaviorFirebox.BurnDurationOf(BedStack);

  /// <summary>Flame temperature (°C) of the charged fuel, or 0 when the bed is empty or unlit.</summary>
  private float BedBurnTemperature =>
    _lit ? BEBehaviorFirebox.BurnTemperatureOf(BedStack) : 0f;
```

Cache `BedStack` against the fuel code rather than allocating one per tick — the production tick runs
every second per boiler.

Serialize `_fuelSeconds` in `ToTreeAttributes` / `FromTreeAttributes` beside `_lit`. ⛔ A field missing
from `ToTreeAttributes` reads **zero on the client** — both must be written.

- [ ] **Step 5: Run**

```bash
./scripts/exmod.sh test 1.21 -Filter Boiler
```

### CB6.6 — two hatches

**Interfaces:**
- Produces: `MainHatchOpen` / `ManHatchOpen` replacing `LidOpen`; `ToggleMainHatch` / `ToggleManHatch`
  replacing `ToggleLid`.

- [ ] **Step 1: Split the state**

Replace the single `LidOpen` bool and `ToggleLid` with two of each. Serialize both
(`mainHatchOpen`, `manHatchOpen`); the old `lidOpen` key is not read — nothing has shipped.

- [ ] **Step 2: Split the animations**

`BlockEntityBoiler` starts and stops `"lidopen"`, which the new art does not contain — Animatable
no-ops on a missing clip, so the hatch would silently never move. Drive `"mainhatchopen"` from
`MainHatchOpen` and `"manhatchopen"` from `ManHatchOpen`, both alongside `"idle"`.

- [ ] **Step 3: Split the verbs**

Per the spec: the `I` cell opens the main hatch, charges fuel, ignites and closes. The `M` cell fills
from a bucket, drains to a bucket, and vents steam. Route `HandleInteractStart` / `Step` / `Stop` on the
clicked cell:

| Clicked cell | Empty hand | Fuel in hand | Water container | Empty container |
|---|---|---|---|---|
| `MainHatchWorldPos` | hold → toggle main hatch; if open and bed full → ignite | charge the bed | — | — |
| `ManHatchWorldPos` | hold → toggle man hatch | — | pour in (man hatch open) | bail out (man hatch open) |

Venting moves to the man hatch: `VentExcessSteam` currently keys on `LidOpen`; key it on
`ManHatchOpen`.

- [ ] **Step 4: Split the interaction help**

`GetPlacedBlockInteractionHelp` and `GetFillerInteractionHelp` gate on `LidWorldPos`. Give each cell its
own hints, and add a charge hint and an ignite hint on the main hatch. Every new `ActionLangCode` must
have a key in CB7 — `LangCallSites` scans call sites and will fail on an unguarded one.

- [ ] **Step 5: Run**

```bash
./scripts/exmod.sh test 1.21 -Filter Boiler
```

---

## CB7 — fuel drives the steam, and the rebalance ✅ DONE 2026-08-23

**Files:**
- Modify: `mods/iiex/src/BlockStructures/Boiler/BlockEntityBoiler.cs`
- Modify: `mods/iiex/src/IiexConfig.cs`
- Test: `mods/iiex/tests/Blocks/Boiler/BoilerFuelTests.cs` (create)

**Interfaces:**
- Consumes: `BEBehaviorFirebox.BurnTemperatureOf` / `.BurnDurationOf` from CB4.
- Produces: `BlockEntityBoiler.FuelRateMultiplier` (float 0..1), used by `BoilStep` and by the
  heat-up gate.

- [ ] **Step 1: Write the failing test**

Create `mods/iiex/tests/Blocks/Boiler/BoilerFuelTests.cs` asserting the ruled table:

```csharp
  [Theory]
  [InlineData("game:ore-anthracite", 1.00f)]
  [InlineData("game:ore-bituminouscoal", 1.00f)]
  [InlineData("game:coke", 1.00f)]
  [InlineData("game:charcoal", 1.00f)]
  [InlineData("game:ore-lignite", 0.91f)]
  public void FuelMultiplierFollowsBurnTemperature(string code, float expected) {
    // stand a boiler with a bed of `code`, at 0 atm so Tsat is the boiling point
    Assert.Equal(expected, BoilerWith(code).FuelRateMultiplier, 2);
  }

  [Fact]
  public void LigniteStillRunsOneWattEngine() {
    float rate = IiexValues.CornishBoilerSteamPerSecond * BoilerWith("game:ore-lignite").FuelRateMultiplier;
    Assert.True(rate > IiexValues.WattEngineSteamRate,
      $"lignite raises {rate} L/s against a Watt engine's {IiexValues.WattEngineSteamRate} L/s");
  }
```

Build `BoilerWith` on the CB8 rig; if CB8 has not run yet, do CB8.1 first.

- [ ] **Step 2: Run and watch it fail**

```bash
./scripts/exmod.sh test 1.21 -Filter BoilerFuelTests
```

- [ ] **Step 3: Implement the multiplier**

```csharp
  /// <summary>
  /// How much of the vessel's rated output the burning fuel supports, 0..1. A boiler is limited by its
  /// heating surface rather than by its flame: every coal is far hotter than the water, so the term
  /// saturates at <see cref="IiexValues.BoilerFuelDesignTemp"/> and fuel choice is felt mainly as how
  /// long a bed lasts. 1 with no bed, so an empty boiler's arithmetic is the rated one.
  /// </summary>
  public float FuelRateMultiplier {
    get {
      float flame = BedBurnTemperature;
      if (flame <= 0f)
        return 1f;
      float sat = SteamTemperature();
      float head = Math.Max(1f, IiexValues.BoilerFuelDesignTemp - sat);
      return GameMath.Clamp((flame - sat) / head, 0f, 1f);
    }
  }
```

Multiply `SteamPerSecond` by it in `BoilStep`, and divide `BoilerHeatUpSeconds` by it in the `Heating`
branch so a cool fire also takes longer to raise steam.

- [ ] **Step 4: Apply the rebalance**

Set the six config values from the *Global constraints* table.

- [ ] **Step 5: Run and watch it pass**

```bash
./scripts/exmod.sh test 1.21 -Filter Boiler
```

- [ ] **Step 6: Check the pressure arithmetic still holds**

With 600 L of steam space, 5 atm needs 3000 L of steam: unloaded that is 47 s, with one Watt engine
88 s, with two 750 s. Assert the unloaded figure in a test so a later capacity change cannot silently
make the boiler un-burstable or instantly fatal.

---

## CB8 — the tests ✅ DONE 2026-08-23

Every boiler fixture hard-wires the vanilla coal pile and the old scene.

**Files:**
- Modify: `mods/iiex/tests/Fixtures/BoilerFakes.cs`
- Modify: `mods/iiex/tests/Fixtures/BoilerRig.cs`
- Modify: `mods/iiex/tests/Fixtures/IiexScenes.cs`
- Modify: `mods/iiex/tests/Blocks/Boiler/BoilerTickTests.cs`
- Modify: `mods/iiex/tests/Blocks/Boiler/BoilerDropTests.cs`

### CB8.1 — the rig

- [ ] **Step 1: Delete the coal-pile scaffolding**

`BoilerFakes` builds a real `BlockEntityCoalPile` with a reflected inventory; `BoilerRig` places it at
`FuelWorldPos` and pokes its private `burning` field for `ExtinguishFire` / `RelightFire`. All of that
goes.

- [ ] **Step 2: Replace with bed charging**

`ExtinguishFire` becomes "clear the lit bit"; `RelightFire` becomes "fill the bed and light". Add
`ChargeBed(string fuelCode, int units)` so `BoilerFuelTests` can stand a bed of any coal.

⛔ **Write the premise as an assertion.** A stub `Block` hides production: assert that the rig's boiler
block is a real `BlockBoilerCornish` carrying the `BEBehaviorFirebox` behaviour, or a later refactor that
drops the behaviour will leave every test green.

- [ ] **Step 3: Drop the multiblock scene**

`IiexScenes` builds the player-built firebox around the boiler. The boiler is self-contained now; the
scene reduces to placing the block and letting its own footprint land.

- [ ] **Step 4: Run**

```bash
./scripts/exmod.sh test 1.21 -Filter Boiler
```

Expected: PASS.

### CB8.2 — re-bless the golden

- [ ] **Step 1: Rewrite**

```bash
EXLIB_WRITE_GOLDENS=1 ./scripts/exmod.sh test 1.21 -Filter DefinitionGoldens
```

- [ ] **Step 2: Read the diff before accepting it**

```bash
git diff mods/iiex/tests/goldens/iiex/blocktypes/boiler/cornish.json
```

Check: 40 filler cells, two of them carrying `portFace`; no `multiblockStructure`; four construction
stages naming the new element paths; the firebox behaviour with its four properties. A golden that
changed in a way you cannot explain is a bug, not a re-bless.

- [ ] **Step 3: Run the whole suite**

```bash
./scripts/exmod.sh test 1.21
```

Expected: 9 targets green.

---

## CB9 — assets and docs ✅ DONE 2026-08-23

- [ ] **Step 1: Lang keys**

Add every `ActionLangCode` CB6.6 introduced to `mods/iiex/assets/iiex/lang/en.json`, then hand-translate into
`ru.json` and `uk.json` following `docs/internal/` § RU/UK conventions. At minimum: main-hatch toggle,
charge, ignite, man-hatch toggle, fill, drain, vent. Remove `boiler-info-lidopen` and add the two
hatch-state HUD lines.

```bash
./scripts/exmod.sh test 1.21 -Filter "Lang|Referenced"
```

Expected: PASS. `LangCallSites` scans call sites and fails on an unguarded key.

- [ ] **Step 2: Handbook**

```bash
EXLIB_WRITE_HANDBOOK=1 ./scripts/exmod.sh test 1.21 -Filter HandbookSync
```

While the page is open, fix the construction bill: it says 24 plates / 18 nails-and-strips / 8 rods /
48 fire bricks; the code totals 22 plates / **32 rivets** / 8 rods / 44 bricks, and CB6.3 changes it
again. Re-derive from the stages rather than editing the old numbers.

- [ ] **Step 3: `docs/design/machines/boiler-cornish.md`**

The authoritative page and currently stale in four places. Rewrite: the structure table (3 × 6 × 3,
40 fillers, no multiblock), the geometry offsets, the three ports, the firebox section replacing *The
player-built firebox*, the new stat table and the fuel table. Line 123 claims no editable shape exists —
two do. Gotcha 12 is resolved: say so, and record that the offsets are now deliberately distinct.

- [ ] **Step 4: `docs/design/items/fuels.md`**

Lignite's row says it is read by "nothing — the firebox excludes it by name". It now has a consumer.
Record that admission is read from `combustibleProps` rather than a name list, that vanilla's burn
figures are read for the first time, and that the metallurgical exclusion moved to
`AcceptsFireboxFuel`. The § *Vanilla coal piles — the cowper and the boilers* table loses both boiler
rows.

- [ ] **Step 5: `docs/design/machines/firebox.md`**

The bed is configurable now: layer count, units per layer, bed element and layer prefix are per-block
data with the shipped furnaces on the defaults. Record the boiler as a host.

- [ ] **Step 6: `docs/design/mechanics/multiblock.md`**

Add the declarative filler port from CB2 next to the existing hosted-behaviour note, and state the
connector-versus-node choice explicitly so the next machine does not have to re-derive it. Its § on
roles says the boiler has firebox-ish cells and no `Firebox` role "because nothing asks them for a fuel
cell set" — still true; the bed is on the block entity, not on a cell. Confirm rather than change.

- [ ] **Step 7: `workbench/layouts.md`**

The boiler section renders under retired domains (`lpex:boilercornish`, `ExCodes.FireBricks`). Re-render
it from the new footprint, and drop the pending internal-firebox note — it landed.

- [ ] **Step 8: `docs/internal/plans/NEXT.md` and the worklog**

Add a *what landed* paragraph to `docs/internal/worklog/2026-08.md` (newest first) and update `NEXT.md`
per the maintenance rule at its foot.

- [ ] **Step 9: The recipe's dead ingredient**

`mods/iiex/src/Recipes/Grid/MachineRecipeDefinitions.cs` § `CornishBoiler` declares
`Ingredient("I", StraightPipe(1))` against pattern `"PHP,BNB"` — there is no `I` in it, so the frame
silently costs no pipe. Either add `I` to the pattern or drop the ingredient; it is a one-line decision
and the area is open.

- [ ] **Step 10: Full green**

```bash
./scripts/exmod.sh format -Check && ./scripts/exmod.sh test 1.21
```

Expected: 9 targets green.

---

## CB10 — follow-through

Not required for the boiler to work; each is a loose end this change creates or exposes.

- [ ] **The Lancashire.** It shares `BlockBoiler`, `IBoilerGeometry` and `BlockEntityBoiler` and has the
  same `fuelOffset (0,0,-1)`. ⛔ **This step's "blocking for a green suite" was wrong.** CB6 kept the
  coal-pile branch on the shared base behind a `Bed != null` test, so the Lancashire still runs
  unconverted — what it lost is its `MultiblockLayout`, which went with the multiblock base. Its fire is
  now a free-placed `game:coalpile` that **nothing requires**, and its art keeps the `lidopen` clip
  (`workbench/shapes/machines/steam/machine-pipe-megablock-boiler-lancashire.json`), which the leaf
  works around by overriding `ManHatchAnimation`. Give it its own footprint and shape work when it comes
  up; it is not urgent, only untidy.
- [ ] **The cowper's coal pile.** `BlockCowperStoveIntake` is the other `@(air|coalpile)` consumer, and
  after the Lancashire it will be the only one. `firebox.md` rules that once the boilers take internal
  fireboxes, nothing should still read a free pile. Out of scope here.
- [ ] **The Watt engine's dead gear ingredient.** `MachineRecipeDefinitions.WattEngine` declares
  `Ingredient("G", Gear(gear, 2))` against pattern `"_H_,PRP,PIP"`, which has no `G` — the same defect
  CB9 fixed on the boiler frame, and worse here, because the recipe is emitted once per gear code and the
  two emissions are therefore identical. Fixing it is a recipe change (and a golden re-bless), not a
  cleanup.
- [ ] **`LangCallSites` only matches lowercase keys.** Its `BareLiteral` regex is `"([a-z0-9-]+)"`, so a
  `SendIngameError` code carrying a capital is skipped and its key ships unguarded. Found by mutation
  while proving the new `iiex-firebox-notcoking` key is covered.
- [ ] **`ShapeExtents` as a machine guard.** `mods/exlib/testing/ShapeExtents.cs` already measures
  drawn mesh against declared extents but is wired only to the stock-art suites. A 3 × 6 × 3 boiler is
  the natural first machine consumer — it would have caught the sixth row automatically.
- [ ] **The four second-slot `iron4` shapes** from CB0.
- [ ] **The 17 shipped shapes** whose `iron4` flips on next conversion, per shape with the code check.

---

## Gate

A player, in survival:

1. crafts a Cornish boiler frame and places it;
2. builds it through four RCC stages and sees each course appear;
3. opens the main hatch, charges it with **lignite**, lights it, and closes the hatch;
4. pipes feedwater into the south face of the base and steam out of the top;
5. pipes the exhaust east and sees the fire choke when it is blocked;
6. raises steam and drives **two** Watt engines from it;
7. bails water with a bucket through the man hatch, and vents through it under pressure;
8. lets it run dry under fire and watches it burst.

No fire brick is placed by hand at any point.
