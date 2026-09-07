using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Industry.Heat;
using ExpandedLib.Industry.Metals;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Machines;
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
using SteelIndustryExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelIndustryExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Drives the blast furnace headlessly (handbook blast-furnace + hot-blast articles): a charged, lit
/// hearth fed hot blast through its tuyeres climbs past iron's melting point, melts, and taps molten pig
/// iron into a canal. Stands up what the gated <c>OnProductionTick</c> reads - shaft charge, blast-fed
/// tuyeres, metal tap and canal - then drives the tick, with timers fast-forwarded so the multi-minute
/// melt is reachable.
/// <para>
/// The structure is built through <see cref="StructureRig"/> and completed by the furnace's own monitor
/// tick, which is what runs <c>CacheAttributes</c> and <c>ScanForOutlets</c>. A broken layout, a wrong
/// rotation or a tuyere one cell out therefore fails here.
/// </para>
/// </summary>
internal sealed class BlastFurnaceRig {
  public readonly TestWorld World;
  public readonly BlockEntityBlastFurnaceHot Furnace;
  public BlockEntityMoltenCanalStart? Canal { get; private set; }

  /// <summary>The standing structure, for tests that address cells by their authored local offsets.</summary>
  public readonly StructureRig Structure;

  private readonly BlockPos _pos = new(0, 16, 0);
  private readonly PipeNetwork[] _tuyeres;
  private readonly BurdenMix? _burden;
  private readonly string? _chargeCode;

  /// <summary>Which fuel this scene lays its rounds with - see <see cref="WithFuel"/>.</summary>
  private string _fuelCode = CokeCode;
  private float _blastTemp = -1f;
  private float _blastPressure = 5f;

  /// <summary>
  /// Temperature (C) a standard-grade burden settles at on cold blast with a full hearth. Derived from
  /// the live tunables - coke factor 1 at the reference grade, air factor 1 at full blast supply, no
  /// preheat - so retuning the heat balance moves the tests with it.
  /// </summary>
  public static float ColdBlastCeiling =>
    IiexValues.BfCombustionBaseTemp
    + IiexValues.BfCombustionCokeGain
    - IiexValues.BfRadiationLossBase
    - IiexValues.BfChargeLossFull;

