using ExpandedLib.Registries.Config;

namespace ExpandedLib;

/// <summary>
/// JSON-serializable gameplay tunables owned by the shared library (<c>exlib</c>). Loaded from (and
/// written to) <c>ModConfig/exlib_values.json</c>; the property defaults below apply when the file is
/// missing or a key is absent (and any NaN/infinite/negative value is reset to its default on load).
/// Accessed through the generated <c>ExlibValues</c> accessor, not directly.
/// <para>
/// These are the constants the framework's own code reads - chiefly the block-network layer
/// (<see cref="Blocks.Networks.BlockNetwork"/> subclasses live here now, so their tunables do too).
/// Content-specific numbers stay in each mod's own config (<c>ppex_values.json</c> etc.). All
/// gas/liquid volumes are in <b>litres</b>; molten flow is in canal <b>units</b>.
/// </para>
/// </summary>
[ExConfigRegister(
  "ex_values.json",
  "exlib",
  LegacyFileNames = new string[] { "exlib_values.json" },
  Manageable = true
)]
public class ExlibConfig : IExVersionedConfig
{
  /// <summary>Mod version that last wrote this file. Managed by the config store - do not set by hand.</summary>
  public string? ConfigVersion { get; set; }

  #region Pipe network
  /// <summary>Litres a single pipe holds at 1 atm (both the gas and water pools). A run's capacity is
  /// this times its node count.</summary>
  [ExConfigRange(1, 1_000_000)] // pipe capacity divides pressure - must stay positive
  public float LitresPerPipe { get; set; } = 30f;

  /// <summary>Gas (L/s) bled per open-ended pipe connector (leak) - only the volume above the
  /// network's 1 atm capacity is vented, so a leaking run can never build pressure.</summary>
  public float GasLeakRate { get; set; } = 8.0f;

  /// <summary>Liquid (L/s) drained from the network per open-ended pipe connector (leak).</summary>
  public float LiquidLeakRate { get; set; } = 10.0f;

  /// <summary>Water (L) lost to natural evaporation per in-game day (pipe water pool and the boiler
  /// pool that reuses this rate). 100 L over 2 days = 50 L/day.</summary>
  public float EvaporationLitresPerDay { get; set; } = 50f;

  /// <summary>Seconds a pipe run may sit at its weakest pipe's burst pressure (nowhere to vent)
  /// before a pipe lets go - mirrors the boiler over-pressure grace.</summary>
  public float PipeOverpressureSeconds { get; set; } = 30f;
  #endregion

  #region Molten network
  /// <summary>Max metal (units) flowing across one canal connection per second.</summary>
  public int MoltenFlowRate { get; set; } = 50;

  /// <summary>Minimum metal (units) that must move across a canal connection for any flow that tick
  /// (stops sub-unit dribbles).</summary>
  public int MoltenMinFlowAmount { get; set; } = 10;
  #endregion
}
