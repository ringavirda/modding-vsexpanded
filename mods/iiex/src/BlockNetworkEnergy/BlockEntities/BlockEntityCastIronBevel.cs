using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockNetworkEnergy.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockNetworkEnergy.BlockEntities;

/// <summary>
/// Block entity for a <see cref="BlockCastIronBevel"/>: a shaft junction that branches the mpenergy run onto any
/// perpendicular face. It inherits the shaft's <see cref="ExpandedLib.Industry.MechanicalPower.IMpEnergyStorage"/> buffer and adds the render, one
/// bevel gear per perpendicular face with a connected neighbour. Those faces are derived from the world on each
/// call (<see cref="GearedFaces"/>) rather than stored, so placing or breaking a perpendicular shaft grows or
/// removes a gear on the next re-tesselation.
/// </summary>
[BlockEntityRegister]
public class BlockEntityCastIronBevel : BlockEntityCastIronShaft {
  /// <summary>The perpendicular faces that currently carry a gear: a branch face with a connected mpenergy
  /// neighbour. Derived from the world each call; empty off-server/client-accessor or before placement.</summary>
  public IEnumerable<BlockFacing> GearedFaces() {
    if (
      Api?.World?.BlockAccessor is not { } ba
      || Block is not BlockCastIronBevel
    )
      yield break;

    foreach (BlockFacing face in BlockFacing.ALLFACES)
      if (
        BlockCastIronBevel.IsPerpendicular(Orientation, face)
        && BlockCastIronBevel.HasConnectedNeighbor(ba, Pos, face)
      )
        yield return face;
  }

  /// <summary>A bevel presents a connector on every face (the junction), so the reciprocal probe sees all
  /// branches and continuations.</summary>
  public override bool HasConnectorAt(BlockFacing face) => true;

  #region Tesselation (shaft body + one gear per connected perpendicular face)

  private MeshData? _shaftMesh;
  private readonly Dictionary<BlockFacing, MeshData> _gearMeshes = [];

  /// <summary>
  /// Composes the bevel from parts: the straight shaft body plus one bevel-gear mesh per face in
  /// <see cref="GearedFaces"/>, each rotated onto its world face (<see cref="EnergyMeshes"/>). Per-face gear
  /// meshes are cached; a removed branch drops out of the loop.
  /// </summary>
  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  ) {
    if (Block == null)
      return false;

    _shaftMesh ??= EnergyMeshes.TesselateShaft(Api, tesselator, Block);
    if (_shaftMesh != null)
      mesher.AddMeshData(_shaftMesh);

    foreach (BlockFacing face in GearedFaces()) {
      if (!_gearMeshes.TryGetValue(face, out MeshData? gearMesh)) {
        gearMesh = EnergyMeshes.TesselateGear(Api, tesselator, Block, face);
        if (gearMesh == null)
          continue;
        _gearMeshes[face] = gearMesh;
      }
      mesher.AddMeshData(gearMesh);
    }

    base.OnTesselation(mesher, tesselator);
    return true;
  }

  /// <summary>A wrench rotation exchanges the block but keeps this BE, so the cached meshes would hold the
  /// pre-rotation axis. Dropping them makes the next tesselation rebuild against the new orientation.</summary>
  public override void OnExchanged(Block block) {
    base.OnExchanged(block);
    _shaftMesh = null;
    _gearMeshes.Clear();
  }

  #endregion
}
