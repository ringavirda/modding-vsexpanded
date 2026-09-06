using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Registries;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// Code-first item definition, the item-side sibling of <see cref="ExBlockDef"/>. Builds the
/// <see cref="JObject"/> the vanilla object loader consumes for an <c>itemtypes/</c> asset;
/// <see cref="ExDefinitionModSystem"/> injects it as a synthetic in-memory asset on the server, so variant
/// expansion, <c>*ByType</c> selection, atlas and client sync run unchanged. Keys without a typed method go
/// through <see cref="Attribute"/> or <see cref="Raw"/>; <see cref="Shape"/> and <see cref="Texture"/> take
/// asset-file references, never inline geometry. Model-transform decimals must be un-suffixed <c>double</c>
/// literals (<c>0.32</c>): a <c>float</c> literal widens to a different double and breaks JSON parity.
/// </summary>
public sealed class ExItemDef : IExDef {
  private readonly string _domain;
  private readonly string _code;
  private readonly string _assetName;
  private readonly JObject _root = new();

  private ExItemDef(string domain, string code, string assetName) {
    _domain = domain;
    _code = code;
    _assetName = assetName;
    _root["code"] = code;
  }

  /// <summary>Starts an item definition for <paramref name="code"/> in <paramref name="domain"/> (the mod
  /// id). The synthetic asset is placed at <c>itemtypes/{code}.json</c>.</summary>
  public static ExItemDef Create(string domain, string code) =>
    new(domain, code, code);

  /// <summary>Starts an item definition whose asset path differs from its <paramref name="code"/>, for a
  /// family of itemtype files that share one code. <paramref name="assetName"/> may include
  /// sub-folders.</summary>
  public static ExItemDef Create(
    string domain,
    string code,
    string assetName
  ) => new(domain, code, assetName);

  /// <summary>The mod id / asset domain this item belongs to.</summary>
  public string Domain => _domain;

  /// <summary>The item's <c>code</c> field.</summary>
  public string Code => _code;

  /// <summary>The synthetic asset location the loader keys on:
  /// <c>{domain}:itemtypes/{assetName}.json</c>.</summary>
  public AssetLocation Location =>
    new(_domain, "itemtypes/" + _assetName + ".json");

  #region Class binding (type-safe)

  /// <summary>Sets <c>class</c> to <typeparamref name="T"/>'s registered key, taken from the same
  /// <c>{modid}.{ClassName}</c> source the registry uses, so a rename cannot desync the two.</summary>
  public ExItemDef Class<T>()
    where T : Item => Set("class", EntityRegistry.KeyFor(_domain, typeof(T)));

  /// <summary>Sets <c>class</c> to an explicit registered key (for a vanilla class by name).</summary>
  public ExItemDef Class(string registeredKey) => Set("class", registeredKey);

  #endregion

  #region Scalars

  /// <summary>Sets <c>maxstacksize</c>.</summary>
  public ExItemDef MaxStackSize(int size) => Set("maxstacksize", size);

  /// <summary>Sets <c>materialDensity</c> (kg/m³; drives float/sink and shove behaviour).</summary>
  public ExItemDef MaterialDensity(int density) =>
    Set("materialDensity", density);

  /// <summary>Sets <c>storageFlags</c> (the inventory slots the item may be stored in).</summary>
  public ExItemDef StorageFlags(int flags) => Set("storageFlags", flags);

  /// <summary>Sets <c>heldTpIdleAnimation</c>, the third-person idle animation played while the item is held.</summary>
  public ExItemDef HeldTpIdleAnimation(string animation) =>
    Set("heldTpIdleAnimation", animation);

  /// <summary>Sets <c>heldRightReadyAnimation</c>, the first-person ready pose when the held item is raised.</summary>
  public ExItemDef HeldRightReadyAnimation(string animation) =>
    Set("heldRightReadyAnimation", animation);

  /// <summary>Sets <c>heldTpUseAnimation</c>, the third-person animation played when the held item is used.</summary>
  public ExItemDef HeldTpUseAnimation(string animation) =>
    Set("heldTpUseAnimation", animation);

  #endregion

  #region Shape / textures (references to art files)

  /// <summary>Sets the <c>shape</c> base reference (<c>{ "base": "domain:path" }</c>).</summary>
  public ExItemDef Shape(string baseShape) {
    Nested("shape")["base"] = baseShape;
    return this;
  }

