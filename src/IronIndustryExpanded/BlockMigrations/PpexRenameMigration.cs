using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using ExpandedLib.Blocks.Networks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockMigrations;

/// <summary>
/// The <c>ppex → iiex</c> mod-rename migration: a released save holds <c>ppex:*</c> codes that no
/// longer resolve, so every surviving block and item is remapped onto its iiex code. ppex is the only
/// domain on this path that ever shipped, so the rename is retargeted IN PLACE rather than chained
/// through the intermediate <c>lpex</c> spelling - <c>CodeRelocation.Remap</c> walks the live registry
/// and rebuilds the table each load, so there is nothing for a second hop to hang off. Rows name the
/// base code as it shipped in ppex 0.6.4 rather than deriving it from the live registry, which would emit
/// codes that never shipped; variants are still carried from the live registry. Covers only what is
/// still an iiex block under an unchanged base code. The Lancashire boiler and Cornish engine moved to
/// hpex (<c>HpexExtractionMigration</c>); the pipe segments and valves lost their <c>material</c> axis
/// (<c>PipeMigration</c>).
/// </summary>
public class PpexRenameMigration : IBlockCodeMigration, IItemCodeMigration {
  public string Name => "ppex → iiex mod rename";

  /// <summary>Base codes that shipped under ppex and are still iiex blocks under the same name.</summary>
  private static readonly string[] Blocks =
  [
    "boilercornish",
    "enginewatt",
    "enginefluidpump",
    "enginempgenerator",
    "manualfluidpump",
    "steamcondenser",
    // The two fittings that shipped with no material variant and still carry the same base code, so a
    // flat rename is all they need. Listed individually because a bare "pipe" base would also claim the
    // segments and valves PipeMigration owns.
    "pipe-outlet",
    "pipe-fluidintake",
  ];

  /// <summary>
  /// Fittings whose base code changed as well as its domain: the passthroughs gained the <c>tier</c>
  /// segment in M.2, so a flat rename no longer finds them and the released <c>ppex:</c> codes would
  /// lose their migration - silently, but for <c>ReleasedCodeCoverageTests</c>.
  /// <c>CodeRelocation</c>'s trailing-separator rule still keeps <c>…-passthrough</c> from swallowing
  /// <c>…-passthroughbend</c>.
  /// </summary>
  private static readonly (string Old, string New)[] RenamedBlocks =
  [
    ("pipe-passthrough", $"pipe-{BlockPipe.CastTier}-passthrough"),
    ("pipe-passthroughbend", $"pipe-{BlockPipe.CastTier}-passthroughbend"),
  ];

  /// <summary>The two items ppex shipped; both are still iiex items under the same name.</summary>
  private static readonly string[] Items = ["gear", "largegear"];

  IEnumerable<(
    AssetLocation oldCode,
    AssetLocation newCode
  )> IBlockCodeMigration.GetRemaps(ICoreServerAPI api) {
    // legacySideWords: ppex spelled its `side` variant as a full word (`ppex:boilercornish-north`) where
    // every side group in this suite now renders a letter. It does not fire on the four fittings, which
    // carry an `orientation` group rather than a `side` one, so their `-n` codes stay as they shipped.
    foreach (string @base in Blocks)
      foreach (
        var pair in CodeRelocation.Remap(
          api,
          "ppex",
          "iiex",
          @base,
          legacySideWords: true
        )
      )
        yield return pair;

    foreach (var (old, live) in RenamedBlocks)
      foreach (
        var pair in CodeRelocation.Remap(
          api,
          "ppex",
          old,
          "iiex",
          live,
          legacySideWords: true
        )
      )
        yield return pair;
  }

  IEnumerable<(
    AssetLocation oldCode,
    AssetLocation newCode
  )> IItemCodeMigration.GetRemaps(ICoreServerAPI api) {
    foreach (Item item in api.World.Items) {
      if (item?.Code is not { Domain: "iiex" } code)
        continue;

      foreach (string @base in Items)
        if (
          code.Path == @base
          || code.Path.StartsWith(@base + "-", System.StringComparison.Ordinal)
        ) {
          yield return (new AssetLocation("ppex", code.Path), code.Clone());
          break;
        }
    }
  }
}
