using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.Crafting.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.Crafting.BlockEntities;

/// <summary>
/// The design table's block entity. Holds the drafting inventory (drawing medium, parchment, output) and the
/// draft logic: draw a chosen diagram onto parchment, consuming one medium and one parchment. Right-click
/// opens <see cref="GuiDialogDesignTable"/>, where the player picks a diagram and presses Draw, which sends
/// the draft packet handled here. See <c>docs/design/diagram-crafting.md</c>.
/// <para>
/// The window is client-side, so slot moves and drafts reach the server only through the open/close/draft
/// handshake in <see cref="OnReceivedClientPacket"/>; without it the two inventories diverge.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityDesignTable : BlockEntityContainer {
  /// <summary>Inventory slots: the drawing medium (charcoal/coal), the parchment, and the drafted output.</summary>
  public const int MediumSlot = 0;
  public const int ParchmentSlot = 1;
  public const int OutputSlot = 2;

  /// <summary>Materials one draft consumes.</summary>
  public const int MediumCost = 1;
  public const int ParchmentCost = 1;

  // Packet ids: 1000/1001 are the vanilla openable-container open/close literals; 1002 is the Draw request
  // carrying the selected diagram's code.
  private const int PacketIdOpen = 1000;
  private const int PacketIdClose = 1001;

  /// <summary>The Draw button's packet: the chosen diagram's code, drafted server-side.</summary>
  public const int PacketIdDraft = 1002;

  private readonly InventoryDesignTable _inventory = new(null, null);
  public override InventoryBase Inventory => _inventory;
  public override string InventoryClassName => "designtable";

  // The open drafting window. Client-side only; null when closed.
  private GuiDialogDesignTable? _dialog;

  /// <summary>Code of the diagram last drafted, persisted so the window re-opens on it. Null until the first
  /// draft.</summary>
  public string? SelectedType { get; set; }

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    _inventory.LateInitialize("designtable-" + Pos, api);
  }

  #region Interaction + packet handshake

  /// <summary>Right-click: toggles the drafting window. Client-side only.</summary>
  public void OnInteract(IPlayer byPlayer) {
    if (Api.Side == EnumAppSide.Client)
      ToggleDialog((ICoreClientAPI)Api, byPlayer);
  }

  private void ToggleDialog(ICoreClientAPI capi, IPlayer byPlayer) {
    if (_dialog != null) {
      _dialog.TryClose();
      return;
    }

    _dialog = new GuiDialogDesignTable(
      Lang.Get("iwex:designtable-title"),
      Inventory,
      Pos,
      capi,
      SelectedType
    );
    _dialog.OnClosed += () => {
      _dialog = null;
      capi.Network.SendBlockEntityPacket(Pos, PacketIdClose);
    };
    _dialog.TryOpen();

    capi.Network.SendPacketClient(Inventory.Open(byPlayer));
    capi.Network.SendBlockEntityPacket(Pos, PacketIdOpen);
  }

  /// <summary>Closes the drafting window if open and drops the reference, so a broken or unloaded table
  /// cannot leave its GUI bound to a dead block entity (see <see cref="OnBlockRemoved"/> and
  /// <see cref="OnBlockUnloaded"/>). Mirrors vanilla's <c>BEOpenableContainer.Dispose</c>
  /// (<c>BEOpenableContainer.cs:264-269</c>).</summary>
  internal virtual void CloseDialog() {
    if (_dialog?.IsOpened() == true)
      _dialog?.TryClose();
    _dialog?.Dispose();
    _dialog = null;
  }

  public override void OnBlockRemoved() {
    base.OnBlockRemoved();
    CloseDialog();
  }

  public override void OnBlockUnloaded() {
    base.OnBlockUnloaded();
    CloseDialog();
  }

  /// <summary>
  /// Server-side handling of the window packets: the vanilla openable-container protocol plus the draft
  /// packet. <see cref="BlockEntityContainer"/> does not route these, so without it slot moves and drafts are
  /// dropped and the server inventory drifts from what the player sees.
  /// </summary>
  public override void OnReceivedClientPacket(
    IPlayer player,
    int packetid,
    byte[] data
  ) {
    if (packetid == PacketIdClose) {
      player.InventoryManager?.CloseInventory(Inventory);
      return;
    }

    if (!Api.World.Claims.TryAccess(player, Pos, EnumBlockAccessFlags.Use)) {
      Api.World.Logger.Audit(
        "Player {0} sent a design-table packet to {1} without claim access. Rejected.",
        player.PlayerName,
        Pos
      );
      return;
    }

    if (packetid < 1000) {
      Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);
      return;
    }

    if (packetid == PacketIdOpen) {
      player.InventoryManager?.OpenInventory(Inventory);
      return;
    }

    if (packetid == PacketIdDraft) {
      OnDraftRequested(SerializerUtil.Deserialize<string>(data));
      return;
    }

    base.OnReceivedClientPacket(player, packetid, data);
  }

  private void OnDraftRequested(string diagramCode) {
    SelectedType = diagramCode;
    TryDraft(diagramCode);
  }

  #endregion

  #region Draft logic (pure where it can be; unit-tested)

  /// <summary>Whether the stack is a drawing medium: charcoal or coal.</summary>
  public static bool IsDrawingMedium(ItemStack? stack) =>
    stack?.Collectible?.Code is { } code
    && (code.Path == "charcoal" || code.FirstCodePart() == "coal");

  /// <summary>Vanilla parchment - its item code is <c>paper</c>.</summary>
  public static bool IsParchment(ItemStack? stack) =>
    stack?.Collectible?.Code?.Path == "paper";

  /// <summary>Both inputs are present in sufficient quantity to draft one diagram.</summary>
  public bool HasInputs =>
    IsDrawingMedium(_inventory[MediumSlot].Itemstack)
    && _inventory[MediumSlot].StackSize >= MediumCost
    && IsParchment(_inventory[ParchmentSlot].Itemstack)
    && _inventory[ParchmentSlot].StackSize >= ParchmentCost;

  /// <summary>Draws <paramref name="diagramCode"/> onto parchment: consumes one medium and one parchment and
  /// puts the diagram in the output slot. Server-side; mutates the inventory.</summary>
  /// <returns>False when the inputs are missing, the code resolves to no item, or the output slot cannot take
  /// another.</returns>
  public bool TryDraft(string diagramCode) {
    if (!HasInputs)
      return false;

    Item? diagram = Api.World.GetItem(new AssetLocation(diagramCode));
    if (diagram == null)
      return false;

    ItemSlot output = _inventory[OutputSlot];
    if (
      !output.Empty
      && (
        output.Itemstack.Collectible.Id != diagram.Id
        || output.StackSize >= output.Itemstack.Collectible.MaxStackSize
      )
    )
      return false;

    _inventory[MediumSlot].TakeOut(MediumCost);
    _inventory[ParchmentSlot].TakeOut(ParchmentCost);
    if (output.Empty)
      output.Itemstack = new ItemStack(diagram, 1);
    else
      output.Itemstack.StackSize++;

    _inventory[MediumSlot].MarkDirty();
    _inventory[ParchmentSlot].MarkDirty();
    output.MarkDirty();
    MarkDirty();
    return true;
  }

  #endregion

  // The container base persists the inventory; only the selected diagram is carried here.
  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    if (SelectedType != null)
      tree.SetString("dt_selected", SelectedType);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor world
  ) {
    base.FromTreeAttributes(tree, world);
    SelectedType = tree.GetString("dt_selected", null);
  }
}

