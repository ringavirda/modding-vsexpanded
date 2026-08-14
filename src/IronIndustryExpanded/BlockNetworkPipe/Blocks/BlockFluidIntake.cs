using System.Collections.Generic;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockNetworkPipe.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockNetworkPipe.Blocks;

[BlockRegister]
public partial class BlockFluidIntake : BlockNetworkNode, IExBlockDefProvider {
  public override string NetworkType => "pipe";

  /// <summary>The fluid-intake blocktype, authored in C#.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [FluidIntake(domain)];

  private static ExBlockDef FluidIntake(string domain) =>
    ExBlockDef
      .Create(domain, "pipe", "pipe/fluidintake")
      .Class<BlockFluidIntake>()
      .EntityClass<BlockEntityFluidIntake>()
      .Material(EnumBlockMaterial.Metal)
      .VariantGroup("type", "fluidintake")
      .VariantGroup("orientation", "n", "s", "w", "e")
      .CreativeTab("general", "*-fluidintake-s")
      .CreativeTab("iiex", "*-fluidintake-s")
      .ShapeByType("*-n", "iiex:pipe/fluidintake", rotateY: 180)
      .ShapeByType("*-e", "iiex:pipe/fluidintake", rotateY: 90)
      .ShapeByType("*-s", "iiex:pipe/fluidintake", rotateY: 0)
      .ShapeByType("*-w", "iiex:pipe/fluidintake", rotateY: 270)
      .SideSolid(false)
      .SideOpaque(false);

  // AllowedOrientations comes from BlockNetworkNode, derived from this block's own def's variant
  // groups. The fallback is "s": the intake faces south by default, not the first-listed orientation.
  protected override string GetFallbackOrientation(string? type) => "s";

  /// <summary>
  /// The intake rests on water in any horizontal facing, so the wrench must cycle all four facings
  /// rather than the one it snapped to. Opting in makes <c>GetWrenchOrientations</c> recompute the
  /// cycle on the fly.
  /// </summary>
  protected override bool IsFullCube => true;

  /// <summary>
  /// The intake may only be placed on top of a water block. The full functional check (whole cube
  /// below is water, no crowding) lives in <see cref="BlockEntities.BlockEntityFluidIntake"/>.
  /// </summary>
  public override bool TryPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    ItemStack itemstack,
    BlockSelection blockSel,
    ref string failureCode
  ) {
    Block below = world.BlockAccessor.GetBlock(
      blockSel.Position.DownCopy(),
      BlockLayersAccess.Fluid
    );
    if (below.LiquidCode != "water") {
      // Shown as Lang.Get("placefailure-" + code), so this must be a plain code with a matching
      // "game:placefailure-…" lang entry, not literal text.
      failureCode = "iiex-fluidintake-nowater";
      return false;
    }

    return base.TryPlaceBlock(
      world,
      byPlayer,
      itemstack,
      blockSel,
      ref failureCode
    );
  }

  /// <summary>
  /// The intake is a standalone source block resting on the water it pumps. Water is not an
  /// attachable surface, so the base self-break would destroy a freshly placed intake. Keeps the
  /// orientation in sync and never self-breaks; losing the water only disables it.
  /// </summary>
  public override void OnNeighbourBlockChange(
    IWorldAccessor world,
    BlockPos pos,
    BlockPos neighbour
  ) {
    if (Orientation == null)
      return;

    RecalculateAndSyncOrientations(world, pos);
  }
}
