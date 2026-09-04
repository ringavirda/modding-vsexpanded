using System;
using ExpandedLib.Metals;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// One molten-metal cell as a composable block-entity behaviour: holds a single cell's metal (amount,
/// type, temperature) and the per-cell operations the molten system drives, so any block entity can be
/// an <see cref="IMoltenCell"/> by composition, including a mega-block footprint cell hosted through
/// <see cref="IFillerHostedBehavior"/>. A hosted cell is not registered in the shared molten network
/// graph: its principal drives flow across the cluster, which may hold a different metal from the
/// outside line. Hosted config is re-applied on load by <see cref="ConfigureFromFiller"/>; only mutable
/// metal state is serialized here. See docs/design/mechanics/molten-network.md.
/// </summary>
[BlockEntityBehaviorRegister]
public class BEBehaviorMoltenCell(BlockEntity blockentity)
  : BlockEntityBehavior(blockentity),
    IMoltenCell,
    IFillerHostedBehavior {
  /// <summary>Fallback capacity (units) when the declaration sets no <c>capacity</c>.</summary>
  public const int DefaultCapacity = 100;

  /// <summary>
  /// The tree-key prefix a cell uses when it declares no <c>key</c>. Save contract: changing it makes
  /// <see cref="FromTreeAttributes"/> read zeros over existing contents.
  /// </summary>
  public const string DefaultKeyPrefix = "mc_";

  #region Config
  private int _capacity = DefaultCapacity;
  private bool _isFlowSource;
  private bool _drainFitting;
  private bool _solidifiesWhenCold = true;
  private float _cooldownSpeed = ExlibValues.MoltenCooldownDefault;

  // One tree is shared by every behaviour a block entity hosts, and a cell holds exactly one metal
  // (PushMetalRaw refuses a second), so a layered vessel such as a crucible holding iron under slag hosts
  // two cells. Distinct prefixes keep the second cell's write from silently landing on the first.
  private string _keyPrefix = DefaultKeyPrefix;

  // Runtime capacity override that wins over the declared/config capacity while set; persisted so it
  // survives a reload. The tree key stays `patcap` as a save contract.
  private int? _runtimeCapacity;

  /// <summary>The principal (controller) block this cell belongs to, or null when hosted standalone.</summary>
  public BlockPos? Principal { get; private set; }

  // Config keys read from the hosted-behaviour properties. flowSource seeds the principal's internal
  // flow, drainFitting takes the final sub-minimum dregs (a mold), solidifies clogs when cold.
  private void ApplyConfig(JsonObject? props) {
    if (props == null)
      return;
    _capacity = props["capacity"].AsInt(_capacity);
    _isFlowSource = props["flowSource"].AsBool(_isFlowSource);
    _drainFitting = props["drainFitting"].AsBool(_drainFitting);
    _solidifiesWhenCold = props["solidifies"].AsBool(_solidifiesWhenCold);
    _cooldownSpeed = props["cooldownSpeed"].AsFloat(_cooldownSpeed);
    _keyPrefix = props["key"].AsString(_keyPrefix);
  }

  /// <inheritdoc/>
  public void ConfigureFromFiller(
    BlockPos? principal,
    BlockFacing? connectorFace,
    JsonObject? properties
  ) {
    Principal = principal;
    ApplyConfig(properties);
  }

  public override void Initialize(ICoreAPI api, JsonObject properties) {
    base.Initialize(api, properties);
    // Hosted-on-filler cells were already configured via ConfigureFromFiller (which runs first); a cell
    // declared directly in a block's behaviours gets its config here instead. Same keys either way.
    ApplyConfig(properties);
  }
  #endregion

  #region State (IMoltenCell)
  /// <inheritdoc/>
  public int CellAmount { get; private set; }

  /// <inheritdoc/>
  public string CellMetalType { get; private set; } = "";

  /// <inheritdoc/>
  public float CellTemperature => _cellTemperature;

  /// <inheritdoc/>
  public int MaxUnitCapacity => _runtimeCapacity ?? _capacity;

  /// <summary>
  /// Overrides this cell's capacity (units) until <see cref="ClearCapacity"/>; a non-positive value
  /// clears the override. The caller guards against changing capacity while metal is present.
  /// </summary>
  public void SetCapacity(int capacity) {
    _runtimeCapacity = capacity > 0 ? capacity : null;
    Blockentity.MarkDirty();
  }

  /// <summary>Drops the runtime capacity override, reverting to the declared/config capacity.</summary>
  public void ClearCapacity() {
    if (_runtimeCapacity == null)
      return;
    _runtimeCapacity = null;
    Blockentity.MarkDirty();
  }

  /// <summary>Hosted cells are never clay-sealed (the seal is a canal-only interaction).</summary>
  public bool Sealed => false;

  /// <inheritdoc/>
  public bool Solidified { get; private set; }

  /// <inheritdoc/>
  public bool IsFlowSource => _isFlowSource;

  /// <inheritdoc/>
  public bool AcceptsSubMinimumFlow => _drainFitting;

  // Server-side temperature carrier: an ItemStack so VS applies its time-based cooling. Null on the
  // client and when empty; rebuilt lazily via EnsureMetalStack after a world load.
  private ItemStack? _cellMetalStack;
  private float _cellTemperature;

  /// <summary>Whether this cell holds no metal at all (neither liquid nor solidified).</summary>
  public bool IsCellEmpty => CellAmount <= 0;

  /// <summary>Whether this cell currently holds liquid (not solidified) metal.</summary>
  public bool HasMoltenMetal => !Solidified && CellAmount > 0;

  /// <summary>Block-light value (0-24) this cell emits from its hot metal; 0 when empty.</summary>
  public byte GlowLightLevel =>
    CellAmount > 0 ? MoltenMetal.GlowLevel(_cellTemperature) : (byte)0;

  /// <summary>
  /// Thermal state (liquid / cooling / hardened) classified against the metal's melting point, honouring
  /// its registered per-metal thresholds. Empty cells read liquid.
  /// </summary>
  public MoltenState CellState {
    get {
      IWorldAccessor? world = Blockentity.Api?.World;
      if (world == null || CellAmount <= 0 || CellMetalType.Length == 0)
        return MoltenState.Liquid;
      Item? item = world.GetItem(new AssetLocation(CellMetalType));
      if (item == null)
        return MoltenState.Liquid;
      float meltPoint = MoltenMetal.MeltingPointOf(world, new ItemStack(item));
      return MoltenMetal.Classify(_cellTemperature, meltPoint, item.Code);
    }
  }

  /// <summary>Whether this cell's metal has cooled past the hardened threshold (chiselable / collectable).</summary>
  public bool IsHardened => CellState == MoltenState.Hardened;
  #endregion

  #region Per-cell metal API
  /// <summary>
  /// Pushes up to <paramref name="amount"/> units of <paramref name="metal"/> into this cell,
  /// temperature-averaging with any same-type metal already present. Returns the amount accepted.
  /// </summary>
  public int PushMetal(int amount, ItemStack metal, IWorldAccessor world) =>
    PushMetalRaw(
      amount,
      metal.Collectible.Code.ToString(),
      metal.Collectible.GetTemperature(world, metal),
      world
    );

  /// <inheritdoc/>
  public int PushMetalRaw(
    int amount,
    string metalType,
    float temperature,
    IWorldAccessor world
  ) {
    if (Solidified || metalType.Length == 0)
      return 0;
    if (CellAmount > 0 && CellMetalType != metalType)
      return 0;

    int accepted = Math.Min(amount, MaxUnitCapacity - CellAmount);
    if (accepted <= 0)
      return 0;

    Item? item = world.GetItem(new AssetLocation(metalType));
    if (item == null)
      return 0;

    float existingTemp = CellAmount > 0 ? _cellTemperature : temperature;
    float total = CellAmount + accepted;
    float newTemp =
      total > 0
        ? (CellAmount * existingTemp + accepted * temperature) / total
        : temperature;

    if (_cellMetalStack == null || CellMetalType != metalType)
      _cellMetalStack = new ItemStack(item, 1);

    SetStackTemperature(world, newTemp);
    CellAmount += accepted;
    CellMetalType = metalType;
    _cellTemperature = newTemp;
    Blockentity.MarkDirty();
    return accepted;
  }

  /// <inheritdoc/>
  public int DrainMetal(int amount) {
    if (Solidified || CellAmount <= 0)
      return 0;

    int actual = Math.Min(amount, CellAmount);
    CellAmount -= actual;
    if (CellAmount <= 0)
      EmptyCell();

    Blockentity.MarkDirty();
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
    MoltenMetal.SetCooldownSpeed(_cellMetalStack, _cooldownSpeed);
  }

  /// <summary>
  /// Raises this cell's temperature toward <paramref name="incomingTemp"/> without adding volume, so a
  /// continuously-fed full fitting stays molten instead of plugging. Returns true if raised.
  /// </summary>
  public bool SoakHeat(IWorldAccessor world, float incomingTemp) {
    if (
      CellAmount <= 0
      || _cellMetalStack == null
      || incomingTemp <= _cellTemperature + 1f
    )
      return false;

    _cellTemperature = incomingTemp;
    SetStackTemperature(world, _cellTemperature);
    Blockentity.MarkDirty();
    return true;
  }

  /// <inheritdoc/>
  public void EnsureMetalStack(IWorldAccessor world) {
    if (_cellMetalStack != null || CellMetalType.Length == 0 || CellAmount <= 0)
      return;

    Item? item = world.GetItem(new AssetLocation(CellMetalType));
    if (item == null)
      return;
    _cellMetalStack = new ItemStack(item, 1);
    SetStackTemperature(world, _cellTemperature);
  }

  /// <inheritdoc/>
  public void UpdateThermal(IWorldAccessor world) {
    if (CellAmount <= 0 || _cellMetalStack == null)
      return;

    float temp = MoltenMetal.GetTemperature(world, _cellMetalStack);
    // Re-stamp the live cooldown rate each tick (rebases the baseline to the current temperature, so an
    // unchanged rate is a no-op) - a config change then applies to metal already standing in the cell.
    SetStackTemperature(world, temp);

    float meltPoint = MoltenMetal.MeltingPointOf(world, _cellMetalStack);

    bool changed = false;
    if (Math.Abs(_cellTemperature - temp) >= 1f) {
      _cellTemperature = temp;
      changed = true;
    }
    if (_solidifiesWhenCold && !Solidified && temp < meltPoint) {
      Solidified = true;
      changed = true;
    }

    if (changed)
      Blockentity.MarkDirty();
  }
  #endregion

  #region Solidified recovery
  /// <summary>Whether breaking this cell would let still-liquid metal spill out.</summary>
  public bool WouldSpillOnRemoval() => !Solidified && CellAmount > 0;

  /// <summary>Returns the solid metal-bit recovery for this cell's contents, or null when empty.</summary>
  public ItemStack? GetRecoveryDrop(IWorldAccessor world) {
    if (CellAmount <= 0 || CellMetalType.Length == 0)
      return null;
    return MoltenChisel.BuildRecovery(
      world,
      new AssetLocation(CellMetalType),
      _cellTemperature,
      CellAmount
    );
  }

  /// <summary>
  /// Empties this cell and clears its solidified latch, e.g. once the principal has collected the
  /// hardened casting. No-op off-server.
  /// </summary>
  public void ClearContents() {
    if (Blockentity.Api?.Side != EnumAppSide.Server)
      return;
    EmptyCell();
    Solidified = false;
    Blockentity.MarkDirty(true);
  }
  #endregion

  #region Serialization (behaviour keys are prefixed to share the BE tree)

  /// <summary>
  /// This cell's tree-key prefix - <see cref="DefaultKeyPrefix"/> unless the declaration sets
  /// <c>key</c>. Distinct prefixes are what let two cells share one block entity's tree.
  /// </summary>
  public string Key => _keyPrefix;

  public override void ToTreeAttributes(ITreeAttribute tree) {
    base.ToTreeAttributes(tree);
    tree.SetInt(_keyPrefix + "amount", CellAmount);
    tree.SetString(_keyPrefix + "type", CellMetalType);
    tree.SetFloat(_keyPrefix + "temp", _cellTemperature);
    tree.SetBool(_keyPrefix + "solid", Solidified);
    if (_runtimeCapacity is { } cap)
      tree.SetInt(_keyPrefix + "patcap", cap);
    else
      tree.RemoveAttribute(_keyPrefix + "patcap");
  }

  public override void FromTreeAttributes(
    ITreeAttribute tree,
    IWorldAccessor worldForResolving
  ) {
    base.FromTreeAttributes(tree, worldForResolving);
    CellAmount = tree.GetInt(_keyPrefix + "amount");
    CellMetalType = tree.GetString(_keyPrefix + "type", "");
    _cellTemperature = tree.GetFloat(_keyPrefix + "temp");
    Solidified = tree.GetBool(_keyPrefix + "solid");
    _runtimeCapacity = tree.HasAttribute(_keyPrefix + "patcap")
      ? tree.GetInt(_keyPrefix + "patcap")
      : null;
    // _cellMetalStack rebuilt lazily server-side in EnsureMetalStack.

    // Invariant: an empty cell is never solidified; also scrubs stale flags from older saves.
    if (CellAmount <= 0) {
      Solidified = false;
      CellMetalType = "";
    }
  }
  #endregion
}
