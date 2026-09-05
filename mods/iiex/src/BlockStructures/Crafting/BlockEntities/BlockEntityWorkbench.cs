using System;
using System.Linq;
using ExpandedLib.Blocks.Machines;
using ExpandedLib.Registries.Entities;
using IronIndustryExpanded.BlockStructures.Crafting.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace IronIndustryExpanded.BlockStructures.Crafting.BlockEntities;

/// <summary>
/// The workbench's block entity: a 5x5 crafting grid over an output inventory, matched with vanilla's own
/// grid matcher. Right-click opens <see cref="GuiDialogWorkbench"/>, whose Craft button sends the craft
/// packet handled here. See docs/design/machines/workbench.md.
/// <para>
/// The window, its packet handshake and its disposal come from <see cref="BlockEntityMachineStation"/>;
/// only the grid, the output distribution and the craft belong to this bench.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityWorkbench : BlockEntityMachineStation {
  #region Shape of the bench

  /// <summary>Columns the grid offers. <see cref="GridRecipe"/>'s matcher is grid-size agnostic -
  /// <c>Width</c>/<c>Height</c> are plain properties defaulting to 3 and its scan walks every
  /// sub-position - so a wider bench needs no recipe engine and every 3x3 recipe matches inside it.</summary>
  public const int GridWidth = 5;

  /// <summary>Rows the grid offers.</summary>
  public const int GridHeight = 5;

  /// <summary>Grid slots, occupying inventory indices <c>0 .. GridSlots-1</c>.</summary>
  public const int GridSlots = GridWidth * GridHeight;

  /// <summary>Output slots, occupying the indices after the grid. An inventory rather than one slot
  /// because the stack cap that motivates the grid applies on the way out too: a craft yielding more
  /// than one stack has to land somewhere, and a single slot would stall the repeat rule after one.</summary>
  public const int OutputSlots = GridWidth;

  /// <summary>The Craft button's packet.</summary>
  public const int PacketIdCraft = FirstMachinePacketId;

  protected override MachineSlotSpec[] SlotSpecs =>
    [
      .. Enumerable.Repeat(MachineSlotSpec.AnyInput(), GridSlots),
      .. Enumerable.Repeat(MachineSlotSpec.Output(), OutputSlots),
    ];

  public override string InventoryClassName => "workbench";

  /// <summary>The grid slots in row-major order, which is the order the matcher reads them in.</summary>
  public ItemSlot[] GridSlotArray =>
    Enumerable.Range(0, GridSlots).Select(i => Inventory[i]).ToArray();

  /// <summary>The output slots.</summary>
  public ItemSlot[] OutputSlotArray =>
    Enumerable
      .Range(GridSlots, OutputSlots)
      .Select(i => Inventory[i])
      .ToArray();

  #endregion

  #region Interaction

  /// <summary>Right-click: toggles the bench window.</summary>
  public void OnInteract(IPlayer byPlayer) => ToggleWindow(byPlayer);

  protected override GuiDialogBlockEntity CreateDialog(ICoreClientAPI capi) =>
    new GuiDialogWorkbench(
      Lang.Get("iiex:workbench-title"),
      Inventory,
      Pos,
      capi
    );

  protected override bool OnStationPacket(
    IPlayer player,
    int packetid,
    byte[] data
  ) {
    if (packetid != PacketIdCraft)
      return false;

    TryCraft(player);
    return true;
  }

  #endregion

  #region Crafting

  /// <summary>
  /// The first loaded grid recipe the grid satisfies, or null. Read against this bench's own width, so a
  /// recipe narrower than the grid matches wherever the player laid it out.
  /// </summary>
  public GridRecipe? MatchingRecipe(IPlayer forPlayer) =>
    Api?.World.GridRecipes.FirstOrDefault(r =>
      r.Matches(forPlayer, Api.World, GridSlotArray, GridWidth)
    );

  /// <summary>
  /// Crafts once: takes the matched recipe's output, consumes the grid and distributes the result across
  /// the output inventory. Refuses without consuming anything when the output has no room, so a full
  /// bench cannot eat a grid. Server-side; the client asks through <see cref="PacketIdCraft"/>.
  /// </summary>
  public bool TryCraft(IPlayer byPlayer) {
    if (Api == null || Api.Side != EnumAppSide.Server)
      return false;

    GridRecipe? recipe = MatchingRecipe(byPlayer);
    ItemStack? resolved = recipe?.Output?.ResolvedItemStack?.Clone();
    if (recipe == null || resolved == null)
      return false;

    // The bench is meant to craft anything the player's own grid does, and a collectible may write onto
    // its own output as it is made - a pie takes its filling from the slots, an omni-rotatable block its
    // facing. Given through a scratch slot because that hook works on one output slot while a bench
    // result may fill several.
    ItemSlot[] grid = GridSlotArray;
    var made = new DummySlot(resolved);
    resolved.Collectible.OnCreatedByCrafting(grid, made, recipe);
    ItemStack? output = made.Itemstack;
    if (output == null)
      return false;

    ItemSlot[] outputs = OutputSlotArray;
    if (!Distribute(outputs, output, Api.World, simulate: true))
      return false;
    if (!recipe.ConsumeInput(byPlayer, grid, GridWidth))
      return false;

    Distribute(outputs, output, Api.World, simulate: false);
    MarkDirty();
    return true;
  }

  /// <summary>
  /// Spreads <paramref name="stack"/> across <paramref name="outputs"/>, filling partial stacks of the
  /// same item before empty slots and capping each at the item's own stack size. Returns whether the
  /// whole stack fits; under <paramref name="simulate"/> nothing is written, which is how a craft is
  /// refused before the grid is consumed.
  /// </summary>
  public static bool Distribute(
    ItemSlot[] outputs,
    ItemStack stack,
    IWorldAccessor world,
    bool simulate
  ) {
    int remaining = stack.StackSize;
    int cap = Math.Max(1, stack.Collectible.MaxStackSize);

    // Partial stacks first, so a repeat craft tops up what is already there rather than opening a new
    // slot each time and running out of room early.
    foreach (ItemSlot slot in outputs) {
      if (remaining <= 0)
        break;
      if (slot.Itemstack == null || !Mergeable(slot.Itemstack, stack, world))
        continue;

      int room = cap - slot.Itemstack.StackSize;
      if (room <= 0)
        continue;

      int put = Math.Min(room, remaining);
      remaining -= put;
      if (simulate)
        continue;
      slot.Itemstack.StackSize += put;
      slot.MarkDirty();
    }

    foreach (ItemSlot slot in outputs) {
      if (remaining <= 0)
        break;
      if (slot.Itemstack != null)
        continue;

      int put = Math.Min(cap, remaining);
      remaining -= put;
      if (simulate)
        continue;
      ItemStack placed = stack.Clone();
      placed.StackSize = put;
      slot.Itemstack = placed;
      slot.MarkDirty();
    }

    return remaining <= 0;
  }

  /// <summary>
  /// Whether an output stack can be topped up with <paramref name="incoming"/>: the same item, with
  /// equal attributes bar the ones the game already ignores when merging stacks. Spelled out rather
  /// than deferred to <see cref="ItemStack.Equals(IWorldAccessor, ItemStack, string[])"/>, which reads
  /// the world off the collectible's own captured API instead of the one it is handed.
  /// </summary>
  private static bool Mergeable(
    ItemStack held,
    ItemStack incoming,
    IWorldAccessor world
  ) =>
    held.Collectible == incoming.Collectible
    && held.Attributes.Equals(
      world,
      incoming.Attributes,
      GlobalConstants.IgnoredStackAttributes
    );

  #endregion
}
