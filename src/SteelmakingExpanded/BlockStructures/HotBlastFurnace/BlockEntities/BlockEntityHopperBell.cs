using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Materials;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;

/// <summary>
/// Block entity for the bell hopper beneath the reinforced hopper. It no longer mixes anything: it pulls
/// the ready-made burden from the reinforced tank above into an internal magazine, then drips that burden
/// down into the furnace shaft while dropping is enabled. The burden's grade (its blast-mix proportions)
/// rides along, so the furnace core reads the same charge the ore mixer stamped.
/// </summary>
[BlockEntityRegister]
public class BlockEntityHopperBell : BlockEntity
{
  private long _tickId;

  // The magazine is one burden stack (grade = its attributes), pulled from the tank above and dripped
  // below. Null when empty.
  private ItemStack? _magazine;

  // Dropping is on by default so a freshly built furnace feeds itself without the player having to
  // discover the Ctrl + right-click toggle first.
  private bool _isDropping = true;

  /// <summary>Burden units currently buffered in the magazine.</summary>
  public int BlastMixMagazine => _magazine?.StackSize ?? 0;

  /// <summary>Maximum burden the magazine can hold.</summary>
  public int MaxMagazineCapacity => SmexValues.HopperMaxMagazineCapacity;

  /// <summary>Whether the hopper is dripping burden into the furnace.</summary>
  public bool IsDropping
  {
    get => _isDropping;
    set
    {
      if (_isDropping == value)
        return;
      _isDropping = value;
      if (Api?.Side == EnumAppSide.Server)
      {
        if (_isDropping)
          StartTicking();
        else
          StopTicking();
      }
    }
  }

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);

    if (api.Side == EnumAppSide.Server && _isDropping)
      StartTicking();
  }

  private void StartTicking()
  {
    if (_tickId == 0 && Api != null)
      _tickId = RegisterGameTickListener(OnServerTick, 1000);
  }

  private void StopTicking()
  {
    if (_tickId != 0 && Api != null)
    {
      UnregisterGameTickListener(_tickId);
      _tickId = 0;
    }
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    _magazine = tree.GetItemstack("magazine");
    _magazine?.ResolveBlockOrItem(worldForResolving);
    if (_magazine?.Collectible == null || _magazine.StackSize <= 0)
      _magazine = null;
    IsDropping = tree.GetBool("isDropping", true);
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    if (_magazine != null)
      tree.SetItemstack("magazine", _magazine);
    tree.SetBool("isDropping", IsDropping);
  }

  private void OnServerTick(float dt)
  {
    PullFromTankAbove();
    DripIntoShaft();
  }

  // Draw ready-made burden from the reinforced tank above into the magazine, respecting the single-grade
  // rule (a different grade waits until the magazine drains).
  private void PullFromTankAbove()
  {
    if (
      Api.World.BlockAccessor.GetBlockEntity(Pos.UpCopy())
      is not BlockEntityHopperReinforced top
    )
      return;

    int space = MaxMagazineCapacity - BlastMixMagazine;
    if (space <= 0)
      return;

    ItemStack? peek = top.PeekTank();
    if (peek == null || (_magazine != null && !IsMergeable(peek)))
      return;

    ItemStack? drawn = top.DrawBurden(space);
    if (drawn == null)
      return;

    if (_magazine == null)
      _magazine = drawn;
    else
      _magazine.StackSize += drawn.StackSize;
    MarkDirty(true);
  }

  private void DripIntoShaft()
  {
    int dropAmount = SmexValues.HopperDropAmount;
    if (_magazine == null || BlastMixMagazine < dropAmount || IsFurnaceFull())
      return;

    BlockPos? targetPos = FindBestPileLocation(dropAmount);
    if (targetPos == null)
      return;

    DropBurden(targetPos, dropAmount);
    _magazine.StackSize -= dropAmount;
    if (_magazine.StackSize <= 0)
      _magazine = null;
    MarkDirty(true);
  }

  /// <summary>Returns <c>true</c> when the furnace shaft below has no room for more burden.</summary>
  public bool IsFurnaceFull()
  {
    if (Api == null)
      return false;

    Block b = Api.World.BlockAccessor.GetBlock(Pos.DownCopy(2));
    if (b.Code?.Path.StartsWith("coalpile") != true)
      return false;

    BlockPos planeCenter = Pos.DownCopy(3);
    for (int dx = -1; dx <= 1; dx++)
    {
      for (int dz = -1; dz <= 1; dz++)
      {
        Block planeBlock = Api.World.BlockAccessor.GetBlock(
          planeCenter.AddCopy(dx, 0, dz)
        );
        if (planeBlock.Code?.Path.StartsWith("coalpile") != true)
          return false;
      }
    }

    return true;
  }

  private BlockPos? FindBestPileLocation(int dropAmount)
  {
    int maxDepth = 15;
    int floorY = Pos.Y;

    for (int d = 2; d <= maxDepth; d++)
    {
      BlockPos checkPos = Pos.DownCopy(d);
      Block b = Api.World.BlockAccessor.GetBlock(checkPos);

      if (b.Replaceable < 6000 && b.Code?.Path.StartsWith("coalpile") != true)
      {
        floorY = checkPos.Y + 1;
        break;
      }
    }

    for (int y = floorY; y < Pos.Y; y++)
    {
      BlockPos centerPos = new BlockPos(Pos.X, y, Pos.Z);

      if (IsValidPileTarget(centerPos, dropAmount))
        return centerPos;

      BlockPos[] neighbors =
      [
        centerPos.AddCopy(1, 0, 0),
        centerPos.AddCopy(-1, 0, 0),
        centerPos.AddCopy(0, 0, 1),
        centerPos.AddCopy(0, 0, -1),
        centerPos.AddCopy(1, 0, 1),
        centerPos.AddCopy(-1, 0, -1),
        centerPos.AddCopy(1, 0, -1),
        centerPos.AddCopy(-1, 0, 1),
      ];

      foreach (var n in neighbors)
      {
        if (IsValidPileTarget(n, dropAmount))
          return n;
      }
    }

    return null;
  }

  private bool IsValidPileTarget(BlockPos pos, int dropAmount)
  {
    Block b = Api.World.BlockAccessor.GetBlock(pos);

    if (b.Replaceable >= 6000)
      return true;

    if (b.Code?.Path.StartsWith("coalpile") == true)
    {
      if (
        Api.World.BlockAccessor.GetBlockEntity(pos)
        is BlockEntityItemPile pileBe
      )
      {
        var slot = pileBe.inventory[0];
        if (slot.Empty)
          return true;

        // The pile below can take more only if it already holds charge (burden).
        if (IsCharge(slot.Itemstack) && slot.StackSize + dropAmount <= 16)
          return true;
      }
    }

    return false;
  }

  private void DropBurden(BlockPos targetPos, int amount)
  {
    if (_magazine == null)
      return;

    Block blockAtTarget = Api.World.BlockAccessor.GetBlock(targetPos);

    if (blockAtTarget.Replaceable >= 6000)
    {
      Block? coalPileBlock = Api.World.GetBlock(
        new AssetLocation("game", "coalpile")
      );
      if (coalPileBlock != null)
      {
        Api.World.BlockAccessor.SetBlock(coalPileBlock.BlockId, targetPos);
        blockAtTarget = coalPileBlock;
      }
    }

    if (
      blockAtTarget.Code?.Path.StartsWith("coalpile") == true
      && Api.World.BlockAccessor.GetBlockEntity(targetPos)
        is BlockEntityItemPile pileBe
    )
    {
      var slot = pileBe.inventory[0];

      if (slot.Empty)
      {
        ItemStack drop = _magazine.Clone();
        drop.StackSize = amount;
        slot.Itemstack = drop;
      }
      else
      {
        slot.Itemstack.StackSize += amount;
      }

      slot.MarkDirty();
      pileBe.MarkDirty(true);
      Api.World.BlockAccessor.MarkBlockDirty(targetPos);
    }

    ExParticles.FallingDust(Api.World, Pos);
    Api.World.PlaySoundAt(ExSounds.StoneCrush, Pos.X, Pos.Y, Pos.Z);
  }

  // Same item and same stamped grade - the magazine holds one grade at a time, mirroring the tank above.
  private bool IsMergeable(ItemStack stack) =>
    _magazine != null
    && stack.Collectible == _magazine.Collectible
    && Burden.Read(stack).Equals(Burden.Read(_magazine));

  // The shaft pile is charge if it holds burden (either family) or any legacy blast mix still tagged with
  // the charge role - so the bell tops up a pile it (or the tall hopper) already dripped into.
  private static bool IsCharge(ItemStack stack) =>
    Burden.IsAny(stack) || MaterialRoleRegistry.IsRole(Roles.Charge, stack);

  public override void OnBlockRemoved()
  {
    base.OnBlockRemoved();
    StopTicking();
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(
      Lang.Get(
        "smex:hopper-info-bell",
        IsDropping
          ? Lang.Get("smex:hopper-state-dropping")
          : Lang.Get("smex:hopper-state-stopped")
      )
    );
    dsc.AppendLine(
      Lang.Get(
        "smex:hopper-info-magazine",
        BlastMixMagazine,
        MaxMagazineCapacity
      )
    );
  }
}
