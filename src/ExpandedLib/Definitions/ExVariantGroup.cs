using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Helpers;

namespace ExpandedLib.Definitions;

/// <summary>
/// One variant group of an <see cref="ExBlockDef"/>, as rendered into the block's code. Groups are ordered
/// and the order is the order their states appear in the code, so <c>type</c> before <c>tier</c> before
/// <c>side</c> yields <c>iiex:furnace-blastcore-tier1-north</c>. Every code derivation
/// (<see cref="ExBlockDef.Any"/>, <see cref="ExBlockDef.WithVariant"/>, the generated <c>{Mod}Blocks</c>
/// table) walks the groups in this order.
/// </summary>
/// <param name="Name">The group's <c>code</c>, or for a codeless worldproperty group the last segment of
/// the property it loads from.</param>
/// <param name="States">The states the definition lists. Empty for a worldproperty-sourced group, whose
/// states live in the game's assets rather than in the definition.</param>
/// <param name="FromProperties">The <c>loadFromProperties</c> path, or null for an explicit state list.</param>
public sealed record ExVariantGroup(
  string Name,
  IReadOnlyList<string> States,
  string? FromProperties
) {
  /// <summary>
  /// Whether this group names a horizontal facing and can therefore be pinned from a <c>BlockFacing</c>
  /// instead of a hand-typed token. True for both spellings in use: the vanilla <c>side</c> group of full
  /// words sourced from <c>abstract/horizontalorientation</c> (which lists no states of its own), and a
  /// network node's <c>orientation</c> group of single letters (<c>n|s|w|e</c>). False for an axis group
  /// (<c>ns|we</c>, <c>nw|ne|se|sw</c>), whose states are not facings.
  /// <para>
  /// The property path is matched on its last segment, since both <c>abstract/horizontalorientation</c> and
  /// <c>game:abstract/horizontalorientation</c> occur in the tree.
  /// </para>
  /// </summary>
  public bool IsHorizontalFacing =>
    FromProperties?.Split(':')[^1] == "abstract/horizontalorientation"
    || (States.Count > 0 && States.All(ExOrientation.IsHorizontalSideWord));

  /// <summary>
  /// Whether this facing group spells its states as single letters (<c>n</c>) rather than full words
  /// (<c>north</c>). Meaningless unless <see cref="IsHorizontalFacing"/>. A worldproperty group lists no
  /// states and reports false, matching vanilla's word-form <c>horizontalorientation</c>.
  /// </summary>
  public bool UsesLetters => States.Count > 0 && States.All(s => s.Length == 1);
}
