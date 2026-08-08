using System;
using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Common;

namespace IronworkingExpanded.Items;

/// <summary>
/// The composition of a <see cref="ItemBurden"/> stack, and of one <c>ChargeSegment</c>: the relative parts
/// of iron ore, flux (lime) and fuel (carbon). Stored as raw parts and read back as fractions, so
/// splitting or merging a stack preserves the per-unit proportions.
/// <para>
/// <b><see cref="Fuel"/> is always 0 on burden the burdenmaker made</b> - burden is ore and flux. The
/// field stays because the same struct describes a <em>column's</em> composition, where fuel bands and
/// legacy unstamped charge both contribute carbon, and because burn-out scales a legacy stamp's carbon
/// through it.
/// </para>
/// </summary>
public readonly record struct BurdenMix(float Iron, float Flux, float Fuel)
{
  public float Sum => Iron + Flux + Fuel;
  public bool HasContent => Sum > 0.0001f;

  public float IronFrac => HasContent ? Iron / Sum : 0f;
  public float FluxFrac => HasContent ? Flux / Sum : 0f;
  public float FuelFrac => HasContent ? Fuel / Sum : 0f;
}

/// <summary>
/// Read/write helpers and the (config-tunable) grade classifier for burden. Shared by the burden item's
/// tooltip, the <c>BlockEntityBurdenmaker</c> (the only writer - it stamps the mix and previews the grade
/// before the gate opens) and the shaft furnaces (they read the mix to drive the heat balance).
/// </summary>
public static class Burden
{
  private const string IronKey = "iron";
  private const string FluxKey = "flux";
  private const string FuelKey = "fuel";

  private const string OreCode = "iwex:burden";

  // There is one burden item and deliberately no family model: "which family" would have exactly one
  // answer, and a gate with one answer is not a gate - it is a thing that can only ever be wrong. The
  // cupola charges metal directly, so no second burden family has a consumer.
  //
  // In place of a family gate, the shaft counts anything it does not recognise as charge as *rejected*,
  // which blocks the conversion and keeps the HUD honest. That covers what a family check would cover
  // and also the cases it never could (fuel-free rubbish, a foreign mod's item).
  //
  // There is likewise no legacy `coke` attribute fallback: a second stamping convention is what
  // `docs/design/items/burden.md` Gotcha 2 warns against, and the burdenmaker is the only writer in
  // existence, so nothing can be stamped any other way.
  // `Two_batches_at_the_same_ratio_read_the_same_grade_and_merge` pins that closed.

  /// <summary>True when <paramref name="stack"/> is the burden item (<c>iwex:burden</c>) - the prepared
  /// blast-furnace charge the burdenmaker's basin and the blast furnace both gate on.</summary>
  public static bool Is([NotNullWhen(true)] ItemStack? stack) =>
    stack?.Collectible?.Code is { Domain: "iwex", Path: "burden" };

  /// <summary>
  /// The code-string form of <see cref="Is"/>. A charge column stores its material as an
  /// asset-location <b>string</b> (<c>ChargeSegment.Material</c>), never as an <c>ItemStack</c> - it holds
  /// units of a substance, which is the whole reason the layered model can split a band without splitting
  /// a stack. So every gate the shaft applies to charge needs a form that takes the code, and the pair
  /// lives here rather than apart so they cannot drift into disagreeing about what a burden is.
  /// <para>
  /// Its negation is how the shaft recognises <b>fuel</b>: a segment that is not burden is coke (or
  /// charcoal), which is what the raceway needs under the burden for the column to light at all.
  /// </para>
  /// </summary>
  public static bool IsCode(string? material) => material == OreCode;

  /// <summary>Stamps the mix parts onto a burden stack (any non-negative parts; read back as fractions).</summary>
  public static void Write(ItemStack stack, BurdenMix mix)
  {
    var a = stack.Attributes;
    a.SetFloat(IronKey, Math.Max(0f, mix.Iron));
    a.SetFloat(FluxKey, Math.Max(0f, mix.Flux));
    a.SetFloat(FuelKey, Math.Max(0f, mix.Fuel));
  }

  /// <summary>Reads the mix parts off a burden stack; an unstamped stack reads as empty.</summary>
  public static BurdenMix Read(ItemStack? stack)
  {
    var a = stack?.Attributes;
    if (a == null)
      return default;
    return new BurdenMix(a.GetFloat(IronKey), a.GetFloat(FluxKey), a.GetFloat(FuelKey));
  }

  /// <summary>
  /// Lang key of the named grade for a mix - the burden tooltip and the burdenmaker's readout both print
  /// it, so <em>"right"</em> at the machine and <em>"right"</em> at the furnace are one answer.
  /// <para>
  /// <b>Graded on flux alone.</b>
  /// Burden is ore and flux; coke is charged as its own bands at the furnace
  /// (<c>docs/design/layered-charge.md</c>), so a coke band on the burden would be a second, disagreeing
  /// answer to "how much carbon is at the raceway". With fuel not on the item, iron is just
  /// <c>1 − flux</c> - so an iron band would be a second knob for one quantity, and the bands are
  /// flux-only.
  /// </para>
  /// <para>
  /// There is no derived low-flux floor either: <c>underfluxed</c> is an explicit band, and an
  /// inference would only ever shadow it.
  /// </para>
  /// </summary>
  public static string ProfileLangKey(BurdenMix mix)
  {
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
