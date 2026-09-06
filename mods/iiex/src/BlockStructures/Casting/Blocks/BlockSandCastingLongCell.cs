using System.Collections.Generic;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Molten;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockStructures.Casting.BlockEntities;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Casting.Blocks;

/// <summary>
/// The 1×2 sand casting long cell: the cast-stock station, and the only block in the suite that can pour a
/// piece longer than twelve voxels. The product follows the impressed pattern - three narrow lanes give
/// billets, two give blooms, one gives a slab.
/// <para>
/// The second cell is a filler for collision and interaction only, so clicks on it are rerouted to the
/// principal through <see cref="IFillerInteractionTarget"/>.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockSandCastingLongCell
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>
  /// Capacity in units of a cell that has been rammed but not yet impressed. Sized above the largest
  /// long-cell capacity (the slab's 3000 u); an impressed pattern overrides it.
  /// </summary>
  private const int UnimpressedCapacity = 3400;

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [LongCell(domain)];

  private static ExBlockDef LongCell(string domain) =>
    ExBlockDef
      .Create(domain, "casting-sandlongcell", "casting/sandlongcell")
      .Class<BlockSandCastingLongCell>()
      .EntityClass<BlockEntitySandCastingLongCell>()
      .Material(EnumBlockMaterial.Ceramic)
      .MiningTier(0)
      .Resistance(3.5f)
      .MaxStackSize(8)
      .Behavior("ExOrientable")
      .FillerOffsets(LongCellLayout.Footprint())
      // One pooled impression on the principal; the filler hosts nothing. drainFitting keeps the station
      // off the molten graph - it is fed by a canal.
      .EntityBehavior<BEBehaviorMoltenCell>(
        new JObject {
          ["capacity"] = UnimpressedCapacity,
          ["drainFitting"] = true,
        }
      )
      // Same brick group and declaration order as the 1×1 cell, so the two share a variant layout.
      .VariantGroup(
        "brick",
        "fire",
        "black",
        "brown",
        "cream",
        "gray",
        "orange",
        "red",
        "tan"
      )
      .SideVariant()
      .ShapeSpunPerOrientation("iiex:casting/sandcastinglongcell")
      .Texture(
        "fire1",
        "game:block/clay/brick/four/running/cream1",
        "game:block/clay/brick/four/running/{brick}1"
      )
      .Texture("burned", "game:block/clay/vessel/sides/burned")
      // The filling shapes draw the rammed sand against the `andesite` key. Omit this and the sand
      // renders untextured.
      .Texture("andesite", GreenSandItemDefinitions.Texture)
      .CreativeTab("general", "*-n")
      .CreativeTab("iiex", "*-n")
      .SideSolid(false)
      .SideOpaque(false)
      .Sound("place", "game:block/ceramicplace")
      .Sound("break", "game:block/ceramic")
      .Sound("hit", "game:block/ceramic")
      .Sound("walk", "game:walk/stone");

  #endregion

  #region Structure

  /// <summary>
  /// Structure/filler rotation. The shape spins by the side angle alone; the body it draws extends
  /// opposite the footprint's declared cell, so the +180 here reconciles the two without touching the
  /// shape's own spin.
  /// </summary>
  public override int StructureAngle =>
    ExOrientation.AngleFromSide(Variant["side"]) + 180;

  #endregion

  #region Interaction

  private static BlockEntitySandCastingLongCell? Cell(
    IWorldAccessor world,
    BlockPos pos
  ) =>
    world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySandCastingLongCell;

  /// <inheritdoc/>
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    Cell(world, blockSel.Position)?.OnInteract(byPlayer) == true
    || base.OnBlockInteractStart(world, byPlayer, blockSel);

  // A click on the filler carries the filler's position, so it is rerouted to the principal, which owns
  // the single impression spanning both cells. The clicked cell is ignored: both cells do the same thing.
  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) =>
    Cell(world, principalSel.Position)?.OnInteract(byPlayer) == true
    || base.OnBlockInteractStart(world, byPlayer, principalSel);

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

  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => GetPlacedBlockInteractionHelp(world, principalSel, forPlayer);

  #endregion
}
