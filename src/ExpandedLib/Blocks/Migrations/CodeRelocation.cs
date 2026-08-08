using System;
using System.Collections.Generic;
using ExpandedLib.Helpers;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace ExpandedLib.Blocks.Migrations;

/// <summary>
/// Builds remaps for a block that kept its shape but changed identity - a domain move, a rename, or
/// both - by pairing an explicitly named <b>historical</b> base code with a <b>live</b> one and
/// carrying every variant suffix across unchanged.
///
/// <para>
/// <b>Why the old base must be named literally.</b> Every migrator in this repo used to derive its
/// old codes by walking the live registry and assuming the path had never changed
/// (<c>ppex:{code.Path}</c>, <c>smex:{code.Path}</c>, <c>lpex:{code.Path}</c>). That assumption holds
/// exactly until a rename, at which point the migrator starts emitting old codes that never shipped and
/// silently stops covering the ones that did - with no crash and no message, because
/// <see cref="BlockMigrationModSystem"/> drops an unresolvable pair with a <c>Logger.Warning</c>. It
/// also produced the inverse failure: enumerating a whole domain made <c>HpexExtractionMigration</c>
/// claim <c>lpex:pipe-*</c>, so once hpex added rolled pipes at the paths lpex already used for cast
/// ones, every placed cast pipe was silently converted to a rolled one.
/// </para>
///
/// <para>
/// So identity is explicit and history is frozen, while <i>variants</i> stay derived from the live
/// registry - the half that genuinely should track the present, so a new orientation needs no migration
/// edit. After a rename only the <c>newBase</c> string moves, one row at a time.
/// </para>
///
/// What has actually shipped is recorded in <c>ReleasedCodes</c> (extracted from
/// <c>dist/Releases/</c>) and asserted by <c>ReleasedCodeCoverageTests</c>: only <c>exlib</c>,
/// <c>ppex</c> and <c>smex</c> ever escaped, so those are the only domains a legacy code can carry.
/// </summary>
public static class CodeRelocation
{
  /// <summary>
  /// Pairs every live <c>{newDomain}:{newBase}[-variants]</c> block with its historical
  /// <c>{oldDomain}:{oldBase}[-variants]</c> code.
  /// <para>
  /// Caution: the suffix match requires a literal <c>-</c> after <paramref name="newBase"/>, so a base of
  /// <c>slag</c> pairs with <c>slag</c> and <c>slag-x</c> but never with <c>slagpath</c>. Losing that
  /// separator would silently pull a whole neighbouring family into the migration.
  /// </para>
  /// </summary>
  /// <param name="legacySideWords">
  /// <b>Set for a row whose released code spelled its <c>side</c> variant as a full word</b>
  /// (<c>ppex:boilercornish-<b>north</b></c>). Every <c>side</c> group in this suite renders single
  /// letters since the 2026-08-04 respelling, so carrying the live suffix across unchanged would emit
  /// <c>ppex:boilercornish-n</c> - a code that never shipped - and leave the 32 word-spelled codes that
  /// <i>did</i> ship with no path to a live block. Silently: <see cref="BlockMigrationModSystem"/> drops
  /// an unresolvable pair with a <c>Logger.Warning</c>, so a player's boiler just stops loading.
  /// <para>
  /// It is per-row and not global because the spelling is a fact about <b>what that mod shipped</b>:
  /// ppex spelled <c>side</c> as words, while <b>smex spelled every facing as a letter already</b>
  /// (<c>smex:blastfurnacetap-n</c>). Respelling a smex row would orphan exactly what it covers today.
  /// </para>
  /// </param>
  public static IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> Remap(
    ICoreServerAPI api,
    string oldDomain,
    string oldBase,
    string newDomain,
    string newBase,
    bool legacySideWords = false
  )
  {
    foreach (Block block in api.World.Blocks)
    {
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

      yield return (new AssetLocation(oldDomain, oldBase + suffix), code.Clone());
    }
  }

  /// <summary>
  /// The block's code suffix with its <c>side</c> letter written back out as the full word the old code
  /// carried. Anything else is returned untouched.
  /// <para>
  /// It keys off the block's own <c>side</c> <b>variant value</b> rather than off "the last segment is
  /// one of nsew", and that distinction is load-bearing: a network node's <c>orientation</c> group
  /// renders single letters too (<c>ppex:pipe-fluidintake-n</c>) and has done since it shipped, so a
  /// positional rule would respell codes that are already correct and orphan them.
  /// </para>
  /// </summary>
  private static string WithSideSpeltOut(Block block, string suffix)
  {
    if (block.Variant is not { } variants || !variants.TryGetValue("side", out string? side))
      return suffix;
    if (side is not { Length: 1 } || !ExOrientation.IsHorizontalSideWord(side))
      return suffix;
    // The side group renders last, so the letter is the final segment. Guarded rather than assumed:
    // a mismatch means the grammar moved and silently doing nothing beats emitting a wrong old code.
    if (!suffix.EndsWith("-" + side, StringComparison.Ordinal))
      return suffix;

    string word = ExOrientation.SideFromAngle(ExOrientation.AngleFromSide(side), asLetter: false);
    return suffix[..^side.Length] + word;
  }

  /// <summary>
  /// <see cref="Remap(ICoreServerAPI, string, string, string, string, bool)"/> for a base code that did
  /// not change name, only domain.
  /// </summary>
  public static IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> Remap(
    ICoreServerAPI api,
    string oldDomain,
    string newDomain,
    string sharedBase,
    bool legacySideWords = false
  ) => Remap(api, oldDomain, sharedBase, newDomain, sharedBase, legacySideWords);

  /// <summary>
  /// A historical block that carried <b>no</b> variants onto a live one that does: the old code cannot
  /// say which variant it meant, so a single default is chosen and must be stated out loud.
  /// </summary>
  public static IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> RemapToDefault(
    ICoreServerAPI api,
    string oldDomain,
    string oldCode,
    string newFullCode
  )
  {
    var target = new AssetLocation(newFullCode);
    if (api.World.GetBlock(target) != null)
      yield return (new AssetLocation(oldDomain, oldCode), target);
  }
}
