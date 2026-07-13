using System;
using ExpandedLib.Registries.Entities;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Definitions;

/// <summary>
/// A code-first block definition: a fluent builder that produces the exact <see cref="JObject"/> the
/// vanilla object loader (<c>ModRegistryObjectTypeLoader</c>) consumes for a <c>blocktypes/</c> asset.
/// The <see cref="ExDefinitionModSystem"/> injects each built def as a synthetic in-memory asset on the
/// server, so the whole vanilla pipeline (variant expansion, <c>*ByType</c> selection, atlas, block-ID
/// assignment, client sync) runs unchanged - we never construct a <see cref="Block"/> instance
/// ourselves (that would reimplement all of that and break on every schema change).
/// <para>
/// Art stays in files: <see cref="Shape"/>/<see cref="Texture"/> take asset-file REFERENCES, never
/// inline geometry. The <see cref="Class{T}"/>/<see cref="EntityClass{T}"/> overloads resolve the
/// registered <c>{modid}.{ClassName}</c> key from the type via <see cref="EntityRegistry.KeyFor"/> -
/// the same source the registry uses - so a class rename can't silently desync the definition.
/// </para>
/// <para>
/// Only the subset of the blocktype schema the migrated blocks need is surfaced as typed methods;
/// anything else is reachable through <see cref="Attribute"/> (a nested POCO/token) or
/// <see cref="Raw"/> (an arbitrary token), so nothing the JSON can express is unrepresentable.
/// </para>
/// </summary>
public sealed class ExBlockDef
{
  private readonly string _domain;
  private readonly string _code;
  private readonly JObject _root = new();

  private ExBlockDef(string domain, string code)
  {
    _domain = domain;
    _code = code;
    _root["code"] = code;
  }

  /// <summary>Starts a block definition for <paramref name="code"/> in <paramref name="domain"/>
  /// (the mod id) - e.g. <c>Create("iwex", "solidifiediron")</c>.</summary>
  public static ExBlockDef Create(string domain, string code) =>
    new(domain, code);

  /// <summary>The mod id / asset domain this block belongs to.</summary>
  public string Domain => _domain;

  /// <summary>The block's short code (no domain, no <c>.json</c>).</summary>
  public string Code => _code;

  /// <summary>The synthetic asset location the loader keys on: <c>{domain}:blocktypes/{code}.json</c>
  /// (path begins <c>blocktypes/</c> and ends <c>.json</c>, exactly as the loader filters).</summary>
  public AssetLocation Location => new(_domain, "blocktypes/" + _code + ".json");

  #region Class binding (type-safe)

  /// <summary>Sets <c>class</c> to <typeparamref name="T"/>'s registered key (type-safe).</summary>
  public ExBlockDef Class<T>()
    where T : Block => Set("class", EntityRegistry.KeyFor(_domain, typeof(T)));

  /// <summary>Sets <c>class</c> to an explicit registered key (for a vanilla class by name).</summary>
  public ExBlockDef Class(string registeredKey) => Set("class", registeredKey);

  /// <summary>Sets <c>entityClass</c> to <typeparamref name="T"/>'s registered key (type-safe).</summary>
  public ExBlockDef EntityClass<T>()
    where T : BlockEntity =>
    Set("entityClass", EntityRegistry.KeyFor(_domain, typeof(T)));

  /// <summary>Sets <c>entityClass</c> to an explicit registered key.</summary>
  public ExBlockDef EntityClass(string registeredKey) =>
    Set("entityClass", registeredKey);

  #endregion

  #region Scalars

  /// <summary>Sets <c>blockmaterial</c> (e.g. <see cref="EnumBlockMaterial.Metal"/>).</summary>
  public ExBlockDef Material(EnumBlockMaterial material) =>
    Set("blockmaterial", material.ToString());

  /// <summary>Sets <c>resistance</c> (break resistance).</summary>
  public ExBlockDef Resistance(float resistance) =>
    Set("resistance", resistance);

  /// <summary>Sets <c>maxstacksize</c>.</summary>
  public ExBlockDef MaxStackSize(int size) => Set("maxstacksize", size);

  /// <summary>Sets <c>requiredMiningTier</c>.</summary>
  public ExBlockDef MiningTier(int tier) => Set("requiredMiningTier", tier);

  /// <summary>Sets <c>mineTool</c> (emitted lower-case, matching the vanilla blocktype convention).</summary>
  public ExBlockDef MineTool(EnumTool tool) =>
    Set("mineTool", tool.ToString().ToLowerInvariant());

  #endregion

  #region Shape / textures (references to art files)

  /// <summary>Sets <c>shape</c> to a single base shape reference (<c>{ "base": "domain:path" }</c>).</summary>
  public ExBlockDef Shape(string baseShape) =>
    Set("shape", new JObject { ["base"] = baseShape });

  /// <summary>Adds a texture mapping <paramref name="key"/> -&gt; <c>{ "base": "domain:path" }</c>
  /// under <c>textures</c> (accumulates across calls).</summary>
  public ExBlockDef Texture(string key, string baseTexture)
  {
    Nested("textures")[key] = new JObject { ["base"] = baseTexture };
    return this;
  }

  /// <summary>Shorthand for <c>Texture("all", baseTexture)</c>.</summary>
  public ExBlockDef TextureAll(string baseTexture) =>
    Texture("all", baseTexture);

  #endregion

  #region Sounds / creative tabs

  /// <summary>Adds one <c>sounds.{type}</c> entry (e.g. <c>Sound("place", "game:block/anvil")</c>).</summary>
  public ExBlockDef Sound(string type, string assetPath)
  {
    Nested("sounds")[type] = assetPath;
    return this;
  }

  /// <summary>Adds a <c>creativeinventory.{tab}</c> selector list (accumulates across calls).</summary>
  public ExBlockDef CreativeTab(string tab, params string[] selectors)
  {
    Nested("creativeinventory")[tab] = new JArray(selectors);
    return this;
  }

  #endregion

  #region Arbitrary attributes + escape hatch

  /// <summary>Sets an arbitrary <c>attributes.{key}</c> entry from a POCO/token - the typed route for
  /// data tables (multiblock offsets, filler offsets) that have no dedicated method yet.</summary>
  public ExBlockDef Attribute(string key, object value)
  {
    Nested("attributes")[key] = value as JToken ?? JToken.FromObject(value);
    return this;
  }

  /// <summary>Sets an arbitrary top-level key to an arbitrary token - the parity escape hatch so
  /// nothing the JSON can express is unrepresentable.</summary>
  public ExBlockDef Raw(string key, JToken token) => Set(key, token);

  #endregion

  /// <summary>The built blocktype JSON (a defensive clone, safe to mutate/serialize).</summary>
  public JObject ToJson() => (JObject)_root.DeepClone();

  private ExBlockDef Set(string key, JToken value)
  {
    _root[key] = value;
    return this;
  }

  // Returns the JObject at `key`, creating it if absent - for the accumulating sub-objects
  // (textures / sounds / creativeinventory / attributes).
  private JObject Nested(string key)
  {
    if (_root[key] is not JObject obj)
    {
      obj = new JObject();
      _root[key] = obj;
    }
    return obj;
  }
}
