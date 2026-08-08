using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockMigrations;

/// <summary>
/// Rewrites the molten-network and blast-furnace blocks that moved from the <c>smex</c> domain to
/// <c>iwex</c> when ironmaking was split into its own foundational mod. Each row names the code
/// <b>as it shipped in smex 0.9.4</b> and the code it lives under now; variants are carried across
/// from the live registry, so a new orientation needs no edit here.
///
/// <para>
/// <b>Three of these rows were once missing, and the blocks they cover were unreachable.</b> The
/// migration previously walked the live iwex registry and matched on <c>Code.FirstCodePart()</c> against a
/// set of smex base codes - which silently stops matching the moment iwex renames the block. It had:
/// <c>blastfurnace</c> (the tuyere, now <c>tuyere</c>), <c>blastfurnacetap</c> (now
/// <c>irontap</c>/<c>slagtap</c>) and no entry at all for <c>blastfurnacedoor</c> (now
/// <c>chargedoor</c>). So a
/// released world's tuyeres, taps and furnace door had <b>no path to a live block</b> - 9 codes in all.
/// Worse, <c>SmexToIwexMigrationTests</c> stayed green throughout because it fabricated
/// <c>iwex:blastfurnace-tuyere-n</c> and <c>iwex:blastfurnacetap-north</c>, blocks no definition has
/// ever produced. <c>ReleasedCodeCoverageTests</c> now asserts the contract against the real
/// definitions instead.
/// </para>
///
/// <para>
/// <c>hopperbell</c>, <c>hopperreinforced</c> and the whole slag-paving line were in the old set but
/// are not here: the hoppers <b>stayed</b> in smex, and the paving blocks were added after the split
/// and never shipped under either domain (see <c>ReleasedCodes</c>). Rows for blocks that never
/// escaped are dead weight that reads like coverage.
/// </para>
///
/// <para>
/// The relocated blocks that carry a block entity (the canals' charge, the tap, the mold pedestal) also
/// get their saved tree copied verbatim: old and new are the same relocated class, so the state maps
/// one-to-one. Block instances held as item stacks are rewritten by the framework too.
/// </para>
/// </summary>
public class SmexToIwexMigration : IBlockCodeMigration, IBlockEntityMigration
{
  public string Name => "Molten network + blast furnace moved smex -> iwex";

  /// <summary>
  /// <c>(smex base code as shipped, current iwex base code)</c>. Where the two differ, the rename
  /// happened <i>after</i> the domain move and is the reason the old registry-walking form went blind.
  /// </summary>
  private static readonly (string Old, string New)[] Relocated =
  [
    // canalbrick/cobblestone × start/straight/bend/t/x-junction/moldpedestal/tap.
    // The left side is the smex code as it shipped and never changes; the right moved in the
    // naming wave (moltencanal -> molten-canal).
    ("moltencanal", "molten-canal"),
    ("slag", "slag-block"),
    // Renamed when the two frozen-melt blocks merged into one variant-grouped block. The left
    // side is the smex code as it shipped and never moves; only the right side follows the rename.
    // A shipped smex block was always a blast furnace's residue, so pig iron is the correct target.
    ("solidifiediron", "hearthmetal-pigiron"),
    // Renamed after the move.
    // Narrowed to the tuyere itself. A broader ("blastfurnace", "furnace") form would walk
    // every live iwex:furnace-* block and invent smex sources for parts that never shipped.
    ("blastfurnace-tuyere", "furnace-tuyere"),
    // Every shipped tap comes back as the iron tap, including the ones the player built as slag taps.
    // smex shipped one tap blocktype for both notches, so its code carries no record of which a given
    // block was - there is nothing to branch on and no heuristic that is better than a guess. Mapping the
    // lot to `irontap` is the only choice that is at least *stated*; a player with a hot furnace will find
    // the upper notch wrong and must re-craft it. Said out loud here because it is the sort of thing that
    // otherwise gets discovered in a save.
    ("blastfurnacetap", "furnace-irontap"),
    // "moltenbarrel" is deliberately absent: its code gained a construction variant, so
    // BarrelConstructionMigration maps smex:moltenbarrel straight to iwex:molten-barrel-plated in one
    // hop rather than through the now-dead iwex:moltenbarrel this table would produce.
  ];

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  )
  {
    foreach (var (old, @new) in Relocated)
      foreach (var pair in CodeRelocation.Remap(api, "smex", old, "iwex", @new))
        yield return pair;

    // The furnace door shipped with no variant group; iwex's charge door has a `side`. The old code
    // cannot say which way it faced, so every one comes back facing north and the player may need to
    // wrench it. Stated here rather than discovered in play.
    foreach (
      var pair in CodeRelocation.RemapToDefault(
        api,
        "smex",
        "blastfurnacedoor",
        "iwex:furnace-chargedoor-n"
      )
    )
      yield return pair;
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
