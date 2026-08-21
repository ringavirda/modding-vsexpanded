# Item piles — stage 1: the exlib foundation

**Status** live, written 2026-08-15, nothing started. This plan covers **stage 1 only** — the pure,
headless foundation in exlib. Stage 2 (the reheat hearth adopting it) gets its own plan when this one
lands; its tasks are named at the bottom so the sequencing is visible, and it is deliberately not written
yet because its detail depends on the shapes stage 1 settles.

> **For agentic workers:** REQUIRED SUB-SKILL: use `superpowers:subagent-driven-development` or
> `superpowers:executing-plans`. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal** Build the generic pile-placement computation, the section class it keys on, and the declared
piece size it measures — all pure, all headless, all in exlib, with no consumer required.

**Architecture** Three small pure units, in dependency order. `SectionClass` answers *what shape is this
piece*; `PieceSize` answers *how big is it*; `PileLayout` turns a bed, a piece and an index into a seat.
Nothing touches a world, a block entity or a mesh — that is stage 2. A prerequisite task first decouples
the puddling furnace from the heating hearth's row count, so no later stage can reach the wrong machine.

**Tech Stack** C# / .NET, Vintage Story API (`Vec3f`, `GameMath.MurmurHash3` only), xUnit + NSubstitute.

**Spec** [docs/design/mechanics/item-piles.md](../../design/mechanics/item-piles.md)

## Global Constraints

- ⛔ **Never commit.** The user owns the git history. Leave every change in the working tree and record
  what landed in `docs/internal/worklog/2026-08.md`. No task in this plan ends in `git commit`.
- Test entry point is `scripts/exmod.ps1 test` (PowerShell) or `scripts/exmod.sh test` (bash).
  `scripts/run-tests.sh` **does not exist**; any instruction naming it is stale.
- The gate is 9 targets across 1.20 / 1.21 / 1.22 and must stay green. Run `scripts/exmod.ps1 test all`
  before declaring a task done.
- Comments **describe, never narrate**. No "we now do X", no changelog voice, no AI-flavoured phrasing
  anywhere a reader sees. Match the surrounding files' comment density and idiom.
- New public members carry XML doc comments citing the design page they implement, as every existing
  exlib type does.
- `PieceSize` lives in `ExpandedLib.Processes` (it describes a piece); everything else in this plan lives
  in `ExpandedLib.Blocks.Structures`. The dependency direction is Blocks → Processes, never the reverse.
- Every jitter value is derived from `GameMath.MurmurHash3`. An RNG anywhere in this plan is a defect.

---

## File structure

| File | Responsibility |
|---|---|
| `src/IronIndustryExpanded/BlockStructures/Furnaces/PuddlingHearthLayout.cs` | the puddling furnace's own row count, replacing its borrow of the heating hearth's |
| `src/ExpandedLib/Processes/SectionClass.cs` | the `Square \| Flat` enum, the family registry, and the resolution order |
| `src/ExpandedLib/Processes/PieceSize.cs` | the declared `(Width, Height, Length)` triple and its JSON parse |
| `src/ExpandedLib/Blocks/Structures/PileLayout.cs` | bed, mode, capacity, seat, jitter |
| `test/ExpandedLib.Tests/Processes/SectionClassTests.cs` | resolution order, registry contribution, defaults |
| `test/ExpandedLib.Tests/Processes/PieceSizeTests.cs` | parse, validation, absence |
| `test/ExpandedLib.Tests/Blocks/PileLayoutTests.cs` | per-layer, capacity, seats, jitter reproducibility |
| `test/IronIndustryExpanded.Tests/Blocks/PuddlingHearthLayoutTests.cs` | the decoupling, asserted |

---

## Task 1: Decouple the puddling furnace from the heating hearth's row count

`BlockEntityPuddlingHearth` sizes `_pigs` and `_fettled` off `HeatingHearthLayout.Rows`
(`BlockEntityPuddlingHearth.cs:21-22`), so any later change to the heating hearth's contents model
silently resizes the puddling furnace's state. The owner ruled 2026-08-15 that the puddling furnace is a
separate structure with different logic, nearer the crucible furnace's hearth than this one. Breaking the
borrow first makes every later task incapable of reaching it.

**Files:**
- Create: `src/IronIndustryExpanded/BlockStructures/Furnaces/PuddlingHearthLayout.cs`
- Modify: `src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingHearth.cs:21-22`
- Test: `test/IronIndustryExpanded.Tests/Blocks/PuddlingHearthLayoutTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `PuddlingHearthLayout.Rows` (`const int`, value 3).

- [ ] **Step 1: Write the failing test**

```csharp
// test/IronIndustryExpanded.Tests/Blocks/PuddlingHearthLayoutTests.cs
public class PuddlingHearthLayoutTests {
  [Fact]
  public void The_puddling_furnace_states_its_own_row_count() {
    Assert.Equal(3, PuddlingHearthLayout.Rows);
  }

