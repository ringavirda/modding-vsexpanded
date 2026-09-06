using System.Collections.Generic;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Metals;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Industry.Molten;

/// <summary>
/// Coarse thermal state of a metal stack relative to its melting point.
/// </summary>
public enum MoltenState {
  /// <summary>Above the liquid threshold (default 80% of the melting point): flows freely.</summary>
  Liquid,

  /// <summary>Between the hardened and liquid thresholds: no longer flows, still hot.</summary>
  Cooling,

  /// <summary>Below the hardened threshold (default 30% of the melting point): chisellable.</summary>
  Hardened,
}

/// <summary>
/// Single source of truth for treating an <see cref="ItemStack"/> as a carrier of molten metal:
/// creating the temperature-tracked stack, reading/writing temperature, classifying thermal state,
/// the incandescent block-light scale, and player-facing metal/state formatting. Shared by every
/// canal cell, tap, pedestal, barrel and the bessemer charge.
/// </summary>
public static class MoltenMetal {
  /// <summary>Fraction of the melting point above which metal counts as liquid.</summary>
  public static float LiquidThreshold => ExlibValues.MetalLiquidThreshold;

  /// <summary>Fraction of the melting point below which metal counts as fully hardened.</summary>
  public static float HardenedThreshold => ExlibValues.MetalHardenedThreshold;

  /// <summary>Below this temperature (°C) hot metal emits no block light.</summary>
  public static float GlowMinTemp => ExlibValues.MetalGlowMinTemp;

  /// <summary>
  /// Creates a single-item temperature carrier for <paramref name="itemCode"/> at
  /// <paramref name="temperature"/> °C, cooling at <paramref name="cooldownSpeed"/>
  /// (default: the mod's molten cooldown). Returns <c>null</c> when the item does not
  /// resolve.
  /// </summary>
  public static ItemStack? CreateStack(
    IWorldAccessor world,
    string itemCode,
    float temperature,
    float? cooldownSpeed = null
  ) {
    Item? item =
      itemCode.Length > 0 ? world.GetItem(new AssetLocation(itemCode)) : null;
    if (item == null)
      return null;
    var stack = new ItemStack(item, 1);
    // SetTemperature first: it creates the "temperature" tree SetCooldownSpeed writes into. On a fresh
    // stack that tree does not exist yet, so setting the cooldown before it silently no-ops.
    SetTemperature(world, stack, temperature);
    SetCooldownSpeed(stack, cooldownSpeed ?? ExlibValues.MoltenCooldownDefault);
    return stack;
  }

  /// <summary>Sets the VS time-based cooldown speed on an existing temperature carrier.</summary>
  public static void SetCooldownSpeed(ItemStack stack, float cooldownSpeed) =>
    (stack.Attributes["temperature"] as ITreeAttribute)?.SetFloat(
      "cooldownSpeed",
      cooldownSpeed
    );

  /// <summary>
  /// Re-applies the cooldown rate to an already-stamped stack, rebasing the baseline to the stack's
  /// current temperature so a changed <c>cooldownSpeed</c> takes effect from this moment forward.
  /// Vanilla scales elapsed cooling by <c>cooldownSpeed</c>, so rewriting the rate without the rebase
  /// would retro-apply it across the whole span since the last stamp. Call once per tick on standing
  /// molten content (canal cells, parked molds, barrels, the bessemer charge) so a live
  /// <c>/exmod config</c> change reaches metal already in the world. With an unchanged rate the rebase
  /// is exact, so the common case is a no-op.
  /// </summary>
  public static void SyncCooldownSpeed(
    IWorldAccessor world,
    ItemStack stack,
    float? cooldownSpeed = null
  ) {
    SetTemperature(world, stack, GetTemperature(world, stack));
    SetCooldownSpeed(stack, cooldownSpeed ?? ExlibValues.MoltenCooldownDefault);
  }

  /// <summary>Sets the stack temperature without delaying the cooldown (the mod-wide convention).</summary>
  public static void SetTemperature(
    IWorldAccessor world,
    ItemStack stack,
    float temperature
  ) =>
    stack.Collectible.SetTemperature(
      world,
      stack,
      temperature,
      delayCooldown: false
    );

  /// <summary>Current stack temperature (°C).</summary>
  public static float GetTemperature(IWorldAccessor world, ItemStack stack) =>
    stack.Collectible.GetTemperature(world, stack);

