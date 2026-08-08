using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace HighPressureExpanded.BlockMigrations;

/// <summary>
/// Moves the two high-pressure machines - the Lancashire boiler and the Cornish engine - onto the
/// <c>hpex</c> domain they were extracted into. Both shipped in <c>ppex</c> 0.6.4 and were carried
/// through the <c>ppex</c> to <c>lpex</c> rename, so a placed machine in any older save holds a code
/// that no longer resolves.
/// <para>
/// Both legacy domains are emitted. Chain-following does not cover the <c>ppex</c> leg:
/// <see cref="LowPressureExpanded.BlockMigrations.LpexRenameMigration"/> only covers blocks that are
/// still lpex, which these are not, so it never produces a <c>lpex:boilerlancashire-*</c> hop to
/// follow. Emitting <c>ppex:</c> directly is the only path for a pre-rename world.
/// </para>
/// </summary>
public class HpexExtractionMigration : IBlockCodeMigration {
  public string Name => "HP machines extracted to hpex";

  /// <summary>The only two blocktypes hpex was extracted with, named literally rather than by
  /// enumerating the domain. Everything else hpex declares - the rolled pipe tier - is new to hpex
  /// and shares block paths with live lpex blocks, so listing it here would remap those live blocks.
  /// <c>ReleasedCodeCoverageTests</c> fails if a migration declares a live code as a legacy source.</summary>
  private static readonly string[] ExtractedBases =
  [
    "boilerlancashire",
    "enginecornish",
  ];

  /// <summary>The domains these two shipped under before the extraction, newest first.</summary>
  private static readonly string[] LegacyDomains = ["lpex", "ppex"];

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) {
    // legacySideWords on both legs: the boiler and the engine carried a word-spelled `side` under
    // ppex and kept it through the lpex rename, so a world old enough to hold either domain holds
    // `-north`, not the `-n` the live block uses.
    foreach (string legacyDomain in LegacyDomains)
      foreach (string @base in ExtractedBases)
        foreach (
          var pair in CodeRelocation.Remap(
            api,
            legacyDomain,
            "hpex",
            @base,
            legacySideWords: true
          )
        )
          yield return pair;
  }
}
