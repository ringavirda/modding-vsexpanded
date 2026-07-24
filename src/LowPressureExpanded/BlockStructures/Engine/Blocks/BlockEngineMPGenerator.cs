using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using LowPressureExpanded.BlockStructures.Engine.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace LowPressureExpanded.BlockStructures.Engine.Blocks;

/// <summary>
/// The MP-generator sub-machine. Couples axles on both ends of its axis (north +
/// south in the natural orientation), driving the vanilla mechanical-power network
/// via the <see cref="BEBehaviorEngineMPGenerator"/> torque producer.
/// </summary>
[BlockRegister]
public partial class BlockEngineMPGenerator
  : BlockEngineSubmachine,
    IMechanicalPowerBlock,
    IExBlockDefProvider
{
  /// <summary>The MP-generator sub-machine blocktype, authored in C# (migrated from engine/mpgenerator.json).</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "enginempgenerator", "engine/mpgenerator")
        .Class<BlockEngineMPGenerator>()
        // Type-safe, not the raw "lpex.BlockEntityEngineMPGenerator" string it used to be: both sides
        // now resolve through EntityRegistry.KeyFor, so renaming the block entity can never leave this
        // registration pointing at a class that no longer exists (a load-time failure with no compile
        // error - see the twin-tub blower for what that looks like).
        .EntityClass<BlockEntityEngineMPGenerator>()
        .Behavior("HorizontalOrientable")
        .EntityBehavior("lpex.BEBehaviorEngineMPGenerator")
        .Material(EnumBlockMaterial.Metal)
        .VariantGroupFromProperties("side", "abstract/horizontalorientation")
        .CreativeCommon("*-north")
        .ShapeByTypePerOrientation("lpex:engine/mpgenerator")
        .NonSolid(),
    ];

  private bool IsXAxis => Variant["side"] is "east" or "west";

  public bool HasMechPowerConnectorAt(
    IWorldAccessor world,
    BlockPos pos,
    BlockFacing face
#if GAME_GE_1_22
    ,
    BlockMPBase forBlock
#endif
  ) =>
    IsXAxis
      ? face == BlockFacing.EAST || face == BlockFacing.WEST
      : face == BlockFacing.NORTH || face == BlockFacing.SOUTH;

  public void DidConnectAt(
    IWorldAccessor world,
    BlockPos pos,
    BlockFacing face
  ) { }

  public MechanicalNetwork? GetNetwork(IWorldAccessor world, BlockPos pos) =>
    world
      .BlockAccessor.GetBlockEntity(pos)
      ?.GetBehavior<BEBehaviorEngineMPGenerator>()
      ?.Network;
}
