using System.Text;
using ExpandedLib;
using ExpandedLib.Blocks;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Molten;
using ExpandedLib.Registries;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace SteelIndustryExpanded.BlockStructures.CowperStove.BlockEntities;

/// <summary>
/// Block entity for the cowper-stove heat sink. Holds the regenerator temperature pushed in by the
/// stove and renders an incandescent glow above 500 °C.
/// </summary>
[BlockEntityRegister]
public class BlockEntityHeatSink : ExBlockEntity {
  [Persist("temperature")]
  private float _temperature = ExlibValues.AmbientTemperature;

  /// <summary>Current heat-sink temperature in °C. Assigning it re-lights the block when the glow level
  /// changes.</summary>
  public float Temperature {
    get => _temperature;
    set {
      byte oldLight = GetLightLevel(_temperature);
      byte newLight = GetLightLevel(value);
      _temperature = value;

      if (oldLight != newLight && Api != null) {
        Api.World.BlockAccessor.MarkBlockDirty(Pos);
      }
    }
  }

  // The shared incandescence scale: canals, barrels and the heat sink glow alike.
  private static byte GetLightLevel(float temp) => MoltenMetal.GlowLevel(temp);

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    byte oldLight = GetLightLevel(_temperature);
    base.FromTreeAttributes(tree, worldForResolving);
    byte newLight = GetLightLevel(_temperature);

    if (oldLight != newLight && Api?.Side == EnumAppSide.Client) {
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
    }
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.AppendLine(
      Lang.Get(
        "siex:heatsink-info-temp",
        ExMeasure.Temperature(Temperature, "F1")
      )
    );
  }

  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  ) {
    if (Temperature <= 500f)
      return false; // Below the glow threshold: default block rendering

    tesselator.TesselateBlock(Block, out MeshData mesh);

    float[] color = ColorUtil.GetIncandescenceColorAsColor4f((int)Temperature);
    byte r = (byte)(color[0] * 255f);
    byte g = (byte)(color[1] * 255f);
    byte b = (byte)(color[2] * 255f);

    int vertexCount = mesh.Rgba.Length / 4;
    for (int i = 0; i < vertexCount; i++) {
      mesh.Rgba[i * 4 + 0] = (byte)((mesh.Rgba[i * 4 + 0] * b) / 255); // Blue
      mesh.Rgba[i * 4 + 1] = (byte)((mesh.Rgba[i * 4 + 1] * g) / 255);
      mesh.Rgba[i * 4 + 2] = (byte)((mesh.Rgba[i * 4 + 2] * r) / 255); // Red
    }

    int glow = (int)GameMath.Clamp((Temperature - 500f) / 2f, 0, 255);
    for (int i = 0; i < mesh.Flags.Length; i++) {
      mesh.Flags[i] |= glow; // The glow flag makes the engine bypass ambient occlusion and shadows
    }

    mesher.AddMeshData(mesh);
    return true; // The chunk mesh for this block was supplied here
  }
}
