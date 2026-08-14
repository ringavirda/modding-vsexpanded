using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The puddling furnace's hearth: a fettled cast-iron bed, three rows wide, charged and worked through the
/// door. Each row takes fettling (the iron-oxide bed that burns the carbon out of the pig) and then up to
/// three pigs; charging an unfettled row is refused, because pig laid on a bare bottom plate would weld to
/// it. Holds and displays the charge only - melting down, rabbling and balling up belong to the puddling
/// cycle and are not built.
/// </summary>
[BlockEntityRegister]
public class BlockEntityPuddlingHearth : BlockEntityFurnacePart {
  private readonly int[] _pigs = new int[HeatingHearthLayout.Rows];
  private readonly bool[] _fettled = new bool[HeatingHearthLayout.Rows];

  /// <summary>Total pigs on the bed.</summary>
  public int PigCount => _pigs[0] + _pigs[1] + _pigs[2];

  /// <summary>True once every row carries its full three pigs, which is one complete heat.</summary>
  public bool IsFullyCharged => PigCount >= PuddlingHearthLayout.PigCapacity;

  private bool CentreLoaded =>
    _fettled[(int)HearthRows.Row.Centre]
    || _pigs[(int)HearthRows.Row.Centre] > 0;

  #region Charging

  /// <summary>
  /// Lays fettling in <paramref name="row"/>. Returns false if the row is already fettled or already
  /// carries a pig (re-fettling would bury the charge), or if the loaded centre row blocks reach.
  /// </summary>
  public bool TryFettle(HearthRows.Row row) {
    if (!HearthRows.CanReach(row, CentreLoaded) && row != HearthRows.Row.Centre)
      return false;
    if (_fettled[(int)row] || _pigs[(int)row] > 0)
      return false;
    _fettled[(int)row] = true;
    Changed();
    return true;
  }

  /// <summary>
  /// Lays one pig in <paramref name="row"/>. Requires the row to be fettled and reachable. Returns false
  /// when the row already holds <c>PuddlingHearthLayout.PigsPerRow</c>.
  /// </summary>
  public bool TryChargePig(HearthRows.Row row) {
    if (!HearthRows.CanReach(row, CentreLoaded) && row != HearthRows.Row.Centre)
      return false;
    if (
      !_fettled[(int)row]
      || _pigs[(int)row] >= PuddlingHearthLayout.PigsPerRow
    )
      return false;
    _pigs[(int)row]++;
    Changed();
    return true;
  }

  /// <summary>Whether <paramref name="row"/> still wants fettling before it can take a pig.</summary>
  public bool NeedsFettle(HearthRows.Row row) => !_fettled[(int)row];

  /// <summary>
  /// Clears the bed after a heat: pigs and fettling in every row at once. The hearth is cleaned rather
  /// than tapped, so the spent fettle and the tap cinder come out together as the next heat's fettlestock.
  /// </summary>
  public void ClearBed() {
    for (int i = 0; i < HeatingHearthLayout.Rows; i++) {
      _pigs[i] = 0;
      _fettled[i] = false;
    }
    Changed();
  }

  private void Changed() {
    MarkDirty(true);
    if (Api?.Side == EnumAppSide.Client)
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  #endregion

  #region Render

  // Static mesh whose contents change, so it draws through OnTesselation with a per-block-entity element
  // set rather than through an animator. The blocktype's shape declares nothing selective; the whole mesh
  // is built here so there is one source of truth for which elements show.
  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  ) {
    if (Api is not ICoreClientAPI capi)
      return base.OnTesselation(mesher, tesselator);

    // Keyed on the charge, the way vanilla's firepit keys on burn and content state: the pigs per row and
    // whether that row is fettled are the only two things the drawn element set reads.
    if (
      ExMeshCache.GetOrCreate(
        capi,
        Block,
        string.Join(',', _pigs) + "|" + string.Join(',', _fettled),
        () => BuildBedMesh(tesselator)
      ) is { } mesh
    )
      mesher.AddMeshData(mesh);

    // True suppresses the default block mesh, which would draw every pig and both fettle beds regardless
    // of what is charged.
    return true;
  }

  private MeshData? BuildBedMesh(ITesselatorAPI tesselator) {
    if (
      ExMeshCache.LoadShape(Api, ExMeshCache.ShapePathOf(Block))
      is not { } shape
    )
      return null;

    // Prune the element tree directly rather than through the engine's selectiveElements matching, whose
    // per-segment prefix rule can keep or drop the wrong subtree without reporting it.
    tesselator.TesselateShape(
      Block,
      ExShapeElements.Pruned(
        shape,
        PuddlingHearthLayout.ElementsFor(_pigs, _fettled)
      ),
      out MeshData mesh
    );
    ExMesh.RotateByShape(mesh, Block);
    return mesh;
  }

  #endregion

  #region Serialization

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    for (int i = 0; i < HeatingHearthLayout.Rows; i++) {
      tree.SetInt("pigs" + i, _pigs[i]);
      tree.SetBool("fettled" + i, _fettled[i]);
    }
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    for (int i = 0; i < HeatingHearthLayout.Rows; i++) {
      // Clamped on read so an edited or older save cannot ask for a pig element the shape does not have,
      // which the tesselator drops without reporting.
      _pigs[i] = GameMath.Clamp(
        tree.GetInt("pigs" + i),
        0,
        PuddlingHearthLayout.PigsPerRow
      );
      _fettled[i] = tree.GetBool("fettled" + i);
    }
    if (Api?.Side == EnumAppSide.Client)
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    if (Core is null) {
      dsc.AppendLine(Lang.Get("iiex:furnacepart-nofurnace"));
      return;
    }
    dsc.AppendLine(
      Lang.Get(
        "iiex:puddlinghearth-charge",
        PigCount,
        PuddlingHearthLayout.PigCapacity
      )
    );
    int unfettled = 0;
    foreach (HearthRows.Row row in HearthRows.All)
      if (!_fettled[(int)row])
        unfettled++;
    if (unfettled > 0)
      dsc.AppendLine(Lang.Get("iiex:puddlinghearth-needsfettle", unfettled));
    if (CentreLoaded && !IsFullyCharged)
      dsc.AppendLine(Lang.Get("iiex:hearth-centreblocks"));
  }

  #endregion
}
