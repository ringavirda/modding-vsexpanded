using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Process;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.Items;
using LowPressureExpanded.BlockNetworkPipe;
using LowPressureExpanded.BlockNetworkPipe.BlockEntities;
using LowPressureExpanded.Tests;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Drives the blast furnace's primary process headlessly (handbook blast-furnace + hot-blast
/// articles): a charged, lit hearth fed hot blast through its tuyeres climbs past iron's melting
/// point, enters the Melting phase, turns blast mix into molten pig iron, and taps it into a canal -
/// the steelmaking line's first stage. Stands up the peripherals the gated <c>OnProductionTick</c>
/// reads (hearth blast-mix piles, blast-fed tuyeres) and the metal tap + canal, then drives the tick.
/// Timers are fast-forwarded so the multi-minute melt is reachable in a test.
/// <para>
/// The furnace's <b>real structure is built</b> through <see cref="StructureRig"/>: every cell of the
/// shipped layout is occupied, and the furnace's own monitor tick then observes it and completes
/// itself, which is what runs <c>CacheAttributes</c> and <c>ScanForOutlets</c>. The rig used to force
/// <c>StructureComplete</c> and invoke those two by reflection, which meant a broken layout, a wrong
/// rotation or a tuyere placed one cell out could not fail here - see <see cref="Tuyeres"/>.
/// </para>
/// </summary>
internal sealed class BlastFurnaceRig
{
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
  /// Where a standard-grade burden settles on cold blast with a full hearth: the furnace's old
  /// "natural max temp" constant, now a consequence of the heat balance rather than a config key.
  /// Derived from the live tunables (coke factor 1 at the reference grade, air factor 1 at full
  /// blast supply, no preheat), so retuning the balance moves the tests with it.
  /// </summary>
  public static float ColdBlastCeiling =>
    IwexValues.BfCombustionBaseTemp
    + IwexValues.BfCombustionCokeGain
    - IwexValues.BfRadiationLossBase
    - IwexValues.BfChargeLossFull;