  /// <summary>Sets the shape's <c>selectiveElements</c> - the sub-elements the item renders, for a shape
  /// file holding a family of them. Mutates the same <c>shape</c> node as <see cref="Shape"/>.</summary>
  /// <remarks>Matching is the engine's per-segment prefix rule, so naming an ancestor keeps more than
  /// intended and naming an element exactly drops its children.</remarks>
  public ExItemDef ShapeSelectiveElements(params string[] elements) {
    Nested("shape")["selectiveElements"] = new JArray(elements);
    return this;
  }

  /// <summary>Adds a texture mapping <paramref name="key"/> -&gt; <c>{ "base": "domain:path" }</c> under
  /// <c>textures</c> (accumulates across calls). Any <paramref name="overlays"/> are emitted as an
  /// <c>overlays</c> array composited over the base.</summary>
  public ExItemDef Texture(
    string key,
    string baseTexture,
    params string[] overlays
  ) {
    var texture = new JObject { ["base"] = baseTexture };
    if (overlays.Length > 0)
      texture["overlays"] = new JArray(overlays);
    Nested("textures")[key] = texture;
    return this;
  }

  /// <summary>Shorthand for <c>Texture("all", baseTexture)</c>.</summary>
  public ExItemDef TextureAll(string baseTexture) =>
    Texture("all", baseTexture);

  /// <summary>Sets a texture mapping <paramref name="key"/> from a fully-formed POCO/anonymous object/token,
  /// for texture shapes the string helper cannot express (e.g. <c>alternates</c>).</summary>
  public ExItemDef Texture(string key, object texture) {
    Nested("textures")[key] = texture as JToken ?? JToken.FromObject(texture);
    return this;
  }

  #endregion

  #region Variant groups / creative tabs

  /// <summary>Appends a <c>variantgroups</c> entry with explicit <paramref name="states"/>. Order is
  /// preserved; variant expansion is left to the vanilla loader.</summary>
  public ExItemDef VariantGroup(string code, params string[] states) {
    NestedArray("variantgroups")
      .Add(new JObject { ["code"] = code, ["states"] = new JArray(states) });
    return this;
  }

  /// <summary>Appends a <c>variantgroups</c> entry sourced from a worldproperty (<c>loadFromProperties</c>).</summary>
  public ExItemDef VariantGroupFromProperties(
    string code,
    string propertiesPath
  ) {
    NestedArray("variantgroups")
      .Add(
        new JObject { ["code"] = code, ["loadFromProperties"] = propertiesPath }
      );
    return this;
  }

  /// <summary>Appends a <c>variantgroups</c> entry sourced from a worldproperty (<c>{ loadFromProperties }</c>
  /// with no <c>code</c>) - the vanilla form for the horizontal-orientation property, whose group code is
  /// implied by the property itself.</summary>
  public ExItemDef VariantGroupFromProperties(string propertiesPath) {
    NestedArray("variantgroups")
      .Add(new JObject { ["loadFromProperties"] = propertiesPath });
    return this;
  }

  /// <summary>Sets <c>skipVariants</c> - variant-code wildcards the loader must not expand.</summary>
  public ExItemDef SkipVariants(params string[] wildcards) =>
    Set("skipVariants", new JArray(wildcards));

  /// <summary>Adds a <c>creativeinventory.{tab}</c> selector list (accumulates across calls).</summary>
  public ExItemDef CreativeTab(string tab, params string[] selectors) {
    Nested("creativeinventory")[tab] = new JArray(selectors);
    return this;
  }

  /// <summary>Adds the item to both the <c>general</c> tab and this def's own mod tab with the same
  /// <paramref name="selectors"/>, the second tab name derived from the def's domain.</summary>
  public ExItemDef CreativeCommon(params string[] selectors) =>
    CreativeTab("general", selectors).CreativeTab(_domain, selectors);

  #endregion

  #region Shape/texture *ByType maps

