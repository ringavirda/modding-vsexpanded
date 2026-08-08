using System;
using System.Collections.Generic;
using System.Linq;

namespace IronworkingExpanded.BlockStructures.Casting;

/// <summary>Where a slot sits across the bed - the two mold flanks and the runner spine between them.</summary>
public enum BedSlotSide
{
  West,
  Centre,
  East,
}

/// <summary>
/// What a bed slot currently is. <see cref="Sand"/> covers <b>both</b> never-carved and shaken-out, because
/// they are the same thing: an undisturbed bed of sand. That is not a shortcut - the art says so, with one
/// <c>…Full</c> element per slot standing for both, so a shaken-out mold and a mold never cut leave the
/// identical flat patch and the player re-carves either the same way.
/// </summary>
public enum BedSlotState
{
  /// <summary>Full, flat sand - never carved, or carved and since shaken out.</summary>
  Sand,

  /// <summary>A carved runner channel. Runner slots only.</summary>
  Runner,

  /// <summary>
  /// A carved row of impressions. <b>One mold shape serves both castings</b>: iron poured into it hardens
  /// into pigs, slag into slag bricks. They are the same furrow because they are the same act - a trough
  /// raked into sand - and splitting them into two carve gestures only ever produced a bed cut for the
  /// wrong thing.
  /// </summary>
  Mold,
}

/// <summary>One carvable slot: which row it belongs to, and where across that row it sits.</summary>
/// <param name="Row">1-4, front to back. Row 1 holds the pour basin at its centre.</param>
public readonly record struct BedSlot(int Row, BedSlotSide Side)
{
  /// <summary>A centre slot is runner spine; the flanks are molds.</summary>
  public bool IsMold => Side != BedSlotSide.Centre;

  /// <summary>Whether this slot can be carved into <paramref name="state"/> at all.</summary>
  public bool Accepts(BedSlotState state) =>
    state switch
    {
      BedSlotState.Sand => true,
      BedSlotState.Runner => !IsMold,
      BedSlotState.Mold => IsMold,
      _ => false,
    };
}

/// <summary>
/// The sand casting bed's carved surface, as the mapping from <b>slot state to shape elements</b>. Every one
/// of the twelve slots always renders exactly one element, so the bed's whole appearance is a list of twelve
/// choices - which is what lets the mesh be rebuilt from block-entity state rather than needing a block
/// variant per combination (there are 2¹² of them).
/// <para>
/// Pure, and separated from the block entity, because element names are <b>string literals against art</b>:
/// a typo or a Blockbench re-export that re-rolls an auto-name produces a silently missing chunk of bed, not
/// an exception. The tests assert every name this can emit exists in the shipped shape, so the art and the
/// code cannot drift apart unnoticed.
/// </para>
/// <para>
/// Paths follow VS's hierarchical <c>SelectiveElements</c> matcher: one <c>&lt;path&gt;/*</c> per element that
/// should render, and <b>never</b> an ancestor on its own - ancestors render automatically as prefixes of the
/// deeper entries, while naming one exactly would drop all of its children. That trap has bitten this repo
/// before (see the engine's broken-mesh selection).
/// </para>
/// </summary>
public static class SandBedLayout
{
  /// <summary>
  /// The group holding every carvable element - and the one the last construction stage adds, so a finished
  /// bed is a bed full of sand with nothing cut into it yet. The construction behaviour can only name the
  /// group as a whole (which would draw every slot's every state at once), so <see cref="Compose"/> replaces
  /// it with one entry per slot.
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
  /// Where <paramref name="slot"/> sits in the bed's north-orientation footprint, relative to the principal.
  /// The principal <b>is</b> row 1's runner (the pour basin), so it is the only slot at (0,0).
  /// </summary>
  public static (int Dx, int Dz) OffsetOf(BedSlot slot) =>
    (
      slot.Side switch
      {
        BedSlotSide.West => -1,
        BedSlotSide.East => 1,
        _ => 0,
      },
      slot.Row - FirstRow
    );

  /// <summary>The slot at a north-orientation footprint offset, or null if that is not a bed cell.</summary>
  public static BedSlot? SlotAt(int dx, int dz)
  {
    if (dz < 0 || dz >= Rows)
      return null;
    BedSlotSide? side = dx switch
    {
      -1 => BedSlotSide.West,
      0 => BedSlotSide.Centre,
      1 => BedSlotSide.East,
      _ => null,
    };
    return side is { } s ? new BedSlot(dz + FirstRow, s) : null;
  }

