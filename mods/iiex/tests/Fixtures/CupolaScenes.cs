using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Machines;
using ExpandedLib.Industry.Metals;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockNetworkMolten.BlockEntities;
using IronIndustryExpanded.BlockNetworkPipe;
using IronIndustryExpanded.BlockNetworkPipe.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.BlockStructures.Products.BlockEntities;
using IronIndustryExpanded.Items;
using IronIndustryExpanded.Tests;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Drives the cupola furnace headlessly (docs/design/iiex.md, the scrap re-melter). Stands up the cupola's
/// peripherals - a single-column shaft, one tuyere, and the two taps with their canals - around a real
/// <see cref="BlockEntityCupolaFurnace"/> (a <see cref="BlockEntityShaftFurnace"/>) and pumps the gated
/// <c>OnProductionTick</c>, so a scene can charge, light and blow a furnace through ignition, melting,
/// tapping and extinguish. Timers are fast-forwarded so the multi-minute melt is reachable in a test.
/// </summary>
internal sealed class CupolaRig {
  public readonly TestWorld World;
  public readonly BlockEntityCupolaFurnace Furnace;
  public BlockEntityMoltenCanalStart? CastIronCanal { get; private set; }
  public BlockEntityMoltenCanalStart? SlagCanal { get; private set; }

  /// <summary>The standing structure, for tests that address cells by their authored local offsets.</summary>
  public readonly StructureRig Structure;

  private readonly BlockPos _pos = new(0, 16, 0);
  private readonly PipeNetwork[] _tuyeres;
  private readonly BurdenMix? _burden;
  private readonly string _chargeCode;

  /// <summary>The fuel this scene lays its rounds with. See <see cref="WithFuel"/>.</summary>
  private string _fuelCode = CokeCode;
  private float _blastTemp = -1f;
  private float _blastPressure = 5f;

  /// <param name="charge">Scrap metal units laid into the shaft's single column. 0 fills it to capacity, a
  /// negative value leaves it empty. Charge is counted in metal units, not items, so one pile block holds
  /// <c>CupolaChargeMetalUnitsPerBlock</c> (3 000) rather than 32.</param>
  /// <param name="burden">Composition stamped on the charge. Its coke fraction drives the heat balance.</param>
  /// <param name="chargeCode">Item path in the iiex domain that the shaft piles hold. Pass <c>burden</c> to
  /// charge material the cupola burns but never converts, which exercises the family gate.</param>
  public CupolaRig(
    int charge = 0,
    BurdenMix? burden = null,
    string chargeCode = "pigchunk"
  ) {
    _burden = burden ?? new BurdenMix(60f, 5f, 35f);
    _chargeCode = chargeCode;
    World = new TestWorld();
    // Server-side, as the cold-furnace rig is: this drives the production tick, and a world that
    // identifies as neither side skips every `Side == Server` branch in it without saying so.
    World.World.Side.Returns(EnumAppSide.Server);

    // The taps resolve their molten carrier through MetalRegistry: cast iron -> iiex:ingot-castiron when
    // the metal is registered, else the game:ingot-castiron convention. Register both codes (and both slag
    // codes) so GetItem resolves whatever the tick asks for, independent of process-wide registry state.
    World.RegisterItem("iiex:ingot-castiron", 1200f);
    World.RegisterItem("game:ingot-castiron", 1200f);
    World.RegisterItem("iiex:slag", 1200f);
    World.RegisterItem("game:ingot-slag", 1200f);
    // The block the melt pools into, with the two cells that hold the metal.
    HearthRig.Register(World, "iiex:hearthmetal-castiron", 70);

    // The charge pile and its entity class, so the cupola's own SyncChargeBlocks materialises real
    // BlockEntityChargePile windows onto its column. The factory is needed as well as the block - see
    // ColdBlastFurnaceScenes.
    World.RegisterBlockEntityFactory(
      "iiex.BlockEntityChargePile",
      () => new BlockEntityChargePile()
    );
    Block chargePile = TestBlocks.Configure(
      new Block(),
      BlockChargePile.PileCode.ToShortString(),
      71,
      ("type", "chargepile")
    );
    chargePile.EntityClass = "iiex.BlockEntityChargePile";
    World.Register(chargePile);
    World.RegisterItem("iiex:" + chargeCode);

    World.RegisterNetwork("pipe", s => new PipeNetwork(s));

    Furnace = new BlockEntityCupolaFurnace {
      Pos = _pos,
      Block = TestBlocks.Configure(
        new Block(),
        "iiex:furnace-cupolacore-tier1-n",
        1,
        ("side", "north")
      ),
    };
    World.Place(_pos, Furnace.Block, Furnace);
    World.Attach(Furnace);

    // The cupola's shipped layout, rotated to its facing (north -> 0). Peripherals go in first, then the
    // rig fills the shell and the furnace completes itself rather than being forced complete - see
    // StructureRig.
    Structure = StructureRig.Around(
      World,
      Furnace,
      BlockCupolaFurnaceCore.Definitions("iiex").Single(),
      angle: 0
    );

    // A single tuyere on the north face, on its own blast network. The inlet cell is (0,2,-1): a course
    // above the well, so the blast enters the bottom of the shaft column and the melt sits above the metal
    // it collects. Layer 1's north row is solid brick, so (0,1,-1) leaves the structure one cell short.
    _tuyeres = [Tuyere(Structure.Cell(0, 2, -1), 40, "n")];

    Structure.Complete();

    // Charged after the structure stands: the core owns no columns until its layout has arrived, so a push
    // before completion lands nowhere and does so silently. 0 fills the shaft; a negative charge leaves it
    // empty for scenes that lay their own charge afterwards (see ChargeWithoutCoke).
    if (charge >= 0)
      Lay(charge > 0 ? charge : ShaftCapacityUnits);
  }

