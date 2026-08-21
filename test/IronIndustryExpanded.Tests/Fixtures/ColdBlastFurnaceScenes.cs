using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Heat;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkMolten.BlockEntities;
using IronIndustryExpanded.BlockNetworkMolten.Blocks;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.BlockStructures.Products.BlockEntities;
using IronIndustryExpanded.Items;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Named scenes for the cold blast furnace. Each stands up the real 160-cell footprint of
/// <see cref="BlockBlastFurnaceCoreCold"/> through <see cref="StructureRig"/>, so the furnace completes
/// itself rather than being told it is complete, and hands back a <see cref="ColdBlastFurnaceRig"/> the
/// test drives on the clock.
/// </summary>
internal static class ColdBlastFurnaceScenes {
  /// <summary>
  /// A burden rich enough in coke to clear the cold break-even. With no cowper the break-even is ~23.9 %
  /// coke at the shipped tunables; 30 % settles the hearth at ~1577 C against a 1482 C melt line.
  /// </summary>
  public static BurdenMix HighCoke => Mix(0.30f);

  /// <summary>
  /// The <c>standard</c> grade - 20 % coke, the reference the heat balance is calibrated at. On cold blast
  /// it settles at 1420 C and never melts, which the furnace names through <c>bf-info-heatstall</c>.
  /// </summary>
  public static BurdenMix Standard => Mix(IiexValues.BfReferenceFuelFrac);

  /// <summary>A burden of a given coke fraction carrying 5 % flux - the same shape
  /// <c>HeatBalanceTests.Burden</c> uses, so the two agree on what "30 % coke" means.</summary>
  public static BurdenMix Mix(float fuelFrac) =>
    new(1f - 0.05f - fuelFrac, 0.05f, fuelFrac);

  /// <summary>
  /// A complete, correctly-tapped furnace charged with a coke-rich burden and blown with ambient air. Both
  /// taps are installed and closed, each over its own canal start; the shaft carries twice the fire
  /// threshold so a campaign can run without starving itself mid-test.
  /// </summary>
  public static ColdBlastFurnaceRig Complete(
    int charge = 2 * 320,
    float fuelFrac = 0.30f
  ) =>
    new ColdBlastFurnaceRig(charge: charge, burden: Mix(fuelFrac))
      .PressuriseBlast()
      .BlowIn();

  /// <summary>
  /// A complete, blown furnace charged to the brim with burden and no coke: a full shaft, a complete
  /// raceway course, and nothing in front of the tuyeres that can burn. The discriminating scene for the
  /// fuel half of the ignition gate - any scene that lit on quantity would light on this one.
  /// </summary>
  public static ColdBlastFurnaceRig NoCokeAtTheRaceway() =>
    new ColdBlastFurnaceRig(charge: -1, burden: HighCoke)
      .PressuriseBlast()
      .ChargeWithoutCoke()
      .BlowIn();

  /// <summary>
  /// <see cref="Complete"/> charged with the <c>standard</c> 20 % burden: it lights, holds at 1420 C and
  /// never crosses the melt line. Charged to the brim (<c>charge: 0</c>) because burn-out interpolates the
  /// surviving coke over the shaft's height, so a column has to reach the top course for the gradient to
  /// have two ends - in a part-charged shaft local (0,5,0), which the salvage cases read, is empty air.
  /// </summary>
  public static ColdBlastFurnaceRig StandardBurden(int charge = 0) =>
    new ColdBlastFurnaceRig(charge: charge, burden: Standard)
      .PressuriseBlast()
      .BlowIn();

  /// <summary>
  /// A complete, blown furnace with its whole charge piled into one column - <paramref name="units"/> in
  /// the shaft and eight of its nine tuyeres blowing into empty air. The discriminating scene for the
  /// geometric half of the ignition gate; every other scene here lays a full course.
  /// </summary>
  public static ColdBlastFurnaceRig OneTallColumn(int units = 3 * 320) =>
    new ColdBlastFurnaceRig(charge: -1, burden: HighCoke)
      .PressuriseBlast()
      .PileIntoOneColumn(units)
      .BlowIn();

  /// <summary>
  /// <see cref="Complete"/> with the iron tap installed backwards - facing east out of the east wall, so
  /// its spout aims into the furnace's own base where there is no canal. The layout pins the tap facing,
  /// so this rig stands up one cell short, with <c>StructureComplete</c> false.
  /// </summary>
  public static ColdBlastFurnaceRig BackwardsIronTap(int charge = 2 * 320) =>
    new ColdBlastFurnaceRig(charge: charge, burden: HighCoke, ironTapSide: "e")
      .PressuriseBlast()
      .BlowIn();
}