  /// <param name="blastMix">Charge items laid into the shaft, spread across every column in proportion to
  /// its cell count, because the raceway course must be complete before a shaft furnace will light. 0 fills
  /// the shaft; negative leaves it empty for scenes that lay their own charge.</param>
  /// <param name="burden">Composition to stamp on the charge. Null charges unstamped <c>iiex:burden</c>,
  /// which <c>ReadChargeMix</c> reads as the reference grade - what keeps the calibration anchors here
  /// equal to the furnace's fixed ceilings.</param>
  /// <param name="chargeCode">Item path the charge is laid as, in the iiex domain. Null follows the
  /// default, <c>burden</c>. Pass <c>remeltburden</c> to charge the wrong family and exercise the
  /// conversion gate.</param>
  /// <param name="blowIn">Whether to light the furnace once it stands. True by default: a shaft has not
  /// caught by itself since U4.9, and every scenario here is about combustion, blast or tapping rather
  /// than about ignition, so the ritual is performed once here instead of in twenty-five chains. Pass
  /// false to watch a furnace that nobody has lit - see
  /// <c>A_built_and_blown_furnace_stays_dark_until_a_torch_reaches_it</c>, which does the gesture itself.</param>
  public BlastFurnaceRig(
    int blastMix = 400,
    BurdenMix? burden = null,
    string? chargeCode = null,
    bool blowIn = true
  ) {
    _burden = burden;
    _chargeCode = chargeCode;
    World = new TestWorld();
    // Server-side, as the cold-furnace rig is: this drives the production tick, and a world that
    // identifies as neither side skips every `Side == Server` branch in it without saying so.
    World.World.Side.Returns(EnumAppSide.Server);
    // The pool stands in hearth blocks the furnace places, so they have to resolve before a melt can put
    // anything anywhere.
    HearthRig.Register(World, "iiex:hearthmetal-pigiron", 70);
    World.RegisterItem(MetalRegistry.MoltenItemOf("pigiron").ToString(), 1500f);
    // The metal tap resolves its molten carrier through MetalRegistry: iiex:ingot-pigiron when the metal
    // is registered, else the game:ingot-pigiron convention. Both codes are registered so GetItem resolves
    // whatever the tick asks for, independent of process-wide registry state.
    World.RegisterItem("iiex:ingot-pigiron", 1500f);
    World.RegisterItem("game:ingot-pigiron", 1500f);
    World.RegisterItem("iiex:slag");
    World.RegisterItem("iiex:" + (chargeCode ?? "burden"));

    // The charge pile and its entity class, so the furnace's own SyncChargeBlocks materialises real
    // BlockEntityChargePile windows onto its columns. The factory registration matters as much as the
    // block: without it the piles are placed but carry no entity.
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

    World.RegisterNetwork("pipe", s => new PipeNetwork(s));

    Furnace = new BlockEntityBlastFurnaceHot {
      Pos = _pos,
      Block = TestBlocks.Configure(
        new Block(),
        "siex:blastfurnacecore-n",
        1,
        ("side", "north")
      ),
    };
    World.Place(_pos, Furnace.Block, Furnace);
    World.Attach(Furnace);

    // The shipped layout, rotated to the furnace's own facing (north -> 0). Everything the furnace has to
    // see goes in before the fill; the rig then stands up the rest of the shell.
    Structure = StructureRig.Around(
      World,
      Furnace,
      BlockBlastFurnaceCoreHot.Definitions("siex").Single(),
      angle: 0
    );

    // A real tuyere block at each tuyere cell, each on its own blast network. Addressed through the
    // structure's own rotation rather than by hand-offsetting, so the cells the furnace reads and the
    // cells the layout wants cannot drift apart.
    _tuyeres =
    [
      Tuyere(Structure.Cell(0, 1, -1), 20, "n"),
      Tuyere(Structure.Cell(0, 1, 1), 21, "s"),
    ];

    // Fill the remaining shell, run the real Initialize, and let the furnace's monitor tick find its
    // own completed structure. Throws with a per-cell breakdown if it cannot.
    Structure.Complete();

    // Charged after the structure stands: the columns belong to the core and it has none until its layout
    // has arrived, so a push before completion lands nowhere and does so silently.
    if (blastMix >= 0)
      Lay(blastMix > 0 ? blastMix : ShaftCapacityUnits);

    // After the charge: the flame is what the player brings, and it is the charge that decides whether it
    // takes. Before it, the furnace would be blown in with an empty shaft, which is a different scene.
    if (blowIn)
      BlowIn();
  }

  /// <summary>Charge units the shaft can hold: every column's cell count times the furnace's block
  /// quantum. What <c>blastMix: 0</c> lays.</summary>
  public int ShaftCapacityUnits {
    get {
      int total = 0;
      foreach (var (x, z) in Furnace.ShaftColumns.Keys)
        total += Furnace.ColumnCapacity(x, z);
      return total;
    }
  }

