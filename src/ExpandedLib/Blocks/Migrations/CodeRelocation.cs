using System;
using System.Collections.Generic;
using ExpandedLib.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Blocks.Migrations;

/// <summary>
/// Builds remaps for a block that kept its shape but changed identity - a domain move, a rename or
/// both - pairing an explicitly named historical base code with a live one and carrying every variant
/// suffix across unchanged. The historical base must be a literal string, never derived from the live
/// registry and never a whole-domain enumeration; variants stay derived from the live registry, so a
/// new orientation needs no migration edit. Only <c>exlib</c>, <c>ppex</c> and <c>siex</c> were ever
/// released, so those are the only domains a legacy code can carry; <c>ReleasedCodes</c> (from
/// <c>dist/Releases/</c>) records what shipped and <c>ReleasedCodeCoverageTests</c> asserts coverage.
/// </summary>
public static class CodeRelocation {
  /// <summary>
  /// Pairs every live <c>{newDomain}:{newBase}[-variants]</c> block with its historical
  /// <c>{oldDomain}:{oldBase}[-variants]</c> code. The suffix match requires a literal <c>-</c> after
  /// <paramref name="newBase"/>, so a base of <c>slag</c> pairs with <c>slag</c> and <c>slag-x</c> but
  /// never with <c>slagpath</c>.
  /// </summary>
  /// <param name="legacySideWords">Set for a row whose released code spelled its <c>side</c> variant as
  /// a full word (<c>ppex:boilercornish-north</c>) where the live group renders a letter. Per-row: ppex
  /// shipped words, smex shipped letters (<c>smex:blastfurnacetap-n</c>).</param>
  public static IEnumerable<(
    AssetLocation oldCode,
    AssetLocation newCode
  )> Remap(
    ICoreServerAPI api,
    string oldDomain,
    string oldBase,
    string newDomain,
    string newBase,
    bool legacySideWords = false
  ) {
    foreach (Block block in api.World.Blocks) {
      if (block?.Code is not { } code || code.Domain != newDomain)
        continue;

      string path = code.Path;
      string suffix;
      if (path == newBase)
        suffix = "";
      else if (path.StartsWith(newBase + "-", StringComparison.Ordinal))
        suffix = path[newBase.Length..];
      else
        continue;

      if (legacySideWords)
        suffix = WithSideSpeltOut(block, suffix);

      yield return (
        new AssetLocation(oldDomain, oldBase + suffix),
        code.Clone()
      );
    }
  }

  /// <summary>
  /// The block's code suffix with its <c>side</c> letter written back out as the full word the old code
  /// carried; anything else is returned untouched. Keys off the block's own <c>side</c> variant value,
  /// not off the last segment being one of nsew: a network node's <c>orientation</c> group renders
  /// single letters too (<c>ppex:pipe-fluidintake-n</c>) and always has.
  /// </summary>
  private static string WithSideSpeltOut(Block block, string suffix) {
    if (
      block.Variant is not { } variants
      || !variants.TryGetValue("side", out string? side)
    )
      return suffix;
    if (side is not { Length: 1 } || !ExOrientation.IsHorizontalSideWord(side))
      return suffix;
    // The side group renders last, so the letter is the final segment. A mismatch means the code
    // grammar moved; returning the suffix unchanged is safer than emitting a wrong old code.
    if (!suffix.EndsWith("-" + side, StringComparison.Ordinal))
      return suffix;

    string word = ExOrientation.SideFromAngle(
      ExOrientation.AngleFromSide(side),
      asLetter: false
    );
    return suffix[..^side.Length] + word;
  }

  /// <summary><see cref="Remap(ICoreServerAPI, string, string, string, string, bool)"/> for a base code
  /// that did not change name, only domain.</summary>
  public static IEnumerable<(
    AssetLocation oldCode,
    AssetLocation newCode
  )> Remap(
    ICoreServerAPI api,
    string oldDomain,
    string newDomain,
    string sharedBase,
    bool legacySideWords = false
  ) =>
    Remap(api, oldDomain, sharedBase, newDomain, sharedBase, legacySideWords);

  /// <summary>
  /// Maps a historical block that carried no variants onto a live one that does. The old code cannot
  /// say which variant it meant, so the caller names the full new code explicitly.
  /// </summary>
  public static IEnumerable<(
    AssetLocation oldCode,
    AssetLocation newCode
  )> RemapToDefault(
    ICoreServerAPI api,
    string oldDomain,
    string oldCode,
    string newFullCode
  ) {
    var target = new AssetLocation(newFullCode);
    if (api.World.GetBlock(target) != null)
      yield return (new AssetLocation(oldDomain, oldCode), target);
  }
}
