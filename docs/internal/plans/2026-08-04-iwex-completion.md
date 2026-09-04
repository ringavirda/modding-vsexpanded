# iwex — the completion plan

> **⛔ Reading note, added 2026-08-14.** This plan predates the `iwex`+`lpex` merge. Where it says
> **`iwex`** as a *domain or assembly* it now means **`iiex`** (block codes, lang keys, asset paths,
> namespaces). Where it says `iwex` as a *scope* - "finish iwex", "an iwex-only player" - the phrase no
> longer refers to anything: ruling **M2** retired per-mod closure in favour of per-**loop**, and the
> early loop is the whole of `iiex`. Paths and type names below were repointed at their live homes on
> 2026-08-14; `iwex:` / `lpex:` **code literals** were deliberately left, being historical migration
> sources.
>
> ★★ **Triaged 2026-08-14: U1 is BUILT — this plan is a record, not a queue.** Verified against `src/`:
> `BlockEntitySandCastingLongCell`, `LongCellLayout`, the `MoldSize.LongCell` refusal, the four
> long-cell patterns and the cast-part item set all exist, and U1.6's acceptance steps are ticked. The
> Global Constraints and Commands blocks at the top are still the current house rules and are why this
> file is kept. ⛔ The gate line says *"all three suites"* — it is **three** now (`exlib`, `iiex`,
> `siex`); see [NEXT.md](NEXT.md).

This file holds the U1 task detail, the Global Constraints and the Commands block. Sequencing and task
detail for U2 onward live in [`2026-08-04-iwex-u2-u10-expansion.md`](2026-08-04-iwex-u2-u10-expansion.md).

**Goal:** finish iwex — every remaining feature, in one ordered sequence, ending at *cast, puddle, roll,
fasten, reward* with no dangling end pointing at steam.

**Architecture:** iwex is a Vintage Story content mod on the in-repo `exlib` framework. Blocktypes,
itemtypes and recipes are authored in C# (`ExBlockDef` / `ExItemDef` / `ExRecipeDef`) and rendered to
JSON pinned by checked-in goldens; machines are `BlockFilledMegastructure` multiblocks whose footprint
is a `MultiblockLayout` drawing carrying per-cell `CellRole`s.

**Tech Stack:** C# / .NET (net7.0 · net8.0 · net10.0, one per game version), Vintage Story API 1.20–1.22,
xUnit + NSubstitute, `ExpandedLib.Testing` harness (`StructureRig`, `TestWorld`, `DefinitionGoldens`).

---

## Global Constraints

Every unit's requirements implicitly include this section.

- Never commit. The user owns the git history. Leave work in the tree; record the reasoning in
  `WORKLOG.md` at the repo root (newest first). Where a plan template says "commit", this plan says
  "append a WORKLOG entry".
- Get the test signal from `./scripts/exmod.sh test 1.21`, not `latest`. The 1.22 lane reports
  ~20 failures caused by an upstream Vintage Story 1.22.6 change that made `IPlayer` unmockable. They are
  not ours and no change in this plan can fix them. 1.20 and 1.21 must be green.
- Baseline to hold: 2506 tests, zero skips, on 1.20 and 1.21.
- Check `assets/editable/shapes/` before asking anyone to draw anything. Most of the art is already
  drawn and merely unexported. `assets/editable/` is source-only and never shipped; every unit that
  touches art exports *into* `assets/iiex/`.
- Density rule: 1 voxel³ = 2.5 units. Every new mass is derived from a drawn shape, never invented.
- A `side` variant renders a single letter (`n`/`e`/`s`/`w`), as does an `orientation` variant. The
  only word-spelled facings left are groups sourced from vanilla's `abstract/horizontalorientation`
  worldproperty (the slag stairs). Reach a facing through the generated table —
  `IiexBlocks.<Block>.WithSide(BlockFacing.NORTH)` — never by typing it.
- A machine lives with the content it *feeds*, not with the content it is made of. iwex must never
  reach "upward" into lpex/smex for an ingredient.
- Never force `StructureComplete` in a test. Build the real footprint with `StructureRig` and let
  the machine complete itself.
- New numbers go through config (`IiexConfig` / `ExlibConfig`), never literals.
- `AllowedX` / candidate-set properties must be cached fields, never recomputed per call.
- Tests are grouped with `#region` blocks.
- `BlockEntityFurnaceCore` is shared by the cold blast furnace, the hot blast furnace (smex), the
  cupola, the puddling hearth and the heating hearth. Every change must leave the two reverberatory
  hearths working — they have a firebox, not a column, so the model must degenerate to one column cleanly.