  /// <summary>
  /// Lays <paramref name="total"/> items of the scene's charge across every column, weighted by each
  /// column's cell count and clamped to it - the same rule <c>ColdBlastFurnaceScenes.Lay</c> follows, for
  /// a complete raceway course and no charge above a column's roof where no block can draw it.
  /// </summary>
  /// <param name="rounds">Lay real rounds - a fuel course, then a burden course. Only
  /// <see cref="ChargeWithoutCoke"/> passes false, which produces a shaft that cannot burn.</param>
  private void Lay(int total, bool rounds = true) {
    if (total <= 0)
      return;

    var keys = new List<(int X, int Z)>(Furnace.ShaftColumns.Keys);
    keys.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
    if (keys.Count == 0)
      return;

    int capacity = 0;
    foreach (var (x, z) in keys)
      capacity += Furnace.ColumnCapacity(x, z);

    string material = "iiex:" + (_chargeCode ?? "burden");

    var want = new int[keys.Count];
    int assigned = 0;
    for (int i = 0; i < keys.Count; i++) {
      want[i] = (int)(
        (long)total
        * Furnace.ColumnCapacity(keys[i].X, keys[i].Z)
        / System.Math.Max(1, capacity)
      );
      assigned += want[i];
    }
    for (int i = 0; assigned < total && i < keys.Count; i++) {
      want[i]++;
      assigned++;
    }

    int carry = 0;
    for (int i = 0; i < keys.Count; i++) {
      var (x, z) = keys[i];
      ChargeColumn column = Furnace.ChargeColumnAt(x, z)!;
      int room = Furnace.ColumnCapacity(x, z) - column.TotalUnits;
      int give = System.Math.Min(want[i] + carry, room);
      carry = want[i] + carry - give;
      if (give <= 0)
        continue;
      if (rounds)
        LayRounds(column, material, give);
      else
        column.Push(material, give, 20f, _burden ?? default);
    }

    Furnace.SyncChargeBlocks();
  }

  /// <summary>
  /// Lays <paramref name="units"/> into one column as alternating fuel and burden courses, at the scene's
  /// own fuel fraction. Fuel must be its own bands: the raceway reads carbon off the bands rather than off
  /// a burden stamp, and only a fuel band burning away makes the void the counter-current shaft descends
  /// into, so a burden-only column never lights. The stamped fuel fraction on the burden remains a grade
  /// signal for the readout, not the fuel supply.
  /// </summary>
  private void LayRounds(ChargeColumn column, string material, int units) {
    int perRound = System.Math.Max(2, Furnace.ChargeUnitsPerBlock);
    float fuelFrac = _burden?.FuelFrac ?? IiexValues.BfDefaultFuelFrac;
    int fuelPerRound = System.Math.Max(1, (int)(perRound * fuelFrac));

    int left = units;
    while (left > 0) {
      int fuel = System.Math.Min(fuelPerRound, left);
      column.Push(_fuelCode, fuel, 20f, default);
      left -= fuel;
      if (left <= 0)
        break;

      int burden = System.Math.Min(perRound - fuelPerRound, left);
      column.Push(material, burden, 20f, _burden ?? default);
      left -= burden;
    }
  }

  /// <summary>
  /// Switches which fuel the scene's rounds are laid with - <see cref="CokeCode"/> by default,
  /// <see cref="CharcoalCode"/> for the charcoal twin of a case. Must be called before charging: the rig
  /// lays its charge in the constructor, so a charcoal scene passes <c>blastMix: -1</c> and charges itself.
  /// </summary>
  public BlastFurnaceRig WithFuel(string fuelCode) {
    _fuelCode = fuelCode;
    return this;
  }

  /// <summary>Lays a full shaft of rounds in <paramref name="fuelCode"/>. <paramref name="units"/> of 0
  /// fills the shaft.</summary>
  public BlastFurnaceRig ChargeWithFuel(string fuelCode, int units = 0) {
    WithFuel(fuelCode);
    Lay(units > 0 ? units : ShaftCapacityUnits);
    return this;
  }

  /// <summary>The fuel the scenes lay their rounds with by default. A scene may lay any fuel the registry
  /// grants - see <see cref="ChargeWithFuel"/>.</summary>
  public const string CokeCode = "game:coke";

  /// <summary>Vanilla charcoal: the pre-coke reductant, worth half of coke per unit.</summary>
  public const string CharcoalCode = "game:charcoal";

  /// <summary>Alias for <see cref="FuelBandUnits"/>, which the scenario cases read by this name. It
  /// delegates rather than counting again, so there is one reader behind both names.</summary>
  public int CokeUnits => FuelBandUnits;

