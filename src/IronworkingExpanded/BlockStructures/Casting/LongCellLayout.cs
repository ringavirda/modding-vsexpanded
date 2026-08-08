using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;

namespace IronworkingExpanded.BlockStructures.Casting;

/// <summary>
/// The long cell's 1×2 footprint. Kept out of the block so the offsets can be pinned without standing a
/// world up - the same split <see cref="SandBedLayout"/> makes.
/// <para>
/// <b>The second cell is a filler for collision and interaction only.</b> The whole station is one
/// casting, so it hosts <b>no molten cell of its own</b> - the impression is a single pool on the
/// principal. That is also what keeps it clear of the rule that a filler may not be a network graph node.
/// </para>
/// <para>
/// The body extends <b>−Z</b> in model space, the same +180 convention the
/// <see cref="Blocks.BlockSandCastingBed"/> uses, so a placed cell runs away from the player rather than
/// through them.
/// </para>
/// </summary>
public static class LongCellLayout
{
  /// <summary>How many blocks the station occupies, principal included.</summary>
  public const int CellCount = 2;

  /// <summary>
  /// The longest lane a filling may draw, in voxels.
  /// <para>
  /// Not a style choice: the interior runs <c>z −14 … 14</c> with 2-thick end dams, so a lane physically
  /// cannot be longer. Recorded here because the <c>iwex.md</c> ladder's item dimensions imply 25- and
  /// 27-voxel pieces, and discovering they do not fit costs a redraw.
  /// </para>
  /// </summary>
  public const int MaxLaneLength = 24;

  /// <summary>The one filler, in model space - the far half of the casting.</summary>
  public static IEnumerable<FillerCellSpec> Footprint() => [new FillerCellSpec(0, 0, 1)];
}
