using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.OreMixer.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockStructures.OreMixer.Blocks;

/// <summary>
/// The ore mixer mega-block. Occupies one grid cell (the principal, bottom-centre) but renders across a
/// 3×2×1 footprint reserved with invisible structure fillers; the two upper-side cells host a
/// mechanical-power port each (declared in the JSON <c>fillerOffsets</c> <c>behaviors</c>), so an axle
/// on the west or east face drives the rotor. Construction is driven by the RightClickConstructable
/// behaviour; a finished mixer's principal toggles the dropping lids to drain into the container below.
/// </summary>
[BlockRegister]
public partial class BlockOreMixer
  : Block,
    IFillerHost,
    IFillerInteractionTarget
{
  /// <summary>Structure/filler rotation, paired with the JSON <c>rotateYByType</c> (north 0 … west 90).</summary>
  private int Angle => ExOrientation.AngleFromSide(Variant["side"]);

  /// <summary>The structure-filler rotation angle, for the BE's port-cell resolution.</summary>
  public int StructureAngle => Angle;

  #region Placement / filler footprint

  public override bool CanPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel,
    ref string failureCode
  )
  {
    if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
      return false;

    var cells = StructureFillers.FootprintCells(this, blockSel.Position, Angle);
    if (!StructureFillers.CanPlace(world, cells))
    {
      failureCode = "notenoughspace";
      return false;
    }
    return true;
  }

  public override void OnBlockPlaced(
    IWorldAccessor world,
    BlockPos blockPos,
    ItemStack? byItemStack = null
  )
  {
    base.OnBlockPlaced(world, blockPos, byItemStack);
    StructureFillers.PlaceFillers(
      world,
      blockPos,
      StructureFillers.FootprintCells(this, blockPos, Angle)
    );
  }

  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    StructureFillers.RemoveFillers(
      world,
      pos,
      StructureFillers.FootprintCells(this, pos, Angle)
    );
    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  // A broken mixer returns only its construction materials (scattered by the RightClickConstructable
  // behaviour), never the mixer block itself.
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [];

  #endregion

  #region Interaction

  // Interaction depends on WHICH cell was clicked: the upper row (the three top cells) takes material
  // input, while the principal/lower row toggles the dropping lids. The principal's own click is the
  // bottom-centre cell, so it falls in the lid-toggle branch. Before construction completes the click
  // falls through to the RightClickConstructable behaviour (HandleInteract returns null).
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    HandleInteract(world, byPlayer, blockSel, blockSel.Position)
    ?? base.OnBlockInteractStart(world, byPlayer, blockSel);

  // Which part of the mega-block was clicked: the upper hopper row, the principal lid control, or an
  // inert lower-corner filler.
  private enum MixerCell
  {
    Upper,
    Principal,
    Lower,
  }

  private static MixerCell ClassifyCell(BlockPos principal, BlockPos clicked)
  {
    if (clicked.Y > principal.Y)
      return MixerCell.Upper;
    return clicked.X == principal.X
      && clicked.Y == principal.Y
      && clicked.Z == principal.Z
      ? MixerCell.Principal
      : MixerCell.Lower;
  }

  private bool? HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection sel,
    BlockPos clickedCell
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(sel.Position)
        is not BlockEntityOreMixer be
      || !be.IsConstructed
    )
      return null; // pre-construction clicks drive the RCC behaviour

    MixerCell cell = ClassifyCell(be.Pos, clickedCell);

    // The lower-corner fillers are inert: swallow the click so nothing is placed against them, but do
    // nothing else (no charging, no lid).
    if (cell == MixerCell.Lower)
      return true;

    // The server owns the charge/mix/lid state; the client just swallows the click so no block is
    // placed against the mixer and the interaction predicts.
    if (world.Side != EnumAppSide.Server)
      return true;

    if (cell == MixerCell.Upper)
      ChargeFromHand(be, byPlayer);
    else
      ToggleLids(be, byPlayer);

    return true;
  }

  /// <summary>Upper hopper cells: charge from the held material (or reload burden), telling the player
  /// why nothing went in when it's rejected.</summary>
  private static void ChargeFromHand(BlockEntityOreMixer be, IPlayer byPlayer)
  {
    ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
    if (active?.Empty != false)
      return; // empty-handed on the hopper: nothing to add, no error

    var notify = byPlayer as IServerPlayer;
    if (!BlockEntityOreMixer.AcceptsAsInput(active.Itemstack))
    {
      notify?.SendIngameError("iwex-mixer-wronginput");
      return;
    }
    if (be.HasReadyBurden || be.IsDraining)
    {
      notify?.SendIngameError("iwex-mixer-drainfirst");
      return;
    }
    if (be.IsFull)
    {
      notify?.SendIngameError("iwex-mixer-full");
      return;
    }

    // Plain right-click adds one; ctrl+right-click adds the whole held stack. (Ctrl, not sneak: a
    // sneak+right-click with a held material is grabbed by vanilla ground-storage placement before the
    // interaction reaches us.) A held crushed-iron/lime/coke charges the mixer; a held burden reloads it.
    bool whole = byPlayer.Entity.Controls.CtrlKey;
    _ = be.TryAddInput(active, whole) || be.TryReloadBurden(active, whole);
  }

  /// <summary>Principal cell: open or close the dropping lids, telling the player when there's nothing
  /// mixed yet to drain.</summary>
  private static void ToggleLids(BlockEntityOreMixer be, IPlayer byPlayer)
  {
    if (!be.ToggleDrain())
      (byPlayer as IServerPlayer)?.SendIngameError("iwex-mixer-nothingmixed");
  }

  #endregion

  #region Interaction help

  // Cached input/reload stacks for the interaction help (resolved once; the block is a singleton).
  private ItemStack[]? _inputStacks;

  // The principal cell shows the lid control (it sits in the lid branch of HandleInteract).
  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) => BuildInteractionHelp(world, selection, forPlayer, MixerCell.Principal);

  /// <summary>
  /// Help for whichever cell is looked at: the upper hopper cells show the add-one / add-stack input
  /// hints, the principal shows the lid toggle, the inert lower corners show nothing. Pre-construction
  /// cells defer to the RCC help.
  /// </summary>
  private WorldInteraction[] BuildInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer,
    MixerCell cell
  )
  {
    WorldInteraction[] baseHelp = base.GetPlacedBlockInteractionHelp(
      world,
      selection,
      forPlayer
    );

    if (
      world.BlockAccessor.GetBlockEntity(selection.Position)
        is not BlockEntityOreMixer be
      || !be.IsConstructed
    )
      return baseHelp; // RCC behaviour supplies the construction help

    var help = new List<WorldInteraction>();
    if (cell == MixerCell.Upper)
    {
      // Resolved once (block is a singleton): the crushed iron / lime / coke / burden the mixer accepts.
      ItemStack[] inputs = _inputStacks ??= ResolveInputStacks();
      help.Add(
        new WorldInteraction
        {
          ActionLangCode = "iwex:mixer-help-add",
          MouseButton = EnumMouseButton.Right,
          Itemstacks = inputs,
        }
      );
      help.Add(
        new WorldInteraction
        {
          ActionLangCode = "iwex:mixer-help-add-stack",
          MouseButton = EnumMouseButton.Right,
          HotKeyCode = "ctrl",
          Itemstacks = inputs,
        }
      );
    }
    else if (cell == MixerCell.Principal)
    {
      help.Add(
        new WorldInteraction
        {
          ActionLangCode = "iwex:mixer-help-lid",
          MouseButton = EnumMouseButton.Right,
        }
      );
    }
    // MixerCell.Lower: inert, no mixer help.

    return [.. help, .. baseHelp];
  }

  /// <summary>Gathers a representative stack of every collectible the mixer accepts as input or reload
  /// (crushed iron / lime / coke / charcoal / burden), so the interaction help cycles through them.</summary>
  private ItemStack[] ResolveInputStacks()
  {
    var stacks = new List<ItemStack>();
    foreach (CollectibleObject coll in api.World.Collectibles)
    {
      if (coll?.Code == null)
        continue;
      var stack = new ItemStack(coll);
      if (BlockEntityOreMixer.AcceptsAsInput(stack))
        stacks.Add(stack);
    }
    return [.. stacks];
  }

  #endregion

  #region Filler interaction forwarding

  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) =>
    HandleInteract(world, byPlayer, principalSel, clickedCell)
    ?? base.OnBlockInteractStart(world, byPlayer, principalSel);

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
  ) =>
    BuildInteractionHelp(
      world,
      principalSel,
      forPlayer,
      ClassifyCell(principalSel.Position, clickedCell)
    );

  #endregion
}