/// <summary>
/// Drives the cold blast furnace headlessly: a charged, lit, blown shaft climbs past iron's melt line,
/// enters Melting, renders ore burden into molten pig iron and slag, taps them into canals, and on going
/// out freezes its pool onto the hearth and leaves the rest as salvage.
/// <para>Every cell of the shipped layout is occupied by the real block and the furnace's own monitor tick
/// completes the structure; nothing forces <c>StructureComplete</c>, so a wrong tap facing, a mis-coded
/// hopper or a tuyere one cell out shows up as a furnace that never completes. <see cref="RunLive"/>
/// advances the world clock and lets the registered production tick fire.</para>
/// </summary>
internal sealed class ColdBlastFurnaceRig {
  /// <summary>Where the anchor stands. High enough that the shaft has room above it.</summary>
  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>
  /// The temperature the scene's charge is laid at, in C - fixed rather than live ambient because
  /// <see cref="ChargeColumn.Push"/> merges only within 1 C, so a drifting temperature arrives as a pile
  /// of one-unit segments and the extinguish cases read whichever sliver landed at their cell.
  /// </summary>
  private const float ChargeTemp = 20f;

  public readonly TestWorld World;

  /// <summary>The furnace core - the anchor of the multiblock and the thing under test.</summary>
  public readonly BlockEntityBlastFurnaceCold Core;

  /// <summary>The standing structure, for tests that address cells by their authored local offsets.</summary>
  public readonly StructureRig Structure;

  /// <summary>The tall hopper that charges the shaft - a real one, at the layout's <c>H</c> cell.</summary>
  public readonly BlockEntityHopperTall Hopper;

  public readonly BlockEntityFurnaceTap IronTap;
  public readonly BlockEntityFurnaceTap SlagTap;

  /// <summary>The runout the iron tap pours into, at the cell a correctly installed tap aims at.</summary>
  public readonly BlockEntityMoltenCanalStart IronCanal;
  public readonly BlockEntityMoltenCanalStart SlagCanal;

  private readonly PipeNetwork[] _tuyeres;
  private readonly BurdenMix _burden;

  /// <summary>Item path (iiex domain) the columns hold - what a <c>ChargeSegment.Material</c> reads as.</summary>
  private readonly string _chargeCode;

  /// <summary>Which fuel this scene lays its rounds with - see <see cref="WithFuel"/>.</summary>
  private string _fuelCode = CokeCode;
  private float _blastTemp = -1f;
  private float _blastPressure = 5f;
  private int _nextBlockId = 100;
  private long _hudClockMs = 5000;

  /// <summary>
  /// The canonical facings of the two taps: each sits in a wall and faces into the furnace, so its spout
  /// (<c>Pos + side.Opposite + down</c>) lands outside the footprint. The east-wall metal tap faces west
  /// and the west-wall slag tap east; inverting either aims the runout into the furnace's own masonry.
  /// Single letters, since that is what a <c>side</c> variant renders into the block code the layout
  /// matches. <c>docs/design/layouts.txt</c> section 1 has this pair the wrong way round.
  /// </summary>
  private const string IronTapFacing = "w";
  private const string SlagTapFacing = "e";

