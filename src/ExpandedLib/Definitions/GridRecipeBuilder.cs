using System;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Definitions;

/// <summary>
/// Fluent builder for one grid (crafting-table) recipe object - the entries of a
/// <c>recipes/grid/*.json</c> file. Emits the exact schema the survival grid-recipe loader reads:
/// <c>{ name, ingredientPattern, ingredients{ key: {…} }, width, height, output }</c>. Only keys that are
/// set are emitted, so an optional field (output quantity, an ingredient's name/allowedVariants/quantity/tool)
/// is absent unless authored - matching the hand-written form for parity.
/// </summary>
public sealed class GridRecipeBuilder
{
  private readonly JObject _root = new();
  private readonly JObject _ingredients = new();

  /// <summary>Sets the recipe's display <c>name</c> (also used to disambiguate handbook entries).</summary>
  public GridRecipeBuilder Name(string name)
  {
    _root["name"] = name;
    return this;
  }

  /// <summary>Sets the <c>ingredientPattern</c> (comma-separated rows of single-char slot keys, e.g.
  /// <c>"_H_,PIP,NPN"</c>; <c>_</c> is an empty slot).</summary>
  public GridRecipeBuilder Pattern(string pattern)
  {
    _root["ingredientPattern"] = pattern;
    return this;
  }

  /// <summary>Sets the grid <c>width</c> and <c>height</c> (the pattern's column and row counts).</summary>
  public GridRecipeBuilder Size(int width, int height)
  {
    _root["width"] = width;
    _root["height"] = height;
    return this;
  }

  /// <summary>Adds one <c>ingredients.{key}</c> entry authored via <see cref="IngredientBuilder"/> - the
  /// <paramref name="key"/> is the single-char slot letter used in the pattern. The config returns the builder
  /// (fluent), so a reusable ingredient factory can be passed by name: a mod defines its common ingredients
  /// once (e.g. <c>Ingredient("H", Hammer)</c> where <c>Hammer</c> is an
  /// <c>IngredientBuilder -&gt; IngredientBuilder</c>) instead of repeating the same code/quantity everywhere.</summary>
  public GridRecipeBuilder Ingredient(string key, Func<IngredientBuilder, IngredientBuilder> configure)
  {
    var builder = new IngredientBuilder();
    configure(builder);
    _ingredients[key] = builder.Build();
    return this;
  }

  /// <summary>Sets the <c>output</c> stack: <c>{ type, code[, quantity] }</c>. <paramref name="quantity"/> is
  /// emitted only when supplied (absent == 1, matching the schema default).</summary>
  public GridRecipeBuilder Output(string type, string code, int? quantity = null)
  {
    var output = new JObject { ["type"] = type, ["code"] = code };
    if (quantity.HasValue)
      output["quantity"] = quantity.Value;
    _root["output"] = output;
    return this;
  }

  /// <summary>Shorthand for a block output (<c>type = "block"</c>).</summary>
  public GridRecipeBuilder OutputBlock(string code, int? quantity = null) =>
    Output("block", code, quantity);

  /// <summary>Shorthand for an item output (<c>type = "item"</c>).</summary>
  public GridRecipeBuilder OutputItem(string code, int? quantity = null) =>
    Output("item", code, quantity);

  /// <summary>Sets an arbitrary top-level key on the recipe - the escape hatch for a grid-recipe field
  /// without a dedicated method (e.g. <c>recipeGroup</c>, <c>shapeless</c>).</summary>
  public GridRecipeBuilder Raw(string key, object value)
  {
    _root[key] = value as JToken ?? JToken.FromObject(value);
    return this;
  }

  internal JObject Build()
  {
    _root["ingredients"] = _ingredients;
    return _root;
  }
}

/// <summary>
/// Fluent builder for one recipe ingredient (a slot in a grid recipe, or a barrel/other recipe ingredient):
/// <c>{ type, code[, name, allowedVariants, quantity, isTool] }</c>. Only set keys are emitted.
/// </summary>
public sealed class IngredientBuilder
{
  private readonly JObject _root = new();

  /// <summary>Sets <c>type = "item"</c> and the item <c>code</c> (may be a wildcard like
  /// <c>game:metalplate-*</c>).</summary>
  public IngredientBuilder Item(string code)
  {
    _root["type"] = "item";
    _root["code"] = code;
    return this;
  }

  /// <summary>Sets <c>type = "block"</c> and the block <c>code</c>.</summary>
  public IngredientBuilder Block(string code)
  {
    _root["type"] = "block";
    _root["code"] = code;
    return this;
  }

  /// <summary>Sets the wildcard capture <c>name</c> and its <c>allowedVariants</c> - binds a <c>*</c> in the
  /// code to a named group so the output can reuse it (e.g. name <c>metal</c> over <c>[iron, steel]</c>).</summary>
  public IngredientBuilder Named(string name, params string[] allowedVariants)
  {
    _root["name"] = name;
    _root["allowedVariants"] = new JArray(allowedVariants);
    return this;
  }

  /// <summary>Shorthand for the ubiquitous iron/steel metal capture: <c>Named("metal", "iron", "steel")</c>.</summary>
  public IngredientBuilder Metal() => Named("metal", "iron", "steel");

  /// <summary>Sets the required <c>quantity</c>.</summary>
  public IngredientBuilder Quantity(int quantity)
  {
    _root["quantity"] = quantity;
    return this;
  }

  /// <summary>Marks the ingredient a tool (<c>isTool = true</c>) - consumed by durability, not by stack.</summary>
  public IngredientBuilder Tool()
  {
    _root["isTool"] = true;
    return this;
  }

  /// <summary>Sets an arbitrary ingredient key (e.g. <c>litres</c> for a barrel ingredient, or
  /// <c>toolDurabilityCost</c>) - the escape hatch.</summary>
  public IngredientBuilder Raw(string key, object value)
  {
    _root[key] = value as JToken ?? JToken.FromObject(value);
    return this;
  }

  internal JObject Build() => _root;
}