  // The premise, written as an assertion: the two machines agree on 3 today and are free to diverge.
  // Reading this as "they must match" inverts it - it exists so a later change to one cannot move the
  // other without a test saying so out loud.
  [Fact]
  public void The_two_hearths_size_themselves_independently() {
    Assert.Equal(
      HeatingHearthLayout.Rows,
      PuddlingHearthLayout.Rows
    );
  }
}
```

- [ ] **Step 2: Run it and confirm it fails**

Run: `scripts/exmod.ps1 test iiex`
Expected: FAIL — `PuddlingHearthLayout` does not exist (CS0103).

- [ ] **Step 3: Write the layout**

```csharp
// src/IronIndustryExpanded/BlockStructures/Furnaces/PuddlingHearthLayout.cs
namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>
/// What a puddling furnace's hearth is sized to. Separate from the reheat furnace's
/// <see cref="HeatingHearthLayout"/> even though both are three rows today: the two are different
/// structures with different logic, and the puddling bed is nearer the crucible furnace's than the reheat
/// bed it shares a chassis with. Borrowing the other machine's constant made a change to either resize
/// both. See docs/design/machines/puddling-furnace.md.
/// </summary>
public static class PuddlingHearthLayout {
  /// <summary>Rows the puddling bed works, one charge each.</summary>
  public const int Rows = 3;
}
```

- [ ] **Step 4: Repoint the block entity**

In `BlockEntityPuddlingHearth.cs`, change the two array sizings only. `HearthRows.Row` usages stay as they
are — this task moves the *count*, not the row model.

```csharp
  private readonly int[] _pigs = new int[PuddlingHearthLayout.Rows];
  private readonly bool[] _fettled = new bool[PuddlingHearthLayout.Rows];
```

- [ ] **Step 5: Confirm nothing else in the puddling furnace reads the heating hearth**

Run: `grep -rn "HeatingHearthLayout" src/IronIndustryExpanded/BlockStructures/Furnaces/BlockEntities/BlockEntityPuddlingHearth.cs`
Expected: no output. If any remains, repoint it the same way.

- [ ] **Step 6: Run the full gate**

Run: `scripts/exmod.ps1 test all`
Expected: PASS, 9 targets. The puddling furnace's own scenario tests are the real check here — they
exercise the arrays this task resized.

- [ ] **Step 7: Mutation-check the decoupling**

Temporarily set `PuddlingHearthLayout.Rows = 2`, run `scripts/exmod.ps1 test iiex`, and confirm the
puddling furnace's tests fail while the *heating* hearth's tests still pass. That is the proof the two are
independent. Restore to 3.

- [ ] **Step 8: Record**

Append to `docs/internal/worklog/2026-08.md` under a new dated heading. Do not commit.

---

## Task 2: The section class

One property with two consumers: it chooses the deformation law
([rolling](../../design/processes/rolling.md)) and it chooses the pile's shape. This task builds only the
property and its resolution; no consumer is wired.

**Files:**
- Create: `src/ExpandedLib/Processes/SectionClass.cs`
- Test: `test/ExpandedLib.Tests/Processes/SectionClassTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum SectionClass { Square, Flat }`
  - `SectionClasses.AttributeKey` → `"sectionClass"`
  - `SectionClasses.Register(string family, SectionClass section)`
  - `SectionClasses.ForFamily(string? family)` → `SectionClass?`
  - `SectionClasses.Parse(string? declared)` → `SectionClass?`
  - `SectionClasses.Of(string? family, string? declared)` → `SectionClass`
  - `SectionClasses.SeedDefaults()`

- [ ] **Step 1: Write the failing tests**

```csharp
// test/ExpandedLib.Tests/Processes/SectionClassTests.cs
public class SectionClassTests {
  public SectionClassTests() => SectionClasses.SeedDefaults();

  #region Defaults

  [Theory]
  [InlineData("grooved", SectionClass.Square)]
  [InlineData("flat", SectionClass.Flat)]
  [InlineData("flatwide", SectionClass.Flat)]
  public void The_shipped_families_carry_their_section(string family, SectionClass expected) =>
    Assert.Equal(expected, SectionClasses.ForFamily(family));

  [Fact]
  public void An_unregistered_family_answers_nothing() =>
    Assert.Null(SectionClasses.ForFamily("hexagonal"));

  #endregion

  #region Resolution order

  // The family wins: a piece is whatever shape the last set left it in, however it started.
  [Fact]
  public void The_rolling_family_outranks_the_items_own_declaration() =>
    Assert.Equal(SectionClass.Square, SectionClasses.Of("grooved", "flat"));

  // A product carries no work piece and so names no family.
  [Fact]
  public void An_item_with_no_family_falls_back_to_its_declaration() =>
    Assert.Equal(SectionClass.Flat, SectionClasses.Of(null, "flat"));