- Regenerating artifacts: `EXLIB_WRITE_BLOCKCODES=1` for `{Mod}Blocks.g.cs`;
  `EXLIB_WRITE_GOLDENS=<comma-separated path list>` for goldens. Never run `EXLIB_WRITE_GOLDENS=1` — it
  re-blesses a whole domain and the tree carries ~110 hand-checked uncommitted goldens.

### Commands

```bash
# One suite, one test class — the inner loop.
dotnet test test/IronIndustryExpanded.Tests/IronworkingExpanded.Tests.csproj \
  -f net8.0 -p:Legacy=true --nologo --filter "FullyQualifiedName~LongCellTests"

# The gate. Must print PASS on all three suites.
./scripts/exmod.sh test 1.21

# Before declaring a unit done, also:
./scripts/exmod.sh test 1.20
```

---

# U1 — The casting route

**Unit goal:** a player can pour cast stock and cast parts. Concretely: cupola → long cell → a cast slab,
and cupola → casting cell → a cast part.

**Why this unit is first among the content units:** the art is entirely drawn, the casting *cell* is live
and its rules are already pure functions, and `castbillet` / `castbloom` / `castslab` are names the heating
hearth and the rolling mill already reference but which no definition produces. It is the cheapest stage
with the largest downstream unlock.

## File structure

| File | Responsibility |
|---|---|
| `src/IronIndustryExpanded/BlockStructures/Casting/BlockEntities/BlockEntitySandCastingCell.cs` | **modify** — reject a `longcell` pattern |
| `src/IronIndustryExpanded/BlockStructures/Casting/Blocks/BlockSandCastingLongCell.cs` | **create** — the 1×2 megablock + its code-first def |
| `src/IronIndustryExpanded/BlockStructures/Casting/BlockEntities/BlockEntitySandCastingLongCell.cs` | **create** — the station's block entity |
| `src/IronIndustryExpanded/BlockStructures/Casting/LongCellLayout.cs` | **create** — footprint + filler offsets, kept out of the block so it is testable without a world |
| `src/IronIndustryExpanded/Items/CastStockItemDefinitions.cs` | **create** — `castbillet`, `castbloom`, `castslab` |
| `src/IronIndustryExpanded/BlockStructures/Casting/PatternItemDefinitions.cs` | **modify** — the four long-cell patterns |
| `src/IronIndustryExpanded/Recipes/Grid/CastingRecipeDefinitions.cs` | **modify** — the long cell's build recipe |
| `assets/iiex/shapes/casting/…` | **create** — seven exported long-cell shapes |
| `assets/iiex/lang/{en,ru,uk}.json` | **modify** — names + descriptions for every new block and item |
| `test/IronIndustryExpanded.Tests/Blocks/Casting/LongCellTests.cs` | **create** |
| `test/IronIndustryExpanded.Tests/Items/CastStockMassTests.cs` | **create** |

---

## U1.1 — The casting cell refuses a long-cell pattern

`MoldSpec.Size` is parsed, round-tripped by two tests, and read by nothing in `src/`. Until something
enforces it, ramming a `longcell` pattern into a 1×1 cell silently produces a cast the cell is the wrong
shape for. This task is deliberately first: it is a one-line guard, and it is the only thing that makes the
field mean anything before the block that consumes it exists.

**Files:**
- Modify: `src/IronIndustryExpanded/BlockStructures/Casting/BlockEntities/BlockEntitySandCastingCell.cs` (the `Imprint` method, ~`:238-264`)
- Modify: `assets/iiex/lang/{en,ru,uk}.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Casting/CastingCellTests.cs` — does not exist yet; the
  folder holds only `SandCastingBedTests.cs`. Create it, and create the `CastingCellScenes` fixture it
  needs in `test/IronIndustryExpanded.Tests/Fixtures/` alongside `ColdBlastFurnaceScenes.cs`, which is the
  model to copy (real blocks, real ticks, nothing forced)

**Interfaces:**
- Consumes: `MoldSpec.Size` (`MoldSize.Cell` | `MoldSize.LongCell`), `MoldSpec.TryParse`.
- Produces: the error key `iwex-castingcell-wrongsize`, reused verbatim by U1.4's long cell (which
  refuses a `cell` pattern with its own key `iwex-longcell-wrongsize`).

- [x] **Step 1: Write the failing test**

Create `test/IronIndustryExpanded.Tests/Blocks/Casting/CastingCellTests.cs` with a `#region Pattern size`
(this repo groups test methods with `#region`):

