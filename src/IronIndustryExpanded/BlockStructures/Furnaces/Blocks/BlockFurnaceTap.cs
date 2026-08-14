using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockNetworkMolten.Blocks;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// A tap-hole in the hearth wall of a shaft furnace. Right-clicking with an empty hand toggles pouring;
/// what comes out runs into the canal start beneath the spout. Two blocktypes: the iron notch at the
/// crucible floor and the cinder notch higher in the same wall, below the tuyeres. Each carries its own
/// code, so a layout glyph names the tap it means and the wrong one built in a tap cell does not
/// complete the structure; <see cref="CellRole.MetalTap"/> and <see cref="CellRole.SlagTap"/> serve only
/// the lookup (<c>MetalTapPos</c> / <c>SlagTapPos</c>).
/// <para>
/// Both types share one shape until the drawn art is adopted, which needs both taps at y=1 rather than a
/// course apart as the layouts have them. See <c>docs/design/layered-charge.md</c> § Typed taps.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockFurnaceTap : Block, IExBlockDefProvider {
  /// <summary>The lower tap-hole, at the crucible floor: drains the metal pool.</summary>
  public const string IronType = "irontap";

  /// <summary>The upper tap-hole, high in the same wall: skims the slag floating on the metal.</summary>
  public const string SlagType = "slagtap";

  #region Code-first definition

  /// <summary>The two shaft-furnace tap-holes: animatable, horizontally orientable ceramic
  /// spouts.</summary>
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
      // Build outline: a tap is a functional cell of the furnace layout, so a player at the tap can
      // preview and complete an incomplete furnace. This carries the help line; the interaction is
      // forwarded explicitly in OnBlockInteractStart below, which overrides without calling base.
      .Behavior("MultiblockStructure")
      .Behavior("ExOrientable")
      // `type` names the family member; every furnace part shares the code `iiex:furnace`
      // (see BlockFurnaceCoreBase.FurnaceCode and N7).
      .VariantGroup("type", type)
      .SideVariant()
      // One shape for both types until the drawn art is adopted; see the class remarks. The path is
      // `furnace/tap` rather than either type's, to mark it as shared.
      .ShapeByTypePerOrientation("iiex:furnace/tap", 0)
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
  ) {
    // This override handles interaction without calling base, so the MultiblockStructure behaviour's own
    // click never runs. The build-outline gesture is forwarded to the shared entry point first, so
    // Ctrl+Shift+right-click previews an incomplete furnace instead of toggling the pour.
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
    ) {
      // Prevent toggling if the player is holding an item/block
      if (!byPlayer.Entity.RightHandItemSlot.Empty)
        return false;

      // Opening requires a canal start directly below the tap's spout.
      bool isOpening = !tap.IsPouring;
      if (isOpening) {
        // ExOrientation.FacingFromSide, not BlockFacing.FromCode: the latter returns null for a
        // single-letter side token. Null-checked besides, so a tap whose variant names no facing
        // refuses instead of throwing.
        BlockFacing? facing = ExOrientation.FacingFromSide(Variant["side"]);
        if (facing == null)
          return false;
        BlockPos startPos = blockSel
          .Position.AddCopy(facing.Opposite)
          .DownCopy();
        if (world.BlockAccessor.GetBlock(startPos) is not BlockMoltenCanalStart) {
          (world.Api as ICoreClientAPI)?.TriggerIngameError(
            this,
            "nocanal",
            Lang.Get("iiex:tap-err-nocanal")
          );
          return true;
        }
      }

      // There is no separate opened/closed block: pouring state lives on the
      // block entity and is shown by holding the "open" animation pose (see
      // BlockEntityFurnaceTap.ApplyPourPose).
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
  ) {
    var baseHelp =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    var toggleHelp = new WorldInteraction {
      ActionLangCode = "iiex:blockhelp-tap-toggle",
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
  /// The stack a picked or mined tap becomes: this tap's own type, normalised to the <c>s</c> facing so a
  /// mined tap stacks with a crafted one instead of splitting the inventory four ways. The type is
  /// carried through, so an iron tap never comes back as a slag tap. <c>s</c> rather than
  /// <see cref="ExpandedLib.Blocks.Behaviors.BlockBehaviorExOrientable"/>'s canonical <c>n</c>, to match the creative entry
  /// (<c>*-s</c>) and both grid recipes; these overrides do not call base, so the behaviour's own
  /// normalisation never runs here. A code that resolves to no block falls back to <c>this</c>, so a
  /// wrong token stops normalisation silently rather than failing.
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
