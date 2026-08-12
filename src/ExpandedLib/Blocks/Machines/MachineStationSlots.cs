using Vintagestory.API.Common;

namespace ExpandedLib.Blocks.Machines;

/// <summary>
/// What one slot of a machine station accepts. A station declares an array of these and
/// <see cref="MachineStationInventory"/> builds the matching slots, so a machine states its inputs,
/// tooling and outputs as data rather than by subclassing the inventory.
/// </summary>
/// <param name="Accepts">Predicate a stack must satisfy to enter the slot. Null accepts anything.</param>
/// <param name="TakeOnly">True for an output: the player may take from it but never put into it.</param>
/// <param name="HexBackgroundColor">Slot tint, so tooling and inputs read apart at a glance.</param>
public readonly record struct MachineSlotSpec(
  System.Func<ItemStack?, bool>? Accepts = null,
  bool TakeOnly = false,
  string? HexBackgroundColor = null
) {
  /// <summary>An input taking only stacks matching <paramref name="accepts"/>.</summary>
  public static MachineSlotSpec Input(
    System.Func<ItemStack?, bool> accepts,
    string? hexColor = null
  ) => new(accepts, false, hexColor);

  /// <summary>An input taking anything.</summary>
  public static MachineSlotSpec AnyInput(string? hexColor = null) =>
    new(null, false, hexColor);

  /// <summary>A take-only output slot.</summary>
  public static MachineSlotSpec Output(string? hexColor = null) =>
    new(null, true, hexColor);
}

/// <summary>
/// A machine station's inventory, built from the station's <see cref="MachineSlotSpec"/> array.
/// <see cref="InventoryGeneric"/>'s own slot-factory delegate does the building, so the specs reach
/// each slot without the inventory needing a subclass per machine.
/// </summary>
public class MachineStationInventory(
  MachineSlotSpec[] specs,
  string? invId = null,
  ICoreAPI? api = null
)
  : InventoryGeneric(
    specs.Length,
    invId,
    api,
    (id, self) => BuildSlot(specs, id, self)
  ) {
  private static ItemSlot BuildSlot(
    MachineSlotSpec[] specs,
    int id,
    InventoryGeneric self
  ) {
    MachineSlotSpec spec = id < specs.Length ? specs[id] : default;
    ItemSlot slot = spec.TakeOnly
      ? new ItemSlotMachineOutput(self)
      : new ItemSlotMachineInput(self, spec.Accepts);
    if (spec.HexBackgroundColor != null)
      slot.HexBackgroundColor = spec.HexBackgroundColor;
    return slot;
  }
}

/// <summary>An input slot that accepts only stacks matching its predicate; a null predicate takes
/// anything.</summary>
public class ItemSlotMachineInput(
  InventoryBase inventory,
  System.Func<ItemStack?, bool>? accepts
) : ItemSlotSurvival(inventory) {
  public override bool CanTakeFrom(
    ItemSlot sourceSlot,
    EnumMergePriority priority = EnumMergePriority.AutoMerge
  ) =>
    (accepts?.Invoke(sourceSlot.Itemstack) ?? true)
    && base.CanTakeFrom(sourceSlot, priority);

  public override bool CanHold(ItemSlot sourceSlot) =>
    (accepts?.Invoke(sourceSlot.Itemstack) ?? true) && base.CanHold(sourceSlot);
}

/// <summary>A take-only output slot, so a finished piece cannot be overwritten by hand.</summary>
public class ItemSlotMachineOutput(InventoryBase inventory)
  : ItemSlotSurvival(inventory) {
  public override bool CanTakeFrom(
    ItemSlot sourceSlot,
    EnumMergePriority priority = EnumMergePriority.AutoMerge
  ) => false;

  public override bool CanHold(ItemSlot sourceSlot) => false;
}