```csharp
[Fact]
public void A_longcell_pattern_is_refused_by_the_one_by_one_cell()
{
  // MoldSpec.Size existed for months with no consumer. Without this the cell accepts a pattern whose
  // cavity is 28 voxels long, sets its capacity from it, and casts a slab out of a 1x1 block.
  var rig = CastingCellScenes.RammedFull();

  bool handled = rig.Interact(CastingCellScenes.PatternStack("castslab"));

  Assert.True(handled);                       // the click is consumed, not passed through
  Assert.False(rig.Cell.HasImpression);       // ...but nothing was imprinted
  Assert.Equal("iwex-castingcell-wrongsize", rig.LastError);
}
```

`CastingCellScenes` does not exist. Build it in this step — a `TestWorld`, a placed
`BlockSandCastingCell` + `BlockEntitySandCastingCell`, and helpers `RammedFull()`, `PatternStack(type)`,
`Interact(stack)` and `LastError`. `LastError` captures the code passed to `SendIngameError` on the
substituted `IServerPlayer`. Do not assert on the message text — the standing convention is
`SendIngameError(code)` only, and the text lives in lang.

- [x] **Step 2: Run the test and watch it fail**

```bash
dotnet test test/IronIndustryExpanded.Tests/IronworkingExpanded.Tests.csproj \
  -f net8.0 -p:Legacy=true --nologo \
  --filter "FullyQualifiedName~A_longcell_pattern_is_refused_by_the_one_by_one_cell"
```

Expected: FAIL — `HasImpression` is `true`, because the cell imprints it.

- [x] **Step 3: Add the guard**

In `BlockEntitySandCastingCell.Imprint`, immediately after the existing `spec == null` refusal:

```csharp
    // The size is not decoration. A `longcell` pattern's cavity is 28 voxels long; imprinting it here
    // would set this 1x1 cell's capacity from a cavity it physically does not contain. MoldSpec.Size had
    // no consumer at all until this guard, so the field parsed and validated and then meant nothing.
    if (spec.Size != MoldSize.Cell)
    {
      if (Api.Side == EnumAppSide.Server)
        (byPlayer as IServerPlayer)?.SendIngameError("iwex-castingcell-wrongsize");
      return true;
    }
```

- [x] **Step 4: Add the lang keys**

In `assets/iiex/lang/en.json` (then translate for `ru`/`uk` — see the RU/UK conventions: single `-`, never
an em-dash, EN is the source of truth):

```json
"iwex-castingcell-wrongsize": "That pattern is for the long cell. This cell only takes 1x1 patterns."
```

- [x] **Step 5: Run the test and watch it pass**

Same command as Step 2. Expected: PASS.

- [x] **Step 6: Run the full 1.21 lane**

```bash
./scripts/exmod.sh test 1.21
```

Expected: PASS on all three suites. If `LangCoverage` fails, a locale is missing the new key.

- [x] **Step 7: Append a WORKLOG entry** (do not commit)

---

## U1.2 — Settle and pin the cast-stock masses

The three sources for every cast-stock mass — the long cell's drawn lane cavity, the drawn item shape, and
the ladder table — disagreed pairwise for the bloom and the slab, and the billet's own three lanes were not
equal to each other. `docs/design/machines/long-cell.md` § *Numbers* has the full audit. Fixing this after
the items exist means touching every recipe that consumes them, so it is settled here, before anything is
defined.

**Files:**
- Create: `src/IronIndustryExpanded/Items/CastStockItemDefinitions.cs`
- Test: `test/IronIndustryExpanded.Tests/Items/CastStockMassTests.cs`
- Modify: `docs/design/machines/long-cell.md` (record the ruling)

**Interfaces:**
- Produces: `CastStockItemDefinitions.BilletUnits`, `.BloomUnits`, `.SlabUnits` (`const int`), and the item
  codes `iwex:caststock-billet`, `iwex:caststock-bloom`, `iwex:caststock-slab`. U1.3 reads the `*Units`
  constants for its cavity capacities; U7's rolling work reads the codes.

Settled 2026-08-04: the ladder wins and the rule binds exactly. Billet **600** · bloom **1000** · slab
**3000**. The art moves to the numbers — lengths `24→27` / `24→25` / `28→25`, then a draft chamfer takes
the billet from 243 to 240 vx³ so `vx³ × 2.5 == units` is a hard equality with no tolerance. The numbers
below are the ruling, not placeholders.

- [x] **Step 1: Write the failing test**

