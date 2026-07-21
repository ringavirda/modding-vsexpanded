using System.Collections.Generic;

namespace ExpandedLib.Materials;

/// <summary>
/// One material-role assignment - the data a machine reads instead of the hardcoded
/// <c>Collectible.Code.Path</c> string equality that used to classify a flux / fuel / ore / scrap /
/// charge item inline in the mixer, hoppers and furnace core. Deserialized from every domain's
/// <c>config/materialroles.json</c> and registered into <see cref="MaterialRoleRegistry"/>, so a
/// content mod (or EM) gives an item a role by shipping/patching one JSON entry - no recompile.
/// <para>
/// A def matches an item by <see cref="Code"/> (exact, domain-normalised like
/// <see cref="Metals.MetalRegistry"/>) <b>or</b> <see cref="PathPrefix"/> (a
/// <c>Code.Path.StartsWith</c> test, domain-blind - it reproduces the old
/// <c>path.StartsWith("crushed-iron")</c> ore check). At least one of the two must be set.
/// </para>
/// </summary>
public class MaterialRoleDef
{
  /// <summary>The role this def grants (one of the <see cref="Roles"/> constants: flux/fuel/ore/...).</summary>
  public string Role { get; set; } = "";

  /// <summary>Exact item code to match ("game:lime"), domain-normalised. Null → match by
  /// <see cref="PathPrefix"/> only.</summary>
  public string? Code { get; set; }

  /// <summary>Match any item whose <c>Code.Path</c> starts with this (domain-blind), reproducing the
  /// old crushed-ore prefix test. Null → match by <see cref="Code"/> only.</summary>
  public string? PathPrefix { get; set; }

  /// <summary>Per-role scalar - the fuel (carbon) value of one item for the fuel role (coke 2,
  /// charcoal 0.5). Null → <see cref="MaterialRoleRegistry.ValueOf"/> returns the caller's fallback (1).</summary>
  public float? Value { get; set; }
}

/// <summary>The <c>config/materialroles.json</c> file shape: a wrapper carrying the role entries
/// (mirrors <see cref="Fluids.LiquidCatalogue"/> - one file with a <c>materials</c> array).</summary>
public class MaterialRoleCatalogue
{
  /// <summary>The role assignments this file contributes.</summary>
  public List<MaterialRoleDef>? Materials { get; set; }
}

/// <summary>
/// The canonical material-role tokens the machines classify by. Kept as constants (not an enum) so the
/// JSON <see cref="MaterialRoleDef.Role"/> strings and the code call sites share one spelling, and a
/// mod can invent a further role by simply shipping the string.
/// </summary>
public static class Roles
{
  /// <summary>Flux - lime and the like (the mixer's / hopper's flux slot).</summary>
  public const string Flux = "flux";

  /// <summary>Fuel - a carbon reductant (coke, charcoal); carries a per-item fuel <see cref="MaterialRoleDef.Value"/>.</summary>
  public const string Fuel = "fuel";

  /// <summary>Iron ore, crushed - the ore-family primary charge (the <c>crushed-iron</c> prefix + mod ores).</summary>
  public const string IronOre = "ironore";

  /// <summary>Scrap metal - vanilla iron bits (the remelt-family primary charge).</summary>
  public const string Scrap = "scrap";

  /// <summary>Prepared furnace charge - blast mix (the legacy count-only shaft charge).</summary>
  public const string Charge = "charge";
}