  /// <summary>
  /// Charge units standing in fuel bands of any material: the column volume the fuel occupies. Not the
  /// same number as <see cref="CarbonUnits"/> once a shaft holds two fuels - a charcoal band occupies a
  /// full band of column and carries half a band's worth of carbon. Membership is asked through the
  /// production predicate <c>IsFuelCode</c>, which consults the material-role registry; a compare against
  /// a coke literal answers no for charcoal and would report zero on a charcoal furnace.
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
  /// Carbon standing in the shaft, in coke units: each fuel band's volume times its own
  /// <c>CarbonPerUnit</c>. A lit shaft runs until this reaches zero, burning
  /// <c>BfRacewayCarbonPerTuyerePerSecond x tuyeres</c> per second.
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

  /// <summary>
  /// Fills every column to the brim with burden alone, no fuel bands anywhere: a full shaft that cannot
  /// burn. The way to keep a furnace dark, since quantity alone no longer gates ignition.
  /// </summary>
  public BlastFurnaceRig ChargeWithoutCoke(int units = 0) {
    Lay(units > 0 ? units : ShaftCapacityUnits, rounds: false);
    return this;
  }

  /// <summary>
  /// A blast-fed tuyere cell: the real <c>iiex:furnace-tuyere-*</c> block on its own single-node network.
  /// It must be the tuyere block and not a generic pipe - the two are identical as network nodes, but only
  /// the tuyere satisfies the layout, so a generic pipe leaves the furnace incomplete and inert.
  /// </summary>
  /// <param name="orientation">Connector face, pinned by the layout: `n` for the cell in the north wall,
  /// `s` for the one in the south. The same letter for both never completes.</param>
  private PipeNetwork Tuyere(BlockPos pos, int id, string orientation) {
    var pipe = PipeTestWorld.MakeTuyere(id, orientation);
    var be = new BlockEntityTuyere { Pos = pos.Copy(), Block = pipe };
    World.Place(pos, pipe, be);
    World.Attach(be);
    World.AddNode(pos, "pipe");
    ReflectionHelpers.SetProperty(be, nameof(be.NetworkSystem), World.Networks);
    return (PipeNetwork)World.NetworkAt(pos)!;
  }

  /// <summary>
  /// Arms the blowers: air at <paramref name="temp"/> (C) delivered at the tuyeres each tick.
  /// <paramref name="pressure"/> (atm) below <c>BlastPressureThreshold</c> models a line the blowers
  /// cannot keep up with, which the furnace stops counting as blast at all.
  /// </summary>
  public BlastFurnaceRig FeedBlast(float temp = 950f, float pressure = 5f) {
    _blastTemp = temp;
    _blastPressure = pressure;
    return this;
  }

  /// <summary>
  /// Lights the furnace - through the iron tap when one has been stood up, otherwise straight at the
  /// furnace. A shaft does not catch by itself; see <c>BlowInRig</c>.
  /// </summary>
  public BlastFurnaceRig BlowIn() {
    BlowInRig.BlowIn(World, Furnace, IronTap);
    return this;
  }

  /// <summary>Cuts the blast off: the blowers stop and the tuyeres are no longer re-fed.</summary>
  public BlastFurnaceRig CutBlast() {
    _blastTemp = -1f;
    return this;
  }

  /// <summary>
  /// Places an open iron tap in the furnace's tap cell with a canal start under it - the runout the molten
  /// pig iron is poured into. The tap sits in the east wall and must face into the furnace (west) so that
  /// <c>TryPourMetal</c> aims its runout outward; that is also the only orientation whose canal cell falls
  /// outside the footprint. A wrong code or facing shows up as a furnace that will not complete.
  /// </summary>
  /// <summary>The iron tap, once <see cref="WithIronTapAndCanal"/> has stood one up - what the blow-in
  /// reaches its flame through.</summary>
  public BlockEntityFurnaceTap? IronTap { get; private set; }