```csharp
public class CastStockMassTests
{
  // The density rule is the only thing keeping four hand-written numbers in agreement: the lane
  // cavity in the long cell's pattern, the drawn item shape, the ladder table and the remelt value.
  // Pinning it here is what stops the next person choosing a fifth.
  private const float UnitsPerVoxel = 2.5f;

  [Theory]
  [InlineData(CastStockItemDefinitions.BilletUnits, 240)] // item/castbillet.json - 3 x 3 x 27 less draft
  [InlineData(CastStockItemDefinitions.BloomUnits, 400)]  // item/castbloom.json  - 4 x 4 x 25
  [InlineData(CastStockItemDefinitions.SlabUnits, 1200)]  // item/castslab.json   - 12 x 4 x 25
  public void Every_cast_stock_mass_is_its_drawn_volume_times_the_density_rule(
    int units,
    int drawnVoxels
  )
  {
    Assert.Equal(drawnVoxels * UnitsPerVoxel, units);
  }
}
```

The `drawnVoxels` figures above are measured from the art as it stands today. If the ruling changes a
mass, the art is redrawn to suit and these numbers move with it — that is the point of the test. Re-measure
before changing a number here.

- [x] **Step 2: Run it and watch it fail to compile**

Expected: `error CS0103: The name 'CastStockItemDefinitions' does not exist`.

- [x] **Step 3: Create the item definitions**

`src/IronIndustryExpanded/Items/CastStockItemDefinitions.cs`, following the shape of the existing
`CastPartItemDefinitions.cs` in the same folder (read it first — it is the pattern for the mass constant
sitting next to the def):

```csharp
using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Items;

/// <summary>
/// The bulk cast stock the long cell pours: billet, bloom and slab. These are the rolling mill's cast-side
/// feed - the wrought side is a hammered bloom - and they are the reason the long cell exists.
/// <para>
/// Every mass here is <b>its drawn shape's volume x 2.5</b> (the density rule), pinned by
/// <c>CastStockMassTests</c>. Three independent sources used to disagree about these numbers; see
/// <c>docs/design/machines/long-cell.md</c> for the audit that settled them.
/// </para>
/// </summary>
public class CastStockItemDefinitions : IExItemDefProvider
{
  /// <summary>Units of cast iron in one billet (= its cavity volume).</summary>
  public const int BilletUnits = 600;

  /// <summary>Units of cast iron in one bloom.</summary>
  public const int BloomUnits = 1000;

  /// <summary>Units of cast iron in one slab.</summary>
  public const int SlabUnits = 3000;

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Stock(domain)];

  private static ExItemDef Stock(string domain) =>
    ExItemDef
      .Create(domain, "caststock")
      .VariantGroup("form", "billet", "bloom", "slab")
      .Raw("shapeByType", new Dictionary<string, object>
      {
        ["*-billet"] = new { @base = "iwex:item/castbillet" },
        ["*-bloom"] = new { @base = "iwex:item/castbloom" },
        ["*-slab"] = new { @base = "iwex:item/castslab" },
      })
      .Raw("attributesByType", new Dictionary<string, object>
      {
        ["*-billet"] = new { materialUnits = BilletUnits },
        ["*-bloom"] = new { materialUnits = BloomUnits },
        ["*-slab"] = new { materialUnits = SlabUnits },
      })
      .MaxStackSize(1)
      .CreativeCommon("*");
}
```

These three numbers are the ruling — the settled ladder, binding exactly against the redrawn cavities
(240 / 400 / 1200 vx³ × 2.5). The art has to move with them — see U1.3 for the lane cavities. Never
change a number here without redrawing.

- [x] **Step 4: Run the test and watch it pass**

```bash
dotnet test test/IronIndustryExpanded.Tests/IronworkingExpanded.Tests.csproj \
  -f net8.0 -p:Legacy=true --nologo --filter "FullyQualifiedName~CastStockMassTests"
```

- [x] **Step 5: Add lang keys and create the golden**

Names + descriptions for all three forms in `en`/`ru`/`uk`. Then create the item golden:

```bash
EXLIB_WRITE_GOLDENS=iwex/itemtypes/caststock \
  dotnet test test/IronIndustryExpanded.Tests/IronworkingExpanded.Tests.csproj \
  -f net8.0 -p:Legacy=true --nologo --filter "FullyQualifiedName~GoldenTests"
```

**Read the written golden before moving on.** A golden accepted unread is not a record of anything.

- [x] **Step 6: Record the ruling in the docs**

Record the ruling and the date in `docs/design/machines/long-cell.md`, and bring the cast-stock ladder
table into agreement wherever it is stated.

- [x] **Step 7: Run the full 1.21 lane, then append a WORKLOG entry**

---

## U1.3 — The long-cell patterns

Four patterns — `castslab`, `castblooms`, `castbillets`, `castframe` — each carrying `size: "longcell"` and
the cavity boxes its filling shape was measured from.

