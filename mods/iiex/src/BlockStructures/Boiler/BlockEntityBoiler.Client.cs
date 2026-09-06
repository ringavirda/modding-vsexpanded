using System;
using System.Linq;
using ExpandedLib;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Helpers;
using IronIndustryExpanded.BlockStructures.Furnaces;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Boiler;

// Client side of the boiler: the in-vessel water-surface renderer, the client tick that keeps it
// fresh, the steam and sound effects, and the fuel-texture resolver below. Server state
// (_waterVolume, _state and the rest) arrives via the synced tree; nothing here mutates it.
public abstract partial class BlockEntityBoiler : ITexPositionSource {
  #region Client rendering + particles

  // The water box is authored in the same frame as the footprint and the geometry offsets, so it turns
  // by StructureAngle - the angle the fillers, the connectors and the interaction cells use - rather
  // than by Shape.rotateY, which is the leaf's own spin and need not carry the same offset.
  private float StructureRotationRad =>
    (float)((BoilerBlock?.StructureAngle ?? 0) * Math.PI / 180.0);

  /// <summary>
  /// In-vessel water-surface footprint (0-16 pixel space, block-local), read from the
  /// block's <c>waterRendererBox</c> attribute. Falls back to a 3-deep box.
  /// </summary>
  protected virtual Cuboidf[] WaterRendererBoxes {
    get {
      var node = (Block as IBoilerGeometry)?.WaterRendererBox;
      if (node == null || !node.Exists)
        return [new Cuboidf(-16f, 0f, 0f, 16f, 16f, 48f)];

      return
      [
        new Cuboidf(
          node["x1"].AsFloat(-16f),
          node["y1"].AsFloat(0f),
          node["z1"].AsFloat(0f),
          node["x2"].AsFloat(16f),
          node["y2"].AsFloat(16f),
          node["z2"].AsFloat(48f)
        ),
      ];
    }
  }

  private void InitWaterRenderer(ICoreClientAPI capi) {
    // The box supplies the horizontal footprint and UV; surface height is driven in discrete steps
    // via SurfaceLevel (see OnClientTick).
    _waterRenderer = new BoilerWaterRenderer(
      Pos,
      capi,
      WaterRendererBoxes,
      StructureRotationRad
    );
    capi.Event.RegisterRenderer(_waterRenderer, EnumRenderStage.Opaque);
  }

  private void OnClientTick(float dt) {
    if (_waterRenderer != null) {
      // Discrete surface height: hidden when dry, low (below the flues) while filling
      // toward the operating threshold, high (above the flues) once it can operate.
      _waterRenderer.SurfaceLevel =
        _waterVolume <= 0.01f ? 0f
        : _waterVolume < MinBoilWater ? IiexValues.BoilerWaterSurfaceLowLevel
        : IiexValues.BoilerWaterSurfaceHighLevel;
      _waterRenderer.Temperature = DisplayWaterTemperature();
    }

    // Boiling plays a low lava rumble loop from the vessel body.
    if (_state == BoilerState.Boiling)
      ExSounds.PlayLoop(
        Api.World,
        BoilerBlock?.ManHatchWorldPos(Pos) ?? Pos,
        ExSounds.Lava,
        ref _boilHumMs,
        2500,
        0.4f,
        16f
      );

    // Near choke pressure, vent warning steam as the visible signal that it is about to burst.
    if (InDangerZone)
      SpawnDangerSteam();

    // Steam escapes only through the open man hatch; a sealed boiler keeps it contained.
    if (ManHatchOpen && _steamVolume > 0f)
      SpawnManHatchSteam();

    // The outlet neck jets steam instead when it has no pipe attached.
    if (_steamLeaking)
      SpawnOutletLeakSteam();
  }

  /// <summary>Warning steam erupting from the man-hatch cell while in the danger zone.</summary>
  private void SpawnDangerSteam() {
    if (Api is not ICoreClientAPI || BoilerBlock == null)
      return;
    EmitSteamPlume(BoilerBlock.ManHatchWorldPos(Pos), 4);
  }

  /// <summary>Water surface temperature for the renderer glow, derived from the operating phase.</summary>
  private float DisplayWaterTemperature() =>
    _state switch {
      BoilerState.Boiling => IiexValues.BoilingPoint,
      BoilerState.Heating => ExlibValues.AmbientTemperature
        + (IiexValues.BoilingPoint - ExlibValues.AmbientTemperature)
          * HeatProgress,
      _ => ExlibValues.AmbientTemperature,
    };

  /// <summary>Steam billowing out of the open man hatch (see
  /// <see cref="BlockBoiler.ManHatchWorldPos"/>).</summary>
  private void SpawnManHatchSteam() {
    if (BoilerBlock != null)
      EmitSteamPlume(BoilerBlock.ManHatchWorldPos(Pos), 6);
  }

  /// <summary>Steam jetting out of the outlet neck when no pipe is attached above it
  /// (see <see cref="BlockBoiler.SteamPipeWorldPos"/>).</summary>
  private void SpawnOutletLeakSteam() {
    if (BoilerBlock != null)
      EmitSteamPlume(BoilerBlock.SteamPipeWorldPos(Pos), 8);
  }

