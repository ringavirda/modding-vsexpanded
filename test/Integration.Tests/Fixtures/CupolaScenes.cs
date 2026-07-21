using ExpandedLib.Blocks.Networks;
using ExpandedLib.Metals;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using IronworkingExpanded.Items;
using PipesAndPowerExpanded.BlockNetworkPipe;
using PipesAndPowerExpanded.BlockNetworkPipe.BlockEntities;
using PipesAndPowerExpanded.Tests;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Drives the cupola furnace headlessly (docs/design/iwex.md, the scrap re-melter): a charged, lit,
/// blast-fed shaft climbs past cast iron's melt line, enters the Melting phase, re-melts remelt burden
/// into molten CAST IRON (and slag), taps cast iron out the lower tap into one canal and slag out the
/// upper into another, and - when extinguished mid-heat - freezes its pool onto the hearth as solid cast
/// iron while leaving the rest of the burden as salvageable spent charge (never slag). The cupola is the
/// same machine as the blast furnace (<see cref="BlockEntityCupolaFurnace"/> derives
/// <see cref="BlockEntityBlastFurnace"/>); this rig stands up its narrower peripherals - a single-column
/// shaft, a single tuyere, and the two taps + canals - and pumps the gated <c>OnProductionTick</c>.
/// Timers are fast-forwarded so the multi-minute melt is reachable in a test.
/// </summary>
internal sealed class CupolaRig
{
  public readonly TestWorld World;
  public readonly BlockEntityCupolaFurnace Furnace;
  public BlockEntityMoltenCanalStart? CastIronCanal { get; private set; }
  public BlockEntityMoltenCanalStart? SlagCanal { get; private set; }

  private readonly BlockPos _pos = new(0, 16, 0);
  private readonly PipeNetwork[] _tuyeres;
  private readonly BurdenMix? _burden;
  private readonly string _chargeCode;
  private float _blastTemp = -1f;
  private float _blastPressure = 5f;

