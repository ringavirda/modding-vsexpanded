using System;
using System.Collections.Generic;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// One horizontal slice of a shaft column, drawn as a 16-band window onto the <see cref="ChargeColumn"/>
/// its furnace owns. A renderer, not a container: it has no inventory and no per-block save, and the
/// charge belongs to the furnace. The band geometry lives on the block rather than the block entity
/// because the block owns collision and selection, and the same slab list feeds the mesh, the collision
/// box and the selection box. See <c>docs/design/layered-charge.md</c>.
/// </summary>
[BlockRegister]
public partial class BlockChargePile : Block, IExBlockDefProvider {
  #region Code-first definition

  /// <summary>
  /// The block code <c>SyncChargeBlocks</c> places into a chargeable cell. A shaft cell must accept this
  /// code or the furnace reads incomplete as soon as it is charged; the charge volume itself comes from
  /// <see cref="FurnaceCellRoles.Chargeable"/>, not from this string. Must match the code the def renders
  /// (<c>iiex:furnace-chargepile</c>) - any other code resolves to null and every <c>SetBlock</c> is
  /// skipped without error.
  /// </summary>
  public static readonly AssetLocation PileCode = new(
    "iiex",
    "furnace-chargepile"
  );

  /// <summary>
  /// No creative-inventory entry and no drops: the pile is materialised by the furnace, never hand-placed,
  /// and a pile that dropped items would duplicate charge the column still holds. <c>replaceable</c> stays
  /// at vanilla's 100 because <c>SetBlock</c> never consults it, while <c>IsReplacableBy</c>'s
  /// <c>&gt;= 6000</c> threshold would let a stray block placement overwrite part of a charged shaft.
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, BlockFurnaceCoreBase.FurnaceCode, "furnace/chargepile")
        // `type` names the family member; every furnace part shares the code `iiex:furnace`
        // (see BlockFurnaceCoreBase.FurnaceCode).
        .VariantGroup("type", "chargepile")
        .Class<BlockChargePile>()
        .EntityClass<BlockEntityChargePile>()
        .Material(EnumBlockMaterial.Soil)
        // A pile is a cell of the furnace layout, so an unfinished furnace can be previewed and completed
        // through it, as at a door or a tap.
        .Behavior("MultiblockStructure")
        .Shape("iiex:furnace/chargepile")
        .Texture("coke", "game:block/coal/coke")
        // The shaft takes two fuels priced apart (coke 2.0, charcoal 1.0 in materialroles.json - see
        // BlockEntityFurnaceCore.CarbonPerUnit), so their stripes must not look alike: a cheap course has
        // to stay visible in the wall afterwards.
        .Texture("charcoal", "game:block/coal/charcoal")
        // The burden stripe reuses the burden item's own ore-coal mix, so the course on the wall and the
        // stack in hand read as the same substance.
        .Texture("burden", "game:block/coal/orecoalmix")
        .NoDrops()
        // Loose material in a shaft: it must not cull the furnace's own faces, and furnace light has to
        // pass through the column rather than being absorbed band by band.
        .NonSolid()
        .LightAbsorption(0)
        .Replaceable(100)
        .Resistance(2f)
        .MaterialDensity(600)
        // Declared flat: the real boxes follow the fill height and are computed per block entity below, so
        // a pile whose furnace is gone has no collision at all.
        .SingleCollisionBox(0f, 0f, 0f, 1f, 0f, 1f)
        .SingleSelectionBox(0f, 0f, 0f, 1f, 0f, 1f)
        .Sound("walk", "walk/gravel")
        .Sound("place", "block/loosestone")
        .SoundByTool(EnumTool.Shovel, "block/loosegravel", "block/loosegravel")
        // Not obtainable; the shaft is documented on the furnace's handbook page instead.
        .HandbookExclude(),
    ];

  #endregion

  #region Band geometry

  /// <summary>Shape element drawing coke, and the fallback for any material not named below.</summary>
  public const string CokeElement = "Coke";

  /// <summary>Shape element drawing charcoal.</summary>
  public const string CharcoalElement = "Charcoal";

  /// <summary>Shape element drawing the ore-bearing charge.</summary>
  public const string BurdenElement = "Burden";

  /// <summary>Block-local height of one band, as a fraction of a block.</summary>
  public const float BandHeight = 1f / ChargeColumn.BandsPerBlock;

  /// <summary>
  /// A drawn stripe: one <see cref="ChargeBandRun"/> placed at its height in the block, in block-local
  /// units where 0 is the block floor and 1 its ceiling.
  /// </summary>
  /// <param name="Material">Item code the run draws, as <see cref="ChargeSegment.Material"/>.</param>
  /// <param name="Mix">Burden composition of the segment it came from; <c>default</c> for fuel.</param>
  /// <param name="FromY">Bottom of the stripe.</param>
  /// <param name="ToY">Top of the stripe.</param>
  public readonly record struct ChargeBandSlab(
    string Material,
    BurdenMix Mix,
    float FromY,
    float ToY
  );

  /// <summary>
  /// Incandescent block light off the hottest band this pile draws, so a charged shaft glows at the
  /// raceway and stays dark at the stockline - the counter-current temperature profile's only visible
  /// output. The block entity owns the level (<see cref="BlockEntityChargePile.GlowLightLevel"/>) and
  /// republishes it from <see cref="BlockEntityChargePile.OnColumnChanged"/>.
  /// </summary>
  public override byte[] GetLightHsv(
    IBlockAccessor blockAccessor,
    BlockPos pos,
    ItemStack? stack = null
  ) {
    if (
      pos != null
      && blockAccessor.GetBlockEntity(pos) is BlockEntityChargePile pile
    ) {
      byte val = pile.GlowLightLevel;
      if (val > 0)
        return [8, 7, val];
    }
    return base.GetLightHsv(blockAccessor, pos, stack);
  }

  /// <summary>
  /// The stripes <paramref name="runs"/> draw, bottom-first and touching: run <c>i</c> starts where run
  /// <c>i-1</c> ended, so the stack has no seams and its top is exactly <see cref="HeightOf"/>. Pure and
  /// separate from any mesh, because the same list feeds the mesh, the collision box and the selection
  /// box, which must not disagree.
  /// </summary>
  public static List<ChargeBandSlab> SlabsOf(IReadOnlyList<ChargeBandRun>? runs) {
    var slabs = new List<ChargeBandSlab>();
    if (runs == null)
      return slabs;

    int band = 0;
    foreach (ChargeBandRun run in runs) {
      if (run.Bands <= 0)
        continue;
      int top = Math.Min(band + run.Bands, ChargeColumn.BandsPerBlock);
      if (top <= band)
        break;
      slabs.Add(
        new ChargeBandSlab(
          run.Material,
          run.Mix,
          band * BandHeight,
          top * BandHeight
        )
      );
      band = top;
    }
    return slabs;
  }

  /// <summary>How far up the block the charge stands, 0 above the stockline and 1 for a full block. The
  /// collision box follows it.</summary>
  public static float HeightOf(IReadOnlyList<ChargeBandRun>? runs) {
    if (runs == null)
      return 0f;
    int bands = 0;
    foreach (ChargeBandRun run in runs)
      bands += Math.Max(0, run.Bands);
    return Math.Min(bands, ChargeColumn.BandsPerBlock) * BandHeight;
  }

  /// <summary>
  /// Which shape element draws <paramref name="material"/>: burden, charcoal, or coke for everything else
  /// the shaft holds. Keyed on the code string rather than on a resolved collectible or a fuel role, so
  /// the function is total and a column keeps drawing after the mod that owned a material is removed.
  /// Runs on the tesselation thread (see <c>BlockEntityChargePile.OnTesselation</c>) and must touch
  /// nothing but its argument. Charcoal matches by substring, so any <c>*-charcoal</c> another mod ships
  /// draws as charcoal; only substances the shape has art for can be named.
  /// </summary>
  public static string ElementOf(string? material) {
    if (string.IsNullOrEmpty(material))
      return CokeElement;
    int colon = material.IndexOf(':');
    string path = colon >= 0 ? material[(colon + 1)..] : material;
    // Burden first: it is the one stripe tested by suffix, and a burden code can never also be a fuel.
    if (path.EndsWith("burden", StringComparison.Ordinal))
      return BurdenElement;
    if (path.Contains("charcoal", StringComparison.Ordinal))
      return CharcoalElement;
    // Fallback: an unknown material draws as coke rather than as a hole in the shaft wall.
    return CokeElement;
  }

  #endregion

  #region Collision and selection

  // Both follow the actual fill height. The selection box keeps a one-band floor even when nothing is
  // drawn, so an orphaned pile - its furnace broken out from under it - stays clickable and removable.
  private static readonly Cuboidf[] Nothing = [];

  private Cuboidf[] BoxesAt(IBlockAccessor accessor, BlockPos pos, float floor) {
    float height = accessor.GetBlockEntity(pos) is BlockEntityChargePile pile
      ? pile.FillHeight
      : 0f;
    height = Math.Max(height, floor);
    return height <= 0f ? Nothing : [new Cuboidf(0f, 0f, 0f, 1f, height, 1f)];
  }

  public override Cuboidf[] GetCollisionBoxes(
    IBlockAccessor accessor,
    BlockPos pos
  ) => BoxesAt(accessor, pos, 0f);

  public override Cuboidf[] GetSelectionBoxes(
    IBlockAccessor accessor,
    BlockPos pos
  ) => BoxesAt(accessor, pos, BandHeight);

  #endregion

  #region Break safety

  /// <summary>Always empty: the furnace holds the charge, so dropping items here would hand out a second
  /// copy of units still in the column.</summary>
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1
  ) => [];

  /// <summary>
  /// Splices units <c>[h·perBlock, (h+1)·perBlock)</c> out of the column the block draws, drops them one
  /// stack per material and grade, and lets everything above settle onto the gap; <c>SyncChargeBlocks</c>
  /// then re-materialises the wall one block lower, so the mined pile returns if there is still charge at
  /// that height. The mid-column break is required for recovery, because a chill sits at the bottom of the
  /// shaft and cannot be reached from the stockline. See <c>docs/design/layered-charge.md</c>.
  /// <para>
  /// An orphan - no core, or a cell outside the shaft box - breaks normally and vanishes, so a half-broken
  /// furnace cannot leave a block that renders nothing and cannot be removed.
  /// </para>
  /// </summary>
  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityChargePile {
        BelongsToFurnace: true
      } pile
    ) {
      foreach (ItemStack stack in pile.TakeWindow())
        world.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 0.5, 0.5));

      // Resync after the block is gone, never before; see ResyncOwner.
      base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
      pile.ResyncOwner();
      return;
    }

    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  #endregion

  #region Placement

  /// <summary>
  /// Refuses hand placement outside a shaft whose core owns the cell, which would otherwise leave an
  /// orphan that renders nothing. The furnace's own materialisation goes through <c>SetBlock</c> and never
  /// reaches this.
  /// </summary>
  public override bool CanPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref string failureCode
  ) {
    // The shaft test comes first, so the failure message names the real reason rather than whatever
    // generic obstruction the base would have reported.
    if (
      BlockEntityMultiblockStructure.FindAnchorOwning<BlockEntityFurnaceCore>(
        world,
        blockSel.Position,
        BlockEntityFurnaceCore.ComponentScanHorizontal,
        BlockEntityFurnaceCore.ComponentScanBelow,
        BlockEntityFurnaceCore.ComponentScanAbove
      )
        is not { } core
      || core.ChargeColumnAt(blockSel.Position, out _) == null
    ) {
      failureCode = "iiex-chargepile-notinshaft";
      return false;
    }

    return base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode);
  }

  #endregion

  #region Interaction

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is not BlockEntityChargePile pile
    )
      return false;

    // The build-outline gesture first, so ctrl+shift+right-click previews an unfinished furnace rather
    // than scooping a handful out of it.
    if (
      BlockBehaviorMultiblockStructure.TryToggleProjection(
        world,
        byPlayer,
        blockSel.Position
      )
    )
      return true;
    if (world.Side != EnumAppSide.Server)
      return true;

    ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
    if (active is { Empty: false })
      // Adding by hand needs the band-order rule (coke only above the last burden, lowest columns first),
      // which does not exist yet, so a held stack does nothing rather than laying a course that rule
      // would refuse.
      return true;

    if (pile.TryTakeTop() is not { } taken)
      return true;

    if (byPlayer.InventoryManager?.TryGiveItemstack(taken) != true)
      world.SpawnItemEntity(
        taken,
        blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5)
      );
    return true;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
    var help = new List<WorldInteraction>
    {
      new()
      {
        ActionLangCode = IiexLang.ChargepileHelpTake,
        MouseButton = EnumMouseButton.Right,
      },
    };
    if (
      world.BlockAccessor.GetBlockEntity(selection.Position)
        is BlockEntityChargePile pile
      && pile.ResolveOwningAnchor() is { StructureComplete: false }
    )
      help.AddRange(BlockBehaviorMultiblockStructure.ProjectionHelp(this));
    return [.. help];
  }

  #endregion
}
