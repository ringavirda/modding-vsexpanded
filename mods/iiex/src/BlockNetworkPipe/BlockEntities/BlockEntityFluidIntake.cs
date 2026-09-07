using System.Text;
using ExpandedLib.Blocks;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockNetworkPipe.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockNetworkPipe.BlockEntities;

/// <summary>
/// A pipe node that draws water from the world. Once a second it scans the cube directly below and
/// feeds the network (<see cref="CanIntake"/>) only when that whole cube is water - a frozen top-edge
/// skin is tolerated, the cell directly below is not - and no other intake sits within the exclusion
/// range, which limits how many intakes one pond can carry. The result syncs to the client for the HUD.
/// </summary>
[BlockEntityRegister]
public class BlockEntityFluidIntake : BlockEntityNetworkNode {
  public override string NetworkType { get; set; } = "pipe";

  /// <summary>True when the cube directly below the intake is fully water.</summary>
  [Persist("intakeHasWater")]
  public bool HasWater { get; private set; }

  /// <summary>True when another fluid intake sits within the exclusion range.</summary>
  [Persist("intakeCrowded")]
  public bool Crowded { get; private set; }

  /// <summary>True when this intake may actually draw water right now.</summary>
  public bool CanIntake => HasWater && !Crowded;

  /// <summary>
  /// Draws up to <paramref name="amount"/> litres from the pond below into this intake's own network;
  /// the intake is the generator, not the pump. Called by a powered fluid pump. Returns the litres
  /// produced, 0 when <see cref="CanIntake"/> is false or the network is full.
  /// </summary>
  public float ProduceWater(float amount, float temperature, IBlockAccessor ba) {
    if (!CanIntake || amount <= 0f)
      return 0f;
    if (NetworkSystem?.GetNetworkAt(Pos) is not PipeNetwork net)
      return 0f;

    // Gravity-fed source head; the output-side pressure is set by the pump's engine.
    return net.ProduceLiquidMeasured(amount, temperature, 1f, ba);
  }

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    // The scan handle is not kept: the base drops every listener on both removal and chunk unload,
    // and nothing else here stops the scan.
    if (api.Side == EnumAppSide.Server) {
      Rescan(0);
      RegisterGameTickListener(Rescan, 1000);
    }
  }

  /// <summary>Server-side periodic validity check; syncs to clients only on change.</summary>
  private void Rescan(float dt) {
    bool water = ScanWaterBelow();
    bool crowded = HasNearbyIntake();
    if (water != HasWater || crowded != Crowded) {
      HasWater = water;
      Crowded = crowded;
      MarkDirty();
    }
  }

  /// <summary>
  /// True when every cell of the <c>depth³</c> cube below is water. Top-layer outer cells may be
  /// ice, so a lake-ice skin does not stop the intake; the cell directly below must stay liquid.
  /// </summary>
  private bool ScanWaterBelow() {
    var ba = Api.World.BlockAccessor;
    int depth = IiexValues.FluidIntakeWaterDepth;
    int half = depth / 2;
    var p = new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension);
    for (int dx = -half; dx <= half; dx++)
      for (int dy = -1; dy >= -depth; dy--)
        for (int dz = -half; dz <= half; dz++) {
          p.Set(Pos.X + dx, Pos.Y + dy, Pos.Z + dz);
          if (ba.GetBlock(p, BlockLayersAccess.Fluid).LiquidCode == "water")
            continue;
          // Top-layer outer cells may be frozen; everything else must be liquid water.
          bool topOuter = dy == -1 && (dx != 0 || dz != 0);
          if (topOuter && ba.GetBlock(p).BlockMaterial == EnumBlockMaterial.Ice)
            continue;
          return false;
        }
    return true;
  }

  /// <summary>True when another fluid intake sits within the exclusion range (excludes self).</summary>
  private bool HasNearbyIntake() {
    var ba = Api.World.BlockAccessor;
    int r = (int)System.Math.Ceiling(IiexValues.FluidIntakeExclusionRange);
    float rSq =
      IiexValues.FluidIntakeExclusionRange
      * IiexValues.FluidIntakeExclusionRange;
    var p = new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension);
    for (int dx = -r; dx <= r; dx++)
      for (int dy = -r; dy <= r; dy++)
        for (int dz = -r; dz <= r; dz++) {
          if (dx == 0 && dy == 0 && dz == 0)
            continue;
          if (dx * dx + dy * dy + dz * dz > rSq)
            continue;
          p.Set(Pos.X + dx, Pos.Y + dy, Pos.Z + dz);
          if (ba.GetBlock(p) is BlockFluidIntake)
            return true;
        }
    return false;
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);

    if (Crowded)
      dsc.AppendLine(Lang.Get("iiex:fluidintake-info-crowded"));
    else if (!HasWater)
      dsc.AppendLine(Lang.Get("iiex:fluidintake-info-nowater"));
    else
      dsc.AppendLine(Lang.Get("iiex:fluidintake-info-active"));
  }
}
