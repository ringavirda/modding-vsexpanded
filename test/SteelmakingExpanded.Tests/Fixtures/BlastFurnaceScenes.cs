using System.Linq;
using ExpandedLib.Networks;
using ExpandedLib.Process;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.Items;
using LowPressureExpanded.BlockNetworkPipe;
using LowPressureExpanded.BlockNetworkPipe.BlockEntities;
using LowPressureExpanded.Tests;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

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

  /// <param name="blastMix">Charge units loaded into the shaft, split across two piles.</param>
  /// <param name="burden">
  /// Composition to stamp on the charge. Null charges the legacy attribute-less <c>iwex:blastmix</c>,
  /// which the furnace reads as a standard grade - that is what keeps the calibration anchors here
  /// equal to the furnace's old fixed ceilings.
  /// </param>
  /// <param name="chargeCode">
  /// Item path the hearth piles are stamped with (in the iwex domain). Null follows the default:
  /// <c>blastmix</c> when no burden mix is given, else <c>burden</c>. Pass <c>remeltburden</c> to charge
  /// the blast furnace with the wrong family and exercise the conversion gate.
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
    // when the metal is registered, else the game:ingot-pigiron convention. Register BOTH codes so
    // GetItem resolves whatever the tick asks for, independent of process-wide registry state.
    World.RegisterItem("iwex:ingot-pigiron", 1500f);
    World.RegisterItem("game:ingot-pigiron", 1500f);
    World.RegisterItem("iwex:slag");
    World.RegisterNetwork("pipe", s => new PipeNetwork(s));

    Furnace = new BlockEntityBlastFurnaceHot
    {
      Pos = _pos,
      Block = TestBlocks.Configure(
        new Block(),
        "smex:blastfurnacecore-north",
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

    // Hearth piles holding the blast-mix charge (split across two cells in the hearth box), lit.
    // These sit in shaft cells, which the layout satisfies with "@(air|coalpile)" - a lit coal pile
    // is one of the two things that cell legitimately holds.
    BlastmixPile(_pos.AddCopy(0, 3, 0), blastMix / 2);
    BlastmixPile(_pos.AddCopy(0, 2, 0), blastMix - blastMix / 2);

    // Tuyeres: a real tuyere block at each tuyere cell, each its own blast network. Addressed through
    // the structure's own rotation rather than by hand-offsetting, so the cells the furnace reads and
    // the cells the layout wants cannot drift apart.
    _tuyeres =
    [
      Tuyere(Structure.Cell(0, 1, -1), 20),
      Tuyere(Structure.Cell(0, 1, 1), 21),
    ];

    // Fill the remaining shell, run the real Initialize, and let the furnace's monitor tick find its
    // own completed structure. Throws with a per-cell breakdown if it cannot.
    Structure.Complete();
  }

  private void BlastmixPile(BlockPos pos, int units)
  {
    var pile = new BlockEntityCoalPile { Pos = pos.Copy() };
    var inv = new InventoryGeneric(1, "coalpile", "test", World.Api, null);
    var charge = new Item
    {
      Code = new AssetLocation(
        "iwex",
        _chargeCode ?? (_burden == null ? "blastmix" : "burden")
      ),
      ItemId = 4242,
    };
    var stack = new ItemStack(charge, units);
    inv[0].Itemstack = stack;
    if (_burden != null)
      Burden.Write(stack, _burden.Value);
    ReflectionHelpers.SetField(pile, "inventory", inv);
    ReflectionHelpers.SetField(pile, "burning", true);
    World.Place(
      pos,
      TestBlocks.Configure(
        new Block(),
        "game:coalpile",
        50 + pos.Y,
        ("dummy", "x")
      ),
      pile
    );
    World.Attach(pile);
  }

  /// <summary>
  /// A blast-fed tuyere cell: the real <c>iwex:tuyere-*</c> block (a pipe node the furnace draws
  /// through) on its own single-node network. It must be the tuyere block, not a generic pipe - the
  /// two behave identically as network nodes, but only the tuyere satisfies the furnace layout, so a
  /// generic pipe here leaves the structure incomplete and the furnace inert.
  /// </summary>
  private PipeNetwork Tuyere(BlockPos pos, int id)
  {
    var pipe = PipeTestWorld.MakeTuyere(id);
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
  /// generalized, so it did not satisfy the layout's <c>iwex:moltenmetaltap*</c> cell. And it faced
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
    const string tapSide = "west";
    BlockPos tapPos = Global(2, 1, 0);
    var tap = new BlockEntityMoltenMetalTap
    {
      Pos = tapPos.Copy(),
      Block = TestBlocks.Configure(
        new Block(),
        $"iwex:moltenmetaltap-{tapSide}",
        30,
        ("side", tapSide)
      ),
    };
    World.Place(tapPos, tap.Block, tap);
    World.Attach(tap);
    tap.TogglePouring(); // open

    BlockPos canalPos = tapPos
      .AddCopy(BlockFacing.FromCode(tapSide).Opposite)
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

  public BlastFurnaceRig SetState(FurnaceState s)
  {
    ReflectionHelpers.SetProperty(Furnace, nameof(Furnace.State), s);
    return this;
  }

  public BlastFurnaceRig SetTemp(float t)
  {
    ReflectionHelpers.SetField(Furnace, "_internalTemp", t);
    return this;
  }

  public BlastFurnaceRig SetSecondsAboveMelting(float s)
  {
    ReflectionHelpers.SetField(Furnace, "_secondsAboveMelting", s);
    return this;
  }

  public BlastFurnaceRig SetMeltSeconds(float s)
  {
    ReflectionHelpers.SetField(Furnace, "_meltSeconds", s);
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
