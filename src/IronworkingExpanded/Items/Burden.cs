using System;
using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Common;

namespace IronworkingExpanded.Items;

/// <summary>
/// The composition of a <see cref="ItemBurden"/> stack: the relative parts of iron ore, flux
/// (crushed limestone) and fuel (the carbon reductant - coke, or charcoal at a worse carbon density)
/// it was mixed from. Stored as raw parts on the stack and read back as fractions, so splitting/merging
/// a stack preserves the per-unit proportions. The blast furnace reads these to scale heat /
/// iron-per-tick / slag; the mixer writes them from its inputs.
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
/// Read/write helpers and the (config-tunable) grade classifier for blast burden. Shared by the
/// burden item's tooltip, the mixer (writes the mix + names the target grade) and, later, the
/// blast furnace (reads the mix to drive smelting).
/// </summary>
public static class Burden
{
  private const string IronKey = "iron";
  private const string FluxKey = "flux";
  private const string FuelKey = "fuel";
  private const string LegacyFuelKey = "coke"; // pre-rename stacks stored the fuel part as "coke"

  /// <summary>Burden family for the blast furnace (ore + flux + coke). The default family, so every
  /// existing burden stack and every legacy blast mix keeps working with no migration.</summary>
  public const string FamilyOre = "ore";

  /// <summary>Burden family for the cupola (scrap metal + flux + coke, remelted to cast iron).</summary>
  public const string FamilyRemelt = "remelt";

  /// <summary>True when <paramref name="stack"/> is the ore burden item (<c>iwex:burden</c>) - the
  /// blast-furnace charge the ore bunker and the blast furnace gate on. Deliberately narrow: the ore
  /// bunker stores only this family, so broadening it would pool remelt burden into an ore bunker.</summary>
  public static bool Is([NotNullWhen(true)] ItemStack? stack) =>
    stack?.Collectible?.Code is { Domain: "iwex", Path: "burden" };

  /// <summary>True when <paramref name="stack"/> is the remelt burden item (<c>iwex:remeltburden</c>) -
  /// the cupola charge.</summary>
  public static bool IsRemelt([NotNullWhen(true)] ItemStack? stack) =>
    stack?.Collectible?.Code is { Domain: "iwex", Path: "remeltburden" };

  /// <summary>True for any prepared burden, either family. The furnace core / mixer recognise charge
  /// through this; the per-furnace family gate then decides which family a given furnace will convert.</summary>
  public static bool IsAny([NotNullWhen(true)] ItemStack? stack) =>
    Is(stack) || IsRemelt(stack);

  /// <summary>
  /// The burden family of a stack, derived from its <b>item identity</b> (which collectible) rather than
  /// a stamped attribute - so distinct items stay legible and can never accidentally merge piles. Remelt
  /// burden reads as <see cref="FamilyRemelt"/>; every other charge (ore burden, legacy blast mix) reads
  /// as <see cref="FamilyOre"/>, which is why an unstamped legacy world keeps loading as ore burden.
  /// </summary>
  public static string FamilyOf(ItemStack? stack) =>
    IsRemelt(stack) ? FamilyRemelt : FamilyOre;

  /// <summary>Stamps the mix parts onto a burden stack (any non-negative parts; read back as fractions).</summary>
  public static void Write(ItemStack stack, BurdenMix mix)
  {
    var a = stack.Attributes;
    a.SetFloat(IronKey, Math.Max(0f, mix.Iron));
    a.SetFloat(FluxKey, Math.Max(0f, mix.Flux));
    a.SetFloat(FuelKey, Math.Max(0f, mix.Fuel));
  }

  /// <summary>Reads the mix parts off a burden stack; an unstamped stack reads as empty. Falls back to
  /// the legacy <c>coke</c> attribute so burden stamped before the fuel rename still reads its carbon.</summary>
  public static BurdenMix Read(ItemStack? stack)
  {
    var a = stack?.Attributes;
    if (a == null)
      return default;
    return new BurdenMix(
      a.GetFloat(IronKey),
      a.GetFloat(FluxKey),
      a.HasAttribute(FuelKey) ? a.GetFloat(FuelKey) : a.GetFloat(LegacyFuelKey)
    );
  }

  /// <summary>
  /// Lang key of the named grade for a mix (used in the burden tooltip and the mixer block info).
  /// Returns the first <see cref="IwexConfig.BurdenProfiles"/> entry whose iron/flux/coke bands all
  /// contain the mix. A mix outside every band is <c>off-spec</c>; the common, fixable case - too
  /// little flux to slag, below every grade's flux floor - gets its own <c>lowflux</c> status so the
  /// player knows to add limestone rather than discard the batch. Both are reloadable into an empty mixer.
  /// </summary>
  public static string ProfileLangKey(BurdenMix mix)
  {
    if (!mix.HasContent)
      return "iwex:burden-profile-empty";

    // Track the lowest flux floor any grade demands while scanning for a match.
    float fluxFloor = float.MaxValue;
    foreach (BurdenProfile p in IwexValues.BurdenProfiles)
    {
      if (p.MinFlux < fluxFloor)
        fluxFloor = p.MinFlux;
      if (
        InBand(mix.IronFrac, p.MinIron, p.MaxIron)
        && InBand(mix.FluxFrac, p.MinFlux, p.MaxFlux)
        && InBand(mix.FuelFrac, p.MinFuel, p.MaxFuel)
      )
        return "iwex:burden-profile-" + p.Key;
    }

    // Off-spec. Single out a flux shortfall when every grade needs some flux and this mix is under it.
    if (fluxFloor > 0f && fluxFloor < float.MaxValue && mix.FluxFrac < fluxFloor)
      return "iwex:burden-profile-lowflux";

    return "iwex:burden-profile-offspec";
  }

  private static bool InBand(float value, float min, float max) =>
    value >= min && value <= max;
}
