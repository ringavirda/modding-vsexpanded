using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten.Blocks;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// A tap-hole in the hearth wall of a shaft furnace. Right-clicking with an empty hand toggles pouring;
/// what comes out runs into the canal start beneath the spout.
/// <para>
/// <b>Two blocktypes come off this one class, because a furnace has two tap-holes and they are not
/// interchangeable.</b> Real hearth anatomy puts the <b>iron notch</b> at the crucible floor and the
/// <b>cinder notch</b> higher in the same wall, below the tuyeres - so which hole a player builds is a
/// decision the drawing should be able to state.
/// </para>
/// <para>
/// <b>It could not state it before this split.</b> Both taps were one blocktype, so the three shaft
/// layouts drew <c>T</c> and <c>S</c> pointing at the <em>same</em> code and told them apart only by
/// <see cref="CellRole.MetalTap"/> / <see cref="CellRole.SlagTap"/> - a workaround for there being one
/// block, not a design. A player could build the iron tap in the slag cell and the structure completed
/// anyway. With two types each glyph carries its own code, and the roles go back to being what they are
/// for: the <em>lookup</em> (<c>MetalTapPos</c> / <c>SlagTapPos</c> ask the drawing where they are).
/// </para>
/// <para>
/// The two share one shape for now. The drawn art
/// (<c>assets/editable/shapes/furnace-block-{iron,slag}tap.json</c>) puts the height difference in the
/// <b>geometry</b> - iron spouts from 2/16, slag from 10/16 - which only reads correctly once both taps
/// sit at y=1, and today's layouts still stack them a course apart. Adopting the shapes is part of that
/// layout move, not of this split; see <c>docs/design/layered-charge.md</c> § Typed taps.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockFurnaceTap : Block, IExBlockDefProvider
{
  /// <summary>The lower tap-hole, at the crucible floor: drains the metal pool.</summary>
  public const string IronType = "irontap";

  /// <summary>The upper tap-hole, high in the same wall: skims the slag floating on the metal.</summary>
  public const string SlagType = "slagtap";

  #region Code-first definition

  /// <summary>The two shaft-furnace tap-holes, authored in C# (migrated from blastfurnace/tap.json).
  /// Animatable, horizontally orientable ceramic spouts.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Tap(domain, IronType), Tap(domain, SlagType)];

  private static ExBlockDef Tap(string domain, string type) =>
    ExBlockDef
      .Create(domain, BlockFurnaceCoreBase.FurnaceCode, "furnace/" + type)
      .Class<BlockFurnaceTap>()
      .EntityClass<BlockEntityFurnaceTap>()
      .EntityBehavior("Animatable")
      .Material(EnumBlockMaterial.Ceramic)
      .MaxStackSize(1)
      // The build-outline projection: a tap is a functional cell of the furnace layout, so a player at the
      // tap can preview + complete an incomplete furnace. This carries the help line; the interaction is
      // forwarded explicitly in OnBlockInteractStart below, which overrides without calling base.
      .Behavior("MultiblockStructure")
      .Behavior("ExOrientable")
      // `type` names the family member; every furnace part shares the code `iwex:furnace`
      // (see BlockFurnaceCoreBase.FurnaceCode and N7).
      .VariantGroup("type", type)
      .SideVariant()
      // ⏳ One shape for both types until the drawn art lands with the y=1 layout move - see the class
      // remarks. The path is `furnace/tap` rather than either type's, so it is obvious it is shared.
      .ShapeByTypePerOrientation("iwex:furnace/tap", 0)
      .CreativeCommon("*-s")
      .Replaceable(400)
      .Resistance(3.5f)
      .LightAbsorption(3)
      .Sound("walk", "walk/stone")
      .Sound("place", "block/ceramicplace")
      .SoundByTool(
        EnumTool.Pickaxe,
        "block/rock-hit-pickaxe",
        "block/rock-break-pickaxe"
      )
      .NonSolid();

  #endregion

  /// <summary>Which tap-hole this is - <see cref="IronType"/> or <see cref="SlagType"/>.</summary>
  public string TapType => Variant["type"];

  #region Interaction

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  )
  {
    // This override handles interaction without calling base, so the MultiblockStructure behaviour's own
    // click never runs; forward the build-outline gesture to the shared entry point first, so Ctrl+Shift+
    // right-click previews an incomplete furnace instead of toggling the pour.
    if (
      BlockBehaviorMultiblockStructure.TryToggleProjection(
        world,
        byPlayer,
        blockSel.Position
      )
    )
      return true;

    if (
      world.BlockAccessor.GetBlockEntity(blockSel.Position)
      is BlockEntityFurnaceTap tap
    )
    {
      // Prevent toggling if the player is holding an item/block
      if (!byPlayer.Entity.RightHandItemSlot.Empty)
        return false;

      // Opening requires a canal start directly below the tap's spout.
      bool isOpening = !tap.IsPouring;
      if (isOpening)
      {
        // ExOrientation.FacingFromSide, not BlockFacing.FromCode: the latter returns null for a
        // single-letter token and this dereferences it immediately, so a letter-spelled side would
        // crash the interact rather than misbehave. Nullable-checked besides, because a tap whose
        // variant names no facing at all should refuse, not throw.
        BlockFacing? facing = ExOrientation.FacingFromSide(Variant["side"]);
        if (facing == null)
          return false;
        BlockPos startPos = blockSel
          .Position.AddCopy(facing.Opposite)
          .DownCopy();
        if (world.BlockAccessor.GetBlock(startPos) is not BlockMoltenCanalStart)
        {
          (world.Api as ICoreClientAPI)?.TriggerIngameError(
            this,
            "nocanal",
            Lang.Get("iwex:tap-err-nocanal")
          );
          return true;
        }
      }

      // The tap no longer swaps to a separate opened/closed block - its pouring
      // state lives on the block entity and is shown by holding the "open"
      // animation pose (see BlockEntityFurnaceTap.ApplyPourPose).
      if (world.Side == EnumAppSide.Server)
        tap.TogglePouring();

      world.PlaySoundAt(
        ExSounds.CokeOvenDoorOpen,
        blockSel.Position.X,
        blockSel.Position.Y,
        blockSel.Position.Z,
        byPlayer
      );

      return true;
    }
    return true;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  )
  {
    var baseHelp =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    var toggleHelp = new WorldInteraction
    {
      ActionLangCode = "iwex:blockhelp-tap-toggle",
      MouseButton = EnumMouseButton.Right,
      // Toggling needs an empty hand (a held item is placed instead). Gate the
      // hint on that rather than RequireFreeHand, which would draw an empty slot.
      ShouldApply = (wi, bs, es) => forPlayer.Entity.RightHandItemSlot.Empty,
    };

    return baseHelp.Append(toggleHelp).ToArray();
  }

  #endregion

  #region Drops

  /// <summary>
  /// The stack a picked or mined tap becomes: this tap's own type, normalised to the <c>s</c> facing -
  /// the one the creative entry and both grid recipes produce - so a mined tap stacks with a crafted one
  /// instead of splitting the inventory four ways. The <b>type</b> is carried through: an iron tap must
  /// never come back as a slag tap.
  /// <para>
  /// <b>The <c>?? this</c> fallback silently eats a dead code.</b> A wrong token here - a stale block
  /// name, or a variant value spelled in the wrong vocabulary (the <c>side</c> group renders single
  /// <b>letters</b>, not words) - makes <c>GetBlock</c> answer null; the fallback then hands back the
  /// block's own facing and the tap merely stops normalising, with nothing failing. Deriving the code
  /// is not the fix; the fix is that <see cref="BlastFurnaceTapTests"/> asserts the resulting code, so
  /// a wrong token fails instead of degrading.
  /// </para>
  /// <para>
  /// The token comes from <see cref="ExOrientation.TokenOf"/> rather than a literal, so it follows the
  /// side vocabulary if that ever respells again.
  /// </para>
  /// <para>
  /// <b><c>s</c> deliberately disagrees with <see cref="BlockBehaviorExOrientable"/>'s canonical,
  /// which is the scheme's first token (<c>n</c>).</b> These overrides do not call base, so the
  /// behaviour's version never runs on this block. The tap is the outlier on purpose - its creative entry
  /// (<c>*-s</c>) and both grid recipes already produce <c>s</c> - and
  /// <c>The_creative_entry_the_grid_recipe_and_the_drop_all_name_the_south_facing</c> pins the three
  /// together so they cannot drift apart silently: the cost if they do is that a crafted tap and a mined
  /// one stop stacking.
  /// </para>
  /// </summary>
  private ItemStack NormalisedStack(IWorldAccessor world) =>
    new(
      world.GetBlock(
        CodeWithVariant(
          "side",
          ExOrientation.TokenOf(BlockFacing.SOUTH, asLetter: true)
        )
      ) ?? this
    );

  public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos) =>
    NormalisedStack(world);

  public override ItemStack[] GetDrops(
    IWorldAccessor worldMap,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [NormalisedStack(worldMap)];

  #endregion
}
