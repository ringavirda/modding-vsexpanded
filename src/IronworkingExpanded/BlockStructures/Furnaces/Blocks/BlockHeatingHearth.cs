using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The reheat furnace's hearth bed: three rows wide and <b>two cells deep</b>, so a slab lies on it
/// whole. Five of its six cells are fillers, and as with the puddling hearth the fillers are the
/// interface - which cell you click picks the row.
/// </summary>
[BlockRegister]
public partial class BlockHeatingHearth
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider
{
  #region Code-first definition

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, BlockFurnaceCoreBase.FurnaceCode, "furnace/heatinghearth")
        .Class<BlockHeatingHearth>()
        .EntityClass<BlockEntityHeatingHearth>()
        .Material(EnumBlockMaterial.Metal)
        .MaxStackSize(1)
        .Behavior("MultiblockStructure")
        .Behavior("ExOrientable")
        // `type` names the family member; every furnace part shares the code `iwex:furnace`
        // (see BlockFurnaceCoreBase.FurnaceCode and N7).
        .VariantGroup("type", "heatinghearth")
        .SideVariant()
        .ShapeByTypePerOrientation("iwex:furnace/heatinghearth", 0)
        .CreativeCommon("*-n")
        // 3 wide x 2 deep. The principal is on the near row (the one at the door); the far row is
        // depth for a slab, which does not fit in one cell.
        .FillerOffsets(
          StructureFootprint.Layout(f =>
            f.Origin(-1, -1)
              .Layer(
                0,
                """
                ###
                #0#
                """
              )
          )
        )
        .Replaceable(400)
        .Resistance(6f)
        .LightAbsorption(0)
        .Sound("walk", "walk/metal")
        .Sound("place", "block/anvil")
        .SoundByTool(EnumTool.Pickaxe, "block/rock-hit-pickaxe", "block/rock-break-pickaxe")
        .SolidNonOpaque(),
    ];

  #endregion

  public override int StructureAngle => ExOrientation.AngleFromSide(Variant["side"]);

  #region Interaction

  // The footprint turns with the block, so a clicked world offset is rotated back into the hearth's own
  // frame before it is read as a row. Both rows of a column map to the same row: the far cell is depth
  // for a slab, not a fourth place to put something.
  private HearthRows.Row? RowAt(BlockPos principal, BlockPos clicked)
  {
    Vec3i world = new(clicked.X - principal.X, clicked.Y - principal.Y, clicked.Z - principal.Z);
    Vec3i local = ExOrientation.RotateOffset(world, -StructureAngle);
    return HearthRows.FromLocalOffset(new Vec3i(local.X, local.Y, 0));
  }

  private bool HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos principal,
    BlockPos clicked
  )
  {
    if (world.BlockAccessor.GetBlockEntity(principal) is not BlockEntityHeatingHearth be)
      return false;
    if (BlockBehaviorMultiblockStructure.TryToggleProjection(world, byPlayer, principal))
      return true;
    if (world.Side != EnumAppSide.Server)
      return true;
    if (RowAt(principal, clicked) is not { } row)
      return true;

    var player = byPlayer as IServerPlayer;
    ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;

    // Empty hand draws a piece out; a held piece lays one in. One verb, decided by what you are holding -
    // the same shape as every other in-world station here.
    if (active == null || active.Empty)
    {
      if (be.TryTake(row) is { } taken)
      {
        if (byPlayer.InventoryManager?.TryGiveItemstack(taken) != true)
          world.SpawnItemEntity(taken, principal.ToVec3d().Add(0.5, 1.0, 0.5));
      }
      else if (be.StockIn(row) != null)
        player?.SendIngameError("iwex-hearth-centreblocks");
      return true;
    }

    if (!be.TryLoad(row, active))
      player?.SendIngameError(
        be.StockIn(row) != null ? "iwex-hearth-rowfull" : "iwex-hearth-notstock"
      );
    return true;
  }

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) => HandleInteract(world, byPlayer, blockSel.Position, blockSel.Position);

  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => HandleInteract(world, byPlayer, principalSel.Position, clickedCell);

  bool IFillerInteractionTarget.OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => false;

  void IFillerInteractionTarget.OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) { }

  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => HearthHelp();

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) => HearthHelp();

  private WorldInteraction[] HearthHelp() =>
    [
      new()
      {
        ActionLangCode = "iwex:furnace-heatinghearth-help-load",
        MouseButton = EnumMouseButton.Right,
      },
      new()
      {
        ActionLangCode = "iwex:furnace-heatinghearth-help-take",
        MouseButton = EnumMouseButton.Right,
      },
    ];

  #endregion
}
