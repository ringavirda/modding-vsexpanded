using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkEnergy.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockNetworkEnergy.Blocks;

/// <summary>
/// A cast-iron <b>transmission</b>: a compact mega-block that changes the speed of an mpenergy run at a point.
/// The bigger gear sits on the north main shaft, so a transmission is a <b>reduction S→N</b> (drive from the
/// south and the north side turns slower, with more torque - the reduction that feeds a heavy forming machine;
/// drive it from the north and the south speeds up). Three variants pick the ratio by <em>which block you
/// build</em> - <c>x2</c> (belt drive), <c>x4</c> (compound gear train), and <c>clutch</c> (ratio 1, a
/// coupling the player engages/disengages).
/// <para>
/// Unlike the shaft/bevel it is <b>not</b> a graph node: it is a machine (like the rolling mill) whose principal
/// couples the two <em>separate</em> mpenergy networks on its south and north faces (read via
/// <c>GetNetworkAt</c>) so the two sides never merge - that coupling, and the clutch, live in
/// <see cref="BlockEntityTransmission"/>. Here is the block: the 2×2 footprint, the three type × four
/// orientation shapes, and the three-stage RightClickConstructable build (Base → MainShafts → SupportShaft).
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockTransmission
  : BlockFilledMegastructure,
    IExBlockDefProvider,
    IFillerInteractionTarget
{
  private static readonly string[] Types = ["x2", "x4", "clutch"];
  private static readonly string[] Sides = ["north", "east", "south", "west"];

  #region Code-first definition

  /// <summary>The transmission blocktype: <c>type</c> (x2 / x4 / clutch) × <c>side</c> (the four horizontal
  /// orientations). One principal + a 2×2 footprint (side slab, slab-above, and a quarter-block cell that hosts
  /// the clutch's interaction); rendered per type and spun per orientation; built through a three-stage RCC.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain)
  {
    ExBlockDef def = ExBlockDef
      .Create(domain, "mpenergy", "mpenergy/transmission")
      .Class<BlockTransmission>()
      .EntityClass<BlockEntityTransmission>()
      .Material(EnumBlockMaterial.Metal)
      .MiningTier(0)
      .Resistance(4.5f)
      .MaxStackSize(1)
      .NoDrops() // the RCC scatters the construction materials; the block itself never drops
      // The 2×2 cross-section (X-slice, S→N): principal O + a side slab, a slab above it, and the
      // quarter-block `i` diagonally that carries the clutch lever / interaction.
      //   - i      (upper row)
      //   O -      (principal row)
      .FillerOffsets(
        [
          new FillerCellSpec(1, 0, 0), // side slab beside the principal
          new FillerCellSpec(0, 1, 0), // slab above the principal
          new FillerCellSpec(1, 1, 0), // quarter-block: the clutch interaction cell
        ]
      )
      .Behavior("ExOrientable")
      .Behavior("BlockEntityInteract")
      .EntityBehavior("Animatable")
      .Construction(c =>
        c.Stage(s => s.AddElements("Base"))
          .Stage(s =>
            // iwex's own cast gear, not lpex's. Reaching upward for `lpex:gear-iron` made this
            // unbuildable for an iwex-only player (the placement rule forbids it either way).
            s.Require("iwex:" + Items.SpurGearItemDefinitions.Code, 2, "iwex:rcc-ingredient-transmissiongears")
              .Require("game:ingot-iron", 4, "iwex:rcc-ingredient-transmissionshafts")
              .AddElements("MainShafts")
          )
          .Stage(s =>
            s.Require("game:ingot-iron", 2, "iwex:rcc-ingredient-transmissionsupport")
              .AddElements("SupportShaft")
          )
      )
      // See BlockFlywheel: `type` names the member so the family shares one code. The gearing is
      // `kind`; the rendered code (mpenergy-transmission-x2-north) is unchanged.
      .VariantGroup("type", "transmission")
      .VariantGroup("kind", Types)
      .SideVariant()
      .CreativeCommon("*-x2-n", "*-x4-n", "*-clutch-n")
      .ShapeSelectiveElements("Base/*")
      .Sounds(
        "game:block/anvil",
        "game:block/metal",
        "game:block/metal",
        "game:walk/stone"
      )
      .SingleSelectionBox(0f, 0f, 0f, 1f, 1f, 1f)
      .SingleCollisionBox(0f, 0f, 0f, 1f, 1f, 1f)
      .SideSolid(false)
      .SideOpaque(false);

    // Per type × orientation: the type picks the shape, the side spins it (derived angle, mixer convention).
    foreach (string type in Types)
      foreach (string side in Sides)
        def.ShapeByType(
          $"*-{type}-{side}",
          $"iwex:mpenergy/transmission-{type}",
          rotateY: ExOrientation.AngleFromSide(side)
        );

    return [def];
  }

  #endregion

  /// <summary>Structure/filler rotation and the BE's port-cell resolution angle (north 0 … west 90).</summary>
  public override int StructureAngle => ExOrientation.AngleFromSide(Variant["side"]);

  #region Clutch interaction (routed from the lever cell)

  // Whichever cell was clicked - the principal or any filler - routes here. Before construction the click
  // falls through to the RightClickConstructable behaviour (null); on a built clutch, a click on the lever cell
  // throws the coupling. Every other click on the built machine is swallowed so nothing is placed against it.
  private bool? HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection sel,
    BlockPos clickedCell
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(sel.Position)
        is not BlockEntityTransmission be
      || !be.IsConstructed
    )
      return null; // pre-construction clicks drive the RCC behaviour

    if (world.Side != EnumAppSide.Server)
      return true; // the server owns the lever state; the client just predicts the swallow

    if (be.IsClutch && be.IsLeverCell(clickedCell))
      be.ToggleEngaged();
    return true;
  }

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    HandleInteract(world, byPlayer, blockSel, blockSel.Position)
    ?? base.OnBlockInteractStart(world, byPlayer, blockSel);

  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) =>
    HandleInteract(world, byPlayer, principalSel, clickedCell)
    ?? base.OnBlockInteractStart(world, byPlayer, principalSel);

  bool IFillerInteractionTarget.OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => base.OnBlockInteractStep(secondsUsed, world, byPlayer, principalSel);

  void IFillerInteractionTarget.OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => base.OnBlockInteractStop(secondsUsed, world, byPlayer, principalSel);

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) => BuildInteractionHelp(world, selection, forPlayer, selection.Position);

  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => BuildInteractionHelp(world, principalSel, forPlayer, clickedCell);

  /// <summary>The "throw the lever" hint, shown only on a built clutch's lever cell.</summary>
  private WorldInteraction[] BuildInteractionHelp(
    IWorldAccessor world,
    BlockSelection sel,
    IPlayer forPlayer,
    BlockPos clickedCell
  )
  {
    WorldInteraction[] baseHelp = base.GetPlacedBlockInteractionHelp(
      world,
      sel,
      forPlayer
    );
    if (
      world.BlockAccessor.GetBlockEntity(sel.Position)
        is BlockEntityTransmission { IsConstructed: true } be
      && be.IsClutch
      && be.IsLeverCell(clickedCell)
    )
      return
      [
        new WorldInteraction
        {
          ActionLangCode = "iwex:transmission-help-clutch",
          MouseButton = EnumMouseButton.Right,
        },
        .. baseHelp,
      ];
    return baseHelp;
  }

  #endregion

  #region Drops

  // A broken transmission returns only its construction materials (scattered by the RCC behaviour).
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    Vintagestory.API.MathTools.BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [];

  #endregion
}
