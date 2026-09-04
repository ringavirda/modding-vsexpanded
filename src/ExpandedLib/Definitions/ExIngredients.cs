using System;

namespace ExpandedLib.Definitions;

/// <summary>
/// Vanilla crafting ingredients shared across the mods' code-first recipe files. Each entry is an
/// <see cref="IngredientBuilder"/> factory in the shape <see cref="GridRecipeBuilder.Ingredient"/>
/// accepts: after a <c>using static ExpandedLib.Definitions.ExIngredients;</c> a provider passes either
/// a method group (<c>.Ingredient("H", Hammer)</c>) or a quantity factory
/// (<c>.Ingredient("P", Plate(1))</c>). Only mod-agnostic <c>game:</c> ingredients belong here; a mod's
/// own codes stay in its own provider.
/// </summary>
public static class ExIngredients {
  /// <summary>A hammer as a tool (consumed by durability, not stack): <c>game:hammer-*</c>, <c>isTool</c>.</summary>
  public static IngredientBuilder Hammer(IngredientBuilder i) =>
    i.Item("game:hammer-*").Tool();

  /// <summary>A chisel as a tool: <c>game:chisel-*</c>, <c>isTool</c>.</summary>
  public static IngredientBuilder Chisel(IngredientBuilder i) =>
    i.Item("game:chisel-*").Tool();

  /// <summary>An iron/steel metal plate (metal capture): <c>game:metalplate-*</c> named <c>metal</c>.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> Plate(int qty) =>
    i => i.Item("game:metalplate-*").Metal().Quantity(qty);

  /// <summary>A steel-only metal plate: <c>game:metalplate-steel</c>.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> PlateSteel(
    int qty
  ) => i => i.Item("game:metalplate-steel").Quantity(qty);

  /// <summary>Iron/steel nails &amp; strips (metal capture): <c>game:metalnailsandstrips-*</c>.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> Nails(int qty) =>
    i => i.Item("game:metalnailsandstrips-*").Metal().Quantity(qty);

  /// <summary>Steel-only nails &amp; strips: <c>game:metalnailsandstrips-steel</c>.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> NailsSteel(
    int qty
  ) => i => i.Item("game:metalnailsandstrips-steel").Quantity(qty);

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
  /// (<c>iiex:gear-*</c>). A recipe that accepts either kind emits one entry per gear code.</summary>
  public static Func<IngredientBuilder, IngredientBuilder> Gear(
    string code,
    int qty
  ) => i => i.Item(code).Quantity(qty);
}
