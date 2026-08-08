using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace HighPressureExpanded.BlockMigrations;

/// <summary>
/// Moves the two high-pressure machines - the Lancashire boiler and the Cornish engine - onto the
/// <c>hpex</c> domain they were extracted into. Both shipped in <c>ppex</c> 0.6.4 and were later
/// carried through the <c>ppex → lpex</c> rename, so a placed machine in any older save holds a code
/// that no longer resolves.
///
/// <para>
/// ⚠⚠ <b>This migration used to enumerate the entire hpex domain</b>, emitting <c>lpex:&lt;path&gt;</c>
/// for every block hpex declares. That was safe only while hpex shipped nothing but these two machines.
/// The moment <c>RolledPipeDefinitions</c> added <c>hpex:pipe-*</c> at the very paths lpex already used
/// for its <b>cast</b> pipes, the migration began claiming <c>lpex:pipe-straight-ns</c> and its 29
/// siblings - all <b>live</b> blocks - and silently rewrote every placed cast pipe into a rolled one,
/// flipping flanged joints to welded and fracturing runs. Its only guard
/// (<c>GetBlock(oldCode) != null</c>) passes for a live block, so nothing downstream could catch it.
/// The two paths are now named literally, and <c>ReleasedCodeCoverageTests</c> fails if any migration
/// ever again declares a live code as a legacy source.
/// </para>
///
/// <para>
/// <b>Both historical domains are emitted deliberately.</b> The migrator now follows chains, but that
/// does not rescue the <c>ppex</c> leg: <see cref="LowPressureExpanded.BlockMigrations.LpexRenameMigration"/>
/// only covers blocks that are <i>still</i> lpex, which these are not, so it never produces a
/// <c>lpex:boilerlancashire-*</c> hop for a chain to follow. Emitting <c>ppex:</c> directly is the only
/// path for a pre-rename world.
/// </para>
/// </summary>
public class HpexExtractionMigration : IBlockCodeMigration
{
  public string Name => "HP machines extracted to hpex";

  /// <summary>The only two blocktypes hpex was extracted with. Everything else hpex declares - the
  /// rolled pipe tier - is new to hpex and has never existed under another domain, so it must never
  /// appear here.</summary>
  private static readonly string[] ExtractedBases = ["boilerlancashire", "enginecornish"];

  /// <summary>The domains these two shipped under before the extraction, newest first.</summary>
  private static readonly string[] LegacyDomains = ["lpex", "ppex"];

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    // ⚠ legacySideWords on BOTH legs: the boiler and the engine carried a word-spelled `side` under
    // ppex and kept it through the lpex rename, so every world old enough to hold either domain holds
    // `-north`, not the `-n` the live block renders since the 2026-08-04 respelling.
    foreach (string legacyDomain in LegacyDomains)
      foreach (string @base in ExtractedBases)
        foreach (
          var pair in CodeRelocation.Remap(api, legacyDomain, "hpex", @base, legacySideWords: true)
        )
          yield return pair;
  }
}
