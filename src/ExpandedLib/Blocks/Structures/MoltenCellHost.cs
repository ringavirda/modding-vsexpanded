using System.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// Addressing molten cells on a block entity that hosts <b>more than one</b>.
/// <para>
/// <b><c>GetBehavior&lt;BEBehaviorMoltenCell&gt;()</c> is not usable on such a host.</b> It returns the
/// first match and offers no way to say which one is wanted, so a caller reaching for the slag cell the
/// obvious way silently operates on the iron one - a data-loss bug wearing a correct-looking call site.
/// These helpers select by the cell's declared <see cref="BEBehaviorMoltenCell.Key"/> instead.
/// </para>
/// <para>
/// A single-cell host needs none of this: one cell keeps the default key and
/// <c>GetBehavior&lt;T&gt;()</c> is still right for it. The rule is only "if a block entity can ever host
/// two, address them by key".
/// </para>
/// </summary>
public static class MoltenCellHost
{
  /// <summary>
  /// The molten cell on <paramref name="be"/> whose declared key is <paramref name="key"/>, or
  /// <c>null</c> when the entity hosts no such cell.
  /// <para>
  /// Null rather than throwing, because "this block does not have that cell" is a legitimate answer:
  /// the same code path serves a hearth block that has been chiselled away and a fitting that never
  /// declared the cell at all.
  /// </para>
  /// </summary>
  public static BEBehaviorMoltenCell? MoltenCell(this BlockEntity? be, string key) =>
    be?.Behaviors.OfType<BEBehaviorMoltenCell>().FirstOrDefault(c => c.Key == key);
}
