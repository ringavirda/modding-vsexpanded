using System.Collections.Generic;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.OreBunker.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.OreBunker.Blocks;

/// <summary>
/// The ore/blast-mix storage bunker mega-block. Occupies one grid cell (the principal) but renders
/// across a 3×1×6 footprint reserved with invisible structure fillers (real per-cell collision);
/// construction is driven by the RightClickConstructable behavior in the block JSON. The brick
/// <c>brick</c> variant only swaps the wall texture, so all colours share this one class.
/// </summary>
[BlockRegister]
public partial class BlockOreBunker
  : Block,
    IFillerHost,
    IFillerInteractionTarget
{
  /// <summary>
  /// Structure/filler rotation, paired with the JSON <c>rotateYByType</c>. The +180 keeps the footprint
  /// flush with the model, whose shape is authored facing the opposite way from the orientation
  /// convention (so a placed bunker extends away from the player, not into them).
  /// </summary>
  private int Angle => ExOrientation.AngleFromSide(Variant["side"]) + 180;

  /// <summary>The structure-filler rotation angle, for multiblock-structure verification.</summary>
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

    // Refuse placement unless the whole volume is clear, else the fillers fail to spawn.
    var cells = StructureFillers.FootprintCells(
      this,
      blockSel.Position,
      Angle
    );
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
    // Clear the reserved volume first so no invisible solid cells are left behind. The stored
    // contents are spilled by the BlockEntityContainer base in the following base call.
    StructureFillers.RemoveFillers(
      world,
      pos,
      StructureFillers.FootprintCells(this, pos, Angle)
    );
    base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
  }

  // A broken bunker returns only its construction materials (scattered by the RightClickConstructable
  // behaviour) plus its stored contents (spilled by the container BE), never the bunker block itself.
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  ) => [];

  #endregion

  #region Crate interaction

  // A finished bunker behaves like a large, GUI-less crate: right-click with burden deposits the
  // held stack, an empty-handed right-click withdraws a stack. Before construction completes the
  // click falls through to the RightClickConstructable behavior instead (HandleInteract returns null).
  public override bool OnBlockInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection blockSel
  ) =>
    HandleInteract(world, byPlayer, blockSel)
    ?? base.OnBlockInteractStart(world, byPlayer, blockSel);

  private bool? HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection sel
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(sel.Position)
        is not BlockEntityOreBunker be
      || !be.IsConstructed
    )
      return null; // pre-construction clicks drive the RCC behavior

    if (world.Side == EnumAppSide.Server)
    {
      ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
      if (active?.Empty == false)
      {
        // Plain right-click deposits one burden; ctrl+right-click deposits the whole held stack. (Ctrl,
        // not sneak: sneak+right-click with a held item is taken by vanilla ground-storage placement.)
        be.TryDeposit(active, byPlayer.Entity.Controls.CtrlKey);
      }
      else
      {
        ItemStack? taken = be.TryWithdraw();
        if (
          taken != null
          && byPlayer.InventoryManager?.TryGiveItemstack(taken) != true
        )
          world.SpawnItemEntity(
            taken,
            sel.Position.ToVec3d().Add(0.5, 0.5, 0.5)
          );
      }
    }
    // A finished bunker swallows the click on both sides so no block is placed against its face.
    return true;
  }

  #endregion

  #region Filler interaction forwarding

  // A click on any reserved footprint cell drives the principal's behaviours (the crate add/take
  // when finished, the RCC construction before that).
  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) =>
    HandleInteract(world, byPlayer, principalSel)
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
  ) => GetPlacedBlockInteractionHelp(world, principalSel, forPlayer);

  #endregion

  #region Interaction help

  // Resolved once (block is a singleton): a burden stack shown in the "add" hint.
  private ItemStack[]? _burdenStack;

  // A finished bunker reads like a vanilla crate: right-click with burden to add, empty-handed to take.
  public override WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  )
  {
    WorldInteraction[] baseHelp = base.GetPlacedBlockInteractionHelp(
      world,
      selection,
      forPlayer
    );

    if (
      world.BlockAccessor.GetBlockEntity(selection.Position)
        is not BlockEntityOreBunker be
      || !be.IsConstructed
    )
      return baseHelp; // RCC behaviour supplies the construction help

    ItemStack[] burden = _burdenStack ??= ResolveBurdenStack();
    var help = new List<WorldInteraction>
    {
      new()
      {
        ActionLangCode = "iwex:bunker-help-add",
        MouseButton = EnumMouseButton.Right,
        Itemstacks = burden,
      },
      new()
      {
        ActionLangCode = "iwex:bunker-help-add-stack",
        MouseButton = EnumMouseButton.Right,
        HotKeyCode = "ctrl",
        Itemstacks = burden,
      },
    };
    if (be.TotalContents > 0)
      help.Add(
        new WorldInteraction
        {
          ActionLangCode = "iwex:bunker-help-take",
          MouseButton = EnumMouseButton.Right,
        }
      );

    return [.. help, .. baseHelp];
  }

  private ItemStack[] ResolveBurdenStack()
  {
    Item? burden = api.World.GetItem(new AssetLocation("iwex", "burden"));
    return burden == null ? [] : [new ItemStack(burden)];
  }

  #endregion
}
