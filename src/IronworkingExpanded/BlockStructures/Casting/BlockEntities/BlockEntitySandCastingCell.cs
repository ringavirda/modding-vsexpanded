using System;
using System.Text;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronworkingExpanded.BlockStructures.Casting.BlockEntities;

/// <summary>
/// The 1×1 sand casting cell: placed as a hollow brick shell, rammed with <b>green sand</b>, impressed with
/// a wooden <b>pattern</b>, then fed molten metal from a canal on its launder face until the impression
/// fills. When the cast hardens the player shakes it out - collecting the part (or scrap on a misrun / short
/// pour) and the returned sand - and the cell drops to half sand, ready to be rammed and impressed again.
/// <para>
/// The cell hosts its metal on a <see cref="BEBehaviorMoltenCell"/> (a drain fitting, NOT registered in
/// the molten network) and pulls into it itself, so its charge stays isolated from the feeding line - the
/// same intake idiom as the pig bed, narrowed to the single launder face. The pattern carries the
/// <see cref="MoldSpec"/>; this entity only reads it.
/// </para>
/// </summary>
[BlockEntityRegister]
public class BlockEntitySandCastingCell : BlockEntity
{
  private const int PullRatePerTick = 25;

  private SandLevel _sand = SandLevel.Empty;
  private string? _patternCode; // full item code of the impressed pattern (domain-carrying), null = no impression

  private MoldSpec? _spec; // resolved lazily from _patternCode
  private MoltenRenderer? _renderer;

  private BEBehaviorMoltenCell? Cell => GetBehavior<BEBehaviorMoltenCell>();

  #region State access

  /// <summary>The cell's current sand level.</summary>
  public SandLevel Sand => _sand;

  /// <summary>
  /// The one <see cref="MoldSize"/> this station will take a pattern for. The station is identified by
  /// this and nothing else: the long cell is the same loop - ram, imprint, pour, shake out - over a bigger
  /// impression, so it is a subclass that changes this and the error code, not a second copy of the state
  /// machine.
  /// </summary>
  protected virtual MoldSize AcceptedSize => MoldSize.Cell;

  /// <summary>Error sent when a pattern is for the other station. Distinct per station so the message can
  /// name the one the player should be using.</summary>
  protected virtual string WrongSizeErrorCode => "iwex-castingcell-wrongsize";

  /// <summary>Whether a pattern has been rammed into the sand.</summary>
  public bool HasImpression => _patternCode != null;

  /// <summary>The mold spec of the impressed pattern, or null when there is no impression.</summary>
  public MoldSpec? Spec
  {
    get
    {
      if (_patternCode == null)
        return null;
      _spec ??= ResolveSpec(_patternCode);
      return _spec;
    }
  }

  /// <summary>
  /// Re-resolves the mold spec for a persisted pattern by its <b>full item code</b> (e.g.
  /// <c>lpex:pattern-cylinder-oak</c>). The code carries its own domain, which is what actually delivers the
  /// design's cross-mod contract: iwex ships the station, another mod ships the pattern and its product, and this
  /// entity never names a mod. Null when the pattern no longer exists (a mod was removed) or carries no spec.
  /// </summary>
  private MoldSpec? ResolveSpec(string patternCode)
  {
    Item? pattern = Api?.World.GetItem(new AssetLocation(patternCode));
    return SpecOf(pattern);
  }

  /// <summary>The mold spec carried by a pattern collectible's attributes, or null if it has none. The pattern
  /// item <b>is</b> the spec (see <see cref="MoldSpec"/>), so this is the one place it is read.</summary>
  private static MoldSpec? SpecOf(CollectibleObject? pattern) =>
    MoldSpec.TryParse(pattern?.Attributes?[MoldSpec.AttributeKey], out MoldSpec? spec, out _)
      ? spec
      : null;

