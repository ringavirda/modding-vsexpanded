using System.Collections.Generic;
using ExpandedLib.Networks;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Catalogues;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockStructures.Forming.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockStructures.Forming.Blocks;

/// <summary>
/// The crop shear: a 3x1x2 megablock and an mpenergy consumer that turns stock at a named stage into one
/// product plus the remainder. It terminates every rolling schedule, which is what lets the mill be a pure
/// reduction machine - two verbs, two stations. The simulation lives in <see cref="BlockEntityShear"/>.
/// See docs/design/machines/shear.md.
/// <para>
/// Footprint, authored in the <c>ns</c> (shaft-along-Z) frame: the blade nest at <c>x=-1</c> carries the
/// window interaction, the principal <c>(0,0,0)</c> is the consumer node whose two shaft faces take the
/// drive, the gear cell sits at <c>x=+1</c>, and three floor slabs cap the row. Drawn from the owner's
/// layout in workbench/machines.txt.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockShear
  : BlockNetworkNode,
    IExBlockDefProvider,
    IFillerHost,
    IFillerInteractionTarget {
  public override string NetworkType => "mpenergy";

  #region Code-first definition

  /// <summary>The shear blocktype: two horizontal orientations, the shaft running along the orientation
  /// axis so its two faces carry the drive connectors. Authored in the <c>ns</c> frame (<c>rotateY:0</c>),
  /// which is the frame the art is drawn in; <c>we</c> is the 90 degree rotation.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "forming", "forming/shear")
        .Class<BlockShear>()
        .EntityClass<BlockEntityShear>()
        .Material(EnumBlockMaterial.Metal)
        .Sound("walk", "game:walk/metal")
        .Sound("place", "game:block/anvil")
        .MaxStackSize(1)
        .Handbook("forming-shear-*")
        .VariantGroup("type", "shear")
        .VariantGroup("orientation", "ns", "we")
        .NetworkOriented()
        // ns = shaft along Z (authored, rotateY 0); we = the 90 degree rotation.
        .ShapeByType("*-ns", "iiex:forming/shear", rotateY: 0)
        .ShapeByType("*-we", "iiex:forming/shear", rotateY: 90)
        .CreativeCommon("*-ns")
        .FillerOffsets(Footprint)
        .EntityBehavior("Animatable")
        // The guillotine overhangs its cell; the placed cell is solid but must not cull neighbour faces.
        .SolidNonOpaque(),
    ];

  #endregion

  #region Footprint

  /// <summary>
  /// The cells the shear reserves besides its principal, in the <c>ns</c> frame. The blade nest
  /// (<c>x=-1</c>) and the gear cell (<c>x=+1</c>) are full fillers; the row above is three floor slabs,
  /// so the machine stands a cell and a half tall without walling the space over it.
  /// </summary>
  private static readonly IReadOnlyList<FillerCellSpec> Footprint =
    StructureFootprint.Layout(f =>
      f.Solid('I')
        .Slab('_', BlockFacing.DOWN)
        .Origin(-1, 1)
        // Rows are -Y (top row y=1), columns +X (x=-1..1); 'O' is the principal, skipped.
        .Face(
          0,
          """
          _ _ _
          I O #
          """
        )
    );

  /// <summary>The blade-nest cell, in the <c>ns</c> frame - where the player feeds stock and swaps
  /// blades.</summary>
  private static readonly Vec3i NestOffset = new(-1, 0, 0);

  /// <summary>The <c>fillerOffsets</c> attribute (from the code-first def), read by the shared helper.</summary>
  public JsonObject? FillerOffsets => Attributes?["fillerOffsets"];

  /// <summary>Rotation in degrees applied to the <c>ns</c>-frame footprint to reach the placed
  /// orientation: <c>ns</c> 0 (authored), <c>we</c> 90, matching the per-orientation shape rotations.</summary>
  public int StructureAngle => Variant?["orientation"] == "we" ? 90 : 0;

  private List<FillerCell> FootprintCells(BlockPos pos) =>
    StructureFillers.FootprintCells(this, pos, StructureAngle);

  /// <summary>The world position of the blade nest for a shear at <paramref name="pos"/>, rotated into the
  /// placed orientation.</summary>
  public BlockPos NestCell(BlockPos pos) {
    Vec3i r = ExpandedLib.Helpers.ExOrientation.RotateOffset(
      NestOffset,
      StructureAngle
    );
    return pos.AddCopy(r.X, r.Y, r.Z);
  }

  public override bool CanPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref string failureCode
  ) {
    if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
      return false;

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
    if (world.Side == EnumAppSide.Server)
      StructureFillers.PlaceFillers(world, blockPos, FootprintCells(blockPos));
  }

  public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos) {
    // Every removal path, not just a player break, so the reserved volume is never left behind as orphan
    // solid cells - the same reason the mill overrides this rather than OnBlockBroken.
    if (world.Side == EnumAppSide.Server)
      StructureFillers.RemoveFillers(world, pos, FootprintCells(pos));
    base.OnBlockRemoved(world, pos);
  }

  #endregion

  #region Interaction

  /// <summary>
  /// Routes a click on the footprint.
  /// <list type="bullet">
  ///   <item>Holding a wrench with a piece under the blades: free it, unchanged.</item>
  ///   <item>Holding a blade set, anywhere on the machine: fit it, swapping out any set already there.</item>
  ///   <item>Holding stock on the blade nest: crop it.</item>
  ///   <item>Sneaking empty-handed on the nest: take the fitted blades back.</item>
  /// </list>
  /// </summary>
  public bool OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => HandleInteract(world, byPlayer, principalSel, clickedCell);

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    HandleInteract(world, byPlayer, blockSel, blockSel.Position)
    || base.OnBlockInteractStart(world, byPlayer, blockSel);

  private bool HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection sel,
    BlockPos clickedCell
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(sel.Position)
      is not BlockEntityShear shear
    )
      return false;

    ItemSlot? slot = byPlayer.InventoryManager?.ActiveHotbarSlot;
    ItemStack? held = slot?.Itemstack;

    if (IsWrench(held) && shear.IsStroking)
      return FreeStuckPiece(world, byPlayer, shear);
    if (MachineTool.IsTool(held))
      return FitBlades(world, byPlayer, shear, slot!);
    if (held == null)
      return byPlayer.Entity.Controls.ShiftKey
        && TakeBlades(world, byPlayer, shear);

    return clickedCell.Equals(NestCell(sel.Position))
      && Crop(world, byPlayer, shear, held);
  }

  private static bool IsWrench(ItemStack? stack) =>
    stack?.Collectible?.Code?.FirstCodePart() == "wrench";

  private static bool FreeStuckPiece(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntityShear shear
  ) {
    if (world.Side != EnumAppSide.Server)
      return true;

    ItemStack? freed = shear.ReleaseStuckPiece();
    if (freed != null && !byPlayer.InventoryManager.TryGiveItemstack(freed))
      world.SpawnItemEntity(freed, byPlayer.Entity.Pos.XYZ);
    return true;
  }

  private static bool FitBlades(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntityShear shear,
    ItemSlot slot
  ) {
    if (world.Side != EnumAppSide.Server)
      return true;

    ItemStack offered = slot.TakeOut(1);
    if (!shear.TryFitBlades(offered, out ItemStack? previous)) {
      // Refused mid-stroke: the blade the player offered goes straight back, or a swap attempted with a
      // piece under the blades would eat it.
      slot.Itemstack = offered;
      slot.MarkDirty();
      return true;
    }

    slot.MarkDirty();
    if (
      previous != null
      && !byPlayer.InventoryManager.TryGiveItemstack(previous)
    )
      world.SpawnItemEntity(previous, byPlayer.Entity.Pos.XYZ);
    return true;
  }

  private static bool TakeBlades(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntityShear shear
  ) {
    if (world.Side != EnumAppSide.Server)
      return true;
    if (!shear.TryFitBlades(null, out ItemStack? previous) || previous == null)
      return true;

    if (!byPlayer.InventoryManager.TryGiveItemstack(previous))
      world.SpawnItemEntity(previous, byPlayer.Entity.Pos.XYZ);
    return true;
  }

  private static bool Crop(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockEntityShear shear,
    ItemStack held
  ) {
    if (world.Side != EnumAppSide.Server)
      return true;

    ShearDecision decision = shear.TryCrop(held);
    if (!decision.Accepted) {
      Report(byPlayer, decision.Verdict);
      return true;
    }

    // The offered stack is consumed one at a time: the piece under the blades is the machine's now, and
    // the remainder comes back off the stroke rather than out of the player's hand.
    byPlayer.InventoryManager?.ActiveHotbarSlot?.TakeOut(1);
    byPlayer.InventoryManager?.ActiveHotbarSlot?.MarkDirty();
    return true;
  }

  /// <summary>A crop is a single click, so neither hold step matters here.</summary>
  public bool OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => false;

  /// <inheritdoc cref="OnFillerInteractStep"/>
  public void OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) { }

  /// <summary>The help shown against the blade nest. Only the nest takes stock, so clicking anywhere else
  /// on the footprint offers nothing to explain.</summary>
  public WorldInteraction[] GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(principalSel.Position)
        is not BlockEntityShear
      || !clickedCell.Equals(NestCell(principalSel.Position))
    )
      return [];

    return
    [
      new WorldInteraction
      {
        ActionLangCode = "iiex:shear-help-crop",
        MouseButton = EnumMouseButton.Right,
      },
      new WorldInteraction
      {
        ActionLangCode = "iiex:shear-help-takeblades",
        MouseButton = EnumMouseButton.Right,
        HotKeyCode = "sneak",
      },
      new WorldInteraction
      {
        ActionLangCode = "iiex:shear-help-free",
        MouseButton = EnumMouseButton.Right,
        Itemstacks = [],
      },
    ];
  }

  /// <summary>Tells the player which refusal they hit, since every one has a different fix. The order
  /// <see cref="ShearFeed.Decide"/> reports them in is the order they can be fixed in.</summary>
  private static void Report(IPlayer byPlayer, ShearVerdict verdict) =>
    (byPlayer as IServerPlayer)?.SendIngameError(
      verdict switch {
        ShearVerdict.NoBladeSet => "iiex-shear-nobladeset",
        ShearVerdict.NoJob => "iiex-shear-nojob",
        ShearVerdict.Spent => "iiex-shear-spent",
        ShearVerdict.BladeTooSoft => "iiex-shear-bladetoosoft",
        ShearVerdict.NotTurning => "iiex-shear-notturning",
        _ => "iiex-shear-notenoughdrive",
      }
    );

  #endregion
}