  /// <param name="charge">Total burden items laid into the shaft, spread across every column in proportion
  /// to its cell count. 0 fills the shaft to capacity, negative leaves it empty.</param>
  /// <param name="burden">Composition stamped on that charge - its coke fraction drives the heat balance.</param>
  /// <param name="chargeCode">Item path (iiex domain) the piles hold. Defaults to the blast furnace's own
  /// ore <c>burden</c>; <c>remeltburden</c> charges the wrong family and exercises the gate.</param>
  /// <param name="ironTapSide">Side variant of the metal tap, defaulting to
  /// <see cref="IronTapFacing"/>; its opposite installs the tap backwards.</param>
  /// <param name="slagTapSide">Side variant of the slag tap; see <paramref name="ironTapSide"/>.</param>
  public ColdBlastFurnaceRig(
    int charge = 640,
    BurdenMix? burden = null,
    string chargeCode = "burden",
    string? ironTapSide = null,
    string? slagTapSide = null
  ) {
    _burden = burden ?? ColdBlastFurnaceScenes.HighCoke;
    _chargeCode = chargeCode;
    World = new TestWorld();
    // The tap's right-click only toggles server-side, and the fake world does not declare a side.
    World.World.Side.Returns(EnumAppSide.Server);

    // The metal tap resolves its carrier through MetalRegistry: iiex:ingot-pigiron when the metal is
    // registered, else the game:ingot-<code> convention. Both spellings are registered so GetItem answers
    // whatever the tick asks for, independent of process-wide registry state.
    World.RegisterItem("iiex:ingot-pigiron", 1500f);
    World.RegisterItem("game:ingot-pigiron", 1500f);
    World.RegisterItem("iiex:slag", 1500f);
    World.RegisterItem("game:ingot-slag", 1500f);
    // Registered so a column's material code resolves back to an item when a player takes a band by hand.
    World.RegisterItem("iiex:" + chargeCode);

    // The block the extinguished pool freezes into, with a factory for its entity class so the
    // The melt places hearth blocks into the crucible floor and pools into their cells, so both the block
    // and the cells have to resolve before anything can be melted.
    HearthRig.Register(World, "iiex:hearthmetal-pigiron", 70);

    // The charge pile and its entity class, so the furnace's own SyncChargeBlocks materialises real
    // BlockEntityChargePile windows onto its columns. Without the factory the SetBlock still lands a block
    // and every placement assertion still passes, while OnColumnChanged has nothing to call.
    World.RegisterBlockEntityFactory(
      "iiex.BlockEntityChargePile",
      () => new BlockEntityChargePile()
    );
    // A real `BlockChargePile`, not a plain `Block`: with a plain one `pile.Block.OnBlockBroken` reaches
    // vanilla's implementation, which says nothing about the charge behind the pile.
    Block chargePile = TestBlocks.Configure(
      new BlockChargePile(),
      BlockChargePile.PileCode.ToShortString(),
      71,
      ("type", "chargepile")
    );
    chargePile.EntityClass = "iiex.BlockEntityChargePile";
    // A block that never went through the asset pipeline has no `api`, and vanilla's
    // Block.OnBlockBroken dereferences it for the break particles.
    ReflectionHelpers.SetField(chargePile, "api", World.Api);
    World.Register(chargePile);

    World.RegisterNetwork("pipe", s => new PipeNetwork(s));

    Core = new BlockEntityBlastFurnaceCold {
      Pos = Anchor,
      Block = TestBlocks.Configure(
        new Block(),
        "iiex:furnace-blastcore-tier1-n",
        1,
        ("side", "n")
      ),
    };
    World.Place(Anchor, Core.Block, Core);
    World.Attach(Core);

    // The shipped layout, rotated to the core's own facing (north -> 0). Everything the furnace must see
    // goes in first; the rig then raises the rest of the shell and lets the furnace notice its own
    // completed structure.
    Structure = StructureRig.Around(
      World,
      Core,
      BlockBlastFurnaceCoreCold.Definitions("iiex").Single(),
      angle: 0
    );

    // Blast intakes: a real tuyere block at each of the layout's two Y cells, each on its own network.
    _tuyeres =
    [
      // y=2, not y=1: the blast enters the top of the hearth rather than the molten pool, and the two
      // cells sit outboard at z=-2 and z=2.
      Tuyere(Structure.Cell(0, 2, -2), 20, "n"),
      Tuyere(Structure.Cell(0, 2, 2), 21, "s"),
    ];

    // Both taps, each closed, each with its runout canal placed where a correctly installed tap aims. The
    // canal position comes from the canonical facing rather than the side actually fitted, so a backwards
    // tap points away from a good runout rather than having no canal at all. The layout's two glyphs pin
    // the types - `T` takes only `furnace-irontap-*`, `S` only `furnace-slagtap-*` - so swapping these two
    // calls leaves the furnace permanently incomplete.
    (IronTap, IronCanal) = TapAndCanal(
      Structure.Cell(2, 1, 0),
      BlockFurnaceTap.IronType,
      ironTapSide ?? IronTapFacing,
      IronTapFacing
    );
    // (-2,1,0), not (-2,2,0): both taps sit on the hearth course, so the slag notch is beside the iron
    // one rather than a level above it.
    (SlagTap, SlagCanal) = TapAndCanal(
      Structure.Cell(-2, 1, 0),
      BlockFurnaceTap.SlagType,
      slagTapSide ?? SlagTapFacing,
      SlagTapFacing
    );

    // The tall hopper, at the layout's 'H' cell and carrying the -east side variant the legend demands; a
    // hopper coded without that variant leaves the structure permanently incomplete. It sits west of the
    // shaft, and its `side` decides which neighbour the drip searches first - the column set is a
    // rotation-symmetric cross, so the facing changes the order, not the cells.
    //
    // Placed, not initialized: World.Attach only assigns Api, so the drip listener
    // BlockEntityHopperTall.Initialize would register never exists and the hopper is inert. This scene
    // pins the hopper's block code, nothing about its behaviour. See Recharge.
    Hopper = new BlockEntityHopperTall();
    BlockPos hopperPos = Structure.Cell(-1, 6, 0);
    World.Place(
      hopperPos,
      TestBlocks.Configure(
        new BlockHopperTall(),
        "iiex:hopper-tall-e",
        80,
        ("side", "e")
      ),
      Hopper
    );
    World.Attach(Hopper);

    // Fill the remaining shell, run the real Initialize, and let the monitor tick find the structure.
    // Structure.Complete throws with a per-cell breakdown if the furnace cannot see what the rig built.
    // A tap fitted backwards leaves its cell unsatisfied and the furnace never completes, so that scene
    // stands up incomplete instead; every other scene gets the strict form.
    bool tapsAsDrawn =
      (ironTapSide ?? IronTapFacing) == IronTapFacing
      && (slagTapSide ?? SlagTapFacing) == SlagTapFacing;
    if (tapsAsDrawn)
      Structure.Complete();
    else {
      Structure.Raise();
      World.Initialize(Core);
      Structure.AwaitCompletion();
    }

    // The charge goes in after the structure stands: a furnace with no box owns no columns, so a push
    // before completion lands nowhere and does so silently. 0 fills the shaft; a negative charge leaves it
    // empty for the scenes that lay their own afterwards (see PileIntoOneColumn).
    if (charge >= 0)
      Lay(charge > 0 ? charge : ShaftCapacityUnits);
  }

