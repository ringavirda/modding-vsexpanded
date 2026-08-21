using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The reheat furnace's hearth bed: three rows wide and two cells deep, so a slab lies on it whole.
/// Five of its six cells are fillers, and the fillers are the interface - the clicked cell picks the row.
/// </summary>
[BlockRegister]
public partial class BlockHeatingHearth
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider {
  #region Code-first definition

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(
          domain,
          BlockFurnaceCoreBase.FurnaceCode,
          "furnace/heatinghearth"
        )
        .Class<BlockHeatingHearth>()
        .EntityClass<BlockEntityHeatingHearth>()
        .Material(EnumBlockMaterial.Metal)
        .MaxStackSize(1)
        .Behavior("MultiblockStructure")
        .Behavior("ExOrientable")
        // `type` names the family member; every furnace part shares the code `iiex:furnace`
        // (see BlockFurnaceCoreBase.FurnaceCode).
        .VariantGroup("type", "heatinghearth")
        .SideVariant()
        .ShapeByTypePerOrientation("iiex:furnace/heatinghearth", 0)
        .CreativeCommon("*-n")
        // 3 wide x 2 deep. The principal sits on the near row, at the door; the far row is depth for a
        // slab, which does not fit in one cell. The course above is the low roof over the bed.
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
              .Layer(
                1,
                """
                ###
                ###
                """
              )
          )
        )
        .Replaceable(400)
        .Resistance(6f)
        .LightAbsorption(0)
        .Sound("walk", "walk/metal")
        .Sound("place", "block/anvil")
        .SoundByTool(
          EnumTool.Pickaxe,
          "block/rock-hit-pickaxe",
          "block/rock-break-pickaxe"
        )
        .SolidNonOpaque(),
    ];

  #endregion

  public override int StructureAngle =>
    ExOrientation.AngleFromSide(Variant["side"]);

  #region Interaction

  // The footprint turns with the block, so a clicked world offset is rotated back into the hearth's own
  // frame before it is read as a row. Both cells of a column map to the same row; the far cell is depth
  // for a slab, not a separate slot.
  private HearthRows.Row? RowAt(BlockPos principal, BlockPos clicked) {
    Vec3i world = new(
      clicked.X - principal.X,
      clicked.Y - principal.Y,
      clicked.Z - principal.Z
    );
    Vec3i local = ExOrientation.RotateOffset(world, -StructureAngle);
    return HearthRows.FromLocalOffset(new Vec3i(local.X, local.Y, 0));
  }

  private bool HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos principal,
    BlockPos clicked
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(principal)
      is not BlockEntityHeatingHearth be
    )
      return false;
    if (
      BlockBehaviorMultiblockStructure.TryToggleProjection(
        world,
        byPlayer,
        principal
      )
    )
      return true;
    if (world.Side != EnumAppSide.Server)
      return true;
    if (RowAt(principal, clicked) is not { } row)
      return true;

    var player = byPlayer as IServerPlayer;
    ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;

    // Empty hand draws a piece out; a held piece lays one in.
    if (active == null || active.Empty) {
      if (be.TryTake(row) is { } taken) {
        if (byPlayer.InventoryManager?.TryGiveItemstack(taken) != true)
          world.SpawnItemEntity(taken, principal.ToVec3d().Add(0.5, 1.0, 0.5));
      } else if (be.StockIn(row) != null)
        player?.SendIngameError("iiex-hearth-centreblocks");
      return true;
    }

    if (!be.TryLoad(row, active))
      player?.SendIngameError(
        be.StockIn(row) != null ? "iiex-hearth-rowfull" : "iiex-hearth-notstock"
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
        ActionLangCode = IiexLang.HeatinghearthHelpLoad,
        MouseButton = EnumMouseButton.Right,
      },
      new()
      {
        ActionLangCode = IiexLang.HeatinghearthHelpTake,
        MouseButton = EnumMouseButton.Right,
      },
    ];

  #endregion
}
