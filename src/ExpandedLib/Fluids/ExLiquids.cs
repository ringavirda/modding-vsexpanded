using System;
using System.Collections.Generic;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace ExpandedLib.Fluids;

/// <summary>
/// Process-wide catalogue of pipe/canal media (<see cref="LiquidDef"/>) and the single
/// <see cref="IMediumTaxonomy"/> the pipe network reads. Mirrors <see cref="Metals.MetalRegistry"/>:
/// populated at <c>AssetsFinalize</c> from every domain's <c>config/liquids.json</c> via the shared
/// <see cref="AssetCatalogueLoader"/>, over a compiled-in baseline.
/// <para>
/// The four built-ins (Air / Steam / Exhaust / Water) are <b>always</b> seeded - in the static
/// constructor and again at the start of every <see cref="Load"/> - so a bare-constructed network in
/// the headless harness (which runs no asset load) and a mis-ordered <c>AssetsFinalize</c> never leave
/// a run with an unknown medium. Their semantics reproduce the old <c>PipeNetworkState</c> string
/// helpers exactly, so nothing regresses until a mod deliberately adds or overrides a medium.
/// </para>
/// </summary>
public static class ExLiquids
{
  private static readonly ExKeyedRegistry<LiquidDef> _defs = new(d => d.Code);

  static ExLiquids() => SeedDefaults();

  /// <summary>The medium policy the pipe network consults (over the current registry contents).</summary>
  public static IMediumTaxonomy Taxonomy { get; } = new MediumTaxonomy();

  /// <summary>Registers (or replaces) a medium by its <see cref="LiquidDef.Code"/>.</summary>
  public static void Register(LiquidDef def) => _defs.Register(def);

  /// <summary>Looks up a medium by code (case-insensitive).</summary>
  public static bool TryGet(string code, out LiquidDef def) =>
    _defs.TryGet(code, out def);

  /// <summary>Every registered medium.</summary>
  public static IReadOnlyCollection<LiquidDef> All => _defs.Values;

  /// <summary>Drops every registered medium (the loader clears before re-seeding + overlaying).</summary>
  public static void Clear() => _defs.Clear();

  /// <summary>Registers the four built-in media with their historical semantics. Idempotent.</summary>
  public static void SeedDefaults()
  {
    Register(new LiquidDef { Code = "Air", Phase = LiquidPhase.Gas, Priority = 0 });
    Register(
      new LiquidDef
      {
        Code = "Steam",
        Phase = LiquidPhase.Gas,
        Priority = 10,
        CondensesTo = "Water",
        CondenseBelowC = 100f,
      }
    );
    Register(
      new LiquidDef { Code = "Exhaust", Phase = LiquidPhase.Gas, Priority = 20 }
    );
    Register(
      new LiquidDef
      {
        Code = "Water",
        Phase = LiquidPhase.Liquid,
        Priority = 0,
        VaporisesTo = "Steam",
        BoilPointC = 100f,
      }
    );
  }

  /// <summary>Re-seeds the built-ins and overlays every domain's <c>config/liquids.json</c>. Call from
  /// <c>ExpandedLibModSystem.AssetsFinalize</c>.</summary>
  public static void Load(ICoreAPI api)
  {
    Clear();
    SeedDefaults();
    foreach (
      LiquidCatalogue cat in AssetCatalogueLoader.GetMany<LiquidCatalogue>(
        api,
        "config/liquids.json"
      )
    )
    {
      if (cat.Liquids == null)
        continue;
      foreach (LiquidDef def in cat.Liquids)
      {
        if (string.IsNullOrEmpty(def.Code))
        {
          api.Logger.Warning("[exlib] Skipping liquid def with no code");
          continue;
        }
        Register(def);
      }
    }
  }

  // The taxonomy over the current registry contents. All comparisons are code-based and route through
  // the registry, so a case mismatch or unknown code degrades to "gas / not-liquid" rather than throwing.
  private sealed class MediumTaxonomy : IMediumTaxonomy
  {
    public bool IsLiquid(string code) =>
      _defs.TryGet(code, out LiquidDef d) && d.Phase == LiquidPhase.Liquid;

    public bool Compatible(string current, string medium)
    {
      if (string.IsNullOrEmpty(current))
        return true; // an unclaimed run accepts any medium
      bool curLiquid = IsLiquid(current);
      if (curLiquid != IsLiquid(medium))
        return false; // gas and liquid never mix
      if (!curLiquid)
        return true; // two gases always mix (Air/Steam/Exhaust family)
      return string.Equals(current, medium, StringComparison.OrdinalIgnoreCase); // liquids: same only
    }

    public string HigherPriority(string a, string b) =>
      PriorityOf(b) > PriorityOf(a) ? b : a;

    public bool CondensationTarget(
      string code,
      out string target,
      out float volumeFactor
    )
    {
      target = "";
      volumeFactor = 0f;
      if (
        !_defs.TryGet(code, out LiquidDef d)
        || string.IsNullOrEmpty(d.CondensesTo)
      )
        return false;
      target = d.CondensesTo!;
      volumeFactor = d.CondenseVolumeFactor ?? 0f;
      return true;
    }

    public bool VaporisationTarget(
      string code,
      out string target,
      out float volumeFactor
    )
    {
      target = "";
      volumeFactor = 0f;
      if (
        !_defs.TryGet(code, out LiquidDef d)
        || string.IsNullOrEmpty(d.VaporisesTo)
      )
        return false;
      target = d.VaporisesTo!;
      volumeFactor = d.VaporiseVolumeFactor ?? 0f;
      return true;
    }

    // Passive condensation: the temp-independent pair, gated below the dew point.
    public bool TryCondensation(
      string code,
      float tempC,
      out string target,
      out float volumeFactor
    )
    {
      if (!CondensationTarget(code, out target, out volumeFactor))
        return false;
      if (
        _defs.TryGet(code, out LiquidDef d)
        && d.CondenseBelowC.HasValue
        && tempC >= d.CondenseBelowC.Value
      )
      {
        target = "";
        volumeFactor = 0f;
        return false;
      }
      return true;
    }

    // Passive vaporisation: the mirror of TryCondensation, gated at/above the boil point.
    public bool TryVaporisation(
      string code,
      float tempC,
      out string target,
      out float volumeFactor
    )
    {
      if (!VaporisationTarget(code, out target, out volumeFactor))
        return false;
      if (
        _defs.TryGet(code, out LiquidDef d)
        && d.BoilPointC.HasValue
        && tempC < d.BoilPointC.Value
      )
      {
        target = "";
        volumeFactor = 0f;
        return false;
      }
      return true;
    }

    private static int PriorityOf(string code) =>
      _defs.TryGet(code, out LiquidDef d) ? d.Priority : 0;
  }
}
