using ExpandedLib.Blocks;
using ExpandedLib.Machines;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockStructures.Crafting.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace IronIndustryExpanded.BlockStructures.Crafting.BlockEntities;

/// <summary>
/// The design table's block entity. Holds the drafting inventory (drawing medium, parchment, output) and the
/// draft logic: draw a chosen diagram onto parchment, consuming one medium and one parchment. Right-click
/// opens <see cref="GuiDialogDesignTable"/>, where the player picks a diagram and presses Draw, which sends
/// the draft packet handled here. See <c>docs/design/mechanics/diagram-crafting.md</c>.
/// <para>
/// The window, its packet handshake and its disposal come from
/// <see cref="BlockEntityMachineStation"/>; only the slot rules and the draft belong to this table.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityDesignTable : BlockEntityMachineStation {
  /// <summary>Inventory slots: the drawing medium (charcoal/coal), the parchment, and the drafted output.</summary>
  public const int MediumSlot = 0;
  public const int ParchmentSlot = 1;
  public const int OutputSlot = 2;

  /// <summary>Materials one draft consumes.</summary>
  public const int MediumCost = 1;
  public const int ParchmentCost = 1;

  /// <summary>The Draw button's packet: the chosen diagram's code, drafted server-side.</summary>
  public const int PacketIdDraft = FirstMachinePacketId;

  protected override MachineSlotSpec[] SlotSpecs =>
    [
      MachineSlotSpec.Input(IsDrawingMedium, "#3A3A3A"),
      MachineSlotSpec.Input(IsParchment, "#D9C9A3"),
      MachineSlotSpec.Output(),
    ];

  public override string InventoryClassName => "designtable";

  /// <summary>Code of the diagram last drafted, persisted so the window re-opens on it. Null until the first
  /// draft.</summary>
  [Persist("dt_selected")]
  public string? SelectedType { get; set; }

  #region Interaction

  /// <summary>Right-click: toggles the drafting window.</summary>
  public void OnInteract(IPlayer byPlayer) => ToggleWindow(byPlayer);

  protected override GuiDialogBlockEntity CreateDialog(ICoreClientAPI capi) =>
    new GuiDialogDesignTable(
      Lang.Get("iiex:designtable-title"),
      Inventory,
      Pos,
      capi,
      SelectedType
    );

  /// <summary>Handles the Draw request; every other window packet is the station's own protocol.</summary>
  protected override bool OnStationPacket(
    IPlayer player,
    int packetid,
    byte[] data
  ) {
    if (packetid != PacketIdDraft)
      return false;

    string diagramCode = SerializerUtil.Deserialize<string>(data);
    SelectedType = diagramCode;
    TryDraft(diagramCode);
    return true;
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
    IsDrawingMedium(Inventory[MediumSlot].Itemstack)
    && Inventory[MediumSlot].StackSize >= MediumCost
    && IsParchment(Inventory[ParchmentSlot].Itemstack)
    && Inventory[ParchmentSlot].StackSize >= ParchmentCost;

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

    ItemSlot output = Inventory[OutputSlot];
    if (
      !output.Empty
      && (
        output.Itemstack.Collectible.Id != diagram.Id
        || output.StackSize >= output.Itemstack.Collectible.MaxStackSize
      )
    )
      return false;

    Inventory[MediumSlot].TakeOut(MediumCost);
    Inventory[ParchmentSlot].TakeOut(ParchmentCost);
    if (output.Empty)
      output.Itemstack = new ItemStack(diagram, 1);
    else
      output.Itemstack.StackSize++;

    Inventory[MediumSlot].MarkDirty();
    Inventory[ParchmentSlot].MarkDirty();
    output.MarkDirty();
    MarkDirty();
    return true;
  }

  #endregion

  // The container base persists the inventory; only the selected diagram is carried here.
}