  [Fact]
  public void An_unknown_family_falls_through_to_the_declaration() =>
    Assert.Equal(SectionClass.Square, SectionClasses.Of("hexagonal", "square"));

  // Flat is the safe default: a flat pile of square pieces looks careless, a pyramid of flat pieces floats.
  [Fact]
  public void Nothing_declared_anywhere_reads_as_flat() =>
    Assert.Equal(SectionClass.Flat, SectionClasses.Of(null, null));

  #endregion

  #region Contribution

  [Fact]
  public void A_mod_registers_its_own_family() {
    SectionClasses.Register("hexagonal", SectionClass.Square);
    Assert.Equal(SectionClass.Square, SectionClasses.ForFamily("hexagonal"));
  }

  [Fact]
  public void Seeding_defaults_restores_an_overridden_family() {
    SectionClasses.Register("grooved", SectionClass.Flat);
    SectionClasses.SeedDefaults();
    Assert.Equal(SectionClass.Square, SectionClasses.ForFamily("grooved"));
  }

  [Theory]
  [InlineData("SQUARE", SectionClass.Square)]
  [InlineData("  flat  ", SectionClass.Flat)]
  public void A_declaration_parses_case_and_space_insensitively(string declared, SectionClass expected) =>
    Assert.Equal(expected, SectionClasses.Parse(declared));

  [Fact]
  public void An_unreadable_declaration_parses_as_nothing() =>
    Assert.Null(SectionClasses.Parse("triangular"));

  #endregion
}
```

- [ ] **Step 2: Run them and confirm they fail**

Run: `scripts/exmod.ps1 test exlib`
Expected: FAIL — `SectionClass` / `SectionClasses` do not exist.

- [ ] **Step 3: Write the implementation**

```csharp
// src/ExpandedLib/Processes/SectionClass.cs
using System;
using System.Collections.Generic;

namespace ExpandedLib.Processes;

/// <summary>The shape of a piece's cross-section, which decides both how rolling deforms it and how it
/// piles. See docs/design/processes/rolling.md and docs/design/mechanics/item-piles.md.</summary>
public enum SectionClass {
  /// <summary>Square: the groove refuses the spread, so the piece runs out lengthways and nests when
  /// piled.</summary>
  Square,

  /// <summary>Flat: the reduction goes into width until the barrel caps it, and the piece stacks.</summary>
  Flat,
}

/// <summary>
/// Which section class a piece is in. A contributed-to registry keyed on the rolling family, plus the
/// resolution order a caller uses for an arbitrary item: the family that last worked the piece, then the
/// item's own declaration, then flat.
/// See docs/design/mechanics/item-piles.md § The mode is the section class.
/// </summary>
public static class SectionClasses {
  /// <summary>The attribute an item declares its own section under.</summary>
  public const string AttributeKey = "sectionClass";

  private static readonly Dictionary<string, SectionClass> _byFamily = new(
    StringComparer.OrdinalIgnoreCase
  );

  static SectionClasses() => SeedDefaults();

  /// <summary>Registers (or replaces) <paramref name="family"/>'s section. A mod's own roller family
  /// piles correctly without an exlib edit.</summary>
  public static void Register(string family, SectionClass section) {
    if (!string.IsNullOrWhiteSpace(family))
      _byFamily[family] = section;
  }

  /// <summary>The section <paramref name="family"/> leaves a piece in, or null when none is
  /// registered.</summary>
  public static SectionClass? ForFamily(string? family) =>
    family != null && _byFamily.TryGetValue(family, out SectionClass section)
      ? section
      : null;

  /// <summary>Reads a declared section, or null when the text names none. An unreadable value is not an
  /// error: the caller falls through to its next source.</summary>
  public static SectionClass? Parse(string? declared) =>
    declared?.Trim().ToLowerInvariant() switch {
      "square" => SectionClass.Square,
      "flat" => SectionClass.Flat,
      _ => null,
    };

  /// <summary>
  /// The section a piece is in, taking the first source that answers: the family that last worked it,
  /// then its own declaration, then <see cref="SectionClass.Flat"/>.
  /// </summary>
  /// <remarks>
  /// The family outranks the item because a piece is whatever shape the last set left it in - a bar taken
  /// grooved is square however it started. Flat is the fallback because its failure is the quieter one: a
  /// flat pile of square pieces reads as careless, where a pyramid of flat pieces floats and reads as a
  /// bug.
  /// </remarks>
  public static SectionClass Of(string? family, string? declared) =>
    ForFamily(family) ?? Parse(declared) ?? SectionClass.Flat;

