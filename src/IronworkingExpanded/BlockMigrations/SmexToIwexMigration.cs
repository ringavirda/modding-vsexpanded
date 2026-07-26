using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockMigrations;

/// <summary>
/// Rewrites the molten-network and blast-furnace blocks that moved from the <c>smex</c> domain to
/// <c>iwex</c> when ironmaking was split into its own foundational mod. The code structure is
/// unchanged across the move - only the domain differs - so every <c>smex:&lt;path&gt;</c> becomes
/// <c>iwex:&lt;path&gt;</c>, variant for variant. Rather than hand-list every variant, this
/// enumerates the registered <c>iwex</c> blocks and pairs each relocated one with its old
/// <c>smex</c> code; the new iwex-only blocks (the ore bunker and mixer) never existed under
/// <c>smex</c> and are deliberately excluded.
/// <para>
/// The relocated blocks that carry a block entity (the molten canals' charge, the furnace, its tap,
/// the bell/reinforced hoppers, the molten barrel and mold pedestal) also get their saved tree copied
/// verbatim: both the old and new entities are the same relocated class, so the state maps one-to-one.
/// Block instances held as item stacks (containers, inventories) are rewritten by the framework too.
/// </para>
/// </summary>
public class SmexToIwexMigration : IBlockCodeMigration, IBlockEntityMigration
{
  public string Name => "Molten network + blast furnace moved smex -> iwex";

  // First code part (before the first '-') of every block relocated from smex to iwex. The new
  // iwex-only blocks (bunker, mixer) are absent on purpose, so they are never remapped.
  private static readonly HashSet<string> Relocated =
  [
    "moltencanal", // canalbrick/cobblestone, start/straight/bend/t/x-junction, tap, moldpedestal
    // "moltenbarrel" is handled by BarrelConstructionMigration instead: its code gained a construction
    // variant (moltenbarrel -> moltenbarrel-bolted), so smex:moltenbarrel maps straight to the bolted
    // variant there, in one hop, rather than to the now-dead iwex:moltenbarrel this list would produce.
    "blastfurnace", // the tuyere block
    "blastfurnacetap",
    "hopperbell",
    "hopperreinforced",
    "slag",
    "solidifiediron",
    "slagpath", // the slag-paving block, slab and stairs (decorative byproduct line)
    "slagpathslab",
    "slagpathstairs",
  ];

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    foreach (Block block in api.World.Blocks)
    {
      if (
        block?.Code == null
        || block.Code.Domain != "iwex"
        || !Relocated.Contains(block.Code.FirstCodePart())
      )
        continue;
      yield return (
        new AssetLocation("smex", block.Code.Path),
        block.Code.Clone()
      );
    }
  }

  /// <summary>Copies the relocated block entity's saved state verbatim - same class, identical tree.</summary>
  public void MigrateBlockEntity(
    AssetLocation oldCode,
    AssetLocation newCode,
    ITreeAttribute? oldState,
    BlockEntity newBlockEntity,
    IWorldAccessor world
  )
  {
    if (oldState != null)
      newBlockEntity.FromTreeAttributes(oldState, world);
  }
}
