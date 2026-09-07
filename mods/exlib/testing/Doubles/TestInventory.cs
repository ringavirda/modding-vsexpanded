using Vintagestory.API.Common;

namespace ExpandedLib.Testing;

/// <summary>Builds a real, empty <see cref="InventoryGeneric"/> against a <see cref="TestWorld"/>'s
/// API, for a test that needs a genuine multi-slot inventory rather than one <see cref="ItemSlot"/>.</summary>
public static class TestInventory {
  /// <summary>An <paramref name="slots"/>-slot inventory with no slot-suitability rules of its own,
  /// registered under <paramref name="id"/>.</summary>
  public static InventoryGeneric Of(
    TestWorld world,
    int slots,
    string id = "test"
  ) => new(slots, id, world.Api);
}