  /// <summary>Maps a variant wildcard (e.g. <c>*-iron-*</c>) to a base shape reference, with optional axis
  /// rotations, under <c>shapebytype</c> (expansion stays in vanilla).</summary>
  public ExItemDef ShapeByType(
    string wildcard,
    string baseShape,
    int? rotateX = null,
    int? rotateY = null,
    int? rotateZ = null
  ) {
    var shape = new JObject { ["base"] = baseShape };
    if (rotateX.HasValue)
      shape["rotateX"] = rotateX.Value;
    if (rotateY.HasValue)
      shape["rotateY"] = rotateY.Value;
    if (rotateZ.HasValue)
      shape["rotateZ"] = rotateZ.Value;
    Nested("shapebytype")[wildcard] = shape;
    return this;
  }

  /// <summary>Maps a variant wildcard to a texture key -&gt; base reference under <c>texturesByType</c>
  /// (accumulates keys per wildcard). Any <paramref name="overlays"/> are emitted as an
  /// <c>overlays</c> array on the texture.</summary>
  public ExItemDef TextureByType(
    string wildcard,
    string textureKey,
    string baseTexture,
    params string[] overlays
  ) {
    var byType = Nested("texturesByType");
    if (byType[wildcard] is not JObject entry) {
      entry = new JObject();
      byType[wildcard] = entry;
    }
    var texture = new JObject { ["base"] = baseTexture };
    if (overlays.Length > 0)
      texture["overlays"] = new JArray(overlays);
    entry[textureKey] = texture;
    return this;
  }

  #endregion

  #region Behaviors

  /// <summary>Appends a collectible behavior by its registered name (a vanilla behavior, e.g.
  /// <c>"GroundStorable"</c>).</summary>
  public ExItemDef Behavior(string name) {
    NestedArray("behaviors").Add(new JObject { ["name"] = name });
    return this;
  }

  /// <summary>Appends a collectible behavior carrying a <c>properties</c> blob (a POCO/anonymous object/token).</summary>
  public ExItemDef Behavior(string name, object properties) {
    NestedArray("behaviors")
      .Add(
        new JObject {
          ["name"] = name,
          ["properties"] =
            properties as JToken ?? JToken.FromObject(properties),
        }
      );
    return this;
  }

  /// <summary>Appends a collectible behavior by type - resolves to <typeparamref name="T"/>'s registered
  /// <c>{modid}.{ClassName}</c> key (type-safe, for a mod's own behavior).</summary>
  public ExItemDef Behavior<T>()
    where T : CollectibleBehavior =>
    Behavior(EntityRegistry.KeyFor(_domain, typeof(T)));

  #endregion

  #region Model transforms

  /// <summary>Sets the <c>guiTransform</c> from a POCO/anonymous object/token. Item transforms are authored
  /// as objects because their shapes vary: some omit <c>translation</c> or <c>origin</c>.</summary>
  public ExItemDef GuiTransform(object transform) =>
    RootKey("guiTransform", transform);

  /// <summary>Sets the <c>guiTransform</c> from translation, rotation, origin and uniform scale - the
  /// positional counterpart of <see cref="GuiTransform(object)"/>, for the common case of a fully
  /// specified transform. All the held/ground/gui transforms share this <c>{ translation, rotation,
  /// origin, scale }</c> object shape.</summary>
  public ExItemDef GuiTransform(
    double tx,
    double ty,
    double tz,
    double rx,
    double ry,
    double rz,
    double ox,
    double oy,
    double oz,
    double scale
  ) => GuiTransform(Transform(tx, ty, tz, rx, ry, rz, ox, oy, oz, scale));

  /// <summary>Sets the <c>fpHandTransform</c> (held in first person; deprecated in favour of
  /// <see cref="TpHandTransform(object)"/> but still read by the loader) from a POCO/anonymous object/token.</summary>
  public ExItemDef FpHandTransform(object transform) =>
    RootKey("fpHandTransform", transform);

  /// <summary>Sets the <c>fpHandTransform</c> from translation, rotation, origin and uniform scale.</summary>
  public ExItemDef FpHandTransform(
    double tx,
    double ty,
    double tz,
    double rx,
    double ry,
    double rz,
    double ox,
    double oy,
    double oz,
    double scale
  ) => FpHandTransform(Transform(tx, ty, tz, rx, ry, rz, ox, oy, oz, scale));

  /// <summary>Sets the <c>tpHandTransform</c> (held in third person) from a POCO/anonymous object/token.</summary>
  public ExItemDef TpHandTransform(object transform) =>
    RootKey("tpHandTransform", transform);

