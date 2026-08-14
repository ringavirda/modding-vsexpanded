using System;
using System.Collections.Generic;
using System.Linq;

namespace IronIndustryExpanded.BlockStructures.Casting;

/// <summary>Where a slot sits across the bed - the two mold flanks and the runner spine between them.</summary>
public enum BedSlotSide {
  West,
  Centre,
  East,
}

/// <summary>
/// What a bed slot currently is. <see cref="Sand"/> covers both never-carved and shaken-out: the art draws
/// one <c>Full</c> element per slot for both, and either is re-carved the same way.
/// </summary>
public enum BedSlotState {
  /// <summary>Full, flat sand - never carved, or carved and since shaken out.</summary>
  Sand,

  /// <summary>A carved runner channel. Runner slots only.</summary>
  Runner,

  /// <summary>
  /// A carved row of impressions. One shape serves both castings: iron hardens into pigs, slag into slag
  /// bricks.
  /// </summary>
  Mold,
}

/// <summary>One carvable slot: which row it belongs to, and where across that row it sits.</summary>
/// <param name="Row">1-4, front to back. Row 1 holds the pour basin at its centre.</param>
public readonly record struct BedSlot(int Row, BedSlotSide Side) {
  /// <summary>A centre slot is runner spine; the flanks are molds.</summary>
  public bool IsMold => Side != BedSlotSide.Centre;

  /// <summary>Whether this slot can be carved into <paramref name="state"/> at all.</summary>
  public bool Accepts(BedSlotState state) =>
    state switch {
      BedSlotState.Sand => true,
      BedSlotState.Runner => !IsMold,
      BedSlotState.Mold => IsMold,
      _ => false,
    };
}

/// <summary>
/// Maps the sand casting bed's slot states to shape elements. Every one of the twelve slots renders exactly
/// one element, so the bed's whole appearance is twelve choices and the mesh can be rebuilt from
/// block-entity state instead of needing a block variant per combination.
/// <para>
/// Paths follow VS's hierarchical <c>SelectiveElements</c> matcher: one <c>&lt;path&gt;/*</c> per element
/// that should render, and never an ancestor on its own - ancestors render as prefixes of the deeper
/// entries, while naming one exactly drops all of its children.
/// </para>
/// </summary>
public static class SandBedLayout {
  /// <summary>
  /// The group holding every carvable element, added by the last construction stage. The construction
  /// behaviour can only name the group whole, which would draw every state of every slot at once, so
  /// <see cref="Compose"/> replaces it with one entry per slot.
  /// </summary>
  public const string RunnersGroup = "SandRunners";

  /// <summary>The front row, whose centre cell is the principal (the pour basin).</summary>
  public const int FirstRow = 1;

  /// <summary>How many rows deep the bed runs. Every row carries molds; the basin shares row 1 with them.</summary>
  public const int Rows = 4;

  /// <summary>The last row, at the far end from the basin.</summary>
  public const int LastRow = FirstRow + Rows - 1;

  /// <summary>Every slot on the bed, in a stable order - the order slot states are persisted in.</summary>
  public static readonly BedSlot[] Slots =
  [
    .. Enumerable
      .Range(FirstRow, Rows)
      .SelectMany(row =>
        new[]
        {
          new BedSlot(row, BedSlotSide.West),
          new BedSlot(row, BedSlotSide.Centre),
          new BedSlot(row, BedSlotSide.East),
        }
      ),
  ];

  /// <summary>The index of <paramref name="slot"/> in <see cref="Slots"/>, or -1 if it is not a real slot.</summary>
  public static int IndexOf(BedSlot slot) => Array.IndexOf(Slots, slot);

  /// <summary>
  /// Where <paramref name="slot"/> sits in the bed's north-orientation footprint, relative to the
  /// principal. The principal is row 1's runner (the pour basin), so it is the only slot at (0,0).
  /// </summary>
  public static (int Dx, int Dz) OffsetOf(BedSlot slot) =>
    (
      slot.Side switch {
        BedSlotSide.West => -1,
        BedSlotSide.East => 1,
        _ => 0,
      },
      slot.Row - FirstRow
    );

  /// <summary>The slot at a north-orientation footprint offset, or null if that is not a bed cell.</summary>
  public static BedSlot? SlotAt(int dx, int dz) {
    if (dz < 0 || dz >= Rows)
      return null;
    BedSlotSide? side = dx switch {
      -1 => BedSlotSide.West,
      0 => BedSlotSide.Centre,
      1 => BedSlotSide.East,
      _ => null,
    };
    return side is { } s ? new BedSlot(dz + FirstRow, s) : null;
  }

