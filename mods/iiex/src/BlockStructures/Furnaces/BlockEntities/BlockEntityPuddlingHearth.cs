using System;
using System.Text;
using ExpandedLib.Blocks;
using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using IronIndustryExpanded.Items;
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

  #region Melting

  private float _meltProgress;

  /// <summary>
  /// How far the charge has melted down, 0-1. Advanced only from the furnace's own melt cycle, so it
  /// rides the core's bounded away-catch-up: a listener registered here would look identical while the
  /// chunk was loaded and teleport the heat the moment it was not.
  /// </summary>
  public float MeltProgress => _meltProgress;

  /// <summary>True once the pigs are gone and the bath is standing.</summary>
  public bool HasBath => _meltProgress >= 1f && _meltedUnits > 0;

  private bool _frozen;

  /// <summary>
  /// True once the bath has gone solid in the bed. A puddling furnace that loses its fire loses the heat,
  /// and the metal sets where it lies: nothing more can be gathered out of it and the bed has to be raked
  /// and started over. Balls already lying on the bed are solid iron and survive.
  /// </summary>
  public bool IsFrozen => _frozen;

  /// <summary>Sets the bath solid. Called by the furnace when its fire goes out, never by a player.</summary>
  public void FreezeBath() {
    if (!HasBath || _frozen)
      return;
    _frozen = true;
    Changed();
  }

  private int _meltedUnits;

  /// <summary>Metal units in the bath - what the charge weighed when it went down.</summary>
  public int BathUnits => _meltedUnits;

  /// <summary>
  /// Melts <paramref name="fraction"/> of a full charge down. Returns true once the bath has just
  /// formed, which is the tick the pigs stop being drawn. Nine pigs is the bed's capacity, not a gate:
  /// a part-charged hearth melts what it has and makes a smaller bath.
  /// </summary>
  public bool MeltDown(float fraction) {
    if (HasBath || PigCount == 0 || fraction <= 0f)
      return false;

    _meltProgress = GameMath.Clamp(_meltProgress + fraction, 0f, 1f);
    if (_meltProgress < 1f) {
      MarkDirty(true);
      return false;
    }

    _meltedUnits = PigCount * ItemPig.PigUnits;
    for (int i = 0; i < HeatingHearthLayout.Rows; i++)
      _pigs[i] = 0;
    Changed();
    return true;
  }

  /// <summary>
  /// The furnace this bed sits in, when it is part of one. <c>Core</c> itself is the part base's and is
  /// protected, so the verbs on the block ask the bed rather than reaching past it.
  /// </summary>
  public BlockEntityPuddlingFurnace? Furnace =>
    Core as BlockEntityPuddlingFurnace;

  private int _balls;

  /// <summary>Balls gathered out of the bath and not yet drawn off the bed.</summary>
  public int BallsOnBed => _balls;

  /// <summary>
  /// Balls this bath still has in it - what the metal divides into, less what has been gathered. The
  /// remainder that does not make a whole ball stays in the bath and comes out as tap cinder.
  /// </summary>
  public int BallsRemaining =>
    Math.Max(
      0,
      _meltedUnits / WroughtBallItemDefinitions.BallUnits - _ballsMade
    );

  private int _ballsMade;

  /// <summary>
  /// Whether only the clean-out is left: the bath is gathered dry, or it has set solid and nothing more
  /// can be got out of it either way.
  /// </summary>
  public bool IsWorkedOut => HasBath && (BallsRemaining == 0 || _frozen);

  /// <summary>
  /// One rabbling stroke: gathers the stiffening metal into a ball and leaves it on the bed. Refused on a
  /// bed with no bath and on one already worked out.
  /// </summary>
  public bool TryRabble() {
    if (!HasBath || _frozen || BallsRemaining == 0)
      return false;
    _ballsMade++;
    _balls++;
    Changed();
    return true;
  }

  /// <summary>Takes one gathered ball off the bed. Refused when none is standing.</summary>
  public bool TryDrawBall() {
    if (_balls <= 0)
      return false;
    _balls--;
    Changed();
    return true;
  }

  private long _lastStrokeMs;

  /// <summary>
  /// Whether the puddler is ready for another stroke through the door. One cooldown covers both verbs:
  /// they are the same motion at the same door, and pacing the two together is what keeps a heat's
  /// thirty-two gestures from collapsing into thirty-two clicks.
  /// </summary>
  /// <remarks>
  /// Not serialised. A cooldown that resets on reload costs nothing and keeps a transient out of the
  /// save; the melt cadence is what paces a heat, and this only stops the gathering being instant.
  /// </remarks>
  public bool StrokeReady =>
    Api == null
    || Api.World.ElapsedMilliseconds - _lastStrokeMs
      >= (long)(IiexValues.PuddlingStrokeCooldownSec * 1000f);

  /// <summary>Starts the cooldown. Called when a stroke is accepted, not when one is refused.</summary>
  public void MarkStroke() {
    if (Api != null)
      _lastStrokeMs = Api.World.ElapsedMilliseconds;
  }

  /// <summary>
  /// Metal left in the bath once every whole ball has been taken - the remainder, raked out as tap cinder
  /// when the bed is cleaned.
  /// </summary>
  public int CinderUnits =>
    HasBath ? _meltedUnits % WroughtBallItemDefinitions.BallUnits : 0;

  #endregion

  #region Charging

  /// <summary>
  /// Lays fettling in <paramref name="row"/>. Returns false if the row is already fettled or already
  /// carries a pig (re-fettling would bury the charge), or if the loaded centre row blocks reach.
  /// </summary>
  public bool TryFettle(HearthRows.Row row) {
    if (_meltProgress > 0f)
      return false;
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
    if (_meltProgress > 0f)
      return false;
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
    _meltProgress = 0f;
    _meltedUnits = 0;
    _balls = 0;
    _ballsMade = 0;
    _frozen = false;
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
        string.Join(',', _pigs)
          + "|"
          + string.Join(',', _fettled)
          + "|"
          + HasBath,
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
        PuddlingHearthLayout.ElementsFor(_pigs, _fettled, HasBath)
      ),
      out MeshData mesh
    );
    ExMesh.RotateByShape(mesh, Block);
    return mesh;
  }

  #endregion

  #region Serialization

  protected override void DeclareState(ExBlockState state) {
    // Clamped on read so an edited or older save cannot ask for a pig element the shape does not have,
    // which the tesselator drops without reporting; the melt/ball chain is clamped the same way, and in
    // declaration order so each clamp reads the field the one before it just set.
    for (int i = 0; i < HeatingHearthLayout.Rows; i++) {
      int row = i;
      state.Int(
        "pigs" + row,
        () => _pigs[row],
        v => _pigs[row] = GameMath.Clamp(v, 0, PuddlingHearthLayout.PigsPerRow)
      );
      state.Bool("fettled" + row, () => _fettled[row], v => _fettled[row] = v);
    }
    state.Float(
      "meltProgress",
      () => _meltProgress,
      v => _meltProgress = GameMath.Clamp(v, 0f, 1f)
    );
    state.Int(
      "meltedUnits",
      () => _meltedUnits,
      v =>
        _meltedUnits = GameMath.Clamp(
          v,
          0,
          PuddlingHearthLayout.PigCapacity * ItemPig.PigUnits
        )
    );
    state.Int(
      "ballsMade",
      () => _ballsMade,
      v =>
        _ballsMade = GameMath.Clamp(
          v,
          0,
          _meltedUnits / WroughtBallItemDefinitions.BallUnits
        )
    );
    state.Int(
      "balls",
      () => _balls,
      v => _balls = GameMath.Clamp(v, 0, _ballsMade)
    );
    state.Bool("bathFrozen", () => _frozen, v => _frozen = v);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    if (Api?.Side == EnumAppSide.Client)
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    if (Core is null) {
      dsc.AppendLine(Lang.Get(IiexLang.FurnacepartNofurnace));
      return;
    }
    dsc.AppendLine(
      Lang.Get(
        IiexLang.PuddlinghearthCharge,
        PigCount,
        PuddlingHearthLayout.PigCapacity
      )
    );
    int unfettled = 0;
    foreach (HearthRows.Row row in HearthRows.All)
      if (!_fettled[(int)row])
        unfettled++;
    if (unfettled > 0)
      dsc.AppendLine(Lang.Get(IiexLang.PuddlinghearthNeedsfettle, unfettled));
    if (CentreLoaded && !IsFullyCharged)
      dsc.AppendLine(Lang.Get(IiexLang.HearthCentreblocks));
  }

  #endregion
}