  /// <param name="blastMix">Charge items laid into the shaft, spread across <b>every</b> column in
  /// proportion to how many cells each has. Every column, not two cells: the raceway course has to be
  /// complete before a shaft furnace will light, so a scene that piled its whole charge into one column
  /// would leave the other tuyeres blowing into empty air.</param>
  /// <param name="burden">
  /// Composition to stamp on the charge. Null charges <b>unstamped</b> <c>iwex:burden</c>, which the
  /// furnace reads as a standard grade - that is what keeps the calibration anchors here equal to the
  /// furnace's old fixed ceilings.
  /// It used to be the attribute-less <c>iwex:blastmix</c> item; that item is gone and
  /// unstamped burden inherited its meaning exactly - charge that carries no composition burns as the
  /// reference grade, which is the branch `ReadChargeMix` has always had for it.
  /// </param>
  /// <param name="chargeCode">
  /// Item path the charge is laid as (in the iwex domain). Null follows the default, <c>burden</c> -
  /// stamped when a mix is given, unstamped when it is not. Pass <c>remeltburden</c> to charge the blast
  /// furnace with the wrong family and exercise the conversion gate.
  /// </param>
  public BlastFurnaceRig(
    int blastMix = 400,
    BurdenMix? burden = null,
    string? chargeCode = null
  )
  {
    _burden = burden;
    _chargeCode = chargeCode;
    World = new TestWorld();
    // The metal tap resolves its molten carrier through MetalRegistry: pig iron -> iwex:ingot-pigiron
    // when the metal is registered, else the game:ingot-pigiron convention. Register both codes so
    // GetItem resolves whatever the tick asks for, independent of process-wide registry state.
    World.RegisterItem("iwex:ingot-pigiron", 1500f);
    World.RegisterItem("game:ingot-pigiron", 1500f);
    World.RegisterItem("iwex:slag");
    World.RegisterItem(
      "iwex:" + (chargeCode ?? "burden")
    );

    // The charge pile and its entity class, so the furnace's own SyncChargeBlocks materialises real
    // BlockEntityChargePile windows onto its columns - see ColdBlastFurnaceScenes for why the factory
    // matters as much as the block.
    World.RegisterBlockEntityFactory(
      "iwex.BlockEntityChargePile",
      () => new BlockEntityChargePile()
    );
    Block chargePile = TestBlocks.Configure(
      new Block(),
      BlockChargePile.PileCode.ToShortString(),
      71,
      ("type", "chargepile")
    );
    chargePile.EntityClass = "iwex.BlockEntityChargePile";
    World.Register(chargePile);

    World.RegisterNetwork("pipe", s => new PipeNetwork(s));

    Furnace = new BlockEntityBlastFurnaceHot
    {
      Pos = _pos,
      Block = TestBlocks.Configure(
        new Block(),
        "smex:blastfurnacecore-n",
        1,
        ("side", "north")
      ),
    };
    World.Place(_pos, Furnace.Block, Furnace);
    World.Attach(Furnace);

    // The shipped layout, rotated to the furnace's own facing (north -> 0). Everything the furnace
    // must actually *see* goes in before the fill; the rig then stands up the rest of the shell.
    Structure = StructureRig.Around(
      World,
      Furnace,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      angle: 0
    );

    // Tuyeres: a real tuyere block at each tuyere cell, each its own blast network. Addressed through
    // the structure's own rotation rather than by hand-offsetting, so the cells the furnace reads and
    // the cells the layout wants cannot drift apart.
    _tuyeres =
    [
      Tuyere(Structure.Cell(0, 1, -1), 20, "n"),
      Tuyere(Structure.Cell(0, 1, 1), 21, "s"),
    ];

    // Fill the remaining shell, run the real Initialize, and let the furnace's monitor tick find its
    // own completed structure. Throws with a per-cell breakdown if it cannot.
    Structure.Complete();

    // Charged after the structure stands: columns are the core's and it has none until its layout has
    // arrived, so a push before completion lands nowhere - silently.
    // 0 means "fill the shaft"; a negative charge means "leave it empty", for the scenes that lay their
    // own charge afterwards (see ChargeWithoutCoke).
    if (blastMix >= 0)
      Lay(blastMix > 0 ? blastMix : ShaftCapacityUnits);
  }

  /// <summary>Everything the shaft can hold - every column's own cell count times the furnace's block
  /// quantum. What <c>blastMix: 0</c> means.</summary>
  public int ShaftCapacityUnits
  {
    get
    {
      int total = 0;
      foreach (var (x, z) in Furnace.ShaftColumns.Keys)
        total += Furnace.ColumnCapacity(x, z);
      return total;
    }
  }

