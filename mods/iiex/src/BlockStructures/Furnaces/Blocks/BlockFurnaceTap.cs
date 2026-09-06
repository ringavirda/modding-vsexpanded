using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Helpers;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockNetworkMolten.Blocks;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// A tap-hole in the hearth wall of a shaft furnace. Right-clicking with an empty hand toggles pouring;
/// what comes out runs into the canal start beneath the spout. Two blocktypes: the iron notch at the
/// crucible floor and the cinder notch higher in the same wall, below the tuyeres. Each carries its own
/// code, so a layout glyph names the tap it means and the wrong one built in a tap cell does not
/// complete the structure; <see cref="FurnaceCellRoles.MetalTap"/> and <see cref="FurnaceCellRoles.SlagTap"/> serve only
/// the lookup (<c>MetalTapPos</c> / <c>SlagTapPos</c>).
/// <para>
/// Each type draws its own shape, and the two encode their notch heights: both taps belong at layout y=1
/// and the difference is art. The closed state is a clay plug, drawn by pruning the shape rather than
/// posed by an animator. See docs/design/processes/ironmaking.md § The ritual.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockFurnaceTap : Block, IExBlockDefProvider {
  /// <summary>The lower tap-hole, at the crucible floor: drains the metal pool.</summary>
  public const string IronType = "irontap";

  /// <summary>The upper tap-hole, high in the same wall: skims the slag floating on the metal.</summary>
  public const string SlagType = "slagtap";

  #region Code-first definition

  /// <summary>The two shaft-furnace tap-holes: horizontally orientable ceramic spouts, each with its own
  /// drawn notch height.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [Tap(domain, IronType), Tap(domain, SlagType)];

  private static ExBlockDef Tap(string domain, string type) =>
    ExBlockDef
      .Create(domain, BlockFurnaceCoreBase.FurnaceCode, "furnace/" + type)
      .Class<BlockFurnaceTap>()
      .EntityClass<BlockEntityFurnaceTap>()
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
      // One shape per type - the two notches sit at different heights in the model, which is the whole
      // reason they are separate blocktypes. Offset 0: both shapes run their launder out at +z, which is
      // where a tap declared `-n` pours (TryPourMetal spouts at facing.Opposite), so the side angles need
      // no reversal.
      .ShapeByTypePerOrientation($"iiex:furnace/{type}", 0)
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

  private static readonly AssetLocation FireClayCode = new("game:clay-fire");

  /// <summary>
  /// Three verbs on a tap:
  /// <list type="bullet">
  /// <item>Empty hand on a plugged tap: breaks the plug out and opens it. The plug is destroyed.</item>
  /// <item>A lit flame on an open tap: blows the furnace in.</item>
  /// <item><see cref="IiexValues.TapPlugClayCost"/> fire clay on an open tap: stops it again.</item>
  /// </list>
  /// Which is the sequence a blow-in is: break the plug, torch it, re-plug, blast on. Opening needs no
  /// canal under the spout - it used to refuse, which is what made that sequence unbuildable.
  /// </summary>
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) {
    // This override handles interaction without calling base, so the MultiblockStructure behaviour's own
    // click never runs. The build-outline gesture is forwarded to the shared entry point first, so
    // Ctrl+Shift+right-click previews an incomplete furnace instead of working the plug.
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
      is not BlockEntityFurnaceTap tap
    )
      return true;

    ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
    ItemStack? held = activeSlot?.Itemstack;

    if (tap.IsPlugged) {
      // Breaking the plug takes a free hand. A held item would be placed instead, so the gesture has to
      // be the empty-handed one the old toggle used.
      if (held != null)
        return false;

      if (world.Side == EnumAppSide.Server)
        tap.SetPlugged(false);
      PlayPlugSound(world, blockSel.Position, byPlayer, ExSounds.StoneCrush);
      return true;
    }

    // Open, and holding a flame: reach it in and light the charge. Before the clay branch, because both
    // are held-stack gestures and a torch is not clay.
    if (CanIgnite(held))
      return TryBlowIn(world, byPlayer, blockSel.Position, tap);

    // Open: fire clay stops it again. Anything else falls through, so an open tap is not a wall the
    // player has to clear before doing something else at that cell.
    if (!IsFireClay(held))
      return false;

    if (held!.StackSize < IiexValues.TapPlugClayCost) {
      if (world.Side == EnumAppSide.Server)
        (byPlayer as IServerPlayer)?.SendIngameError("iiex-tapnotenoughclay");
      return false;
    }

    if (world.Side == EnumAppSide.Server) {
      tap.SetPlugged(true);
      if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) {
        activeSlot!.TakeOut(IiexValues.TapPlugClayCost);
        activeSlot.MarkDirty();
      }
    }
    PlayPlugSound(world, blockSel.Position, byPlayer, ExSounds.Build);
    return true;
  }

  private static void PlayPlugSound(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    AssetLocation sound
  ) => world.PlaySoundAt(sound, pos.X, pos.Y, pos.Z, byPlayer);

  /// <summary>
  /// Whether the held stack is a flame that can set something alight - vanilla's own marker, which every
  /// <c>*-lit-*</c> block carries. Deliberately not the firestarter: vanilla treats that as a separate
  /// class of igniter, and a bow drill does not reach two metres up a tap-hole.
  /// </summary>
  private static bool CanIgnite(ItemStack? stack) =>
    stack?.Block?.HasBehavior<BlockBehaviorCanIgnite>() == true;

  /// <summary>
  /// Routes the flame to the furnace this tap drains. The tap already resolves its own core for the pool
  /// readout, so nothing new is looked up. A tap on no furnace - or on a hearth, which has no tap-hole to
  /// reach through - falls through and the click does nothing.
  /// </summary>
  private static bool TryBlowIn(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos pos,
    BlockEntityFurnaceTap tap
  ) {
    if (tap.ResolveOwningAnchor() is not BlockEntityFurnaceCore core)
      return false;
    if (world.Side != EnumAppSide.Server)
      return true;
    if (!core.TryLightFromTap(pos))
      (byPlayer as IServerPlayer)?.SendIngameError("iiex-tapalreadylit");
    return true;
  }

  private static bool IsFireClay(ItemStack? stack) =>
    stack?.Collectible?.Code is { } code
    && code.Domain == FireClayCode.Domain
    && code.Path == FireClayCode.Path;

  private static ItemStack[]? _fireClayStacks;

  /// <summary>The fire-clay stacks the plugging hint draws in the player's hand, resolved once.</summary>
  private static ItemStack[] FireClayStacks(IWorldAccessor world) =>
    _fireClayStacks ??= world.GetItem(FireClayCode) is { } clay
      ? [new ItemStack(clay, IiexValues.TapPlugClayCost)]
      : [];

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) {
    var baseHelp =
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer) ?? [];

    // One line per verb, each gated on the state it belongs to, so a player sees the gesture that works
    // here rather than both. The state is read at draw time: a tap is plugged or open, never neither.
    var breakHelp = new WorldInteraction {
      ActionLangCode = "iiex:blockhelp-tap-unplug",
      MouseButton = EnumMouseButton.Right,
      // Breaking needs an empty hand (a held item is placed instead). Gated on that rather than
      // RequireFreeHand, which would draw an empty slot.
      ShouldApply = (wi, bs, es) =>
        forPlayer.Entity.RightHandItemSlot.Empty && IsPlugged(world, selection),
    };

    var plugHelp = new WorldInteraction {
      ActionLangCode = "iiex:blockhelp-tap-plug",
      MouseButton = EnumMouseButton.Right,
      Itemstacks = FireClayStacks(world),
      ShouldApply = (wi, bs, es) => !IsPlugged(world, selection),
    };

    var blowInHelp = new WorldInteraction {
      ActionLangCode = "iiex:blockhelp-tap-blowin",
      MouseButton = EnumMouseButton.Right,
      // Vanilla's own igniter list, minus the firestarter - the same set CanIgnite accepts, so the hint
      // draws exactly what works.
      Itemstacks = BlockBehaviorCanIgnite
        .CanIgniteStacks(world.Api, false)
        .ToArray(),
      ShouldApply = (wi, bs, es) => !IsPlugged(world, selection),
    };

    return baseHelp
      .Append(breakHelp)
      .Append(blowInHelp)
      .Append(plugHelp)
      .ToArray();
  }

  private static bool IsPlugged(
    IWorldAccessor world,
    BlockSelection selection
  ) =>
    world.BlockAccessor.GetBlockEntity(selection.Position)
      is not BlockEntityFurnaceTap tap
    || tap.IsPlugged;

  #endregion

  #region Drops

  /// <summary>
  /// The stack a picked or mined tap becomes: this tap's own type, normalised to the <c>s</c> facing so a
  /// mined tap stacks with a crafted one instead of splitting the inventory four ways. The type is
  /// carried through, so an iron tap never comes back as a slag tap. <c>s</c> rather than
  /// <see cref="ExpandedLib.Blocks.BlockBehaviorExOrientable"/>'s canonical <c>n</c>, to match the creative entry
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
