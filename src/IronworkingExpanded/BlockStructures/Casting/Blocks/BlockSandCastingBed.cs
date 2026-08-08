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

  // Per-cell molten-cell configs hosted on the footprint fillers: a thin pass-through runner and a mold
  // that hoards its charge (drainFitting) until it hardens into pigs.
  private static readonly FillerBehaviorSpec RunnerCell =
    new("exlib.BEBehaviorMoltenCell", null, new { capacity = 50 });

  // A mold's capacity is its ROW's impression count at one pig each - never a literal. The rows are not the
  // same size (the two end rows carry 2 impressions, the middle rows 3), which is the whole reason a middle
  // row is worth more per pour; a single shared number was short for every middle row and silently tracked
  // the pig's mass. SandBedLayout.CapacityOf is the same expression the harvest reads, so the cavity the
  // bed pours into and the cavity it hands back cannot drift apart.
  private static FillerBehaviorSpec MoldCell(BedSlot slot) =>
    new(
      "exlib.BEBehaviorMoltenCell",
      null,
      new { capacity = SandBedLayout.CapacityOf(slot, BedSlotState.Mold), drainFitting = true }
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
      // The pour basin lives on the principal itself - the internal flow's source.
      .EntityBehavior(
        "exlib.BEBehaviorMoltenCell",
        new JObject { ["capacity"] = 200, ["flowSource"] = true }
      )
      // The RCC behaviour suppresses the default block mesh, so the built brick+sand stages only render
      // through a ConstructedAnimator (the ore-bunker/engine idiom). Without Animatable the bed draws
      // nothing but its molten surfaces. The bed is static, so the animator only tesselates - no anims.
      .EntityBehavior("Animatable")
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
          // Filling the bed with green sand is the last build step, and it finishes UNCARVED: every slot
          // starts broken (plain sand), so the runners and molds are the player's own work afterwards
          // rather than something construction hands them.
          // Prepared green sand, not raw `game:sand-*`. Green sand (sand + blue clay) is what holds an
          // impression at all, so the bed takes the same prepared item the 1×1 cell is rammed with - one
          // moulding material across the whole casting suite.
          .Stage(s =>
            s.Require(
                $"{domain}:{GreenSandItemDefinitions.Code}",
                12,
                "iwex:rcc-ingredient-sand"
              )
              .AddElements(SandBedLayout.RunnersGroup)
          )
      )
      // No `sand` variant group. Carrying the rock type of whatever sand filled the bed (over vanilla's
      // 20-state `block/rock` property) would multiply the bed's codes twentyfold to record a purely
      // cosmetic fact. With one prepared moulding material there is nothing to record: every bed is
      // rammed with the same green sand, so the block is `casting-sandbed-{brick}-{side}` and the sand
      // texture is a constant. iwex has never shipped, so no migration is owed for the dropped group.
      .VariantGroup("brick", "black", "brown", "cream", "gray", "orange", "red", "tan")
      .SideVariant()
      .CreativeTab("general", "*-n")
      .CreativeTab("iwex", "*-n")
      // Caution: `iwex:sandcasting-bed` is the pre-rework shape and must not come back. It still carries
      // `SandFull` and has none of the ten per-slot elements SandBedLayout emits, so the bed rendered
      // through it shows nothing at all for a carved runner or mold - selective-element matching drops
      // an unknown name silently, so the failure is an invisible hole rather than an exception.
      // SandBedLayoutTests walks THIS shape; the two must stay the same file.
      .Shape("iwex:casting/sandcastingbed")
      .ShapeRotateYByType("*-n", 180)
      .ShapeRotateYByType("*-e", 90)
      .ShapeRotateYByType("*-s", 0)
      .ShapeRotateYByType("*-w", 270)
      // Brick colour is a tint overlay over the running-bond base (the ore-bunker pattern); the burned-clay
      // mold cavities stay fixed. The `andesite` key is the rammed-sand key the shapes are drawn against
      // - named for the rock the bed once defaulted to, back when it took any sand and keyed this per
      // variant. With one prepared moulding sand it simply is the green-sand texture, exactly as on the
      // 1×1 cell, and the shapes (which still declare the historical key) resolve straight through.
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

  // The north-orientation footprint: every carvable slot that is not the principal's own cell. Every cell of
  // the bed is now a slot - the basin's old brick shoulders became row 1's molds when the bed grew to four
  // rows - so this is generated FROM the layout rather than restated here, and the order it emits is the
  // order SlotCells zips against.
  private static IReadOnlyList<FillerCellSpec> Footprint()
  {
    var cells = new List<FillerCellSpec>();
    foreach (BedSlot slot in SandBedLayout.FillerSlots)
    {
      (int dx, int dz) = SandBedLayout.OffsetOf(slot);
      cells.Add(new FillerCellSpec(dx, 0, dz, Behaviors: [slot.IsMold ? MoldCell(slot) : RunnerCell]));
    }
    return cells;
  }

  /// <summary>
  /// The world position of every carvable slot for a bed at <paramref name="pos"/>. Built by zipping the
  /// resolved footprint against the specs it was generated from - <see cref="Footprint"/> and
  /// <c>FootprintCells</c> preserve order, and both come from <see cref="SandBedLayout"/>, so there is no
  /// separate inverse rotation to get wrong. The principal's own cell carries row 1's runner (the basin).
  /// </summary>
  public IReadOnlyDictionary<BlockPos, BedSlot> SlotCells(BlockPos pos)
  {
    var map = new Dictionary<BlockPos, BedSlot>
    {
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

  /// <summary>
  /// A click on the bed's <b>principal</b> cell. Without this the principal's own slot is unreachable:
  /// <see cref="BlockEntitySandCastingBed.OnCellInteract"/> is otherwise only wired through the filler path, and
  /// <c>SandBedLayout.FillerSlots</c> deliberately excludes offset (0,0) - which is exactly where row 1's
  /// centre slot sits. So one slot of the bed could never be carved or harvested however the player clicked.
  /// Routes identically to the filler path, falling through to construction when the cell has nothing to do.
  /// </summary>
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    Bed(world, blockSel.Position)?.OnCellInteract(blockSel.Position, byPlayer) == true
    || base.OnBlockInteractStart(world, byPlayer, blockSel);

  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) =>
    Bed(world, principalSel.Position)?.OnCellInteract(clickedCell, byPlayer) == true
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