  /// <summary>
  /// Impressions cut into one mold slot. The bed narrows at both ends - row 1 to the pour basin's
  /// shoulders, row 4 to the back wall - so the end rows take two apiece and the middle rows three, which
  /// is why a full bed is 20 castings and not 24.
  /// </summary>
  public static int ImpressionsPerMold(int row) =>
    row == FirstRow || row == LastRow ? 2 : 3;

  /// <summary>Castings a fully-carved, fully-poured bed yields: 20.</summary>
  public static int BedCapacity =>
    Slots.Where(s => s.IsMold).Sum(s => ImpressionsPerMold(s.Row));

  /// <summary>Units a carved runner channel holds in transit - it is a conduit, not a cavity.</summary>
  public const int RunnerCapacity = 50;

  /// <summary>
  /// How much metal <paramref name="slot"/> holds once carved into <paramref name="state"/>, in units. A
  /// mold's cavity is its row's impression count at one casting each; sand has no cavity. It takes the slot
  /// rather than just the state because rows differ in impression count.
  /// </summary>
  public static int CapacityOf(BedSlot slot, BedSlotState state) =>
    state switch {
      BedSlotState.Mold => ImpressionsPerMold(slot.Row)
        * Items.ItemPig.PigUnits,
      BedSlotState.Runner => RunnerCapacity,
      _ => 0,
    };

  /// <summary>
  /// Every slot other than the principal's own cell, in footprint order. The block's <c>fillerOffsets</c>
  /// is generated from this, so the footprint and the carved surface cannot disagree about where a slot is,
  /// and the order emitted here is the order <c>SlotCells</c> zips against.
  /// </summary>
  public static IEnumerable<BedSlot> FillerSlots =>
    Slots.Where(s => OffsetOf(s) != (0, 0));

  /// <summary>The element group holding a row's slots.</summary>
  public static string RowGroup(int row) => $"RunnerRow{row}";

  private static string SideSuffix(BedSlotSide side) =>
    side == BedSlotSide.West ? "W" : "E";

  /// <summary>
  /// The single element that draws <paramref name="slot"/> in <paramref name="state"/>. A state the slot
  /// cannot hold falls back to sand rather than throwing, so a bad persisted value shows an uncarved slot
  /// instead of failing a chunk render. The <c>Full</c> suffix names the uncarved element - full of sand -
  /// not a poured one.
  /// </summary>
  public static string ElementFor(BedSlot slot, BedSlotState state) {
    if (!slot.Accepts(state))
      state = BedSlotState.Sand;

    string carved = slot.IsMold
      ? $"Mold{slot.Row}{SideSuffix(slot.Side)}"
      : $"RunnerCenter{slot.Row}";
    return state == BedSlotState.Sand ? carved + "Full" : carved;
  }

  /// <summary>The selective-element path drawing <paramref name="slot"/> in <paramref name="state"/>.</summary>
  public static string PathFor(BedSlot slot, BedSlotState state) =>
    $"{RunnersGroup}/{RowGroup(slot.Row)}/{ElementFor(slot, state)}/*";

  /// <summary>
  /// The full selective-element list for a carved bed: one entry per slot. <paramref name="states"/> is
  /// indexed as <see cref="Slots"/>; anything short or missing reads as plain sand, so a truncated save
  /// renders an uncarved bed instead of nothing.
  /// </summary>
  public static string[] CarvedElements(IReadOnlyList<BedSlotState>? states) {
    var paths = new List<string>(Slots.Length);
    for (int i = 0; i < Slots.Length; i++) {
      BedSlotState state =
        states != null && i < states.Count ? states[i] : BedSlotState.Sand;
      paths.Add(PathFor(Slots[i], state));
    }
    return [.. paths];
  }

  /// <summary>
  /// Composes what the animator should draw from what the construction behaviour has built
  /// (<paramref name="built"/>) and the current slot states. It expands the construction's whole-group
  /// <c>SandRunners</c> entry into one path per slot, since the group on its own would draw every state of
  /// every slot in the same hole; a freshly built bed has every slot on <see cref="BedSlotState.Sand"/> and
  /// so comes out flat.
  /// <para>
  /// It composes over the construction's list rather than replacing it: that behaviour rebuilds the mesh
  /// from its own list on every stage, so a pushed list would be clobbered by the next stage.
  /// </para>
  /// </summary>
  public static string[] Compose(
    IReadOnlyList<string>? built,
    IReadOnlyList<BedSlotState>? states,
    bool constructed
  ) {
    IEnumerable<string> baseElements = built ?? [];
    if (!constructed)
      return [.. baseElements];

    // Drop the group entry (and anything under it) - the per-slot paths replace it whole.
    string[] kept =
    [
      .. baseElements.Where(e =>
        e != RunnersGroup
        && !e.StartsWith(RunnersGroup + "/", StringComparison.Ordinal)
      ),
    ];
    return [.. kept, .. CarvedElements(states)];
  }
}