**Files:**
- Modify: `src/IronIndustryExpanded/BlockStructures/Casting/PatternItemDefinitions.cs`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Casting/MoldSpecTests.cs`

**Interfaces:**
- Consumes: `CastStockItemDefinitions.{Billet,Bloom,Slab}Units` (U1.2); the `Mold(...)` and `Box(...)`
  helpers already private to `PatternItemDefinitions`.
- Produces: pattern type variants `castslab`, `castblooms`, `castbillets`, `castframe`, which
  `PatternItemDefinitions.PatternTypes` picks up automatically — and with it the diagram item and the
  diagram→pattern recipe, both of which derive from that array.

- [x] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData("castslab")]
[InlineData("castblooms")]
[InlineData("castbillets")]
[InlineData("castframe")]
public void Every_long_pattern_declares_the_long_size(string type)
{
  // A long pattern that forgets `size` defaults to "cell" (MoldSpec.cs:60) and is then silently
  // accepted by the 1x1 station - the exact hole U1.1's guard was added to close. The default is what
  // makes this worth a test rather than an eyeball.
  JsonObject mold = PatternMold(type);

  Assert.True(MoldSpec.TryParse(mold, out MoldSpec? spec, out string? error), error);
  Assert.Equal(MoldSize.LongCell, spec!.Size);
}
```

`PatternMold(type)` runs `PatternItemDefinitions.Definitions("iwex")`, renders to JSON, and pulls
`attributesByType["*-{type}-*"].mold` — mirror however `MoldSpecTests` already reaches the shipped
patterns.

- [x] **Step 2: Run it and watch it fail**

Expected: FAIL — `TryParse` returns false, `error` is `missing 'mold' attribute` (the type does not exist
yet).

- [x] **Step 3: Add the four entries**

In `PatternItemDefinitions.Molds`, after the existing four. The cavity boxes are measured from
`long-cell.md` § *Cavities*; caution — they span `z -12 … 12`, i.e. negative coordinates in principal-local
16-space, the first patterns in the mod to do so.

```csharp
    // ── Long cell (1x2). Every cavity runs z -12..12; the lane count is the product
    //    (3 narrow = billets, 2 = blooms, 1 = slab), which is the whole cast-stock ladder off one block.
    ["castslab"] = Mold(
      "iwex:casting/longcell-filling-castslab",
      CastStockItemDefinitions.SlabUnits,
      [Box(4, 6, -12, 12, 10, 12)],
      "iwex:caststock-slab",
      size: "longcell"
    ),
    ["castblooms"] = Mold(
      "iwex:casting/longcell-filling-blooms",
      CastStockItemDefinitions.BloomUnits * 2,   // two lanes, one pour
      [Box(4, 6, -12, 7, 10, 12), Box(9, 6, -12, 12, 10, 12)],
      "iwex:caststock-bloom",
      size: "longcell"
    ),
    ["castbillets"] = Mold(
      "iwex:casting/longcell-filling-billets",
      CastStockItemDefinitions.BilletUnits * 3,  // three lanes, one pour
      [Box(3, 8, -12, 5, 11, 12), Box(6, 8, -12, 9, 11, 12), Box(10, 8, -12, 13, 11, 12)],
      "iwex:caststock-billet",
      size: "longcell"
    ),
    ["castframe"] = Mold(
      "iwex:casting/longcell-filling-castframe",
      636 * 5 / 2,
      [Box(4, 6, -12, 12, 10, 12)],
      "iwex:castframe",
      size: "longcell"
    ),
```

Caution: a multi-lane pattern yields more than one item. `Capacity` is the whole impression, but a
two-lane bloom pour must shake out two blooms. The `MoldSpec` record has no field for that today.
Add one:

```csharp
/// <param name="Yield">How many <see cref="Output"/> stacks a full cast shakes out. Multi-lane long-cell
/// patterns pour one impression and yield one item per lane; every 1x1 pattern is 1.</param>
```

…parsed as `mold["yield"].AsInt(1)` (defaulting to 1 keeps every existing pattern valid), passed to
`Mold(...)` as an optional argument, and multiplied into the stack size in
`BlockEntitySandCastingCell.BuildHarvest`. Doing it on the shared record rather than in the long cell is
what keeps the two stations one implementation.

- [x] **Step 4: Run the test and watch it pass**

- [x] **Step 5: Add the four diagram textures**

`assets/iiex/textures/item/diagram/diag-item-{castslab,castblooms,castbillets,castframe}.png`, 32×32 each.
These are derived automatically from `PatternTypes`, so a missing one is a missing texture at runtime, not
a compile error. This is the one genuinely-new art in U1.

- [x] **Step 6: Add lang keys, re-bless the pattern goldens (scoped), read them, run the 1.21 lane**

- [x] **Step 7: Append a WORKLOG entry**

---

