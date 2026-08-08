using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Metals;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using IronworkingExpanded.Items;
using LowPressureExpanded.BlockNetworkPipe;
using LowPressureExpanded.BlockNetworkPipe.BlockEntities;
using LowPressureExpanded.Tests;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LowPressureExpanded.Tests;

/// <summary>
/// Drives the cupola furnace headlessly (docs/design/iwex.md, the scrap re-melter): a charged, lit,
/// blast-fed shaft climbs past cast iron's melt line, enters the Melting phase, re-melts remelt burden
/// into molten cast iron (and slag), taps cast iron out the lower tap into one canal and slag out the
/// upper into another, and - when extinguished mid-heat - freezes its pool onto the hearth as solid cast
/// iron while leaving the rest of the burden as salvageable spent charge (never slag). The cupola is the
/// same machine as the blast furnace (<see cref="BlockEntityCupolaFurnace"/> derives
/// <see cref="BlockEntityShaftFurnace"/>); this rig stands up its narrower peripherals - a single-column
/// shaft, a single tuyere, and the two taps + canals - and pumps the gated <c>OnProductionTick</c>.
/// Timers are fast-forwarded so the multi-minute melt is reachable in a test.
/// </summary>
internal sealed class CupolaRig
{
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

  /// <summary>Which fuel this scene lays its rounds with - see <see cref="WithFuel"/>.</summary>
  private string _fuelCode = CokeCode;
  private float _blastTemp = -1f;
  private float _blastPressure = 5f;

  /// <param name="charge">Scrap <b>metal units</b> laid into the shaft's single column. <b>0 fills it to
  /// capacity.</b> The cupola counts its charge in metal units, not items - a 5 u bit, a 25 u chunk and a
  /// 375 u pig all go into the same pile, which is how a cupola was charged in life - so one pile block
  /// holds <c>CupolaChargeMetalUnitsPerBlock</c> (3 000), not 32.</param>
  /// <param name="burden">Composition stamped on the charge (its coke fraction drives the heat balance).</param>
  /// <param name="chargeCode">Item path (iwex domain) the shaft piles hold. Defaults to <c>pigchunk</c> -
  /// it was <c>remeltburden</c> until the cupola started charging metal directly,
  /// once the only machine that made remelt burden was removed. Pass <c>burden</c> to charge it with
  /// something it will burn but never convert, and exercise the gate.</param>
  public CupolaRig(
    int charge = 0,
    BurdenMix? burden = null,
    string chargeCode = "pigchunk"
  )
  {
    _burden = burden ?? new BurdenMix(60f, 5f, 35f);
    _chargeCode = chargeCode;
    World = new TestWorld();

    // The taps resolve their molten carrier through MetalRegistry: cast iron -> iwex:ingot-castiron when
    // the metal is registered, else the game:ingot-castiron convention. Register both codes (and both slag
    // codes) so GetItem resolves whatever the tick asks for, independent of process-wide registry state.
    World.RegisterItem("iwex:ingot-castiron", 1200f);
    World.RegisterItem("game:ingot-castiron", 1200f);
    World.RegisterItem("iwex:slag", 1200f);
    World.RegisterItem("game:ingot-slag", 1200f);
    // The block the extinguished pool freezes into (BlockEntityHearthMetal, so StampSolidProduct lands).
    World.RegisterBlockEntityFactory(
      "iwex.BlockEntityHearthMetal",
      () => new BlockEntityHearthMetal()
    );
    Block solid = TestBlocks.Configure(
      new Block(),
      "iwex:hearthmetal-castiron",
      70,
      ("dummy", "x")
    );
    solid.EntityClass = "iwex.BlockEntityHearthMetal";
    World.Register(solid);

    // The charge pile and its entity class, so the cupola's own SyncChargeBlocks materialises real
    // BlockEntityChargePile windows onto its column - see ColdBlastFurnaceScenes for why the factory and
    // not just the block.
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
    World.RegisterItem("iwex:" + chargeCode);

    World.RegisterNetwork("pipe", s => new PipeNetwork(s));

    Furnace = new BlockEntityCupolaFurnace
    {
      Pos = _pos,
      Block = TestBlocks.Configure(
        new Block(),
        "iwex:furnace-cupolacore-tier1-n",
        1,
        ("side", "north")
      ),
    };
    World.Place(_pos, Furnace.Block, Furnace);
    World.Attach(Furnace);

    // The cupola's shipped layout, rotated to its facing (north -> 0). Peripherals go in first, then
    // the rig fills the shell and the furnace completes itself - see StructureRig for why this is not
    // a forced StructureComplete.
    Structure = StructureRig.Around(
      World,
      Furnace,
      BlockCupolaFurnaceCore.Definitions("iwex").Single(),
      angle: 0
    );

    // A single tuyere on the north face, its own blast network.
    // (0,2,-1), not (0,1,-1): the 2026-08-03 redraw lifted the cupola's inlet a course, so it blows
    // into the bottom of the shaft column rather than into the well - which is what puts the melt above
    // the metal it collects. Layer 1's north row is solid brick now, so the old cell left the structure
    // one short and every scenario in this file threw before it ran a single assertion.
    _tuyeres = [Tuyere(Structure.Cell(0, 2, -1), 40, "n")];

    Structure.Complete();

    // Charged after the structure stands: columns are the core's and it has none until its layout has
    // arrived, so a push before completion lands nowhere - silently, because a furnace with no shaft box
    // simply owns no columns.
    // 0 means "fill the shaft"; a negative charge means "leave it empty", for the scenes that lay their
    // own charge afterwards (see ChargeWithoutCoke).
    if (charge >= 0)
      Lay(charge > 0 ? charge : ShaftCapacityUnits);
  }

