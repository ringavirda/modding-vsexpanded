using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace HighPressureExpanded.BlockMigrations;

/// <summary>
/// Moves the two high-pressure machines - the Lancashire boiler and the Cornish engine - onto the
/// <c>hpex</c> domain they were extracted into. Every block this mod ships was previously an
/// <c>lpex:</c> block (and, before the <c>ppex → lpex</c> rename, a <c>ppex:</c> one), so a placed
/// machine in any older save carries a code that no longer resolves.
/// <para>
/// <b>Both historical domains are emitted deliberately.</b> The migrator's table is a flat
/// <c>oldCode → newBlock</c> map applied with a single lookup per cell - it does <i>not</i> chain
/// remaps - so a pre-rename save's <c>ppex:boilerlancashire-*</c> would never reach <c>hpex:</c> by
/// hopping through lpex's rename migration. It also cannot: that migration is derived from the live
/// registry and only covers blocks whose domain is still <c>lpex</c>, which these no longer are.
/// Emitting <c>ppex:</c> here is therefore the only path for pre-rename worlds, and it cannot collide
/// with lpex's rename pass because that pass never produces these paths.
/// </para>
/// <para>
/// Derived from the live registry, so it always covers exactly the orientations this mod ships. Pairs
/// whose old code is absent in a world are skipped. Blocks only - hpex ships no items.
/// </para>
/// </summary>
public class HpexExtractionMigration : IBlockCodeMigration
{
  public string Name => "HP machines extracted to hpex";

  /// <summary>The domains these blocks shipped under before the extraction, newest first.</summary>
  private static readonly string[] LegacyDomains = ["lpex", "ppex"];

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    foreach (Block block in api.World.Blocks)
    {
      if (block?.Code is not { Domain: "hpex" } code)
        continue;

      foreach (string domain in LegacyDomains)
        yield return (new AssetLocation(domain, code.Path), code.Clone());
    }
  }
}
