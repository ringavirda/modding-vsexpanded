using System;
using ExpandedLib;
using ExpandedLib.Helpers;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace LowPressureExpanded.BlockStructures.Boiler;

// Client side of the boiler: the in-vessel water-surface renderer, the client tick that keeps it
// fresh, and the steam and sound effects. Server state (_waterVolume, _state and the rest) arrives
// via the synced tree; nothing here mutates it.
public abstract partial class BlockEntityBoiler {
  #region Client rendering + particles

  // Water and steam boxes are authored in the structure-offset frame (body along local +z), so they
  // rotate by the angle the fillers and connectors use (StructureAngle = AngleFromSide + 180), not
  // Shape.rotateY. The two differ by 180°, which would swing the water surface onto the hatch side.
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
        : _waterVolume < MinBoilWater ? LpexValues.BoilerWaterSurfaceLowLevel
        : LpexValues.BoilerWaterSurfaceHighLevel;
      _waterRenderer.Temperature = DisplayWaterTemperature();
    }

    // Boiling plays a low lava rumble loop from the vessel body.
    if (_state == BoilerState.Boiling)
      ExSounds.PlayLoop(
        Api.World,
        BoilerBlock?.LidWorldPos(Pos) ?? Pos,
        ExSounds.Lava,
        ref _boilHumMs,
        2500,
        0.4f,
        16f
      );

    // Near choke pressure, vent warning steam as the visible signal that it is about to burst.
    if (InDangerZone)
      SpawnDangerSteam();

    // Steam escapes only through the open lid; a sealed boiler keeps it contained.
    if (LidOpen && _steamVolume > 0f)
      SpawnLidSteam();

    // The outlet neck jets steam instead when it has no pipe attached.
    if (_steamLeaking)
      SpawnOutletLeakSteam();
  }

  /// <summary>Warning steam erupting from the access-lid filler while in the danger zone.</summary>
  private void SpawnDangerSteam() {
    if (Api is not ICoreClientAPI || BoilerBlock == null)
      return;
    EmitSteamPlume(BoilerBlock.LidWorldPos(Pos), 4);
  }

  /// <summary>Water surface temperature for the renderer glow, derived from the operating phase.</summary>
  private float DisplayWaterTemperature() =>
    _state switch {
      BoilerState.Boiling => LpexValues.BoilingPoint,
      BoilerState.Heating => ExlibValues.AmbientTemperature
        + (LpexValues.BoilingPoint - ExlibValues.AmbientTemperature)
          * HeatProgress,
      _ => ExlibValues.AmbientTemperature,
    };

  /// <summary>Steam billowing out of the open access lid (see <see cref="BlockBoiler.LidWorldPos"/>).</summary>
  private void SpawnLidSteam() {
    if (BoilerBlock != null)
      EmitSteamPlume(BoilerBlock.LidWorldPos(Pos).AddCopy(0, -1, 0), 6);
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
}
