using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Casting.BlockEntities;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Casting.Blocks;

/// <summary>
/// The sand casting bed mega-block: a 3x1x4 footprint, built through right-click construction (two brick
/// courses then a sand fill, with the runners carved by the player), that casts poured molten pig iron
/// into solid pigs. Each footprint filler cell hosts a <see cref="BEBehaviorMoltenCell"/> - the central
/// column the runners, the two side columns the double-molds - and the principal hosts the pour basin.
/// <see cref="BlockEntitySandCastingBed"/> drives the isolated internal flow and the harvest; this block
/// wires the footprint, the construction, and the click that reaches the harvest.
/// See docs/design/machines/casting-bed.md.
/// </summary>
[BlockRegister]
public partial class BlockSandCastingBed
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider {
  #region Code-first definition

  // Per-cell molten-cell configs hosted on the footprint fillers: a thin pass-through runner and a mold
  // that hoards its charge (drainFitting) until it hardens into pigs.
  private static readonly FillerBehaviorSpec RunnerCell =
    FillerBehaviorSpec.Of<BEBehaviorMoltenCell>(
      properties: new { capacity = 50 }
    );

  // A mold's capacity is its row's impression count at one pig each: the two end rows carry 2 impressions,
  // the middle rows 3. SandBedLayout.CapacityOf is the same expression the harvest reads, so the cavity
  // the bed pours into and the cavity it hands back cannot drift apart.
  private static FillerBehaviorSpec MoldCell(BedSlot slot) =>
    FillerBehaviorSpec.Of<BEBehaviorMoltenCell>(
      properties: new {
        capacity = SandBedLayout.CapacityOf(slot, BedSlotState.Mold),
        drainFitting = true,
      }
    );

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Bed(domain)];

  private static ExBlockDef Bed(string domain) =>
    ExBlockDef
      .Create(domain, "casting-sandbed", "casting/sandbed")
      .Class<BlockSandCastingBed>()
      .EntityClass<BlockEntitySandCastingBed>()
      .Material(EnumBlockMaterial.Ceramic)
      .MiningTier(0)
      .Resistance(3.5f)
      .MaxStackSize(1)
      .NoDrops()
      .Behavior("ExOrientable")
      .FillerOffsets(Footprint())
      // The pour basin lives on the principal itself and is the internal flow's source.
      .EntityBehavior<BEBehaviorMoltenCell>(
        new JObject { ["capacity"] = 200, ["flowSource"] = true }
      )
      // The RCC behaviour suppresses the default block mesh, so the built stages render only through a
      // ConstructedAnimator; without Animatable the bed draws nothing but its molten surfaces. The bed is
      // static, so the animator only tesselates and runs no animations.
      .EntityBehavior("Animatable")
      .Construction(c =>
        c.Stage(s =>
            // storeWildCard "brick" captures the fired-brick colour used; the second course and the
            // block's own {brick} texture then resolve to the same colour.
            s.Require(
                "game:burnedbrick-{brick}",
                8,
                "iiex:rcc-ingredient-brick",
                storeWildCard: "brick"
              )
              .AddElements("Base")
          )
          .Stage(s =>
            s.Require(
                "game:burnedbrick-{brick}",
                16,
                "iiex:rcc-ingredient-brick"
              )
              .AddElements("BaseExtension")
          )
          // The sand fill is the last build step and finishes uncarved: every slot starts as plain sand,
          // and the player carves the runners and molds afterwards. Takes prepared green sand (sand plus
          // blue clay), the same item the 1x1 cell is rammed with, since only that holds an impression.
          .Stage(s =>
            s.Require(
                $"{domain}:{GreenSandItemDefinitions.Code}",
                12,
                "iiex:rcc-ingredient-sand"
              )
              .AddElements(SandBedLayout.RunnersGroup)
          )
      )
      // No `sand` variant group: every bed is filled with the same green sand, so the code is
      // `casting-sandbed-{brick}-{side}` and the sand texture is a constant.
      .VariantGroup(
        "brick",
        "black",
        "brown",
        "cream",
        "gray",
        "orange",
        "red",
        "tan"
      )
      .SideVariant()
      .CreativeTab("general", "*-n")
      .CreativeTab("iiex", "*-n")
      // Must stay `iiex:casting/sandcastingbed`: it is the shape carrying the per-slot elements
      // SandBedLayout emits, and selective-element matching drops an unknown element name silently, so a
      // shape without them renders a carved runner or mold as nothing at all. SandBedLayoutTests walks
      // this same shape file.
      .Shape("iiex:casting/sandcastingbed")
      .ShapeRotateYByType("*-n", 180)
      .ShapeRotateYByType("*-e", 90)
      .ShapeRotateYByType("*-s", 0)
      .ShapeRotateYByType("*-w", 270)
      // Brick colour is a tint overlay over the running-bond base; the burned-clay mold cavities stay
      // fixed. The shapes draw their rammed sand against the `andesite` key, bound here to the green-sand
      // texture, as on the 1x1 cell.
      .Texture(
        "fire1",
        "game:block/clay/brick/four/running/cream1",
        "game:block/clay/brick/four/running/{brick}1"
      )
      .Texture("burned", "game:block/clay/vessel/sides/burned")
      .Texture("andesite", GreenSandItemDefinitions.Texture)
      .SingleSelectionBox(0f, 0f, 0f, 1f, 1f, 1f)
      .SingleCollisionBox(0f, 0f, 0f, 1f, 0.875f, 1f)
      .SideSolid(false)
      .SideOpaque(false)
      .Sound("place", "game:block/ceramicplace")
      .Sound("break", "game:block/ceramic")
      .Sound("hit", "game:block/ceramic")
      .Sound("walk", "game:walk/stone");

  // The north-orientation footprint: every carvable slot that is not the principal's own cell. Generated
  // from SandBedLayout, and the order it emits is the order SlotCells zips against.
  private static IReadOnlyList<FillerCellSpec> Footprint() {
    var cells = new List<FillerCellSpec>();
    foreach (BedSlot slot in SandBedLayout.FillerSlots) {
      (int dx, int dz) = SandBedLayout.OffsetOf(slot);
      cells.Add(
        new FillerCellSpec(
          dx,
          0,
          dz,
          Behaviors: [slot.IsMold ? MoldCell(slot) : RunnerCell]
        )
      );
    }
    return cells;
  }

  /// <summary>
  /// The world position of every carvable slot for a bed at <paramref name="pos"/>. Built by zipping the
  /// resolved footprint against the specs it was generated from: <see cref="Footprint"/> and
  /// <c>FootprintCells</c> preserve order, so no separate inverse rotation is needed. The principal's own
  /// cell carries row 1's runner, the basin.
  /// </summary>
  public IReadOnlyDictionary<BlockPos, BedSlot> SlotCells(BlockPos pos) {
    var map = new Dictionary<BlockPos, BedSlot> {
      [pos] = new BedSlot(SandBedLayout.FirstRow, BedSlotSide.Centre),
    };

    IReadOnlyList<FillerCellSpec> specs = Footprint();
    IReadOnlyList<BlockPos> resolved = FootprintPositions(pos);
    for (int i = 0; i < specs.Count && i < resolved.Count; i++)
      if (SandBedLayout.SlotAt(specs[i].X, specs[i].Z) is { } slot)
        map[resolved[i]] = slot;

    return map;
  }

  #endregion

  /// <summary>
  /// Structure and filler rotation, paired with the shape <c>rotateYByType</c>. The +180 keeps the
  /// footprint flush with the model, whose body is authored extending the opposite way from the
  /// orientation convention. Also rotates the block entity's molten surfaces.
  /// </summary>
  public override int StructureAngle =>
    ExOrientation.AngleFromSide(Variant["side"]) + 180;

  /// <summary>The bed's footprint cell world positions, for the block entity's flow + surface loops.</summary>
  public IReadOnlyList<BlockPos> FootprintPositions(BlockPos pos) =>
    FootprintCells(pos).Select(c => c.Pos).ToList();

  #region Drops

  // The bed is raised through construction, so a broken bed scatters only the construction materials the
  // RCC behaviour returns, never the bed block itself.
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [];

  #endregion

  #region Interaction (harvest a hardened mold, else fall through to construction)

  private static BlockEntitySandCastingBed? Bed(
    IWorldAccessor world,
    BlockPos pos
  ) => world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySandCastingBed;

  /// <summary>
  /// A click on the bed's principal cell. <c>SandBedLayout.FillerSlots</c> excludes offset (0,0), where
  /// row 1's centre slot sits, so that slot has no filler and is reachable only here. Routes to
  /// <see cref="BlockEntitySandCastingBed.OnCellInteract"/> exactly as the filler path does, falling
  /// through to construction when the cell has nothing to do.
  /// </summary>
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    Bed(world, blockSel.Position)?.OnCellInteract(blockSel.Position, byPlayer)
      == true
    || base.OnBlockInteractStart(world, byPlayer, blockSel);

  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) =>
    Bed(world, principalSel.Position)?.OnCellInteract(clickedCell, byPlayer)
      == true
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
