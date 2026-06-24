using ExpandedLib.Registries.Config;
using Vintagestory.API.Common;

namespace IronworkingExpanded;

/// <summary>
/// JSON-serializable gameplay tunables for Ironworking Expanded (iwex) - the "magic numbers" that
/// balance the molten-metal network (canals, taps, barrels, mold pedestals) that this foundational
/// mod owns. Loaded from (and written to) <c>ModConfig/iwex_values.json</c>; the property defaults
/// below are used when the file is missing or a key is absent (and any NaN/infinite/negative value
/// is reset to its default on load). Accessed through <see cref="IwexValues"/>, not directly.
/// </summary>
[ExConfigRegister("iwex_values.json", "iwex", Manageable = true)]
public class IwexConfig : IExVersionedConfig
{
  /// <summary>Mod version that last wrote this file; drives the <see cref="Migrations"/> resets.
  /// Managed by <see cref="ExConfigRegister{TConfig}"/> - do not set by hand.</summary>
  public string? ConfigVersion { get; set; }

  /// <summary>Version-driven default resets (none yet).</summary>
  public static readonly ExConfigMigration[] Migrations = [];

  #region Molten system
  /// <summary>Temperature cooldown speed applied to molten-metal stacks held by the molten system (canal cells, taps, barrels, molds, the bessemer charge).</summary>
  public float MoltenCooldownSpeed { get; set; } = 24f;

  /// <summary>Multiplier on <see cref="MoltenCooldownSpeed"/> for metal stored in a standalone molten
  /// barrel. Below 1 the barrel holds its heat longer; 1 = the base molten rate. Applied live to metal already in the barrel.</summary>
  public float BarrelCooldownCoefficient { get; set; } = 1f;

  /// <summary>Multiplier on <see cref="MoltenCooldownSpeed"/> for metal cast in a mold parked under a
  /// canal tap. Below 1 the cast holds its heat longer; 1 = the base molten rate. Applied live.</summary>
  public float TapMoldCooldownCoefficient { get; set; } = 1f;

  /// <summary>Multiplier on <see cref="MoltenCooldownSpeed"/> for metal cast in a mold on a pedestal.
  /// Below 1 the cast holds its heat longer; 1 = the base molten rate. Applied live.</summary>
  public float MoldPedestalCooldownCoefficient { get; set; } = 1f;

  /// <summary>Max metal (units) flowing across one canal connection per second; balance against <see cref="MoltenCooldownSpeed"/>.</summary>
  public int MoltenFlowRate { get; set; } = 50;

  /// <summary>Minimum metal (units) that must move across a canal connection for any flow that tick (stops sub-unit dribbles).</summary>
  public int MoltenMinFlowAmount { get; set; } = 10;

  /// <summary>Default per-canal-block capacity (units) when a block sets no <c>maxUnits</c> attribute.</summary>
  public int CanalDefaultUnitCapacity { get; set; } = 50;

  /// <summary>Default canal-tap network drain speed (units/s) when no <c>drainSpeed</c> attribute is set.</summary>
  public float CanalDefaultDrainSpeed { get; set; } = 20f;

  /// <summary>Default large-mold capacity (units) when the mold sets no <c>requiredUnits</c> attribute.</summary>
  public int MoldDefaultUnits { get; set; } = 100;

  /// <summary>Default molten-barrel capacity (units) when no <c>maxUnits</c> attribute is set.</summary>
  public int BarrelDefaultMaxUnits { get; set; } = 800;

  /// <summary>Fire-clay consumed to seal a straight canal into a separator.</summary>
  public int CanalSealClayCost { get; set; } = 4;

  /// <summary>Fire-clay refunded when breaking a canal seal.</summary>
  public int CanalUnsealClayRefund { get; set; } = 2;
  #endregion
}
