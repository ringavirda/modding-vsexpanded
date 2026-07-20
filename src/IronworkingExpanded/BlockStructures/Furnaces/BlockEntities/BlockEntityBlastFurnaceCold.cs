using ExpandedLib.Registries.Entities;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The cold blast furnace. Everything it does lives in <see cref="BlockEntityBlastFurnace"/>; what
/// makes it the cold one is that its blast arrives at ambient off a mechanical blower, so the heat
/// balance gives it no preheat and its burden has to carry the coke instead.
/// </summary>
[BlockEntityRegister]
public class BlockEntityBlastFurnaceCold : BlockEntityBlastFurnace
{
  /// <summary>
  /// The cold furnace has no exhaust outlets: its open top IS the chimney (docs/design/iwex.md), and
  /// its layout puts plain refractory brick where the hot furnace carries its pipe outlets. Registering
  /// the hot furnace's outlet cells here regardless made every lit tick re-run the outlet scan against
  /// brick, and left <see cref="BlockEntityFurnaceCore.IsChoked"/> permanently unreachable.
  /// </summary>
  protected override Vec3i[] GasOutletCells => [];
}