  /// <summary>The stack's melting point (°C), resolved through a dummy slot.</summary>
  public static float MeltingPointOf(IWorldAccessor world, ItemStack stack) =>
    stack.Collectible.GetMeltingPoint(world, null, new DummySlot(stack));

  /// <summary>Classifies the stack against its melting point (liquid / cooling / hardened).</summary>
  public static MoltenState StateOf(IWorldAccessor world, ItemStack stack) =>
    Classify(
      GetTemperature(world, stack),
      MeltingPointOf(world, stack),
      stack.Collectible.Code
    );

  /// <summary>
  /// Classifies a metal at <paramref name="temperature"/> (°C) against its
  /// <paramref name="meltingPoint"/> using the thresholds registered for <paramref name="moltenItem"/>:
  /// a <see cref="MetalDef"/>'s <c>liquidThreshold</c>/<c>hardenedThreshold</c> when it ships them,
  /// otherwise the global <see cref="ExlibValues"/> defaults. Pure and world-free, so the stack-based
  /// <see cref="StateOf"/> and the canal cell (which tracks its own temperature rather than a live
  /// stack) share one primitive and a registered metal's thresholds apply at every classification site.
  /// </summary>
  public static MoltenState Classify(
    float temperature,
    float meltingPoint,
    AssetLocation moltenItem
  ) {
    if (
      temperature
      > MetalRegistry.LiquidThresholdOf(moltenItem) * meltingPoint
    )
      return MoltenState.Liquid;
    if (
      temperature
      < MetalRegistry.HardenedThresholdOf(moltenItem) * meltingPoint
    )
      return MoltenState.Hardened;
    return MoltenState.Cooling;
  }

  /// <summary>True when the stack has cooled below the hardened threshold (chisellable).</summary>
  public static bool IsHardened(IWorldAccessor world, ItemStack stack) =>
    StateOf(world, stack) == MoltenState.Hardened;

  /// <summary>True when the stack is hot enough to flow (above the liquid threshold).</summary>
  public static bool IsLiquid(IWorldAccessor world, ItemStack stack) =>
    StateOf(world, stack) == MoltenState.Liquid;

  /// <summary>
  /// Incandescent block-light level (0-24) for metal at <paramref name="temperature"/>. The shared
  /// scale used by canals, barrels and the cowper heat sink.
  /// </summary>
  public static byte GlowLevel(float temperature) =>
    temperature > GlowMinTemp
      ? (byte)GameMath.Clamp((temperature - GlowMinTemp) / 30f, 0, 24)
      : (byte)0;

  /// <summary>
  /// Human-readable metal name from an item code ("game:ingot-iron" → "Iron", "iiex:slag" → "Slag").
  /// Delegates to <see cref="MetalRegistry.DisplayName"/>, which honours a registered metal's
  /// localization key and otherwise applies the strip-and-capitalise convention.
  /// </summary>
  public static string DisplayName(string metalItemCode) =>
    MetalRegistry.DisplayName(metalItemCode);

  /// <summary>
  /// Formats a molten temperature for display through <see cref="ExMeasure.Temperature"/>, honouring the
  /// player's metric/imperial preference. Injectable so a consumer can override it; the simulation
  /// itself always stays metric.
  /// </summary>
  public static System.Func<float, string> TemperatureFormatter { get; set; } =
    t => ExMeasure.Temperature(t);

  /// <summary>"Cold" below room temperature, otherwise the formatted temperature.</summary>
  public static string FormatTemperature(float temperature) =>
    temperature < 21f
      ? Lang.Get("exlib:metalstate-cold")
      : TemperatureFormatter(temperature);

  /// <summary>
  /// Every smelted-crucible block as an <see cref="ItemStack"/>: the "pour from" list a molten sink
  /// advertises in its interaction help (canal start, barrel). Matched by code path
  /// (<c>crucible-*-smelted</c>), domain-blind, so vanilla and modded crucibles qualify alike. Scans
  /// every loaded block; call once on block load and cache the result.
  /// </summary>
  public static ItemStack[] SmeltedCrucibleStacks(IWorldAccessor world) {
    var stacks = new List<ItemStack>();
    foreach (Block block in world.Blocks) {
      if (
        block.Code != null
        && block.Code.Path.StartsWith("crucible-")
        && block.Code.Path.EndsWith("-smelted")
      )
        stacks.Add(new ItemStack(block));
    }
    return stacks.ToArray();
  }
}
