using ExpandedLib.Blocks.Networks;
using ExpandedLib.Process;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.Items;
using PipesAndPowerExpanded.BlockNetworkPipe;
using PipesAndPowerExpanded.BlockNetworkPipe.BlockEntities;
using PipesAndPowerExpanded.Tests;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
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
/// </summary>
internal sealed class BlastFurnaceRig
{
  public readonly TestWorld World;
  public readonly BlockEntityBlastFurnaceHot Furnace;
  public BlockEntityMoltenCanalStart? Canal { get; private set; }

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
    ReflectionHelpers.Invoke(Furnace, "UpdateStructureRotation");
    ReflectionHelpers.Invoke(Furnace, "CacheAttributes");
    ReflectionHelpers.SetProperty(
      Furnace,
      nameof(Furnace.StructureComplete),
      true
    );
    // Initialize (which scans for the gas-outlet/tuyere cells) isn't run headlessly - do that scan
    // so the tick reads the tuyeres we place below.
    ReflectionHelpers.Invoke(Furnace, "ScanForOutlets");

    // Hearth piles holding the blast-mix charge (split across two cells in the hearth box), lit.
    BlastmixPile(_pos.AddCopy(0, 3, 0), blastMix / 2);
    BlastmixPile(_pos.AddCopy(0, 2, 0), blastMix - blastMix / 2);

    // Tuyeres: a pipe at each tuyere cell, each its own blast network.
    _tuyeres =
    [
      Tuyere(_pos.AddCopy(0, 1, -1), 20),
      Tuyere(_pos.AddCopy(0, 1, 1), 21),
    ];
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

  private PipeNetwork Tuyere(BlockPos pos, int id)
  {
    var pipe = PipeTestWorld.MakePipe(orientation: "ns", id: id);
    var be = new BlockEntityPipe { Pos = pos.Copy(), Block = pipe };
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

  /// <summary>Places an open iron tap below the furnace with a canal start under it.</summary>
  public BlastFurnaceRig WithIronTapAndCanal()
  {
    BlockPos tapPos = Global(2, 1, 0);
    var tap = new BlockEntityMoltenMetalTap
    {
      Pos = tapPos.Copy(),
      Block = TestBlocks.Configure(
        new Block(),
        "iwex:blastfurnacetap-north",
        30,
        ("side", "north")
      ),
    };
    World.Place(tapPos, tap.Block, tap);
    World.Attach(tap);
    tap.TogglePouring(); // open

    BlockPos canalPos = tapPos.AddCopy(BlockFacing.NORTH.Opposite).DownCopy();
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

  /// <summary>Advances the furnace tick <paramref name="ticks"/> times, re-feeding blast each tick.</summary>
  public BlastFurnaceRig Tick(int ticks = 1)
  {
    for (int i = 0; i < ticks; i++)
    {
      if (_blastTemp >= 0f)
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
      ReflectionHelpers.Invoke(Furnace, "OnProductionTick", 1f);
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