  public BlastFurnaceRig WithIronTapAndCanal() {
    // The tap faces in, so its runout lands one cell further east, clear of the structure. Production
    // derives the pour cell from this same variant, so the two cannot drift. `w`, not `west`: a side
    // variant renders a single letter, and this constant is pasted into the code the tap cell must match.
    const string tapSide = "w";
    BlockPos tapPos = Global(2, 1, 0);
    var tap = new BlockEntityFurnaceTap {
      Pos = tapPos.Copy(),
      // The real block class, not a stand-in: the blow-in gesture runs through its interaction.
      Block = TestBlocks.Configure(
        new BlockFurnaceTap(),
        $"iiex:furnace-{BlockFurnaceTap.IronType}-{tapSide}",
        30,
        ("type", BlockFurnaceTap.IronType),
        ("side", tapSide)
      ),
    };
    World.Place(tapPos, tap.Block, tap);
    World.Attach(tap);
    tap.SetPlugged(false); // open
    IronTap = tap;

    // FacingFromSide, not BlockFacing.FromCode - vanilla's returns null for a letter.
    BlockPos canalPos = tapPos
      .AddCopy(ExOrientation.FacingFromSide(tapSide)!.Opposite)
      .DownCopy();
    Canal = new BlockEntityMoltenCanalStart {
      Pos = canalPos.Copy(),
      Block = TestBlocks.Configure(
        new Block(),
        "siex:moltencanalstart-ns",
        31,
        ("type", "start"),
        ("orientation", "ns")
      ),
    };
    World.Place(canalPos, Canal.Block, Canal);
    World.Attach(Canal);
    return this;
  }

  private BlockPos Global(int x, int y, int z) =>
    (BlockPos)ReflectionHelpers.Invoke(Furnace, "GetGlobalPos", x, y, z)!;

  /// <summary>
  /// Charges the tuyere networks with blast once, without ticking the furnace, so a test can set a known
  /// amount of air in the main and then watch the furnace draw it down. Unlike <see cref="FeedBlast"/>
  /// this does not arm the per-tick re-feed.
  /// </summary>
  public BlastFurnaceRig PrimeBlast(float temp = 950f, float pressure = 5f) {
    foreach (var net in _tuyeres) {
      net.TryProduceGas(
        150f,
        temp,
        "Air",
        World.Accessor,
        maxOutputPressure: pressure
      );
      net.BroadcastUpdate(World.Accessor);
    }
    return this;
  }

  /// <summary>One tick of the blowers: tops the tuyere mains back up to the armed blast.</summary>
  private void FeedTuyeres() {
    if (_blastTemp < 0f)
      return;
    foreach (var net in _tuyeres) {
      net.TryProduceGas(
        150f,
        _blastTemp,
        "Air",
        World.Accessor,
        maxOutputPressure: _blastPressure
      );
      net.BroadcastUpdate(World.Accessor); // pushes Medium/Pressure/Temperature to the tuyere pipes
    }
  }

  /// <summary>
  /// Advances the furnace tick <paramref name="ticks"/> times, re-feeding blast each tick. Invokes the
  /// production tick directly, which lets a test jump the multi-minute heat-up with the <c>Set*</c>
  /// fast-forwards and assert one transition in isolation. Use <see cref="RunLive"/> for a run with no
  /// fast-forward and no reflection.
  /// </summary>
  public BlastFurnaceRig Tick(int ticks = 1) {
    for (int i = 0; i < ticks; i++) {
      FeedTuyeres();
      Furnace
        .GetBehavior<BEBehaviorProductionMachine>()
        .DriveProductionTick(1f);
    }
    return this;
  }

  /// <summary>
  /// Runs <paramref name="seconds"/> of simulated time through the furnace's own registered production
  /// tick - the listener its <c>Initialize</c> put on the clock, at the interval it asked for - with the
  /// blowers topping the mains up each second. Nothing is invoked by reflection and no state is
  /// fast-forwarded, so the outcome is what a built, charged and blown furnace does.
  /// </summary>
  public BlastFurnaceRig RunLive(int seconds) {
    for (int i = 0; i < seconds; i++) {
      FeedTuyeres();
      World.AdvanceBlockEntityTime(1000);
    }
    return this;
  }