  /// <summary>Puts the shipped families back, replacing any override of them.</summary>
  public static void SeedDefaults() {
    _byFamily.Clear();
    Register("grooved", SectionClass.Square);
    Register("flat", SectionClass.Flat);
    Register("flatwide", SectionClass.Flat);
  }
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `scripts/exmod.ps1 test exlib`
Expected: PASS.

- [ ] **Step 5: Mutation-check the resolution order**

Change `Of` to `Parse(declared) ?? ForFamily(family) ?? SectionClass.Flat` and confirm
`The_rolling_family_outranks_the_items_own_declaration` fails. Restore. If it still passes, the test is
not testing the order and must be fixed before moving on.

- [ ] **Step 6: Run the full gate**

Run: `scripts/exmod.ps1 test all`
Expected: PASS, 9 targets.

- [ ] **Step 7: Record**

Append to the worklog. Do not commit.

---

## Task 3: The declared piece size

The pile needs a width, a height and a length before anything is drawn — `TryLoad` has to know whether a
piece fits without tesselating it — so the size is declared, never measured off a shape's bounding box.

**Files:**
- Create: `src/ExpandedLib/Processes/PieceSize.cs`
- Test: `test/ExpandedLib.Tests/Processes/PieceSizeTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `readonly record struct PieceSize(float Width, float Height, float Length)`
  - `PieceSize.AttributeKey` → `"pieceSize"`
  - `PieceSize.TryParse(JsonObject? node, out PieceSize size, out string? error)` — reads a
    three-number array
  - `PieceSize.IsEmpty`

- [ ] **Step 1: Write the failing tests**

```csharp
// test/ExpandedLib.Tests/Processes/PieceSizeTests.cs
public class PieceSizeTests {
  private static JsonObject Json(string raw) => new(JToken.Parse(raw));

  [Fact]
  public void A_three_number_array_reads_as_width_height_length() {
    Assert.True(PieceSize.TryParse(Json("[4.5, 2, 18]"), out PieceSize size, out _));
    Assert.Equal(4.5f, size.Width);
    Assert.Equal(2f, size.Height);
    Assert.Equal(18f, size.Length);
  }

  [Theory]
  [InlineData("[4.5, 2]")]
  [InlineData("[4.5, 2, 18, 9]")]
  public void An_array_of_the_wrong_length_is_refused(string raw) {
    Assert.False(PieceSize.TryParse(Json(raw), out _, out string? error));
    Assert.Contains("three", error);
  }

  // A zero dimension divides by zero in PerLayer, so it fails at load rather than at a bed.
  [Theory]
  [InlineData("[0, 2, 18]")]
  [InlineData("[4.5, -2, 18]")]
  public void A_non_positive_dimension_is_refused(string raw) {
    Assert.False(PieceSize.TryParse(Json(raw), out _, out string? error));
    Assert.Contains("positive", error);
  }

  // Absence is not an error: a stage or item that declares no size simply has none, and the caller
  // decides whether that matters.
  [Fact]
  public void An_absent_declaration_reads_as_empty_without_an_error() {
    Assert.True(PieceSize.TryParse(null, out PieceSize size, out string? error));
    Assert.True(size.IsEmpty);
    Assert.Null(error);
  }
}
```

- [ ] **Step 2: Run them and confirm they fail**

Run: `scripts/exmod.ps1 test exlib`
Expected: FAIL — `PieceSize` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
// src/ExpandedLib/Processes/PieceSize.cs
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Processes;

/// <summary>
/// How big one piece is, in voxels, as declared. Taken from the declaration rather than measured off the
/// drawn shape: a shape's bounding box exists only once the shape is loaded, and capacity has to be
/// answerable before a bed is ever drawn.
/// See docs/design/mechanics/item-piles.md § Piece size is declared, not measured.
/// </summary>
/// <param name="Width">Across the bed - the divisor in a layer's count.</param>
/// <param name="Height">The pile's step per layer.</param>
/// <param name="Length">Along the bed - what the bed's own length has to accommodate.</param>
public readonly record struct PieceSize(float Width, float Height, float Length) {
  /// <summary>The attribute an item declares its size under, as <c>[width, height, length]</c>.</summary>
  public const string AttributeKey = "pieceSize";

  /// <summary>Whether nothing was declared. A caller decides whether that is a fault.</summary>
  public bool IsEmpty => Width <= 0f || Height <= 0f || Length <= 0f;

  /// <summary>
  /// Reads a <c>[width, height, length]</c> array. An absent node is not an error and reads as empty; a
  /// malformed one fails at load, so a bad declaration is reported rather than dividing by zero at a bed.
  /// </summary>
  public static bool TryParse(
    JsonObject? node,
    out PieceSize size,
    out string? error
  ) {
    size = default;
    error = null;

    if (node is not { Exists: true })
      return true;

    float[] v = node.AsArray<float>([]) ?? [];
    if (v.Length != 3) {
      error =
        $"'{AttributeKey}' takes three numbers - width, height, length - and was given {v.Length}";
      return false;
    }
    if (v[0] <= 0f || v[1] <= 0f || v[2] <= 0f) {
      error =
        $"'{AttributeKey}' needs every dimension positive; [{v[0]}, {v[1]}, {v[2]}] has one that is not";
      return false;
    }

    size = new PieceSize(v[0], v[1], v[2]);
    return true;
  }
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `scripts/exmod.ps1 test exlib`
Expected: PASS.

- [ ] **Step 5: Run the full gate and record**

Run: `scripts/exmod.ps1 test all` — 9 targets green. Append to the worklog. Do not commit.

---

## Task 4: `PileLayout` — capacity

Capacity before seats, because `PerLayer` is the divisor every seat depends on and it is worth pinning
alone.

**Files:**
- Create: `src/ExpandedLib/Blocks/Structures/PileLayout.cs`
- Test: `test/ExpandedLib.Tests/Blocks/PileLayoutTests.cs`

**Interfaces:**
- Consumes: `SectionClass` (Task 2), `PieceSize` (Task 3).
- Produces:
  - `enum PileMode { Pyramid, Flat }`
  - `readonly record struct PileBed(float Width, float Length, int Layers)`
  - `PileLayout.ModeFor(SectionClass section)` → `PileMode`
  - `PileLayout.PerLayer(PileBed bed, PieceSize piece)` → `int`
  - `PileLayout.Capacity(PileBed bed, PieceSize piece, PileMode mode)` → `int`

- [ ] **Step 1: Write the failing tests**

```csharp
// test/ExpandedLib.Tests/Blocks/PileLayoutTests.cs
public class PileLayoutTests {
  // One cell across, two deep - the reheat hearth's lengthwise bed.
  private static readonly PileBed Hearth = new(16f, 32f, 3);
  private static readonly PieceSize Bar = new(4f, 3f, 18f);
  private static readonly PieceSize Slab = new(15f, 2f, 32f);

