using System.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// Addressing molten cells on a block entity that hosts more than one.
/// <c>GetBehavior&lt;BEBehaviorMoltenCell&gt;()</c> returns the first match with no way to say which
/// cell is wanted, so on a multi-cell host it can silently operate on the wrong one. These helpers
/// select by the cell's declared <see cref="BEBehaviorMoltenCell.Key"/> instead. A single-cell host
/// keeps the default key, and <c>GetBehavior&lt;T&gt;()</c> remains correct for it.
/// </summary>
public static class MoltenCellHost {
  /// <summary>
  /// The molten cell on <paramref name="be"/> whose declared key is <paramref name="key"/>, or
  /// <c>null</c> when the entity hosts no such cell. A missing cell is a legitimate answer - a hearth
  /// block chiselled away, or a fitting that never declared the cell - so this returns null rather
  /// than throwing.
  /// </summary>
  public static BEBehaviorMoltenCell? MoltenCell(
    this BlockEntity? be,
    string key
  ) =>
    be
      ?.Behaviors.OfType<BEBehaviorMoltenCell>()
      .FirstOrDefault(c => c.Key == key);
}
