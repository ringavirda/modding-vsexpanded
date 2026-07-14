using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Construction;
using ExpandedLib.Blocks.Structures;
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
  private readonly string _assetName;
  private readonly JObject _root = new();

  private ExBlockDef(string domain, string code, string assetName)
  {
    _domain = domain;
    _code = code;
    _assetName = assetName;
    _root["code"] = code;
  }

  /// <summary>Starts a block definition for <paramref name="code"/> in <paramref name="domain"/>
  /// (the mod id) - e.g. <c>Create("iwex", "solidifiediron")</c>. The synthetic asset is placed at
  /// <c>blocktypes/{code}.json</c>.</summary>
  public static ExBlockDef Create(string domain, string code) =>
    new(domain, code, code);

  /// <summary>Starts a block definition whose asset <b>path</b> differs from its <paramref name="code"/>
  /// - needed when several blocktype files share one code (e.g. the pipe class:
  /// <c>Create("ppex", "pipe", "pipes/straight")</c> -&gt; code <c>pipe</c> at
  /// <c>blocktypes/pipes/straight.json</c>). <paramref name="assetName"/> may include sub-folders.</summary>
  public static ExBlockDef Create(string domain, string code, string assetName) =>
    new(domain, code, assetName);

  /// <summary>The mod id / asset domain this block belongs to.</summary>
  public string Domain => _domain;

  /// <summary>The block's <c>code</c> field (may be shared across several defs).</summary>
  public string Code => _code;

  /// <summary>The synthetic asset location the loader keys on:
  /// <c>{domain}:blocktypes/{assetName}.json</c> - unique per def (path begins <c>blocktypes/</c> and
  /// ends <c>.json</c>, exactly as the loader filters).</summary>
  public AssetLocation Location =>
    new(_domain, "blocktypes/" + _assetName + ".json");

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

  /// <summary>Sets <c>drops</c> to an empty array - the block never drops itself (mega-blocks that are
  /// raised through construction and hand back only their materials/contents).</summary>
  public ExBlockDef NoDrops() => Set("drops", new JArray());

  #endregion

  #region Shape / textures (references to art files)

  /// <summary>Sets the <c>shape</c> base reference (<c>{ "base": "domain:path" }</c>). Order-independent
  /// with <see cref="ShapeRotateYByType"/>/<see cref="ShapeSelectiveElements"/> - all mutate one node.</summary>
  public ExBlockDef Shape(string baseShape)
  {
    NestedShape()["base"] = baseShape;
    return this;
  }

  /// <summary>Maps a variant wildcard to a Y rotation under the single shape's <c>rotateYByType</c> (for a
  /// block that keeps one base shape and only spins it per orientation, e.g. a mega-block footprint).</summary>
  public ExBlockDef ShapeRotateYByType(string wildcard, int degrees)
  {
    JObject shape = NestedShape();
    if (shape["rotateYByType"] is not JObject map)
    {
      map = new JObject();
      shape["rotateYByType"] = map;
    }
    map[wildcard] = degrees;
    return this;
  }

  /// <summary>Sets the shape's <c>selectiveElements</c> (the sub-elements the block renders, e.g. a
  /// mega-block's construction-stage roots).</summary>
  public ExBlockDef ShapeSelectiveElements(params string[] elements)
  {
    NestedShape()["selectiveElements"] = new JArray(elements);
    return this;
  }

  /// <summary>Adds a texture mapping <paramref name="key"/> -&gt; <c>{ "base": "domain:path" }</c> under
  /// <c>textures</c> (accumulates across calls). Any <paramref name="overlays"/> are emitted as an
  /// <c>overlays</c> array composited over the base (e.g. a brick tint over a running-bond base).</summary>
  public ExBlockDef Texture(
    string key,
    string baseTexture,
    params string[] overlays
  )
  {
    var texture = new JObject { ["base"] = baseTexture };
    if (overlays.Length > 0)
      texture["overlays"] = new JArray(overlays);
    Nested("textures")[key] = texture;
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

  /// <summary>Adds a per-tool hit/break sound override under
  /// <c>sounds.byTool.{tool}</c> (e.g. a ceramic pipe that breaks like rock under a pickaxe).</summary>
  public ExBlockDef SoundByTool(EnumTool tool, string hit, string breakSound)
  {
    var byTool = Nested("sounds");
    if (byTool["byTool"] is not JObject tools)
    {
      tools = new JObject();
      byTool["byTool"] = tools;
    }
    tools[tool.ToString()] = new JObject
    {
      ["hit"] = hit,
      ["break"] = breakSound,
    };
    return this;
  }

  /// <summary>Adds a <c>creativeinventory.{tab}</c> selector list (accumulates across calls).</summary>
  public ExBlockDef CreativeTab(string tab, params string[] selectors)
  {
    Nested("creativeinventory")[tab] = new JArray(selectors);
    return this;
  }

  #endregion

  #region Variant groups + *ByType maps

  /// <summary>Appends a <c>variantgroups</c> entry with explicit <paramref name="states"/> (order is
  /// preserved - variant expansion is left to the vanilla loader).</summary>
  public ExBlockDef VariantGroup(string code, params string[] states)
  {
    NestedArray("variantgroups")
      .Add(new JObject { ["code"] = code, ["states"] = new JArray(states) });
    return this;
  }

  /// <summary>Appends a <c>variantgroups</c> entry sourced from a worldproperty
  /// (<c>loadFromProperties</c>) - the vanilla worldproperty expansion is reused as-is.</summary>
  public ExBlockDef VariantGroupFromProperties(string code, string propertiesPath)
  {
    NestedArray("variantgroups")
      .Add(
        new JObject { ["code"] = code, ["loadFromProperties"] = propertiesPath }
      );
    return this;
  }

  /// <summary>Maps a variant wildcard (e.g. <c>*-straight-we-*</c>) to a base shape reference, with
  /// optional axis rotations, under <c>shapebytype</c> (expansion stays in vanilla).</summary>
  public ExBlockDef ShapeByType(
    string wildcard,
    string baseShape,
    int? rotateX = null,
    int? rotateY = null,
    int? rotateZ = null
  )
  {
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
  /// <c>overlays</c> array on the texture (e.g. a brick tint composited over a base).</summary>
  public ExBlockDef TextureByType(
    string wildcard,
    string textureKey,
    string baseTexture,
    params string[] overlays
  )
  {
    var byType = Nested("texturesByType");
    if (byType[wildcard] is not JObject entry)
    {
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

  /// <summary>Appends a block behavior by its registered name (a vanilla behavior, e.g.
  /// <c>"Lockable"</c>).</summary>
  public ExBlockDef Behavior(string name)
  {
    NestedArray("behaviors").Add(new JObject { ["name"] = name });
    return this;
  }

  /// <summary>Appends a block behavior by type - resolves to <typeparamref name="T"/>'s registered
  /// <c>{modid}.{ClassName}</c> key (type-safe, for a mod's own behavior).</summary>
  public ExBlockDef Behavior<T>()
    where T : BlockBehavior => Behavior(EntityRegistry.KeyFor(_domain, typeof(T)));

  /// <summary>Appends a block-<b>entity</b> behavior by its registered name (a vanilla behavior, e.g.
  /// <c>"Animatable"</c>).</summary>
  public ExBlockDef EntityBehavior(string name)
  {
    NestedArray("entityBehaviors").Add(new JObject { ["name"] = name });
    return this;
  }

  /// <summary>Appends a block-entity behavior carrying a <c>properties</c> blob (e.g. the construction
  /// behavior's <c>stages</c>). Prefer <see cref="Construction"/> for the RCC behavior.</summary>
  public ExBlockDef EntityBehavior(string name, JObject properties)
  {
    NestedArray("entityBehaviors")
      .Add(new JObject { ["name"] = name, ["properties"] = properties });
    return this;
  }

  /// <summary>Appends a block-entity behavior by type - resolves to <typeparamref name="T"/>'s
  /// registered key (type-safe, for a mod's own block-entity behavior).</summary>
  public ExBlockDef EntityBehavior<T>()
    where T : BlockEntityBehavior =>
    EntityBehavior(EntityRegistry.KeyFor(_domain, typeof(T)));

  #endregion

  #region Physics / collision / render

  /// <summary>Appends a <c>collisionboxes</c> cuboid (0..1 block-local coordinates).</summary>
  public ExBlockDef CollisionBox(
    float x1,
    float y1,
    float z1,
    float x2,
    float y2,
    float z2
  )
  {
    NestedArray("collisionboxes").Add(Box(x1, y1, z1, x2, y2, z2));
    return this;
  }

  /// <summary>Appends a <c>selectionboxes</c> cuboid (0..1 block-local coordinates).</summary>
  public ExBlockDef SelectionBox(
    float x1,
    float y1,
    float z1,
    float x2,
    float y2,
    float z2
  )
  {
    NestedArray("selectionboxes").Add(Box(x1, y1, z1, x2, y2, z2));
    return this;
  }

  /// <summary>Sets the single <c>collisionbox</c> object (one cuboid) rather than the
  /// <c>collisionboxes</c> array - the idiom for a mega-block that fills its whole cell.</summary>
  public ExBlockDef SingleCollisionBox(
    float x1,
    float y1,
    float z1,
    float x2,
    float y2,
    float z2
  ) => Set("collisionbox", Box(x1, y1, z1, x2, y2, z2));

  /// <summary>Sets the single <c>selectionbox</c> object (one cuboid) rather than the
  /// <c>selectionboxes</c> array.</summary>
  public ExBlockDef SingleSelectionBox(
    float x1,
    float y1,
    float z1,
    float x2,
    float y2,
    float z2
  ) => Set("selectionbox", Box(x1, y1, z1, x2, y2, z2));

  /// <summary>Sets <c>sidesolid</c> for all faces at once (<c>{ "all": value }</c>).</summary>
  public ExBlockDef SideSolid(bool all) =>
    Set("sidesolid", new JObject { ["all"] = all });

  /// <summary>Sets <c>sideopaque</c> for all faces at once (<c>{ "all": value }</c>).</summary>
  public ExBlockDef SideOpaque(bool all) =>
    Set("sideopaque", new JObject { ["all"] = all });

  /// <summary>Sets <c>renderpass</c> (e.g. <c>"OpaqueNoCull"</c>).</summary>
  public ExBlockDef RenderPass(string pass) => Set("renderpass", pass);

  /// <summary>Sets <c>faceCullMode</c> (e.g. <c>"NeverCull"</c>).</summary>
  public ExBlockDef FaceCullMode(string mode) => Set("faceCullMode", mode);

  /// <summary>Sets <c>lightAbsorption</c>.</summary>
  public ExBlockDef LightAbsorption(int absorption) =>
    Set("lightAbsorption", absorption);

  private static JObject Box(
    float x1,
    float y1,
    float z1,
    float x2,
    float y2,
    float z2
  ) =>
    new()
    {
      ["x1"] = x1,
      ["y1"] = y1,
      ["z1"] = z1,
      ["x2"] = x2,
      ["y2"] = y2,
      ["z2"] = z2,
    };

  #endregion

  #region Arbitrary attributes + escape hatch

  /// <summary>Sets an arbitrary <c>attributes.{key}</c> entry from a POCO/token - the typed route for
  /// data tables (multiblock offsets, filler offsets) that have no dedicated method yet.</summary>
  public ExBlockDef Attribute(string key, object value)
  {
    Nested("attributes")[key] = value as JToken ?? JToken.FromObject(value);
    return this;
  }

  /// <summary>Sets <c>attributes.handbook.groupBy</c> - the handbook variant grouping (so a family of
  /// variants shows as one collapsed handbook entry).</summary>
  public ExBlockDef Handbook(params string[] groupBy)
  {
    Nested("attributes")["handbook"] = new JObject
    {
      ["groupBy"] = new JArray(groupBy),
    };
    return this;
  }

  /// <summary>Sets <c>attributes.fillerOffsets</c> from a computed/validated footprint - the mega-block's
  /// invisible per-cell collision reservation (see <see cref="StructureFootprint"/>). Each cell emits
  /// <c>{ x, y, z }</c> plus <c>allowAttach: true</c> only when set (matching the hand-written form).
  /// The footprint is validated (no duplicate cell, none at the principal origin) so a bad table fails at
  /// load instead of silently clobbering a filler - retiring the block-number class of bugs in C#.</summary>
  public ExBlockDef FillerOffsets(IEnumerable<FillerCellSpec> cells)
  {
    var list = cells as IReadOnlyList<FillerCellSpec> ?? cells.ToArray();
    StructureFootprint.Validate(list);

    var array = new JArray();
    foreach (FillerCellSpec cell in list)
    {
      var entry = new JObject
      {
        ["x"] = cell.X,
        ["y"] = cell.Y,
        ["z"] = cell.Z,
      };
      if (cell.AllowAttach)
        entry["allowAttach"] = true;
      array.Add(entry);
    }
    Nested("attributes")["fillerOffsets"] = array;
    return this;
  }

  /// <summary>Appends the exlib right-click construction behavior (<c>ExRightClickConstructable</c>) with
  /// its staged material/shape table authored via a typed builder rather than a hand-nested JSON blob.</summary>
  public ExBlockDef Construction(Action<ConstructionStages> configure)
  {
    var stages = new ConstructionStages();
    configure(stages);
    return EntityBehavior(nameof(ExRightClickConstructable), stages.Build());
  }

  /// <summary>Sets an arbitrary top-level key to an arbitrary token - the parity escape hatch so
  /// nothing the JSON can express is unrepresentable.</summary>
  public ExBlockDef Raw(string key, JToken token) => Set(key, token);

  #endregion

  /// <summary>
  /// The explicit states of a variant group by its code (empty when the group is absent or sourced
  /// from a worldproperty via <see cref="VariantGroupFromProperties"/>). Lets a block derive its
  /// runtime tables - e.g. <c>AllowedOrientations</c> - straight from the def instead of hand-keeping a
  /// duplicate list that drifts out of sync.
  /// </summary>
  public string[] VariantStates(string groupCode)
  {
    if (_root["variantgroups"] is not JArray groups)
      return [];
    foreach (JToken g in groups)
      if ((string?)g["code"] == groupCode && g["states"] is JArray states)
        return states.Select(s => (string)s!).ToArray();
    return [];
  }

  /// <summary>The built blocktype JSON (a defensive clone, safe to mutate/serialize).</summary>
  public JObject ToJson() => (JObject)_root.DeepClone();

  private ExBlockDef Set(string key, JToken value)
  {
    _root[key] = value;
    return this;
  }

  // Returns the JObject at `key`, creating it if absent - for the accumulating sub-objects
  // (textures / sounds / creativeinventory / attributes / *ByType maps).
  private JObject Nested(string key)
  {
    if (_root[key] is not JObject obj)
    {
      obj = new JObject();
      _root[key] = obj;
    }
    return obj;
  }

  // Returns the single `shape` object, creating it if absent - so base / rotateYByType / selectiveElements
  // can be set in any order onto one node.
  private JObject NestedShape() => Nested("shape");

  // Returns the JArray at `key`, creating it if absent - for the accumulating, order-preserving lists
  // (variantgroups / behaviors / collisionboxes / selectionboxes).
  private JArray NestedArray(string key)
  {
    if (_root[key] is not JArray arr)
    {
      arr = new JArray();
      _root[key] = arr;
    }
    return arr;
  }
}
