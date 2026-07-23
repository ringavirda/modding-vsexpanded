using System.Collections.Generic;
using System.Linq;
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
/// The sand casting bed mega-block: a 3×1×4 footprint (built through right-click construction - two
/// brick courses, a sand fill, then the player forms the runners) that casts poured molten pig iron
/// into solid pigs. Its footprint filler cells each host a <see cref="BEBehaviorMoltenCell"/> - the
/// central column the runners, the two side columns the double-molds - and the principal hosts the
/// pour basin. <see cref="BlockEntitySandCastingBed"/> drives the isolated internal flow and the
/// harvest; this block wires the footprint, the construction, and routes a click on a hardened mold
/// through to the harvest (falling through to construction otherwise).
/// </summary>
[BlockRegister]
public partial class BlockSandCastingBed
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider
{
  #region Code-first definition

  // Per-cell molten-cell configs hosted on the footprint fillers: a thin pass-through runner and a
  // double-mold that hoards its charge (drainFitting) until it hardens into two pigs.
  private static readonly FillerBehaviorSpec RunnerCell =
    new("exlib.BEBehaviorMoltenCell", null, new { capacity = 50 });
  private static readonly FillerBehaviorSpec MoldCell =
    new("exlib.BEBehaviorMoltenCell", null, new { capacity = 300, drainFitting = true });

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Bed(domain)];

  private static ExBlockDef Bed(string domain) =>
    ExBlockDef
      .Create(domain, "sandcastingbed", "casting/sandcastingbed")
      .Class<BlockSandCastingBed>()
      .EntityClass<BlockEntitySandCastingBed>()
      .Material(EnumBlockMaterial.Ceramic)
      .MiningTier(0)
      .Resistance(3.5f)
      .MaxStackSize(1)
      .NoDrops()
      .Behavior("HorizontalOrientable")
      .FillerOffsets(Footprint())
      // The pour basin lives on the principal itself - the internal flow's source.
      .EntityBehavior(
        "exlib.BEBehaviorMoltenCell",
        new JObject { ["capacity"] = 200, ["flowSource"] = true }
      )
      .Construction(c =>
        c.Stage(s =>
            // storeWildCard "brick" captures the fired-brick colour used; the second course and the
            // block's own {brick} texture then resolve to the same colour.
            s.Require(
                "game:burnedbrick-{brick}",
                8,
                "iwex:rcc-ingredient-brick",
                storeWildCard: "brick"
              )
              .AddElements("Base")
          )
          .Stage(s =>
            s.Require("game:burnedbrick-{brick}", 16, "iwex:rcc-ingredient-brick")
              .AddElements("BaseExtension")
          )
          .Stage(s =>
            // Any sand (rock) variant; the captured "sand" wildcard drives the sand texture in the shape.
            s.Require(
                "game:sand-{sand}",
                12,
                "iwex:rcc-ingredient-sand",
                type: "block",
                storeWildCard: "sand"
              )
              .AddElements("SandFull", "SandFull2")
          )
          // The final step needs no material: the player forms the runners in the sand by hand.
          .Stage(s => s.RemoveElements("SandFull", "SandFull2").AddElements("SandRunners"))
      )
      // The bed carries its build materials as variants: the fired-brick colour and the sand's rock
      // type, plus the horizontal facing. Textures below key off {brick} and {sand}.
      .VariantGroup("brick", "black", "brown", "cream", "gray", "orange", "red", "tan")
      .VariantGroupFromProperties("sand", "block/rock")
      .VariantGroupFromProperties("side", "abstract/horizontalorientation")
      .CreativeTab("general", "*-andesite-north")
      .CreativeTab("iwex", "*-andesite-north")
      .Shape("iwex:sandcasting-bed")
      .ShapeRotateYByType("*-north", 180)
      .ShapeRotateYByType("*-east", 90)
      .ShapeRotateYByType("*-south", 0)
      .ShapeRotateYByType("*-west", 270)
      // Brick colour is a tint overlay over the running-bond base (the ore-bunker pattern); sand is a
      // direct per-rock texture; the burned-clay mold cavities stay fixed.
      .Texture(
        "fire1",
        "game:block/clay/brick/four/running/cream1",
        "game:block/clay/brick/four/running/{brick}1"
      )
      .Texture("burned", "game:block/clay/vessel/sides/burned")
      .Texture("andesite", "game:block/stone/sand/{sand}")
      .SingleSelectionBox(0f, 0f, 0f, 1f, 1f, 1f)
      .SingleCollisionBox(0f, 0f, 0f, 1f, 0.875f, 1f)
      .SideSolid(false)
      .SideOpaque(false)
      .Sound("place", "game:block/ceramicplace")
      .Sound("break", "game:block/ceramic")
      .Sound("hit", "game:block/ceramic")
      .Sound("walk", "game:walk/stone");

  // The north-orientation footprint: two brick edge cells beside the basin, then three rows of a
  // central runner flanked by the two double-molds, running +Z away from the principal.
  private static IReadOnlyList<FillerCellSpec> Footprint()
  {
    var cells = new List<FillerCellSpec>
    {
      new(-1, 0, 0),
      new(1, 0, 0),
    };
    for (int z = 1; z <= 3; z++)
    {
      cells.Add(new FillerCellSpec(0, 0, z, Behaviors: [RunnerCell]));
      cells.Add(new FillerCellSpec(-1, 0, z, Behaviors: [MoldCell]));
      cells.Add(new FillerCellSpec(1, 0, z, Behaviors: [MoldCell]));
    }
    return cells;
  }

  #endregion

  /// <summary>
  /// Structure/filler rotation, paired with the shape <c>rotateYByType</c>: the +180 keeps the footprint
  /// flush with the model, whose body is authored extending the opposite way from the orientation
  /// convention (so a placed bed runs away from the player). Also rotates the BE's molten surfaces.
  /// </summary>
  public override int StructureAngle =>
    ExOrientation.AngleFromSide(Variant["side"]) + 180;

  /// <summary>The bed's footprint cell world positions, for the block entity's flow + surface loops.</summary>
  public IReadOnlyList<BlockPos> FootprintPositions(BlockPos pos) =>
    FootprintCells(pos).Select(c => c.Pos).ToList();

  #region Drops

  // Raised through construction; a broken bed scatters only its construction materials (the RCC
  // behaviour), never the bed block itself.
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [];

  #endregion

  #region Interaction (harvest a hardened mold, else fall through to construction)

  private static BlockEntitySandCastingBed? Bed(IWorldAccessor world, BlockPos pos) =>
    world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySandCastingBed;

  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) =>
    Bed(world, principalSel.Position)?.TryHarvest(clickedCell, byPlayer) == true
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
