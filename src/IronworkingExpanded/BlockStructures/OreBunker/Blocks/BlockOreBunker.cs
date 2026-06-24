using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.OreBunker.Blocks;

/// <summary>
/// The ore/blast-mix storage bunker mega-block. Occupies one grid cell (the principal) but renders
/// across a 3×1×6 footprint reserved with invisible structure fillers (real per-cell collision);
/// construction is driven by the RightClickConstructable behavior in the block JSON. The brick
/// <c>brick</c> variant only swaps the wall texture, so all colours share this one class.
/// </summary>
[BlockRegister]
public partial class BlockOreBunker
  : Block,
    IFillerHost,
    IFillerInteractionTarget
{
  /// <summary>Structure/filler rotation, paired with the JSON <c>rotateYByType</c> (north 0 … west 90).</summary>
  private int Angle => ExOrientation.AngleFromSide(Variant["side"]);

  /// <summary>The structure-filler rotation angle, for multiblock-structure verification.</summary>
  public int StructureAngle => Angle;

  #region Placement / filler footprint

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
    var cells = StructureFillers.FootprintCells(
      this,
      blockSel.Position,
      Angle
    );
    if (!StructureFillers.CanPlace(world, cells))
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
    StructureFillers.PlaceFillers(
      world,
      blockPos,
      StructureFillers.FootprintCells(this, blockPos, Angle)
    );
  }

  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    // Clear the reserved volume first so no invisible solid cells are left behind. The stored
    // contents are spilled by the BlockEntityContainer base in the following base call.
    StructureFillers.RemoveFillers(
      world,
      pos,
      StructureFillers.FootprintCells(this, pos, Angle)
    );
    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  // A broken bunker returns only its construction materials (scattered by the RightClickConstructable
  // behaviour) plus its stored contents (spilled by the container BE), never the bunker block itself.
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [];

  #endregion

  #region Filler interaction forwarding

  // A click on any reserved footprint cell drives the principal's behaviours (RCC construction,
  // structure projection); post-construction container access will hook in here later.
  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => base.OnBlockInteractStart(world, byPlayer, principalSel);

  bool IFillerInteractionTarget.OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => base.OnBlockInteractStep(secondsUsed, world, byPlayer, principalSel);

  void IFillerInteractionTarget.OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => base.OnBlockInteractStop(secondsUsed, world, byPlayer, principalSel);

  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => base.GetPlacedBlockInteractionHelp(world, principalSel, forPlayer);

  #endregion
}
