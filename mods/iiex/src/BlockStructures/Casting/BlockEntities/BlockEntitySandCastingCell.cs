using System;
using System.Text;
using ExpandedLib.Blocks;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Helpers;
using ExpandedLib.Industry.Molten;
using ExpandedLib.Networks;
using ExpandedLib.Registries;
using ExpandedLib.Structures;
using IronIndustryExpanded.BlockNetworkMolten;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.BlockStructures.Casting.BlockEntities;

/// <summary>
/// The 1x1 sand casting cell: a hollow brick shell rammed with green sand, impressed with a wooden
/// pattern, then fed molten metal from a canal on its launder face until the impression fills. Once the
/// cast hardens a shake-out yields the part, or scrap on a misrun or short pour, and leaves the cell
/// rammed and ready to be impressed again.
/// <para>
/// The cell hosts its metal on a <see cref="BEBehaviorMoltenCell"/> that is not registered in the molten
/// network and pulls into it itself, so its charge stays isolated from the feeding line. The pattern
/// carries the <see cref="MoldSpec"/>; this entity only reads it.
/// See docs/design/machines/casting-cell.md.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntitySandCastingCell : ExBlockEntity {
  private const int PullRatePerTick = 25;

  [Persist("cc_sand")]
  private SandLevel _sand = SandLevel.Empty;

  // full item code of the impressed pattern; null = no impression
  [Persist("cc_pattern")]
  private string? _patternCode;

  // The metal's temperature (degrees C) in the tick the cavity filled, which is what the misrun rule
  // judges: by shake-out the cast has hardened far below any pour minimum. Null until the cavity fills,
  // and for a cell saved before the value was kept, which shakes out as a clean cast.
  private float? _fillTemperature;

  private MoldSpec? _spec; // resolved lazily from _patternCode
  private MoltenRenderer? _renderer;

  private BEBehaviorMoltenCell? Cell => GetBehavior<BEBehaviorMoltenCell>();

  #region State access

  /// <summary>The cell's current sand level.</summary>
  public SandLevel Sand => _sand;

  /// <summary>
  /// The one <see cref="MoldSize"/> this station accepts a pattern for. The long cell runs the same
  /// ram/imprint/pour/shake-out loop over a bigger impression, so it is a subclass that overrides this
  /// and <see cref="WrongSizeErrorCode"/> only.
  /// </summary>
  protected virtual MoldSize AcceptedSize => MoldSize.Cell;

  /// <summary>Error sent when a pattern is for the other station. Distinct per station so the message can
  /// name the one the player should be using.</summary>
  protected virtual string WrongSizeErrorCode => "iiex-castingcell-wrongsize";

  /// <summary>Whether a pattern has been rammed into the sand.</summary>
  public bool HasImpression => _patternCode != null;

  /// <summary>The mold spec of the impressed pattern, or null when there is no impression.</summary>
  public MoldSpec? Spec {
    get {
      if (_patternCode == null)
        return null;
      _spec ??= ResolveSpec(_patternCode);
      return _spec;
    }
  }

  /// <summary>
  /// Re-resolves the mold spec for a persisted pattern by its full item code (e.g.
  /// <c>iiex:pattern-cylinder-oak</c>). The code carries its own domain, so a pattern shipped by another
  /// mod resolves here without this entity naming one. Null when the pattern no longer exists or carries
  /// no spec.
  /// </summary>
  private MoldSpec? ResolveSpec(string patternCode) {
    Item? pattern = Api?.World.GetItem(new AssetLocation(patternCode));
    return SpecOf(pattern);
  }

  /// <summary>The <see cref="MoldSpec"/> carried by a pattern collectible's attributes, or null if it has
  /// none. The only place the attribute is read.</summary>
  private static MoldSpec? SpecOf(CollectibleObject? pattern) =>
    MoldSpec.TryParse(
      pattern?.Attributes?[MoldSpec.AttributeKey],
      out MoldSpec? spec,
      out _
    )
      ? spec
      : null;

  /// <summary>The impressed pattern's localised name for block info, taken from its own item lang key so a
  /// pattern from another mod reads correctly here. Falls back to the raw code if the item is gone.</summary>
  private string PatternName {
    get {
      if (_patternCode == null)
        return "";
      AssetLocation loc = new(_patternCode);
      string key = $"{loc.Domain}:item-{loc.Path}";
      string name = Lang.GetMatching(key);
      return name == key ? _patternCode : name;
    }
  }

  #endregion

  #region Lifecycle

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    // Re-apply the impressed pattern's capacity so a reloaded, mid-cast cell keeps its cavity size.
    if (Spec is { } s)
      Cell?.SetCapacity(s.Capacity);

    if (api.Side == EnumAppSide.Server)
      RegisterGameTickListener(OnServerTick, 1000);
    else {
      RebuildRenderer((ICoreClientAPI)api);
      RegisterGameTickListener(_ => UpdateRenderer(), 1000);
    }
  }

  public override void OnBlockRemoved() {
    _renderer?.Dispose();
    _renderer = null;
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded() {
    _renderer?.Dispose();
    _renderer = null;
    base.OnBlockUnloaded();
  }

  #endregion

  #region Server tick: pull from the launder, cool

  private void OnServerTick(float dt) {
    if (Cell is not { } cell)
      return;

    if (
      CastingCellLogic.CanIntake(
        HasImpression,
        cavityFull: cell.CellAmount >= cell.MaxUnitCapacity,
        solidified: cell.Solidified
      )
    )
      PullFromLaunder(cell);

    cell.EnsureMetalStack(Api.World);
    cell.UpdateThermal(Api.World);
  }

  // Drains the external molten cell adjacent to the launder face, a canal delivering metal, into the
  // hosted cell. That face only.
  private void PullFromLaunder(BEBehaviorMoltenCell cell) {
    BlockPos feedPos = Pos.AddCopy(LaunderFace);
    if (
      Api.World.BlockAccessor.GetBlockEntity(feedPos) is not IMoltenCell src
      || src.Solidified
      || src.Sealed
      || src.CellAmount <= 0
    )
      return;

    int want = Math.Min(PullRatePerTick, src.CellAmount);
    int accepted = cell.PushMetalRaw(
      want,
      src.CellMetalType,
      src.CellTemperature,
      Api.World
    );
    if (accepted <= 0)
      return;
    src.DrainMetal(accepted);
    if (cell.CellAmount >= cell.MaxUnitCapacity)
      _fillTemperature = cell.CellTemperature;
  }

  /// <summary>The horizontal face the launder, and so the feeding canal, sits on: the face the spout is
  /// drawn on, which is the block's facing turned around. Resolved with
  /// <c>ExOrientation.FacingFromSide</c> because <c>BlockFacing.FromCode</c> returns null for a
  /// single-letter side token, which the north fallback would then swallow. The fallback covers a missing
  /// variant, not a misread one.</summary>
  public BlockFacing LaunderFace =>
    ExOrientation.FacingFromSide(Block.Variant["side"])?.Opposite
    ?? BlockFacing.NORTH;

  #endregion

  #region Interactions (ram / imprint / shake-out)

  /// <summary>Routes a right-click to the one action it resolves to. Returns true if handled.</summary>
  public bool OnInteract(IPlayer byPlayer) {
    if (Cell is not { } cell)
      return false;

    ItemStack? held = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
    bool holdingSand = CastingCellLogic.IsMoldingSand(
      held?.Item?.Code?.ToString()
    );
    bool holdingPattern = held?.Item?.FirstCodePart() == "pattern";
    bool emptyHand = held == null;

    CellAction action = CastingCellLogic.Decide(
      holdingSand,
      holdingPattern,
      emptyHand,
      _sand,
      HasImpression,
      hasMetal: cell.CellAmount > 0,
      isHardened: cell.IsHardened
    );

    switch (action) {
      case CellAction.RamSand:
        return RamSand(byPlayer, held!);
      case CellAction.Imprint:
        return Imprint(byPlayer, held!);
      case CellAction.Harvest:
        return Harvest(byPlayer, cell);
      case CellAction.TooHot:
        if (Api.Side == EnumAppSide.Server)
          (byPlayer as IServerPlayer)?.SendIngameError(
            "iiex-castingcell-toohot"
          );
        return true;
      default:
        return false;
    }
  }

  private bool RamSand(IPlayer byPlayer, ItemStack sand) {
    if (Api.Side == EnumAppSide.Server) {
      _sand = SandLevel.Full;
      if (byPlayer.WorldData?.CurrentGameMode != EnumGameMode.Creative)
        byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
      byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
      ExSounds.Play(Api, Pos, ExSounds.StoneCrush, 0.5f);
      MarkDirtyAndTesselate();
    }
    return true;
  }

  private bool Imprint(IPlayer byPlayer, ItemStack pattern) {
    // Read the spec off the held stack. A pattern code is `pattern-{type}-{wood}`, so there is no
    // `pattern-{type}` item to look the spec up by parts.
    string code = pattern.Collectible.Code.ToString();
    MoldSpec? spec = SpecOf(pattern.Collectible);
    if (spec == null) {
      if (Api.Side == EnumAppSide.Server)
        (byPlayer as IServerPlayer)?.SendIngameError(
          "iiex-castingcell-badpattern"
        );
      return true;
    }

    // A `longcell` pattern's cavity spans two blocks; imprinting it here would set this 1x1 cell's
    // capacity from a cavity it does not contain.
    if (spec.Size != AcceptedSize) {
      if (Api.Side == EnumAppSide.Server)
        (byPlayer as IServerPlayer)?.SendIngameError(WrongSizeErrorCode);
      return true;
    }

    if (Api.Side == EnumAppSide.Server) {
      _patternCode = code;
      _spec = spec;
      Cell?.SetCapacity(spec.Capacity);
      // Patterns are durable tools worn by use, not consumed. No wear in creative.
      if (byPlayer.WorldData?.CurrentGameMode != EnumGameMode.Creative)
        pattern.Collectible.DamageItem(
          Api.World,
          byPlayer.Entity,
          byPlayer.InventoryManager.ActiveHotbarSlot
        );
      ExSounds.Play(Api, Pos, ExSounds.StoneCrush, 0.6f);
      MarkDirtyAndTesselate();
    }
    return true;
  }

  private bool Harvest(IPlayer byPlayer, BEBehaviorMoltenCell cell) {
    if (Api.Side == EnumAppSide.Client)
      return true;
    if (Spec is not { } spec)
      return false;

    bool full = cell.CellAmount >= cell.MaxUnitCapacity;
    bool misrun = CastingCellLogic.IsMisrun(
      full,
      _fillTemperature,
      spec.MinPourTemp
    );

    foreach (ItemStack stack in BuildHarvest(spec, cell, full, misrun))
      if (byPlayer.InventoryManager?.TryGiveItemstack(stack) != true)
        Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.6, 0.5));

    cell.ClearContents();
    cell.ClearCapacity();
    _patternCode = null;
    _spec = null;
    _fillTemperature = null;
    _sand = CastingCellLogic.AfterShakeOut;
    ExSounds.Play(Api, Pos, ExSounds.StoneCrush, 0.7f);
    MarkDirtyAndTesselate();
    return true;
  }

  // A clean full cast yields the part. A misrun or an under-filled pour yields recovered scrap for the
  // metal actually in the cavity, conserving its mass.
  private System.Collections.Generic.IEnumerable<ItemStack> BuildHarvest(
    MoldSpec spec,
    BEBehaviorMoltenCell cell,
    bool full,
    bool misrun
  ) {
    if (full && !misrun) {
      spec.Output.Resolve(
        Api.World,
        "sand casting cell output for " + Block.Code
      );
      if (spec.Output.ResolvedItemstack?.Clone() is { } part) {
        part.StackSize = Math.Max(1, spec.Output.Quantity);
        yield return part;
      }
      yield break;
    }

    ItemStack? scrap = MoltenChisel.BuildRecovery(
      Api.World,
      new AssetLocation(cell.CellMetalType),
      cell.CellTemperature,
      cell.CellAmount
    );
    if (scrap != null)
      yield return scrap;
  }

  // Shake-out returns no sand: the cell rakes level and stays full (CastingCellLogic.AfterShakeOut).
  // Sand is a one-off cost at ram-up; the repeated loop is impress, pour, shake out.

  #endregion

  #region Rendering

  private void MarkDirtyAndTesselate() {
    MarkDirty(true);
    if (Api?.Side == EnumAppSide.Client)
      RebuildRenderer((ICoreClientAPI)Api);
  }

  private void RebuildRenderer(ICoreClientAPI capi) {
    _renderer?.Dispose();
    _renderer = null;
    if (Spec is not { } spec || spec.Cavity.Length == 0)
      return;

    float floor = spec.Cavity[0].Y1;
    float height = spec.Cavity[0].Y2 - spec.Cavity[0].Y1;
    // Rotate the molten surface with the block so an asymmetric cavity lands where the rammed-sand mesh
    // does: the same Y rotation ExMesh.RotateByShape applies to that mesh.
    float rotY = (Block.Shape?.rotateY ?? 0f) * GameMath.DEG2RAD;
    _renderer = new MoltenRenderer(
      Pos,
      capi,
      spec.Cavity,
      rotY,
      floor / 16f,
      height
    );
    capi.Event.RegisterRenderer(_renderer, EnumRenderStage.Opaque);
    UpdateRenderer();
  }

  private void UpdateRenderer() {
    if (_renderer == null || Cell is not { } cell)
      return;
    if (cell.CellAmount <= 0 || cell.CellMetalType.Length == 0) {
      _renderer.FillRatio = 0f;
      _renderer.MetalStack = null;
      return;
    }
    _renderer.FillRatio = GameMath.Clamp(
      cell.CellAmount / (float)Math.Max(1, cell.MaxUnitCapacity),
      0f,
      1f
    );
    _renderer.Temperature = cell.CellTemperature;
    Item? item = Api.World.GetItem(new AssetLocation(cell.CellMetalType));
    _renderer.MetalStack = item != null ? new ItemStack(item) : null;
  }

  // Draws the state's filling shape on top of the default brick shell: flat sand once rammed, the
  // pattern's cavity once impressed. The shell renders itself as long as base.OnTesselation is returned.
  // The filling shapes declare their sand texture under the key `andesite`, which the block binds to the
  // green-sand texture.
  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  ) {
    if (
      Api is ICoreClientAPI capi
      && CastingCellLogic.FillingShape(_sand, HasImpression, Spec?.Shape)
        is { } shapeRef
      // The state already reduces to a shape reference, so that reference is the key. The block code goes
      // in with it because the texture source below is the block's.
      && ExMeshCache.GetOrCreate(
        capi,
        Block,
        shapeRef,
        () => BuildFillingMesh(capi, tesselator, shapeRef)
      )
        is { } mesh
    )
      mesher.AddMeshData(mesh);

    return base.OnTesselation(mesher, tesselator);
  }

  private MeshData? BuildFillingMesh(
    ICoreClientAPI capi,
    ITesselatorAPI tesselator,
    string shapeRef
  ) {
    var loc = new AssetLocation(shapeRef);
    if (
      ExMeshCache.LoadShape(
        Api,
        new AssetLocation(loc.Domain, "shapes/" + loc.Path + ".json")
      )
      is not { } shape
    )
      return null;

    tesselator.TesselateShape(
      "sandcastingcell-filling",
      shape,
      out MeshData mesh,
      capi.Tesselator.GetTextureSource(Block)
    );
    // The filling shape is authored in the north frame like the shell, so it needs the same rotation the
    // tesselator bakes into the shell mesh.
    ExMesh.RotateByShape(mesh, Block);
    return mesh;
  }

  #endregion

  #region HUD + serialization

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    if (Cell is not { } cell)
      return;

    if (!HasImpression) {
      dsc.AppendLine(
        Lang.Get(
          _sand == SandLevel.Full
            ? "iiex:castingcell-needspattern"
            : "iiex:castingcell-needssand"
        )
      );
      return;
    }

    if (cell.CellAmount <= 0) {
      dsc.AppendLine(Lang.Get("iiex:castingcell-ready", PatternName));
      return;
    }

    string state = cell.IsHardened
      ? Lang.Get("iiex:metalstate-hardened")
      : Lang.Get("iiex:metalstate-liquid");
    dsc.AppendLine(
      Lang.Get(
        "iiex:castingcell-casting",
        cell.CellAmount,
        cell.MaxUnitCapacity,
        state,
        ExMeasure.Temperature(cell.CellTemperature)
      )
    );
  }

  protected override void DeclareState(ExBlockState state) =>
    state.Tree(
      "cc_filltemp",
      tree => {
        if (_fillTemperature is { } fillTemperature)
          tree.SetFloat("cc_filltemp", fillTemperature);
        else
          tree.RemoveAttribute("cc_filltemp");
      },
      (tree, world) =>
        _fillTemperature = tree.HasAttribute("cc_filltemp")
          ? tree.GetFloat("cc_filltemp")
          : null
    );

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor world
  ) {
    base.FromTreeAttributes(tree, world);
    // `cc_sandcode`, the rock type of the older per-variant sand, is not read: every cell holds green
    // sand, so a save carrying vanilla sand returns green sand on shake-out.
    _spec = null; // re-resolve lazily
    if (Api?.Side == EnumAppSide.Client)
      MarkDirtyAndTesselate();
  }

  #endregion
}