  /// <param name="charge">Remelt-burden units loaded into the shaft, split across the bottom two cells.</param>
  /// <param name="burden">Composition stamped on the charge (its coke fraction drives the heat balance).</param>
  /// <param name="chargeCode">Item path (iwex domain) the shaft piles hold. Defaults to the cupola's own
  /// <c>remeltburden</c>; pass <c>burden</c> to charge it with the wrong (ore) family and exercise the gate.</param>
  public CupolaRig(
    int charge = 320,
    BurdenMix? burden = null,
    string chargeCode = "remeltburden"
  )
  {
    _burden = burden ?? new BurdenMix(60f, 5f, 35f);
    _chargeCode = chargeCode;
    World = new TestWorld();

    // The taps resolve their molten carrier through MetalRegistry: cast iron -> iwex:ingot-castiron when
    // the metal is registered, else the game:ingot-castiron convention. Register BOTH codes (and both slag
    // codes) so GetItem resolves whatever the tick asks for, independent of process-wide registry state.
    World.RegisterItem("iwex:ingot-castiron", 1200f);
    World.RegisterItem("game:ingot-castiron", 1200f);
    World.RegisterItem("iwex:slag", 1200f);
    World.RegisterItem("game:ingot-slag", 1200f);
    // The block the extinguished pool freezes into (BlockEntitySolidifiedIron, so StampSolidProduct lands).
    World.RegisterBlockEntityFactory(
      "iwex.BlockEntitySolidifiedIron",
      () => new BlockEntitySolidifiedIron()
    );
    Block solid = TestBlocks.Configure(
      new Block(),
      "iwex:solidifiedcastiron",
      70,
      ("dummy", "x")
    );
    solid.EntityClass = "iwex.BlockEntitySolidifiedIron";
    World.Register(solid);

    World.RegisterNetwork("pipe", s => new PipeNetwork(s));

    Furnace = new BlockEntityCupolaFurnace
    {
      Pos = _pos,
      Block = TestBlocks.Configure(
        new Block(),
        "iwex:cupolafurnacecore-north",
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
    ReflectionHelpers.Invoke(Furnace, "ScanForOutlets");

    // Single-column shaft: piles in the bottom two chargeable cells (0,1,0) and (0,2,0), lit.
    ChargePile(_pos.AddCopy(0, 1, 0), charge / 2);
    ChargePile(_pos.AddCopy(0, 2, 0), charge - charge / 2);

    // A single tuyere on the north face of the hearth (0,1,-1), its own blast network.
    _tuyeres = [Tuyere(_pos.AddCopy(0, 1, -1), 40)];
  }

  private void ChargePile(BlockPos pos, int units)
  {
    var pile = new BlockEntityCoalPile { Pos = pos.Copy() };
    var inv = new InventoryGeneric(1, "coalpile", "test", World.Api, null);
    var charge = new Item
    {
      Code = new AssetLocation("iwex", _chargeCode),
      ItemId = 7000,
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

  /// <summary>Turns the blast on: air at <paramref name="temp"/> and <paramref name="pressure"/> at the tuyere.</summary>
  public CupolaRig FeedBlast(float temp = 950f, float pressure = 5f)
  {
    _blastTemp = temp;
    _blastPressure = pressure;
    return this;
  }

  /// <summary>Cuts the blast off.</summary>
  public CupolaRig CutBlast()
  {
    _blastTemp = -1f;
    return this;
  }

  /// <summary>Places the lower (cast-iron) tap on its layout cell with a canal start beneath it, open.</summary>
  public CupolaRig WithCastIronTapAndCanal()
  {
    CastIronCanal = TapAndCanal(Cell("MetalTapCell"), 30, 31);
    return this;
  }

  /// <summary>Places the upper (slag) tap on its layout cell with a canal start beneath it, open.</summary>
  public CupolaRig WithSlagTapAndCanal()
  {
    SlagCanal = TapAndCanal(Cell("SlagTapCell"), 32, 33);
    return this;
  }

  private BlockEntityMoltenCanalStart TapAndCanal(
    Vec3i localCell,
    int tapId,
    int canalId
  )
  {
    BlockPos tapPos = Global(localCell.X, localCell.Y, localCell.Z);
    var tap = new BlockEntityMoltenMetalTap
    {
      Pos = tapPos.Copy(),
      Block = TestBlocks.Configure(
        new Block(),
        "iwex:moltenmetaltap-north",
        tapId,
        ("side", "north")
      ),
    };
    World.Place(tapPos, tap.Block, tap);
    World.Attach(tap);
    tap.TogglePouring(); // open

    // The tap pours into the canal start at Pos + facing.Opposite + down (its spout foot).
    BlockPos canalPos = tapPos.AddCopy(BlockFacing.NORTH.Opposite).DownCopy();
    var canal = new BlockEntityMoltenCanalStart
    {
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

  private Vec3i Cell(string property) =>
    (Vec3i)ReflectionHelpers.GetProperty(Furnace, property)!;

  private BlockPos Global(int x, int y, int z) =>
    (BlockPos)ReflectionHelpers.Invoke(Furnace, "GetGlobalPos", x, y, z)!;

  /// <summary>Advances the furnace tick <paramref name="ticks"/> times, re-feeding blast each tick.</summary>
  public CupolaRig Tick(int ticks = 1)
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
          net.BroadcastUpdate(World.Accessor);
        }
      ReflectionHelpers.Invoke(Furnace, "OnProductionTick", 1f);
    }
    return this;
  }

  #region Fast-forward + accessors

  public CupolaRig SetState(FurnaceState s)
  {
    ReflectionHelpers.SetProperty(Furnace, nameof(Furnace.State), s);
    return this;
  }

  public CupolaRig SetTemp(float t)
  {
    ReflectionHelpers.SetField(Furnace, "_internalTemp", t);
    return this;
  }

  public CupolaRig SetSecondsAboveMelting(float s)
  {
    ReflectionHelpers.SetField(Furnace, "_secondsAboveMelting", s);
    return this;
  }

  public CupolaRig SetMeltSeconds(float s)
  {
    ReflectionHelpers.SetField(Furnace, "_meltSeconds", s);
    return this;
  }

  public CupolaRig SetFuelBurnSeconds(float s)
  {
    ReflectionHelpers.SetField(Furnace, "_fuelBurnSeconds", s);
    return this;
  }

  public CupolaRig SetMoltenCastIron(float v)
  {
    ReflectionHelpers.SetField(Furnace, "_moltenIron", v);
    return this;
  }

  public CupolaRig SetMoltenSlag(float v)
  {
    ReflectionHelpers.SetField(Furnace, "_moltenSlag", v);
    return this;
  }

  public FurnaceState State => Furnace.State;
  public float Temp =>
    (float)ReflectionHelpers.GetField(Furnace, "_internalTemp")!;
  public float MoltenCastIron =>
    (float)ReflectionHelpers.GetField(Furnace, "_moltenIron")!;
  public float MoltenSlag =>
    (float)ReflectionHelpers.GetField(Furnace, "_moltenSlag")!;
  public int CastIronCanalUnits => CastIronCanal?.CellAmount ?? 0;
  public int SlagCanalUnits => SlagCanal?.CellAmount ?? 0;

  /// <summary>Full item code of the metal the lower tap poured (for the cast-iron assertion).</summary>
  public string? CastIronCanalMetalType => CastIronCanal?.CellMetalType;

  /// <summary>The block at a structure-local cell (for the extinguish-residue assertions).</summary>
  public Block BlockAtLocal(int x, int y, int z) => World.GetBlock(Global(x, y, z));

  /// <summary>The coal pile at a structure-local shaft cell, or null (for the salvage assertions).</summary>
  public BlockEntityCoalPile? PileAtLocal(int x, int y, int z) =>
    World.GetBlockEntity(Global(x, y, z)) as BlockEntityCoalPile;

  /// <summary>The block entity at a structure-local cell (for the frozen-cast-iron assertion).</summary>
  public BlockEntity? BlockEntityAtLocal(int x, int y, int z) =>
    World.GetBlockEntity(Global(x, y, z));

  #endregion
}