  /// <summary>
  /// Impressions cut into one mold slot. The bed narrows at both ends - row 1 gives ground to the pour
  /// basin's shoulders and row 4 to the back wall - so the two <b>end rows take two</b> apiece and the two
  /// <b>middle rows three</b>. It is the art's own geometry, and the reason a full bed is twenty castings
  /// rather than a round twenty-four.
  /// </summary>
  public static int ImpressionsPerMold(int row) => row == FirstRow || row == LastRow ? 2 : 3;

  /// <summary>Castings a fully-carved, fully-poured bed yields - <b>20</b>.</summary>
  public static int BedCapacity =>
    Slots.Where(s => s.IsMold).Sum(s => ImpressionsPerMold(s.Row));

  /// <summary>Units a carved runner channel holds in transit - it is a conduit, not a cavity.</summary>
  public const int RunnerCapacity = 50;

  /// <summary>
  /// How much metal <paramref name="slot"/> holds once carved into <paramref name="state"/>, in units. A
  /// mold's cavity is its row's impression count at one casting each; plain sand has no cavity at all.
  /// <para>
  /// It takes the <b>slot</b> and not just the state because the rows are not the same size - which is the
  /// whole reason a middle row is worth more per pour than an end row.
  /// </para>
  /// </summary>
  public static int CapacityOf(BedSlot slot, BedSlotState state) =>
    state switch
    {
      BedSlotState.Mold => ImpressionsPerMold(slot.Row) * Items.ItemPig.PigUnits,
      BedSlotState.Runner => RunnerCapacity,
      _ => 0,
    };

  /// <summary>
  /// Every slot that is <b>not</b> the principal's own cell, in footprint order. The block's
  /// <c>fillerOffsets</c> is generated from this, so the footprint and the carved surface cannot disagree
  /// about where a slot is - and so the order it emits is the order <c>SlotCells</c> zips against.
  /// </summary>
  public static IEnumerable<BedSlot> FillerSlots => Slots.Where(s => OffsetOf(s) != (0, 0));

  /// <summary>The element group holding a row's slots.</summary>
  public static string RowGroup(int row) => $"RunnerRow{row}";

  private static string SideSuffix(BedSlotSide side) => side == BedSlotSide.West ? "W" : "E";

  /// <summary>
  /// The single element that draws <paramref name="slot"/> in <paramref name="state"/>. A state the slot
  /// cannot hold falls back to plain sand rather than throwing - a bad persisted value should show an
  /// uncarved slot, not crash a chunk render.
  /// <para>
  /// The <c>Full</c> suffix is the <b>uncarved</b> element, not the poured one: a shaken-out slot is flat
  /// sand again, so "full" reads as "full of sand". Getting that backwards renders every finished bed as a
  /// carved one and every carved one as flat, with nothing raised anywhere to say so.
  /// </para>
  /// </summary>
  public static string ElementFor(BedSlot slot, BedSlotState state)
  {
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
  public static string[] CarvedElements(IReadOnlyList<BedSlotState>? states)
  {
    var paths = new List<string>(Slots.Length);
    for (int i = 0; i < Slots.Length; i++)
    {
      BedSlotState state = states != null && i < states.Count ? states[i] : BedSlotState.Sand;
      paths.Add(PathFor(Slots[i], state));
    }
    return [.. paths];
  }

  /// <summary>
  /// Composes what the animator should draw, given what the construction behaviour has built
  /// (<paramref name="built"/>) and the current slot states.
  /// <para>
  /// Two jobs. It <b>expands</b> the construction's whole-group <c>SandRunners</c> entry into one path per
  /// slot - the group on its own would draw every state of every slot stacked in the same hole, so this is
  /// what turns "the bed is full of sand" into "each slot shows what is cut into it". A freshly built bed
  /// has every slot on <see cref="BedSlotState.Sand"/>, so it comes out flat and uncarved, exactly as the
  /// last build stage leaves it.
  /// </para>
  /// <para>
  /// And it <b>composes rather than overrides</b>: the construction behaviour rebuilds the mesh from its own
  /// list on every stage, so a bed that simply pushed its own list would be clobbered by the next stage. Run
  /// this over the construction's current list instead and the two cannot fight.
  /// </para>
  /// </summary>
  public static string[] Compose(
    IReadOnlyList<string>? built,
    IReadOnlyList<BedSlotState>? states,
    bool constructed
  )
  {
    IEnumerable<string> baseElements = built ?? [];
    if (!constructed)
      return [.. baseElements];

    // Drop the group entry (and anything under it) - the per-slot paths replace it whole.
    string[] kept =
    [
      .. baseElements.Where(e =>
        e != RunnersGroup && !e.StartsWith(RunnersGroup + "/", StringComparison.Ordinal)
      ),
    ];
    return [.. kept, .. CarvedElements(states)];
  }
}