  /// <summary>Sets the <c>tpHandTransform</c> from translation, rotation, origin and uniform scale.</summary>
  public ExItemDef TpHandTransform(
    double tx,
    double ty,
    double tz,
    double rx,
    double ry,
    double rz,
    double ox,
    double oy,
    double oz,
    double scale
  ) => TpHandTransform(Transform(tx, ty, tz, rx, ry, rz, ox, oy, oz, scale));

  /// <summary>Sets the <c>groundTransform</c> (dropped on the ground) from a POCO/anonymous object/token.</summary>
  public ExItemDef GroundTransform(object transform) =>
    RootKey("groundTransform", transform);

  /// <summary>Sets the <c>groundTransform</c> from translation, rotation, origin and uniform scale.</summary>
  public ExItemDef GroundTransform(
    double tx,
    double ty,
    double tz,
    double rx,
    double ry,
    double rz,
    double ox,
    double oy,
    double oz,
    double scale
  ) => GroundTransform(Transform(tx, ty, tz, rx, ry, rz, ox, oy, oz, scale));

  private static JObject Transform(
    double tx,
    double ty,
    double tz,
    double rx,
    double ry,
    double rz,
    double ox,
    double oy,
    double oz,
    double scale
  ) =>
    new() {
      ["translation"] = new JObject {
        ["x"] = tx,
        ["y"] = ty,
        ["z"] = tz,
      },
      ["rotation"] = new JObject {
        ["x"] = rx,
        ["y"] = ry,
        ["z"] = rz,
      },
      ["origin"] = new JObject {
        ["x"] = ox,
        ["y"] = oy,
        ["z"] = oz,
      },
      ["scale"] = scale,
    };

  #endregion

  #region Recipes / attributes + escape hatch

  /// <summary>Sets the top-level <c>combustibleProps</c> from a POCO/anonymous object: the smelting/burning
  /// recipe (melting point, burn temperature and duration, smelted stack) the fuel/smelt system reads.</summary>
  public ExItemDef CombustibleProps(object props) =>
    Set("combustibleProps", props as JToken ?? JToken.FromObject(props));

  /// <summary>Sets the top-level <c>grindingProps</c> from a POCO/anonymous object: the quern grinding recipe
  /// (e.g. slag -&gt; powdered slag).</summary>
  public ExItemDef GrindingProps(object props) =>
    Set("grindingProps", props as JToken ?? JToken.FromObject(props));

  /// <summary>Sets an arbitrary <c>attributes.{key}</c> entry from a POCO/anonymous object/token/collection:
  /// the generic route for any attribute without a dedicated method.</summary>
  public ExItemDef Attribute(string key, object value) {
    Nested("attributes")[key] = value as JToken ?? JToken.FromObject(value);
    return this;
  }

  /// <summary>Merges every property of a POCO/anonymous object into <c>attributes</c> at once. Each property
  /// becomes one <c>attributes.{name}</c> entry; later calls overwrite by key.</summary>
  public ExItemDef Attributes(object poco) {
    JObject source =
      poco as JObject
      ?? JToken.FromObject(poco) as JObject
      ?? throw new ArgumentException(
        "Attributes(poco) needs an object with named properties.",
        nameof(poco)
      );
    JObject attributes = Nested("attributes");
    foreach (JProperty property in source.Properties())
      attributes[property.Name] = property.Value;
    return this;
  }

  /// <summary>Sets <c>attributes.handbook.groupBy</c> - the handbook variant grouping (so a family of
  /// variants shows as one collapsed handbook entry).</summary>
  public ExItemDef Handbook(params string[] groupBy) {
    Nested("attributes")["handbook"] = new JObject {
      ["groupBy"] = new JArray(groupBy),
    };
    return this;
  }

  /// <summary>Sets <c>attributes.handbook.exclude</c> - hides the item from the survival handbook. The
  /// handbook system reads this nested under <c>attributes</c>, same as <see cref="Handbook"/>'s
  /// grouping; a bare top-level <c>handbook</c> key is never read.</summary>
  public ExItemDef HandbookExclude() {
    JObject attributes = Nested("attributes");
    JObject handbook = attributes["handbook"] as JObject ?? new JObject();
    handbook["exclude"] = true;
    attributes["handbook"] = handbook;
    return this;
  }