/// <summary>
/// The design table's 3-slot inventory: a drawing-medium input, a parchment input, and a take-only output.
/// The typed input slots carry the accept rules, so only the matching material fits.
/// </summary>
public class InventoryDesignTable(string? invId, ICoreAPI? api)
  : InventoryGeneric(3, invId, api) {
  protected override ItemSlot NewSlot(int i) =>
    i switch {
      BlockEntityDesignTable.MediumSlot => new ItemSlotDesignInput(
        this,
        BlockEntityDesignTable.IsDrawingMedium,
        "#3A3A3A"
      ),
      BlockEntityDesignTable.ParchmentSlot => new ItemSlotDesignInput(
        this,
        BlockEntityDesignTable.IsParchment,
        "#D9C9A3"
      ),
      _ => new ItemSlotDesignOutput(this),
    };
}

/// <summary>An input slot that accepts only stacks matching a predicate.</summary>
public class ItemSlotDesignInput : ItemSlotSurvival {
  private readonly System.Func<ItemStack?, bool> _accepts;

  public ItemSlotDesignInput(
    InventoryBase inventory,
    System.Func<ItemStack?, bool> accepts,
    string hexColor
  )
    : base(inventory) {
    _accepts = accepts;
    HexBackgroundColor = hexColor;
  }

  public override bool CanTakeFrom(
    ItemSlot sourceSlot,
    EnumMergePriority priority = EnumMergePriority.AutoMerge
  ) => _accepts(sourceSlot.Itemstack) && base.CanTakeFrom(sourceSlot, priority);

  public override bool CanHold(ItemSlot sourceSlot) =>
    _accepts(sourceSlot.Itemstack) && base.CanHold(sourceSlot);
}

/// <summary>The drafted-output slot: take-only, so its contents cannot be overwritten by hand.</summary>
public class ItemSlotDesignOutput(InventoryBase inventory)
  : ItemSlotSurvival(inventory) {
  public override bool CanTakeFrom(
    ItemSlot sourceSlot,
    EnumMergePriority priority = EnumMergePriority.AutoMerge
  ) => false;

  public override bool CanHold(ItemSlot sourceSlot) => false;
}
