using System;
using System.Linq;
using IronIndustryExpanded.BlockStructures.Crafting.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Crafting.Gui;

/// <summary>
/// The workbench window: the 5x5 grid, a preview of what it currently makes, the Craft button and the
/// output inventory the result lands in. See docs/design/machines/workbench.md.
/// <para>
/// Client-side only; the block entity applies slot moves and the craft through the open/close/craft
/// packet handshake (see <see cref="BlockEntityWorkbench"/>). The preview runs vanilla's own matcher
/// against the same grid width the bench crafts at, so what it shows is what a craft produces.
/// </para>
/// </summary>
public class GuiDialogWorkbench : GuiDialogBlockEntity {
  /// <summary>Seconds between repeats while the Craft button is held. Bulk crafting is one gesture
  /// rather than N clicks, which is what makes the bench better at repetition than the player's own
  /// grid. Multi-step craft sequences are excluded from repeating by design; none ship yet, so nothing
  /// gates this beyond the button being held.</summary>
  private const float RepeatSeconds = 0.35f;

  private readonly ICoreClientAPI _capi;
  private readonly BlockPos _pos;
  private readonly string _title;

  // Holds the preview stack only; never part of the bench's own inventory.
  private readonly DummyInventory _previewInventory;
  private ElementBounds _craftBounds = ElementBounds.Fixed(0, 0, 0, 0);

  private bool _holdingCraft;
  private float _sinceCraft;

  public GuiDialogWorkbench(
    string title,
    InventoryBase inventory,
    BlockPos pos,
    ICoreClientAPI capi
  )
    : base(title, inventory, pos, capi) {
    _capi = capi;
    _pos = pos;
    _title = title;
    _previewInventory = new DummyInventory(capi);

    if (IsDuplicate)
      return;

    Inventory.SlotModified += OnSlotModified;
    Compose();
  }

  #region Composition

  private void Compose() {
    double slot =
      GuiElementPassiveItemSlot.unscaledSlotSize
      + GuiElementItemSlotGridBase.unscaledSlotPadding;
    double gridWidth = slot * BlockEntityWorkbench.GridWidth;

    var labelFont = CairoFont.WhiteSmallText();

    double y = GuiStyle.TitleBarHeight + 6;
    ElementBounds gridLabel = ElementBounds.Fixed(0, y, gridWidth, 18);

    double gridY = y + 22;
    ElementBounds grid = ElementStdBounds.SlotGrid(
      EnumDialogArea.LeftTop,
      0,
      gridY,
      BlockEntityWorkbench.GridWidth,
      BlockEntityWorkbench.GridHeight
    );

    // The preview and the button sit beside the grid, on the axis the result travels: grid, result,
    // then the output row underneath.
    double sideX = gridWidth + 24;
    ElementBounds previewLabel = ElementBounds.Fixed(sideX, y, 140, 18);
    ElementBounds preview = ElementBounds.Fixed(sideX, gridY, slot, slot);
    _craftBounds = ElementBounds.Fixed(sideX, gridY + slot + 16, 130, 30);

    double outY = gridY + slot * BlockEntityWorkbench.GridHeight + 14;
    ElementBounds outLabel = ElementBounds.Fixed(0, outY, gridWidth, 18);
    ElementBounds outGrid = ElementStdBounds.SlotGrid(
      EnumDialogArea.LeftTop,
      0,
      outY + 22,
      BlockEntityWorkbench.OutputSlots,
      1
    );

    ElementBounds bg = ElementBounds.Fill.WithFixedPadding(
      GuiStyle.ElementToDialogPadding
    );
    bg.BothSizing = ElementSizing.FitToChildren;
    ElementBounds dialog = ElementStdBounds.AutosizedMainDialog.WithAlignment(
      EnumDialogArea.CenterMiddle
    );

    int[] gridSlotIds = Enumerable
      .Range(0, BlockEntityWorkbench.GridSlots)
      .ToArray();
    int[] outputSlotIds = Enumerable
      .Range(BlockEntityWorkbench.GridSlots, BlockEntityWorkbench.OutputSlots)
      .ToArray();

    SingleComposer = _capi
      .Gui.CreateCompo("workbench" + _pos, dialog)
      .AddShadedDialogBG(bg, true)
      .AddDialogTitleBar(_title, CloseIconPressed)
      .BeginChildElements(bg)
      .AddStaticText(Lang.Get("iiex:workbench-grid"), labelFont, gridLabel)
      .AddStaticText(Lang.Get("iiex:workbench-result"), labelFont, previewLabel)
      .AddItemSlotGrid(
        Inventory,
        DoSendPacket,
        BlockEntityWorkbench.GridWidth,
        gridSlotIds,
        grid
      )
      .AddPassiveItemSlot(preview, _previewInventory, _previewInventory[0])
      .AddButton(Lang.Get("iiex:workbench-craft"), OnCraftClicked, _craftBounds)
      .AddStaticText(Lang.Get("iiex:workbench-output"), labelFont, outLabel)
      .AddItemSlotGrid(
        Inventory,
        DoSendPacket,
        BlockEntityWorkbench.OutputSlots,
        outputSlotIds,
        outGrid
      )
      .EndChildElements()
      .Compose();

    UpdatePreview();
  }

