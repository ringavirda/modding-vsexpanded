using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkEnergy.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockNetworkEnergy.BlockEntities;

/// <summary>
/// Block entity for a <see cref="BlockCastIronBevel"/>: a shaft junction that branches the mpenergy run onto any
/// perpendicular face. It inherits the shaft's <see cref="IMpEnergyStorage"/> buffer; the only extra behaviour is
/// the render - one bevel gear per perpendicular face that has a connected neighbour. Those faces are <b>derived
/// live</b> from the world (<see cref="GearedFaces"/>), not stored, so placing or breaking a perpendicular shaft
/// grows or removes a gear automatically (the block re-tesselates on neighbour change).
/// </summary>
[BlockEntityRegister]
public class BlockEntityCastIronBevel : BlockEntityCastIronShaft
{
  /// <summary>The perpendicular faces that currently carry a gear: a branch face with a connected mpenergy
  /// neighbour. Derived from the world each call; empty off-server/client-accessor or before placement.</summary>
  public IEnumerable<BlockFacing> GearedFaces()
  {
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
  /// Composes the bevel from parts: the straight shaft body plus one bevel-gear mesh per geared face, each
  /// rotated onto its world face (<see cref="EnergyMeshes"/>). The geared faces come from <see cref="GearedFaces"/>
  /// (the connected perpendicular neighbours), so a branch is drawn as soon as a shaft is placed against it.
  /// Per-face gear meshes are cached; a removed branch simply drops out of the loop.
  /// </summary>
  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  )
  {
    if (Block == null)
      return false;

    _shaftMesh ??= EnergyMeshes.TesselateShaft(Api, tesselator, Block);
    if (_shaftMesh != null)
      mesher.AddMeshData(_shaftMesh);

    foreach (BlockFacing face in GearedFaces())
    {
      if (!_gearMeshes.TryGetValue(face, out MeshData? gearMesh))
      {
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

  /// <summary>A wrench rotation exchanges the block (keeping this BE), so the cached shaft body would keep the
  /// pre-rotation axis. Drop the caches so the next tesselation rebuilds against the new orientation.</summary>
  public override void OnExchanged(Block block)
  {
    base.OnExchanged(block);
    _shaftMesh = null;
    _gearMeshes.Clear();
  }

  #endregion
}