  #region Fast-forward + accessors

  // There is no `SetState`, `SetSecondsAboveMelting` or `SetMeltSeconds`, and none may be added. A shaft
  // furnace recomputes its state from the charge every tick, so an assigned premise is discarded on the
  // next tick; `State` has no setter, which `FurnaceBranchGuards.NoFurnaceExposesASettableState` enforces.
  // Arrange instead by charging and blowing: `HeatSoak` runs the real machine until it reaches the wanted
  // state and fails if it never does, at a cost of a few hundred simulated seconds per case.

  /// <summary>
  /// Runs the furnace a second at a time until <paramref name="done"/> holds, up to
  /// <paramref name="maxSeconds"/>. Returns the seconds it took, or -1 if it never got there.
  /// <paramref name="each"/> runs after every simulated second.
  /// </summary>
  public int RunUntil(
    System.Func<BlastFurnaceRig, bool> done,
    int maxSeconds,
    System.Action<BlastFurnaceRig>? each = null
  ) {
    for (int i = 1; i <= maxSeconds; i++) {
      FeedTuyeres();
      World.AdvanceBlockEntityTime(1000);
      each?.Invoke(this);
      if (done(this))
        return i;
    }
    return -1;
  }

  /// <summary>Runs until the furnace is <see cref="FurnaceState.Melting"/>, failing with a diagnostic if
  /// it never gets there, so a case about melting cannot silently become a case about idling.</summary>
  public BlastFurnaceRig HeatSoak(int maxSeconds = 900) {
    int took = RunUntil(r => r.State == FurnaceState.Melting, maxSeconds);
    Assert.True(
      took > 0,
      // Both figures: on a charcoal scene carbon and band volume differ by 2x, so reporting one alone
      // points at the wrong tunable.
      $"the scene should have reached Melting within {maxSeconds} s; it was {State} at "
        + $"{Temp:F0} C with {CarbonUnits:F0} u of carbon left ({FuelBandUnits} u of fuel bands)"
    );
    return this;
  }

  public BlastFurnaceRig SetTemp(float t) {
    ReflectionHelpers.SetField(Furnace, "_internalTemp", t);
    return this;
  }

  public BlastFurnaceRig SetMoltenIron(float v) {
    ReflectionHelpers.SetField(Furnace, "_moltenIron", v);
    return this;
  }

  public FurnaceState State => Furnace.State;
  public float Temp =>
    (float)ReflectionHelpers.GetField(Furnace, "_internalTemp")!;

  /// <summary>Total air (L) sitting in the tuyere networks - what the furnace draws its blast from.</summary>
  public float TuyereVolume {
    get {
      float total = 0f;
      foreach (var net in _tuyeres)
        total += net.State?.Volume ?? 0f;
      return total;
    }
  }

  /// <summary>Whether the furnace read as air-starved on the last tick: blast under the floor.</summary>
  public bool AirStarved =>
    (bool)ReflectionHelpers.GetField(Furnace, "_airStarved")!;

  /// <summary>The heat balance the last tick computed: gains, losses and the target temperature.</summary>
  public HeatBalance Heat =>
    (HeatBalance)ReflectionHelpers.GetField(Furnace, "_lastHeatBalance")!;

  /// <summary>Melt-cycle speed multiplier at the current internal temperature.</summary>
  public float MeltSpeed =>
    (float)ReflectionHelpers.Invoke(Furnace, "MeltSpeedFactor")!;

  // The pool is the crucible floor's own cells now; summing them leaves every scenario assertion reading
  // as it did against the field.
  public float MoltenIron =>
    HearthRig.Pooled(
      World,
      Furnace.PoolCells,
      BlockEntityHearthMetal.IronCellKey
    );
  public int CanalIron => Canal?.CellAmount ?? 0;

  /// <summary>Full item code of the metal the tap poured into the canal.</summary>
  public string? CanalMetalType => Canal?.CellMetalType;

  #endregion
}
