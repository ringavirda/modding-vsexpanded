using System;
using System.Collections.Generic;
using System.Text;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockNetworkMolten.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.BlockNetworkMolten.BlockEntities;

/// <summary>
/// Block entity for all molten-canal blocks. Each block is a self-contained cell holding its own
/// liquid metal (amount, type, temperature); the owning <see cref="MoltenNetwork"/> provides
/// connectivity and drives the per-tick cell-to-cell flow and cooling. A cell solidifies when its
/// metal drops below the melting point, blocking flow until chiselled or broken.
/// </summary>
[BlockEntityRegister]
public class BlockEntityMoltenCanal
  : BlockEntityNetworkNode,
    IChiselableMolten,
    IMoltenCell {
  #region Network
  public override string NetworkType {
    get => "molten";
    set { }
  }

  /// <summary>This cell's metal capacity, in units (from the block's <c>maxUnits</c> attribute).</summary>
  public virtual int MaxUnitCapacity => IwexValues.CanalDefaultUnitCapacity;

  /// <summary>Plain canals are not a flow source; the start cell overrides this to <c>true</c>.</summary>
  public virtual bool IsFlowSource => false;

  /// <summary>Plain canals require the minimum per-tick transfer. Drain fittings (tap, mold pedestal)
  /// override this to <c>true</c> so a run can empty its final sub-minimum dregs into them.</summary>
  public virtual bool AcceptsSubMinimumFlow => false;

  /// <summary>Units of liquid (or, once latched, solidified) metal held by this cell.</summary>
  public int CellAmount { get; protected set; }

  /// <summary>Full code of the metal in this cell, e.g. "game:ingot-iron"; empty when empty.</summary>
  public string CellMetalType { get; protected set; } = "";

  /// <summary>This cell's metal temperature (°C), updated from <see cref="_cellMetalStack"/> each tick.</summary>
  public float CellTemperature => _cellTemperature;

  // Server-side temperature carrier: an ItemStack so VS applies time-based cooling. Null on
  // clients and when empty; rebuilt lazily on load.
  private ItemStack? _cellMetalStack;
  private float _cellTemperature;

  // Client-only predicted fill from in-flight metal, for instant pour feedback.
  private float _pendingFillAmount;

  /// <summary>Whether this cell's metal has solidified.</summary>
  public bool Solidified { get; protected set; } = false;

  /// <summary>
  /// Whether this canal has been clay-sealed into a separator. A sealed canal severs
  /// the network at its position (acts as a manual valve) and renders capped ends on
  /// both of its connector faces.
  /// </summary>
  public bool Sealed { get; protected set; } = false;

  /// <summary>
  /// A sealed node severs connectivity at its position (manual valve), and so does a solidified
  /// one: a hardened cell must not pass metal or pull freshly placed neighbours into itself.
  /// Clearing it with a chisel and hammer (<see cref="ClearSolidified"/>) or breaking it restores
  /// flow.
  /// </summary>
  public override bool IsConnectionBroken() => Sealed || Solidified;

  /// <summary>Whether this cell currently holds liquid (not solidified) metal.</summary>
  public bool HasMoltenMetal => !Solidified && CellAmount > 0f;

  /// <summary>Whether this cell holds no metal at all (neither liquid nor solidified).</summary>
  public bool IsCellEmpty => CellAmount <= 0;

  /// <summary>
  /// Thermal state of this cell's metal (liquid, cooling or hardened), classified against the
  /// melting point. Independent of the <see cref="Solidified"/> latch, so fittings that never clog
  /// (start, tap, pedestal) still report when their metal has cooled. Works on both sides, since the
  /// melting point resolves from the synced <see cref="CellMetalType"/>. Empty cells read liquid.
  /// </summary>
  public MoltenState CellState {
    get {
      if (Api?.World == null || CellAmount <= 0 || CellMetalType.Length == 0)
        return MoltenState.Liquid;
      Item? item = Api.World.GetItem(new AssetLocation(CellMetalType));
      if (item == null)
        return MoltenState.Liquid;
      float meltPoint = MoltenMetal.MeltingPointOf(
        Api.World,
        new ItemStack(item)
      );
      // Shared classifier, so this cell honours the metal's registered per-metal thresholds (or the
      // global defaults) like the stack-based MoltenMetal.StateOf every other fitting uses.
      return MoltenMetal.Classify(_cellTemperature, meltPoint, item.Code);
    }
  }

  /// <summary>
  /// Whether this cell's metal has cooled enough to be chiselled out, below the hardened threshold
  /// times the melting point. A just-solidified cell blocks flow but is still too hot to chip out.
  /// </summary>
  public bool IsHardened => CellState == MoltenState.Hardened;

  #region Incandescent block light
  /// <summary>
  /// Block-light value (0-24) this cell emits from its hot metal. Read by
  /// <see cref="Blocks.BlockMoltenCanal.GetLightHsv"/>; 0 when empty. A hardened cell still glows
  /// until it cools.
  /// </summary>
  public byte GlowLightLevel =>
    CellAmount > 0 ? MoltenMetal.GlowLevel(_cellTemperature) : (byte)0;

  /// <summary>
  /// Re-lights the block via <c>MarkBlockDirty</c> when the glow level differs from
  /// <paramref name="oldGlow"/>. The block id does not change, so the engine will not relight on
  /// its own.
  /// </summary>
  private void RelightIfGlowChanged(byte oldGlow) {
    if (Api != null && GlowLightLevel != oldGlow)
      Api.World.BlockAccessor.MarkBlockDirty(Pos);
  }
  #endregion

  /// <summary>
  /// Whether this cell latches <see cref="Solidified"/> when its metal cools below the melting
  /// point. Plain canals clog; functional fittings (start, tap, pedestal) override this to keep
  /// passing metal even when cool.
  /// </summary>
  protected virtual bool SolidifiesWhenCold => true;

  /// <summary>
  /// Seals or unseals this canal, then re-registers the node so the graph splits around the seal
  /// (or rejoins when removed).
  /// </summary>
  public void SetSealed(bool sealedState) {
    if (Sealed == sealedState)
      return;
    Sealed = sealedState;

    ResyncNetworkNode();
    RefreshOpenConnectorFaces();
    MarkDirty(true);
  }

  /// <summary>
  /// Re-walks the graph at this position so a change to <see cref="IsConnectionBroken"/> (seal, tap
  /// close) splits or rejoins the run immediately. Server-side only.
  /// </summary>
  protected void ResyncNetworkNode() {
    if (
      Api?.Side == EnumAppSide.Server
      && NetworkSystem != null
      && Api.World?.BlockAccessor is { } ba
    ) {
      NetworkSystem.RemoveNode(ba, Pos);
      NetworkSystem.AddNode(ba, Pos, NetworkType);
    }
  }
  #endregion

  #region Per-cell metal API
  /// <summary>
  /// Pushes up to <paramref name="amount"/> units of <paramref name="metal"/> into this cell,
  /// temperature-averaging with any metal already present (same type only). Returns the units
  /// accepted. Server-side.
  /// </summary>
  public int PushMetal(int amount, ItemStack metal, IWorldAccessor world) =>
    PushMetalRaw(
      amount,
      metal.Collectible.Code.ToString(),
      metal.Collectible.GetTemperature(world, metal),
      world
    );

  public int PushMetalRaw(
    int amount,
    string type,
    float temperature,
    IWorldAccessor world
  ) {
    if (Solidified || type.Length == 0)
      return 0;
    if (CellAmount > 0f && CellMetalType != type)
      return 0;

    var accepted = Math.Min(amount, MaxUnitCapacity - CellAmount);
    if (accepted <= 0f)
      return 0;

    Item? item = world.GetItem(new AssetLocation(type));
    if (item == null)
      return 0;

    byte oldGlow = GlowLightLevel;
    float existingTemp = CellAmount > 0f ? _cellTemperature : temperature;
    float total = CellAmount + accepted;
    float newTemp =
      total > 0f
        ? (CellAmount * existingTemp + accepted * temperature) / total
        : temperature;

    if (_cellMetalStack == null || CellMetalType != type)
      _cellMetalStack = new ItemStack(item, 1);

    SetStackTemperature(world, newTemp);
    CellAmount += accepted;
    CellMetalType = type;
    _cellTemperature = newTemp;
    RelightIfGlowChanged(oldGlow);
    MarkDirty();
    return accepted;
  }

  /// <summary>Removes up to <paramref name="amount"/> liquid units from this cell. Returns the amount drained. Server-side.</summary>
  public int DrainMetal(int amount) {
    if (Solidified || CellAmount <= 0f)
      return 0;

    byte oldGlow = GlowLightLevel;
    var actual = Math.Min(amount, CellAmount);
    CellAmount -= actual;
    if (CellAmount <= 0.0001f)
      EmptyCell();

    RelightIfGlowChanged(oldGlow);
    MarkDirty();
    return actual;
  }

  private void EmptyCell() {
    CellAmount = 0;
    CellMetalType = "";
    _cellMetalStack = null;
    _cellTemperature = 0f;
  }

  private void SetStackTemperature(IWorldAccessor world, float temp) {
    if (_cellMetalStack == null)
      return;
    MoltenMetal.SetTemperature(world, _cellMetalStack, temp);
    MoltenMetal.SetCooldownSpeed(
      _cellMetalStack,
      IwexValues.MoltenCooldownSpeed
    );
  }

  /// <summary>
  /// Raises this cell's temperature toward <paramref name="incomingTemp"/> without adding volume:
  /// hot metal poured over an already-full cell, so a continuously fed fitting stays molten instead
  /// of plugging. Returns true when the temperature was raised. Server-side.
  /// </summary>
  public bool SoakHeat(IWorldAccessor world, float incomingTemp) {
    if (
      CellAmount <= 0f
      || _cellMetalStack == null
      || incomingTemp <= _cellTemperature + 1f
    )
      return false;

    byte oldGlow = GlowLightLevel;
    _cellTemperature = incomingTemp;
    SetStackTemperature(world, _cellTemperature);
    RelightIfGlowChanged(oldGlow);
    MarkDirty();
    return true;
  }

  /// <summary>Rebuilds the server temperature carrier after a world load (To/FromTreeAttributes only persist type + temperature).</summary>
  public void EnsureMetalStack(IWorldAccessor world) {
    if (
      _cellMetalStack != null
      || CellMetalType.Length == 0
      || CellAmount <= 0f
    )
      return;

    Item? item = world.GetItem(new AssetLocation(CellMetalType));
    if (item == null)
      return;
    _cellMetalStack = new ItemStack(item, 1);
    SetStackTemperature(world, _cellTemperature);
  }

  /// <summary>
  /// Server per-tick thermal update: refreshes the displayed temperature from the engine's
  /// time-based decay and latches <see cref="Solidified"/> once the metal drops below its melting
  /// point. Driven by <see cref="MoltenNetwork.OnTick"/>.
  /// </summary>
  public void UpdateThermal(IWorldAccessor world) {
    if (CellAmount <= 0f || _cellMetalStack == null)
      return;

    byte oldGlow = GlowLightLevel;
    float temp = MoltenMetal.GetTemperature(world, _cellMetalStack);

    // Re-stamp the live cooldown rate each tick so a MoltenCooldownSpeed config change applies to
    // metal already standing in this cell, not just to the next pour. SetStackTemperature rebases
    // the cooldown baseline to the current temperature (see MoltenMetal.SyncCooldownSpeed), so an
    // unchanged rate is a no-op and a changed rate takes effect from this tick forward.
    SetStackTemperature(world, temp);

    float meltPoint = MoltenMetal.MeltingPointOf(world, _cellMetalStack);

    bool changed = false;
    bool retesselate = false;
    if (Math.Abs(_cellTemperature - temp) >= 1f) {
      _cellTemperature = temp;
      changed = true;
    }
    if (SolidifiesWhenCold && !Solidified && temp < meltPoint) {
      Solidified = true;
      changed = true;
      retesselate = true;
    }

    if (changed)
      MarkDirty(retesselate);

    RelightIfGlowChanged(oldGlow);
  }
  #endregion

  #region Solidified clearing / drops (IChiselableMolten)

  // A solidified cell is the chiselable target and can be chipped out once cooled past the hardened
  // threshold. There is no size cap here, so "too hot" is the only blocked state.
  bool IChiselableMolten.HasChiselableContent => Solidified;
  bool IChiselableMolten.CanChiselOut => Solidified && IsHardened;
  string? IChiselableMolten.ChiselBlockedError => "iwex-canaltoohot";

  ItemStack? IChiselableMolten.ChiselOut() => ClearSolidified();

  /// <summary>
  /// Server-side: chips the hardened metal out of this cell. Empties the cell, lifts the
  /// <see cref="Solidified"/> latch, rebuilds the run so the cell rejoins it, and returns the
  /// recoverable drop. Returns <c>null</c> off-server or when not solidified.
  /// </summary>
  public ItemStack? ClearSolidified() {
    if (Api?.Side != EnumAppSide.Server || !Solidified || !IsHardened)
      return null;

    ItemStack? recovered = GetSolidifiedDrop(Api.World);
    EmptyCell();
    Solidified = false;
    MarkDirty(true);

    // No longer broken: rebuild so this cell re-merges with its neighbours.
    if (NetworkSystem != null && Api.World?.BlockAccessor is { } ba)
      NetworkSystem.RebuildFromRoot(ba, Pos, NetworkType);

    return recovered;
  }

  /// <summary>Whether breaking this canal would let still-liquid metal spill out.</summary>
  public bool WouldSpillOnRemoval() => !Solidified && CellAmount > 0f;

  /// <summary>The solid metal-bit drop for this solidified cell, or <c>null</c> when there is nothing to drop.</summary>
  public ItemStack? GetSolidifiedDrop(IWorldAccessor world) {
    if (!Solidified || CellAmount <= 0f || CellMetalType.Length == 0)
      return null;

    return MoltenChisel.BuildRecovery(
      world,
      new AssetLocation(CellMetalType),
      _cellTemperature,
      (int)CellAmount
    );
  }
  #endregion

  #region Tesselation
  /// <summary>Faces with no canal neighbour, capped with an end-piece mesh; <c>null</c> when none.</summary>
  public BlockFacing[]? OpenConnectorFaces { get; set; }
  private readonly Dictionary<BlockFacing, MeshData> _cachedEndingMeshes = [];
  private MeshData? _baseMesh;

  public override bool OnTesselation(
    ITerrainMeshPool mesher,
    ITesselatorAPI tesselator
  ) {
    // Recompute open faces on every tessellation rather than trusting the cache: on chunk load the
    // cache is populated before cross-boundary neighbours exist, which caps a connected face. The
    // engine re-tessellates edge blocks once the neighbour chunk arrives, so refreshing here
    // self-corrects without a network broadcast.
    RefreshOpenConnectorFaces();

    // Shape and rotation both come from the blocktype, so one cached mesh serves every canal piece of
    // that type. The _baseMesh field below it looked cached and never was - it was assigned on every
    // call, so each re-tesselation re-read the JSON and re-tesselated. The rotation must happen inside
    // the factory: Rotate mutates the mesh in place, so rotating a shared one per call would compound.
    _baseMesh = Api is not ICoreClientAPI capi
      ? null
      : ExMeshCache.GetOrCreate(
        capi,
        Block!,
        "canalbase",
        () => {
          Shape? baseShape = ExMeshCache.LoadShape(
            Api,
            new AssetLocation(
              $"iwex:shapes/molten/canal/{Block?.Variant["type"]}.json"
            )
          );
          if (baseShape == null)
            return null;

          tesselator.TesselateShape(Block, baseShape, out MeshData built);
          if (Block?.Shape != null) {
            float rotX = Block.Shape.rotateX * GameMath.DEG2RAD;
            float rotY = Block.Shape.rotateY * GameMath.DEG2RAD;
            float rotZ = Block.Shape.rotateZ * GameMath.DEG2RAD;

            if (rotX != 0 || rotY != 0 || rotZ != 0)
              built.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), rotX, rotY, rotZ);
          }
          return built;
        }
      );
    if (_baseMesh != null)
      mesher.AddMeshData(_baseMesh);

    // Add rotated ending meshes for open connector faces.
    if (OpenConnectorFaces != null) {
      foreach (var face in OpenConnectorFaces) {
        if (!_cachedEndingMeshes.TryGetValue(face, out var endMesh)) {
          endMesh = MoltenMeshes.TesselateEndCap(Api, tesselator, Block!, face);
          if (endMesh == null)
            continue;
          _cachedEndingMeshes.Add(face, endMesh);
        }
        mesher.AddMeshData(endMesh);
      }
    }

    base.OnTesselation(mesher, tesselator);
    return true;
  }
  #endregion

  #region Rendering
  protected MoltenRenderer? _renderer;
  private string? _cachedMetalType;
  private ItemStack? _cachedMetalStack;

  // Orientation the molten-surface renderer was last built for, so OnExchanged only rebuilds it on a
  // real orientation change.
  private string? _rendererOrientation;

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);

    RefreshOpenConnectorFaces();

    if (api.Side == EnumAppSide.Client) {
      InitRenderer((ICoreClientAPI)api);
      UpdateRenderer();
    }
  }

  /// <summary>
  /// Rebuilds the molten-surface renderer and the capped open faces against the new block's shape.
  /// A wrench rotates the canal via <c>ExchangeBlock</c>, which keeps this BE alive, so
  /// <see cref="Initialize"/> never re-runs and the renderer would otherwise stay bound to the
  /// original orientation's <c>rotateY</c>.
  /// </summary>
  public override void OnExchanged(Block block) {
    base.OnExchanged(block);

    RefreshOpenConnectorFaces();

    if (
      Api is ICoreClientAPI capi
      && block.Variant["orientation"] != _rendererOrientation
    ) {
      _renderer?.Dispose();
      _renderer = null;
      InitRenderer(capi);
      UpdateRenderer();
    }
  }

  private void RefreshOpenConnectorFaces() {
    if (
      Api?.World?.BlockAccessor == null
      || Block is not BlockNetworkNode netBlock
    ) {
      OpenConnectorFaces = null;
      return;
    }

    // A sealed canal caps every connector face regardless of neighbours; that is the visible seal.
    if (Sealed) {
      BlockFacing[]? faces = netBlock.GetConnectorFaces();
      OpenConnectorFaces = faces is { Length: > 0 } ? faces : null;
      return;
    }

    if (NetworkSystem == null) {
      OpenConnectorFaces = null;
      return;
    }

    BlockFacing[] open = NetworkSystem.GetOpenConnectorFaces(
      Api.World.BlockAccessor,
      Pos,
      netBlock
    );
    OpenConnectorFaces = open.Length > 0 ? open : null;
  }

  /// <summary>Creates the molten-fill renderer from the block's fill-quad attributes. Override to customise the fill geometry.</summary>
  protected virtual void InitRenderer(ICoreClientAPI capi) {
    if (Block is not BlockMoltenCanal canal)
      return;

    Cuboidf[] boxes = FillQuads.BoxesFrom(
      canal.FillQuadsByLevel,
      new Cuboidf(7f, 0f, 0f, 9f, 16f, 16f)
    );
    float fillStartY = canal.FillStart / 16f;
    float fillHeightLevels = canal.FillHeight;
    float rotY = (Block.Shape?.rotateY ?? 0f) * GameMath.DEG2RAD;

    _renderer = new MoltenRenderer(
      Pos,
      capi,
      boxes,
      rotY,
      fillStartY,
      fillHeightLevels
    );
    _rendererOrientation = Block.Variant["orientation"];
    capi.Event.RegisterRenderer(_renderer, EnumRenderStage.Opaque);
  }

  /// <summary>Pushes this cell's fill ratio, temperature and metal stack into the renderer. Override to add custom render state.</summary>
  protected virtual void UpdateRenderer() {
    if (_renderer == null)
      return;

    float displayAmount = CellAmount + _pendingFillAmount;
    _renderer.FillRatio =
      MaxUnitCapacity > 0 ? displayAmount / MaxUnitCapacity : 0f;
    _renderer.Temperature = _cellTemperature;

    if (CellMetalType != _cachedMetalType) {
      if (CellMetalType.Length == 0) {
        _cachedMetalStack = null;
        _cachedMetalType = "";
      } else {
        Item? item = Api.World.GetItem(new AssetLocation(CellMetalType));
        _cachedMetalStack = item != null ? new ItemStack(item) : null;
        // Advance the cache key only when the item resolved, so an unregistered item is retried.
        if (item != null)
          _cachedMetalType = CellMetalType;
      }
    }
    _renderer.MetalStack = _cachedMetalStack;
  }

  /// <summary>Client-side: shows in-flight poured metal immediately, before the server confirms.</summary>
  public void ShowPendingFill(float amount) {
    _pendingFillAmount = amount;
    UpdateRenderer();
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

  #region Serialization / info
  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetBool("solidified", Solidified);
    tree.SetBool("sealed", Sealed);
    tree.SetInt("cellAmount", CellAmount);
    tree.SetString("cellMetalType", CellMetalType);
    tree.SetFloat("cellTemperature", _cellTemperature);
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    byte oldGlow = GlowLightLevel;
    Solidified = tree.GetBool("solidified");
    Sealed = tree.GetBool("sealed");
    CellAmount = tree.GetInt("cellAmount");
    CellMetalType = tree.GetString("cellMetalType", "");
    _cellTemperature = tree.GetFloat("cellTemperature");
    // _cellMetalStack is rebuilt lazily server-side in EnsureMetalStack.

    // Invariant: an empty cell is never solidified (also scrubs phantom flags from old saves).
    if (CellAmount <= 0f) {
      Solidified = false;
      CellMetalType = "";
    }

    // Authoritative state has arrived, so drop any client-predicted pour fill.
    _pendingFillAmount = 0f;

    RefreshOpenConnectorFaces();
    UpdateRenderer();

    // New authoritative state on the client: re-light if the glow level moved.
    if (Api?.Side == EnumAppSide.Client)
      RelightIfGlowChanged(oldGlow);
  }

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);

    if (Sealed)
      dsc.AppendLine(Lang.Get("iwex:canal-sealed"));

    if (Solidified) {
      string solidMetalName = MoltenMetal.DisplayName(CellMetalType);
      dsc.AppendLine(
        Lang.Get(
          "iwex:canal-solidified",
          CellAmount,
          MaxUnitCapacity,
          solidMetalName,
          ExMeasure.Temperature(_cellTemperature)
        )
      );
      // Hot solid plug: still glowing and too hot to chip out until it cools below the hardened
      // threshold.
      dsc.AppendLine(
        Lang.Get(IsHardened ? "iwex:canal-chiselready" : "iwex:canal-cooling")
      );
      return;
    }

    if (CellAmount <= 0f) {
      dsc.AppendLine(Lang.Get("iwex:canal-empty"));
    } else {
      string metalName = MoltenMetal.DisplayName(CellMetalType);
      string state = Lang.Get(
        CellState switch {
          MoltenState.Liquid => "iwex:metalstate-liquid",
          MoltenState.Hardened => "iwex:metalstate-hardened",
          _ => "iwex:metalstate-cooling",
        }
      );
      dsc.AppendLine(
        Lang.Get(
          "iwex:canal-content2",
          CellAmount,
          MaxUnitCapacity,
          metalName,
          state,
          ExMeasure.Temperature(_cellTemperature)
        )
      );
    }
  }

  #endregion
}
