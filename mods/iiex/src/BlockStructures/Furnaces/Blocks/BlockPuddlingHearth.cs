using System.Collections.Generic;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.Items;
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
        // One filler each side: the bed is 3 cells across, one row per cell. The course above is the
        // low roof over the bed, which is what makes the hearth a chamber rather than a plate.
        .FillerOffsets(
          StructureFootprint.Layout(f =>
            f.Origin(-1, 0).Layer(0, "#0#").Layer(1, "###")
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
    AssetLocation? held = active?.Itemstack?.Collectible?.Code;
    var player = byPlayer as IServerPlayer;

    // Fettle before pig: a row charged onto a bare plate would weld to it, so fettling is a per-heat cost
    // rather than part of the build.
    if (Holds(held, FettleItemDefinitions.Code)) {
      if (be.TryFettle(row))
        active!.TakeOut(1);
      else
        player?.SendIngameError("iiex-hearth-cannotfettle");
      return true;
    }

    if (Holds(held, ItemPig.Code)) {
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

    if (Holds(held, PuddlingToolItemDefinitions.RabbleCode))
      return Rabble(world, be, player);

    if (Holds(held, PuddlingToolItemDefinitions.PaddleCode))
      return DrawOut(world, byPlayer, be, player);

    // Empty-handed on a bed that is worked out: the clean-out, which is where the spent fettle and the
    // tap cinder come from. Refused while a ball is still standing, or the heat's metal would be swept
    // away with the waste.
    if (held is null && be.IsWorkedOut)
      return CleanOut(world, byPlayer, be, player);

    return true;
  }

  /// <summary>
  /// Rakes the bed out: the spent fettling and the tap cinder together, which is the whole "cleaned, not
  /// tapped" decision. The cinder grid-crafts straight back into the next heat's fettling.
  /// </summary>
  private bool CleanOut(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntityPuddlingHearth be,
    IServerPlayer? player
  ) {
    if (be.BallsOnBed > 0) {
      player?.SendIngameError("iiex-hearth-ballstodraw");
      return true;
    }

    int cinder = IiexValues.PuddlingCinderPerHeat;
    be.ClearBed();
    Give(world, byPlayer, be, FettleItemDefinitions.TapCinderCode, cinder);
    return true;
  }

  private void Give(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntityPuddlingHearth be,
    string path,
    int count
  ) {
    if (count <= 0)
      return;
    Item? item = world.GetItem(new AssetLocation(Code.Domain, path));
    if (item == null)
      return;
    var stack = new ItemStack(item, count);
    if (byPlayer.InventoryManager?.TryGiveItemstack(stack) != true)
      world.SpawnItemEntity(stack, be.Pos.ToVec3d().Add(0.5, 1.0, 0.5));
  }

  /// <summary>
  /// Whether the held item is <paramref name="path"/> in this hearth's own domain. Matched on domain and
  /// path together: on path alone, any mod shipping an item pathed <c>pig</c> would charge this hearth.
  /// The domain is read off the block rather than written down, so a mod reusing this class gets its own.
  /// </summary>
  private bool Holds(AssetLocation? held, string path) =>
    held is not null && held.Domain == Code.Domain && held.Path == path;

  /// <summary>
  /// One rabbling stroke, through the small working door. The door is the gate: the bath is worked
  /// through it precisely so the main door can stay shut and the heat stay in.
  /// </summary>
  private bool Rabble(
    IWorldAccessor world,
    BlockEntityPuddlingHearth be,
    IServerPlayer? player
  ) {
    if (be.Furnace is not { } furnace)
      return true;
    if (!furnace.SmallDoorOpen) {
      player?.SendIngameError("iiex-hearth-doorshut");
      return true;
    }
    if (!be.StrokeReady) {
      player?.SendIngameError("iiex-hearth-toosoon");
      return true;
    }
    if (!be.TryRabble()) {
      player?.SendIngameError(
        !be.HasBath ? "iiex-hearth-nobath"
        : be.IsFrozen ? "iiex-hearth-bathfrozen"
        : "iiex-hearth-workedout"
      );
      return true;
    }
    be.MarkStroke();
    furnace.PlayWorkingStroke(drawingOut: false);
    return true;
  }

  /// <summary>Draws one gathered ball out on the paddle, through the same door.</summary>
  private bool DrawOut(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntityPuddlingHearth be,
    IServerPlayer? player
  ) {
    if (be.Furnace is not { } furnace)
      return true;
    if (!furnace.SmallDoorOpen) {
      player?.SendIngameError("iiex-hearth-doorshut");
      return true;
    }
    if (!be.StrokeReady) {
      player?.SendIngameError("iiex-hearth-toosoon");
      return true;
    }
    if (!be.TryDrawBall()) {
      player?.SendIngameError("iiex-hearth-noball");
      return true;
    }
    be.MarkStroke();

    Item? ball = world.GetItem(
      new AssetLocation(Code.Domain, WroughtBallItemDefinitions.Code)
    );
    if (ball != null) {
      var stack = new ItemStack(ball);
      // Straight off the bed at whatever the bath is holding, and the engine cools it in the hand from
      // there. Not a constant: the ball has to leave hot enough to shingle without a reheat, which is the
      // whole reason it goes straight under the helve, and a furnace barely holding its process
      // temperature should hand out a cooler ball than one running properly.
      stack.Collectible.SetTemperature(world, stack, furnace.BathTemperature);
      if (byPlayer.InventoryManager?.TryGiveItemstack(stack) != true)
        world.SpawnItemEntity(stack, be.Pos.ToVec3d().Add(0.5, 1.0, 0.5));
    }
    furnace.PlayWorkingStroke(drawingOut: true);
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
        ActionLangCode = IiexLang.HearthHelpFettle,
        MouseButton = EnumMouseButton.Right,
        Itemstacks = StackOf(FettleItemDefinitions.Code),
      },
      new()
      {
        ActionLangCode = IiexLang.HearthHelpCharge,
        MouseButton = EnumMouseButton.Right,
        Itemstacks = StackOf(ItemPig.Code),
      },
      new()
      {
        ActionLangCode = IiexLang.HearthHelpRabble,
        MouseButton = EnumMouseButton.Right,
        Itemstacks = StackOf(PuddlingToolItemDefinitions.RabbleCode),
      },
      new()
      {
        ActionLangCode = IiexLang.HearthHelpDraw,
        MouseButton = EnumMouseButton.Right,
        Itemstacks = StackOf(PuddlingToolItemDefinitions.PaddleCode),
      },
      new()
      {
        ActionLangCode = IiexLang.HearthHelpClean,
        MouseButton = EnumMouseButton.Right,
      },
    ];

  private ItemStack[] StackOf(string path) {
    Item? item = api.World.GetItem(new AssetLocation(Code.Domain, path));
    return item == null ? [] : [new ItemStack(item)];
  }

  #endregion
}
