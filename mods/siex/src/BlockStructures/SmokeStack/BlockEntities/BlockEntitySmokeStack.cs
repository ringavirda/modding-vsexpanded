using System.Text;
using ExpandedLib;
using ExpandedLib.Blocks;
using ExpandedLib.Networks;
using ExpandedLib.Structures;
using ExpandedLib.Catalogues;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Helpers;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Registries;
using IronIndustryExpanded.BlockNetworkPipe;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace SteelIndustryExpanded.BlockStructures.SmokeStack.BlockEntities;

/// <summary>
/// Block entity for the smoke-stack multiblock. Registers as a gas-network node and acts as a sink:
/// each production tick it consumes gas from the connected network and vents it as smoke.
/// </summary>
[BlockEntityRegister]
public class BlockEntitySmokeStack
  : BlockEntityMultiblockMachine,
    INetworkNode,
    IPipeNode {
  [Persist("lastConsumedAmount")]
  private float _lastConsumedAmount;
  private BlockNetworkModSystem? _system;
  private long _lastVentSoundMs;

  public override void Initialize(ICoreAPI api) {
    base.Initialize(api);
    _system = api.ModLoader.GetModSystem<BlockNetworkModSystem>();

    // Register this position in the gas graph. BlockEntityNetworkNode does this automatically, but
    // this class derives from BlockEntityMultiblockStructure, so it must register itself.
    if (api.Side == EnumAppSide.Server && _system.GetNetworkAt(Pos) == null)
      _system.AddNode(api.World.BlockAccessor, Pos, "pipe");
  }

  /// <summary>
  /// Drops the stack out of the gas graph, mirroring the <see cref="Initialize"/> registration this class
  /// has to do by hand.
  /// removal-only teardown: a chunk unload leaves the stack plumbed in, so deregistering there would cut
  /// the exhaust run every time a player walked away. The base clears the listeners and the build outline
  /// on unload, which is the whole of what an unload owes.
  /// </summary>
  public override void OnBlockRemoved() {
    if (Api?.Side == EnumAppSide.Server)
      _system?.RemoveNode(Api.World.BlockAccessor, Pos);
    base.OnBlockRemoved();
  }

  #region INetworkNode

  /// <inheritdoc/>
  public string NetworkType => "pipe";

  /// <inheritdoc/>
  public string? Orientation { get; set; }

  /// <inheritdoc/>
  public string[] PossibleOrientations { get; set; } = [];

  /// <inheritdoc/>
  public bool HasConnectorAt(BlockFacing face) =>
    face.Code.StartsWith(Orientation ?? "n");

  /// <inheritdoc/>
  public void OnOpenConnectorsChanged(BlockFacing[] openFaces) { }

  /// <summary>No-op: the stack draws gas through <see cref="TryConsume"/> and does not leak.</summary>
  public void OnLeak(
    BlockFacing[] leakingFaces,
    bool isLiquid,
    float intensity
  ) { }

  /// <summary>No-op: the stack reads the network directly and caches no local state.</summary>
  public void OnNetworkUpdate(object? state) { }

  #endregion

  #region IPipeNode

  /// <summary>Delegates to the network to complete <see cref="IPipeNode"/>; unused, the stack only
  /// vents.</summary>
  public bool TryProduce(
    float volume,
    float temperature,
    string gasType = "Air",
    float maxOutputPressure = 1.0f,
    bool bypassLeakCap = false
  ) {
    if (_system?.GetNetworkAt(Pos) is not PipeNetwork gasNet)
      return false;
    return gasNet.TryProduceGas(
      volume,
      temperature,
      gasType,
      Api.World.BlockAccessor,
      maxOutputPressure: maxOutputPressure,
      bypassLeakCap: bypassLeakCap
    );
  }

  /// <summary>Consumes up to <paramref name="requestedVolume"/> L from the gas network; returns the amount consumed.</summary>
  public float TryConsume(float requestedVolume) {
    if (_system?.GetNetworkAt(Pos) is not PipeNetwork gasNet)
      return 0f;
    return gasNet.TryConsumeGas(requestedVolume, Api.World.BlockAccessor);
  }

  /// <inheritdoc/>
  public float Temperature =>
    _system?.GetNetworkAt(Pos) is PipeNetwork gasNet
      ? gasNet.State?.Temperature ?? ExlibValues.AmbientTemperature
      : ExlibValues.AmbientTemperature;

  /// <inheritdoc/>
  public string Medium =>
    _system?.GetNetworkAt(Pos) is PipeNetwork gasNet
      ? gasNet.State?.MediumType ?? ""
      : "";

  /// <inheritdoc/>
  public bool IsLiquid => ExLiquids.Taxonomy.IsLiquid(Medium);

  /// <inheritdoc/>
  public float Pressure =>
    _system?.GetNetworkAt(Pos) is PipeNetwork gasNet
      ? gasNet.State?.Pressure ?? 0f
      : 0f;

  /// <inheritdoc/>
  public float Volume =>
    _system?.GetNetworkAt(Pos) is PipeNetwork gasNet
      ? gasNet.State?.Volume ?? 0f
      : 0f;

  /// <inheritdoc/>
  public float MaxVolume =>
    _system?.GetNetworkAt(Pos) is PipeNetwork gasNet
      ? gasNet.State?.MaxVolume ?? 0f
      : 0f;

  #endregion

  #region Structure orientation

  protected override void UpdateStructureRotation() {
    if (Block == null)
      return;

    // Single-letter orientation codes share the side-angle convention.
    SetStructureAngle(
      ExOrientation.AngleFromSide(Block.Variant["orientation"])
    );
  }

  protected override string GetIncompleteMessage(int missingCount) =>
    Lang.Get(SiexLang.StructureIncompleteCount, missingCount);

  protected override string GetCompleteMessage() =>
    Lang.Get(SiexLang.SmokestackComplete);

  #endregion

  #region Production tick

  protected override void OnProductionTick(float dt) {
    if (!StructureComplete)
      return;

    var gasIntakeVolume = SiexValues.SmokestackGasIntakeVolume;

    // Read the medium before drawing: TryConsume can empty the pool and clear its label. It refuses
    // a liquid run, so the medium here is always exhaust, steam or air.
    string medium = Medium;
    float consumed = TryConsume(gasIntakeVolume);

    if (System.Math.Abs(_lastConsumedAmount - consumed) > 0.001f) {
      _lastConsumedAmount = consumed;
      MarkDirty(true);
    } else {
      _lastConsumedAmount = consumed;
    }

    if (_lastConsumedAmount <= 0)
      return;

    SpawnSmokeParticles(medium);
    // Soft draught of exhaust venting up the stack.
    ExSounds.PlayThrottled(
      Api,
      Pos,
      ExSounds.Fire,
      ref _lastVentSoundMs,
      6000,
      0.3f,
      32f
    );
  }

  private void SpawnSmokeParticles(string medium) {
    // Colour by what's venting: soot for exhaust, vapour for steam, nothing for air.
    if (ExParticles.GasColor(medium, ventAir: false) is not int color)
      return;

    // The column sits over the cell in front of the stack - local (0,*,1) rotated by orientation.
    Vec3i d = ExOrientation.RotateOffset(0, 0, 1, _currentAngle);
    int dx = d.X,
      dz = d.Z;

    Vec3d minPos = new(Pos.X + dx + 0.1, Pos.Y + 1.0, Pos.Z + dz + 0.1);
    Vec3d maxPos = new(Pos.X + dx + 0.9, Pos.Y + 13.0, Pos.Z + dz + 0.9);

    ExParticles.RisingPlume(
      Api.World,
      color,
      minPos,
      maxPos,
      new Vec3f(-0.5f, 1f, -0.5f),
      new Vec3f(0.5f, 3f, 0.5f),
      _lastConsumedAmount * 15f,
      _lastConsumedAmount * 25f,
      1.5f,
      -0.1f,
      0.5f,
      1.5f,
      new EvolvingNatFloat(EnumTransformFunction.LINEAR, -200f),
      new EvolvingNatFloat(EnumTransformFunction.LINEAR, 2)
    );
  }

  #endregion

  #region HUD

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    if (!StructureComplete) {
      dsc.AppendLine(Lang.Get(SiexLang.StructureIncomplete));
      return;
    }
    dsc.AppendLine(
      Lang.Get(
        "siex:smokestack-info-consuming",
        ExMeasure.Volume(_lastConsumedAmount, "F1")
      )
    );
  }

  #endregion

  #region Serialization

  // "orientation" is written unconditionally, even when null - unlike ExBlockState's string helper (and
  // [Persist]'s string primitive), which skip a null value - so it stays a Tree.
  protected override void DeclareState(ExBlockState state) {
    state.Tree(
      "orientation",
      tree => tree.SetString("orientation", Orientation),
      (tree, _) => Orientation = tree.GetString("orientation")
    );
    state.Tree(
      "possibleOrientations",
      tree => tree.SetStrings("possibleOrientations", PossibleOrientations),
      (tree, _) =>
        // The old encoding of this key was a JSON string; it is read second so worlds saved before the
        // cutover keep their rotation choices, and converts on the stack's next save.
        PossibleOrientations =
          tree.GetStrings("possibleOrientations")
          ?? ExTree.SafeDeserialize(
            tree.GetString("possibleOrientations"),
            PossibleOrientations
          )
    );
  }

  #endregion
}