  #region Standing the peripherals up

  /// <summary>
  /// A blast-fed tuyere cell: the real <c>iiex:furnace-tuyere-*</c> block on its own single-node network.
  /// A generic pipe behaves identically as a network node but does not satisfy the furnace layout.
  /// </summary>
  /// <param name="orientation">Connector face, which the layout pins: <c>n</c> for the cell in the north
  /// wall, <c>s</c> for the south. The same letter for both leaves the furnace permanently incomplete.</param>
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
  /// Places a real tap at <paramref name="tapPos"/> wearing <paramref name="fittedSide"/>, plus the canal
  /// start under the spout of a tap wearing <paramref name="canonicalSide"/>. Both are left closed:
  /// opening goes through <see cref="Open"/>, that is through the production right-click.
  /// </summary>
  private (
    BlockEntityFurnaceTap tap,
    BlockEntityMoltenCanalStart canal
  ) TapAndCanal(
    BlockPos tapPos,
    string type,
    string fittedSide,
    string canonicalSide
  ) {
    var tap = new BlockEntityFurnaceTap();
    World.Place(
      tapPos,
      TestBlocks.Configure(
        new BlockFurnaceTap(),
        $"iiex:furnace-{type}-{fittedSide}",
        _nextBlockId++,
        ("type", type),
        ("side", fittedSide)
      ),
      tap
    );
    World.Attach(tap);

    // ExOrientation.FacingFromSide, never BlockFacing.FromCode: vanilla's returns null for a single letter,
    // and the facing constants above are letters.
    BlockPos canalPos = tapPos
      .AddCopy(ExOrientation.FacingFromSide(canonicalSide)!.Opposite)
      .DownCopy();
    var canal = new BlockEntityMoltenCanalStart();
    World.Place(
      canalPos,
      TestBlocks.Configure(
        new BlockMoltenCanalStart(),
        "iiex:moltencanalstart-ns",
        _nextBlockId++,
        ("type", "start"),
        ("orientation", "ns")
      ),
      canal
    );
    World.Attach(canal);
    return (tap, canal);
  }

  /// <summary>
  /// Piles <paramref name="units"/> into one column and leaves the other eight empty - a shaft that is
  /// full by any total-units measure with an incomplete raceway course. The only thing in the suite that
  /// separates the geometric ignition gate from a threshold.
  /// </summary>
  public ColdBlastFurnaceRig PileIntoOneColumn(int units) {
    var keys = new List<(int X, int Z)>(Core.ShaftColumns.Keys);
    keys.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
    var (x, z) = keys[0];
    Core.ChargeColumnAt(x, z)!
      .Push($"iiex:{_chargeCode}", units, ChargeTemp, _burden);
    Core.SyncChargeBlocks();
    return this;
  }

  /// <summary>
  /// The shaft's column keys in the ascending <c>(x, z)</c> order the furnace walks them in, so "the first
  /// column" means what the production code means.
  /// </summary>
  public List<(int X, int Z)> ColumnKeys {
    get {
      var keys = new List<(int X, int Z)>(Core.ShaftColumns.Keys);
      keys.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
      return keys;
    }
  }

