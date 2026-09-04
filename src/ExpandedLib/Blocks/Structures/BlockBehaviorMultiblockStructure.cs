using ExpandedLib.Registries.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// Centralises the multiblock build-outline projection: Ctrl+Shift+right-click toggles the hologram of
/// missing or incorrect blocks (routed to <see cref="BlockEntityMultiblockStructure.Interact"/>) and
/// contributes the help line. Both act only while the structure is incomplete. Carried by the anchor
/// block and by every functional component of the same structure (tap, hopper, tuyere); a component with
/// no resolvable anchor does nothing. Add via <c>{ "name": "MultiblockStructure" }</c> before any
/// behaviour that also consumes right-click, so its <see cref="EnumHandling.PreventSubsequent"/> wins; a
/// block that overrides <c>OnBlockInteractStart</c> without calling base, or reroutes it through a
/// filler, calls <see cref="TryToggleProjection"/> directly instead.
/// </summary>
[BlockBehaviorRegister("MultiblockStructure", PrefixModId = false)]
public class BlockBehaviorMultiblockStructure : BlockBehavior {
  public BlockBehaviorMultiblockStructure(Block block)
    : base(block) { }

  /// <summary>Whether the player is making the build-outline gesture (Ctrl+Shift held).</summary>
  public static bool IsProjectionGesture(IPlayer? byPlayer) {
    var controls = byPlayer?.Entity?.Controls;
    return controls != null && controls.CtrlKey && controls.ShiftKey;
  }

  /// <summary>
  /// Resolves the incomplete multiblock anchor the block at <paramref name="pos"/> should project, or
  /// null. The block is either the anchor itself or an <see cref="IMultiblockComponent"/> that scans up
  /// to the anchor whose layout owns its cell. A complete structure, or none, yields null.
  /// </summary>
  public static BlockEntityMultiblockStructure? ResolveIncompleteAnchor(
    IWorldAccessor world,
    BlockPos pos
  ) {
    BlockEntityMultiblockStructure? anchor = world.BlockAccessor.GetBlockEntity(
      pos
    ) switch {
      BlockEntityMultiblockStructure structure => structure,
      IMultiblockComponent component => component.ResolveOwningAnchor(),
      _ => null,
    };
    return anchor is { StructureComplete: false } ? anchor : null;
  }

  /// <summary>
  /// Shared build-outline entry point. When the player makes the projection gesture and
  /// <paramref name="pos"/> resolves to an incomplete anchor (<see cref="ResolveIncompleteAnchor"/>),
  /// toggles that anchor's outline and missing-parts report and returns true (the click is consumed);
  /// otherwise returns false and the caller handles its own click. Called directly by a block that
  /// overrides <c>OnBlockInteractStart</c> or reroutes it through a filler.
  /// </summary>
  public static bool TryToggleProjection(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos pos
  ) {
    if (
      !IsProjectionGesture(byPlayer)
      || ResolveIncompleteAnchor(world, pos) is not { } anchor
    )
      return false;

    anchor.Interact(byPlayer);
    (byPlayer as IClientPlayer)?.TriggerFpAnimation(
      EnumHandInteract.HeldItemInteract
    );
    return true;
  }

  /// <summary>
  /// The Ctrl+Shift+right-click "show multiblock structure" help line, resolved against
  /// <paramref name="forBlock"/>'s own domain so each mod supplies its own translation of
  /// <c>blockhelp-mulblock-struc-show</c>. Shown only while the structure is incomplete.
  /// </summary>
  public static WorldInteraction[] ProjectionHelp(Block forBlock) =>
    [
      new WorldInteraction
      {
        ActionLangCode =
          forBlock.Code.Domain + ":blockhelp-mulblock-struc-show",
        HotKeyCodes = ["ctrl", "shift"],
        MouseButton = EnumMouseButton.Right,
      },
    ];

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref EnumHandling handling
  ) {
    if (TryToggleProjection(world, byPlayer, blockSel.Position)) {
      handling = EnumHandling.PreventSubsequent;
      return true;
    }

    handling = EnumHandling.PassThrough;
    return false;
  }

  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer,
    ref EnumHandling handling
  ) =>
    ResolveIncompleteAnchor(world, selection.Position) == null
      ? []
      : ProjectionHelp(block);
}