  /// <summary>Everything the cupola's shaft can hold - its single column's cell count times the remelt
  /// block quantum. What <c>charge: 0</c> means.</summary>
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
  /// Lays <paramref name="total"/> units of the scene's charge into the shaft, clamped to each column's
  /// own capacity. The cupola's shaft is a single column, so there is nothing to spread - but the walk is
  /// written over the column set rather than over a literal cell, because the cupola's whole point in this
  /// suite is that it is the <b>same machine</b> as the blast furnace with a narrower drawing.
  /// </summary>
  /// <param name="rounds">Lay <b>real rounds</b> - a fuel course, then a metal course, in the scene's own
  /// fuel (<see cref="WithFuel"/>). Only <see cref="ChargeWithoutCoke"/> passes false, and what it produces
  /// is a shaft that cannot burn.</param>
  private void Lay(int total, bool rounds = true)
  {
    if (total <= 0)
      return;
    int remaining = total;
    var keys = new List<(int X, int Z)>(Furnace.ShaftColumns.Keys);
    keys.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));

    foreach (var (x, z) in keys)
    {
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
        // A single fixed temperature, so a charge arrives as one band - see ColdBlastFurnaceScenes for
        // why a drifting one shatters the column into slivers the salvage assertions cannot address.
        column.Push($"iwex:{_chargeCode}", give, ChargeTemp, _burden ?? default);
      remaining -= give;
    }

    Furnace.SyncChargeBlocks();
  }

  /// <summary>
  /// Lays <paramref name="units"/> into one column as <b>real rounds</b> - a fuel course, then a metal
  /// course - at the scene's own fuel fraction, in whichever fuel <see cref="WithFuel"/> armed.
  /// <para>
  /// <b>A cupola is a shaft furnace, so it burns coke at a raceway like any other</b>, and every scene
  /// in this file once charged remelt burden alone. Carbon comes from fuel bands and nowhere
  /// else, so the cupola simply never lit: all six lifecycle cases were arranging the state by assignment
  /// and so could not see it.
  /// </para>
  /// <para>
  /// <b>The band is a volume, not a carbon quantity.</b> A
  /// charcoal round occupies the same <c>fuelPerRound</c> units of column as a coke round and carries
  /// <em>half</em> the carbon - so a scene switched to charcoal must burn out in about half the time, and
  /// <see cref="FuelBandUnits"/> and <see cref="CarbonUnits"/> stop being the same number. Scaling the band
  /// up to compensate would hide precisely the effect the charcoal cases exist to measure.
  /// </para>
  /// <para>
  /// <b>The fuel is measured in the cupola's own currency - metal units - and that is a simplification
  /// the charging UI still owes.</b> <c>layered-charge.md</c> rules a cupola pile block as
  /// <i>"3 000 u of metal <b>against</b> 8 bands of coke"</i>: the metal half is units and the coke half is
  /// items, which one integer per segment cannot carry. The furnace model does not care - a column is
  /// homogeneous in whatever unit that furnace counts, and <c>BlockEntityShaftFurnace.ChargeUnitScale</c> is
  /// what lets the shared raceway constants work at either scale. What is unresolved is how a player
  /// <em>loads</em> the two, and that belongs to the hopper, not here.
  /// </para>
  /// </summary>
  private void LayRounds(ChargeColumn column, int units)
  {
    int perRound = System.Math.Max(2, Furnace.ChargeUnitsPerBlock);
    float fuelFrac = (_burden ?? default).FuelFrac;
    int fuelPerRound = System.Math.Max(
      1,
      (int)(perRound * (fuelFrac > 0f ? fuelFrac : 0.35f))
    );

    int left = units;
    while (left > 0)
    {
      int fuel = System.Math.Min(fuelPerRound, left);
      column.Push(_fuelCode, fuel, ChargeTemp, default);
      left -= fuel;
      if (left <= 0)
        break;

      int metal = System.Math.Min(perRound - fuelPerRound, left);
      column.Push($"iwex:{_chargeCode}", metal, ChargeTemp, _burden ?? default);
      left -= metal;
    }
  }

  /// <summary>The fuel the scenes lay their rounds with by default. A scene may lay any fuel the registry
  /// grants - see <see cref="ChargeWithFuel"/> - which is what lets the cupola gain a charcoal counterpart
  /// for every case without a second rig.</summary>
  public const string CokeCode = "game:coke";

  /// <summary>Vanilla charcoal: the pre-coke reductant, worth half of coke per unit.</summary>
  public const string CharcoalCode = "game:charcoal";

  /// <summary>The temperature the scene's charge is laid at - one fixed value, because
  /// <see cref="ChargeColumn.Push"/> merges only within 1 °C.</summary>
  private const float ChargeTemp = 20f;

  /// <summary>The mix the scene stamped on its charge - for the cases that must prove a furnace is reading
  /// the fuel bands and not the stamp.</summary>
  public BurdenMix ChargedMix => _burden ?? default;

  /// <summary>Carbon still standing in the shaft - what a campaign is made of. The historical spelling of
  /// <see cref="FuelBandUnits"/>, kept because the scenarios read it; on a coke-charged cupola the two
  /// numbers and <see cref="CarbonUnits"/> all coincide, which is why the distinction stayed invisible.
  /// </summary>
  public int CokeUnits => FuelBandUnits;

  /// <summary>
  /// Charge units standing in <b>fuel bands</b> of any material - the column volume the fuel occupies.
  /// <para>
  /// Asked through the production predicate (<c>IsFuelCode</c>), never by comparing to a coke literal -
  /// which is what this read once did. A string compare answers "not fuel" for charcoal, so a
  /// charcoal-charged cupola read zero carbon: <c>A_full_shaft_with_no_coke_at_the_raceway_stays_cold</c>
  /// would have passed on a shaft packed with perfectly good fuel, and <see cref="HeatSoak"/>'s failure
  /// message would have reported an empty shaft on a furnace that was merely slow.
  /// </para>
  /// <para>
  /// <b>Not the same number as <see cref="CarbonUnits"/> once a shaft can hold two fuels.</b> A charcoal
  /// band occupies a band's worth of column and carries half a band's worth of carbon; conflating the two is
  /// the double-count this model has already made once.
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
  /// <c>CarbonPerUnit</c>. This is what the campaign length is actually made of: the raceway spends
  /// carbon, never bands, so a charcoal cupola of the same volume goes out twice as soon.
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

  /// <summary>Fills the shaft with charge alone - no fuel bands: a full cupola that cannot burn.</summary>
  public CupolaRig ChargeWithoutCoke(int units = 0)
  {
    Lay(units > 0 ? units : ShaftCapacityUnits, rounds: false);
    return this;
  }

  /// <summary>
  /// Switches which fuel the scene's rounds are laid with - <see cref="CokeCode"/> by default,
  /// <see cref="CharcoalCode"/> for the charcoal twin of any case.
  /// <para>
  /// Parameterising the fuel rather than adding a second rig is what makes every existing cupola
  /// scenario gain a charcoal counterpart for free - and a counterpart is the only thing that can tell a
  /// furnace that <em>prices</em> its fuel from one that merely accepts it. Call it before charging: the
  /// rig lays its charge in the constructor, so a scene wanting charcoal passes <c>charge: -1</c> and
  /// charges itself (or uses <see cref="ChargeWithFuel"/>).
  /// </para>
  /// </summary>
  public CupolaRig WithFuel(string fuelCode)
  {
    _fuelCode = fuelCode;
    return this;
  }

  /// <summary>Lays a full shaft of rounds in <paramref name="fuelCode"/> - the charcoal twin of the
  /// constructor's default charge.</summary>
  public CupolaRig ChargeWithFuel(string fuelCode, int units = 0)
  {
    WithFuel(fuelCode);
    Lay(units > 0 ? units : ShaftCapacityUnits);
    return this;
  }

  /// <summary>
  /// The blast-fed tuyere cell: the real <c>iwex:furnace-tuyere-*</c> block, not a generic pipe. Both behave
  /// identically as network nodes, but only the tuyere satisfies the furnace layout.
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

  // Both taps go on literal structure-local cells, not on `Furnace.MetalTapPos`/`SlagTapPos`.
  //
  // Reading the accessors looks tidier and made this rig worthless as an oracle: a scene that places its
  // taps wherever the roles say cannot observe the roles being wrong. Swap the cupola drawing's `T` and
  // `S` marks and a role-following rig swaps its own two canals to match, every assertion still holds, and
  // `A_melting_cupola_taps_cast_iron_low_and_slag_high` - a test whose name is a claim about which tap is
  // which - stays green. Literals make the scene an independent statement of the drawing, so the swap
  // pours cast iron into the canal under the high tap and the metal-type assertions bite.
  //
  // The values are the cupola's own `I` at (-1,1,0) and `S` at (1,1,0). Named through the structure so
  // they follow its rotation (this rig stands at north, so the two coincide) exactly as the tuyere cell
  // above does.
  //
  // `S` was (1,2,0) - a course above the metal tap - until the 2026-08-03 redraw brought both notches
  // down onto the hearth course, on opposite walls. The facing is now part of each cell's demand too,
  // and it reads backwards: `TryPourMetal` spouts at `facing.Opposite`, so the west-wall iron notch is
  // `-east` and the east-wall cinder notch is `-west`. A `-north` tap - which is what both of these were
  // while the legend wildcarded the facing - now aims its spout at the cell south of it, inside the
  // furnace's own shell.

  /// <summary>Places the cast-iron tap on the drawing's own <c>I</c> cell - the west wall, so the block
  /// faces east - with a canal start under its spout, open.</summary>
  public CupolaRig WithCastIronTapAndCanal()
  {
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
  public CupolaRig WithSlagTapAndCanal()
  {
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
  )
  {
    var tap = new BlockEntityFurnaceTap
    {
      Pos = tapPos.Copy(),
      Block = TestBlocks.Configure(
        new Block(),
        $"iwex:furnace-{type}-{side}",
        tapId,
        ("type", type),
        ("side", side)
      ),
    };
    World.Place(tapPos, tap.Block, tap);
    World.Attach(tap);
    tap.TogglePouring(); // open

    // The tap pours into the canal start at Pos + facing.Opposite + down (its spout foot).
    BlockPos canalPos = tapPos
      .AddCopy(BlockFacing.FromCode(side).Opposite)
      .DownCopy();
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

  // `SetState`, `SetSecondsAboveMelting`, `SetMeltSeconds` and `SetFuelBurnSeconds` are gone.
  // A cupola is a shaft furnace, so it recomputes what it is doing from its charge every
  // tick: a case that arranged one by writing `State = Melting` and a soak counter was stating a premise
  // the next tick discarded, and every one of them was green while the machine underneath had never lit at
  // all - the scenes charged no coke, so the real furnace could not have caught fire. `State` has no setter
  // now (`FurnaceBranchGuards.NoFurnaceExposesASettableState`), so the same call throws.

  /// <summary>
  /// Runs the cupola until <paramref name="done"/> holds, up to <paramref name="maxSeconds"/>, returning the
  /// seconds it took (or -1). The replacement for every <c>Set*</c> above: a melting cupola is one that
  /// was built, charged and blown until it melted.
  /// </summary>
  public int RunUntil(
    System.Func<CupolaRig, bool> done,
    int maxSeconds,
    System.Action<CupolaRig>? each = null
  )
  {
    for (int i = 1; i <= maxSeconds; i++)
    {
      Tick(1);
      each?.Invoke(this);
      if (done(this))
        return i;
    }
    return -1;
  }

  /// <summary>Runs until the cupola is <see cref="FurnaceState.Melting"/>, failing legibly if it never
  /// gets there - so a case about melting cannot silently become a case about idling.</summary>
  public CupolaRig HeatSoak(int maxSeconds = 600)
  {
    int took = RunUntil(r => r.State == FurnaceState.Melting, maxSeconds);
    Xunit.Assert.True(
      took > 0,
      // Reports the weighted carbon, not the band volume: on a charcoal scene the two differ by 2x, and
      // the volume would make a half-fuelled shaft look fully stocked in the one message that has to
      // explain why it never melted.
      $"the cupola should have reached Melting within {maxSeconds} s; it was {State} at "
        + $"{Temp:F0} C with {CarbonUnits:F0} u of carbon left ({FuelBandUnits} u of fuel bands)"
    );
    return this;
  }

  public CupolaRig SetTemp(float t)
  {
    ReflectionHelpers.SetField(Furnace, "_internalTemp", t);
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

  /// <summary>Full item code of what the upper tap poured. The half that makes "slag high" a real
  /// statement: a unit count alone stays positive when the two taps are swapped, because something still
  /// pours. Only the material says which.</summary>
  public string? SlagCanalMetalType => SlagCanal?.CellMetalType;

  /// <summary>The block at a structure-local cell (for the extinguish-residue assertions).</summary>
  public Block BlockAtLocal(int x, int y, int z) => World.GetBlock(Global(x, y, z));

  /// <summary>The charge pile at a structure-local shaft cell, or null (for the salvage assertions).</summary>
  public BlockEntityChargePile? PileAtLocal(int x, int y, int z) =>
    World.GetBlockEntity(Global(x, y, z)) as BlockEntityChargePile;

  /// <summary>
  /// The burden mix standing in the charge block at a structure-local cell - <c>default</c> when the
  /// column does not reach that cell, or when that block holds no burden at all. Reads the <b>column</b>:
  /// a charge pile is a window onto it and holds no inventory of its own, which is what makes breaking one
  /// safe.
  /// <para>
  /// <b>It skips fuel bands, and it has to</b> <i>(mirrored from
  /// <c>ColdBlastFurnaceScenes.SalvageAtLocal</c>)</i>. Since the cupola charges real rounds, the band at
  /// the <em>base</em> of a block is the round's fuel course - a segment carrying <c>default</c> mix,
  /// because coke is carbon and nothing else - so the unfiltered walk returned an empty struct for a block
  /// full of perfectly good salvage. It answered <c>default</c> for every rounds-charged cell, which is a
  /// reading no assertion could distinguish from "the salvage was lost".
  /// </para>
  /// <para>
  /// The span is bounded at <b>both</b> ends. Without the <c>high</c> clause the walk runs off the end of
  /// this block's own slice and reports the burden of the block <em>above</em>, which is the granularity
  /// burn-out decides at (<c>BlockEntityShaftFurnace.BurnOutCharge</c> is per block for exactly this
  /// reason) - so a per-cell claim would silently become a claim about a different cell.
  /// </para>
  /// </summary>
  public BurdenMix SalvageAtLocal(int x, int y, int z)
  {
    if (Furnace.ChargeColumnAt(Global(x, y, z), out int blockIndex) is not { } column)
      return default;

    int low = blockIndex * Furnace.ChargeUnitsPerBlock;
    int high = low + Furnace.ChargeUnitsPerBlock;
    int at = 0;
    foreach (ChargeSegment segment in column.Segments)
    {
      int end = at + segment.Units;
      // `IsFuelCode`, never `!= CokeCode`. The literal answers "this is burden" for a charcoal band, so a
      // charcoal cupola's salvage read would come back as `default` - silently, and in the direction that
      // looks like lost salvage.
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
  /// Whether a <b>fuel</b> band stands in the block-sized slice of the column covering the structure-local
  /// cell <c>(x, y, z)</c>.
  /// <para>
  /// It exists to let a case state the premise the extinguish residue turns on rather than assume it:
  /// <c>BlockEntityFurnaceCore.PileHoldsRejectedCharge</c> walks exactly this slice to decide whether a
  /// pool cell is free to freeze over, and a cupola that ran its carbon out has none left there - which is
  /// why the ordinary burn-out case never saw the wrong-family misread of a coke band.
  /// </para>
  /// </summary>
  public bool HoldsFuelAtLocal(int x, int y, int z)
  {
    if (
      Furnace.ChargeColumnAt(Global(x, y, z), out int blockIndex) is not { } column
    )
      return false;

    int perBlock = System.Math.Max(1, Furnace.ChargeUnitsPerBlock);
    int low = blockIndex * perBlock;
    int at = 0;
    foreach (ChargeSegment segment in column.Segments)
    {
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
