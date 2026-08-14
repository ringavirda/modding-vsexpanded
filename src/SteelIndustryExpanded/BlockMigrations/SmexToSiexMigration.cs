using System.Collections.Generic;
using ExpandedLib.Blocks.Migrations;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace SteelIndustryExpanded.BlockMigrations;

/// <summary>
/// Carries the blocks that stayed in this mod across the <c>smex</c> to <c>siex</c> domain rename.
/// Only the domain moved: every base code and variant grammar is unchanged, so each row names one
/// shared base and the variants come off the live registry.
/// <para>
/// smex reached players at 0.9.8, so these are codes in real worlds. The blocks that left for iiex
/// are handled by <c>IronIndustryExpanded.BlockMigrations.SmexToIiexMigration</c>, whose <c>smex</c>
/// left sides stay frozen and are none of the bases below.
/// </para>
/// <para>
/// The word-spelled <c>side</c> codes the same blocktypes also shipped (<c>convertercontrol-north</c>)
/// are not covered here. They already reached no live block before this rename and are recorded in
/// <c>ReleasedCodeDebt</c> as B25; paying them off is a separate change, and adding
/// <c>legacySideWords</c> here would claim them without mapping the letter codes this rename breaks.
/// </para>
/// </summary>
public class SmexToSiexMigration : IBlockCodeMigration, IBlockEntityMigration {
  public string Name => "Steel line moved smex -> siex";

  /// <summary>
  /// Base codes whose blocks stayed in this mod. <c>converter</c> covers the gas intake alone: the
  /// suffix match requires a literal <c>-</c>, so it pairs with <c>converter-intake-*</c> and never
  /// with <c>converterbessemer</c>. <c>smokestack</c> likewise covers <c>smokestack-intake-*</c>,
  /// refractory tier included.
  /// </summary>
  private static readonly string[] SharedBases =
  [
    "converter",
    "converterbessemer",
    "convertercontrol",
    "convertertransmission",
    "cowperstoveheatsink",
    "engineairblower",
    "hopperbell",
    "hopperreinforced",
    "smokestack",
  ];

  public IEnumerable<(AssetLocation oldCode, AssetLocation newCode)> GetRemaps(
    ICoreServerAPI api
  ) {
    foreach (string @base in SharedBases)
      foreach (var pair in CodeRelocation.Remap(api, "smex", "siex", @base))
        yield return pair;
  }

  /// <summary>Copies the saved state verbatim - the class is the same type under a new domain key, so
  /// the tree maps one to one. Without this a migrated converter keeps its blocks and loses its heat,
  /// charge and construction stage.</summary>
  public void MigrateBlockEntity(
    AssetLocation oldCode,
    AssetLocation newCode,
    ITreeAttribute? oldState,
    BlockEntity newBlockEntity,
    IWorldAccessor world
  ) {
    if (oldState != null)
      newBlockEntity.FromTreeAttributes(oldState, world);
  }
}
