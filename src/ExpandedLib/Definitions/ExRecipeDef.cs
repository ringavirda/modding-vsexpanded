using System;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// A code-first recipe FILE: the recipe-side sibling of <see cref="ExBlockDef"/> / <see cref="ExItemDef"/>.
/// One def maps to one <c>recipes/{category}/{assetName}.json</c> synthetic asset the survival recipe loaders
/// consume. A recipe file is EITHER a JSON array of recipes (most grid/smithing files) OR a single recipe
/// object (a one-recipe grid file, or a clayforming/barrel file) - the survival loaders accept both, and this
/// def mirrors whichever the source used so the injected JSON is byte-faithful:
/// <list type="bullet">
/// <item>ARRAY mode - call <see cref="Grid"/> / <see cref="Add"/> one or more times (accumulates);</item>
/// <item>SINGLE-OBJECT mode - call <see cref="GridObject"/> (a lone grid recipe) or <see cref="Body"/> (an
/// arbitrary single-object recipe, e.g. clayforming/barrel) exactly once.</item>
/// </list>
/// The two modes are mutually exclusive (mixing them throws).
/// <para>
/// The recipe loaders run well after the object loader (they resolve block/item codes, which only exist once
/// the object loader has built them), so the same server-only, before-patches injection timing that carries
/// blocktypes/itemtypes carries recipes too - and the loaders read the asset index origin-agnostically, so an
/// injected recipe file loads exactly like a file on disk.
/// </para>
/// <para>
/// Recipes are declarative and reference codes, so authoring is a thin, typed wrapper over the JSON via
/// <see cref="GridRecipeBuilder"/> (for grid recipes) or <see cref="Add"/> / <see cref="Body"/> (an arbitrary
/// recipe object, for the other recipe types until they get dedicated builders).
/// </para>
/// </summary>
public sealed class ExRecipeDef : IExDef
{
  private readonly string _domain;
  private readonly string _category;
  private readonly string _assetName;
  private JArray? _recipes;
  private JToken? _single;

  private ExRecipeDef(string domain, string category, string assetName)
  {
    _domain = domain;
    _category = category;
    _assetName = assetName;
  }

  /// <summary>Starts a recipe file of <paramref name="category"/> (<c>grid</c>/<c>smithing</c>/…) in
  /// <paramref name="domain"/> (the mod id), placed at <c>recipes/{category}/{assetName}.json</c>.</summary>
  public static ExRecipeDef Create(string domain, string category, string assetName) =>
    new(domain, category, assetName);

  /// <summary>The mod id / asset domain this recipe file belongs to.</summary>
  public string Domain => _domain;

  /// <summary>The recipe category (the <c>recipes/</c> sub-folder: <c>grid</c>, <c>smithing</c>, …).</summary>
  public string Category => _category;

  /// <summary>The file's base name (used as the def's code/key).</summary>
  public string Code => _assetName;

  /// <summary>The synthetic asset location the recipe loader keys on:
  /// <c>{domain}:recipes/{category}/{assetName}.json</c>.</summary>
  public AssetLocation Location =>
    new(_domain, "recipes/" + _category + "/" + _assetName + ".json");

  /// <summary>Appends one grid recipe (ARRAY mode) authored via <see cref="GridRecipeBuilder"/>. Order is
  /// preserved (recipe files are ordered arrays), so author in the source file's order for byte-for-byte
  /// parity.</summary>
  public ExRecipeDef Grid(Action<GridRecipeBuilder> configure)
  {
    var builder = new GridRecipeBuilder();
    configure(builder);
    Recipes().Add(builder.Build());
    return this;
  }

  /// <summary>Appends an arbitrary recipe object (ARRAY mode) - the escape hatch for a recipe type without a
  /// dedicated builder yet, or an entry the grid builder can't express.</summary>
  public ExRecipeDef Add(object recipe)
  {
    Recipes().Add(recipe as JToken ?? JToken.FromObject(recipe));
    return this;
  }

  /// <summary>Sets the file to a SINGLE grid recipe object (not wrapped in an array) authored via
  /// <see cref="GridRecipeBuilder"/> - for a one-recipe grid file the source authored as a lone object
  /// (e.g. the ore-bunker or molten-barrel recipe).</summary>
  public ExRecipeDef GridObject(Action<GridRecipeBuilder> configure)
  {
    var builder = new GridRecipeBuilder();
    configure(builder);
    return Body(builder.Build());
  }

  /// <summary>Sets the file to a SINGLE recipe object from a POCO/anonymous object/token - for a recipe type
  /// authored as a lone object (clayforming/barrel), or the escape hatch for one.</summary>
  public ExRecipeDef Body(object recipe)
  {
    if (_recipes is { Count: > 0 })
      throw new InvalidOperationException(
        "ExRecipeDef is already in array mode (Grid/Add was called); cannot also set a single-object body."
      );
    _single = recipe as JToken ?? JToken.FromObject(recipe);
    return this;
  }

  /// <summary>The number of recipe entries in the file (for tests/derivation).</summary>
  public int Count => _single != null ? 1 : _recipes?.Count ?? 0;

  /// <summary>The built recipe-file JSON (a defensive clone) - the array (or single object) the loader
  /// reads.</summary>
  public JToken ToJson() => (_single ?? (JToken?)_recipes ?? new JArray()).DeepClone();

  private JArray Recipes()
  {
    if (_single != null)
      throw new InvalidOperationException(
        "ExRecipeDef is already in single-object mode (GridObject/Body was called); cannot also append array entries."
      );
    return _recipes ??= new JArray();
  }
}
