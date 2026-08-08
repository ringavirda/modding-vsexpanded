using System;
using ExpandedLib.Metals;
using ExpandedLib.Networks;
using ExpandedLib.Registries.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks.Structures;

/// <summary>
/// A molten-metal <em>cell</em> as a composable block-entity behaviour: it holds one cell's worth of
/// liquid (or, once latched, solidified) metal - amount, type, temperature - and exposes the per-cell
/// operations the molten system drives (push / drain / soak / thermal update / solidify / recover).
/// It lifts the <see cref="IMoltenCell"/> contract off <c>BlockEntityMoltenCanal</c> so that an
/// invisible mega-block footprint cell (via <see cref="IFillerHostedBehavior"/>) - or any block entity
/// - can <em>be</em> a molten cell by composition rather than by extending the canal block entity.
/// <para>
/// Unlike the canal, a hosted cell is not auto-registered in the shared molten network graph. The
/// principal that hosts a fixed cluster of them (the sand casting bed; later the ladle / casting cell)
/// drives flow across the cluster itself, keeping it an isolated internal network so the outside line
/// can carry a different metal. Config - capacity, whether it seeds flow, whether it is a drain
/// fitting, whether it clogs when cold, its cooldown rate - comes from the hosted-behaviour
/// <c>properties</c> and is re-applied on load (the filler replays the saved spec through
/// <see cref="ConfigureFromFiller"/>); only the mutable metal state is serialized here.
/// </para>
/// </summary>
[BlockEntityBehaviorRegister]
public class BEBehaviorMoltenCell(BlockEntity blockentity)
  : BlockEntityBehavior(blockentity),
    IMoltenCell,
    IFillerHostedBehavior
{
  /// <summary>Fallback capacity (units) when the declaration sets no <c>capacity</c>.</summary>
  public const int DefaultCapacity = 100;

  /// <summary>
  /// The tree-key prefix a cell uses when it declares no <c>key</c>.
  /// <para>
  /// <b>This value is a save contract and must never change.</b> Every cell that shipped before the
  /// prefix existed - the sand casting bed's runner, mold and basin cells, and the standalone casting
  /// cell - declares no <c>key</c>, so their saved trees carry exactly these names. Changing the default
  /// (to <c>""</c>, to the behaviour's name, to anything tidier) makes <see cref="FromTreeAttributes"/>
  /// look for keys that are not in the tree and read zeros - every casting bed in every existing world
  /// silently empties. <c>MoltenCellPairTests</c> spells the literal key set out rather than deriving it,
  /// so this cannot drift.
  /// </para>
  /// </summary>
  public const string DefaultKeyPrefix = "mc_";

  #region Config
  private int _capacity = DefaultCapacity;
  private bool _isFlowSource;
  private bool _drainFitting;
  private bool _solidifiesWhenCold = true;
  private float _cooldownSpeed = ExlibValues.MoltenCooldownDefault;

  // Why a prefix exists at all: a block entity hands one tree to every behaviour it hosts, and a cell
  // holds exactly one metal by design (PushMetalRaw refuses a second). So a layered vessel - the blast
  // furnace's crucible, iron under slag - has to host two cells, and without distinct keys the second
  // one's write lands on top of the first. Silently: nothing throws, the metal is simply gone on reload.
  private string _keyPrefix = DefaultKeyPrefix;

  // A runtime capacity override that wins over the declared/config one while set. Persisted so it
  // survives a reload.
  //
  // Not only about patterns: a mold rammed into a casting cell is one caller, and the blast furnace's
  // hearth - whose capacity is its band height times the footprint the player actually built - is
  // another. The tree key stays `patcap` even though a hearth block has no pattern rammed into it,
  // because the key is a save contract; the field name is not.
  private int? _runtimeCapacity;

  /// <summary>The principal (controller) block this cell belongs to, or null when hosted standalone.</summary>
  public BlockPos? Principal { get; private set; }

  // Config keys read from the hosted-behaviour properties. flowSource seeds the principal's internal
  // flow, drainFitting takes the final sub-minimum dregs (a mold), solidifies clogs when cold.
  private void ApplyConfig(JsonObject? props)
  {
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
  )
  {
    Principal = principal;
    ApplyConfig(properties);
  }

  public override void Initialize(ICoreAPI api, JsonObject properties)
  {
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
  /// Overrides this cell's capacity at runtime. Wins over the declared and config capacity until
  /// <see cref="ClearCapacity"/> is called. The caller guards against changing capacity while metal is
  /// present.
  /// <para>
  /// Two callers, and they are not the same kind of thing: a mold rammed into a casting cell sets the
  /// capacity from the pattern's spec (cleared on shake-out), and a furnace hearth sets it from its band
  /// height times the footprint the player built. The override is deliberately generic - do not describe
  /// it as "the pattern capacity" again.
  /// </para>
  /// </summary>
  public void SetCapacity(int capacity)
  {
    _runtimeCapacity = capacity > 0 ? capacity : null;
    Blockentity.MarkDirty();
  }

  /// <summary>Drops the runtime capacity override, reverting to the declared/config capacity.</summary>
  public void ClearCapacity()
  {
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
  /// Thermal state (liquid / cooling / hardened) classified against the metal's melting point,
  /// honouring the metal's registered per-metal thresholds. Empty cells read liquid.
  /// </summary>
  public MoltenState CellState
  {
    get
    {
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
  )
  {
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
  public int DrainMetal(int amount)
  {
    if (Solidified || CellAmount <= 0)
      return 0;

    int actual = Math.Min(amount, CellAmount);
    CellAmount -= actual;
    if (CellAmount <= 0)
      EmptyCell();

    Blockentity.MarkDirty();
    return actual;
  }

  private void EmptyCell()
  {
    CellAmount = 0;
    CellMetalType = "";
    _cellMetalStack = null;
    _cellTemperature = 0f;
  }

  private void SetStackTemperature(IWorldAccessor world, float temp)
  {
    if (_cellMetalStack == null)
      return;
    MoltenMetal.SetTemperature(world, _cellMetalStack, temp);
    MoltenMetal.SetCooldownSpeed(_cellMetalStack, _cooldownSpeed);
  }

  /// <summary>
  /// Raises this cell's temperature toward <paramref name="incomingTemp"/> without adding volume - hot
  /// metal poured over an already-full cell, so a continuously-fed fitting stays molten instead of
  /// plugging. Returns true if raised.
  /// </summary>
  public bool SoakHeat(IWorldAccessor world, float incomingTemp)
  {
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
  public void EnsureMetalStack(IWorldAccessor world)
  {
    if (_cellMetalStack != null || CellMetalType.Length == 0 || CellAmount <= 0)
      return;

    Item? item = world.GetItem(new AssetLocation(CellMetalType));
    if (item == null)
      return;
    _cellMetalStack = new ItemStack(item, 1);
    SetStackTemperature(world, _cellTemperature);
  }

  /// <inheritdoc/>
  public void UpdateThermal(IWorldAccessor world)
  {
    if (CellAmount <= 0 || _cellMetalStack == null)
      return;

    float temp = MoltenMetal.GetTemperature(world, _cellMetalStack);
    // Re-stamp the live cooldown rate each tick (rebases the baseline to the current temperature, so an
    // unchanged rate is a no-op) - a config change then applies to metal already standing in the cell.
    SetStackTemperature(world, temp);

    float meltPoint = MoltenMetal.MeltingPointOf(world, _cellMetalStack);

    bool changed = false;
    if (Math.Abs(_cellTemperature - temp) >= 1f)
    {
      _cellTemperature = temp;
      changed = true;
    }
    if (_solidifiesWhenCold && !Solidified && temp < meltPoint)
    {
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
  public ItemStack? GetRecoveryDrop(IWorldAccessor world)
  {
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
  /// Empties this cell and clears its solidified latch (e.g. after the principal has collected the
  /// hardened casting). No-op off-server.
  /// </summary>
  public void ClearContents()
  {
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
  /// <c>key</c>. It is what lets two cells share one block entity's tree; see
  /// <see cref="Blocks.Structures.MoltenCellHost.MoltenCell"/> for how a consumer addresses one.
  /// </summary>
  public string Key => _keyPrefix;

  public override void ToTreeAttributes(ITreeAttribute tree)
  {
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
  )
  {
    base.FromTreeAttributes(tree, worldForResolving);
    CellAmount = tree.GetInt(_keyPrefix + "amount");
    CellMetalType = tree.GetString(_keyPrefix + "type", "");
    _cellTemperature = tree.GetFloat(_keyPrefix + "temp");
    Solidified = tree.GetBool(_keyPrefix + "solid");
    _runtimeCapacity = tree.HasAttribute(_keyPrefix + "patcap")
      ? tree.GetInt(_keyPrefix + "patcap")
      : null;
    // _cellMetalStack rebuilt lazily server-side in EnsureMetalStack.

    // Invariant: an empty cell is never solidified (also scrubs phantom flags from old saves).
    if (CellAmount <= 0)
    {
      Solidified = false;
      CellMetalType = "";
    }
  }
  #endregion
}
