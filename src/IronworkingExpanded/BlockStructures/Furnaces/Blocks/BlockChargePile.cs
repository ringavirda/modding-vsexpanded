using System;
using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.Items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// One horizontal slice of a shaft column drawn as bands - the 16-band window onto the
/// <see cref="ChargeColumn"/> its furnace owns, and the block the player actually sees and clicks when
/// they look into a charged furnace.
/// <para>
/// <b>It is a renderer, not a container.</b> It has no inventory, no stack size, no per-block save and
/// no <c>BlockEntityItemPile</c> anywhere in its ancestry: the charge belongs to the furnace, and this
/// block is a window onto it. That is what makes it safe where <c>game:coalpile</c> was not - vanilla's
/// pile collapses its own column from inside its block entity
/// (<c>TriggerPileChanged -> TryPartialCollapse</c>, both private, reached from interaction and merge),
/// which is why consumption had to be written top-down. None of that machinery is inherited, so the
/// collapse is not suppressed here, it is <b>absent</b>. See <c>docs/design/layered-charge.md</c>.
/// </para>
/// <para>
/// The geometry below is pure and lives here rather than in the block entity because the block is what
/// owns collision and selection: the same slab list feeds the mesh, the collision box and the selection
/// box, so what you stand on is what you see.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockChargePile : Block, IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>
  /// The block code a shaft layout has to admit for a pile to be allowed to stand in it - the code
  /// <c>SyncChargeBlocks</c> <c>SetBlock</c>s into a chargeable cell. Stated once so the demand and the
  /// block cannot come to mean different strings.
  /// <para>
  /// <b>No longer how the furnace finds its charge volume.</b> <c>ChargeableCells</c> asked
  /// <c>CellsAccepting(PileCode)</c> until the layouts gained <see cref="CellRole.Chargeable"/>; it now
  /// asks the role, so the volume no longer depends on this string appearing inside three separate shaft
  /// legends. What survives is the reverse duty: a shaft cell must still <em>accept</em> this code or the
  /// furnace reads incomplete the moment it is charged and puts itself out. The two answers being the same
  /// set is pinned by <c>ChargeableCellsTests</c>, which is the only cross-check that a role was hung on
  /// the right glyph.
  /// </para>
  /// <para>
  /// Not named <c>Code</c>: <see cref="Block.Code"/> is the instance's own placed code, and shadowing it
  /// with a static would read as the same thing at every call site.
  /// </para>
  /// <para>
  /// <b>The constant must match the code the def actually renders.</b> Every furnace part shares the
  /// code <c>iwex:furnace</c> with a <c>type</c> variant, so
  /// the def renders <c>iwex:furnace-chargepile</c>. A constant naming any other code resolves
  /// <b>null</b>, skips every
  /// <c>SetBlock</c>, and leaves a charged furnace drawing no piles with the whole suite green. Pinned
  /// against the rendered def by <c>ChargePileTests.PileCode_is_the_code_the_definition_actually_renders</c>
  /// so a rename cannot open that gap.
  /// </para>
  /// </summary>
  public static readonly AssetLocation PileCode = new("iwex", "furnace-chargepile");

  /// <summary>
  /// <b>No creative-inventory entry, and no drops.</b> The pile only means anything inside a shaft whose
  /// core owns its cell, so it is furnace-materialised and never hand-placed; and a pile that dropped items
  /// would <b>duplicate</b> the charge, because the furnace still holds every unit the block is drawing.
  /// <para>
  /// <c>replaceable</c> is vanilla's <b>100</b>, not something high. The furnace places and removes piles
  /// with <c>SetBlock</c>, which never consults <c>Replaceable</c> at all - so a high value buys the descent
  /// nothing, while <c>IsReplacableBy</c>'s <c>&gt;= 6000</c> threshold would let any stray block placement
  /// silently overwrite a section of a charged shaft.
  /// </para>
  /// </summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, BlockFurnaceCoreBase.FurnaceCode, "furnace/chargepile")
        // `type` names the family member; every furnace part shares the code `iwex:furnace`
        // (see BlockFurnaceCoreBase.FurnaceCode).
        .VariantGroup("type", "chargepile")
        .Class<BlockChargePile>()
        .EntityClass<BlockEntityChargePile>()
        .Material(EnumBlockMaterial.Soil)
        // The build outline: a pile is a cell of the furnace layout, so a player standing at the shaft can
        // preview and complete an unfinished furnace through it, exactly as at a door or a tap.
        .Behavior("MultiblockStructure")
        .Shape("iwex:furnace/chargepile")
        .Texture("coke", "game:block/coal/coke")
        // The shaft takes two fuels and they are priced apart (coke 2.0, charcoal 1.0 in
        // materialroles.json - see BlockEntityFurnaceCore.CarbonPerUnit), so they must not look alike:
        // a player laying a cheap course has to be able to see it in the wall afterwards. Vanilla already
        // names the texture, so the second fuel stripe costs one key and no new PNG - the same trade
        // BlockFirebox makes for its four fuels.
        .Texture("charcoal", "game:block/coal/charcoal")
        // The burden stripe wears the same ore-coal mix the burden item does, so a course of burden on the
        // shaft wall and the stack in the player's hand read as the same substance.
        .Texture("burden", "game:block/coal/orecoalmix")
        .NoDrops()
        // Loose material in a shaft: it must not cull the furnace's own faces, and the furnace's light has
        // to pass through the column rather than being absorbed by every band of it.
        .NonSolid()
        .LightAbsorption(0)
        .Replaceable(100)
        .Resistance(2f)
        .MaterialDensity(600)
        // Declared flat, like vanilla's pile: the real boxes are the fill height and are computed per block
        // entity below. A pile whose furnace is gone therefore has no collision at all.
        .SingleCollisionBox(0f, 0f, 0f, 1f, 0f, 1f)
        .SingleSelectionBox(0f, 0f, 0f, 1f, 0f, 1f)
        .Sound("walk", "walk/gravel")
        .Sound("place", "block/loosestone")
        .SoundByTool(EnumTool.Shovel, "block/loosegravel", "block/loosegravel")
        // Not obtainable, so a handbook page for it would be a page about a block the player can never
        // hold; the shaft is taught on the furnace's page instead.
        .HandbookExclude(),
    ];

  #endregion

  #region Band geometry

  /// <summary>Shape element drawing coke, and the fallback for any fuel not named below. Dark and
  /// glassy.</summary>
  public const string CokeElement = "Coke";

  /// <summary>Shape element drawing charcoal. Matte black, and visibly not coke.</summary>
  public const string CharcoalElement = "Charcoal";

  /// <summary>Shape element drawing the ore-bearing charge. Rusty.</summary>
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
  /// The stripes <paramref name="runs"/> draw, bottom-first and touching: run <c>i</c> starts where run
  /// <c>i-1</c> ended, so the stack has no seams and its top is exactly <see cref="HeightOf"/>.
  /// <para>
  /// Pure, and separate from any mesh, for the reason <c>FillQuads.BoxesFrom</c> is: this arithmetic is
  /// the part that can be wrong in a way nobody notices in a screenshot, and it feeds three consumers
  /// (the mesh, the collision box, the selection box) that must not disagree.
  /// </para>
  /// </summary>
  /// <summary>
  /// Incandescent block light off the hottest band this pile draws, so a charged shaft glows white at the
  /// raceway and stays dark at the stockline. The same <c>GetLightHsv</c> + <c>MarkBlockDirty</c>-on-change
  /// idiom as the molten canals, the barrel and the casting molds; the block entity owns the level
  /// (<see cref="BlockEntityChargePile.GlowLightLevel"/>) and republishes it from
  /// <see cref="BlockEntityChargePile.OnColumnChanged"/>.
  /// <para>
  /// This is the counter-current profile's only visible output. Without it the temperature every band
  /// carries is real, saved and simulated, and completely invisible to the player standing in front of
  /// the furnace.
  /// </para>
  /// </summary>
  public override byte[] GetLightHsv(
    IBlockAccessor blockAccessor,
    BlockPos pos,
    ItemStack? stack = null
  )
  {
    if (
      pos != null
      && blockAccessor.GetBlockEntity(pos) is BlockEntityChargePile pile
    )
    {
      byte val = pile.GlowLightLevel;
      if (val > 0)
        return [8, 7, val];
    }
    return base.GetLightHsv(blockAccessor, pos, stack);
  }

  public static List<ChargeBandSlab> SlabsOf(IReadOnlyList<ChargeBandRun>? runs)
  {
    var slabs = new List<ChargeBandSlab>();
    if (runs == null)
      return slabs;

    int band = 0;
    foreach (ChargeBandRun run in runs)
    {
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

  /// <summary>How far up the block the charge stands, 0 for a block above the stockline and 1 for a full
  /// one. What the collision box follows, exactly as vanilla's pile height does.</summary>
  public static float HeightOf(IReadOnlyList<ChargeBandRun>? runs)
  {
    if (runs == null)
      return 0f;
    int bands = 0;
    foreach (ChargeBandRun run in runs)
      bands += Math.Max(0, run.Bands);
    return Math.Min(bands, ChargeColumn.BandsPerBlock) * BandHeight;
  }

  /// <summary>
  /// Which shape element draws <paramref name="material"/>: burden is the ore-bearing charge and reads
  /// rusty, charcoal reads matte black, and everything else the shaft holds is fuel and reads as coke.
  /// <para>
  /// Decided on the <b>code</b> rather than on a resolved collectible, because a column stores codes and
  /// has to keep drawing after the mod that owned one has been removed - and because it is then a total
  /// function: no material can fail to draw a stripe.
  /// </para>
  /// <para>
  /// <b>Three-way since charcoal was priced.</b> The comment that stood here argued the classifier should
  /// stay two-way until a third material actually existed, because "a classifier with no third material to
  /// classify would be guessing at what distinguishes them". That reasoning was right and it has now been
  /// <b>paid off, not overturned</b>: the third material arrived when charcoal stopped being coke's equal.
  /// The shaft had accepted charcoal and burned it at coke's rate all along, so drawing them alike cost
  /// nothing; now that a charcoal band is worth <b>half</b> the carbon of a coke band
  /// (<c>BlockEntityFurnaceCore.CarbonPerUnit</c>), a column of each is a column with twice the fuel, and
  /// two stripes that render pixel-identical would hide the single most consequential choice the player
  /// makes about a charge. The mismatch is only visible in the wall.
  /// </para>
  /// <para>
  /// <b>Still keyed on the code, deliberately not on the fuel role.</b> The tempting generalisation is to
  /// ask <c>MaterialRoles</c> for the role value and pick an element per weight, so a later fuel classifies
  /// itself. It is refused for three reasons: this runs on the <b>tesselation thread</b> and must touch
  /// nothing but its argument (see <c>BlockEntityChargePile.OnTesselation</c>); a role lookup is not total,
  /// so a fuel the config forgot would draw nothing rather than drawing wrong; and an element is a
  /// <b>drawing</b>, not a number - there is no shape element to interpolate to for a value between 1.0 and
  /// 2.0. A classifier can only name substances the shape has art for, which is why it enumerates them.
  /// Matched by substring for the reason <c>BlockFirebox.TextureKeyOf</c> is: <c>game:charcoal</c> and any
  /// <c>*-charcoal</c> another mod ships are the same black stuff.
  /// </para>
  /// <para>
  /// <b>Still owed: the cupola's pig and scrap.</b> When it takes them directly they will draw as coke,
  /// on the same terms as before - a fourth element and a fourth branch, added <em>with</em> the materials.
  /// </para>
  /// </summary>
  public static string ElementOf(string? material)
  {
    if (string.IsNullOrEmpty(material))
      return CokeElement;
    int colon = material.IndexOf(':');
    string path = colon >= 0 ? material[(colon + 1)..] : material;
    // Burden first: it is the one stripe tested by suffix, and a burden code can never also be a fuel.
    if (path.EndsWith("burden", StringComparison.Ordinal))
      return BurdenElement;
    if (path.Contains("charcoal", StringComparison.Ordinal))
      return CharcoalElement;
    // Coke, and the honest fallback for anything a later mod charges: an unknown material draws as the
    // metallurgical default rather than as a hole in the shaft wall.
    return CokeElement;
  }

  #endregion

  #region Collision and selection

  // Both follow the actual fill height, as vanilla's pile does - what you stand on is what you see. The
  // selection box keeps a one-band floor even when nothing is drawn: an orphaned pile (its furnace broken
  // out from under it) renders nothing, and without something to click it would be unremovable.
  private static readonly Cuboidf[] Nothing = [];

  private Cuboidf[] BoxesAt(IBlockAccessor accessor, BlockPos pos, float floor)
  {
    float height =
      accessor.GetBlockEntity(pos) is BlockEntityChargePile pile
        ? pile.FillHeight
        : 0f;
    height = Math.Max(height, floor);
    return height <= 0f ? Nothing : [new Cuboidf(0f, 0f, 0f, 1f, height, 1f)];
  }

  public override Cuboidf[] GetCollisionBoxes(IBlockAccessor accessor, BlockPos pos) =>
    BoxesAt(accessor, pos, 0f);

  public override Cuboidf[] GetSelectionBoxes(IBlockAccessor accessor, BlockPos pos) =>
    BoxesAt(accessor, pos, BandHeight);

  #endregion

  #region Break safety

  /// <summary>Never anything. The furnace holds the charge; a pile that dropped items would hand the
  /// player a second copy of units that are still in the column.</summary>
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1
  ) => [];

  /// <summary>
  /// <b>Breaking a pile digs that block's charge back out of the shaft, and everything above falls.</b>
  /// The block is a window onto a column the furnace owns, so the break splices units
  /// <c>[h·perBlock, (h+1)·perBlock)</c> out of the column, drops them one stack per material and grade, and
  /// lets the rest settle - see <c>docs/design/layered-charge.md</c>.
  /// <para>
  /// <b>The mid-column break must be allowed.</b> A rule of "empty a shaft from
  /// the top instead" fails because a <b>chill sits at the bottom of the shaft by definition</b>, so a
  /// recovery that only reaches the stockline leaves a chilled furnace permanently bricked, with the
  /// recoverability gate reading green. <c>ChargeColumn.TakeSpan</c> is what makes the mid-column splice
  /// expressible.
  /// </para>
  /// <para>
  /// <b>The wall repairs itself and there is no hole.</b> Removing the block is not removing the charge -
  /// the column simply got shorter, so <c>SyncChargeBlocks</c> re-materialises the wall one block down and
  /// the pile the player just mined comes straight back if there is still charge at that height. Everything
  /// above falling is one list rebuild with no block writes, which is the behaviour that made vanilla's coal
  /// piles unusable and is free here.
  /// </para>
  /// <para>
  /// An <b>orphan</b> - no core, or a cell outside the shaft box - breaks normally and vanishes, exactly
  /// as before. That is the escape hatch for a half-broken furnace: a block that renders nothing, collides
  /// with nothing and cannot be removed would be a permanent scar.
  /// </para>
  /// </summary>
  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(pos)
      is BlockEntityChargePile { BelongsToFurnace: true } pile
    )
    {
      foreach (ItemStack stack in pile.TakeWindow())
        world.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 0.5, 0.5));

      // Resync after the block is gone, never before - see ResyncOwner for what the other order does.
      base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
      pile.ResyncOwner();
      return;
    }

    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  #endregion

  #region Placement

  /// <summary>
  /// A pile only means anything inside a shaft whose core owns its cell, so placing one anywhere else
  /// fails cleanly rather than producing an orphan that renders nothing. Code placement (the furnace's own
  /// materialisation) goes through <c>SetBlock</c> and never reaches this.
  /// </summary>
  public override bool CanPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref string failureCode
  )
  {
    // The shaft test comes first, so the message a player gets names the real reason. Left to the base it
    // would report whatever generic obstruction it found and the actual rule would never be stated.
    if (
      BlockEntityMultiblockStructure.FindAnchorOwning<BlockEntityFurnaceCore>(
        world,
        blockSel.Position,
        BlockEntityFurnaceCore.ComponentScanHorizontal,
        BlockEntityFurnaceCore.ComponentScanBelow,
        BlockEntityFurnaceCore.ComponentScanAbove
      ) is not { } core
      || core.ChargeColumnAt(blockSel.Position, out _) == null
    )
    {
      failureCode = "iwex-chargepile-notinshaft";
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
  )
  {
    if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityChargePile pile)
      return false;

    // The build-outline gesture first, so ctrl+shift+right-click previews an unfinished furnace instead of
    // scooping a handful out of it.
    if (BlockBehaviorMultiblockStructure.TryToggleProjection(world, byPlayer, blockSel.Position))
      return true;
    if (world.Side != EnumAppSide.Server)
      return true;

    ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
    if (active is { Empty: false })
      // Adding by hand needs the band-order rule (coke only above the last burden, lowest columns first),
      // which is Task 3.2's. Until it exists a held stack does nothing rather than laying a course the
      // rule would have refused.
      return true;

    if (pile.TryTakeTop() is not { } taken)
      return true;

    if (byPlayer.InventoryManager?.TryGiveItemstack(taken) != true)
      world.SpawnItemEntity(taken, blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5));
    return true;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  )
  {
    var help = new List<WorldInteraction>
    {
      new()
      {
        ActionLangCode = "iwex:furnace-chargepile-help-take",
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