  #region Mode

  [Theory]
  [InlineData(SectionClass.Square, PileMode.Pyramid)]
  [InlineData(SectionClass.Flat, PileMode.Flat)]
  public void The_section_class_chooses_the_mode(SectionClass section, PileMode expected) =>
    Assert.Equal(expected, PileLayout.ModeFor(section));

  #endregion

  #region Per layer

  [Fact]
  public void A_layer_holds_as_many_as_fit_across() =>
    Assert.Equal(4, PileLayout.PerLayer(Hearth, Bar)); // 16 / 4

  [Fact]
  public void A_piece_as_wide_as_the_bed_gets_one_per_layer() =>
    Assert.Equal(1, PileLayout.PerLayer(Hearth, Slab)); // 16 / 15 -> 1

  // A piece wider than the bed still seats: refusing it is the container's rule, not the pile's.
  [Fact]
  public void A_piece_wider_than_the_bed_still_gets_one() =>
    Assert.Equal(1, PileLayout.PerLayer(Hearth, new PieceSize(20f, 2f, 10f)));

  #endregion

  #region Capacity

  // 4 + 3 + 2 across three layers.
  [Fact]
  public void A_pyramid_loses_one_per_layer() =>
    Assert.Equal(9, PileLayout.Capacity(Hearth, Bar, PileMode.Pyramid));

  // The pyramid runs out of pieces before it runs out of layers: 2 + 1 and then nothing.
  [Fact]
  public void A_pyramid_stops_when_a_layer_would_be_empty() =>
    Assert.Equal(3, PileLayout.Capacity(new PileBed(16f, 32f, 5), new PieceSize(8f, 3f, 18f), PileMode.Pyramid));

  [Fact]
  public void A_flat_pile_is_one_per_layer_however_wide_the_bed() =>
    Assert.Equal(3, PileLayout.Capacity(Hearth, Bar, PileMode.Flat));

  // Capacity is a function of the piece, not a constant of the container.
  [Fact]
  public void A_bed_holds_more_narrow_pieces_than_wide_ones() =>
    Assert.True(
      PileLayout.Capacity(Hearth, Bar, PileMode.Pyramid)
        > PileLayout.Capacity(Hearth, Slab, PileMode.Pyramid)
    );

  #endregion
}
```

- [ ] **Step 2: Run them and confirm they fail**

Run: `scripts/exmod.ps1 test exlib`
Expected: FAIL — `PileLayout` does not exist.

- [ ] **Step 3: Write the types and the capacity half**

```csharp
// src/ExpandedLib/Blocks/Structures/PileLayout.cs
using System;
using ExpandedLib.Processes;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Structures;

/// <summary>How a bed's pieces are arranged. Not an axis of its own - it is the section class.</summary>
public enum PileMode {
  /// <summary>Square sections nest: each layer holds one fewer, offset half a width and dropped to the
  /// valley between the two below.</summary>
  Pyramid,

