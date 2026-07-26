using ExpandedLib.Blocks.Networks;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;

namespace IronworkingExpanded.BlockNetworkEnergy.BlockEntities;

/// <summary>
/// Block entity for a <see cref="Blocks.BlockCastIronShaft"/>: a pass-through node of the mpenergy run that also
/// acts as a small <see cref="IMpEnergyStorage"/> buffer - a shaft line carries a little rotating mass, so a
/// short run rides jitter without a dedicated flywheel (the design's "Buffer" node). Graph membership (add /
/// remove / connectivity) is handled by the <see cref="BlockEntityNetworkNode"/> base.
/// </summary>
[BlockEntityRegister]
public class BlockEntityCastIronShaft : BlockEntityNetworkNode, IMpEnergyStorage
{
  public override string NetworkType
  {
    get => "mpenergy";
    set { }
  }

  /// <summary>The small rotational inertia this shaft segment adds to its run. Read live from iwex config so a
  /// shaft line's buffering can be retuned without a rebuild.</summary>
  public float Inertia => IwexValues.ShaftInertia;
}
