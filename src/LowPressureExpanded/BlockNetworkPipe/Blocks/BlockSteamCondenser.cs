using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using LowPressureExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LowPressureExpanded.BlockNetworkPipe.Blocks;

/// <summary>
/// The steam condenser: a tjunction-shaped fixed port (not a network node) that recycles leftover
/// steam back into the water line. In north orientation west/east are the water line and north is
/// the steam line; steam drawn from the north condenses into the water passing W↔E. The three
/// adjacent runs stay separate networks (it's a connector, not a node); the BE bridges the two
/// water sides itself.
/// </summary>
[BlockRegister]
public partial class BlockSteamCondenser : Block, INetworkConnector, IExBlockDefProvider
{
  public string NetworkType => "pipe";

  /// <summary>The steam-condenser blocktype, authored in C# (migrated from pipes/steamcondenser.json).
  /// It orients via the vanilla <c>HorizontalOrientable</c> behavior, not the pipe orientation table.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [SteamCondenser(domain)];

  private static ExBlockDef SteamCondenser(string domain) =>
    ExBlockDef
      .Create(domain, "steamcondenser", "steamcondenser")
      .Class<BlockSteamCondenser>()
      .EntityClass<BlockEntitySteamCondenser>()
      .Material(EnumBlockMaterial.Metal)
      .MetalSounds()
      .MaxStackSize(8)
      .Behavior("ExOrientable")
      .SideVariant()
      .CreativeTab("general", "*-n")
      .CreativeTab("lpex", "*-n")
      .ShapeByType("*-n", "lpex:steamcondenser", rotateY: 0)
      .ShapeByType("*-e", "lpex:steamcondenser", rotateY: 270)
      .ShapeByType("*-s", "lpex:steamcondenser", rotateY: 180)
      .ShapeByType("*-w", "lpex:steamcondenser", rotateY: 90)
      .CollisionBox(0f, 0.3125f, 0.3125f, 1f, 0.6875f, 0.6875f)
      .CollisionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 0.3125f)
      .SelectionBox(0f, 0.3125f, 0.3125f, 1f, 0.6875f, 0.6875f)
      .SelectionBox(0.3125f, 0.3125f, 0f, 0.6875f, 0.6875f, 0.3125f)
      .RenderPass("OpaqueNoCull")
      .FaceCullMode("NeverCull")
      .LightAbsorption(0)
      .SideSolid(false)
      .SideOpaque(false);

  private int Angle => ExOrientation.AngleFromSide(Variant["side"]);

  // JSON collision/selection boxes are authored in the north orientation and do not
  // auto-rotate with the placed variant, so rotate them to match the shape's rotateY.
  private Cuboidf[]? _rotatedCollisionBoxes;
  private Cuboidf[]? _rotatedSelectionBoxes;

  public override Cuboidf[] GetCollisionBoxes(
    IBlockAccessor blockAccessor,
    BlockPos pos
  ) =>
    _rotatedCollisionBoxes ??= ExOrientation.RotateBoxes(CollisionBoxes, Angle);

  public override Cuboidf[] GetSelectionBoxes(
    IBlockAccessor blockAccessor,
    BlockPos pos
  ) =>
    _rotatedSelectionBoxes ??= ExOrientation.RotateBoxes(SelectionBoxes, Angle);

  /// <summary>Facing of one of the two horizontal water connectors (local west).</summary>
  public BlockFacing SideAFace =>
    ExOrientation.RotateFacing(BlockFacing.WEST, Angle);

  /// <summary>Facing of the other horizontal water connector (local east).</summary>
  public BlockFacing SideBFace =>
    ExOrientation.RotateFacing(BlockFacing.EAST, Angle);

  /// <summary>Facing of the steam-inlet connector (local north).</summary>
  public BlockFacing SteamInletFace =>
    ExOrientation.RotateFacing(BlockFacing.NORTH, Angle);

  public bool HasConnectorAt(BlockFacing face)
  {
    int angle = Angle;
    return face == ExOrientation.RotateFacing(BlockFacing.WEST, angle)
      || face == ExOrientation.RotateFacing(BlockFacing.EAST, angle)
      || face == ExOrientation.RotateFacing(BlockFacing.NORTH, angle);
  }

  public override bool CanAttachBlockAt(
    IBlockAccessor world,
    Block block,
    BlockPos pos,
    BlockFacing blockFace,
    Cuboidi attachmentArea
  ) => HasConnectorAt(blockFace) || SideSolid[blockFace.Index];
}