## U1.4 — The long cell block

**Files:**
- Create: `src/IronIndustryExpanded/BlockStructures/Casting/LongCellLayout.cs`
- Create: `src/IronIndustryExpanded/BlockStructures/Casting/Blocks/BlockSandCastingLongCell.cs`
- Create: `src/IronIndustryExpanded/BlockStructures/Casting/BlockEntities/BlockEntitySandCastingLongCell.cs`
- Create: `assets/iiex/shapes/casting/{sandcastinglongcell,longcell-filling-base,longcell-filling-half,longcell-filling-billets,longcell-filling-blooms,longcell-filling-castslab,longcell-filling-castframe}.json`
- Test: `test/IronIndustryExpanded.Tests/Blocks/Casting/LongCellTests.cs`

**Interfaces:**
- Consumes: `BlockFilledMegastructure`, `IFillerHost`, `IFillerInteractionTarget` (exlib);
  `CastingCellLogic.{Decide,FillingShape,IsMisrun,CanIntake}` — unchanged, the long cell is the same
  state machine at a different size; `BEBehaviorMoltenCell`.
- Produces: block code `iwex:casting-sandlongcell-{brick}-{side}`; `LongCellLayout.FillerOffsets`.

- [x] **Step 1: Export the seven shapes**

From `assets/editable/shapes/molten-megablock-sandlongcell.json` and the five
`molten-sandlongcellfilling-*.json` sources, plus a `-half` legacy mesh. Use `scripts/tools/convert-shape.py` if
it applies; otherwise export from the editor.