  /// <summary>Total units the shaft can hold: each column's cell count times the charge block quantum.
  /// What <c>charge: 0</c> lays.</summary>
  public int ShaftCapacityUnits {
    get {
      int total = 0;
      foreach (var (x, z) in Furnace.ShaftColumns.Keys)
        total += Furnace.ColumnCapacity(x, z);
      return total;
    }
  }

  /// <summary>
  /// Lays <paramref name="total"/> units of the scene's charge into the shaft, clamped to each column's
  /// capacity. The walk is written over the column set rather than a literal cell so it matches the blast
  /// furnace's.
  /// </summary>
  /// <param name="rounds">True lays real rounds - a fuel course, then a metal course, in the scene's own
  /// fuel (<see cref="WithFuel"/>). False lays charge alone, producing a shaft that cannot burn.</param>
  private void Lay(int total, bool rounds = true) {
    if (total <= 0)
      return;
    int remaining = total;
    var keys = new List<(int X, int Z)>(Furnace.ShaftColumns.Keys);
    keys.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));

    foreach (var (x, z) in keys) {
      if (remaining <= 0)
        break;
      ChargeColumn column = Furnace.ChargeColumnAt(x, z)!;
      int give = System.Math.Min(
        remaining,
        Furnace.ColumnCapacity(x, z) - column.TotalUnits
      );
      if (give <= 0)
        continue;
      if (rounds)
        LayRounds(column, give);
      else
        // One fixed temperature, so the charge arrives as a single band rather than a run of slivers the
        // salvage assertions cannot address.
        column.Push(
          $"iiex:{_chargeCode}",
          give,
          ChargeTemp,
          _burden ?? default
        );
      remaining -= give;
    }

    Furnace.SyncChargeBlocks();
  }

  /// <summary>
  /// Lays <paramref name="units"/> into one column as real rounds - a fuel course, then a metal course - at
  /// the scene's fuel fraction, in whichever fuel <see cref="WithFuel"/> armed. Carbon comes from fuel bands
  /// and nowhere else, so a column laid without a fuel course cannot light. Fuel is counted in the cupola's
  /// own metal units; <c>BlockEntityShaftFurnace.ChargeUnitScale</c> lets the shared raceway constants work
  /// at either scale (docs/design/layered-charge.md). A round's fuel course is a fixed volume, not a fixed
  /// carbon quantity, so a charcoal course fills the same <c>fuelPerRound</c> units as a coke one at half
  /// the carbon and <see cref="FuelBandUnits"/> and <see cref="CarbonUnits"/> diverge.
  /// </summary>
  private void LayRounds(ChargeColumn column, int units) {
    int perRound = System.Math.Max(2, Furnace.ChargeUnitsPerBlock);
    float fuelFrac = (_burden ?? default).FuelFrac;
    int fuelPerRound = System.Math.Max(
      1,
      (int)(perRound * (fuelFrac > 0f ? fuelFrac : 0.35f))
    );

    int left = units;
    while (left > 0) {
      int fuel = System.Math.Min(fuelPerRound, left);
      column.Push(_fuelCode, fuel, ChargeTemp, default);
      left -= fuel;
      if (left <= 0)
        break;

      int metal = System.Math.Min(perRound - fuelPerRound, left);
      column.Push($"iiex:{_chargeCode}", metal, ChargeTemp, _burden ?? default);
      left -= metal;
    }
  }

  /// <summary>The fuel the scenes lay their rounds with by default. A scene may lay any registered fuel -
  /// see <see cref="ChargeWithFuel"/>.</summary>
  public const string CokeCode = "game:coke";

  /// <summary>Vanilla charcoal: the pre-coke reductant, worth half of coke per unit.</summary>
  public const string CharcoalCode = "game:charcoal";

  /// <summary>The temperature the scene's charge is laid at - one fixed value, because
  /// <see cref="ChargeColumn.Push"/> merges only within 1 °C.</summary>
  private const float ChargeTemp = 20f;

  /// <summary>The mix the scene stamped on its charge, for cases that check the furnace reads the fuel
  /// bands rather than the stamp.</summary>
  public BurdenMix ChargedMix => _burden ?? default;

  /// <summary>Alias for <see cref="FuelBandUnits"/>, kept because the scenarios read it. On a coke-charged
  /// cupola it also equals <see cref="CarbonUnits"/>; on a charcoal-charged one it does not.</summary>
  public int CokeUnits => FuelBandUnits;

  /// <summary>
  /// Charge units standing in fuel bands of any material - the column volume the fuel occupies. Asked
  /// through the production predicate <c>IsFuelCode</c> rather than against a coke literal, which reads
  /// zero on a charcoal-charged shaft. Not the same number as <see cref="CarbonUnits"/>: a charcoal band
  /// occupies a band's worth of column and carries half a band's worth of carbon.
  /// </summary>
  public int FuelBandUnits {
    get {
      int units = 0;
      foreach (ChargeColumn column in Furnace.ShaftColumns.Values)
        foreach (ChargeSegment segment in column.Segments)
          if (BlockEntityFurnaceCore.IsFuelCode(segment.Material))
            units += segment.Units;
      return units;
    }
  }

  /// <summary>
  /// Carbon standing in the shaft, in coke units - each fuel band's volume times its own
  /// <c>CarbonPerUnit</c>. The raceway spends carbon rather than bands, so this is what sets campaign
  /// length: a charcoal cupola of the same volume goes out twice as soon.
  /// </summary>
  public float CarbonUnits {
    get {
      float carbon = 0f;
      foreach (ChargeColumn column in Furnace.ShaftColumns.Values)
        foreach (ChargeSegment segment in column.Segments)
          carbon +=
            segment.Units
            * BlockEntityFurnaceCore.CarbonPerUnit(segment.Material);
      return carbon;
    }
  }

  /// <summary>Fills the shaft with charge alone - no fuel bands: a full cupola that cannot burn.</summary>
  public CupolaRig ChargeWithoutCoke(int units = 0) {
    Lay(units > 0 ? units : ShaftCapacityUnits, rounds: false);
    return this;
  }

  /// <summary>
  /// Switches which fuel the scene's rounds are laid with - <see cref="CokeCode"/> by default. Must be
  /// called before charging: the rig lays its charge in the constructor, so a scene wanting charcoal passes
  /// <c>charge: -1</c> and charges itself, or uses <see cref="ChargeWithFuel"/>.
  /// </summary>
  public CupolaRig WithFuel(string fuelCode) {
    _fuelCode = fuelCode;
    return this;
  }

  /// <summary>Lays a full shaft of rounds in <paramref name="fuelCode"/>.</summary>
  public CupolaRig ChargeWithFuel(string fuelCode, int units = 0) {
    WithFuel(fuelCode);
    Lay(units > 0 ? units : ShaftCapacityUnits);
    return this;
  }

  /// <summary>
  /// The blast-fed tuyere cell: the real <c>iiex:furnace-tuyere-*</c> block rather than a generic pipe. Both
  /// behave identically as network nodes, but only the tuyere satisfies the furnace layout.
  /// </summary>
  /// <param name="orientation">Connector face, pinned by the layout: <c>n</c> for a cell in the north wall,
  /// <c>s</c> for one in the south. A tuyere facing into the hearth leaves the structure incomplete.</param>
  private PipeNetwork Tuyere(BlockPos pos, int id, string orientation) {
    var pipe = PipeTestWorld.MakeTuyere(id, orientation);
    var be = new BlockEntityTuyere { Pos = pos.Copy(), Block = pipe };
    World.Place(pos, pipe, be);
    World.Attach(be);
    World.AddNode(pos, "pipe");
    ReflectionHelpers.SetProperty(be, nameof(be.NetworkSystem), World.Networks);
    return (PipeNetwork)World.NetworkAt(pos)!;
  }

  /// <summary>Turns the blast on: air at <paramref name="temp"/> and <paramref name="pressure"/> at the tuyere.</summary>
  public CupolaRig FeedBlast(float temp = 950f, float pressure = 5f) {
    _blastTemp = temp;
    _blastPressure = pressure;
    return this;
  }

  /// <summary>Cuts the blast off.</summary>
  /// <summary>
  /// Lights the furnace - through the cast-iron tap when one has been stood up, otherwise straight at the
  /// furnace. A shaft does not catch by itself; see <see cref="BlowInRig"/>.
  /// </summary>
  public CupolaRig BlowIn() {
    BlowInRig.BlowIn(World, Furnace, CastIronTap);
    return this;
  }

  public CupolaRig CutBlast() {
    _blastTemp = -1f;
    return this;
  }

  // Both taps stand on literal structure-local cells rather than on `Furnace.MetalTapPos`/`SlagTapPos`, so
  // the scene states the drawing independently of the roles and a swapped `T`/`S` pair is caught by the
  // metal-type assertions. The cells are the cupola's own `I` at (-1,1,0) and `S` at (1,1,0), named through
  // the structure so they follow its rotation.
  //
  // Tap facing is inverted: `TryPourMetal` spouts at `facing.Opposite`, so the west-wall iron notch is
  // `-east` and the east-wall cinder notch is `-west`.

  /// <summary>Places the cast-iron tap on the drawing's own <c>I</c> cell - the west wall, so the block
  /// faces east - with a canal start under its spout, open.</summary>
  /// <summary>The cast-iron tap, once <see cref="WithCastIronTapAndCanal"/> has stood one up - what the
  /// blow-in reaches its flame through.</summary>
  public BlockEntityFurnaceTap? CastIronTap { get; private set; }

  public CupolaRig WithCastIronTapAndCanal() {
    CastIronCanal = TapAndCanal(
      Structure.Cell(-1, 1, 0),
      BlockFurnaceTap.IronType,
      "east",
      30,
      31
    );
    return this;
  }

  /// <summary>Places the slag tap on the drawing's own <c>S</c> cell - the east wall, so the block faces
  /// west - with a canal start under its spout, open. Level with the metal tap, not above it.</summary>
  public CupolaRig WithSlagTapAndCanal() {
    SlagCanal = TapAndCanal(
      Structure.Cell(1, 1, 0),
      BlockFurnaceTap.SlagType,
      "west",
      32,
      33
    );
    return this;
  }

  private BlockEntityMoltenCanalStart TapAndCanal(
    BlockPos tapPos,
    string type,
    string side,
    int tapId,
    int canalId
  ) {
    var tap = new BlockEntityFurnaceTap {
      Pos = tapPos.Copy(),
      // The real block class, not a stand-in: the blow-in gesture runs through its interaction, so a plain
      // Block here would make every scene with a tap take the fixture's fallback instead.
      Block = TestBlocks.Configure(
        new BlockFurnaceTap(),
        $"iiex:furnace-{type}-{side}",
        tapId,
        ("type", type),
        ("side", side)
      ),
    };
    World.Place(tapPos, tap.Block, tap);
    World.Attach(tap);
    tap.SetPlugged(false); // open
    if (type == BlockFurnaceTap.IronType)
      CastIronTap = tap;

    // The tap pours into the canal start at Pos + facing.Opposite + down (its spout foot).
    BlockPos canalPos = tapPos
      .AddCopy(BlockFacing.FromCode(side).Opposite)
      .DownCopy();
    var canal = new BlockEntityMoltenCanalStart {
      Pos = canalPos.Copy(),
      Block = TestBlocks.Configure(
        new Block(),
        "smex:moltencanalstart-ns",
        canalId,
        ("type", "start"),
        ("orientation", "ns")
      ),
    };
    World.Place(canalPos, canal.Block, canal);
    World.Attach(canal);
    return canal;
  }

  private BlockPos Global(int x, int y, int z) =>
    (BlockPos)ReflectionHelpers.Invoke(Furnace, "GetGlobalPos", x, y, z)!;

  /// <summary>Advances the furnace tick <paramref name="ticks"/> times, re-feeding blast each tick.</summary>
  public CupolaRig Tick(int ticks = 1) {
    for (int i = 0; i < ticks; i++) {
      if (_blastTemp >= 0f)
        foreach (var net in _tuyeres) {
          net.TryProduceGas(
            150f,
            _blastTemp,
            "Air",
            World.Accessor,
            maxOutputPressure: _blastPressure
          );
          net.BroadcastUpdate(World.Accessor);
        }
      Furnace.GetBehavior<BEBehaviorProductionMachine>().DriveProductionTick(1f);
    }
    return this;
  }

  #region Fast-forward + accessors

  // A cupola is a shaft furnace: it recomputes what it is doing from its charge every tick, and `State` has
  // no setter (`FurnaceBranchGuards.NoFurnaceExposesASettableState`). A scene therefore arranges a melting
  // cupola by charging, lighting and blowing it, through `RunUntil` and `HeatSoak` below.

  /// <summary>
  /// Runs the cupola until <paramref name="done"/> holds, up to <paramref name="maxSeconds"/>, returning the
  /// seconds it took or -1 if it never did.
  /// </summary>
  public int RunUntil(
    System.Func<CupolaRig, bool> done,
    int maxSeconds,
    System.Action<CupolaRig>? each = null
  ) {
    for (int i = 1; i <= maxSeconds; i++) {
      Tick(1);
      each?.Invoke(this);
      if (done(this))
        return i;
    }
    return -1;
  }

  /// <summary>Runs until the cupola reaches <see cref="FurnaceState.Melting"/>, asserting that it does, so
  /// a case about melting cannot quietly become a case about idling.</summary>
  public CupolaRig HeatSoak(int maxSeconds = 600) {
    int took = RunUntil(r => r.State == FurnaceState.Melting, maxSeconds);
    Xunit.Assert.True(
      took > 0,
      // Reports the weighted carbon rather than the band volume: the two differ by 2x on a charcoal scene,
      // and the volume would make a half-fuelled shaft look fully stocked.
      $"the cupola should have reached Melting within {maxSeconds} s; it was {State} at "
        + $"{Temp:F0} C with {CarbonUnits:F0} u of carbon left ({FuelBandUnits} u of fuel bands)"
    );
    return this;
  }

  public CupolaRig SetTemp(float t) {
    ReflectionHelpers.SetField(Furnace, "_internalTemp", t);
    return this;
  }

  public CupolaRig SetMoltenCastIron(float v) {
    ReflectionHelpers.Invoke(Furnace, "PoolIntoHearth", (int)v, 0);
    return this;
  }

  public CupolaRig SetMoltenSlag(float v) {
    ReflectionHelpers.Invoke(Furnace, "PoolIntoHearth", 0, (int)v);
    return this;
  }

  public FurnaceState State => Furnace.State;
  public float Temp =>
    (float)ReflectionHelpers.GetField(Furnace, "_internalTemp")!;
  public float MoltenCastIron =>
    HearthRig.Pooled(
      World,
      Furnace.PoolCells,
      BlockEntityHearthMetal.IronCellKey
    );
  public float MoltenSlag =>
    HearthRig.Pooled(
      World,
      Furnace.PoolCells,
      BlockEntityHearthMetal.SlagCellKey
    );
  public int CastIronCanalUnits => CastIronCanal?.CellAmount ?? 0;
  public int SlagCanalUnits => SlagCanal?.CellAmount ?? 0;

  /// <summary>Full item code of the metal the lower tap poured (for the cast-iron assertion).</summary>
  public string? CastIronCanalMetalType => CastIronCanal?.CellMetalType;

  /// <summary>Full item code of what the upper tap poured. A unit count alone stays positive when the two
  /// taps are swapped, because something still pours; only the material distinguishes them.</summary>
  public string? SlagCanalMetalType => SlagCanal?.CellMetalType;

  /// <summary>The block at a structure-local cell (for the extinguish-residue assertions).</summary>
  public Block BlockAtLocal(int x, int y, int z) =>
    World.GetBlock(Global(x, y, z));

  /// <summary>The charge pile at a structure-local shaft cell, or null (for the salvage assertions).</summary>
  public BlockEntityChargePile? PileAtLocal(int x, int y, int z) =>
    World.GetBlockEntity(Global(x, y, z)) as BlockEntityChargePile;

  /// <summary>
  /// The burden mix standing in the charge block at a structure-local cell; <c>default</c> when the column
  /// does not reach that cell or that block holds no burden. Reads the column rather than the block: a
  /// charge pile is a window onto the column and holds no inventory of its own. The walk skips fuel bands
  /// (a round's base band is its fuel course, carrying <c>default</c> mix) and is bounded at both ends -
  /// without the <c>high</c> clause it runs off this block's slice and reports the block above, and
  /// <c>BlockEntityShaftFurnace.BurnOutCharge</c> decides salvage per block.
  /// </summary>
  public BurdenMix SalvageAtLocal(int x, int y, int z) {
    if (
      Furnace.ChargeColumnAt(Global(x, y, z), out int blockIndex)
      is not { } column
    )
      return default;

    int low = blockIndex * Furnace.ChargeUnitsPerBlock;
    int high = low + Furnace.ChargeUnitsPerBlock;
    int at = 0;
    foreach (ChargeSegment segment in column.Segments) {
      int end = at + segment.Units;
      // `IsFuelCode` rather than `!= CokeCode`: the literal treats a charcoal band as burden, so a charcoal
      // cupola's salvage read comes back as `default`.
      if (
        end > low
        && at < high
        && !BlockEntityFurnaceCore.IsFuelCode(segment.Material)
      )
        return segment.Mix;
      at = end;
    }
    return default;
  }

  /// <summary>The block entity at a structure-local cell (for the frozen-cast-iron assertion).</summary>
  public BlockEntity? BlockEntityAtLocal(int x, int y, int z) =>
    World.GetBlockEntity(Global(x, y, z));

  /// <summary>
  /// Whether a fuel band stands in the block-sized slice of the column covering the structure-local cell
  /// <c>(x, y, z)</c>. <c>BlockEntityFurnaceCore.PileHoldsRejectedCharge</c> walks the same slice to decide
  /// whether a pool cell is free to freeze over, so a case can state that premise rather than assume it. A
  /// cupola that ran its carbon out holds no fuel there.
  /// </summary>
  public bool HoldsFuelAtLocal(int x, int y, int z) {
    if (
      Furnace.ChargeColumnAt(Global(x, y, z), out int blockIndex)
      is not { } column
    )
      return false;

    int perBlock = System.Math.Max(1, Furnace.ChargeUnitsPerBlock);
    int low = blockIndex * perBlock;
    int at = 0;
    foreach (ChargeSegment segment in column.Segments) {
      int end = at + segment.Units;
      if (
        end > low
        && at < low + perBlock
        && BlockEntityFurnaceCore.IsFuelCode(segment.Material)
      )
        return true;
      at = end;
      if (at >= low + perBlock)
        break;
    }
    return false;
  }

  #endregion
}
