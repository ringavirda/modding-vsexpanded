using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Construction;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
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
public sealed class ExBlockDef : IExDef
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
  public static ExBlockDef Create(
    string domain,
    string code,
    string assetName
  ) => new(domain, code, assetName);

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

  /// <summary>Sets <c>storageFlags</c> (the inventory slots the block's item may be stored in - e.g.
  /// <c>2</c> = backpack only, for a bulky held block like the molten barrel).</summary>
  public ExBlockDef StorageFlags(int flags) => Set("storageFlags", flags);

  /// <summary>Sets <c>replaceable</c> - the replace-priority the world uses when another block is placed over
  /// this one (higher = more easily overwritten, e.g. <c>400</c> for a built-in furnace fitting).</summary>
  public ExBlockDef Replaceable(int priority) => Set("replaceable", priority);

  /// <summary>Sets <c>materialDensity</c> (kg/m³-ish, drives float/sink and shove behaviour).</summary>
  public ExBlockDef MaterialDensity(int density) =>
    Set("materialDensity", density);

  /// <summary>Sets <c>heldTpIdleAnimation</c> - the third-person idle animation played while the block is
  /// held (e.g. <c>"holdbothhandslarge"</c> for a two-handed carry).</summary>
  public ExBlockDef HeldTpIdleAnimation(string animation) =>
    Set("heldTpIdleAnimation", animation);

  /// <summary>Sets <c>heldRightReadyAnimation</c> - the first-person "ready" pose when the held block is raised.</summary>
  public ExBlockDef HeldRightReadyAnimation(string animation) =>
    Set("heldRightReadyAnimation", animation);

  /// <summary>Sets <c>heldTpUseAnimation</c> - the third-person animation played when the held block is used/placed.</summary>
  public ExBlockDef HeldTpUseAnimation(string animation) =>
    Set("heldTpUseAnimation", animation);

  /// <summary>Sets <c>walkspeedmultiplier</c> - the movement-speed factor while standing on the block (e.g.
  /// a smooth path &gt; 1). Takes a <see cref="double"/> so the emitted number matches the JSON-parsed value
  /// exactly (a <see cref="float"/> like <c>1.3f</c> widens to a different double and would break parity).</summary>
  public ExBlockDef WalkSpeedMultiplier(double multiplier) =>
    Set("walkspeedmultiplier", multiplier);

  /// <summary>Sets <c>requiredMiningTier</c>.</summary>
  public ExBlockDef MiningTier(int tier) => Set("requiredMiningTier", tier);

  /// <summary>Sets <c>mineTool</c> (emitted lower-case, matching the vanilla blocktype convention).</summary>
  public ExBlockDef MineTool(EnumTool tool) =>
    Set("mineTool", tool.ToString().ToLowerInvariant());

  /// <summary>Sets <c>drops</c> to an empty array - the block never drops itself (mega-blocks that are
  /// raised through construction and hand back only their materials/contents).</summary>
  public ExBlockDef NoDrops() => Set("drops", new JArray());

  /// <summary>Appends one <c>drops</c> entry (<c>{ type, code[, quantity] }</c>) - for a block that hands back a
  /// specific stack rather than itself (e.g. a stairs/slab variant that always drops its base "-free" form).
  /// Accumulates across calls.</summary>
  public ExBlockDef Drop(string type, string code, int? quantity = null)
  {
    var entry = new JObject { ["type"] = type, ["code"] = code };
    if (quantity.HasValue)
      entry["quantity"] = quantity.Value;
    NestedArray("drops").Add(entry);
    return this;
  }

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

  /// <summary>Sets one base shape and spins it for the four horizontal orientations via <c>rotateYByType</c>,
  /// with the per-side angles DERIVED from <see cref="ExOrientation.AngleFromSide"/> (the same convention the
  /// runtime uses for structure/box rotation) instead of a hand-typed 0/270/180/90 table. <paramref name="offset"/>
  /// is added to every angle (a block whose model faces away, like the mega-blocks that pass 180).</summary>
  public ExBlockDef ShapeSpunPerOrientation(string baseShape, int offset = 0)
  {
    Shape(baseShape);
    foreach (string side in HorizontalSides)
      ShapeRotateYByType(
        $"*-{side}",
        (ExOrientation.AngleFromSide(side) + offset + 360) % 360
      );
    return this;
  }

  /// <summary>The <c>shapebytype</c> form of <see cref="ShapeSpunPerOrientation"/> (one base shape, four
  /// per-orientation rotations) - for a block that keys its shape by the full variant wildcard.</summary>
  public ExBlockDef ShapeByTypePerOrientation(string baseShape, int offset = 0)
  {
    foreach (string side in HorizontalSides)
      ShapeByType(
        $"*-{side}",
        baseShape,
        rotateY: (ExOrientation.AngleFromSide(side) + offset + 360) % 360
      );
    return this;
  }

  private static readonly string[] HorizontalSides =
  [
    "north",
    "east",
    "south",
    "west",
  ];

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

  /// <summary>Sets a texture mapping <paramref name="key"/> from a fully-formed POCO/anonymous object/token - the
  /// escape hatch for texture shapes the <see cref="Texture(string, string, string[])"/> helper can't express,
  /// notably <c>alternates</c> (a list of alternative <c>{ base, overlays }</c> the atlas picks between per block,
  /// e.g. the pebble-scatter road textures). Overwrites any existing entry for the key.</summary>
  public ExBlockDef Texture(string key, object texture)
  {
    Nested("textures")[key] = texture as JToken ?? JToken.FromObject(texture);
    return this;
  }

  #endregion

  #region Model transforms

  /// <summary>Sets the <c>tpHandTransform</c> (the model transform applied when the block is held in third
  /// person): translation, XYZ rotation (degrees) and uniform scale. The generic route for any of the
  /// held/ground/gui transforms is the same <c>{ translation, rotation, scale }</c> object shape.</summary>
  public ExBlockDef TpHandTransform(
    double tx,
    double ty,
    double tz,
    double rx,
    double ry,
    double rz,
    double scale
  ) => Set("tpHandTransform", Transform(tx, ty, tz, rx, ry, rz, scale));

  private static JObject Transform(
    double tx,
    double ty,
    double tz,
    double rx,
    double ry,
    double rz,
    double scale
  ) =>
    new()
    {
      ["translation"] = new JObject
      {
        ["x"] = tx,
        ["y"] = ty,
        ["z"] = tz,
      },
      ["rotation"] = new JObject
      {
        ["x"] = rx,
        ["y"] = ry,
        ["z"] = rz,
      },
      ["scale"] = scale,
    };

  #endregion

  #region Sounds / creative tabs

  /// <summary>Adds one <c>sounds.{type}</c> entry (e.g. <c>Sound("place", "game:block/anvil")</c>).</summary>
  public ExBlockDef Sound(string type, string assetPath)
  {
    Nested("sounds")[type] = assetPath;
    return this;
  }

  /// <summary>Sets the four common <c>sounds</c> entries at once - the place/break/hit/walk block almost every
  /// block repeats. Equivalent to four <see cref="Sound"/> calls.</summary>
  public ExBlockDef Sounds(
    string place,
    string breakSound,
    string hit,
    string walk
  ) =>
    Sound("place", place)
      .Sound("break", breakSound)
      .Sound("hit", hit)
      .Sound("walk", walk);

  /// <summary>The metal machine sound set (anvil place/break/hit, stone walk) shared by the pipes, boilers,
  /// engines and converters - authored once instead of copied into every def.</summary>
  public ExBlockDef MetalSounds() =>
    Sounds(
      "game:block/anvil",
      "game:block/anvil",
      "game:block/anvil",
      "game:walk/stone"
    );

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

  /// <summary>Adds a per-variant sound override under <c>sounds.{type}ByType</c> (accumulates) - e.g.
  /// <c>SoundByType("break", "*-snow", "game:block/snow")</c> so a snow-covered variant breaks with a snow
  /// sound. <paramref name="type"/> is the base sound key (<c>break</c>/<c>hit</c>/…); the map key emitted is
  /// <c>{type}ByType</c>.</summary>
  public ExBlockDef SoundByType(string type, string wildcard, string assetPath)
  {
    JObject sounds = Nested("sounds");
    string key = type + "ByType";
    if (sounds[key] is not JObject map)
    {
      map = new JObject();
      sounds[key] = map;
    }
    map[wildcard] = assetPath;
    return this;
  }

  /// <summary>Adds a <c>creativeinventory.{tab}</c> selector list (accumulates across calls).</summary>
  public ExBlockDef CreativeTab(string tab, params string[] selectors)
  {
    Nested("creativeinventory")[tab] = new JArray(selectors);
    return this;
  }

  /// <summary>Adds the block to both the <c>general</c> tab and this def's own mod tab with the same
  /// <paramref name="selectors"/> - the pair every migrated block repeats, with the second tab name DERIVED
  /// from the def's domain instead of hand-copied.</summary>
  public ExBlockDef CreativeCommon(params string[] selectors) =>
    CreativeTab("general", selectors).CreativeTab(_domain, selectors);

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
  public ExBlockDef VariantGroupFromProperties(
    string code,
    string propertiesPath
  )
  {
    NestedArray("variantgroups")
      .Add(
        new JObject { ["code"] = code, ["loadFromProperties"] = propertiesPath }
      );
    return this;
  }

  /// <summary>Appends a CODELESS <c>variantgroups</c> entry sourced from a worldproperty (<c>{ loadFromProperties }</c>
  /// with no <c>code</c>) - the vanilla form for the horizontal-orientation property, whose group code is implied
  /// by the property itself (e.g. stairs' <c>game:abstract/horizontalorientation</c>).</summary>
  public ExBlockDef VariantGroupFromProperties(string propertiesPath)
  {
    NestedArray("variantgroups")
      .Add(new JObject { ["loadFromProperties"] = propertiesPath });
    return this;
  }

  /// <summary>Sets <c>skipVariants</c> - variant-code wildcards the loader must NOT expand (e.g. rock types a
  /// block doesn't ship a texture for).</summary>
  public ExBlockDef SkipVariants(params string[] wildcards) =>
    Set("skipVariants", new JArray(wildcards));

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

  /// <summary>Appends a block behavior carrying a <c>properties</c> blob (a POCO/anonymous object/token) - e.g.
  /// <c>Behavior("GroundStorable", new { layout = "SingleCenter" })</c>.</summary>
  public ExBlockDef Behavior(string name, object properties)
  {
    NestedArray("behaviors")
      .Add(
        new JObject
        {
          ["name"] = name,
          ["properties"] =
            properties as JToken ?? JToken.FromObject(properties),
        }
      );
    return this;
  }

  /// <summary>Appends a block behavior by type - resolves to <typeparamref name="T"/>'s registered
  /// <c>{modid}.{ClassName}</c> key (type-safe, for a mod's own behavior).</summary>
  public ExBlockDef Behavior<T>()
    where T : BlockBehavior =>
    Behavior(EntityRegistry.KeyFor(_domain, typeof(T)));

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

  /// <summary>Sets <c>sidesolid</c> from a per-face POCO (<c>{ all, up, down, north, … }</c>) - for a block
  /// that is solid on all faces but one (e.g. a lowered path: <c>new { all = true, up = false }</c>).</summary>
  public ExBlockDef SideSolid(object faces) =>
    Set("sidesolid", faces as JToken ?? JToken.FromObject(faces));

  /// <summary>Sets <c>sideopaque</c> for all faces at once (<c>{ "all": value }</c>).</summary>
  public ExBlockDef SideOpaque(bool all) =>
    Set("sideopaque", new JObject { ["all"] = all });

  /// <summary>Sets <c>sideopaque</c> from a per-face POCO (<c>{ all, down, … }</c>) - e.g. a path opaque only
  /// on its bottom face (<c>new { all = false, down = true }</c>).</summary>
  public ExBlockDef SideOpaque(object faces) =>
    Set("sideopaque", faces as JToken ?? JToken.FromObject(faces));

  /// <summary>Sets <c>sideAo</c> (ambient-occlusion contribution) for all faces at once
  /// (<c>{ "all": value }</c>) - e.g. a thin door that shouldn't darken its neighbours.</summary>
  public ExBlockDef SideAo(bool all) =>
    Set("sideAo", new JObject { ["all"] = all });

  /// <summary>Sets <c>emitSideAo</c> (whether the block casts ambient occlusion onto neighbours) for all faces at
  /// once (<c>{ "all": value }</c>). Distinct from <see cref="SideAo"/> (which controls AO the block RECEIVES).</summary>
  public ExBlockDef EmitSideAo(bool all) =>
    Set("emitSideAo", new JObject { ["all"] = all });

  /// <summary>Sets <c>renderpass</c> (e.g. <c>"OpaqueNoCull"</c>).</summary>
  public ExBlockDef RenderPass(string pass) => Set("renderpass", pass);

  /// <summary>Sets <c>faceCullMode</c> (e.g. <c>"NeverCull"</c>).</summary>
  public ExBlockDef FaceCullMode(string mode) => Set("faceCullMode", mode);

  /// <summary>Sets <c>drawtype</c> (e.g. <c>"json"</c> for a shape-driven block, or <c>"empty"</c>).</summary>
  public ExBlockDef DrawType(string drawType) => Set("drawtype", drawType);

  /// <summary>Sets <c>lightAbsorption</c>.</summary>
  public ExBlockDef LightAbsorption(int absorption) =>
    Set("lightAbsorption", absorption);

  /// <summary>Marks the block non-solid and non-opaque on all faces (<c>sidesolid</c> + <c>sideopaque</c>
  /// both <c>{ all: false }</c>) - the pair almost every mega-block/pipe repeats.</summary>
  public ExBlockDef NonSolid() => SideSolid(false).SideOpaque(false);

  /// <summary>Solid but non-opaque on all faces (the brick pipe variant: <c>sidesolid true</c>,
  /// <c>sideopaque false</c>).</summary>
  public ExBlockDef SolidNonOpaque() => SideSolid(true).SideOpaque(false);

  /// <summary>The transparent-render preset the metal pipes/valves share:
  /// <c>renderpass OpaqueNoCull</c>, <c>faceCullMode NeverCull</c>, <c>lightAbsorption 0</c>.</summary>
  public ExBlockDef NoCullRender() =>
    RenderPass("OpaqueNoCull").FaceCullMode("NeverCull").LightAbsorption(0);

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

  /// <summary>Sets an arbitrary <c>attributes.{key}</c> entry from a POCO/anonymous object/token/collection -
  /// the generic route for any attribute without a dedicated method. Anonymous objects and lists serialize
  /// through the game's JSON conventions, e.g.
  /// <c>Attribute("steamConnectorOffset", new { x = 0, y = 1, z = 4 })</c> or
  /// <c>Attribute("tiers", new[] { 1, 2, 3 })</c>.</summary>
  public ExBlockDef Attribute(string key, object value)
  {
    Nested("attributes")[key] = value as JToken ?? JToken.FromObject(value);
    return this;
  }

  /// <summary>Merges every property of a POCO/anonymous object into <c>attributes</c> at once - convenient for
  /// a block that carries several custom scalars/objects, e.g.
  /// <c>Attributes(new { steamConnectorOffset = new { x = 0, y = 1, z = 4 }, fillHeight = 0.5f })</c>. Each
  /// property becomes one <c>attributes.{name}</c> entry (later calls overwrite by key).</summary>
  public ExBlockDef Attributes(object poco)
  {
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
  public ExBlockDef Handbook(params string[] groupBy)
  {
    Nested("attributes")["handbook"] = new JObject
    {
      ["groupBy"] = new JArray(groupBy),
    };
    return this;
  }

  /// <summary>Sets the TOP-LEVEL <c>handbook.exclude</c> flag - hides the block from the survival handbook (for
  /// an internal block a player never crafts, e.g. the invisible structure filler). Distinct from
  /// <see cref="Handbook"/>, which sets the grouping under <c>attributes</c>.</summary>
  public ExBlockDef HandbookExclude() =>
    Set("handbook", new JObject { ["exclude"] = true });

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
      if (cell.Behaviors is { Count: > 0 })
      {
        var behaviors = new JArray();
        foreach (FillerBehaviorSpec b in cell.Behaviors)
        {
          var behavior = new JObject { ["code"] = b.Code };
          if (b.Face != null)
            behavior["face"] = b.Face;
          if (b.Properties != null)
            behavior["properties"] = JToken.FromObject(b.Properties);
          behaviors.Add(behavior);
        }
        entry["behaviors"] = behaviors;
      }
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

  /// <summary>Sets <c>attributes.multiblockStructure</c> (the <c>blockNumbers</c> map + <c>offsets</c> table)
  /// from a typed builder that names the block numbers, fills regular sub-volumes, and validates that every
  /// offset resolves to a declared number with no duplicate cell - see <see cref="MultiblockBuilder"/>.</summary>
  public ExBlockDef Multiblock(Action<MultiblockBuilder> configure)
  {
    var structure = new MultiblockBuilder();
    configure(structure);
    Nested("attributes")["multiblockStructure"] = structure.Build();
    return this;
  }

  /// <summary>Sets <c>attributes.multiblockStructure</c> from an ASCII layer diagram - a <c>Legend</c> plus one
  /// <c>Layer</c> per Y level drawn as a top-down grid (see <see cref="MultiblockLayoutBuilder"/>). The visual
  /// layout replaces the coordinate array; the generated table is behaviour-identical to any hand ordering of
  /// the same cells (the game treats the offsets as a set).</summary>
  public ExBlockDef MultiblockLayout(Action<MultiblockLayoutBuilder> configure)
  {
    var layout = new MultiblockLayoutBuilder();
    configure(layout);
    Nested("attributes")["multiblockStructure"] = layout.Build();
    return this;
  }

  /// <summary>Sets the top-level <c>combustibleProps</c> from a POCO/anonymous object - the smelting/burning
  /// recipe (melting point, duration, smelted stack) the game's fuel/smelt system reads. Unlike most nested
  /// data this lives at the block root, not under <c>attributes</c>.</summary>
  public ExBlockDef CombustibleProps(object props) =>
    Set("combustibleProps", props as JToken ?? JToken.FromObject(props));

  /// <summary>Adds a <c>{wildcard: value}</c> entry to a <c>{key}ByType</c> map under <c>attributes</c>
  /// (accumulates across calls) - e.g. <c>AttributeByType("widthByType", "*", 1)</c> for a door's
  /// per-type width. <paramref name="value"/> may be a scalar, POCO or token.</summary>
  public ExBlockDef AttributeByType(string key, string wildcard, object value)
  {
    JObject attrs = Nested("attributes");
    if (attrs[key] is not JObject map)
    {
      map = new JObject();
      attrs[key] = map;
    }
    map[wildcard] = value as JToken ?? JToken.FromObject(value);
    return this;
  }

  /// <summary>Adds a <c>{wildcard: value}</c> entry to a TOP-LEVEL <c>{key}ByType</c> map (accumulates) - for
  /// the per-type transform maps (<c>guiTransformByType</c>/<c>tpHandTransformByType</c>/<c>groundTransformByType</c>),
  /// whose values are transform objects with <c>{ translation, rotation, origin, scale }</c>.</summary>
  public ExBlockDef RawByType(string key, string wildcard, object value)
  {
    if (_root[key] is not JObject map)
    {
      map = new JObject();
      _root[key] = map;
    }
    map[wildcard] = value as JToken ?? JToken.FromObject(value);
    return this;
  }

  /// <summary>Sets an arbitrary top-level key to an arbitrary token - the parity escape hatch so
  /// nothing the JSON can express is unrepresentable.</summary>
  public ExBlockDef Raw(string key, JToken token) => Set(key, token);

  /// <summary>Sets an arbitrary top-level key from a POCO/anonymous object - the object-valued companion to
  /// <see cref="Raw(string, JToken)"/>, for the block-root transforms (<c>guiTransform</c>/<c>tpHandTransform</c>/
  /// <c>groundTransform</c>) whose shape varies per block (some carry <c>origin</c>, some omit <c>rotation</c>).</summary>
  public ExBlockDef Raw(string key, object value) =>
    Set(key, value as JToken ?? JToken.FromObject(value));

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

  // Explicit IExDef surface: the interface returns the base JToken; the public ToJson keeps the more precise
  // JObject its callers rely on (implicit interface implementation can't covary the return type).
  JToken IExDef.ToJson() => ToJson();

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