  #endregion

  #region Preview

  private void OnSlotModified(int slotId) {
    if (slotId < BlockEntityWorkbench.GridSlots)
      UpdatePreview();
  }

  /// <summary>Shows what the loaded grid currently makes, matched exactly as the bench matches it.</summary>
  private void UpdatePreview() {
    _previewInventory[0].Itemstack = MatchedOutput();
    _previewInventory[0].MarkDirty();
  }

  private ItemStack? MatchedOutput() {
    IPlayer? player = _capi.World.Player;
    if (player == null)
      return null;

    ItemSlot[] gridSlots = Enumerable
      .Range(0, BlockEntityWorkbench.GridSlots)
      .Select(i => Inventory[i])
      .ToArray();

    return _capi
      .World.GridRecipes.FirstOrDefault(r =>
        r.Matches(
          player,
          _capi.World,
          gridSlots,
          BlockEntityWorkbench.GridWidth
        )
      )
      ?.Output?.ResolvedItemStack?.Clone();
  }

  #endregion

  #region Craft

  /// <summary>The button's own handler, which does not craft: a text button fires on mouse *up*, so
  /// crafting here as well as from the held-down clock below would give a held press one extra craft on
  /// release. The press is what starts a run, and a click shorter than one interval is one craft.
  /// Returns true so the click is still consumed.</summary>
  private static bool OnCraftClicked() => true;

  private void SendCraft() =>
    _capi.Network.SendBlockEntityPacket(
      _pos,
      BlockEntityWorkbench.PacketIdCraft
    );

  public override void OnMouseDown(MouseEvent args) {
    base.OnMouseDown(args);
    if (
      args.Button == EnumMouseButton.Left
      && _craftBounds.PointInside(args.X, args.Y)
    ) {
      _holdingCraft = true;
      // Due immediately, so a press crafts on the next frame rather than after the first interval.
      _sinceCraft = RepeatSeconds;
    }
  }

  public override void OnMouseUp(MouseEvent args) {
    base.OnMouseUp(args);
    _holdingCraft = false;
  }

  public override void OnRenderGUI(float deltaTime) {
    base.OnRenderGUI(deltaTime);
    if (!_holdingCraft)
      return;

    _sinceCraft += deltaTime;
    if (_sinceCraft < RepeatSeconds)
      return;

    _sinceCraft = 0f;
    SendCraft();
  }

  #endregion

  public override void OnGuiClosed() {
    _holdingCraft = false;
    base.OnGuiClosed();
  }

  public override void Dispose() {
    Inventory.SlotModified -= OnSlotModified;
    base.Dispose();
  }
}