  /// <summary>Flat sections stack: one per layer, a full height apart, turned slightly.</summary>
  Flat,
}

/// <summary>A container's bed, in voxels, in its own frame.</summary>
/// <param name="Width">Across the bed.</param>
/// <param name="Length">Along the bed.</param>
/// <param name="Layers">How tall a pile it will take.</param>
public readonly record struct PileBed(float Width, float Length, int Layers);

/// <summary>
/// Where each piece of a pile sits. Pure arithmetic - no world, no mesh, no block entity - so the whole
/// rule set is pinnable headless, and one implementation serves the reheat hearth's bed and the stock
/// rack's shelf alike. See docs/design/mechanics/item-piles.md.
/// </summary>
public static class PileLayout {
  /// <summary>Drop per layer of a nesting pile, as a multiple of piece height: <c>sqrt(3)/2</c>, the
  /// height of an equilateral triangle of unit side, which is how far a piece settles into the valley
  /// between the two below it. A full height instead leaves the upper layer visibly floating.</summary>
  public const float PyramidYStep = 0.866f;

  /// <summary>Step per layer of a stacked pile - a full piece height, because nothing nests.</summary>
  public const float FlatYStep = 1.0f;

  /// <summary>The mode <paramref name="section"/> piles in.</summary>
  public static PileMode ModeFor(SectionClass section) =>
    section == SectionClass.Square ? PileMode.Pyramid : PileMode.Flat;

  /// <summary>How many pieces fit across one layer. At least one: a piece wider than the bed still
  /// seats, since refusing it is the container's rule rather than the pile's.</summary>
  public static int PerLayer(PileBed bed, PieceSize piece) =>
    piece.Width <= 0f
      ? 1
      : Math.Max(1, (int)MathF.Floor(bed.Width / piece.Width));

