using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Structures;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockStructures.Boiler;

/// <summary>
/// Shared base for the boiler mega-blocks. Each occupies one grid cell but renders across a
/// multi-cell volume reserved with invisible structure fillers (so the player gets real
/// collision); construction is driven by the RightClickConstructable behavior in the block JSON.
/// </summary>
public abstract class BlockBoiler
  : BlockFilledMegastructure,
    INetworkConnector,
    IFillerInteractionTarget,
    IBoilerGeometry {
  // Boiler geometry offsets, read at runtime from the block's own attributes, populated from the JSON file
  // or the injected code-first def alike. Implemented here so the Lancashire and Cornish leaves share one
  // copy and a leaf can drop its blocktype JSON without losing these accessors.
  public JsonObject? FuelOffset => Attributes?["fuelOffset"];
  public JsonObject? ExhaustOutletOffset => Attributes?["exhaustOutletOffset"];
  public JsonObject? MainHatchOffset => Attributes?["mainHatchOffset"];
  public JsonObject? ManHatchOffset => Attributes?["manHatchOffset"];
  public JsonObject? SteamConnectorOffset =>
    Attributes?["steamConnectorOffset"];
  public JsonObject? LightSampleOffset => Attributes?["lightSampleOffset"];
  public JsonObject? ExplosionCenterOffset =>
    Attributes?["explosionCenterOffset"];
  public JsonObject? WaterRendererBox => Attributes?["waterRendererBox"];
  public string? FeedwaterFace => Attributes?["feedwaterFace"].AsString();

  /// <summary>
  /// The offset <see cref="Angle"/> adds to the variant's side angle, raising the body away from the
  /// player. A leaf that passes this same number to <see cref="BoilerShell"/> as its shape spin draws
  /// its mesh in the frame its footprint is placed in; one that passes anything else does not.
  /// </summary>
  protected const int BodySpinOffset = 180;

  // The footprint, the geometry offsets and the connector faces are authored in the same frame the art
  // is drawn in, and both are turned by this angle - the shape through its own rotateYByType, which the
  // leaf sets to match.
  private int Angle =>
    (ExOrientation.AngleFromSide(Variant["side"]) + BodySpinOffset) % 360;

  /// <summary>The rotation applied to the footprint, the geometry offsets and the connector faces.</summary>
  public override int StructureAngle => Angle;

  // Water is drawn through a pipe on the principal's own feedwater face; steam and exhaust leave via
  // their footprint port cells, which answer for the boiler through their own filler entities.
  public string NetworkType => "pipe";

  /// <summary>
  /// World face the feedwater pipe couples to: the declared north-orientation
  /// <see cref="IBoilerGeometry.FeedwaterFace"/> turned into the placed orientation, so the same
  /// authored layout plumbs the same way whichever way the vessel is laid.
  /// </summary>
  public BlockFacing FeedwaterWorldFace =>
    ExOrientation.RotateFacing(
      ExOrientation.FacingFromSide(Geo.FeedwaterFace) ?? BlockFacing.DOWN,
      Angle
    );

  /// <summary>
  /// World face the exhaust outlet cell carries its port on, or <c>null</c> when that cell declares no
  /// port and the outlet is a real node block the player set there instead. Read back off the
  /// footprint's own declaration so the face a machine probes across and the face the cell answers on
  /// cannot drift apart, and turned by <see cref="Angle"/> because the cell rotates with the machine
  /// while a facing written in code would not.
  /// </summary>
  public BlockFacing? ExhaustWorldFace =>
    PortWorldFaceAt(Geo.ExhaustOutletOffset);

  /// <summary>
  /// World face the steam connector cell carries its port on, or <c>null</c> when that cell declares no
  /// port. Read back off the footprint for the same reason <see cref="ExhaustWorldFace"/> is: the cell
  /// answers a pipe on the face its own declaration names, so a steam push that named a face in code
  /// would go on pushing into the old one after the footprint moved the port.
  /// </summary>
  public BlockFacing? SteamWorldFace =>
    PortWorldFaceAt(Geo.SteamConnectorOffset);

  private BlockFacing? PortWorldFaceAt(JsonObject? offsetNode) {
    Vec3i want = ExOrientation.ReadOffset(offsetNode, Vec3i.Zero);
    foreach (FillerOffset cell in StructureFillers.ReadOffsets(FillerOffsets)) {
      if (
        cell.Offset.X != want.X
        || cell.Offset.Y != want.Y
        || cell.Offset.Z != want.Z
        || cell.PortFace == null
      )
        continue;
      return ExOrientation.FacingFromSide(cell.PortFace) is { } face
        ? ExOrientation.RotateFacing(face, Angle)
        : null;
    }
    return null;
  }

  public bool HasConnectorAt(BlockFacing face) => face == FeedwaterWorldFace;

  /// <summary>This boiler's geometry offsets (implemented on the base above; the leaves inherit them).</summary>
  private IBoilerGeometry Geo => this;

  /// <summary>
  /// The code-first surface both boiler variants share: material, sounds, break resistance, the
  /// orientable and interact behaviors, the Animatable entity behavior, the side variant group, one base
  /// shape spun per orientation, and the non-solid flags. Each leaf's <c>Definitions</c> starts here and
  /// overlays only what differs - mining tier, geometry offsets, filler footprint, the elements its own
  /// art raises first, and construction stages.
  /// <para>
  /// <paramref name="spinOffset"/> must equal the offset <see cref="Angle"/> adds to the side angle, or
  /// the mesh is drawn in one frame while the fillers, connectors and interaction cells are placed in
  /// another. It is a per-leaf number because the two vessels' art grows along opposite axes.
  /// <c>BoilerFootprintGuards</c> asserts the pairing for the Cornish.
  /// </para>
  /// </summary>
  protected static ExBlockDef BoilerShell(
    ExBlockDef def,
    string shapeBase,
    int spinOffset
  ) =>
    def.Material(EnumBlockMaterial.Metal)
      .MetalSounds()
      .Resistance(45f)
      .MaxStackSize(1)
      .NoDrops()
      .Behavior("ExOrientable")
      .Behavior("BlockEntityInteract")
      .EntityBehavior("Animatable")
      .SideVariant()
      .CreativeCommon("*-n")
      .ShapeSpunPerOrientation(shapeBase, spinOffset)
      .NonSolid();

  // Each offset lives solely in the def attribute - both boiler variants author all of them - so there is
  // no hand-kept fallback to drift from it: a missing attribute resolves to the origin.
  private BlockPos OffsetWorldPos(BlockPos boilerPos, JsonObject? offsetNode) =>
    ExOrientation.WorldPosFromAttr(boilerPos, offsetNode, Vec3i.Zero, Angle);

  /// <summary>World cell of the firebox slot.</summary>
  public BlockPos FuelWorldPos(BlockPos boilerPos) =>
    OffsetWorldPos(boilerPos, Geo.FuelOffset);

  /// <summary>World cell of the exhaust gas outlet.</summary>
  public BlockPos ExhaustOutletWorldPos(BlockPos boilerPos) =>
    OffsetWorldPos(boilerPos, Geo.ExhaustOutletOffset);

  /// <summary>World cell of the main (firing) hatch: where the bed is charged, lit and shut in.</summary>
  public BlockPos MainHatchWorldPos(BlockPos boilerPos) =>
    OffsetWorldPos(boilerPos, Geo.MainHatchOffset);

  /// <summary>World cell of the man hatch: bucket fill and drain, and the emergency steam vent.</summary>
  public BlockPos ManHatchWorldPos(BlockPos boilerPos) =>
    OffsetWorldPos(boilerPos, Geo.ManHatchOffset);

  /// <summary>
  /// World cell of the steam connector (the port filler on the body); the steam pipe attaches in the
  /// cell across that port's <see cref="SteamWorldFace"/>.
  /// </summary>
  public BlockPos SteamPipeWorldPos(BlockPos boilerPos) =>
    OffsetWorldPos(boilerPos, Geo.SteamConnectorOffset);

  /// <summary>
  /// World cell the animated vessel mesh is lit from, read from <c>lightSampleOffset</c> and rotated by
  /// angle. It points at a body cell instead of the firebox-adjacent cell vanilla would light the whole
  /// footprint from, which tints the vessel red at night.
  /// </summary>
  public BlockPos LightSampleWorldPos(BlockPos boilerPos) =>
    OffsetWorldPos(boilerPos, Geo.LightSampleOffset);

  /// <summary>
  /// World cell at the footprint centre, where the burst explosion is centred so it goes off
  /// inside the boiler. Read from <c>explosionCenterOffset</c>, rotated by angle.
  /// </summary>
  public BlockPos ExplosionCenterPos(BlockPos boilerPos) =>
    OffsetWorldPos(boilerPos, Geo.ExplosionCenterOffset);

  /// <summary>Removes the boiler's reserved filler footprint (used by the explosion path).</summary>
  public void RemoveStructure(IWorldAccessor world, BlockPos pos) =>
    StructureFillers.RemoveFillers(world, pos, FootprintCells(pos));

  // Placement, the filler footprint and break-time filler removal are handled by
  // BlockFilledMegastructure; each boiler's own footprint declares the port cells it couples on.

  // A broken boiler returns only its construction materials, scattered by the RightClickConstructable
  // behaviour at brokenDropsRatio, never the boiler block itself. The JSON "drops": [] declares this but is
  // not reliably honoured for a variant block - the per-pressure variant can still be handed its own code
  // as a fallback drop at registration - so the empty drop list is enforced here.
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [];

  #region Hatch interactions

  // The boiler answers on two footprint cells. The main hatch (MainHatchWorldPos) is the firing door:
  // hold to swing it, charge the internal bed through it, light a full bed, hold again to shut it. The
  // man hatch (ManHatchWorldPos) is the water access: bucket fill, bucket drain, and the vent that
  // bleeds steam off while it stands open. A boiler with no internal bed has no main hatch at all -
  // its fuel is a block the player tends directly - so that cell falls through to the default.
  // Interactions arrive on the boiler's own cell or forwarded from a filler; both funnel into the
  // Handle* helpers, which route on the clicked cell and otherwise defer to the default behavior.

  /// <summary>Hold duration (seconds) required to swing a hatch, or to light a charged bed.</summary>
  private const float HatchHoldSeconds = 0.5f;

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    HandleInteractStart(world, byPlayer, blockSel, blockSel.Position)
    ?? base.OnBlockInteractStart(world, byPlayer, blockSel);

  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) =>
    HandleInteractStart(world, byPlayer, principalSel, clickedCell)
    ?? base.OnBlockInteractStart(world, byPlayer, principalSel);

  /// <summary>
  /// The boiler behind <paramref name="sel"/>, once it is finished. <c>null</c> hands the click back to
  /// the default behavior, which is what drives the construction stages before the vessel stands.
  /// </summary>
  private static BlockEntityBoiler? InteractableBoiler(
    IWorldAccessor world,
    BlockSelection sel
  ) =>
    world.BlockAccessor.GetBlockEntity(sel.Position)
      is BlockEntityBoiler { IsConstructed: true } be
      ? be
      : null;

  /// <summary>
  /// Shared hatch click logic. <paramref name="sel"/> is the boiler's own cell (for BE lookup);
  /// <paramref name="clickedCell"/> is the cell looked at. Returns <c>null</c> to defer.
  /// </summary>
  private bool? HandleInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection sel,
    BlockPos clickedCell
  ) {
    if (InteractableBoiler(world, sel) is not { } be)
      return null;

    ItemSlot? slot = byPlayer.InventoryManager?.ActiveHotbarSlot;
    bool server = world.Side == EnumAppSide.Server;

    if (be.Bed != null && clickedCell.Equals(MainHatchWorldPos(sel.Position))) {
      // Fuel in hand at an open firing door → charge the bed a stack at a time.
      if (be.MainHatchOpen && slot is { Empty: false }) {
        if (server)
          be.TryChargeBed(byPlayer, slot);
        return true;
      }
      // Empty hands → begin the hold; swinging or lighting happens past the threshold.
      if (slot?.Empty != false) {
        be.MainHatchToggled = false;
        return true;
      }
      return null;
    }

    if (!clickedCell.Equals(ManHatchWorldPos(sel.Position)))
      return null;

    // A water container while the man hatch is open → pour its entire contents in.
    if (be.ManHatchOpen && IsWaterContainer(slot?.Itemstack)) {
      if (server && slot != null)
        be.TryManualFill(byPlayer, slot);
      return true;
    }

    // An empty liquid container while the man hatch is open → bail water out into it.
    if (be.ManHatchOpen && IsEmptyLiquidContainer(slot?.Itemstack)) {
      if (server && slot != null)
        be.TryManualDrain(byPlayer, slot);
      return true;
    }

    // Empty hands → begin the hold; the toggle happens in the step loop past the threshold.
    if (slot?.Empty != false) {
      be.ManHatchToggled = false;
      return true;
    }

    return null;
  }

  public override bool OnBlockInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    HandleInteractStep(
      secondsUsed,
      world,
      byPlayer,
      blockSel,
      blockSel.Position
    ) ?? base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);

  bool IFillerInteractionTarget.OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) =>
    HandleInteractStep(secondsUsed, world, byPlayer, principalSel, clickedCell)
    ?? base.OnBlockInteractStep(secondsUsed, world, byPlayer, principalSel);

  private bool? HandleInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection sel,
    BlockPos clickedCell
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(sel.Position)
        is not BlockEntityBoiler be
      || !be.IsConstructed
    )
      return null;

    bool main =
      be.Bed != null && clickedCell.Equals(MainHatchWorldPos(sel.Position));
    if (!main && !clickedCell.Equals(ManHatchWorldPos(sel.Position)))
      return null;

    // Only the empty-handed hold uses the step loop; a held item ends the interaction at once.
    if (byPlayer.InventoryManager?.ActiveHotbarSlot?.Empty != true)
      return false;

    // Act once past the threshold, then keep returning true until release so the engine
    // doesn't restart the interaction (which would swing the hatch repeatedly).
    bool held = main ? be.MainHatchToggled : be.ManHatchToggled;
    if (secondsUsed >= HatchHoldSeconds && !held) {
      if (main)
        be.MainHatchToggled = true;
      else
        be.ManHatchToggled = true;
      if (world.Side == EnumAppSide.Server) {
        // An open door over a charged, unlit bed is the firing gesture; anything else swings it.
        if (main && be.CanLightBed)
          be.LightBed();
        else if (main)
          be.ToggleMainHatch();
        else
          be.ToggleManHatch();
      }
    }

    return true;
  }

  public override void OnBlockInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (!HandleInteractStop(world, byPlayer, blockSel))
      base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
  }

  void IFillerInteractionTarget.OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) {
    if (!HandleInteractStop(world, byPlayer, principalSel))
      base.OnBlockInteractStop(secondsUsed, world, byPlayer, principalSel);
  }

  private bool HandleInteractStop(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection sel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(sel.Position)
        is not BlockEntityBoiler be
      || !be.IsConstructed
    )
      return false;

    be.MainHatchToggled = false;
    be.ManHatchToggled = false;
    return true;
  }

  private static bool IsWaterContainer(ItemStack? stack) {
    if (stack?.Collectible is not BlockLiquidContainerBase cont)
      return false;
    ItemStack? content = cont.GetContent(stack);
    return content?.Collectible?.Code?.Path?.Contains("water") == true;
  }

  /// <summary>An empty liquid container (e.g. an empty bucket) - the tool used to bail water out.</summary>
  private static bool IsEmptyLiquidContainer(ItemStack? stack) {
    if (stack?.Collectible is not BlockLiquidContainerBase cont)
      return false;
    return cont.GetContent(stack) == null;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
    var help = HandleInteractionHelp(
      world,
      selection,
      forPlayer,
      selection.Position
    );
#if !GAME_GE_1_22
    // Legacy lacks the vanilla IInteractableWithHelp path, so surface the construction help here.
    help = ExpandedLib.Blocks.ExRightClickConstructable.AppendConstructionHelp(
      world,
      selection,
      help
    );
#endif
    return help;
  }

  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => HandleInteractionHelp(world, principalSel, forPlayer, clickedCell);

  private WorldInteraction[] HandleInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) {
    var help = new List<WorldInteraction>(
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? []
    );

    // The hatch hints only show on a finished vessel, and each cell advertises its own.
    if (
      world.BlockAccessor.GetBlockEntity(selection.Position)
      is not BlockEntityBoiler { IsConstructed: true } be
    )
      return help.ToArray();

    if (
      be.Bed != null
      && clickedCell.Equals(MainHatchWorldPos(selection.Position))
    ) {
      // Lighting takes the same empty-handed hold as swinging the door, so only one of the two is
      // advertised at a time - whichever the next hold will actually do.
      help.Add(
        new WorldInteraction {
          ActionLangCode = be.CanLightBed
            ? "iiex:blockhelp-boiler-ignite"
            : "iiex:blockhelp-boiler-mainhatch",
          MouseButton = EnumMouseButton.Right,
          RequireFreeHand = true,
        }
      );
      // Fuel only goes in through an open firing door, so only advertise it then.
      if (be.MainHatchOpen)
        help.Add(
          new WorldInteraction {
            ActionLangCode = "iiex:blockhelp-boiler-charge",
            MouseButton = EnumMouseButton.Right,
          }
        );
      return help.ToArray();
    }

    if (!clickedCell.Equals(ManHatchWorldPos(selection.Position)))
      return help.ToArray();

    // Empty-handed right click swings the man hatch.
    help.Add(
      new WorldInteraction {
        ActionLangCode = "iiex:blockhelp-boiler-manhatch",
        MouseButton = EnumMouseButton.Right,
        RequireFreeHand = true,
      }
    );

    // The manual water fill/drain only work while the man hatch is open, so only advertise them then.
    if (be.ManHatchOpen) {
      help.Add(
        new WorldInteraction {
          ActionLangCode = "iiex:blockhelp-boiler-fill",
          MouseButton = EnumMouseButton.Right,
          Itemstacks = WaterContainerStacks(world),
        }
      );
      help.Add(
        new WorldInteraction {
          ActionLangCode = "iiex:blockhelp-boiler-drain",
          MouseButton = EnumMouseButton.Right,
          Itemstacks = EmptyContainerStacks(world),
        }
      );
    }

    return help.ToArray();
  }

  // True for the vanilla wood-bucket family, matched on the code path because those blocks carry no
  // attribute to key on without a JSON patch. The substring keeps every bucket variant in the
  // fill/drain interaction hints.
  private static bool IsWoodBucket(Block? block) =>
    block?.Code != null && block.Code.Path.Contains("woodbucket");

  /// <summary>Water-filled liquid containers shown on the manual-fill interaction hint, resolved once.</summary>
  private static ItemStack[]? _waterContainerStacks;

  private static ItemStack[] WaterContainerStacks(IWorldAccessor world) {
    if (_waterContainerStacks != null)
      return _waterContainerStacks;

    var waterStack = new ItemStack(
      world.GetItem(new AssetLocation("game:waterportion"))
    );
    var list = new List<ItemStack>();
    foreach (var block in world.Blocks) {
      if (block is not BlockLiquidContainerBase cont || !IsWoodBucket(block))
        continue;
      var bucket = new ItemStack(block);
      cont.SetContent(bucket, waterStack);
      list.Add(bucket);
    }
    // Fall back to a bare water portion if no fillable bucket resolved.
    if (list.Count == 0 && waterStack.Collectible != null)
      list.Add(waterStack);

    return _waterContainerStacks = list.ToArray();
  }

  /// <summary>Empty liquid containers shown on the manual-drain interaction hint, resolved once.</summary>
  private static ItemStack[]? _emptyContainerStacks;

  private static ItemStack[] EmptyContainerStacks(IWorldAccessor world) {
    if (_emptyContainerStacks != null)
      return _emptyContainerStacks;

    var list = new List<ItemStack>();
    foreach (var block in world.Blocks) {
      if (block is not BlockLiquidContainerBase || !IsWoodBucket(block))
        continue;
      list.Add(new ItemStack(block));
    }

    return _emptyContainerStacks = list.ToArray();
  }

  #endregion
}
