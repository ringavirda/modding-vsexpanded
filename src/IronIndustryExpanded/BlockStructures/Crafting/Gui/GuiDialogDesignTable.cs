using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace IronIndustryExpanded.BlockStructures.Crafting.Gui;

/// <summary>
/// The design table's drafting window: the medium/parchment input slots and the drafted output, a picker
/// over every loaded diagram, an info panel describing the selected plan, and a Draw button that asks the
/// block entity to draft it. See <c>docs/design/diagram-crafting.md</c>.
/// <para>
/// Client-side only; the block entity applies slot moves and the draft through the open/close/draft packet
/// handshake (see <see cref="BlockEntities.BlockEntityDesignTable"/>). The picker enumerates the loaded
/// <c>diagram-*</c> items directly, so plans contributed by other mods appear in it.
/// </para>
/// </summary>
public class GuiDialogDesignTable : GuiDialogBlockEntity {
  /// <summary>Block-entity packet the Draw button sends, carrying the chosen diagram's code. The block
  /// entity drafts it server-side (see
  /// <see cref="BlockEntities.BlockEntityDesignTable.PacketIdDraft"/>).</summary>
  public const int PacketIdDraft = 1002;

  private readonly ICoreClientAPI _capi;
  private readonly BlockPos _pos;
  private readonly string _title;

  // Every loaded diagram, sorted by code for a stable list; the picker keys off the short code.
  private readonly Item[] _diagrams = [];
  private string? _selectedCode;

  public GuiDialogDesignTable(
    string title,
    InventoryBase inventory,
    BlockPos pos,
    ICoreClientAPI capi,
    string? preselected
  )
    : base(title, inventory, pos, capi) {
    _capi = capi;
    _pos = pos;
    _title = title;

    if (IsDuplicate)
      return;

    _diagrams = capi
      .World.Items.Where(IsDiagram)
      .OrderBy(i => i.Code.ToShortString(), StringComparer.Ordinal)
      .ToArray();

    _selectedCode =
      preselected != null
      && _diagrams.Any(i => i.Code.ToShortString() == preselected)
        ? preselected
        : _diagrams.FirstOrDefault()?.Code.ToShortString();

    Compose();
  }

  /// <summary>A drafted-plan item: first code part <c>diagram</c> with a <c>type</c> variant. Domain is
  /// not matched, so other mods' diagrams qualify too.</summary>
  private static bool IsDiagram(Item item) =>
    item?.Code != null
    && item.Code.FirstCodePart() == "diagram"
    && item.Variant != null
    && item.Variant.ContainsKey("type");

  private static string DisplayName(Item diagram) =>
    new ItemStack(diagram).GetName();

  private void Compose() {
    double slot =
      GuiElementPassiveItemSlot.unscaledSlotSize
      + GuiElementItemSlotGridBase.unscaledSlotPadding;
    const double width = 360;

    var labelFont = CairoFont.WhiteSmallText();
    var nameFont = CairoFont.WhiteSmallText().WithFontSize(17f);
    var descFont = CairoFont.WhiteDetailText();

    double y = GuiStyle.TitleBarHeight + 6;

    ElementBounds inputLabel = ElementBounds.Fixed(0, y, slot * 2, 18);
    ElementBounds outputLabel = ElementBounds.Fixed(slot * 3, y, slot, 18);

    double gridY = y + 22;
    ElementBounds inputGrid = ElementStdBounds.SlotGrid(
      EnumDialogArea.LeftTop,
      0,
      gridY,
      2,
      1
    );
    ElementBounds outputGrid = ElementStdBounds.SlotGrid(
      EnumDialogArea.LeftTop,
      slot * 3,
      gridY,
      1,
      1
    );

    double ddY = gridY + slot + 14;
    ElementBounds ddBounds = ElementBounds.Fixed(0, ddY, width, 30);
    ElementBounds nameBounds = ElementBounds.Fixed(0, ddY + 42, width, 22);
    ElementBounds descBounds = ElementBounds.Fixed(0, ddY + 66, width, 92);
    ElementBounds drawBounds = ElementBounds.Fixed(0, ddY + 168, 130, 30);

    ElementBounds bg = ElementBounds.Fill.WithFixedPadding(
      GuiStyle.ElementToDialogPadding
    );
    bg.BothSizing = ElementSizing.FitToChildren;
    ElementBounds dialog = ElementStdBounds.AutosizedMainDialog.WithAlignment(
      EnumDialogArea.CenterMiddle
    );

    string[] codes = _diagrams.Select(i => i.Code.ToShortString()).ToArray();
    string[] names = _diagrams.Select(DisplayName).ToArray();
    int sel = Array.IndexOf(codes, _selectedCode);
    if (sel < 0)
      sel = 0;

    SingleComposer = _capi
      .Gui.CreateCompo("designtable" + _pos, dialog)
      .AddShadedDialogBG(bg, true)
      .AddDialogTitleBar(_title, CloseIconPressed)
      .BeginChildElements(bg)
      .AddStaticText(Lang.Get("iiex:designtable-inputs"), labelFont, inputLabel)
      .AddStaticText(
        Lang.Get("iiex:designtable-result"),
        labelFont,
        outputLabel
      )
      .AddItemSlotGrid(Inventory, DoSendPacket, 2, [0, 1], inputGrid)
      .AddItemSlotGrid(Inventory, DoSendPacket, 1, [2], outputGrid)
      .AddDropDown(codes, names, sel, OnDiagramSelected, ddBounds, "diagramdd")
      .AddDynamicText("", nameFont, nameBounds, "diagname")
      .AddDynamicText("", descFont, descBounds, "diagdesc")
      .AddButton(Lang.Get("iiex:designtable-draw"), OnDrawClicked, drawBounds)
      .EndChildElements()
      .Compose();

    UpdateInfo();
  }

  private void OnDiagramSelected(string code, bool selected) {
    if (!selected)
      return;
    _selectedCode = code;
    UpdateInfo();
  }

  private void UpdateInfo() {
    Item? diagram = _diagrams.FirstOrDefault(i =>
      i.Code.ToShortString() == _selectedCode
    );
    string name = diagram != null ? DisplayName(diagram) : "";
    string desc =
      diagram != null
        ? Lang.GetIfExists(
          diagram.Code.Domain + ":diagramdesc-" + diagram.Variant["type"]
        ) ?? Lang.Get("iiex:designtable-desc-generic")
        : "";

    SingleComposer.GetDynamicText("diagname").SetNewText(name);
    SingleComposer
      .GetDynamicText("diagdesc")
      .SetNewText(desc, autoHeight: true);
  }

  private bool OnDrawClicked() {
    if (_selectedCode != null)
      _capi.Network.SendBlockEntityPacket(
        _pos,
        PacketIdDraft,
        SerializerUtil.Serialize(_selectedCode)
      );
    return true;
  }
}
