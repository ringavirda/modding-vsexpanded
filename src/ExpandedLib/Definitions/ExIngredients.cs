using System;

namespace ExpandedLib.Definitions;

/// <summary>
/// The catalogue of vanilla crafting ingredients that recur across every mod's code-first recipe files -
/// authored ONCE here instead of re-declared as a private factory in each provider (the <c>Hammer</c>/
/// <c>Plate</c>/<c>Nails</c>/<c>Rod</c>/<c>Gear</c> trio was copy-pasted across the ppex/iwex/smex recipe
/// files). Each entry is an <see cref="IngredientBuilder"/> factory in the exact shape
/// <see cref="GridRecipeBuilder.Ingredient"/> accepts, so a provider references it by name after a
/// <c>using static ExpandedLib.Definitions.ExIngredients;</c> - e.g. <c>.Ingredient("H", Hammer)</c> (a method
/// group) or <c>.Ingredient("P", Plate(1))</c> (a quantity factory).
/// <para>
/// Only genuinely shared, mod-agnostic <c>game:</c> ingredients live here; a mod's own codes (a pipe segment,
/// slag, a refractory-brick or coloured-brick capture) stay local to its provider, because they encode that
/// mod's domain or a per-file wildcard quirk.
/// </para>
/// </summary>
public static class ExIngredients
{
  /// <summary>A hammer as a tool (consumed by durability, not stack): <c>game:hammer-*</c>, <c>isTool</c>.</summary>
  public static IngredientBuilder Hammer(IngredientBuilder i) => i.Item("game:hammer-*").Tool();

  /// <summary>A chisel as a tool: <c>game:chisel-*</c>, <c>isTool</c>.</summary>
  public static IngredientBuilder Chisel(IngredientBuilder i) => i.Item("game:chisel-*").Tool();

  /// <summary>An iron/steel metal plate (metal capture): <c>game:metalplate-*</c> named <c>metal</c>.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> Plate(int qty) =>
    i => i.Item("game:metalplate-*").Metal().Quantity(qty);

  /// <summary>A steel-only metal plate: <c>game:metalplate-steel</c>.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> PlateSteel(int qty) =>
    i => i.Item("game:metalplate-steel").Quantity(qty);

  /// <summary>Iron/steel nails &amp; strips (metal capture): <c>game:metalnailsandstrips-*</c>.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> Nails(int qty) =>
    i => i.Item("game:metalnailsandstrips-*").Metal().Quantity(qty);

  /// <summary>Steel-only nails &amp; strips: <c>game:metalnailsandstrips-steel</c>.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> NailsSteel(int qty) =>
    i => i.Item("game:metalnailsandstrips-steel").Quantity(qty);

  /// <summary>An iron/steel metal rod (metal capture): <c>game:rod-*</c>.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> Rod(int qty) =>
    i => i.Item("game:rod-*").Metal().Quantity(qty);

  /// <summary>A steel-only metal rod: <c>game:rod-steel</c>.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> RodSteel(int qty) =>
    i => i.Item("game:rod-steel").Quantity(qty);

  /// <summary>Fire clay: <c>game:clay-fire</c>.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> FireClay(int qty) =>
    i => i.Item("game:clay-fire").Quantity(qty);

  /// <summary>A gear by explicit code - vanilla rusty (<c>game:gear-rusty</c>) or a mod's craftable gear
  /// (<c>ppex:gear-*</c>). A recipe that accepts either kind emits one entry per gear code.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> Gear(string code, int qty) =>
    i => i.Item(code).Quantity(qty);
}
