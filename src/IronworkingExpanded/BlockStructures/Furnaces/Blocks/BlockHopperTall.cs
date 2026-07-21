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
/// The tall hopper: a two-cell-high burden tank that drips its contents into the furnace shaft below it
/// (<see cref="BlockEntityHopperTall"/>). It occupies one grid cell - the base - and renders a
/// two-block-high model up through its <b>top filler cell</b>, which is where every player interaction
/// lands: the filler reroutes right-clicks to the base's block entity through
/// <see cref="IFillerInteractionTarget"/>, so burden is poured into (and drawn out of) the top of the
/// hopper rather than its base. Fills either burden family; the furnace it feeds gates acceptance.
/// </summary>
[BlockRegister]
public partial class BlockHopperTall
  : BlockFilledMegastructure,
    IFillerHost,
    IFillerInteractionTarget,
    IExBlockDefProvider
{
  #region Code-first definition

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "hopper-tall", "furnaces/hopper-tall")
        .Class<BlockHopperTall>()
        .EntityClass<BlockEntityHopperTall>()
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(1)
        .Shape("iwex:furnaces/hopper-tall")
        .CreativeCommon("*")
        // The footprint is a single filler one cell up (the hopper is 2 tall over its base): '#' over the
        // '0' principal with Origin(0, 1) resolves to the cell at (0, +1, 0) - the top the model fills and
        // every interaction routes through.
        .FillerOffsets(
          StructureFootprint.Layout(f =>
            f.Origin(0, 1)
              .Slice(
                0,
                """
                #
                0
                """
              )
          )
        )
        .SolidNonOpaque(),
    ];

  #endregion

  // A single, non-oriented tank: the drip and the filler are rotation-invariant (the filler sits
  // directly above the base, the drip searches its own column and the four neighbours), so the footprint
  // needs no rotation.
  public override int StructureAngle => 0;

  #region Drops

  // Placement, the top filler cell and its removal on break are handled by BlockFilledMegastructure. A
  // broken hopper returns itself plus whatever burden the tank still held, so the charge is never lost.
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer? byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    var drops = new List<ItemStack>(
      base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier)
    );
    if (
      world.BlockAccessor.GetBlockEntity(pos) is BlockEntityHopperTall be
      && be.TankContents is { } burden
    )
      drops.Add(burden.Clone());
    return [.. drops];
  }

  #endregion

  #region Interaction (routed from the top filler cell)

  // Deposit/withdraw is deliberately reachable ONLY through the top filler cell, not the base block: the
  // hopper is charged at its mouth (the top), and the filler reroutes those clicks here. A right-click
  // with burden fills the tank (ctrl = the whole held stack), an empty-handed right-click empties it.
  private bool HandleInteract(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel
  )
  {
    if (
      world.BlockAccessor.GetBlockEntity(principalSel.Position)
      is not BlockEntityHopperTall be
    )
      return false;

    if (world.Side == EnumAppSide.Server)
    {
      ItemSlot? active = byPlayer.InventoryManager?.ActiveHotbarSlot;
      if (active?.Empty == false)
      {
        // A mismatched grade/family is a real mistake (one stack can't hold two), so it is named; a full
        // tank is a state the block info already shows, not an error, so it is swallowed silently.
        if (
          !be.TryDeposit(active, byPlayer.Entity.Controls.CtrlKey) && !be.IsFull
        )
          (byPlayer as IServerPlayer)?.SendIngameError(
            "iwex-hoppertall-wronggrade"
          );
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
            principalSel.Position.ToVec3d().Add(0.5, 0.5, 0.5)
          );
      }
    }
    // Swallow the click on both sides so no block is placed against the filler/hopper face.
    return true;
  }

  bool IFillerInteractionTarget.OnFillerInteractStart(
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => HandleInteract(world, byPlayer, principalSel);

  bool IFillerInteractionTarget.OnFillerInteractStep(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) => false;

  void IFillerInteractionTarget.OnFillerInteractStop(
    float secondsUsed,
    IWorldAccessor world,
    IPlayer byPlayer,
    BlockSelection principalSel,
    BlockPos clickedCell
  ) { }

  WorldInteraction[] IFillerInteractionTarget.GetFillerInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel,
    IPlayer forPlayer,
    BlockPos clickedCell
  ) => HopperInteractionHelp(world, principalSel);

  #endregion

  #region Interaction help

  // Resolved once (the block is a singleton): a burden stack shown in the "add" hints.
  private ItemStack[]? _burdenStack;

  private WorldInteraction[] HopperInteractionHelp(
    IWorldAccessor world,
    BlockSelection principalSel
  )
  {
    ItemStack[] burden = _burdenStack ??= ResolveBurdenStack();
    var help = new List<WorldInteraction>
    {
      new()
      {
        ActionLangCode = "iwex:hoppertall-help-add",
        MouseButton = EnumMouseButton.Right,
        Itemstacks = burden,
      },
      new()
      {
        ActionLangCode = "iwex:hoppertall-help-addstack",
        MouseButton = EnumMouseButton.Right,
        HotKeyCode = "ctrl",
        Itemstacks = burden,
      },
    };
    if (
      world.BlockAccessor.GetBlockEntity(principalSel.Position)
        is BlockEntityHopperTall be
      && be.TankCount > 0
    )
      help.Add(
        new WorldInteraction
        {
          ActionLangCode = "iwex:hoppertall-help-take",
          MouseButton = EnumMouseButton.Right,
        }
      );
    return [.. help];
  }

  private ItemStack[] ResolveBurdenStack()
  {
    Item? burden = api.World.GetItem(new AssetLocation("iwex", "burden"));
    return burden == null ? [] : [new ItemStack(burden)];
  }

  #endregion
}
