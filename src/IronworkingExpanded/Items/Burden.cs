using System;
using Vintagestory.API.Common;

namespace IronworkingExpanded.Items;

/// <summary>
/// The composition of a <see cref="ItemBurden"/> stack: the relative parts of iron ore, flux
/// (crushed limestone) and coke it was mixed from. Stored as raw parts on the stack and read back
/// as fractions, so splitting/merging a stack preserves the per-unit proportions. The blast furnace
/// reads these to scale heat / iron-per-tick / slag; the mixer writes them from its inputs.
/// </summary>
public readonly record struct BurdenMix(float Iron, float Flux, float Coke)
{
  public float Sum => Iron + Flux + Coke;
  public bool HasContent => Sum > 0.0001f;

  public float IronFrac => HasContent ? Iron / Sum : 0f;
  public float FluxFrac => HasContent ? Flux / Sum : 0f;
  public float CokeFrac => HasContent ? Coke / Sum : 0f;
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
  private const string CokeKey = "coke";

  // STRAWMAN bands - tune freely (these will move to config). Grade is keyed on the coke fraction,
  // gated on a flux minimum for proper slag formation.
  public const float FluxFloor = 0.05f;
  public const float LowCokeMax = 0.15f;
  public const float HighCokeMin = 0.25f;

  /// <summary>Stamps the mix parts onto a burden stack (any non-negative parts; read back as fractions).</summary>
  public static void Write(ItemStack stack, BurdenMix mix)
  {
    var a = stack.Attributes;
    a.SetFloat(IronKey, Math.Max(0f, mix.Iron));
    a.SetFloat(FluxKey, Math.Max(0f, mix.Flux));
    a.SetFloat(CokeKey, Math.Max(0f, mix.Coke));
  }

  /// <summary>Reads the mix parts off a burden stack; an unstamped stack reads as empty.</summary>
  public static BurdenMix Read(ItemStack? stack)
  {
    var a = stack?.Attributes;
    if (a == null)
      return default;
    return new BurdenMix(
      a.GetFloat(IronKey),
      a.GetFloat(FluxKey),
      a.GetFloat(CokeKey)
    );
  }

  /// <summary>Lang key of the named grade for a mix (used in tooltips and the mixer block info).</summary>
  public static string ProfileLangKey(BurdenMix mix)
  {
    if (!mix.HasContent)
      return "iwex:burden-profile-empty";
    if (mix.FluxFrac < FluxFloor)
      return "iwex:burden-profile-unfluxed";
    if (mix.CokeFrac <= LowCokeMax)
      return "iwex:burden-profile-lowcoke";
    if (mix.CokeFrac >= HighCokeMin)
      return "iwex:burden-profile-highcoke";
    return "iwex:burden-profile-standard";
  }
}