  /// <summary>
  /// Lays one round - <paramref name="fuelUnits"/> of fuel, then <paramref name="burdenUnits"/> of burden
  /// - into the single column <c>(<paramref name="x"/>, <paramref name="z"/>)</c>, repeated
  /// <paramref name="rounds"/> times. The only way to charge two columns differently, which the chill
  /// cases need, since a hang is per column. Fuel first, then burden: that is the order
  /// <c>NextChargeColumn</c>'s band-order rule allows, and burden first builds a column no hopper can lay.
  /// </summary>
  /// <param name="burdenTemp">Temperature the burden bands are laid at, in C; <c>-1</c> uses the scene's
  /// own charging temperature. The chill cases set it directly, since waiting for the counter-current
  /// model to chill a column kills the furnace first.</param>
  public ColdBlastFurnaceRig LayCourseInto(
    int x,
    int z,
    int fuelUnits,
    int burdenUnits,
    int rounds = 1,
    float burdenTemp = -1f
  ) {
    ChargeColumn column =
      Core.ChargeColumnAt(x, z)
      ?? throw new InvalidOperationException($"no column at ({x},{z})");

    float temp = burdenTemp < 0f ? ChargeTemp : burdenTemp;
    for (int i = 0; i < rounds; i++) {
      if (fuelUnits > 0)
        column.Push(_fuelCode, fuelUnits, ChargeTemp, default);
      if (burdenUnits > 0)
        column.Push($"iiex:{_chargeCode}", burdenUnits, temp, _burden);
    }

    Core.SyncChargeBlocks();
    return this;
  }

  /// <summary>Units standing in one named column - the per-column half of <see cref="ColumnUnits"/>. A
  /// hang has to be asserted here: a hung column holds its charge while its neighbour's falls, which the
  /// shaft total alone cannot show.</summary>
  public int ColumnUnitsAt(int x, int z) =>
    Core.ChargeColumnAt(x, z)?.TotalUnits ?? 0;

  /// <summary>Whether the named column has chilled - production's own read.</summary>
  public bool IsHung(int x, int z) => Core.IsHung(x, z);

  /// <summary>How many columns have chilled - production's own read.</summary>
  public int HungColumns => Core.HungColumnCount;

  /// <summary>
  /// The mix the furnace read at its raceway on its last tick, not a hand-built <see cref="BurdenMix"/>:
  /// a hang's claim is that it moves what the furnace reads.
  /// </summary>
  public BurdenMix RacewayMix =>
    (BurdenMix)ReflectionHelpers.GetField(Core, "_chargeMix")!;

  /// <summary>
  /// Knocks a wall block out of the furnace. Takes the first cell the rig filled with a plain stand-in, so
  /// it never hits a tuyere, a tap, the hopper or an open shaft cell.
  /// </summary>
  public ColdBlastFurnaceRig Breach() {
    foreach (var (pos, wanted) in Structure.Cells) {
      if (wanted.Contains('@') || wanted.Contains('*'))
        continue; // an alternation - an open shaft cell or a fuel slot, not a wall
      if (World.GetBlock(pos).Id == 0)
        continue;
      World.Place(
        pos,
        new Block { Code = new AssetLocation("game:air"), BlockId = 0 }
      );
      return this;
    }
    throw new InvalidOperationException("no wall cell to breach");
  }

  /// <summary>Total units standing in the shaft's columns, read straight off them - unlike
  /// <see cref="ShaftUnits"/>, which is what the furnace cached on its last tick.</summary>
  public int ColumnUnits => Core.ShaftChargeUnits;

  /// <summary>Everything the shaft can hold - every column's own cell count times the furnace's block
  /// quantum. What <c>charge: 0</c> means, and the ceiling <see cref="Lay"/> spreads under.</summary>
  public int ShaftCapacityUnits {
    get {
      int total = 0;
      foreach (var (x, z) in Core.ShaftColumns.Keys)
        total += Core.ColumnCapacity(x, z);
      return total;
    }
  }

  /// <summary>
  /// Lays <paramref name="total"/> items of the scene's burden into the shaft, spread over every column in
  /// proportion to its cell count. Proportional, not equal: three of the nine columns are a cell taller
  /// (the crucible well), so an equal split pushes the short six past their own roofs. Not the shipped
  /// selection rule (<see cref="BlockEntityFurnaceCore.NextChargeColumn"/>), so a broken selection stays
  /// detectable. The remainder goes to the first keys in ascending <c>(x, z)</c>.
  /// </summary>
  /// <param name="rounds">Lay real rounds - a coke course, then a burden course. Only
  /// <see cref="ChargeWithoutCoke"/> passes false, which builds a shaft that cannot burn.</param>
  private void Lay(int total, bool rounds = true) {
    if (total <= 0)
      return;

    var keys = new List<(int X, int Z)>(Core.ShaftColumns.Keys);
    keys.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
    if (keys.Count == 0)
      return;

    int capacity = ShaftCapacityUnits;
    var want = new int[keys.Count];
    int assigned = 0;
    for (int i = 0; i < keys.Count; i++) {
      want[i] = (int)(
        (long)total
        * Core.ColumnCapacity(keys[i].X, keys[i].Z)
        / Math.Max(1, capacity)
      );
      assigned += want[i];
    }
    for (int i = 0; assigned < total && i < keys.Count; i++) {
      want[i]++;
      assigned++;
    }

    // Clamp each column to its own room and carry the overflow forward, so a scene asking for more than
    // the shaft holds fills it rather than pushing charge through the roof.
    int carry = 0;
    for (int i = 0; i < keys.Count; i++) {
      var (x, z) = keys[i];
      ChargeColumn column = Core.ChargeColumnAt(x, z)!;
      int room = Core.ColumnCapacity(x, z) - column.TotalUnits;
      int give = Math.Min(want[i] + carry, room);
      carry = want[i] + carry - give;
      if (give <= 0)
        continue;
      if (rounds)
        LayRounds(column, give);
      else
        column.Push($"iiex:{_chargeCode}", give, ChargeTemp, _burden);
    }

    Core.SyncChargeBlocks();
  }

