using System.Collections.Generic;
using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Structures;

/// <summary>
/// Shared base for a mega-block that occupies one grid cell but renders across a multi-cell footprint
/// reserved with invisible <see cref="BlockStructureFiller"/> cells (real per-cell collision, with
/// interaction/break/info rerouted to the principal). It refuses placement unless the whole footprint is
/// clear, spawns the fillers on placement and clears them again on removal. A concrete block may
/// override <see cref="StructureAngle"/> for a rotation the placed <c>side</c>/<c>orientation</c> variant
/// does not already give it, and <see cref="OnFootprintPlaced"/> when it needs to touch a filler cell
/// right after placement. Drops are left to the derived block: construction-raised ones override
/// <c>GetDrops</c> to <c>[]</c>, the engines keep a craftable-frame self-drop.
/// <para>
/// Registered under <c>ExFilledMegastructure</c> so a JSON-only blocktype can name this class directly
/// and get per-cell collision with no C#; <see cref="BlockEntityMultiblock"/> is its JSON-only entity
/// counterpart.
/// </para>
/// </summary>
[BlockRegister("ExFilledMegastructure", PrefixModId = false)]
public class BlockFilledMegastructure : Block, IFillerHost {
  /// <summary>The block's <c>fillerOffsets</c> attribute - from JSON, from a JSON <c>multiblockLayout</c>
  /// (<see cref="JsonMultiblockLayout"/>), or from a code-first
  /// <see cref="ExpandedLib.Definitions.ExBlockDef"/> injected as a synthetic asset - or null if none.
  /// Satisfies <see cref="IFillerHost"/> for every mega-block.</summary>
  public virtual JsonObject? FillerOffsets => Attributes?["fillerOffsets"];

  /// <summary>
  /// Rotation applied to the north-orientation footprint offsets to reach the placed orientation.
  /// Defaults to the block's own <c>side</c> (full word) or <c>orientation</c> (single letter) variant,
  /// the same rule <see cref="ExpandedLib.Blocks.BlockBehaviorExOrientable"/> applies; a concrete block
  /// overrides this when it needs a different formula, such as a 180-degree offset so the body is raised
  /// away from the player. Public because the block entity rotates its own render and collision boxes by
  /// the same angle.
  /// </summary>
  public virtual int StructureAngle =>
    ExOrientation.AngleFromSide(Variant?["side"] ?? Variant?["orientation"]);

  public override void OnLoaded(ICoreAPI api) {
    base.OnLoaded(api);
    JsonMultiblockLayout.Resolve(this, api.Logger);
  }

  /// <summary>The block's world footprint cells for a principal at <paramref name="pos"/>.</summary>
  protected List<FillerCell> FootprintCells(BlockPos pos) =>
    StructureFillers.FootprintCells((IFillerHost)this, pos, StructureAngle);

  public override bool CanPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref string failureCode
  ) {
    if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
      return false;

    // Refuse placement unless the whole volume is clear, else the fillers fail to spawn.
    if (!StructureFillers.CanPlace(world, FootprintCells(blockSel.Position))) {
      failureCode = "notenoughspace";
      return false;
    }
    return true;
  }

  public override void OnBlockPlaced(
    IWorldAccessor world,
    BlockPos blockPos,
    ItemStack? byItemStack = null
  ) {
    base.OnBlockPlaced(world, blockPos, byItemStack);
    StructureFillers.PlaceFillers(world, blockPos, FootprintCells(blockPos));
    OnFootprintPlaced(world, blockPos);
  }

  /// <summary>Hook run right after the footprint fillers are placed. Runs on whichever side placed the
  /// block, so an override must guard its own side-effects. Default: nothing.</summary>
  protected virtual void OnFootprintPlaced(
    IWorldAccessor world,
    BlockPos blockPos
  ) { }

  public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos) {
    // The engine calls this on every removal path - a player break, an explosion
    // (Block.OnBlockExploded goes straight to a bulk SetBlock, never OnBlockBroken), a worldedit
    // delete - unlike OnBlockBroken, which only a player break triggers. Clearing the footprint
    // here, rather than there, keeps every path from leaving invisible solid orphan cells behind.
    // Mirrors vanilla's BlockLargeGear3m.OnBlockRemoved.
    StructureFillers.RemoveFillers(world, pos, FootprintCells(pos));
    base.OnBlockRemoved(world, pos);
  }
}
