using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;

namespace IronworkingExpanded.BlockStructures.Casting;

/// <summary>
/// The long cell's 1×2 footprint, held outside the block so the offsets can be pinned without standing a
/// world up - the same split <see cref="SandBedLayout"/> makes. The second cell is a filler for collision
/// and interaction only and hosts no molten cell of its own: the impression is a single pool on the
/// principal, which also keeps it clear of the rule that a filler may not be a network graph node.
/// <para>
/// The body extends −Z in model space, the same +180 convention <see cref="Blocks.BlockSandCastingBed"/>
/// uses, so a placed cell runs away from the player rather than through them.
/// </para>
/// </summary>
public static class LongCellLayout {
  /// <summary>How many blocks the station occupies, principal included.</summary>
  public const int CellCount = 2;

  /// <summary>
  /// The longest lane a filling may draw, in voxels. The interior runs <c>z −14 … 14</c> with 2-thick end
  /// dams, so a lane cannot be longer. The item dimensions in the <c>iwex.md</c> ladder imply 25- and
  /// 27-voxel pieces, which do not fit.
  /// </summary>
  public const int MaxLaneLength = 24;

  /// <summary>The one filler, in model space - the far half of the casting.</summary>
  public static IEnumerable<FillerCellSpec> Footprint() =>
    [new FillerCellSpec(0, 0, 1)];
}