  /// <summary>Spawns a short-lived steam plume rising out of the top of <paramref name="cell"/>.</summary>
  private void EmitSteamPlume(BlockPos cell, int count) {
    if (Api is ICoreClientAPI) {
      ExParticles.SteamPlume(Api.World, cell, count);
      ExSounds.HissSound(Api.World, cell);
    }
  }

  #endregion

  #region Fuel texture

  /// <summary>
  /// The internal fuel bed, or <c>null</c> on a vessel whose leaf blocktype declares no
  /// <see cref="BEBehaviorFirebox"/> and burns a coal pile in the firebox cell instead. Which of the
  /// two a boiler is is read from this everywhere: the fire, the main hatch and the fuel texture.
  /// </summary>
  public BEBehaviorFirebox? Bed => GetBehavior<BEBehaviorFirebox>();

  /// <summary>
  /// The charged fuel's own first declared texture path, resolved off its collectible rather than off a
  /// fixed set of declared keys (contrast <c>BlockFirebox.TextureKeyOf</c>, which maps a fuel to one of
  /// four textures the blocktype declares up front): a coal another mod adds is never one of a small
  /// enum, so the bed's own charged item is the only fixed point that works for it. Split out from the
  /// atlas lookup in the indexer below so the fuel-code-to-path mapping is exercisable without a live
  /// render client (see <c>BoilerFuelTextureTests</c>). Null when there is no bed, no charge, or the
  /// charged code no longer resolves to an item or block - the indexer's default covers all three.
  /// </summary>
  internal AssetLocation? FuelTexturePath() {
    if (Bed?.FuelCode is not { } code)
      return null;
    var loc = new AssetLocation(code);
    CollectibleObject? collectible =
      Api?.World.GetItem(loc) ?? (CollectibleObject?)Api?.World.GetBlock(loc);
    return FirstTextureOf(collectible);
  }

  /// <summary>An item's and a block's own texture dictionaries are unrelated properties on the engine
  /// side, so resolving either needs its own branch.</summary>
  private static AssetLocation? FirstTextureOf(CollectibleObject? collectible) {
    var textures = collectible switch {
      Item item => item.Textures,
      Block block => block.Textures,
      _ => null,
    };
    CompositeTexture? first = textures?.Values.FirstOrDefault();
    return first?.Baked?.BakedName ?? first?.Base;
  }

  #endregion

  #region ITexPositionSource

  // The coal-layer courses (assets/iiex/shapes/boiler/cornish.json, "CoalLayers/L1".."L4") are the only
  // elements authored against this texture code; every other code the boiler's shape carries (the
  // casing, the flues, the masonry) is untouched by a fuel charge and passes straight through to the
  // block's own resolution. This is the piece BlockFirebox.TextureKeyOf cannot be for the boiler: that
  // repoints a bed to one of four keys declared on the blocktype in advance, and a third-party coal can
  // never be one of them, so this reads the atlas directly at tesselation time instead.

  /// <summary>
  /// Shape texture code the fuel-layer elements are authored against. Internal, not private -
  /// <c>BoilerFuelTextureGuards</c> reads this constant directly and checks it against the shipped
  /// shape's own textures map, so a re-keyed CoalLayers element fails a test instead of silently
  /// falling through to the block's empty texture set.
  /// </summary>
  internal const string FuelTextureCode = "bituminous";

  /// <summary>
  /// The shape's own baked-in fuel texture - what the coal-layer elements were originally drawn against
  /// (see <c>assets/iiex/shapes/boiler/cornish.json</c>). The bed's last-resort default: an empty or
  /// unresolvable charge still has to draw as something, and it draws as this rather than as the
  /// engine's missing-texture placeholder.
  /// </summary>
  private static readonly AssetLocation DefaultFuelTexture = new(
    "game:block/coal/bituminous"
  );

  /// <inheritdoc/>
  public Size2i AtlasSize =>
    (Api as ICoreClientAPI)?.BlockTextureAtlas.Size ?? new Size2i();

  /// <inheritdoc/>
  public TextureAtlasPosition this[string textureCode] {
    get {
      if (Api is not ICoreClientAPI capi)
        return new TextureAtlasPosition();
      if (
        !string.Equals(textureCode, FuelTextureCode, StringComparison.Ordinal)
      )
        return capi.Tesselator.GetTextureSource(Block)[textureCode]
          ?? new TextureAtlasPosition();

      AssetLocation path = FuelTexturePath() ?? DefaultFuelTexture;
      return AtlasPositionOf(capi, path)
        ?? AtlasPositionOf(capi, DefaultFuelTexture)
        ?? capi.BlockTextureAtlas.UnknownTexturePosition;
    }
  }

  /// <summary>
  /// The atlas position of <paramref name="path"/>, inserting it if this is the first bed to reach for
  /// it - a fuel's texture is never one of the blocktype's own declared textures, so it is essentially
  /// never already present. Mirrors <c>BlockEntityStorageRack</c>'s own insert-on-demand.
  /// </summary>
  private static TextureAtlasPosition? AtlasPositionOf(
    ICoreClientAPI capi,
    AssetLocation path
  ) {
    TextureAtlasPosition? pos = capi.BlockTextureAtlas[path];
    if (pos != null)
      return pos;
    return capi.BlockTextureAtlas.GetOrInsertTexture(path, out _, out pos, null)
      ? pos
      : null;
  }

  #endregion
}
