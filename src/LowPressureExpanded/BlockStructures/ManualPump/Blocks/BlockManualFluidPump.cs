using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using LowPressureExpanded.BlockStructures.ManualPump.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LowPressureExpanded.BlockStructures.ManualPump.Blocks;

/// <summary>
/// The hand-cranked fluid pump: a two-cell-tall, horizontally orientable pipe connector, not a
/// network node. In north orientation it draws from the south face and delivers out the north face;
/// placement is look-aware but the block is not wrench-orientable. The cell above is reserved by an
/// invisible filler carrying the collision box and the crank target. All work lives in
/// <see cref="BlockEntityManualFluidPump"/>; this block exposes the connectors, manages the filler
/// footprint and forwards the crank interaction.
/// </summary>
[BlockRegister]
public partial class BlockManualFluidPump
  : Block,
    INetworkConnector,
    IFillerInteractionTarget,
    IFillerHost,
    IExBlockDefProvider {
  public string NetworkType => "pipe";

  #region Code-first definition

  // The single-cell filler footprint (the crank cell above), read at runtime from the block's own
  // attributes so the value lives only in the def. Satisfies IFillerHost, which
  // StructureFillers.FootprintCells reads.
  public JsonObject? FillerOffsets => Attributes?["fillerOffsets"];

  /// <summary>The hand-cranked fluid pump blocktype: a two-cell-tall horizontally orientable pipe
  /// connector with an invisible filler reserving the crank cell above.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "manualfluidpump")
        .Class<BlockManualFluidPump>()
        .EntityClass<BlockEntityManualFluidPump>()
        .Behavior("ExOrientable")
        .EntityBehavior("Animatable")
        .Material(EnumBlockMaterial.Metal)
        .SideVariant()
        .CreativeCommon("*-n")
        .FillerOffsets([new FillerCellSpec(0, 1, 0)])
        .ShapeByTypePerOrientation("lpex:manualfluidpump", 0)
        .NonSolid(),
    ];

  #endregion

  /// <summary>Horizontal placement angle (north 0, west 90, south 180, east 270).</summary>
  private int Angle => ExOrientation.AngleFromSide(Variant["side"]);

  /// <summary>The input (water source) connector face - south in the north orientation.</summary>
  private BlockFacing InputFace =>
    ExOrientation.RotateFacing(BlockFacing.SOUTH, Angle);

  /// <summary>The output (delivery) connector face - north in the north orientation.</summary>
  private BlockFacing OutputFace =>
    ExOrientation.RotateFacing(BlockFacing.NORTH, Angle);

  public bool HasConnectorAt(BlockFacing face) =>
    face == InputFace || face == OutputFace;

  #region Filler footprint (the cell above)

  public override bool CanPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref string failureCode
  ) {
    if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
      return false;

    var cells = StructureFillers.FootprintCells(this, blockSel.Position, Angle);
    if (!StructureFillers.CanPlace(world, cells)) {
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
    StructureFillers.PlaceFillers(
      world,
      blockPos,
      StructureFillers.FootprintCells(this, blockPos, Angle)
    );
  }

  public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos) {
    // Runs on every removal path (a player break, an explosion, a worldedit delete), unlike
    // OnBlockBroken, so the crank cell above is never left behind as an orphan solid cell.
    StructureFillers.RemoveFillers(
      world,
      pos,
      StructureFillers.FootprintCells(this, pos, Angle)
    );
    base.OnBlockRemoved(world, pos);
  }

  #endregion

  #region Crank interaction (own cell + forwarded from the top filler)

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    HandleStart(world, byPlayer, blockSel.Position)
    ?? base.OnBlockInteractStart(world, byPlayer, blockSel);

  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) =>
    HandleStart(world, byPlayer, principalSel.Position)
    ?? base.OnBlockInteractStart(world, byPlayer, principalSel);

  /// <summary>
  /// Begins a crank on an empty-handed right-click. Returns <c>null</c> to defer, so a held item
  /// such as a wrench falls through to the default behavior.
  /// </summary>
  private bool? HandleStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos pumpPos
  ) {
    if (byPlayer.InventoryManager?.ActiveHotbarSlot?.Empty != true)
      return null;
    if (
      world.BlockAccessor.GetBlockEntity(pumpPos)
      is not BlockEntityManualFluidPump be
    )
      return null;

    be.OnPumpStart();
    return true;
  }

  public override bool OnBlockInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) => HandleStep(world, byPlayer, blockSel.Position);

  bool IFillerInteractionTarget.OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => HandleStep(world, byPlayer, principalSel.Position);

  private bool HandleStep(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos pumpPos
  ) {
    // Keep cranking only while the hand stays empty; a held item ends the hold.
    if (byPlayer.InventoryManager?.ActiveHotbarSlot?.Empty != true)
      return false;
    if (
      world.BlockAccessor.GetBlockEntity(pumpPos)
      is not BlockEntityManualFluidPump be
    )
      return false;

    be.OnPumpStep();
    return true;
  }

  public override void OnBlockInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) => HandleStop(world, blockSel.Position);

  void IFillerInteractionTarget.OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => HandleStop(world, principalSel.Position);

  private static void HandleStop(IWorldAccessor world, BlockPos pumpPos) {
    if (
      world.BlockAccessor.GetBlockEntity(pumpPos)
      is BlockEntityManualFluidPump be
    )
      be.OnPumpStop();
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) => InteractionHelp(world, selection, forPlayer);

  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => InteractionHelp(world, principalSel, forPlayer);

  private WorldInteraction[] InteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
    var help = new List<WorldInteraction>(
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? []
    );
    help.Add(
      new WorldInteraction {
        ActionLangCode = "lpex:blockhelp-manualfluidpump-crank",
        MouseButton = EnumMouseButton.Right,
        RequireFreeHand = true,
      }
    );
    return help.ToArray();
  }

  #endregion
}