  /// <summary>
  /// Fills every column to the brim with burden alone - no coke bands, a full shaft that cannot burn. The
  /// burden still carries its stamped coke fraction, which is a grade signal and not a fuel supply: a
  /// furnace reading carbon off the stamp lights here. See <c>BlockEntityShaftFurnace.Accumulate</c>.
  /// </summary>
  public ColdBlastFurnaceRig ChargeWithoutCoke(int units = 0) {
    Lay(units > 0 ? units : ShaftCapacityUnits, rounds: false);
    return this;
  }

  /// <summary>
  /// Lays <paramref name="units"/> into one column as real rounds - a coke course, then a burden course,
  /// repeating - at the scene's own coke fraction. Carbon has to arrive as its own bands: coke stamped
  /// into a burden band cannot burn away, so nothing descends and the shaft locks solid once the bottom
  /// band burns out. The stamped fuel fraction is a grade signal only.
  /// </summary>
  private void LayRounds(ChargeColumn column, int units) {
    // One round is one charge block, split at the scene's coke fraction - what a player lays.
    int perRound = Math.Max(2, Core.ChargeUnitsPerBlock);
    int fuelPerRound = Math.Max(1, (int)(perRound * _burden.FuelFrac));

    int left = units;
    while (left > 0) {
      int fuel = Math.Min(fuelPerRound, left);
      column.Push(_fuelCode, fuel, ChargeTemp, default);
      left -= fuel;
      if (left <= 0)
        break;

      int burden = Math.Min(perRound - fuelPerRound, left);
      column.Push($"iiex:{_chargeCode}", burden, ChargeTemp, _burden);
      left -= burden;
    }
  }

  /// <summary>
  /// Switches which fuel the scene's rounds are laid with - <see cref="CokeCode"/> by default. Must be
  /// called before charging; the rig charges in its constructor, so a charcoal scene passes
  /// <c>charge: -1</c> and charges itself.
  /// </summary>
  public ColdBlastFurnaceRig WithFuel(string fuelCode) {
    _fuelCode = fuelCode;
    return this;
  }

  /// <summary>Lays a full shaft of rounds in <paramref name="fuelCode"/> - the charcoal twin of the
  /// constructor's default charge.</summary>
  public ColdBlastFurnaceRig ChargeWithFuel(string fuelCode, int units = 0) {
    WithFuel(fuelCode);
    Lay(units > 0 ? units : ShaftCapacityUnits);
    return this;
  }

  #endregion

  #region Driving it

  /// <summary>
  /// Turns the blowers on. There is no cowper on the cold furnace's line, so the default blast is 20 C and
  /// the heat balance gets no preheat.
  /// </summary>
  public ColdBlastFurnaceRig PressuriseBlast(
    float temp = 20f,
    float pressure = 5f
  ) {
    _blastTemp = temp;
    _blastPressure = pressure;
    return this;
  }

  /// <summary>
  /// Performs the blow-in on the iron tap - break the plug, torch it, re-plug - so the furnace can catch.
  /// A shaft does not light itself; see <see cref="BlowInRig"/> for why this goes through the gesture.
  /// </summary>
  public ColdBlastFurnaceRig BlowIn() {
    BlowInRig.BlowIn(World, Core, IronTap);
    return this;
  }

  /// <summary>
  /// The flame alone, on the iron tap as it stands - no plug work either side of it. For the case that a
  /// plugged tap cannot be lit through, where the ritual's own unplug would defeat the point.
  /// </summary>
  public ColdBlastFurnaceRig TorchIronTap() {
    BlowInRig.Torch(World, IronTap);
    return this;
  }

