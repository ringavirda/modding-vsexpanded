using System;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Definitions;

/// <summary>
/// Typed builder for the <c>ExRightClickConstructable</c> behavior's <c>stages</c> table. Each stage
/// adds or removes shape elements and optionally requires materials; the builder emits the
/// <c>{ "stages": [...] }</c> properties object the vanilla behavior consumes.
/// </summary>
public sealed class ConstructionStages {
  private readonly JArray _stages = new();
  private float? _brokenDropsRatio;

  /// <summary>Appends one build stage configured through <paramref name="configure"/> (its required
  /// materials and the shape elements it reveals). Stage order is the build order.</summary>
  public ConstructionStages Stage(Action<ConstructionStage> configure) {
    var stage = new ConstructionStage();
    configure(stage);
    _stages.Add(stage.Build());
    return this;
  }

  /// <summary>Sets the top-level <c>brokenDropsRatio</c> (the salvage fraction returned when the finished
  /// structure is broken). Omit to leave it at the behavior default.</summary>
  public ConstructionStages BrokenDropsRatio(float ratio) {
    _brokenDropsRatio = ratio;
    return this;
  }

  internal JObject Build() {
    var properties = new JObject { ["stages"] = _stages };
    if (_brokenDropsRatio.HasValue)
      properties["brokenDropsRatio"] = _brokenDropsRatio.Value;
    return properties;
  }
}

/// <summary>One construction stage: the shape elements it adds/removes and the materials it requires.</summary>
public sealed class ConstructionStage {
  private readonly JObject _stage = new();
  private JArray? _requireStacks;

  /// <summary>The shape elements this stage reveals (each becomes an <c>elem/*</c> selector). Sets
  /// <c>addElements</c>.</summary>
  public ConstructionStage AddElements(params string[] elements) {
    _stage["addElements"] = new JArray(elements);
    return this;
  }

  /// <summary>The shape elements this stage hides again. Sets <c>removeElements</c>.</summary>
  public ConstructionStage RemoveElements(params string[] elements) {
    _stage["removeElements"] = new JArray(elements);
    return this;
  }

  /// <summary>
  /// Requires <paramref name="quantity"/> of an ingredient to advance this stage. <paramref name="code"/>
  /// may carry a variant placeholder (e.g. <c>game:burnedbrick-{brick}</c>); <paramref name="name"/> is the
  /// lang key shown in the "missing X" hint; <paramref name="type"/> is <c>item</c> or <c>block</c>;
  /// <paramref name="storeWildCard"/> records the chosen variant so later stages/drops resolve to the same
  /// one. Accumulates across calls (a stage may need several materials).
  /// </summary>
  public ConstructionStage Require(
    string code,
    int quantity,
    string? name = null,
    string type = "item",
    string? storeWildCard = null,
    string[]? allowedVariants = null
  ) {
    var ingredient = new JObject {
      ["type"] = type,
      ["code"] = code,
      ["quantity"] = quantity,
    };
    if (name != null)
      ingredient["name"] = name;
    if (allowedVariants != null)
      ingredient["allowedVariants"] = new JArray(allowedVariants);
    if (storeWildCard != null)
      ingredient["storeWildCard"] = storeWildCard;

    _requireStacks ??= new JArray();
    _requireStacks.Add(ingredient);
    _stage["requireStacks"] = _requireStacks;
    return this;
  }

  /// <summary>Requires iron or steel metal plate (<c>metalplate-*</c>, stored under the <c>metal</c>
  /// wildcard so later stages and drops resolve to the same metal). Hint key
  /// <c>{domain}:rcc-ingredient-metalplate</c>.</summary>
  public ConstructionStage RequireMetalPlate(string domain, int quantity) =>
    RequireMetal(domain, "metalplate-*", "metalplate", quantity);

  /// <summary>Requires iron or steel nails and strips (<c>metalnailsandstrips-*</c>, stored under the
  /// <c>metal</c> wildcard). Hint key <c>{domain}:rcc-ingredient-nailsandstrips</c>.</summary>
  public ConstructionStage RequireMetalNails(string domain, int quantity) =>
    RequireMetal(domain, "metalnailsandstrips-*", "nailsandstrips", quantity);

  /// <summary>Requires an iron or steel metal rod (<c>rod-*</c>, stored under the <c>metal</c>
  /// wildcard). Hint key <c>{domain}:rcc-ingredient-rod</c>.</summary>
  public ConstructionStage RequireMetalRod(string domain, int quantity) =>
    RequireMetal(domain, "rod-*", "rod", quantity);

  // Shared iron/steel ingredient shape: a metal-captured wildcard code stored under "metal", limited
  // to iron and steel, with the conventional rcc-ingredient hint key.
  private ConstructionStage RequireMetal(
    string domain,
    string code,
    string kind,
    int quantity
  ) =>
    Require(
      code,
      quantity,
      $"{domain}:rcc-ingredient-{kind}",
      storeWildCard: "metal",
      allowedVariants: ["iron", "steel"]
    );

  internal JObject Build() => _stage;
}
