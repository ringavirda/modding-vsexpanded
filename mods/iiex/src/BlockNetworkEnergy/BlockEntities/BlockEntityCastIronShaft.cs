using ExpandedLib.Blocks.Networks;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;

namespace IronIndustryExpanded.BlockNetworkEnergy.BlockEntities;

/// <summary>
/// Block entity for a <see cref="Blocks.BlockCastIronShaft"/>: a pass-through node of the mpenergy run
/// that also acts as a small <see cref="IMpEnergyStorage"/> buffer, giving a short run enough rotating
/// mass to ride jitter without a dedicated flywheel. Graph membership (add, remove, connectivity) is
/// handled by the <see cref="BlockEntityNetworkNode"/> base.
/// </summary>
[BlockEntityRegister]
public class BlockEntityCastIronShaft : BlockEntityNetworkNode, IMpEnergyStorage {
  public override string NetworkType {
    get => "mpenergy";
    set { }
  }

  /// <summary>Rotational inertia this shaft segment adds to its run. Read live from iiex config, so
  /// buffering can be retuned without a rebuild.</summary>
  public float Inertia => IiexValues.ShaftInertia;
}
