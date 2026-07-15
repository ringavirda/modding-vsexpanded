using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Metals;

/// <summary>
/// A body of molten metal held inside a machine or fitting: the temperature-tracked
/// <see cref="ItemStack"/> carrier that identifies the metal and its heat, plus a unit count. Wraps
/// the reads/writes - temperature, live cooldown, state classification, chisel recovery, tree
/// round-trip - that the bessemer converter, the molten barrel, the canal tap and the mold pedestal
/// each used to hand-roll around a bare <c>ItemStack _content</c> + <c>int _units</c> pair
/// (audit Theme D). One domain type they all compose.
/// <para>
/// A <c>MoltenCharge</c> instance is a PRESENT charge (its <see cref="Stack"/> is never null); an
/// absent charge is a null reference. World-coupled reads (temperature, melting point) take the
/// <see cref="IWorldAccessor"/> the same way <see cref="MoltenMetal"/> does - the heat lives on the
/// stack's vanilla temperature tree, so it keeps decaying on its own between reads.
/// </para>
/// </summary>
public sealed class MoltenCharge
{
  private MoltenCharge(ItemStack stack, int units)
  {
    Stack = stack;
    Units = units;
  }

  /// <summary>The molten-metal carrier stack (identity + tracked temperature). Never null.</summary>
  public ItemStack Stack { get; private set; }

  /// <summary>Units of metal held.</summary>
  public int Units { get; set; }

  /// <summary>The carrier's item code (e.g. <c>game:ingot-iron</c>).</summary>
  public AssetLocation MetalCode => Stack.Collectible.Code;

  #region Factories
  /// <summary>Wraps an existing molten stack + unit count as a charge.</summary>
  public static MoltenCharge Of(ItemStack stack, int units) => new(stack, units);

  /// <summary>
  /// Creates a charge of <paramref name="units"/> of <paramref name="itemCode"/> at
  /// <paramref name="temperature"/> °C, cooling at <paramref name="cooldownSpeed"/> (default: the
  /// mod's molten cooldown). Returns <c>null</c> when the item code does not resolve.
  /// </summary>
  public static MoltenCharge? Create(
    IWorldAccessor world,
    string itemCode,
    float temperature,
    int units,
    float? cooldownSpeed = null
  )
  {
    ItemStack? stack = MoltenMetal.CreateStack(
      world,
      itemCode,
      temperature,
      cooldownSpeed
    );
    return stack == null ? null : new MoltenCharge(stack, units);
  }
  #endregion

  #region Temperature + state
  /// <summary>Current charge temperature (°C).</summary>
  public float Temperature(IWorldAccessor world) =>
    MoltenMetal.GetTemperature(world, Stack);

  /// <summary>Stamps the charge temperature (without delaying the cooldown, the mod convention).</summary>
  public void SetTemperature(IWorldAccessor world, float temperature) =>
    MoltenMetal.SetTemperature(world, Stack, temperature);

  /// <summary>Re-applies the (live) cooldown rate, rebasing to the current temperature - call once
  /// per tick so a live <c>/exmod config</c> cooldown change reaches metal already in the world.</summary>
  public void SyncCooldown(IWorldAccessor world, float cooldownSpeed) =>
    MoltenMetal.SyncCooldownSpeed(world, Stack, cooldownSpeed);

  /// <summary>The charge's melting point (°C).</summary>
  public float MeltingPoint(IWorldAccessor world) =>
    MoltenMetal.MeltingPointOf(world, Stack);

  /// <summary>True while the charge is hot enough to flow (above the liquid threshold).</summary>
  public bool IsLiquid(IWorldAccessor world) => MoltenMetal.IsLiquid(world, Stack);

  /// <summary>True once the charge has cooled below the hardened (chisellable) threshold.</summary>
  public bool IsHardened(IWorldAccessor world) => MoltenMetal.IsHardened(world, Stack);

  /// <summary>True once the charge has cooled below its melting point (solidified but perhaps still
  /// too hot to chisel). The latch-worthy "is it frozen" test the converter/tap gate on.</summary>
  public bool IsBelowMeltingPoint(IWorldAccessor world) =>
    Temperature(world) < MeltingPoint(world);
  #endregion

  #region Transform + recovery
  /// <summary>
  /// Replaces the carrier with <paramref name="newItemCode"/> at the charge's CURRENT temperature,
  /// keeping the unit count (the iron→steel refine step). Returns <c>false</c> - leaving the charge
  /// unchanged - when the new item code does not resolve.
  /// </summary>
  public bool RetypeTo(
    IWorldAccessor world,
    string newItemCode,
    float? cooldownSpeed = null
  )
  {
    ItemStack? next = MoltenMetal.CreateStack(
      world,
      newItemCode,
      Temperature(world),
      cooldownSpeed
    );
    if (next == null)
      return false;
    Stack = next;
    return true;
  }

  /// <summary>The solid recovery drop for <paramref name="units"/> of this charge (metal bits, or
  /// slag as a fallback), carrying the charge temperature. See <see cref="MoltenChisel.BuildRecovery"/>.</summary>
  public ItemStack? BuildRecovery(
    IWorldAccessor world,
    int units,
    int unitsPerBit = 5,
    bool slagFallback = false
  ) =>
    MoltenChisel.BuildRecovery(
      world,
      MetalCode,
      Temperature(world),
      units,
      unitsPerBit,
      slagFallback
    );
  #endregion

  #region Serialization
  /// <summary>Writes the charge under the given tree keys (stack + unit count).</summary>
  public void ToTree(ITreeAttribute tree, string stackKey, string unitsKey)
  {
    tree.SetItemstack(stackKey, Stack);
    tree.SetInt(unitsKey, Units);
  }

  /// <summary>Reads a charge from the given tree keys, resolving the stack's item; returns
  /// <c>null</c> when no stack is stored.</summary>
  public static MoltenCharge? FromTree(
    ITreeAttribute tree,
    string stackKey,
    string unitsKey,
    IWorldAccessor world
  )
  {
    ItemStack? stack = tree.GetItemstack(stackKey);
    if (stack == null)
      return null;
    stack.ResolveBlockOrItem(world);
    return new MoltenCharge(stack, tree.GetInt(unitsKey));
  }
  #endregion
}