They carry the `andesite` texture key (the rammed-sand key the casting cell's fillings use), so the
block must declare that key or the sand renders untextured.

- [x] **Step 2: Write the failing test — the block completes its own footprint**

```csharp
[Theory]
[InlineData("n")]
[InlineData("e")]
[InlineData("s")]
[InlineData("w")]
public void A_long_cell_completes_its_two_cell_footprint_at_every_facing(string side)
{
  // Never force StructureComplete: build the real footprint and let the machine notice it. A rig that
  // forces it cannot see a wrong filler offset, which at 1x2 is the only thing that can be wrong.
  var rig = LongCellScenes.Complete(side);

  Assert.True(rig.Principal.StructureComplete);
  Assert.Equal(1, rig.FillerCount);
}
```

- [x] **Step 3: Run it and watch it fail to compile**

Expected: `LongCellScenes` does not exist.

- [x] **Step 4: Write `LongCellLayout`**

One filler at `(0, 0, 1)` in model space — the body extends −Z, the same +180 convention the casting bed
uses:

```csharp
using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;

namespace IronworkingExpanded.BlockStructures.Casting;

/// <summary>
/// The long cell's 1x2 footprint. Kept out of the block so the offsets can be pinned without standing a
/// world up - the same split <see cref="SandBedLayout"/> makes.
/// <para>
/// The second cell is a filler for collision and interaction <b>only</b>. The whole station is one
/// casting, so it hosts no molten cell of its own - which also keeps it clear of the rule that a filler
/// may not be a network graph node.
/// </para>
/// </summary>
public static class LongCellLayout
{
  /// <summary>The one filler, in model space. The body extends -Z from the principal.</summary>
  public static IEnumerable<FillerCellSpec> Footprint() =>
    [new FillerCellSpec(0, 0, 1)];
}
```

`ExBlockDef.FillerOffsets` takes `IEnumerable<FillerCellSpec>` (a `readonly record struct` of
`X, Y, Z, AllowAttach, Behaviors`), not `Vec3i`. The extra fields are why: a cell that hosts a
behaviour or accepts attachment says so here. The long cell's filler needs neither.

- [x] **Step 5: Write the block**

`BlockSandCastingLongCell : BlockFilledMegastructure, IFillerHost, IFillerInteractionTarget,
IExBlockDefProvider`. Read `BlockSandCastingBed.cs` first — it is the megablock idiom this follows
(filler offsets, `StructureAngle = AngleFromSide + 180`, the brick/side variant groups, the `fire1` tint
overlay and the `andesite` sand key). The def:

```csharp
  private static ExBlockDef LongCell(string domain) =>
    ExBlockDef
      .Create(domain, "casting-sandlongcell", "casting/sandlongcell")
      .Class<BlockSandCastingLongCell>()
      .EntityClass<BlockEntitySandCastingLongCell>()
      .Material(EnumBlockMaterial.Ceramic)
      .MiningTier(0)
      .Resistance(3.5f)
      .MaxStackSize(8)
      .Behavior("ExOrientable")
      .FillerOffsets(LongCellLayout.Footprint())
      // Capacity is overridden from the impressed pattern, exactly as the 1x1 cell does; this is only
      // the pre-impression ceiling. drainFitting: it is fed, it does not join the graph.
      .EntityBehavior(
        "exlib.BEBehaviorMoltenCell",
        new JObject { ["capacity"] = 3400, ["drainFitting"] = true }
      )
      .VariantGroup("brick", "fire", "black", "brown", "cream", "gray", "orange", "red", "tan")
      .SideVariant()
      .ShapeSpunPerOrientation("iwex:casting/sandcastinglongcell", offset: 180)
      .Texture(
        "fire1",
        "game:block/clay/brick/four/running/cream1",
        "game:block/clay/brick/four/running/{brick}1"
      )
      .Texture("burned", "game:block/clay/vessel/sides/burned")
      .Texture("andesite", GreenSandItemDefinitions.Texture)
      .CreativeTab("general", "*-n")
      .CreativeTab("iwex", "*-n")
      .SideSolid(false)
      .SideOpaque(false)
      .Sound("place", "game:block/ceramicplace")
      .Sound("break", "game:block/ceramic")
      .Sound("hit", "game:block/ceramic")
      .Sound("walk", "game:walk/stone");
```

`.SideVariant()` renders letters (`-n`), which is why the creative-tab selectors read `*-n`.

- [x] **Step 6: Write the block entity**

`BlockEntitySandCastingLongCell` is `BlockEntitySandCastingCell` with three differences, and only
three. Read the 467-line original and copy its structure; do not re-derive the state machine.

1. `Imprint` refuses `MoldSize.Cell` (mirror of U1.1) with `iwex-longcell-wrongsize`.
2. It implements `IFillerInteractionTarget.OnFillerInteractStart`, routing a click on the filler cell to
   the principal — the casting bed does exactly this.
3. The molten renderer takes the cavity boxes in principal-local 16-space spanning `z -12 … 12` —
   negative coordinates. `Cuboidf` handles them, but no existing renderer call site in the mod passes
   any, so verify the glow renders across the whole length rather than assuming it.

If the two entities end up sharing more than ~40 lines verbatim, extract the shared half into a
`CastingStationBase` rather than leaving two copies — the mod's own convention is one API per pattern.

- [x] **Step 7: Run the footprint test and watch it pass**

- [x] **Step 8: Write the second failing test — the launder face**

```csharp
[Theory]
[InlineData("n")]
[InlineData("e")]
[InlineData("s")]
[InlineData("w")]
public void The_long_cell_pulls_from_the_canal_on_its_launder_face(string side)
{
  // LaunderFace collapsed to NORTH for every letter-spelled side until FacingFromSide replaced
  // BlockFacing.FromCode - a silent wrong-wall pull, not an exception. Pinned at all four facings so a
  // regression cannot hide in three of them.
  var rig = LongCellScenes.CompleteWithCanal(side);
  rig.Imprint("castslab");

  rig.RunLive(seconds: 5);

  Assert.True(rig.Principal.Cell!.CellAmount > 0);
}
```

- [x] **Step 9: Implement the intake, run both tests**

- [x] **Step 10: Add the build recipe**

In `CastingRecipeDefinitions`: the casting cell's family at roughly double the size (its own recipe is the
model — six bricks + fire clay + hammer + chisel). Output **`iwex:casting-sandlongcell-fire-n`** — a
concrete, registered code. `IiexRecipeOutputTests` fails if it names nothing.

- [x] **Step 11: Lang keys, block golden (scoped, read it), full 1.21 lane, then 1.20**

- [x] **Step 12: Append a WORKLOG entry**

---

## U1.5 — The cast-part items and their patterns

The eleven drawn `molten-sandcellfilling-*` shapes are the redesigned 1×1 cast-part set. Each is one
`Molds` entry + one output item + one exported filling shape.

**Files:**
- Modify: `src/IronIndustryExpanded/BlockStructures/Casting/PatternItemDefinitions.cs`
- Modify: `src/IronIndustryExpanded/Items/CastPartItemDefinitions.cs`
- Create: `assets/iiex/shapes/casting/cell-filling-*.json` (export from `assets/editable/shapes/`)
- Test: `test/IronIndustryExpanded.Tests/Blocks/Casting/CastPartCatalogueTests.cs`

**Parts:** `castshell`, `castcylinder`, `castwheelpart`, `castrods`, `castshafts`, `gearblanksmall`,
`gearblanklarge`, `castingotmold`, `castplatemold`. `castframe` is U1.3's (it is a long-cell pattern);
`heavyplate`, `moldplate`, `molddoubleingot` and `castbarrel` already ship.