  /// <summary>Cuts the blast off (the blower stopped, the main was severed).</summary>
  public ColdBlastFurnaceRig CutBlast() {
    _blastTemp = -1f;
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
      net.BroadcastUpdate(World.Accessor); // push Medium/Pressure/Temperature to the tuyere pipes
    }
  }

  /// <summary>
  /// Runs <paramref name="seconds"/> of simulated time through the furnace's own registered production
  /// tick, with the blowers topping the mains up each second. <paramref name="each"/> runs after every
  /// simulated second, so a scenario can watch a value or intervene between ticks.
  /// </summary>
  public ColdBlastFurnaceRig RunLive(
    int seconds,
    Action<ColdBlastFurnaceRig>? each = null
  ) {
    for (int i = 0; i < seconds; i++) {
      FeedTuyeres();
      World.AdvanceBlockEntityTime(1000);
      each?.Invoke(this);
    }
    return this;
  }

  /// <summary>
  /// Runs until <paramref name="done"/> holds, up to <paramref name="maxSeconds"/>.
  /// </summary>
  /// <returns>Seconds elapsed, or -1 if the condition never held.</returns>
  public int RunUntil(
    System.Func<ColdBlastFurnaceRig, bool> done,
    int maxSeconds,
    Action<ColdBlastFurnaceRig>? each = null
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

  /// <summary>
  /// Adds <paramref name="units"/> of the scene's burden straight onto the columns, laid the same way the
  /// initial charge is. One call where the shipped route is a trickle - <see cref="BlockEntityHopperTall"/>
  /// drips <c>HopperTallDropPerSecond</c> a second - but both push onto the same columns, so only the
  /// cadence differs. The hopper's cadence and band-order rule belong to <c>HopperTallTests</c>.
  /// </summary>
  public ColdBlastFurnaceRig Recharge(int units) {
    Lay(units);
    return this;
  }

  /// <summary>
  /// Right-clicks the iron tap with an empty hand - the production interaction, which breaks the clay plug
  /// out, rather than a direct <c>SetPlugged</c>.
  /// </summary>
  /// <returns>Whether the tap opened. Since U4.8 a missing canal is no longer a reason to refuse, so this
  /// is true wherever the spout points.</returns>
  public bool OpenIronTap() => Open(IronTap);

  /// <summary>Right-clicks the slag tap with an empty hand. See <see cref="OpenIronTap"/>.</summary>
  public bool OpenSlagTap() => Open(SlagTap);

  private bool Open(BlockEntityFurnaceTap tap) {
    ((BlockFurnaceTap)tap.Block).OnBlockInteractStart(
      World.World,
      EmptyHandedPlayer(),
      new BlockSelection { Position = tap.Pos.Copy() }
    );
    return tap.IsPouring;
  }

  /// <summary>A player with nothing in hand and no modifier keys held - the plain toggle gesture.</summary>
  private static IPlayer EmptyHandedPlayer() {
    var player = Substitute.For<IPlayer>();
    var entity = Substitute.For<EntityPlayer>();
    entity.RightHandItemSlot.Returns(new DummySlot());
    player.Entity.Returns(entity);
    return player;
  }

  #endregion

  #region Reading it

  public FurnaceState State => Core.State;

  public float Temp =>
    (float)ReflectionHelpers.GetField(Core, "_internalTemp")!;

  // The pool is the crucible floor's own cells now; summing them leaves every scenario assertion reading
  // as it did against the two fields.
  public float MoltenIron =>
    HearthRig.Pooled(World, Core.PoolCells, BlockEntityHearthMetal.IronCellKey);

  public float MoltenSlag =>
    HearthRig.Pooled(World, Core.PoolCells, BlockEntityHearthMetal.SlagCellKey);

  /// <summary>Burden units the furnace read in its shaft on the last tick - what its own disruption check
  /// compares against.</summary>
  public int ShaftUnits =>
    (int)ReflectionHelpers.GetField(Core, "_cachedMixCount")!;

  /// <summary>The heat balance the last tick computed.</summary>
  public HeatBalance Heat =>
    (HeatBalance)ReflectionHelpers.GetField(Core, "_lastHeatBalance")!;

  /// <summary>Whether the furnace read as air-starved on the last tick (blast under the floor).</summary>
  public bool AirStarved =>
    (bool)ReflectionHelpers.GetField(Core, "_airStarved")!;

  /// <summary>
  /// Fuel-band units still standing in the shaft, and so how long the furnace has left:
  /// <c>coke / (BfRacewayCarbonPerSecond x AirFactor)</c>. Matched on the material as the production read
  /// does, since a burden band's stamped fuel fraction is a grade signal and not carbon.
  /// </summary>
  public int CokeUnits => FuelBandUnits;

  /// <summary>
  /// Charge units standing in fuel bands of any material - the column volume the fuel occupies, which is
  /// not <see cref="CarbonUnits"/>: a charcoal band occupies a full band and carries half a band's carbon.
  /// Asked through the production predicate <c>IsFuelCode</c>, never by comparing to a coke literal.
  /// </summary>
  public int FuelBandUnits {
    get {
      int units = 0;
      foreach (ChargeColumn column in Core.ShaftColumns.Values)
        foreach (ChargeSegment segment in column.Segments)
          if (BlockEntityFurnaceCore.IsFuelCode(segment.Material))
            units += segment.Units;
      return units;
    }
  }

  /// <summary>
  /// Carbon standing in the shaft, in coke units - each fuel band's volume times its own
  /// <c>CarbonPerUnit</c>. A lit shaft burns it at <c>BfRacewayCarbonPerTuyerePerSecond x tuyeres</c> a
  /// second and runs until it reaches zero.
  /// </summary>
  public float CarbonUnits {
    get {
      float carbon = 0f;
      foreach (ChargeColumn column in Core.ShaftColumns.Values)
        foreach (ChargeSegment segment in column.Segments)
          carbon +=
            segment.Units
            * BlockEntityFurnaceCore.CarbonPerUnit(segment.Material);
      return carbon;
    }
  }

  /// <summary>The fuel the scenes lay their rounds with by default. A scene may lay any fuel the registry
  /// grants - see <see cref="ChargeWithFuel"/>.</summary>
  public const string CokeCode = "game:coke";

  /// <summary>Vanilla charcoal: the pre-coke reductant, worth half of coke per unit.</summary>
  public const string CharcoalCode = "game:charcoal";

  /// <summary>How many tuyeres the furnace found when it scanned its own layout - the multiplier on its
  /// carbon rate, read off the machine rather than restated as a literal.</summary>
  public int TuyereCount =>
    (
      (System.Collections.Generic.IReadOnlyList<Vintagestory.API.MathTools.BlockPos>)
        ReflectionHelpers.GetField(Core, "_tuyeres")!
    ).Count;

  public int IronCanalUnits => IronCanal.CellAmount;
  public string IronCanalMetal => IronCanal.CellMetalType;
  public int IronCanalCapacity => IronCanal.MaxUnitCapacity;
  public int SlagCanalUnits => SlagCanal.CellAmount;
  public string SlagCanalMetal => SlagCanal.CellMetalType;

  /// <summary>The block standing at a structure-local cell (for the extinguish-residue assertions).</summary>
  public Block BlockAtLocal(int x, int y, int z) =>
    World.GetBlock(Structure.Cell(x, y, z));

  /// <summary>The block entity at a structure-local cell.</summary>
  public BlockEntity? BlockEntityAtLocal(int x, int y, int z) =>
    World.GetBlockEntity(Structure.Cell(x, y, z));

  /// <summary>The charge pile at a structure-local cell, or null (for the salvage assertions).</summary>
  public BlockEntityChargePile? PileAtLocal(int x, int y, int z) =>
    World.GetBlockEntity(Structure.Cell(x, y, z)) as BlockEntityChargePile;

  /// <summary>
  /// The burden mix standing in the charge block at a structure-local cell - <c>default</c> when the
  /// column does not reach that cell, or when the block holds no burden. Reads the column, not the block:
  /// a charge pile is a window and holds nothing itself. The span read is that block's own slice, the
  /// granularity burn-out decides at (<c>BlockEntityShaftFurnace.BurnOutCharge</c>). Fuel bands are
  /// skipped: a round's coke course carries a <c>default</c> mix.
  /// </summary>
  public BurdenMix SalvageAtLocal(int x, int y, int z) {
    BlockPos pos = Structure.Cell(x, y, z);
    if (Core.ChargeColumnAt(pos, out int blockIndex) is not { } column)
      return default;

    int low = blockIndex * Core.ChargeUnitsPerBlock;
    int high = low + Core.ChargeUnitsPerBlock;
    int at = 0;
    foreach (ChargeSegment segment in column.Segments) {
      int end = at + segment.Units;
      // `IsFuelCode`, not `!= CokeCode`: the literal answers "this is burden" for a charcoal band, so a
      // charcoal furnace's salvage read comes back as an empty mix.
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

  /// <summary>The mix the scene stamped on the charge, for before/after salvage comparisons.</summary>
  public BurdenMix ChargedMix => _burden;

  /// <summary>
  /// The core's own block info, with the world clock pushed past the 1 s HUD throttle so the text is
  /// rebuilt rather than served from cache. The headless lang service echoes keys back, so a line is
  /// present exactly when its key appears.
  /// </summary>
  public string CoreInfo() {
    _hudClockMs += 5000;
    World.World.ElapsedMilliseconds.Returns(_hudClockMs);
    var sb = new StringBuilder();
    Core.GetBlockInfo(null!, sb);
    return sb.ToString();
  }

  #endregion
}
