using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Helpers;

namespace ExpandedLib.Definitions;

/// <summary>
/// One variant group of an <see cref="ExBlockDef"/>, as it will be rendered into the block's code.
/// <para>
/// Groups are ordered, and the order is not cosmetic: it is the order the states appear in the rendered
/// code, so <c>type</c> before <c>tier</c> before <c>side</c> is what makes
/// <c>iwex:furnace-blastcore-tier1-north</c> read the way it does. Anything deriving a code from a def -
/// <see cref="ExBlockDef.Any"/>, <see cref="ExBlockDef.WithVariant"/>, the generated <c>{Mod}Blocks</c>
/// table - walks them in this order.
/// </para>
/// </summary>
/// <param name="Name">The group's <c>code</c>, or - for a codeless worldproperty group - the last segment
/// of the property it loads from.</param>
/// <param name="States">The states the definition lists. <b>Empty for a worldproperty-sourced group</b>,
/// whose states live in the game's assets rather than in our definition; that is an absence of knowledge
/// here, not an absence of states.</param>
/// <param name="FromProperties">The <c>loadFromProperties</c> path, or null for an explicit state list.</param>
public sealed record ExVariantGroup(
  string Name,
  IReadOnlyList<string> States,
  string? FromProperties
)
{
  /// <summary>
  /// Whether this group names a <b>horizontal facing</b>, and can therefore be pinned from a
  /// <c>BlockFacing</c> instead of a hand-typed token.
  /// <para>
  /// True in two shapes, which is the point: the vanilla <c>side</c> group carrying full words
  /// (<c>north</c>), sourced from <c>abstract/horizontalorientation</c> and therefore listing no states of
  /// its own; and a network node's <c>orientation</c> group carrying single letters (<c>n|s|w|e</c>). That
  /// is the N4 split, and a caller should not have to know which side of it a given part falls on.
  /// </para>
  /// <para>
  /// <b>False for an axis group</b> - <c>ns|we</c> on a mill axle, <c>nw|ne|se|sw</c> on a canal bend.
  /// Those states are not facings, so a <c>BlockFacing</c> cannot name one and offering the overload would
  /// invite a code that matches nothing.
  /// </para>
  /// </summary>
  /// <para>
  /// The property is matched <b>domain-insensitively</b>. Both spellings are in the tree -
  /// <c>abstract/horizontalorientation</c> and <c>game:abstract/horizontalorientation</c> - and an
  /// exact match silently excluded the second, which is the <b>only</b>
  /// word-spelled facing group left in the suite (the slag stairs). It was therefore the one group a
  /// layout author could not name from a <c>BlockFacing</c>, and the one where hand-typing the letter
  /// everything else uses produces a code matching no block.
  /// </para>
  public bool IsHorizontalFacing =>
    FromProperties?.Split(':')[^1] == "abstract/horizontalorientation"
    || (States.Count > 0 && States.All(ExOrientation.IsHorizontalSideWord));

  /// <summary>
  /// Whether this facing group spells its states as single letters (<c>n</c>) rather than full words
  /// (<c>north</c>). Meaningless unless <see cref="IsHorizontalFacing"/>.
  /// <para>
  /// A worldproperty group lists no states, so it is judged by the property: vanilla's
  /// <c>horizontalorientation</c> is the word form.
  /// </para>
  /// </summary>
  public bool UsesLetters => States.Count > 0 && States.All(s => s.Length == 1);
}