  /// <summary>
  /// How many pieces the bed holds in total. A pyramid loses one per layer and stops when a layer would
  /// be empty, whichever comes first; a flat pile is one per layer.
  /// </summary>
  public static int Capacity(PileBed bed, PieceSize piece, PileMode mode) {
    if (bed.Layers <= 0)
      return 0;
    if (mode == PileMode.Flat)
      return bed.Layers;

    int perLayer = PerLayer(bed, piece);
    int total = 0;
    for (int layer = 0; layer < bed.Layers; layer++) {
      int inLayer = perLayer - layer;
      if (inLayer <= 0)
        break;
      total += inLayer;
    }
    return total;
  }
}
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `scripts/exmod.ps1 test exlib`
Expected: PASS.

- [ ] **Step 5: Run the full gate and record**

Run: `scripts/exmod.ps1 test all`. Append to the worklog. Do not commit.

---

## Task 5: `PileLayout` — seats and jitter

**Files:**
- Modify: `src/ExpandedLib/Blocks/Structures/PileLayout.cs`
- Test: `test/ExpandedLib.Tests/Blocks/PileLayoutTests.cs`

**Interfaces:**
- Consumes: everything from Task 4.
- Produces:
  - `readonly record struct PileSeat(Vec3f Offset, float Yaw)` — `Yaw` in **radians**, matching
    `MeshData.Rotate`
  - `PileLayout.Seat(PileBed bed, PieceSize piece, PileMode mode, int index, int seed)` → `PileSeat`
  - `PileLayout.JitterXZ` (voxels), `PileLayout.JitterYaw` (radians) — both playtest knobs

- [ ] **Step 1: Write the failing tests**

```csharp
  #region Seats

  [Fact]
  public void The_first_piece_sits_on_the_bed() =>
    Assert.Equal(0f, PileLayout.Seat(Hearth, Bar, PileMode.Pyramid, 0, 1).Offset.Y, 3);

  [Fact]
  public void A_pyramids_second_piece_sits_beside_the_first() {
    PileSeat a = PileLayout.Seat(Hearth, Bar, PileMode.Pyramid, 0, 1);
    PileSeat b = PileLayout.Seat(Hearth, Bar, PileMode.Pyramid, 1, 1);
    Assert.Equal(0f, b.Offset.Y, 3);                          // same layer
    Assert.Equal(Bar.Width, b.Offset.X - a.Offset.X, 1);      // one width along
  }

  // Layer 1 starts after the 4 of layer 0, is offset half a width, and drops 0.866 of a height.
  [Fact]
  public void A_pyramids_next_layer_is_offset_half_a_width_and_stepped_by_0866() {
    PileSeat first = PileLayout.Seat(Hearth, Bar, PileMode.Pyramid, 0, 1);
    PileSeat up = PileLayout.Seat(Hearth, Bar, PileMode.Pyramid, 4, 1);
    Assert.Equal(PileLayout.PyramidYStep * Bar.Height, up.Offset.Y, 3);
    Assert.Equal(0.5f * Bar.Width, up.Offset.X - first.Offset.X, 1);
  }

  [Fact]
  public void A_flat_pile_steps_a_full_height_every_piece() {
    PileSeat a = PileLayout.Seat(Hearth, Slab, PileMode.Flat, 0, 1);
    PileSeat b = PileLayout.Seat(Hearth, Slab, PileMode.Flat, 1, 1);
    Assert.Equal(Slab.Height, b.Offset.Y - a.Offset.Y, 3);
  }

  // Nesting is the point of a pyramid, and turning a piece breaks it.
  [Fact]
  public void A_pyramid_takes_no_yaw() =>
    Assert.Equal(0f, PileLayout.Seat(Hearth, Bar, PileMode.Pyramid, 2, 7).Yaw, 5);

  [Fact]
  public void A_flat_pile_turns_each_piece_a_little() =>
    Assert.NotEqual(0f, PileLayout.Seat(Hearth, Slab, PileMode.Flat, 2, 7).Yaw, 5);

  #endregion

  #region Jitter

  [Fact]
  public void The_same_seat_is_the_same_every_call() =>
    Assert.Equal(
      PileLayout.Seat(Hearth, Bar, PileMode.Pyramid, 3, 42),
      PileLayout.Seat(Hearth, Bar, PileMode.Pyramid, 3, 42)
    );

  // Two containers a block apart must not pile identically.
  [Fact]
  public void Two_seeds_give_different_jitter() =>
    Assert.NotEqual(
      PileLayout.Seat(Hearth, Bar, PileMode.Pyramid, 3, 42),
      PileLayout.Seat(Hearth, Bar, PileMode.Pyramid, 3, 43)
    );

  [Fact]
  public void Jitter_never_moves_a_piece_further_than_its_bound() {
    for (int i = 0; i < 9; i++)
      for (int seed = 0; seed < 64; seed++) {
        PileSeat seat = PileLayout.Seat(Hearth, Bar, PileMode.Pyramid, i, seed);
        PileSeat clean = PileLayout.Seat(Hearth, Bar, PileMode.Pyramid, i, 0);
        Assert.True(MathF.Abs(seat.Offset.X - clean.Offset.X) <= 2f * PileLayout.JitterXZ);
        Assert.True(MathF.Abs(seat.Offset.Z - clean.Offset.Z) <= 2f * PileLayout.JitterXZ);
      }
  }

  #endregion
```

- [ ] **Step 2: Run them and confirm they fail**

Run: `scripts/exmod.ps1 test exlib`
Expected: FAIL — `PileSeat` / `Seat` do not exist.

- [ ] **Step 3: Implement seats and jitter**

```csharp
/// <summary>Where one piece sits, relative to the bed's own origin corner.</summary>
/// <param name="Offset">Voxels along, up and across the bed.</param>
/// <param name="Yaw">Turn about the vertical, in radians, matching <c>MeshData.Rotate</c>.</param>
public readonly record struct PileSeat(Vec3f Offset, float Yaw);
```

added to `PileLayout`:

```csharp
  /// <summary>Bound on how far hand placement moves a piece from its nominal seat, in voxels. A playtest
  /// knob.</summary>
  public const float JitterXZ = 0.25f;

  /// <summary>Bound on how far a stacked piece is turned, in radians. A playtest knob; pyramids take
  /// none.</summary>
  public const float JitterYaw = 0.07f;

  /// <summary>
  /// Where the <paramref name="index"/>-th piece of a pile sits. A flat index rather than a
  /// (layer, slot) pair: a caller has n pieces and wants the i-th seated, and which layer that lands in
  /// is this function's arithmetic rather than the caller's.
  /// </summary>
  /// <param name="seed">Stable per container - the block position - so one pile never moves and two
  /// containers differ. Never an RNG: re-tesselation happens for reasons unrelated to the pile, and a
  /// rolled value makes it jump every time.</param>
  public static PileSeat Seat(
    PileBed bed,
    PieceSize piece,
    PileMode mode,
    int index,
    int seed
  ) {
    (int layer, int slot) = mode == PileMode.Flat
      ? (Math.Max(0, index), 0)
      : PyramidPlace(PerLayer(bed, piece), Math.Max(0, index));

    float x = mode == PileMode.Flat
      ? 0f
      : (slot + 0.5f * layer) * piece.Width;
    float y =
      layer * piece.Height * (mode == PileMode.Flat ? FlatYStep : PyramidYStep);
    float z = 0f;

    return new PileSeat(
      new Vec3f(
        x + Jitter(seed, layer, slot, 0) * JitterXZ,
        y,
        z + Jitter(seed, layer, slot, 1) * JitterXZ
      ),
      // A pyramid takes no yaw: the nesting is the point, and turning a piece breaks it.
      mode == PileMode.Flat ? Jitter(seed, layer, slot, 2) * JitterYaw : 0f
    );
  }

  // Which layer the flat index falls in, and where along it. Layer 0 holds perLayer, layer 1 one fewer,
  // and so on; an index past the last non-empty layer piles on top of it rather than throwing, because a
  // caller that over-fills is the container's fault to report and not this function's to crash on.
  private static (int Layer, int Slot) PyramidPlace(int perLayer, int index) {
    int layer = 0;
    int remaining = index;
    while (perLayer - layer > 0 && remaining >= perLayer - layer) {
      remaining -= perLayer - layer;
      layer++;
    }
    return (layer, remaining);
  }

  // Reproducible by construction. MurmurHash3 takes three ints, so the axis is folded into the third
  // alongside the slot rather than needing a fourth.
  private static float Jitter(int seed, int layer, int slot, int axis) =>
    GameMath.MurmurHash3(seed, layer, slot * 4 + axis) / (float)int.MaxValue;
```

- [ ] **Step 4: Run the tests and confirm they pass**

Run: `scripts/exmod.ps1 test exlib`
Expected: PASS. If `Jitter_never_moves_a_piece_further_than_its_bound` fails, `MurmurHash3` returned a
negative below `-int.MaxValue`; clamp the division rather than widening the bound.

- [ ] **Step 5: Mutation-check the two numbers that carry the look**

Change `PyramidYStep` to `1.0f` and confirm
`A_pyramids_next_layer_is_offset_half_a_width_and_stepped_by_0866` fails. Change the pyramid's x term
from `0.5f * layer` to `0f` and confirm the same test fails on its other assertion. Restore both. These
are the two values that make a pile read as stacked rather than placed, and a test that survives either
change is not testing them.

- [ ] **Step 6: Mutation-check reproducibility**

Replace `Jitter` with `Random.Shared.NextSingle()` and confirm `The_same_seat_is_the_same_every_call`
fails. Restore. This is the defect the whole seeded design exists to prevent.

- [ ] **Step 7: Run the full gate and record**

Run: `scripts/exmod.ps1 test all` — 9 targets green. Append to `docs/internal/worklog/2026-08.md`.
Update [item-piles.md](../../design/mechanics/item-piles.md)'s **Status** line: the foundation is built,
the consumers are not. Do not commit.

---

## Self-review

**Spec coverage.** The spec's *two modes*, *mode is the section class*, *piece size is declared*, *jitter*,
*capacity* and *the interface* sections all have tasks. Its *bed* table, *Code* table and every *Gotcha*
belong to stage 2 — they are about tesselation, contents models and the hearth's own furniture, none of
which this plan touches. The spec's **Open** items stay open: mixing classes in one bed, the rack's
`allowAttach` flag, and break behaviour are all container rules rather than pile rules.

**Deliberately not covered here.** The `sectionClass` and `pieceSize` attributes are *parsed* by this plan
and *declared by nothing*. Wiring them onto `ProcessStage`, `RollSetSpec` and the item definitions is
stage 2's first task, because the values only matter once something reads them, and putting them on
shipped items now would ship a number no test could hold to anything.

**Type consistency.** `PileBed`, `PieceSize`, `PileMode`, `PileSeat`, `SectionClass`, `PerLayer`,
`Capacity`, `ModeFor`, `Seat`, `PyramidYStep`, `FlatYStep`, `JitterXZ`, `JitterYaw` are spelled the same
in every task and in the spec's interface table.

---

## Stage 2, when this lands

Named so the sequencing is visible; each gets written as its own plan against the shape stage 1 settles.

1. **Declare the attributes** — `ProcessStage.Size`, `RollSetSpec.Section`, `sectionClass` / `pieceSize`
   on the item definitions, and the roll sets registering their families. First, because everything below
   reads them.
2. **The hearth's accepts table** — a `config/hearthbeds/` catalogue keyed by machine, in the
   `ProcessJobLoader` / `ProcessJobRegistry` idiom, declaring item → seating → layers. Retires
   `HeatingHearthLayout.StockOf`'s code-prefix matching, which is what made every cast piece unreheatable
   until 2026-08-14. Its design belongs on [reheat-furnace.md](../../design/machines/reheat-furnace.md),
   not on the pile page.
3. **The composed-stock renderer** — the hearth tesselates each piece from the item's own shape through
   `PileLayout.Seat`. Deletes the 15 authored groups in
   `assets/editable/shapes/furnaces/firebox/furnace-megablock-heatinghearth.json` and
   `HeatingHearthLayout`'s element map with them, and with them the `Items1/3/2` ordering trap.
   Carries all five documented traps: five-arg `ShapeTextureSource`, main-thread-only atlas insert
   against a chunk-worker `OnTesselation`, cache the clone not the seat, translate before the single
   final `ExMesh.RotateByShape`, and `ResolveBlockOrItem` after reading a stack off the tree.
4. **Crosswise seating** — one contents model with a mode tag replacing `ItemStack?[3]`, the two seatings
   mutually exclusive, LIFO falling out of reachability. Task 1 of this plan is what makes it safe.
5. **The stock rack** — blocked on art; the owner is providing the shape. It is the second consumer, and
   the reason `PileLayout` is in exlib rather than in iiex.
