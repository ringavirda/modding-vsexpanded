using System;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// Code-first recipe file, the recipe-side sibling of <see cref="ExBlockDef"/> and <see cref="ExItemDef"/>.
/// One def produces one <c>recipes/{category}/{assetName}.json</c> synthetic asset; the survival recipe
/// loaders read the asset index origin-agnostically, so an injected file loads like a file on disk. A recipe
/// file is either an array of recipes (<see cref="Grid"/> / <see cref="Add"/>, callable repeatedly) or a
/// single recipe object (<see cref="GridObject"/> or <see cref="Body"/>, callable once), and the def mirrors
/// whichever the source used. The two modes are mutually exclusive; mixing them throws.
/// </summary>
public sealed class ExRecipeDef : IExDef {
  private readonly string _domain;
  private readonly string _category;
  private readonly string _assetName;
  private JArray? _recipes;
  private JToken? _single;

  private ExRecipeDef(string domain, string category, string assetName) {
    _domain = domain;
    _category = category;
    _assetName = assetName;
  }

  /// <summary>Starts a recipe file of <paramref name="category"/> (<c>grid</c>/<c>smithing</c>/…) in
  /// <paramref name="domain"/> (the mod id), placed at <c>recipes/{category}/{assetName}.json</c>.</summary>
  public static ExRecipeDef Create(
    string domain,
    string category,
    string assetName
  ) => new(domain, category, assetName);

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

  /// <summary>Appends one grid recipe (array mode) authored via <see cref="GridRecipeBuilder"/>. Recipe files
  /// are ordered arrays and call order is preserved.</summary>
  public ExRecipeDef Grid(Action<GridRecipeBuilder> configure) {
    var builder = new GridRecipeBuilder();
    configure(builder);
    Recipes().Add(builder.Build());
    return this;
  }

  /// <summary>Appends an arbitrary recipe object (array mode), for a recipe type without a dedicated builder
  /// or an entry the grid builder cannot express.</summary>
  public ExRecipeDef Add(object recipe) {
    Recipes().Add(recipe as JToken ?? JToken.FromObject(recipe));
    return this;
  }

  /// <summary>Sets the file to a single grid recipe object, not wrapped in an array, authored via
  /// <see cref="GridRecipeBuilder"/>.</summary>
  public ExRecipeDef GridObject(Action<GridRecipeBuilder> configure) {
    var builder = new GridRecipeBuilder();
    configure(builder);
    return Body(builder.Build());
  }

  /// <summary>Sets the file to a single recipe object from a POCO/anonymous object/token, for a recipe type
  /// authored as a lone object (clayforming, barrel).</summary>
  /// <exception cref="InvalidOperationException">The def is already in array mode.</exception>
  public ExRecipeDef Body(object recipe) {
    if (_recipes is { Count: > 0 })
      throw new InvalidOperationException(
        "ExRecipeDef is already in array mode (Grid/Add was called); cannot also set a single-object body."
      );
    _single = recipe as JToken ?? JToken.FromObject(recipe);
    return this;
  }

  /// <summary>The number of recipe entries in the file; 1 in single-object mode.</summary>
  public int Count => _single != null ? 1 : _recipes?.Count ?? 0;

  /// <summary>The built recipe-file JSON the loader reads - the array, or the single object. A defensive
  /// clone.</summary>
  public JToken ToJson() =>
    (_single ?? (JToken?)_recipes ?? new JArray()).DeepClone();

  private JArray Recipes() {
    if (_single != null)
      throw new InvalidOperationException(
        "ExRecipeDef is already in single-object mode (GridObject/Body was called); cannot also append array entries."
      );
    return _recipes ??= new JArray();
  }
}
