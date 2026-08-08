using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// Shared base for a mega-block that occupies one grid cell but renders across a multi-cell footprint
/// reserved with invisible <see cref="BlockStructureFiller"/> cells (real per-cell collision, with
/// interaction/break/info rerouted to the principal). It refuses placement unless the whole footprint is
/// clear, spawns the fillers on placement and clears them again on break. Concrete blocks supply the
/// footprint rotation through <see cref="StructureAngle"/> and override
/// <see cref="OnFootprintPlaced"/> when they need to touch a filler cell right after placement. Drops
/// are left to the derived block: construction-raised ones override <c>GetDrops</c> to <c>[]</c>, the
/// engines keep a craftable-frame self-drop.
/// </summary>
public abstract class BlockFilledMegastructure : Block, IFillerHost {
  /// <summary>The block's <c>fillerOffsets</c> attribute - from JSON or from a code-first
  /// <see cref="ExpandedLib.Definitions.ExBlockDef"/> injected as a synthetic asset - or null if
  /// none. Satisfies <see cref="IFillerHost"/> for every mega-block.</summary>
  public virtual JsonObject? FillerOffsets => Attributes?["fillerOffsets"];

  /// <summary>
  /// Rotation applied to the north-orientation footprint offsets to reach the placed orientation. Some
  /// blocks offset it by 180 degrees so the body is raised away from the player, matching their JSON
  /// <c>rotateYByType</c>; the concrete block supplies the formula. Public because the block entity
  /// rotates its own render and collision boxes by the same angle.
  /// </summary>
  public abstract int StructureAngle { get; }

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

  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) {
    // Clear the reserved volume first so no invisible solid cells are left behind; the base call then
    // spills container contents / scatters construction materials as applicable.
    StructureFillers.RemoveFillers(world, pos, FootprintCells(pos));
    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }
}
