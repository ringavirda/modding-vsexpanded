using System.Text;
using ExpandedLib.Helpers;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;

/// <summary>
/// The puddling furnace's hearth: a fettled cast-iron bed, three rows wide, that the player charges and
/// works <b>directly through the door</b>.
/// <para>
/// Two things go onto it, and the order is the mechanic: <b>fettling</b> first - the iron-oxide bed whose
/// oxygen burns the carbon out of the pig, which is the reaction puddling actually <em>is</em> - then up
/// to three <b>pigs</b> per row. Charging a row that has not been fettled is refused, because pig laid on
/// a bare bottom plate would weld itself to it; that is the reason fettling was a running cost rather than
/// a build cost.
/// </para>
/// <para>
/// <b>Incomplete by design.</b> This is the charge half. Melting down, rabbling the pasty iron and balling
/// it up belong to the puddling cycle and are not built - the hearth holds a charge and shows it.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntityPuddlingHearth : BlockEntityFurnacePart
{
  private readonly int[] _pigs = new int[HeatingHearthLayout.Rows];
  private readonly bool[] _fettled = new bool[HeatingHearthLayout.Rows];

  /// <summary>Total pigs on the bed.</summary>
  public int PigCount => _pigs[0] + _pigs[1] + _pigs[2];

  /// <summary>True once every row carries its full three pigs - a complete heat.</summary>
  public bool IsFullyCharged => PigCount >= PuddlingHearthLayout.PigCapacity;

  private bool CentreLoaded =>
    _fettled[(int)HearthRows.Row.Centre] || _pigs[(int)HearthRows.Row.Centre] > 0;

  #region Charging

  /// <summary>
  /// Lays fettling in <paramref name="row"/>. Refused if the row already carries any (re-fettling over a
  /// charge would bury it) or if the centre is in the way.
  /// </summary>
  public bool TryFettle(HearthRows.Row row)
  {
    if (!HearthRows.CanReach(row, CentreLoaded) && row != HearthRows.Row.Centre)
      return false;
    if (_fettled[(int)row] || _pigs[(int)row] > 0)
      return false;
    _fettled[(int)row] = true;
    Changed();
    return true;
  }

  /// <summary>
  /// Lays one pig in <paramref name="row"/>. Requires the row to be fettled first, and the row to be
  /// reachable. Returns false when the row is full.
  /// </summary>
  public bool TryChargePig(HearthRows.Row row)
  {
    if (!HearthRows.CanReach(row, CentreLoaded) && row != HearthRows.Row.Centre)
      return false;
    if (!_fettled[(int)row] || _pigs[(int)row] >= PuddlingHearthLayout.PigsPerRow)
      return false;
    _pigs[(int)row]++;
    Changed();
    return true;
  }

  /// <summary>Whether <paramref name="row"/> still wants fettling before it can take a pig.</summary>
  public bool NeedsFettle(HearthRows.Row row) => !_fettled[(int)row];

  /// <summary>
  /// Clears the bed after a heat: everything on it comes back at once. This is the design's answer to
  /// where the slag goes - a puddling furnace makes far too little to plumb, and what it makes is stiff
  /// tap cinder rather than a pour, so the hearth is <b>cleaned, not tapped</b>, and the spent fettle and
  /// the cinder come out together as the next heat's fettlestock.
  /// </summary>
  public void ClearBed()
  {
    for (int i = 0; i < HeatingHearthLayout.Rows; i++)
    {
      _pigs[i] = 0;
      _fettled[i] = false;
    }
    Changed();
  }

  private void Changed()
  {
    MarkDirty(true);
    if (Api?.Side == EnumAppSide.Client)
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  #endregion

  #region Render

  // The hearth has no animations at all - it is a static mesh whose contents change - so it draws through
  // OnTesselation with a per-BE SelectiveElements set rather than through an animator. The blocktype's own
  // shape declares nothing selective, so the base mesh is suppressed and everything comes from here; that
  // keeps one source of truth for which elements are showing.
  public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
  {
    if (Api is not ICoreClientAPI)
      return base.OnTesselation(mesher, tesselator);

    // Prune the element tree rather than leaning on the engine's selectiveElements matching, whose
    // per-segment prefix rule has silently kept or dropped the wrong subtree here before.
    Shape? shape = Api.Assets.TryGet(
      Block.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")
    )?.ToObject<Shape>();
    if (shape == null)
      return base.OnTesselation(mesher, tesselator);

    tesselator.TesselateShape(
      Block,
      ExShapeElements.Pruned(shape, PuddlingHearthLayout.ElementsFor(_pigs, _fettled)),
      out MeshData mesh
    );
    ExMesh.RotateByShape(mesh, Block);
    mesher.AddMeshData(mesh);
    // True: this block entity draws its own mesh entirely, so the default block mesh must not also be
    // drawn - it would render every pig and both fettle beds regardless of what is actually charged.
    return true;
  }

  #endregion

  #region Serialization

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    for (int i = 0; i < HeatingHearthLayout.Rows; i++)
    {
      tree.SetInt("pigs" + i, _pigs[i]);
      tree.SetBool("fettled" + i, _fettled[i]);
    }
  }

  public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
  {
    base.FromTreeAttributes(tree, worldForResolving);
    for (int i = 0; i < HeatingHearthLayout.Rows; i++)
    {
      // Clamped on read: a hand-edited or older save must not be able to ask for a pig element that
      // does not exist, which would silently drop it rather than fail.
      _pigs[i] = GameMath.Clamp(tree.GetInt("pigs" + i), 0, PuddlingHearthLayout.PigsPerRow);
      _fettled[i] = tree.GetBool("fettled" + i);
    }
    if (Api?.Side == EnumAppSide.Client)
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);
    if (Core is null)
    {
      dsc.AppendLine(Lang.Get("iwex:furnacepart-nofurnace"));
      return;
    }
    dsc.AppendLine(
      Lang.Get("iwex:furnace-puddlinghearth-charge", PigCount, PuddlingHearthLayout.PigCapacity)
    );
    int unfettled = 0;
    foreach (HearthRows.Row row in HearthRows.All)
      if (!_fettled[(int)row])
        unfettled++;
    if (unfettled > 0)
      dsc.AppendLine(Lang.Get("iwex:furnace-puddlinghearth-needsfettle", unfettled));
    if (CentreLoaded && !IsFullyCharged)
      dsc.AppendLine(Lang.Get("iwex:hearth-centreblocks"));
  }

  #endregion
}