  /// <summary>The impressed pattern's localised name for block-info, from its own item lang key (so an lpex
  /// pattern reads correctly in a station iwex owns). Falls back to the raw code if the item is gone.</summary>
  private string PatternName
  {
    get
    {
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

  public override void Initialize(ICoreAPI api)
  {
    base.Initialize(api);
    // Re-apply the impressed pattern's capacity so a reloaded, mid-cast cell keeps its cavity size.
    if (Spec is { } s)
      Cell?.SetCapacity(s.Capacity);

    if (api.Side == EnumAppSide.Server)
      RegisterGameTickListener(OnServerTick, 1000);
    else
    {
      RebuildRenderer((ICoreClientAPI)api);
      RegisterGameTickListener(_ => UpdateRenderer(), 1000);
    }
  }

  public override void OnBlockRemoved()
  {
    _renderer?.Dispose();
    _renderer = null;
    base.OnBlockRemoved();
  }

  public override void OnBlockUnloaded()
  {
    _renderer?.Dispose();
    _renderer = null;
    base.OnBlockUnloaded();
  }

  #endregion

  #region Server tick: pull from the launder, cool

  private void OnServerTick(float dt)
  {
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

  // The intake: drain the adjacent EXTERNAL molten cell on the launder face (a canal delivering metal)
  // into our own hosted cell. Only that one face, so the model does not lie about where metal enters.
  private void PullFromLaunder(BEBehaviorMoltenCell cell)
  {
    BlockPos feedPos = Pos.AddCopy(LaunderFace);
    if (
      Api.World.BlockAccessor.GetBlockEntity(feedPos) is not IMoltenCell src
      || src.Solidified
      || src.Sealed
      || src.CellAmount <= 0
    )
      return;

    int want = Math.Min(PullRatePerTick, src.CellAmount);
    int accepted = cell.PushMetalRaw(want, src.CellMetalType, src.CellTemperature, Api.World);
    if (accepted > 0)
      src.DrainMetal(accepted);
  }

  /// <summary>The horizontal face the launder (and so the feeding canal) sits on - the block's facing.
  /// <para>
  /// Note: <c>ExOrientation.FacingFromSide</c>, not <c>BlockFacing.FromCode</c>: the latter answers null
  /// for a single-letter token, and the <c>?? BlockFacing.NORTH</c> below would then swallow it - every
  /// cell silently claiming its launder is on the north wall whatever it was built facing. The fallback
  /// is meant to cover a MISSING variant, not a misread one.
  /// </para></summary>
  public BlockFacing LaunderFace =>
    ExOrientation.FacingFromSide(Block.Variant["side"]) ?? BlockFacing.NORTH;

  #endregion

  #region Interactions (ram / imprint / shake-out)

  /// <summary>Routes a right-click to the one action it resolves to. Returns true if handled.</summary>
  public bool OnInteract(IPlayer byPlayer)
  {
    if (Cell is not { } cell)
      return false;

    ItemStack? held = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
    bool holdingSand = CastingCellLogic.IsMoldingSand(held?.Item?.Code?.ToString());
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

    switch (action)
    {
      case CellAction.RamSand:
        return RamSand(byPlayer, held!);
      case CellAction.Imprint:
        return Imprint(byPlayer, held!);
      case CellAction.Harvest:
        return Harvest(byPlayer, cell);
      case CellAction.TooHot:
        if (Api.Side == EnumAppSide.Server)
          (byPlayer as IServerPlayer)?.SendIngameError("iwex-castingcell-toohot");
        return true;
      default:
        return false;
    }
  }

  private bool RamSand(IPlayer byPlayer, ItemStack sand)
  {
    if (Api.Side == EnumAppSide.Server)
    {
      _sand = SandLevel.Full;
      if (byPlayer.WorldData?.CurrentGameMode != EnumGameMode.Creative)
        byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
      byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
      ExSounds.Play(Api, Pos, ExSounds.StoneCrush, 0.5f);
      MarkDirtyAndTesselate();
    }
    return true;
  }

  private bool Imprint(IPlayer byPlayer, ItemStack pattern)
  {
    // Read the spec straight off the held stack - the pattern item is the spec. Reconstructing an item code
    // from parts cannot work here: a pattern is `pattern-{type}-{wood}`, so there is no `pattern-{type}` item
    // to look up, and the wood must not leak into the lookup either.
    string code = pattern.Collectible.Code.ToString();
    MoldSpec? spec = SpecOf(pattern.Collectible);
    if (spec == null)
    {
      if (Api.Side == EnumAppSide.Server)
        (byPlayer as IServerPlayer)?.SendIngameError("iwex-castingcell-badpattern");
      return true;
    }

    // The size is not decoration. A `longcell` pattern's cavity spans two blocks; imprinting it here
    // would set this 1×1 cell's capacity from a cavity it physically does not contain - a slab's worth of
    // metal poured into one block. This guard is what gives MoldSpec.Size a consumer at all.
    if (spec.Size != AcceptedSize)
    {
      if (Api.Side == EnumAppSide.Server)
        (byPlayer as IServerPlayer)?.SendIngameError(WrongSizeErrorCode);
      return true;
    }

    if (Api.Side == EnumAppSide.Server)
    {
      _patternCode = code;
      _spec = spec;
      Cell?.SetCapacity(spec.Capacity);
      // Patterns are durable tools worn by use, not consumed (wear skipped in creative, like ramming sand).
      if (byPlayer.WorldData?.CurrentGameMode != EnumGameMode.Creative)
        pattern.Collectible.DamageItem(Api.World, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot);
      ExSounds.Play(Api, Pos, ExSounds.StoneCrush, 0.6f);
      MarkDirtyAndTesselate();
    }
    return true;
  }

  private bool Harvest(IPlayer byPlayer, BEBehaviorMoltenCell cell)
  {
    if (Api.Side == EnumAppSide.Client)
      return true;
    if (Spec is not { } spec)
      return false;

    bool full = cell.CellAmount >= cell.MaxUnitCapacity;
    bool misrun = CastingCellLogic.IsMisrun(full, cell.CellTemperature, spec.MinPourTemp);

    foreach (ItemStack stack in BuildHarvest(spec, cell, full, misrun))
      if (byPlayer.InventoryManager?.TryGiveItemstack(stack) != true)
        Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.6, 0.5));

    cell.ClearContents();
    cell.ClearCapacity();
    _patternCode = null;
    _spec = null;
    _sand = CastingCellLogic.AfterShakeOut;
    ExSounds.Play(Api, Pos, ExSounds.StoneCrush, 0.7f);
    MarkDirtyAndTesselate();
    return true;
  }

  // A clean full cast yields the part; a misrun or a short (under-filled) pour yields recovered scrap of
  // the metal actually in the cavity, conserving its mass.
  private System.Collections.Generic.IEnumerable<ItemStack> BuildHarvest(
    MoldSpec spec,
    BEBehaviorMoltenCell cell,
    bool full,
    bool misrun
  )
  {
    if (full && !misrun)
    {
      spec.Output.Resolve(Api.World, "sand casting cell output for " + Block.Code);
      if (spec.Output.ResolvedItemstack?.Clone() is { } part)
      {
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

  // Shake-out no longer hands sand back, because it no longer takes any away: the cell rakes level and stays
  // full (CastingCellLogic.AfterShakeOut). Sand is a one-off cost at ram-up, and the loop the player repeats
  // is impress -> pour -> shake out.

  #endregion

  #region Rendering

  private void MarkDirtyAndTesselate()
  {
    MarkDirty(true);
    if (Api?.Side == EnumAppSide.Client)
      RebuildRenderer((ICoreClientAPI)Api);
  }

  private void RebuildRenderer(ICoreClientAPI capi)
  {
    _renderer?.Dispose();
    _renderer = null;
    if (Spec is not { } spec || spec.Cavity.Length == 0)
      return;

    float floor = spec.Cavity[0].Y1;
    float height = spec.Cavity[0].Y2 - spec.Cavity[0].Y1;
    // Rotate the molten surface with the block so an asymmetric cavity (e.g. the on-edge heavy plate) lands
    // where the rammed-sand mesh does - the same Y rotation ExMesh.RotateByShape applies to that mesh.
    float rotY = (Block.Shape?.rotateY ?? 0f) * GameMath.DEG2RAD;
    _renderer = new MoltenRenderer(Pos, capi, spec.Cavity, rotY, floor / 16f, height);
    capi.Event.RegisterRenderer(_renderer, EnumRenderStage.Opaque);
    UpdateRenderer();
  }

  private void UpdateRenderer()
  {
    if (_renderer == null || Cell is not { } cell)
      return;
    if (cell.CellAmount <= 0 || cell.CellMetalType.Length == 0)
    {
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

  // The rammed sand is drawn on top of the default brick-shell mesh: the shell renders itself (returning
  // base.OnTesselation keeps it), and this adds the state's filling shape - flat sand once rammed, the
  // pattern's cavity once impressed, half sand after shake-out. The filling shapes carry a texture key named
  // `andesite` for historical reasons (the cell once took any sand and remapped this key per-BE to whichever
  // rock type was rammed in); with one prepared molding sand there is nothing to remap, so the block simply
  // declares that key as the green-sand texture and the shapes resolve straight through it.
  public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
  {
    if (
      Api is ICoreClientAPI capi
      && CastingCellLogic.FillingShape(_sand, HasImpression, Spec?.Shape) is { } shapeRef
      && LoadShape(shapeRef) is { } shape
    )
    {
      tesselator.TesselateShape(
        "sandcastingcell-filling",
        shape,
        out MeshData mesh,
        capi.Tesselator.GetTextureSource(Block)
      );
      // Re-apply the orientation the tesselator bakes into the default shell mesh (the shape is authored
      // in the north frame, same as the base shell), matching BlockEntityMoltenCanal's added-mesh rotate.
      ExMesh.RotateByShape(mesh, Block);
      mesher.AddMeshData(mesh);
    }
    return base.OnTesselation(mesher, tesselator);
  }

  // Resolve a shape reference (e.g. "iwex:casting/cell-filling-base") to its loaded shape asset.
  private Shape? LoadShape(string shapeRef)
  {
    var loc = new AssetLocation(shapeRef);
    return Api.Assets.TryGet(new AssetLocation(loc.Domain, "shapes/" + loc.Path + ".json"))
      ?.ToObject<Shape>();
  }

  #endregion

  #region HUD + serialization

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
  {
    base.GetBlockInfo(forPlayer, dsc);
    if (Cell is not { } cell)
      return;

    if (!HasImpression)
    {
      dsc.AppendLine(
        Lang.Get(_sand == SandLevel.Full ? "iwex:castingcell-needspattern" : "iwex:castingcell-needssand")
      );
      return;
    }

    if (cell.CellAmount <= 0)
    {
      dsc.AppendLine(Lang.Get("iwex:castingcell-ready", PatternName));
      return;
    }

    string state =
      cell.IsHardened ? Lang.Get("iwex:metalstate-hardened")
      : Lang.Get("iwex:metalstate-liquid");
    dsc.AppendLine(
      Lang.Get(
        "iwex:castingcell-casting",
        cell.CellAmount,
        cell.MaxUnitCapacity,
        state,
        ExMeasure.Temperature(cell.CellTemperature)
      )
    );
  }

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
    base.ToTreeAttributes(tree);
    tree.SetInt("cc_sand", (int)_sand);
    if (_patternCode != null)
      tree.SetString("cc_pattern", _patternCode);
  }

  public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
  {
    base.FromTreeAttributes(tree, world);
    _sand = (SandLevel)tree.GetInt("cc_sand");
    _patternCode = tree.GetString("cc_pattern", null);
    // `cc_sandcode` (the rock type of the sand rammed in) is deliberately not read: green sand replaced the
    // per-variant sand, so a cell saved with vanilla sand in it simply returns green sand on shake-out.
    _spec = null; // re-resolve lazily
    if (Api?.Side == EnumAppSide.Client)
      MarkDirtyAndTesselate();
  }

  #endregion
}