  /// <summary>
  /// Lays <paramref name="total"/> items of the scene's charge across every column, weighted by each
  /// column's own cell count and clamped to it - the same rule
  /// <c>ColdBlastFurnaceScenes.Lay</c> follows, and for the same reasons (a complete raceway course, and
  /// no charge pushed above a column's roof where no block can draw it).
  /// </summary>
  /// <param name="rounds">Lay <b>real rounds</b> - a coke course, then a burden course. Only
  /// <see cref="ChargeWithoutCoke"/> passes false, and what it produces is a shaft that cannot burn.</param>
  private void Lay(int total, bool rounds = true)
  {
    if (total <= 0)
      return;

    var keys = new List<(int X, int Z)>(Furnace.ShaftColumns.Keys);
    keys.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
    if (keys.Count == 0)
      return;

    int capacity = 0;
    foreach (var (x, z) in keys)
      capacity += Furnace.ColumnCapacity(x, z);

    string material =
      "iwex:" + (_chargeCode ?? "burden");

    var want = new int[keys.Count];
    int assigned = 0;
    for (int i = 0; i < keys.Count; i++)
    {
      want[i] = (int)(
        (long)total * Furnace.ColumnCapacity(keys[i].X, keys[i].Z)
        / System.Math.Max(1, capacity)
      );
      assigned += want[i];
    }
    for (int i = 0; assigned < total && i < keys.Count; i++)
    {
      want[i]++;
      assigned++;
    }

    int carry = 0;
    for (int i = 0; i < keys.Count; i++)
    {
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
  /// Lays <paramref name="units"/> into one column as <b>real rounds</b> - a coke course, then a burden
  /// course, repeating - at the scene's own coke fraction.
  /// <para>
  /// <b>Charging burden-only is a fiction from the mixed-pile model and it deadlocks the counter-current
  /// furnace.</b> Coke stamped into a burden band cannot burn away, so it makes no void, so nothing
  /// descends - and with the raceway reading carbon off the <em>bands</em> rather than off the stamp, a
  /// burden-only shaft simply never lights at all. Traced in the cold suite; every scene here was charging
  /// the same fiction. The hopper has laid coke as its own bands for a long while; only the fixtures lagged.
  /// </para>
  /// <para>
  /// The burden still carries its stamped fuel fraction: that is what the grade readout and the legacy
  /// path read. It is a <em>grade</em> signal here, not the fuel supply.
  /// </para>
  /// </summary>
  private void LayRounds(ChargeColumn column, string material, int units)
  {
    int perRound = System.Math.Max(2, Furnace.ChargeUnitsPerBlock);
    float fuelFrac = _burden?.FuelFrac ?? IwexValues.BfDefaultFuelFrac;
    int fuelPerRound = System.Math.Max(1, (int)(perRound * fuelFrac));

    int left = units;
    while (left > 0)
    {
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
  /// <see cref="CharcoalCode"/> for the charcoal twin of any case.
  /// <para>
  /// Parameterising the fuel rather than adding a second rig is what makes every existing scenario gain
  /// a charcoal counterpart for free - and a counterpart is the only thing that can tell a furnace that
  /// <em>prices</em> its fuel from one that merely accepts it. Call it before charging: the rig lays its
  /// charge in the constructor, so a scene wanting charcoal passes <c>blastMix: -1</c> and charges itself.
  /// </para>
  /// </summary>
  public BlastFurnaceRig WithFuel(string fuelCode)
  {
    _fuelCode = fuelCode;
    return this;
  }

  /// <summary>Lays a full shaft of rounds in <paramref name="fuelCode"/> - the charcoal twin of the
  /// constructor's default charge.</summary>
  public BlastFurnaceRig ChargeWithFuel(string fuelCode, int units = 0)
  {
    WithFuel(fuelCode);
    Lay(units > 0 ? units : ShaftCapacityUnits);
    return this;
  }

  /// <summary>The fuel the scenes lay their rounds with by default. A scene may lay any fuel the
  /// registry grants - see <see cref="ChargeWithFuel"/> - which is what makes the charcoal cases possible
  /// without a second rig.</summary>
  public const string CokeCode = "game:coke";

  /// <summary>Vanilla charcoal: the pre-coke reductant, worth half of coke per unit.</summary>
  public const string CharcoalCode = "game:charcoal";

  /// <summary>Alias for <see cref="FuelBandUnits"/>, kept because the scenario cases read it by this
  /// name. One reader behind two names, never two readers - the whole reason the string compare below
  /// it survived as long as it did was that nothing else counted bands to disagree with.</summary>
  public int CokeUnits => FuelBandUnits;

  /// <summary>
  /// Charge units standing in <b>fuel bands</b> of any material - the column volume the fuel occupies.
  /// <para>
  /// <b>Not the same number as <see cref="CarbonUnits"/> once a shaft can hold two fuels</b>, and
  /// conflating them is the double-count this model has already made once. A charcoal band occupies a
  /// band's worth of column and carries <em>half</em> a band's worth of carbon.
  /// </para>
  /// <para>
  /// Asked through the production predicate (<c>IsFuelCode</c>), never by comparing to a coke literal -
  /// which is what this read used to do while its own comment claimed it matched production. Production
  /// asks the material-role registry; a string compare answers "no" for charcoal, so every campaign-length
  /// assertion in the suite would have read zero carbon on a charcoal furnace and passed for the wrong
  /// reason.
  /// </para>
  /// </summary>
  public int FuelBandUnits
  {
    get
    {
      int units = 0;
      foreach (ChargeColumn column in Furnace.ShaftColumns.Values)
        foreach (ChargeSegment segment in column.Segments)
          if (BlockEntityFurnaceCore.IsFuelCode(segment.Material))
            units += segment.Units;
      return units;
    }
  }

  /// <summary>
  /// Carbon standing in the shaft, in <b>coke units</b> - each fuel band's volume times its own
  /// <c>CarbonPerUnit</c>. This is what a campaign is made of: a lit shaft runs until it reaches zero,
  /// at <c>BfRacewayCarbonPerTuyerePerSecond x tuyeres</c> a second.
  /// </summary>
  public float CarbonUnits
  {
    get
    {
      float carbon = 0f;
      foreach (ChargeColumn column in Furnace.ShaftColumns.Values)
        foreach (ChargeSegment segment in column.Segments)
          carbon +=
            segment.Units * BlockEntityFurnaceCore.CarbonPerUnit(segment.Material);
      return carbon;
    }
  }

  /// <summary>
  /// Fills every column to the brim with <b>burden alone</b> - no coke bands anywhere: a full shaft that
  /// cannot burn, which is what keeps a furnace dark now that the quantity threshold no longer does.
  /// </summary>
  public BlastFurnaceRig ChargeWithoutCoke(int units = 0)
  {
    Lay(units > 0 ? units : ShaftCapacityUnits, rounds: false);
    return this;
  }

  /// <summary>
  /// A blast-fed tuyere cell: the real <c>iwex:furnace-tuyere-*</c> block (a pipe node the furnace draws
  /// through) on its own single-node network. It must be the tuyere block, not a generic pipe - the
  /// two behave identically as network nodes, but only the tuyere satisfies the furnace layout, so a
  /// generic pipe here leaves the structure incomplete and the furnace inert.
  /// </summary>
  /// <paramref name="orientation"/> is the connector face and the layout now pins it - `n` for the cell
  /// in the north wall, `s` for the one in the south. Passing the same letter for both leaves the furnace
  /// permanently incomplete, which is the point: a tuyere facing into the hearth used to complete it.
  private PipeNetwork Tuyere(BlockPos pos, int id, string orientation)
  {
    var pipe = PipeTestWorld.MakeTuyere(id, orientation);
    var be = new BlockEntityTuyere { Pos = pos.Copy(), Block = pipe };
    World.Place(pos, pipe, be);
    World.Attach(be);
    World.AddNode(pos, "pipe");
    ReflectionHelpers.SetProperty(be, nameof(be.NetworkSystem), World.Networks);
    return (PipeNetwork)World.NetworkAt(pos)!;
  }

  /// <summary>
  /// Turns the air blowers on: air at <paramref name="temp"/> at the tuyeres each tick.
  /// <paramref name="pressure"/> below <c>BlastPressureThreshold</c> models a line the blowers cannot
  /// keep up with - the furnace stops counting it as blast at all.
  /// </summary>
  public BlastFurnaceRig FeedBlast(float temp = 950f, float pressure = 5f)
  {
    _blastTemp = temp;
    _blastPressure = pressure;
    return this;
  }

  /// <summary>Cuts the blast off (the air blowers stopped / the cowpers ran cold).</summary>
  public BlastFurnaceRig CutBlast()
  {
    _blastTemp = -1f;
    return this;
  }

  /// <summary>
  /// Places an open iron tap in the furnace's tap cell with a canal start under it - the runout the
  /// molten pig iron is poured into.
  /// <para>
  /// Two details here used to be wrong and could not fail while the rig forced <c>StructureComplete</c>.
  /// The tap was coded <c>iwex:blastfurnacetap-*</c>, a code no block has carried since the tap was
  /// generalized, so it did not satisfy the layout's tap cell. And it faced
  /// north, which made <c>TryPourMetal</c> aim its runout at the cell south-and-down of the tap -
  /// <b>inside the furnace's own east wall</b>. The tap sits in that wall, so it must face <em>into</em>
  /// the furnace (west) and pour outward, which is also the only orientation whose canal cell is not
  /// part of the footprint. Both now show up as a furnace that will not complete.
  /// </para>
  /// </summary>
  public BlastFurnaceRig WithIronTapAndCanal()
  {
    // The tap sits in the east wall facing in, so its runout lands one cell further east - clear of
    // the structure. Production derives the pour cell from this same variant, so the two cannot drift.
    // `w`, not `west`: a `side` variant renders a single letter since the 2026-08-04 respelling, and
    // this constant is pasted straight into the code the layout's tap cell has to match.
    const string tapSide = "w";
    BlockPos tapPos = Global(2, 1, 0);
    var tap = new BlockEntityFurnaceTap
    {
      Pos = tapPos.Copy(),
      Block = TestBlocks.Configure(
        new Block(),
        $"iwex:furnace-{BlockFurnaceTap.IronType}-{tapSide}",
        30,
        ("type", BlockFurnaceTap.IronType),
        ("side", tapSide)
      ),
    };
    World.Place(tapPos, tap.Block, tap);
    World.Attach(tap);
    tap.TogglePouring(); // open

    // FacingFromSide, not BlockFacing.FromCode - vanilla's returns null for a letter.
    BlockPos canalPos = tapPos
      .AddCopy(ExOrientation.FacingFromSide(tapSide)!.Opposite)
      .DownCopy();
    Canal = new BlockEntityMoltenCanalStart
    {
      Pos = canalPos.Copy(),
      Block = TestBlocks.Configure(
        new Block(),
        "smex:moltencanalstart-ns",
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
  /// Charges the tuyere networks with blast once, without ticking the furnace - so a test can set a
  /// known amount of air in the main and then watch the furnace draw it down (or leave it be while
  /// idle). Unlike <see cref="FeedBlast"/> this does not arm the per-tick re-feed.
  /// </summary>
  public BlastFurnaceRig PrimeBlast(float temp = 950f, float pressure = 5f)
  {
    foreach (var net in _tuyeres)
    {
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
  private void FeedTuyeres()
  {
    if (_blastTemp < 0f)
      return;
    foreach (var net in _tuyeres)
    {
      net.TryProduceGas(
        150f,
        _blastTemp,
        "Air",
        World.Accessor,
        maxOutputPressure: _blastPressure
      );
      net.BroadcastUpdate(World.Accessor); // push Medium/Pressure/Temperature to the tuyere pipes
    }
  }

  /// <summary>
  /// Advances the furnace tick <paramref name="ticks"/> times, re-feeding blast each tick. Invokes the
  /// production tick directly, which is what lets a test jump the multi-minute heat-up with the
  /// <c>Set*</c> fast-forwards and assert one transition in isolation. For the emergent run - no
  /// fast-forward, no reflection - use <see cref="RunLive"/>.
  /// </summary>
  public BlastFurnaceRig Tick(int ticks = 1)
  {
    for (int i = 0; i < ticks; i++)
    {
      FeedTuyeres();
      ReflectionHelpers.Invoke(Furnace, "OnProductionTick", 1f);
    }
    return this;
  }

  /// <summary>
  /// Runs <paramref name="seconds"/> of simulated time through the furnace's <b>own</b> registered
  /// production tick - the listener its <c>Initialize</c> put on the clock, fired at the interval it
  /// asked for - with the blowers topping the mains up each second. Nothing is invoked by reflection
  /// and no state is fast-forwarded, so what comes out is what the machine actually does when it is
  /// built, charged and blown: the headline process is an outcome rather than a sequence of assignments.
  /// </summary>
  public BlastFurnaceRig RunLive(int seconds)
  {
    for (int i = 0; i < seconds; i++)
    {
      FeedTuyeres();
      World.AdvanceBlockEntityTime(1000);
    }
    return this;
  }

  #region Fast-forward + accessors

  // `SetState`, `SetSecondsAboveMelting` and `SetMeltSeconds` are gone, and they must not
  // come back. A shaft furnace recomputes what it is doing from its charge every tick, so a case that
  // arranged one by assigning `State = Melting` and a soak counter was stating a premise the very next tick
  // threw away - green, and testing nothing. `State` has no setter at all now, so the same call throws
  // rather than quietly succeeding (`FurnaceBranchGuards.NoFurnaceExposesASettableState`).
  //
  // What replaces them is arrangement by charging and blowing: `HeatSoak` below runs the real machine
  // until it reaches the state the case is about, and fails loudly if it never does. That costs a few
  // hundred simulated seconds per case and buys a suite whose premises are real.

  /// <summary>
  /// Runs the furnace until it reaches <paramref name="wanted"/>, up to <paramref name="maxSeconds"/>, and
  /// returns the seconds it took (or -1 if it never got there).
  /// <para>
  /// <b>The replacement for every <c>SetState</c> in this suite.</b> A case that wants a melting furnace
  /// gets one by building, charging and blowing a real one - which is the only arrangement a derived state
  /// can have.
  /// </para>
  /// </summary>
  public int RunUntil(
    System.Func<BlastFurnaceRig, bool> done,
    int maxSeconds,
    System.Action<BlastFurnaceRig>? each = null
  )
  {
    for (int i = 1; i <= maxSeconds; i++)
    {
      FeedTuyeres();
      World.AdvanceBlockEntityTime(1000);
      each?.Invoke(this);
      if (done(this))
        return i;
    }
    return -1;
  }

  /// <summary>Runs until the furnace is <see cref="FurnaceState.Melting"/>, throwing a legible failure if
  /// it never gets there - so a case about melting cannot silently become a case about idling.</summary>
  public BlastFurnaceRig HeatSoak(int maxSeconds = 900)
  {
    int took = RunUntil(r => r.State == FurnaceState.Melting, maxSeconds);
    Assert.True(
      took > 0,
      // CarbonUnits, not the band count: on a charcoal scene the two differ by 2x, and a diagnostic
      // that reported band volume while calling it carbon would send the reader after the wrong tunable.
      $"the scene should have reached Melting within {maxSeconds} s; it was {State} at "
        + $"{Temp:F0} C with {CarbonUnits:F0} u of carbon left ({FuelBandUnits} u of fuel bands)"
    );
    return this;
  }

  public BlastFurnaceRig SetTemp(float t)
  {
    ReflectionHelpers.SetField(Furnace, "_internalTemp", t);
    return this;
  }

  public BlastFurnaceRig SetMoltenIron(float v)
  {
    ReflectionHelpers.SetField(Furnace, "_moltenIron", v);
    return this;
  }

  public FurnaceState State => Furnace.State;
  public float Temp =>
    (float)ReflectionHelpers.GetField(Furnace, "_internalTemp")!;

  /// <summary>Total air (L) sitting in the tuyere networks - what the furnace draws its blast from.</summary>
  public float TuyereVolume
  {
    get
    {
      float total = 0f;
      foreach (var net in _tuyeres)
        total += net.State?.Volume ?? 0f;
      return total;
    }
  }

  /// <summary>Whether the furnace read as air-starved on the last tick (blast under the floor).</summary>
  public bool AirStarved =>
    (bool)ReflectionHelpers.GetField(Furnace, "_airStarved")!;

  /// <summary>The heat balance the last tick computed - what the furnace is chasing, and why.</summary>
  public HeatBalance Heat =>
    (HeatBalance)ReflectionHelpers.GetField(Furnace, "_lastHeatBalance")!;

  /// <summary>Melt-cycle speed multiplier at the current internal temperature.</summary>
  public float MeltSpeed =>
    (float)ReflectionHelpers.Invoke(Furnace, "MeltSpeedFactor")!;

  public float MoltenIron =>
    (float)ReflectionHelpers.GetField(Furnace, "_moltenIron")!;
  public int CanalIron => Canal?.CellAmount ?? 0;

  /// <summary>Full item code of the metal the tap poured into the canal (for the pig-iron assertion).</summary>
  public string? CanalMetalType => Canal?.CellMetalType;

  #endregion
}
