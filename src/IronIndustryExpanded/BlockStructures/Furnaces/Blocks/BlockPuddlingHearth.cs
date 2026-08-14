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
/// The puddling furnace's hearth - a cast-iron bottom plate three cells wide, fettled with iron oxide and
/// charged with pig through the door. A mega-block whose two flanking cells are fillers; the clicked cell
/// selects the row being worked, which is what the layout's slab shoulders make reachable for all three.
/// </summary>
[BlockRegister]
public partial class BlockPuddlingHearth
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
          "furnace/puddlinghearth"
        )
        .Class<BlockPuddlingHearth>()
        .EntityClass<BlockEntityPuddlingHearth>()
        .Material(EnumBlockMaterial.Metal)
        .MaxStackSize(1)
        .Behavior("MultiblockStructure")
        .Behavior("ExOrientable")
        // `type` names the family member; every furnace part shares the code `iiex:furnace`
        // (see BlockFurnaceCoreBase.FurnaceCode).
        .VariantGroup("type", "puddlinghearth")
        .SideVariant()
        .ShapeByTypePerOrientation("iiex:furnace/puddlinghearth", 0)
        .CreativeCommon("*-n")
        // One filler each side: the bed is 3 cells across, one row per cell.
        .FillerOffsets(
          StructureFootprint.Layout(f => f.Origin(-1, 0).Layer(0, "#0#"))
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

  /// <summary>
  /// The row a clicked cell belongs to. The footprint turns with the block, so the world offset is rotated
  /// back into the hearth's own frame before it is read as a row; otherwise a hearth facing west has its
  /// left and right swapped.
  /// </summary>
  private HearthRows.Row? RowAt(BlockPos principal, BlockPos clicked) {
    Vec3i world = new(
      clicked.X - principal.X,
      clicked.Y - principal.Y,
      clicked.Z - principal.Z
    );
    Vec3i local = ExOrientation.RotateOffset(world, -StructureAngle);
    return HearthRows.FromLocalOffset(local);
  }

  private bool HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos principal,
    BlockPos clicked
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(principal)
      is not BlockEntityPuddlingHearth be
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

    ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
    string? held = active?.Itemstack?.Collectible?.Code?.Path;
    var player = byPlayer as IServerPlayer;

    // Fettle before pig: a row charged onto a bare plate would weld to it, so fettling is a per-heat cost
    // rather than part of the build.
    if (held == "puddlingfettle") {
      if (be.TryFettle(row))
        active!.TakeOut(1);
      else
        player?.SendIngameError("iiex-hearth-cannotfettle");
      return true;
    }

    if (held == "pig") {
      if (be.TryChargePig(row))
        active!.TakeOut(1);
      else
        player?.SendIngameError(
          be.NeedsFettle(row)
            ? "iiex-hearth-needsfettle"
            : "iiex-hearth-cannotcharge"
        );
      return true;
    }

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
        ActionLangCode = "iiex:hearth-help-fettle",
        MouseButton = EnumMouseButton.Right,
        Itemstacks = StackOf("puddlingfettle"),
      },
      new()
      {
        ActionLangCode = "iiex:hearth-help-charge",
        MouseButton = EnumMouseButton.Right,
        Itemstacks = StackOf("pig"),
      },
    ];

  private ItemStack[] StackOf(string path) {
    Item? item = api.World.GetItem(new AssetLocation("iiex", path));
    return item == null ? [] : [new ItemStack(item)];
  }

  #endregion
}