  /// <summary>Adds a <c>{wildcard: value}</c> entry to a <c>{key}ByType</c> map under <c>attributes</c>
  /// (accumulates across calls) - e.g. <c>AttributeByType("widthByType", "*", 1)</c>. <paramref name="value"/>
  /// may be a scalar, POCO or token.</summary>
  public ExItemDef AttributeByType(string key, string wildcard, object value) {
    JObject attrs = Nested("attributes");
    if (attrs[key] is not JObject map) {
      map = new JObject();
      attrs[key] = map;
    }
    map[wildcard] = value as JToken ?? JToken.FromObject(value);
    return this;
  }

  /// <summary>Adds a <c>{wildcard: value}</c> entry to a top-level <c>{key}ByType</c> map (accumulates) - for
  /// the per-type transform maps (<c>guiTransformByType</c>/<c>tpHandTransformByType</c>/<c>groundTransformByType</c>),
  /// whose values are transform objects with <c>{ translation, rotation, origin, scale }</c>.</summary>
  public ExItemDef RootKeyByType(string key, string wildcard, object value) {
    if (_root[key] is not JObject map) {
      map = new JObject();
      _root[key] = map;
    }
    map[wildcard] = value as JToken ?? JToken.FromObject(value);
    return this;
  }

  /// <summary>Sets an arbitrary top-level key to an arbitrary token - the escape hatch for itemtype
  /// schema with no dedicated method. Read only when <paramref name="key"/> is a real itemtype key
  /// the game's object loader understands (see <see cref="KnownRootKeys"/>); a mistyped key is
  /// written into the JSON and never read by anything.</summary>
  public ExItemDef RootKey(string key, JToken token) => Set(key, token);

  /// <summary>Sets an arbitrary top-level key from a POCO/anonymous object: the object-valued companion to
  /// <see cref="RootKey(string, JToken)"/>.</summary>
  public ExItemDef RootKey(string key, object value) =>
    Set(key, value as JToken ?? JToken.FromObject(value));

  /// <summary>Obsolete name for <see cref="RootKeyByType(string, string, object)"/>.</summary>
  [Obsolete(
    "Raw writes a top-level key the game reads only when it is a real blocktype key; use RootKey, or Attribute for attributes.{key}"
  )]
  public ExItemDef RawByType(string key, string wildcard, object value) =>
    RootKeyByType(key, wildcard, value);

  /// <summary>Obsolete name for <see cref="RootKey(string, JToken)"/>.</summary>
  [Obsolete(
    "Raw writes a top-level key the game reads only when it is a real blocktype key; use RootKey, or Attribute for attributes.{key}"
  )]
  public ExItemDef Raw(string key, JToken token) => RootKey(key, token);

  /// <summary>Obsolete name for <see cref="RootKey(string, object)"/>.</summary>
  [Obsolete(
    "Raw writes a top-level key the game reads only when it is a real blocktype key; use RootKey, or Attribute for attributes.{key}"
  )]
  public ExItemDef Raw(string key, object value) => RootKey(key, value);

  #endregion

  /// <summary>The explicit states of a variant group by its code (empty when absent or worldproperty-sourced).</summary>
  public string[] VariantStates(string groupCode) {
    if (_root["variantgroups"] is not JArray groups)
      return [];
    foreach (JToken g in groups)
      if ((string?)g["code"] == groupCode && g["states"] is JArray states)
        return states.Select(s => (string)s!).ToArray();
    return [];
  }

  /// <summary>The built itemtype JSON (a defensive clone, safe to mutate/serialize).</summary>
  public JObject ToJson() => (JObject)_root.DeepClone();

  // Explicit implementation: the interface returns JToken while the public ToJson returns the more precise
  // JObject its callers rely on, and an implicit implementation cannot covary the return type.
  JToken IExDef.ToJson() => ToJson();

  private ExItemDef Set(string key, JToken value) {
    _root[key] = value;
    return this;
  }

  private JObject Nested(string key) {
    if (_root[key] is not JObject obj) {
      obj = new JObject();
      _root[key] = obj;
    }
    return obj;
  }

  private JArray NestedArray(string key) {
    if (_root[key] is not JArray arr) {
      arr = new JArray();
      _root[key] = arr;
    }
    return arr;
  }
}
