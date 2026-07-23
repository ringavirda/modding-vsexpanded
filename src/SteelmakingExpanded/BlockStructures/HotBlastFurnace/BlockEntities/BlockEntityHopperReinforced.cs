using System;
using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.Items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;

/// <summary>
/// The reinforced hopper: a small burden tank that sits above the bell hopper and feeds it. It no longer
/// mixes anything - the ore mixer now stamps the blast-furnace burden, and this is just the loading bunker
/// the player (or, later, the skip hoist) tops up. A right-click with burden fills the tank, an empty-handed
/// right-click empties it, and Ctrl + right-click toggles the bell hopper's dropping below.
/// <para>
/// A single burden <see cref="ItemStack"/>, so it holds one grade at a time (a mismatched deposit is
/// refused). Deliberately a much smaller buffer than the tall hopper's tank: it is meant to be skip-hoist
/// fed rather than hand-loaded to the brim. The bell hopper below pulls from it (<see cref="DrawBurden"/>)
/// into its own magazine and drips that down the shaft.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityHopperReinforced : BlockEntity
{
  // The whole tank is one burden stack (item identity = family, attributes = grade). Null when empty.
  private ItemStack? _tank;

  // Cached, untranslated mesh of the burden contents pile (built lazily client-side).
  private MeshData? _contentsBaseMesh;

  // The contents pile is drawn between these heights (in 1/16 block units) inside the hopper, scaling
  // with how full the tank is.
  private const float ContentsMinY = 9f;
  private const float ContentsMaxY = 14f;

  /// <summary>Burden units currently held (0 when empty). Serialized, so the client HUD/mesh read it.</summary>
  public int TankCount => _tank?.StackSize ?? 0;

  /// <summary>The burden stack the tank holds (or null when empty), for the block's break drops. The
  /// caller must not mutate it - clone first.</summary>
  public ItemStack? TankContents => _tank;

  /// <summary>Maximum units the tank holds. Live config; small by design (skip-hoist fed).</summary>
  public int Capacity => SmexValues.HopperReinforcedCapacity;

  /// <summary>Whether the tank is at capacity.</summary>
  public bool IsFull => TankCount >= Capacity;

  #region Deposit / withdraw (driven from the block)

  /// <summary>
  /// Whether <paramref name="stack"/> can enter the tank right now: it must be prepared burden of either
  /// family, and - once the tank holds something - must match what is already in it (same item and grade),
  /// because one stack cannot carry two grades. An empty tank accepts any single burden.
  /// </summary>
  public bool Accepts(ItemStack? stack) =>
    Burden.IsAny(stack) && (_tank == null || IsMergeable(stack));

  /// <summary>
  /// Moves burden from <paramref name="fromSlot"/> into the tank. With <paramref name="wholeStack"/> it
  /// takes the whole held stack (ctrl+right-click), otherwise a single unit (plain right-click), each
  /// capped by the remaining room. Server-side. Returns true when anything moved; false for a foreign or
  /// mismatched stack, or a full tank - the block turns the mismatch into an in-game error.
  /// </summary>
  public bool TryDeposit(ItemSlot fromSlot, bool wholeStack)
  {
    ItemStack? incoming = fromSlot.Itemstack;
    if (!Accepts(incoming))
      return false;

    int room = Capacity - TankCount;
    int take = Math.Min(wholeStack ? fromSlot.StackSize : 1, room);
    if (take <= 0)
      return false;

    if (_tank == null)
    {
      _tank = incoming!.Clone();
      _tank.StackSize = take;
    }
    else
    {
      _tank.StackSize += take;
    }

    fromSlot.TakeOut(take);
    fromSlot.MarkDirty();
    MarkDirty(true);
    return true;
  }

  /// <summary>Empties the tank, handing back the whole burden stack (or null when already empty).</summary>
  public ItemStack? TryWithdraw()
  {
    if (_tank == null || _tank.StackSize <= 0)
      return null;
    ItemStack taken = _tank;
    _tank = null;
    MarkDirty(true);
    return taken;
  }

  /// <summary>The tank's burden stack for a read-only peek (the bell reads its grade before pulling). Do
  /// not mutate - clone first.</summary>
  public ItemStack? PeekTank() => _tank;

  /// <summary>Removes up to <paramref name="max"/> burden units from the tank and returns them as a stack
  /// (its grade preserved), or null when empty. The bell hopper below draws its magazine this way.</summary>
  public ItemStack? DrawBurden(int max)
  {
    if (_tank == null || _tank.StackSize <= 0 || max <= 0)
      return null;

    int take = Math.Min(max, _tank.StackSize);
    ItemStack drawn = _tank.Clone();
    drawn.StackSize = take;

    _tank.StackSize -= take;
    if (_tank.StackSize <= 0)
      _tank = null;

    MarkDirty(true);
    return drawn;
  }

  /// <summary>Flips the bell hopper below between dropping and stopped (the Ctrl + right-click gesture).
  /// Server-side.</summary>
  public void ToggleBellDropping()
  {
    if (
      Api.Side == EnumAppSide.Server
      && Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy())
        is BlockEntityHopperBell bell
    )
    {
      bell.IsDropping = !bell.IsDropping;
      bell.MarkDirty(true);
    }
  }

  // Same item and same stamped grade - one stack cannot hold two grades, so a different mix (or the other
  // family) is refused rather than silently pooling into one stack.
  private bool IsMergeable(ItemStack? stack) =>
    _tank != null
    && stack?.Collectible == _tank.Collectible
    && Burden.Read(stack).Equals(Burden.Read(_tank));

  #endregion

  #region Rendering

  /// <summary>
  /// Draws the burden contents pile on top of the normal hopper mesh, raised between
  /// <see cref="ContentsMinY"/> and <see cref="ContentsMaxY"/> in proportion to how full the tank is.
  /// </summary>
  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  )
  {
    if (TankCount > 0)
    {
      _contentsBaseMesh ??= BuildContentsMesh(tesselator);
      if (_contentsBaseMesh != null)
      {
        float fill = GameMath.Clamp((float)TankCount / Capacity, 0f, 1f);
        float yOffset = (ContentsMinY + fill * (ContentsMaxY - ContentsMinY)) / 16f;

        MeshData mesh = _contentsBaseMesh.Clone();
        mesh.Translate(0f, yOffset, 0f);
        mesher.AddMeshData(mesh);
      }
    }

    // Keep the default hopper block mesh as well.
    return base.OnTesselation(mesher, tesselator);
  }

  private MeshData? BuildContentsMesh(ITesselatorAPI tesselator)
  {
    Shape? shape = Api
      .Assets.TryGet(new AssetLocation("iwex:shapes/ore/burden.json"))
      ?.ToObject<Shape>();
    if (shape == null)
      return null;

    tesselator.TesselateShape(Block, shape, out MeshData mesh);
    return mesh;
  }

  #endregion

  #region Serialization

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    _tank = tree.GetItemstack("tank");
    _tank?.ResolveBlockOrItem(worldForResolving);
    // A resolved-away stack (the item no longer exists) or a zero stack reads as empty.
    if (_tank?.Collectible == null || _tank.StackSize <= 0)
      _tank = null;
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    if (_tank != null)
      tree.SetItemstack("tank", _tank);
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);

    if (_tank == null || _tank.StackSize <= 0)
      dsc.AppendLine(Lang.Get("smex:hopper-empty"));
    else
      dsc.AppendLine(
        Lang.Get("smex:hopper-holds", _tank.StackSize, Capacity, _tank.GetName())
      );

    if (
      Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy())
      is BlockEntityHopperBell bell
    )
    {
      dsc.AppendLine(
        Lang.Get(
          "smex:hopper-info-bell",
          bell.IsDropping
            ? Lang.Get("smex:hopper-state-dropping")
            : Lang.Get("smex:hopper-state-stopped")
        )
      );
      dsc.AppendLine(
        Lang.Get(
          "smex:hopper-info-magazine",
          bell.BlastMixMagazine,
          bell.MaxMagazineCapacity
        )
      );
      if (bell.IsFurnaceFull())
        dsc.AppendLine(Lang.Get("smex:hopper-info-furnacefull"));
    }
    else
    {
      dsc.AppendLine(Lang.Get("smex:hopper-info-nobell"));
    }
  }

  #endregion
}
