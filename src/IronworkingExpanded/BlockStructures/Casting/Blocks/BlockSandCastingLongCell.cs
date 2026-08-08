using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Casting.BlockEntities;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Casting.Blocks;

/// <summary>
/// The 1×2 sand casting <b>long cell</b> - the cast-stock station, and the only block in the suite that can
/// pour a piece longer than twelve voxels.
/// <para>
/// Its quieter job is the <b>lane ladder</b>: one cell, three fillings, three products. Three narrow
/// lanes give billets, two give blooms, one gives a slab - the whole cast-stock ladder off one block with
/// nothing but a pattern swap, which is the trick the 1×1
/// <see cref="BlockSandCastingCell"/> plays for capital goods.
/// </para>
/// <para>
/// The second cell is a filler for collision and interaction only, so every click on it is rerouted to
/// the principal through <see cref="IFillerInteractionTarget"/>. Without that reroute, half the station is
/// dead to the player - the failure the casting bed shipped with (B-bed-2).
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockSandCastingLongCell
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>
  /// Pre-impression ceiling for the single pooled impression. Sized above the largest shipped long-cell
  /// capacity (the slab's 3000 u) with headroom, because an impressed pattern overrides it anyway - this
  /// only bounds a cell that has been rammed but not yet impressed.
  /// </summary>
  private const int UnimpressedCapacity = 3400;

  public static IEnumerable<ExBlockDef> Definitions(string domain) => [LongCell(domain)];

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
      // One pooled impression on the principal; the filler hosts nothing. drainFitting: the station is fed
      // by a canal, it does not join the molten graph.
      .EntityBehavior(
        "exlib.BEBehaviorMoltenCell",
        new JObject { ["capacity"] = UnimpressedCapacity, ["drainFitting"] = true }
      )
      // Same brick group and declaration order as the 1×1 cell, so the two read as one family and the
      // code lands as casting-sandlongcell-{brick}-{side}.
      .VariantGroup("brick", "fire", "black", "brown", "cream", "gray", "orange", "red", "tan")
      .SideVariant()
      .ShapeSpunPerOrientation("iwex:casting/sandcastinglongcell", offset: 180)
      .Texture(
        "fire1",
        "game:block/clay/brick/four/running/cream1",
        "game:block/clay/brick/four/running/{brick}1"
      )
      .Texture("burned", "game:block/clay/vessel/sides/burned")
      // The filling shapes are drawn against the historical `andesite` key (the name the cell defaulted
      // to when it took any sand). Omit this and the rammed sand renders untextured.
      .Texture("andesite", GreenSandItemDefinitions.Texture)
      .CreativeTab("general", "*-n")
      .CreativeTab("iwex", "*-n")
      .SideSolid(false)
      .SideOpaque(false)
      .Sound("place", "game:block/ceramicplace")
      .Sound("break", "game:block/ceramic")
      .Sound("hit", "game:block/ceramic")
      .Sound("walk", "game:walk/stone");

  #endregion

  #region Structure

  /// <summary>
  /// Structure/filler rotation, paired with the shape's <c>rotateYByType</c> offset: the +180 keeps the
  /// footprint flush with the model, whose body is authored extending the opposite way from the
  /// orientation convention. It must equal the angle passed to the shape, or the filler lands on the
  /// wrong side of the principal and the structure can never complete.
  /// </summary>
  public override int StructureAngle => ExOrientation.AngleFromSide(Variant["side"]) + 180;

  #endregion

  #region Interaction

  private static BlockEntitySandCastingLongCell? Cell(
    IWorldAccessor world,
    BlockPos pos
  ) => world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySandCastingLongCell;

  /// <inheritdoc/>
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    Cell(world, blockSel.Position)?.OnInteract(byPlayer) == true
    || base.OnBlockInteractStart(world, byPlayer, blockSel);

  // The reroute that keeps the far half alive. A click on the filler carries the filler's position, so
  // without this the station only responds on the cell the player happened to place - and the impression
  // spans both. The clicked cell is deliberately ignored: the whole station is one casting, so both cells
  // do the same thing.
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
