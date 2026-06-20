using System;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using SteelmakingExpanded.BlockStructures.Converter.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SteelmakingExpanded.BlockStructures.Converter.Blocks;

/// <summary>
/// The 3×3×3 converter vessel block. Construction is driven by the
/// RightClickConstructable block-entity behavior; this block scatters any
/// solidified charge when broken with an iron-tier pickaxe.
/// <para>
/// Implements <see cref="IFillerInteractionTarget"/> so a small hardened residue (one that
/// solidified mid-pour) can be chiselled out of the upper-rear <c>(0,1,1)</c> footprint cell with a
/// chisel + hammer, rather than forcing the player to break the whole vessel.
/// </para>
/// </summary>
[BlockRegister]
public partial class BlockConverterBessemer
  : Block,
    IFillerHost,
    IFillerInteractionTarget
{
  // The footprint cell (north-orientation offset from the vessel principal) that exposes the
  // chisel-out interaction - the upper-rear hatch, matching the player-facing hint.
  private static readonly Vec3i ChiselFillerOffset = new(0, 1, 1);

  // RMB construction and its build prompts are routed to the
  // RightClickConstructable block-entity behaviour by the "BlockEntityInteract"
  // block behaviour declared in the block JSON.

  // The converter is welded plate over a refractory lining - breaking it needs an iron-tier pickaxe.
  // The actual mining requirement is enforced via "requiredMiningTier" in the
  // block JSON; here we just scatter whatever solidified charge it held.
  public override void OnBlockBroken(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    ItemStack? solidifiedDrops = null;
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityConverterBessemer be
    )
      solidifiedDrops = be.CollectBreakDrops();

    // Clear the reserved 3x3x3 filler volume. Done before base.OnBlockBroken so
    // that even if the construction-drop path below throws, the fillers are gone
    // and we don't leave invisible solid cells behind.
    int fillerAngle = ExOrientation.AngleFromSide(Variant["side"]);
    var fillerCells = StructureFillers.FootprintCells(this, pos, fillerAngle);
    StructureFillers.RemoveFillers(world, pos, fillerCells);

    // base.OnBlockBroken drives the RightClickConstructable behaviour, which
    // resolves each completed stage's ingredients back into drops. That path
    // expands wildcard codes (e.g. metalplate-*) using the wildcard values
    // captured at build time; a converter raised before "storeWildCard" was
    // added to the recipe has none stored, so vanilla GetDrops throws while
    // expanding the "*" - and because it runs before the block is cleared, the
    // exception escapes to the client and crashes the game. Guard it so a
    // legacy/corrupt construction state degrades to "no construction drops"
    // and the block is still removed.
    try
    {
      base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }
    catch (Exception e)
    {
      world.Logger.Warning(
        "[smex] Bessemer converter at {0} could not drop its construction "
          + "materials (likely built before the recipe fix); removing it "
          + "anyway. {1}",
        pos,
        e
      );
      if (world.BlockAccessor.GetBlock(pos) == this)
        world.BlockAccessor.SetBlock(0, pos);
    }

    if (solidifiedDrops != null && world.Side == EnumAppSide.Server)
      world.SpawnItemEntity(solidifiedDrops, pos.ToVec3d().Add(0.5, 0.5, 0.5));
  }

  #region Chisel-out interaction (IFillerInteractionTarget)

  // Every footprint cell forwards interaction to this principal block. We single out the (0,1,1)
  // hatch cell for the chisel-out and forward everything else to the normal construction handling.
  public bool OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  )
  {
    if (
      IsChiselCell(principalSel.Position, clickedCell)
      && TryChiselOut(world, byPlayer, principalSel.Position)
    )
      return true;
    return OnBlockInteractStart(world, byPlayer, principalSel);
  }

  public bool OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => OnBlockInteractStep(secondsUsed, world, byPlayer, principalSel);

  public void OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => OnBlockInteractStop(secondsUsed, world, byPlayer, principalSel);

  public WorldInteraction[] GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  )
  {
    WorldInteraction[] baseHelp =
      GetPlacedBlockInteractionHelp(world, principalSel, forPlayer) ?? [];

    // The chisel hint shows only on the hatch cell, and only once the residue is small + hardened.
    if (
      IsChiselCell(principalSel.Position, clickedCell)
      && world.BlockAccessor.GetBlockEntity(principalSel.Position)
        is BlockEntityConverterBessemer be
      && be.CanChiselOut()
    )
      return
      [
        .. baseHelp,
        new WorldInteraction
        {
          ActionLangCode = "smex:blockhelp-bessemer-chiselresidue",
          MouseButton = EnumMouseButton.Right,
          Itemstacks = _chiselStacks ??=
            [
              .. world
                .SearchItems(new AssetLocation("chisel-*"))
                .Select(i => new ItemStack(i)),
            ],
        },
      ];

    return baseHelp;
  }

  private bool IsChiselCell(BlockPos principalPos, BlockPos clickedCell)
  {
    int angle = ExOrientation.AngleFromSide(Variant["side"]);
    Vec3i r = ExOrientation.RotateOffset(ChiselFillerOffset, angle);
    return clickedCell.Equals(principalPos.AddCopy(r.X, r.Y, r.Z));
  }

  // Chisel in hand + hammer in the off-hand chips a hardened residue out of the vessel - mirrors the
  // molten-canal clear. Returns true when this owns the click (so it isn't forwarded to construction).
  private bool TryChiselOut(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockPos principalPos
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(principalPos)
        is not BlockEntityConverterBessemer be
      || !be.HasSolidifiedCharge
    )
      return false; // nothing solidified here - let the click fall through to construction handling

    ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
    ItemStack? held = activeSlot?.Itemstack;
    ItemStack? offhand = byPlayer.Entity?.LeftHandItemSlot?.Itemstack;
    if (!IsTool(held, EnumTool.Chisel) || !IsTool(offhand, EnumTool.Hammer))
      return false; // not chiselling - fall through

    // The player is actively trying to chisel a solidified charge: own the click and explain why if
    // it can't be chipped out yet.
    if (!be.CanChiselOut())
    {
      if (world.Side == EnumAppSide.Server)
        (byPlayer as IServerPlayer)?.SendIngameError(
          be.ChargeIsHardened ? "smex-bessemertoofull" : "smex-bessemertoohot"
        );
      return true;
    }

    if (world.Side == EnumAppSide.Server)
    {
      ItemStack? recovered = be.ChiselOutContent();
      if (
        recovered != null
        && !byPlayer.InventoryManager.TryGiveItemstack(recovered)
      )
        world.SpawnItemEntity(
          recovered,
          principalPos.ToVec3d().Add(0.5, 0.6, 0.5)
        );

      if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
        held!.Collectible.DamageItem(world, byPlayer.Entity, activeSlot, 2);

      ExSounds.Play(world.Api, principalPos, ExSounds.StoneCrush, 0.8f);
    }
    return true;
  }

  private static bool IsTool(ItemStack? stack, EnumTool tool) =>
    stack?.Collectible?.Tool == tool;

  private static ItemStack[]? _chiselStacks;

  #endregion

#if !GAME_GE_1_22
  // Legacy lacks the vanilla IInteractableWithHelp path, so surface the construction help here.
  public override Vintagestory.API.Client.WorldInteraction[] GetPlacedBlockInteractionHelp(
    IWorldAccessor world,
    BlockSelection selection,
    IPlayer forPlayer
  ) =>
    ExpandedLib.Blocks.Construction.ExRightClickConstructable.AppendConstructionHelp(
      world,
      selection,
      base.GetPlacedBlockInteractionHelp(world, selection, forPlayer)
    );
#endif
}