Delivered scope, 2026-08-04 — narrower than the list above, and split across two mods. Shipped:
**`iwex:castwheelsection`** (the list's `castwheelpart`) and **`lpex:castshell`**, both at **600 u**, with
patterns, diagrams, carve recipes, exported runtime shapes, three-language lang and re-blessed goldens in
both suites; the flywheel's rim is now four wheel sections and the large wheel's eight, both grids
diagram-led. Ownership follows consumption — the shell went to lpex because its consumers (water tank,
ore crusher, engines) are all lpex's and iwex had none.

The side effect is the more valuable half: lpex now has a casting bootstrap, built on two new public
factories in iwex (`PatternItemDefinitions.Itemtype`, `DiagramItemDefinitions.Itemtype`). iwex owns the
pattern *system*; each mod contributes *entries*; the casting cell reads the spec off the held pattern, so
neither mod names the other's codes. `patterns.md` Open 3 drops from "lpex has no casting folder at all" to
one `Molds` row per remaining part.

Not shipped, and not deferred by accident: `castcylinder`, both gear blanks, `castrods` and `castshafts`
are machined blanks whose whole point is that they go on to a boring machine — U9's station — so casting
them with nothing to machine them on would ship four more orphans. `castingotmold`/`castplatemold` already
ship as `moldplate` / `molddoubleingot`. See the expansion plan's § *Three further rulings* for the 600-u
argument and the capacity↔mass invariant.

- [x] **Step 1: Write the failing catalogue test**

```csharp
[Fact]
public void Every_pattern_type_has_an_output_that_names_a_real_item_or_block()
{
  // A pattern whose output names nothing is invisible: the cast completes, shake-out yields null and
  // the metal is simply gone. The same class of silent failure as a recipe output naming no block.
  var missing = new List<string>();
  foreach (string type in PatternItemDefinitions.PatternTypes)
  {
    Assert.True(MoldSpec.TryParse(PatternMold(type), out MoldSpec? spec, out string? err), err);
    if (!DefinitionCatalogue.Resolves(spec!.Output))
      missing.Add($"{type} -> {spec.Output.Code}");
  }
  Assert.True(missing.Count == 0, string.Join("\n  ", missing));
}
```

`DefinitionCatalogue.Resolves` does not exist. Build it on `DefinitionCodes.PatternsForDomain` (blocks)
plus the equivalent item expansion — the same wildcard-match approach `RecipeCodes` uses, and for the same
reason: a worldproperty group has states the headless harness cannot enumerate.

- [x] **Step 2: Run it, watch it fail once the new patterns are added, add the items, watch it pass**

- [x] **Step 3: Apply the density rule to all eleven cavities**

Caution: the four previously shipped cavities used four different implicit densities (1.0 / 0.68 / 0.76 /
0.39 u per vx³), and `castplate-heavy`'s 160 u was written at exactly 1 u/vx³. Fixing them retroactively
later means touching every recipe that consumes them. Extend `CastStockMassTests` to cover the part masses
too.

- [x] **Step 4: Lang, goldens (scoped, read), 1.21 lane, 1.20 lane**

- [x] **Step 5: Append a WORKLOG entry**

---

## U1.6 — Acceptance

- [x] **Step 1: Full three-lane run**

```bash
./scripts/exmod.sh test all
```

Expected: 1.20 and 1.21 PASS on all three suites; 1.22 shows only the known ~20 `IPlayer`
`TypeLoadException` failures. Any *other* 1.22 failure is yours.

- [x] **Step 2: Play the gate in a real world**

Load a survival world with only exlib + iwex. Then:
1. Build a cupola, charge it, run it to melting.
2. Run a canal to a long cell. Ram sand, ram up the `castslab` pattern, pour, wait, shake out.
   **Expected: one cast slab.**
3. Ram up `castbillets`. Pour, shake out. **Expected: three billets** (the `Yield` field from U1.3).
4. Run a canal to a 1×1 casting cell, ram up a part pattern, pour, shake out.
   **Expected: the cast part.**
5. Try to ram a `castslab` pattern into the 1×1 cell. **Expected: refused with a message.**

Steps 2–5 cannot be automated and are the only thing that proves the renderer, the interaction routing
and the filler reroute actually work. A green gate is necessary, not sufficient.

- [x] **Step 3: Update the docs**

`docs/design/machines/long-cell.md` — `**Status**` from "art-only — the block does not exist" to `live`,
and delete the "what would have to be built" list. `docs/internal/plans/iwex-bringup.md` — mark its Stage 2 done
(that file still owns the art queue and the playtest gates). `docs/internal/plans/STATE.md` — the ladder diagram's
long-cell row becomes live.

- [x] **Step 4: Append the U1 WORKLOG entry**
