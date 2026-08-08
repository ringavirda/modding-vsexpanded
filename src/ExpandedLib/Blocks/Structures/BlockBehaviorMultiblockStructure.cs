using ExpandedLib.Registries.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// Generic behaviour that centralises the multiblock build-outline projection:
/// Ctrl+Shift+right-click toggles the hologram of missing/incorrect blocks (routed to
/// <see cref="BlockEntityMultiblockStructure.Interact"/>) and contributes the help line. Both act only
/// while the structure is incomplete; once complete the projection auto-hides and the gesture passes through.
/// <para>
/// Carried by both the anchor block (whose BE <b>is</b> a <see cref="BlockEntityMultiblockStructure"/> - the
/// furnace core) and every functional component of the same structure (a tap, a hopper, a tuyere - whose BE is
/// an <see cref="IMultiblockComponent"/> that scans up to the anchor whose layout owns its cell). So a player
/// standing at any real component - not only the core - can preview and complete the structure. A component
/// with no resolvable anchor (placed before its anchor, or a plain brick/filler) does nothing.
/// </para>
/// <para>
/// Add via <c>{ "name": "MultiblockStructure" }</c>, placed <b>before</b> any behaviour that also consumes
/// right-click so its <see cref="EnumHandling.PreventSubsequent"/> wins. A component whose own
/// <c>OnBlockInteractStart</c> overrides without calling base (the tap) or reroutes through a filler (the
/// hopper) instead calls <see cref="TryToggleProjection"/> directly - the one shared entry point.
/// </para>
/// </summary>
[BlockBehaviorRegister("MultiblockStructure", PrefixModId = false)]
public class BlockBehaviorMultiblockStructure : BlockBehavior
{
  public BlockBehaviorMultiblockStructure(Block block)
    : base(block) { }

  /// <summary>Whether the player is making the build-outline gesture (Ctrl+Shift held).</summary>
  public static bool IsProjectionGesture(IPlayer? byPlayer)
  {
    var controls = byPlayer?.Entity?.Controls;
    return controls != null && controls.CtrlKey && controls.ShiftKey;
  }

  /// <summary>
  /// Resolves the incomplete multiblock anchor the block at <paramref name="pos"/> should project, or null.
  /// The block is either the anchor itself (its BE is a <see cref="BlockEntityMultiblockStructure"/> - the
  /// core) or a functional component (its BE is an <see cref="IMultiblockComponent"/> - a tap, hopper, tuyere -
  /// that scans up to the anchor whose layout owns its cell). Either way the anchor is returned only while its
  /// structure is still incomplete, so the projection is offered exactly when there is something left to build;
  /// a complete structure, or none, yields null.
  /// </summary>
  public static BlockEntityMultiblockStructure? ResolveIncompleteAnchor(
    IWorldAccessor world,
    BlockPos pos
  )
  {
    BlockEntityMultiblockStructure? anchor = world.BlockAccessor.GetBlockEntity(
      pos
    ) switch
    {
      BlockEntityMultiblockStructure structure => structure,
      IMultiblockComponent component => component.ResolveOwningAnchor(),
      _ => null,
    };
    return anchor is { StructureComplete: false } ? anchor : null;
  }

  /// <summary>
  /// The shared build-outline entry point. When the player is making the projection gesture and the block at
  /// <paramref name="pos"/> resolves to an incomplete anchor (<see cref="ResolveIncompleteAnchor"/>), toggles
  /// that anchor's build outline / missing-parts report and returns true (the click is consumed); otherwise
  /// returns false and the caller handles its own click. Blocks whose <c>OnBlockInteractStart</c> runs
  /// behaviours reach this through this behaviour; a block that overrides <c>OnBlockInteractStart</c> (the tap)
  /// or reroutes it through a filler (the hopper) calls this directly - so the projection is reachable from
  /// every functional component through one implementation.
  /// </summary>
  public static bool TryToggleProjection(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos pos
  )
  {
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
  /// <paramref name="forBlock"/>'s own domain so each mod shows its own translation (both lpex and smex - and
  /// iwex - ship <c>blockhelp-mulblock-struc-show</c>). The caller shows it only while the structure is
  /// incomplete.
  /// </summary>
  public static WorldInteraction[] ProjectionHelp(Block forBlock) =>
    [
      new WorldInteraction
      {
        ActionLangCode = forBlock.Code.Domain + ":blockhelp-mulblock-struc-show",
        HotKeyCodes = ["ctrl", "shift"],
        MouseButton = EnumMouseButton.Right,
      },
    ];

  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref EnumHandling handling
  )
  {
    if (TryToggleProjection(world, byPlayer, blockSel.Position))
    {
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
