using System;
using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Common;

namespace IronworkingExpanded.Items;

/// <summary>
/// The composition of a <see cref="ItemBurden"/> stack, and of one <c>ChargeSegment</c>: the relative parts
/// of iron ore, flux (lime) and fuel (carbon). Stored as raw parts and read back as fractions, so
/// splitting or merging a stack preserves the per-unit proportions.
/// <para>
/// <see cref="Fuel"/> is 0 on burden the burdenmaker stamps, which is ore and flux only. The field carries
/// carbon when the struct describes a column's composition, where fuel bands and legacy unstamped charge
/// both contribute it, and burn-out scales a legacy stamp's carbon through it.
/// </para>
/// </summary>
public readonly record struct BurdenMix(float Iron, float Flux, float Fuel) {
  public float Sum => Iron + Flux + Fuel;
  public bool HasContent => Sum > 0.0001f;

  public float IronFrac => HasContent ? Iron / Sum : 0f;
  public float FluxFrac => HasContent ? Flux / Sum : 0f;
  public float FuelFrac => HasContent ? Fuel / Sum : 0f;
}

/// <summary>
/// Read/write helpers and the config-tunable grade classifier for burden. Shared by the burden item's
/// tooltip, by <c>BlockEntityBurdenmaker</c> (the only writer - it stamps the mix and previews the grade
/// before the gate opens) and by the shaft furnaces, which read the mix to drive the heat balance.
/// </summary>
public static class Burden {
  private const string IronKey = "iron";
  private const string FluxKey = "flux";
  private const string FuelKey = "fuel";

  private const string OreCode = "iwex:burden";

  // There is one burden item and no family model. In place of a family gate, the shaft counts charge it
  // does not recognise as rejected, which blocks the conversion and shows on the HUD; that also covers
  // fuel-free rubbish and a foreign mod's item. There is no legacy `coke` attribute fallback either - the
  // burdenmaker is the only writer, and a second stamping convention is what
  // `docs/design/items/burden.md` Gotcha 2 warns against.

  /// <summary>True when <paramref name="stack"/> is the burden item (<c>iwex:burden</c>), the prepared
  /// blast-furnace charge the burdenmaker's basin and the blast furnace both gate on.</summary>
  public static bool Is([NotNullWhen(true)] ItemStack? stack) =>
    stack?.Collectible?.Code is { Domain: "iwex", Path: "burden" };

  /// <summary>
  /// The code-string form of <see cref="Is"/>. A charge column stores its material as an asset-location
  /// string (<c>ChargeSegment.Material</c>) rather than an <c>ItemStack</c>, because it holds units of a
  /// substance and a band can be split without splitting a stack, so every gate the shaft applies to
  /// charge needs a form that takes the code. Both forms live here so they cannot drift apart. The
  /// negation is how the shaft recognises fuel: a segment that is not burden is coke or charcoal.
  /// </summary>
  public static bool IsCode(string? material) => material == OreCode;

  /// <summary>Stamps the mix parts onto a burden stack (any non-negative parts; read back as fractions).</summary>
  public static void Write(ItemStack stack, BurdenMix mix) {
    var a = stack.Attributes;
    a.SetFloat(IronKey, Math.Max(0f, mix.Iron));
    a.SetFloat(FluxKey, Math.Max(0f, mix.Flux));
    a.SetFloat(FuelKey, Math.Max(0f, mix.Fuel));
  }

  /// <summary>Reads the mix parts off a burden stack; an unstamped stack reads as empty.</summary>
  public static BurdenMix Read(ItemStack? stack) {
    var a = stack?.Attributes;
    if (a == null)
      return default;
    return new BurdenMix(
      a.GetFloat(IronKey),
      a.GetFloat(FluxKey),
      a.GetFloat(FuelKey)
    );
  }

  /// <summary>
  /// Lang key of the named grade for a mix. The burden tooltip and the burdenmaker's readout both print
  /// it, so the machine and the furnace report one answer. Graded on flux alone: burden is ore and flux,
  /// coke is charged as its own bands at the furnace (<c>docs/design/layered-charge.md</c>), and with fuel
  /// off the item iron is just <c>1 - flux</c>. A low-flux mix is not inferred; <c>underfluxed</c> is an
  /// explicit band.
  /// </summary>
  public static string ProfileLangKey(BurdenMix mix) {
    if (!mix.HasContent)
      return "iwex:burden-profile-empty";

    foreach (BurdenProfile p in IwexValues.BurdenProfiles)
      if (InBand(mix.FluxFrac, p.MinFlux, p.MaxFlux))
        return "iwex:burden-profile-" + p.Key;

    // Unreachable with the shipped bands (they tile 0..1), but a player's edited config can leave a gap.
    return "iwex:burden-profile-offspec";
  }

  private static bool InBand(float value, float min, float max) =>
    value >= min && value <= max;
}
