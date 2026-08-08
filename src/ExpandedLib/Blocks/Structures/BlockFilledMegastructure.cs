using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// Shared base for a mega-block that occupies one grid cell but renders across a multi-cell footprint
/// reserved with invisible <see cref="BlockStructureFiller"/> cells (real per-cell collision, with
/// interaction/break/info rerouted to the principal). It folds the filler triad that would otherwise be
/// copy-pasted byte-for-byte across every such block: refuse placement unless the whole
/// footprint is clear, spawn the fillers on placement, and clear them again on break.
/// <para>
/// Concrete blocks supply the footprint rotation through <see cref="StructureAngle"/> and, when they need
/// to touch a filler cell right after placement, override <see cref="OnFootprintPlaced"/> (the boiler turns
/// its steam-connector cell into a port; the engine snaps an already-present sub-machine to match). Drops
/// are deliberately not handled here: most of these blocks are raised through construction and must never
/// drop themselves (so they override <c>GetDrops</c> to <c>[]</c>), but the engines keep a craftable-frame
/// self-drop - leaving that choice to each block.
/// </para>
/// <para>
/// It implements <see cref="IFillerHost"/> here with a single runtime accessor that reads the block's own
/// <c>fillerOffsets</c> attribute. That attribute is populated the same way whether it comes from a JSON
/// file (the not-yet-migrated blocks) or a code-first <see cref="ExpandedLib.Definitions.ExBlockDef"/>
/// injected as a synthetic asset - so a mega-block no longer depends on the JSON-scanning attribute source
/// generator for its footprint (the generator's collision-avoidance sees this member and stops emitting a
/// per-class one). This is the "runtime reads the same record" half of the code-first generator inversion.
/// </para>
/// </summary>
public abstract class BlockFilledMegastructure : Block, IFillerHost
{
  /// <summary>The block's <c>fillerOffsets</c> attribute (from JSON or the injected code-first def), or
  /// null if none. One accessor for every mega-block, replacing the per-class generated member.</summary>
  public virtual JsonObject? FillerOffsets => Attributes?["fillerOffsets"];

  /// <summary>
  /// Rotation applied to the north-orientation footprint offsets to reach the placed orientation. Some
  /// blocks offset this by 180° so the body is raised AWAY from the player (matching their JSON
  /// <c>rotateYByType</c>); the concrete block supplies the exact formula. Public because the block entity
  /// rotates its own render/collision boxes by the same angle.
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
  )
  {
    if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
      return false;

    // Refuse placement unless the whole volume is clear, else the fillers fail to spawn.
    if (!StructureFillers.CanPlace(world, FootprintCells(blockSel.Position)))
    {
      failureCode = "notenoughspace";
      return false;
    }
    return true;
  }

  public override void OnBlockPlaced(
    IWorldAccessor world,
    BlockPos blockPos,
    ItemStack? byItemStack = null
  )
  {
    base.OnBlockPlaced(world, blockPos, byItemStack);
    StructureFillers.PlaceFillers(world, blockPos, FootprintCells(blockPos));
    OnFootprintPlaced(world, blockPos);
  }

  /// <summary>
  /// Hook run right after the footprint fillers are placed. Runs on whichever side placed the block, so
  /// guard your own side-effects (both current overrides act server-side only). Default: nothing.
  /// </summary>
  protected virtual void OnFootprintPlaced(IWorldAccessor world, BlockPos blockPos) { }

  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    // Clear the reserved volume first so no invisible solid cells are left behind; the base call then
    // spills container contents / scatters construction materials as applicable.
    StructureFillers.RemoveFillers(world, pos, FootprintCells(pos));
    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }
}
