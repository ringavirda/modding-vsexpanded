using Vintagestory.API.Common;

namespace IronIndustryExpanded.BlockStructures.Furnaces;

/// <summary>
/// How many heats a steel crucible has left. A fireclay pot is a consumable that dies from exactly the
/// abuse it exists for - Sheffield practice was two or three heats - so the pot is the ceiling being
/// enforced, paid in pots rather than in refusals. See docs/design/machines/crucible-furnace.md.
/// </summary>
/// <remarks>
/// The count lives on the itemstack, which is why the pot's stack size is 1: two pots of different ages
/// must never merge into one. Pure and world-free, so the arithmetic is testable without a firepit.
/// </remarks>
public static class CrucibleFiring {
  /// <summary>The stack attribute the count lives under.</summary>
  public const string Key = "firings";

  /// <summary>Heats <paramref name="stack"/> has already been through; 0 for a new pot or a null.</summary>
  public static int Of(ItemStack? stack) =>
    stack?.Attributes?.GetInt(Key, 0) ?? 0;

  /// <summary>
  /// Stamps <paramref name="firings"/> onto <paramref name="stack"/>.
  /// </summary>
  /// <remarks>
  /// Re-stamping is not optional. Every point where vanilla moves a crucible between its variants builds
  /// a <c>new ItemStack(block)</c> rather than mutating the one in hand - <c>DoSmelt</c> at
  /// <c>BlockSmeltingContainer.cs:101</c>, and both emptied-pot swaps at
  /// <c>BlockSmeltedContainer.cs:121</c> and <c>:245</c> - so a count left on the old stack is simply
  /// dropped, and the pot lives for ever.
  /// </remarks>
  public static void Set(ItemStack? stack, int firings) =>
    stack?.Attributes?.SetInt(Key, firings < 0 ? 0 : firings);

  /// <summary>Whether a pot that has been through <paramref name="firings"/> heats is finished.</summary>
  public static bool IsSpent(int firings) =>
    firings >= IiexValues.CruciblePotFirings;

  /// <summary>Whether <paramref name="stack"/> is a pot with no heats left in it.</summary>
  public static bool IsSpent(ItemStack? stack) => IsSpent(Of(stack));
}
