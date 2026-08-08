using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Helpers;
using ExpandedLib.Networks;
using ExpandedLib.Process;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using IronworkingExpanded.BlockNetworkMolten.Blocks;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.BlockStructures.Furnaces.Blocks;
using IronworkingExpanded.BlockStructures.Products.BlockEntities;
using IronworkingExpanded.Items;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Named scenes for the <b>cold</b> blast furnace - the machine the whole iron economy starts at. Each
/// one stands up the real 160-cell footprint of
/// <see cref="BlockBlastFurnaceCoreCold"/> through <see cref="StructureRig"/>, so the furnace
/// completes itself rather than being told it is complete, and hands back a
/// <see cref="ColdBlastFurnaceRig"/> the test drives on the clock.
/// <para>
/// The scenes are the stable surface (later phases reuse them); the rig is the machinery. Anything a
/// scenario needs to <em>vary</em> - what is charged, how the taps are installed - is a scene, not a
/// flag the test has to remember to set.
/// </para>
/// </summary>
internal static class ColdBlastFurnaceScenes
{
  /// <summary>
  /// A burden rich enough in coke to clear the cold break-even. The cold furnace has no cowper, so
  /// every degree above iron's melt line has to come out of the charge: at the shipped tunables the
  /// break-even is ~23.9 % coke, and 30 % settles the hearth at ~1577 C against a 1482 C melt line.
  /// </summary>
  public static BurdenMix HighCoke => Mix(0.30f);

  /// <summary>
  /// The <c>standard</c> grade - 20 % coke, the reference the whole heat balance is calibrated at. On
  /// cold blast it settles at 1420 C and <b>never melts</b>. That is a taught trade, not a defect: the
  /// furnace names it through <c>bf-info-heatstall</c> every tick. See
  /// <see cref="ColdBlastFurnaceScenarioTests"/>' stall case.
  /// </summary>
  public static BurdenMix Standard => Mix(IwexValues.BfReferenceFuelFrac);

  /// <summary>A burden of a given coke fraction carrying enough flux to grade as a real burden - the
  /// same shape <c>HeatBalanceTests.Burden</c> uses, so the two agree on what "30 % coke" means.</summary>
  public static BurdenMix Mix(float fuelFrac) =>
    new(1f - 0.05f - fuelFrac, 0.05f, fuelFrac);

  /// <summary>
  /// The headline scene: a complete, correctly-tapped cold blast furnace charged with a coke-rich
  /// burden and blown with <b>ambient</b> air (a mechanical blower, no cowper - that is what makes it
  /// the cold furnace). Both taps are installed and closed, each over its own canal start; the shaft
  /// carries twice the fire threshold so a campaign can run without starving itself mid-test.
  /// </summary>
  public static ColdBlastFurnaceRig Complete(
    int charge = 2 * 320,
    float fuelFrac = 0.30f
  ) =>
    new ColdBlastFurnaceRig(
      charge: charge,
      burden: Mix(fuelFrac)
    ).PressuriseBlast();

  /// <summary>
  /// A complete, blown furnace whose every column is charged to the brim with <b>burden and no coke at
  /// all</b> - a full shaft, a complete raceway course, and nothing in front of the tuyeres that can burn.
  /// <para>
  /// The discriminating scene for the <b>fuel</b> half of the ignition gate, and the replacement for the
  /// quantity threshold this file used to test. Burden has no carbon in it, so a shaft packed with it is a
  /// furnace with a full stack and no fire - which is the honest reason a charge fails to light now that
  /// "≥ N units" has stopped being one.
  /// </para>
  /// <para>
  /// It is charged to <b>capacity</b> deliberately: any scene that lit on quantity would light on this one.
  /// </para>
  /// </summary>
  public static ColdBlastFurnaceRig NoCokeAtTheRaceway() =>
    new ColdBlastFurnaceRig(charge: -1, burden: HighCoke)
      .PressuriseBlast()
      .ChargeWithoutCoke();

  /// <summary>
  /// <see cref="Complete"/> charged with the <c>standard</c> 20 % burden: it lights, holds at 1420 C
  /// and never crosses the melt line. The taught cold/hot trade.
  /// <para>
  /// <b>Charged to the brim</b> (<c>charge: 0</c> means "fill the shaft"), where the other scenes take a
  /// number. That is what makes the extinguish cases readable: burn-out interpolates the surviving coke
  /// over the shaft's own height, so a column has to reach the <b>top</b> course for the gradient to have
  /// two ends to be measured between. A part-charged shaft stands three blocks tall and local (0,5,0) -
  /// which the salvage cases read - is simply empty air.
  /// </para>
  /// </summary>
  public static ColdBlastFurnaceRig StandardBurden(int charge = 0) =>
    new ColdBlastFurnaceRig(
      charge: charge,
      burden: Standard
    ).PressuriseBlast();

  /// <summary>
  /// A complete, blown furnace whose whole charge is piled into <b>one</b> column - a shaft holding
  /// <paramref name="units"/> with eight of its nine tuyeres blowing into empty air.
  /// <para>
  /// The discriminating scene for the geometric half of the ignition gate. Every other scene in this
  /// file lays a full course, so on all of them "the raceway course is complete" and "the total is over
  /// the threshold" are true together - and a furnace that only ever checked the total would pass every
  /// one of them.
  /// </para>
  /// </summary>
  public static ColdBlastFurnaceRig OneTallColumn(int units = 3 * 320) =>
    new ColdBlastFurnaceRig(charge: -1, burden: HighCoke)
      .PressuriseBlast()
      .PileIntoOneColumn(units);

  /// <summary>
  /// <see cref="Complete"/> with the <b>iron tap installed backwards</b> - facing east out of the east
  /// wall instead of west into the furnace, so its spout aims at the cell one step <em>into</em> the
  /// furnace's own base, where there is no canal.
  /// <para>
  /// This scene is <b>deliberately incomplete</b>. The legend used to wildcard the facing
  /// (<c>iwex:furnace-irontap-*</c>) and the structure completed anyway - the mistake was invisible
  /// until the player tried to tap. The furnace redraw pinned the facing, so the wrong tap no longer
  /// satisfies its cell: this rig stands up with one cell short and <c>StructureComplete</c> false.
  /// That is the scene's subject now, not a defect in it.
  /// </para>
  /// </summary>
  public static ColdBlastFurnaceRig BackwardsIronTap(int charge = 2 * 320) =>
    new ColdBlastFurnaceRig(
      charge: charge,
      burden: HighCoke,
      ironTapSide: "e"
    ).PressuriseBlast();
}

/// <summary>
/// Drives the cold blast furnace headlessly: a charged, lit, blown shaft climbs past iron's melt line,
/// enters Melting, renders ore burden into molten pig iron and slag, taps them into canals, and - when
/// it goes out - freezes its pool onto the hearth and leaves the rest of the burden as salvage.
/// <para>
/// The furnace's <b>real structure is built</b>: every cell of the shipped layout is occupied and the
/// furnace's own monitor tick observes it and completes itself. Nothing here forces
/// <c>StructureComplete</c>, so a wrong tap facing, a mis-coded hopper or a tuyere one cell out shows up
/// as a furnace that never completes rather than as a scene that silently tests nothing.
/// </para>
/// <para>
/// The peripherals are the <b>real blocks</b>, not stand-ins: real <see cref="BlockFurnaceTap"/>
/// (so opening a tap goes through the production right-click, including its refusal to open over
/// nothing), real <see cref="BlockMoltenCanalStart"/> (the type that refusal tests for), real
/// <see cref="BlockTuyere"/>/<see cref="BlockEntityTuyere"/> on real pipe networks, and a real
/// <see cref="BlockHopperTall"/> carrying the <c>-east</c> side variant the layout demands.
/// </para>
/// <para>
/// Time is real too: <see cref="RunLive"/> advances the world clock and lets the furnace's own
/// registered production tick fire. Nothing is fast-forwarded by reflection, because the whole point of
/// this suite is that the process is an <em>outcome</em>. Reflection is used only to <em>read</em>
/// private state the machine has no public accessor for.
/// </para>
/// </summary>
internal sealed class ColdBlastFurnaceRig
{
  /// <summary>Where the anchor stands. High enough that the shaft has room above it.</summary>
  private static readonly BlockPos Anchor = new(0, 16, 0);

  /// <summary>
  /// The temperature the scene's charge is laid at. A single fixed value, not the live ambient, for the
  /// reason the shipped hopper quantises its own: <see cref="ChargeColumn.Push"/> merges only within
  /// 1 °C, so a charge laid at a drifting temperature arrives as a <b>pile of one-unit segments</b> and
  /// the extinguish cases - which read "the mix of the band at this cell" - would read whichever sliver
  /// happened to land there.
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

  /// <summary>The runout the iron tap pours into, at the cell a <em>correctly</em> installed tap aims at.</summary>
  public readonly BlockEntityMoltenCanalStart IronCanal;
  public readonly BlockEntityMoltenCanalStart SlagCanal;

  private readonly PipeNetwork[] _tuyeres;
  private readonly BurdenMix _burden;

  /// <summary>Item path (iwex domain) the columns hold - what a <c>ChargeSegment.Material</c> reads as.</summary>
  private readonly string _chargeCode;

  /// <summary>Which fuel this scene lays its rounds with - see <see cref="WithFuel"/>.</summary>
  private string _fuelCode = CokeCode;
  private float _blastTemp = -1f;
  private float _blastPressure = 5f;
  private int _nextBlockId = 100;
  private long _hudClockMs = 5000;

  /// <summary>
  /// The canonical facings of the two taps: each sits in a wall and faces <b>into</b> the furnace, so
  /// its spout - <c>Pos + side.Opposite + down</c> - lands outside the footprint. The east-wall metal
  /// tap therefore faces west and the west-wall slag tap faces east. Inverting either aims the runout
  /// into the furnace's own masonry. (<c>docs/design/layouts.txt</c> §1 has this pair the wrong way
  /// round; the code and <c>BlastFurnaceTapTests</c> are the source of truth.)
  /// </summary>
  /// Single letters, because that is what a <c>side</c> variant renders since the side
  /// respelling - and this constant is pasted straight into a block code the layout has to match.
  private const string IronTapFacing = "w";
  private const string SlagTapFacing = "e";

  /// <param name="charge">Total burden <b>items</b> laid into the shaft, spread across every column in
  /// proportion to how many cells each one has. <b>0 fills the shaft to capacity.</b> Every column, not
  /// one: the raceway course has to be complete before a furnace will light, which is the geometric half
  /// of the ignition gate and the reason a scene cannot pile its whole charge into a single column.</param>
  /// <param name="burden">Composition stamped on that charge - its coke fraction drives the heat balance.</param>
  /// <param name="chargeCode">Item path (iwex domain) the piles hold. Defaults to the blast furnace's own
  /// ore <c>burden</c>; pass <c>remeltburden</c> to charge the wrong family and exercise the gate.</param>
  /// <param name="ironTapSide">Side variant of the metal tap. Defaults to the correct
  /// <see cref="IronTapFacing"/>; pass its opposite to install the tap backwards.</param>
  /// <param name="slagTapSide">Side variant of the slag tap; see <paramref name="ironTapSide"/>.</param>
  public ColdBlastFurnaceRig(
    int charge = 640,
    BurdenMix? burden = null,
    string chargeCode = "burden",
    string? ironTapSide = null,
    string? slagTapSide = null
  )
  {
    _burden = burden ?? ColdBlastFurnaceScenes.HighCoke;
    _chargeCode = chargeCode;
    World = new TestWorld();
    // The tap's right-click only toggles server-side, and the fake world does not declare a side.
    World.World.Side.Returns(EnumAppSide.Server);

    // The metal tap resolves its carrier through MetalRegistry: pig iron -> iwex:ingot-pigiron when the
    // metal is registered, else the game:ingot-<code> convention. Register both spellings (and both slag
    // ones) so GetItem answers whatever the tick asks for, independent of process-wide registry state.
    World.RegisterItem("iwex:ingot-pigiron", 1500f);
    World.RegisterItem("game:ingot-pigiron", 1500f);
    World.RegisterItem("iwex:slag", 1500f);
    World.RegisterItem("game:ingot-slag", 1500f);
    // Registered so a column's material code resolves back to an item when a player takes a band by hand.
    World.RegisterItem("iwex:" + chargeCode);

    // The block the extinguished pool freezes into, with a factory for its entity class so the
    // SetBlock in SolidifyBottomLayer spawns a real BlockEntityHearthMetal for StampSolidProduct.
    World.RegisterBlockEntityFactory(
      "iwex.BlockEntityHearthMetal",
      () => new BlockEntityHearthMetal()
    );
    Block solid = TestBlocks.Configure(
      new Block(),
      "iwex:hearthmetal-pigiron",
      70,
      ("dummy", "x")
    );
    solid.EntityClass = "iwex.BlockEntityHearthMetal";
    World.Register(solid);

    // The charge pile and its entity class, so the furnace's own SyncChargeBlocks materialises real
    // BlockEntityChargePile windows onto its columns - the same idiom as the hearth-metal block above.
    // Without the factory the SetBlock still lands a block and every placement assertion still passes,
    // while OnColumnChanged has nothing to call and the redraw half of the seam silently tests nothing.
    World.RegisterBlockEntityFactory(
      "iwex.BlockEntityChargePile",
      () => new BlockEntityChargePile()
    );
    // A real `BlockChargePile`, not a plain `Block`. It once stood as a plain one, and that
    // made the whole break path invisible to every scenario on this rig: `pile.Block.OnBlockBroken` reached
    // vanilla's implementation, so a case could dig a pile out, watch the block vanish and assert
    // absolutely nothing about the charge behind it. The same class of trap as a fixture with no block
    // entities - a feature that works in tests and is dead in game, or the reverse.
    Block chargePile = TestBlocks.Configure(
      new BlockChargePile(),
      BlockChargePile.PileCode.ToShortString(),
      71,
      ("type", "chargepile")
    );
    chargePile.EntityClass = "iwex.BlockEntityChargePile";
    // A block that never went through the asset pipeline has no `api`, and vanilla's own
    // Block.OnBlockBroken dereferences it for the break particles.
    ReflectionHelpers.SetField(chargePile, "api", World.Api);
    World.Register(chargePile);

    World.RegisterNetwork("pipe", s => new PipeNetwork(s));

    Core = new BlockEntityBlastFurnaceCold
    {
      Pos = Anchor,
      Block = TestBlocks.Configure(
        new Block(),
        "iwex:furnace-blastcore-tier1-n",
        1,
        ("side", "n")
      ),
    };
    World.Place(Anchor, Core.Block, Core);
    World.Attach(Core);

    // The shipped layout, rotated to the core's own facing (north -> 0). Everything the furnace must
    // actually see goes in first; the rig then raises the rest of the shell and lets the furnace
    // notice its own completed structure.
    Structure = StructureRig.Around(
      World,
      Core,
      BlockBlastFurnaceCoreCold.Definitions("iwex").Single(),
      angle: 0
    );

    // Blast intakes: a real tuyere block at each of the layout's two Y cells, each on its own network.
    _tuyeres =
    [
      // y=2, not y=1: the tuyeres were raised to the wall face so the blast enters
      // the top of the hearth instead of the molten pool, and moved outboard to z=∓2 where their
      // passthroughs used to sit - the passthrough existed only to reach a tuyere buried in the brick.
      Tuyere(Structure.Cell(0, 2, -2), 20, "n"),
      Tuyere(Structure.Cell(0, 2, 2), 21, "s"),
    ];

    // Both taps, each closed, each with its runout canal placed where a correctly installed tap aims.
    // The canal position is derived from the canonical facing rather than from the side actually
    // fitted, so a backwards tap is a tap pointing away from a perfectly good runout - which is the
    // mistake being modelled, not a scene with no canal in it at all.
    // The type is what the layout's two glyphs now demand - `T` takes only `furnace-irontap-*` and
    // `S` only `furnace-slagtap-*`. Swapping these two lines is enough to leave the furnace permanently
    // incomplete, which is exactly the mistake the drawing could not catch before the taps were typed.
    (IronTap, IronCanal) = TapAndCanal(
      Structure.Cell(2, 1, 0),
      BlockFurnaceTap.IronType,
      ironTapSide ?? IronTapFacing,
      IronTapFacing
    );
    // (-2,1,0), not (-2,2,0): the furnace redraw brought both taps down onto the hearth course,
    // so the slag notch sits beside the iron one rather than a level above it.
    (SlagTap, SlagCanal) = TapAndCanal(
      Structure.Cell(-2, 1, 0),
      BlockFurnaceTap.SlagType,
      slagTapSide ?? SlagTapFacing,
      SlagTapFacing
    );

    // The tall hopper, at the layout's 'H' cell and carrying the -east side variant the legend
    // demands. A hopper coded without that variant leaves the structure permanently incomplete, which
    // is how this scene depends on the hopper's side variant group existing at all.
    //
    // -east, not -north, since the redraw. The hopper sits west of the shaft, and its `side` decides
    // which neighbour the drip searches first - the column set is a rotation-symmetric cross, so the
    // facing changes the order, not the cells. East is the shaft.
    //
    // It is placed, not initialized: World.Attach only assigns Api, so the drip listener that
    // BlockEntityHopperTall.Initialize would register never exists and the hopper is inert. What this
    // scene pins about the hopper is its block code (the layout's oriented-part demand), nothing about
    // its behaviour - so the charging half of the charge loop is uncovered here by design. See
    // Recharge above.
    Hopper = new BlockEntityHopperTall();
    BlockPos hopperPos = Structure.Cell(-1, 6, 0);
    World.Place(
      hopperPos,
      TestBlocks.Configure(
        new BlockHopperTall(),
        "iwex:hopper-tall-e",
        80,
        ("side", "e")
      ),
      Hopper
    );
    World.Attach(Hopper);

    // Fill the remaining shell, run the real Initialize, and let the monitor tick find the structure.
    // Throws with a per-cell breakdown if the furnace cannot see what the rig built.
    //
    // Unless a tap was deliberately fitted backwards. Since the furnace redraw pinned the tap
    // facings, a backwards tap leaves its cell unsatisfied and the furnace never completes - which is
    // the whole point of BackwardsIronTap, so that scene has to be allowed to stand up incomplete
    // rather than throwing in its own constructor. Everything else still gets the strict form: a scene
    // that quietly failed to build would test nothing at all.
    bool tapsAsDrawn =
      (ironTapSide ?? IronTapFacing) == IronTapFacing
      && (slagTapSide ?? SlagTapFacing) == SlagTapFacing;
    if (tapsAsDrawn)
      Structure.Complete();
    else
    {
      Structure.Raise();
      World.Initialize(Core);
      Structure.AwaitCompletion();
    }

    // The charge goes in after the structure stands, not before. Columns are the core's and it has none
    // until its layout has arrived, so a push before completion would land nowhere - silently, since a
    // furnace with no box simply owns no columns. It is also the honest order: a furnace is built, then
    // charged.
    // 0 means "fill the shaft"; a negative charge means "leave it empty", for the scenes that lay their
    // own charge afterwards (see PileIntoOneColumn).
    if (charge >= 0)
      Lay(charge > 0 ? charge : ShaftCapacityUnits);
  }

  #region Standing the peripherals up

  /// <summary>
  /// A blast-fed tuyere cell: the real <c>iwex:furnace-tuyere-*</c> block on its own single-node network. It
  /// must be the tuyere block, not a generic pipe - the two behave identically as network nodes, but
  /// only the tuyere satisfies the furnace layout.
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
  /// Places a real tap at <paramref name="tapPos"/> wearing <paramref name="fittedSide"/>, plus the
  /// canal start under the spout of a tap wearing <paramref name="canonicalSide"/>. Both are left
  /// closed - opening goes through <see cref="Open"/>, i.e. through the production right-click.
  /// </summary>
  private (
    BlockEntityFurnaceTap tap,
    BlockEntityMoltenCanalStart canal
  ) TapAndCanal(
    BlockPos tapPos,
    string type,
    string fittedSide,
    string canonicalSide
  )
  {
    var tap = new BlockEntityFurnaceTap();
    World.Place(
      tapPos,
      TestBlocks.Configure(
        new BlockFurnaceTap(),
        $"iwex:furnace-{type}-{fittedSide}",
        _nextBlockId++,
        ("type", type),
        ("side", fittedSide)
      ),
      tap
    );
    World.Attach(tap);

    // ExOrientation.FacingFromSide, never BlockFacing.FromCode: vanilla's returns null for a letter,
    // and the constants above are letters now. The failure would be an NRE in a fixture, which is at
    // least loud - in production the same call silently collapsed a tap's facing to nothing.
    BlockPos canalPos = tapPos
      .AddCopy(ExOrientation.FacingFromSide(canonicalSide)!.Opposite)
      .DownCopy();
    var canal = new BlockEntityMoltenCanalStart();
    World.Place(
      canalPos,
      TestBlocks.Configure(
        new BlockMoltenCanalStart(),
        "iwex:moltencanalstart-ns",
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
  /// Piles <paramref name="units"/> into <b>one</b> column and leaves the other eight empty - a shaft
  /// that is full by any total-units measure and has an incomplete raceway course.
  /// <para>
  /// The only thing in the suite that can tell the geometric ignition gate from a threshold. Every
  /// other scene charges the whole course, so on all of them "every column holds charge" and "the total
  /// is over the threshold" are true together and a furnace that checked only the total would look
  /// identical.
  /// </para>
  /// </summary>
  public ColdBlastFurnaceRig PileIntoOneColumn(int units)
  {
    var keys = new List<(int X, int Z)>(Core.ShaftColumns.Keys);
    keys.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
    var (x, z) = keys[0];
    Core.ChargeColumnAt(x, z)!
      .Push($"iwex:{_chargeCode}", units, ChargeTemp, _burden);
    Core.SyncChargeBlocks();
    return this;
  }

  /// <summary>
  /// The shaft's column keys in the deterministic <c>(x, z)</c> order the furnace itself walks them in, so
  /// a scene can name "the first column" and "every other column" and mean the same two things the
  /// production code does.
  /// </summary>
  public List<(int X, int Z)> ColumnKeys
  {
    get
    {
      var keys = new List<(int X, int Z)>(Core.ShaftColumns.Keys);
      keys.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
      return keys;
    }
  }

  /// <summary>
  /// Lays one round - <paramref name="fuelUnits"/> of fuel, then <paramref name="burdenUnits"/> of burden -
  /// into the single column <c>(<paramref name="x"/>, <paramref name="z"/>)</c>, repeated
  /// <paramref name="rounds"/> times.
  /// <para>
  /// <b>The only way to charge two columns differently</b>, and the chill cannot be scened without it:
  /// a hang is per column by definition, so a scene that lays the same course everywhere can only ever
  /// produce a furnace where every column hangs or none does - which is exactly the case that cannot tell
  /// a per-column rule from a whole-furnace one.
  /// </para>
  /// <para>
  /// Fuel first, then burden, because that is a round - and because <c>NextChargeColumn</c>'s band-order
  /// rule is the shipped charging path's own. Pushing burden first would build a column no hopper can lay.
  /// </para>
  /// </summary>
  /// <param name="burdenTemp">
  /// The temperature the burden bands are laid at, <c>-1</c> for the scene's own charging temperature.
  /// <para>
  /// <b>The chill cases set it, and they have to.</b> A hang is "the burden at this raceway is below the
  /// melt line", and reaching that state through a campaign means waiting for the counter-current model to
  /// produce it - which it does, and then immediately kills the furnace, because a column that does not
  /// descend never brings fresh carbon down to its own raceway. Laying the temperature states the premise
  /// directly instead of hoping a 20-minute run lands on it, and leaves the campaign-length case free to
  /// asserting the *consequence*.
  /// </para>
  /// </param>
  public ColdBlastFurnaceRig LayCourseInto(
    int x,
    int z,
    int fuelUnits,
    int burdenUnits,
    int rounds = 1,
    float burdenTemp = -1f
  )
  {
    ChargeColumn column =
      Core.ChargeColumnAt(x, z)
      ?? throw new InvalidOperationException($"no column at ({x},{z})");

    float temp = burdenTemp < 0f ? ChargeTemp : burdenTemp;
    for (int i = 0; i < rounds; i++)
    {
      if (fuelUnits > 0)
        column.Push(_fuelCode, fuelUnits, ChargeTemp, default);
      if (burdenUnits > 0)
        column.Push($"iwex:{_chargeCode}", burdenUnits, temp, _burden);
    }

    Core.SyncChargeBlocks();
    return this;
  }

  /// <summary>Units standing in one named column - the per-column half of <see cref="ColumnUnits"/>, which
  /// is what a hang has to be asserted against: a hung column holds its charge while its neighbour's
  /// falls, and the shaft total alone cannot tell those apart.</summary>
  public int ColumnUnitsAt(int x, int z) =>
    Core.ChargeColumnAt(x, z)?.TotalUnits ?? 0;

  /// <summary>Whether the named column has chilled - production's own read.</summary>
  public bool IsHung(int x, int z) => Core.IsHung(x, z);

  /// <summary>How many columns have chilled - production's own read.</summary>
  public int HungColumns => Core.HungColumnCount;

  /// <summary>
  /// The mix the furnace <b>actually read at its raceway</b> on its last tick - not a hand-built
  /// <see cref="BurdenMix"/>.
  /// <para>
  /// Handing <c>RequiredBlastPressureFor</c> a mix built in the test proves nothing about a hung column;
  /// the whole claim is that the hang moves what the furnace reads. This is the only honest source.
  /// </para>
  /// </summary>
  public BurdenMix RacewayMix =>
    (BurdenMix)ReflectionHelpers.GetField(Core, "_chargeMix")!;

  /// <summary>
  /// Knocks a wall block out of the furnace - a <b>breach</b>, the physical opposite of a choke.
  /// <para>
  /// Takes the first cell the rig filled with a plain stand-in, so it can never hit a tuyere, a tap, the
  /// hopper or an open shaft cell: what comes out is brickwork, which is what a breach is.
  /// </para>
  /// </summary>
  public ColdBlastFurnaceRig Breach()
  {
    foreach (var (pos, wanted) in Structure.Cells)
    {
      if (wanted.Contains('@') || wanted.Contains('*'))
        continue; // an alternation - an open shaft cell or a fuel slot, not a wall
      if (World.GetBlock(pos).Id == 0)
        continue;
      World.Place(pos, new Block { Code = new AssetLocation("game:air"), BlockId = 0 });
      return this;
    }
    throw new InvalidOperationException("no wall cell to breach");
  }

  /// <summary>Total units standing in the shaft's columns, read straight off them - unlike
  /// <see cref="ShaftUnits"/>, which is what the furnace cached on its last tick.</summary>
  public int ColumnUnits => Core.ShaftChargeUnits;

  /// <summary>Everything the shaft can hold - every column's own cell count times the furnace's block
  /// quantum. What <c>charge: 0</c> means, and the ceiling <see cref="Lay"/> spreads under.</summary>
  public int ShaftCapacityUnits
  {
    get
    {
      int total = 0;
      foreach (var (x, z) in Core.ShaftColumns.Keys)
        total += Core.ColumnCapacity(x, z);
      return total;
    }
  }

  /// <summary>
  /// Lays <paramref name="total"/> items of the scene's burden into the shaft, spread over <b>every</b>
  /// column in proportion to how many cells each one has.
  /// <para>
  /// <b>Proportional, not equal.</b> The shipped hearth has a three-cell crucible well in its middle
  /// row, so three of the nine columns are a cell taller than the rest; an equal split fills the short six
  /// past their own roofs while the tall three stand short, and charge above a column's roof is charge no
  /// block can draw and no player can dig out. Weighting by capacity makes "charge the shaft full" land
  /// exactly full.
  /// </para>
  /// <para>
  /// Deliberately <b>not</b> the shipped selection rule
  /// (<see cref="BlockEntityFurnaceCore.NextChargeColumn"/>). A fixture that charged through production
  /// code could not tell a broken selection from a correct one - it would simply be wrong in the same way
  /// on both sides. The remainder goes to the first keys in ascending <c>(x, z)</c>, so it is
  /// deterministic and two identical scenes charge identically.
  /// </para>
  /// </summary>
  /// <param name="rounds">Lay <b>real rounds</b> - a coke course, then a burden course. Only
  /// <see cref="ChargeWithoutCoke"/> passes false, and what it produces is a shaft that cannot burn.</param>
  private void Lay(int total, bool rounds = true)
  {
    if (total <= 0)
      return;

    var keys = new List<(int X, int Z)>(Core.ShaftColumns.Keys);
    keys.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
    if (keys.Count == 0)
      return;

    int capacity = ShaftCapacityUnits;
    var want = new int[keys.Count];
    int assigned = 0;
    for (int i = 0; i < keys.Count; i++)
    {
      want[i] = (int)(
        (long)total * Core.ColumnCapacity(keys[i].X, keys[i].Z) / Math.Max(1, capacity)
      );
      assigned += want[i];
    }
    for (int i = 0; assigned < total && i < keys.Count; i++)
    {
      want[i]++;
      assigned++;
    }

    // Clamp each column to its own room and carry the overflow forward, so a scene asking for more than
    // the shaft holds fills it rather than pushing charge through the roof.
    int carry = 0;
    for (int i = 0; i < keys.Count; i++)
    {
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
        column.Push($"iwex:{_chargeCode}", give, ChargeTemp, _burden);
    }

    Core.SyncChargeBlocks();
  }

  /// <summary>
  /// Fills every column to the brim with <b>burden alone</b> - no coke bands anywhere.
  /// <para>
  /// A full shaft that cannot burn. The burden still carries its stamped coke <em>fraction</em>, which is
  /// exactly what makes this scene worth having: the stamp is a grade signal and not a fuel supply, so a
  /// furnace that read carbon off it would light here and a furnace that reads the bands cannot. See
  /// <c>BlockEntityShaftFurnace.Accumulate</c>.
  /// </para>
  /// </summary>
  public ColdBlastFurnaceRig ChargeWithoutCoke(int units = 0)
  {
    Lay(units > 0 ? units : ShaftCapacityUnits, rounds: false);
    return this;
  }

  /// <summary>
  /// Lays <paramref name="units"/> into one column as <b>real rounds</b> - a coke course, then a burden
  /// course, repeating - at the scene's own coke fraction.
  /// <para>
  /// <b>Charging burden-only is a leftover of the old model and it deadlocks the counter-current
  /// furnace.</b> Coke stamped into a burden band cannot burn away, so it makes no void, so nothing
  /// descends - the bottom band burns off its own carbon in a few minutes, stalls short of the melting
  /// point, and the shaft locks solid with the coke of every band above it out of reach. The fix is
  /// not a constant: <b>carbon has to arrive as its own bands</b>, which is what burns away and what lets
  /// the column fall into the gap. The hopper has long laid coke as its own bands; only the fixtures
  /// were still charging the old way.
  /// </para>
  /// <para>
  /// The burden still carries its stamped fuel fraction, because that is what the grade readout and the
  /// legacy path read. It is a <em>grade</em> signal here, not the fuel supply.
  /// </para>
  /// </summary>
  private void LayRounds(ChargeColumn column, int units)
  {
    // One round is one charge block, split at the scene's coke fraction - what a player lays.
    int perRound = Math.Max(2, Core.ChargeUnitsPerBlock);
    int fuelPerRound = Math.Max(1, (int)(perRound * _burden.FuelFrac));

    int left = units;
    while (left > 0)
    {
      int fuel = Math.Min(fuelPerRound, left);
      column.Push(_fuelCode, fuel, ChargeTemp, default);
      left -= fuel;
      if (left <= 0)
        break;

      int burden = Math.Min(perRound - fuelPerRound, left);
      column.Push($"iwex:{_chargeCode}", burden, ChargeTemp, _burden);
      left -= burden;
    }
  }

  /// <summary>
  /// Switches which fuel the scene's rounds are laid with - <see cref="CokeCode"/> by default,
  /// <see cref="CharcoalCode"/> for the charcoal twin of any case.
  /// <para>
  /// Parameterising the fuel rather than adding a second rig is what makes every existing scenario
  /// gain a charcoal counterpart for free - and a counterpart is the only thing that can tell a furnace
  /// that <em>prices</em> its fuel from one that merely accepts it. Call it before charging: the rig
  /// lays its charge in the constructor, so a scene wanting charcoal passes <c>charge: -1</c> and charges
  /// itself.
  /// </para>
  /// </summary>
  public ColdBlastFurnaceRig WithFuel(string fuelCode)
  {
    _fuelCode = fuelCode;
    return this;
  }

  /// <summary>Lays a full shaft of rounds in <paramref name="fuelCode"/> - the charcoal twin of the
  /// constructor's default charge.</summary>
  public ColdBlastFurnaceRig ChargeWithFuel(string fuelCode, int units = 0)
  {
    WithFuel(fuelCode);
    Lay(units > 0 ? units : ShaftCapacityUnits);
    return this;
  }

  #endregion

  #region Driving it

  /// <summary>
  /// Turns the blowers on. The cold furnace's air arrives at <b>ambient</b> - there is no cowper on its
  /// line, which is the entire difference between it and the hot furnace - so the default temperature
  /// is 20 C and the heat balance gets no preheat.
  /// </summary>
  public ColdBlastFurnaceRig PressuriseBlast(
    float temp = 20f,
    float pressure = 5f
  )
  {
    _blastTemp = temp;
    _blastPressure = pressure;
    return this;
  }

  /// <summary>Cuts the blast off (the blower stopped, the main was severed).</summary>
  public ColdBlastFurnaceRig CutBlast()
  {
    _blastTemp = -1f;
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
  /// Runs <paramref name="seconds"/> of simulated time through the furnace's <b>own</b> registered
  /// production tick - the listener its <c>Initialize</c> put on the clock - with the blowers topping
  /// the mains up each second. <paramref name="each"/> runs after every simulated second, which is
  /// what lets a scenario watch a value it must never cross (or intervene, e.g. recharge the shaft).
  /// </summary>
  public ColdBlastFurnaceRig RunLive(
    int seconds,
    Action<ColdBlastFurnaceRig>? each = null
  )
  {
    for (int i = 0; i < seconds; i++)
    {
      FeedTuyeres();
      World.AdvanceBlockEntityTime(1000);
      each?.Invoke(this);
    }
    return this;
  }

  /// <summary>
  /// Runs until <paramref name="done"/> holds, up to <paramref name="maxSeconds"/>, and returns the
  /// seconds actually elapsed (or -1 if it never held). Lets a scenario say "melt until the shaft is
  /// half gone" without restating a duration derived from four config keys.
  /// </summary>
  public int RunUntil(
    System.Func<ColdBlastFurnaceRig, bool> done,
    int maxSeconds,
    Action<ColdBlastFurnaceRig>? each = null
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

  /// <summary>
  /// Adds <paramref name="units"/> of the scene's burden straight onto the columns, laid the same way the
  /// initial charge is - models "the player charged again" so a scenario can pin what the <b>furnace</b>
  /// does with new burden.
  /// <para>
  /// It is one call where the shipped route is a trickle: <see cref="BlockEntityHopperTall"/> drips
  /// <c>HopperTallDropPerSecond</c> a second out of a one-stack tank. That difference is cadence only now
  /// - both push onto the same columns, and there is no per-cell ceiling left for the two to disagree
  /// about. The hopper's own cadence and its band-order rule are <c>HopperTallTests</c>'.
  /// </para>
  /// </summary>
  public ColdBlastFurnaceRig Recharge(int units)
  {
    Lay(units);
    return this;
  }

  /// <summary>
  /// Right-clicks the iron tap with an empty hand - the production interaction, not a direct
  /// <c>TogglePouring</c>. Returns whether the tap actually opened, so a scenario can pin the block's
  /// own refusal to open a tap whose spout has no canal start under it.
  /// </summary>
  public bool OpenIronTap() => Open(IronTap);

  /// <summary>Right-clicks the slag tap with an empty hand. See <see cref="OpenIronTap"/>.</summary>
  public bool OpenSlagTap() => Open(SlagTap);

  private bool Open(BlockEntityFurnaceTap tap)
  {
    ((BlockFurnaceTap)tap.Block).OnBlockInteractStart(
      World.World,
      EmptyHandedPlayer(),
      new BlockSelection { Position = tap.Pos.Copy() }
    );
    return tap.IsPouring;
  }

  /// <summary>A player with nothing in hand and no modifier keys held - the plain toggle gesture.</summary>
  private static IPlayer EmptyHandedPlayer()
  {
    var player = Substitute.For<IPlayer>();
    var entity = Substitute.For<EntityPlayer>();
    entity.RightHandItemSlot.Returns(new DummySlot());
    player.Entity.Returns(entity);
    return player;
  }

  #endregion

  #region Reading it

  public FurnaceState State => Core.State;

  public float Temp => (float)ReflectionHelpers.GetField(Core, "_internalTemp")!;

  public float MoltenIron => (float)ReflectionHelpers.GetField(Core, "_moltenIron")!;

  public float MoltenSlag => (float)ReflectionHelpers.GetField(Core, "_moltenSlag")!;

  /// <summary>Burden units the furnace read in its shaft on the last tick - the number its own
  /// disruption check compares against <see cref="DisruptionFloor"/>.</summary>
  public int ShaftUnits => (int)ReflectionHelpers.GetField(Core, "_cachedMixCount")!;

  /// <summary>The heat balance the last tick computed - what the furnace is chasing, and why.</summary>
  public HeatBalance Heat =>
    (HeatBalance)ReflectionHelpers.GetField(Core, "_lastHeatBalance")!;

  /// <summary>Whether the furnace read as air-starved on the last tick (blast under the floor).</summary>
  public bool AirStarved => (bool)ReflectionHelpers.GetField(Core, "_airStarved")!;

  /// <summary>
  /// Carbon still standing in the shaft - the units of <b>fuel bands</b> across every column.
  /// <para>
  /// <b>This is what a campaign is made of</b>, and it is the number the two retired accessors here used
  /// to stand in for. <c>DisruptionFloor</c> read <c>DisruptionMixFloor</c> and <c>ExtinguishWindow</c> read
  /// <c>ExtinguishThresholdDefault</c>; both are firebox machinery with no meaning at all on a shaft, which
  /// derives its state from the charge rather than counting down to anything. A scenario that wants to know
  /// how long a furnace has left asks how much carbon it has, because that <em>is</em> the answer:
  /// <c>coke ÷ (BfRacewayCarbonPerSecond × AirFactor)</c>.
  /// </para>
  /// <para>
  /// Matched on the <b>material</b>, exactly as the production read does - a burden band's stamped fuel
  /// fraction is a grade signal and not carbon, so counting it here would re-introduce the double-count the
  /// model was fixed to remove.
  /// </para>
  /// </summary>
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
  /// which is what this read used to do while its own comment claimed it matched production. A string
  /// compare answers "no" for charcoal, so every campaign-length assertion in the suite would have read
  /// zero carbon on a charcoal furnace and passed for the wrong reason.
  /// </para>
  /// </summary>
  public int FuelBandUnits
  {
    get
    {
      int units = 0;
      foreach (ChargeColumn column in Core.ShaftColumns.Values)
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
      foreach (ChargeColumn column in Core.ShaftColumns.Values)
        foreach (ChargeSegment segment in column.Segments)
          carbon +=
            segment.Units * BlockEntityFurnaceCore.CarbonPerUnit(segment.Material);
      return carbon;
    }
  }

  /// <summary>The fuel the scenes lay their rounds with by default. A scene may lay any fuel the
  /// registry grants - see <see cref="ChargeWithFuel"/> - which is what makes the charcoal cases possible
  /// without a second rig.</summary>
  public const string CokeCode = "game:coke";

  /// <summary>Vanilla charcoal: the pre-coke reductant, worth half of coke per unit.</summary>
  public const string CharcoalCode = "game:charcoal";

  /// <summary>How many tuyeres the furnace actually found when it scanned its own layout - the multiplier
  /// on its carbon rate, read off the machine rather than restated as a literal here.</summary>
  public int TuyereCount =>
    (
      (System.Collections.Generic.IReadOnlyList<
        Vintagestory.API.MathTools.BlockPos
      >)
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
  /// column does not reach that cell, or when that block holds no burden at all.
  /// <para>
  /// It reads the <b>column</b>, not the block: a charge pile is a window and holds nothing itself. The
  /// span read is that block's own slice, which is the granularity burn-out decided at - see
  /// <c>BlockEntityShaftFurnace.BurnOutCharge</c>, which is per block for exactly this reason.
  /// </para>
  /// <para>
  /// <b>It skips fuel bands, and it has to.</b> Since the scenes charge real rounds, the band at the
  /// <em>base</em> of a block is usually the round's coke course - a segment carrying <c>default</c> mix,
  /// because coke is carbon and nothing else. Reading it would answer "0 % iron, 0 % flux, 0 % coke" for a
  /// block full of perfectly good salvage, and every salvage assertion in the suite would be comparing
  /// against an empty struct rather than against the burden. What these cases are about is what became of
  /// the <b>burden's stamp</b>, so the burden is what is read.
  /// </para>
  /// </summary>
  public BurdenMix SalvageAtLocal(int x, int y, int z)
  {
    BlockPos pos = Structure.Cell(x, y, z);
    if (Core.ChargeColumnAt(pos, out int blockIndex) is not { } column)
      return default;

    int low = blockIndex * Core.ChargeUnitsPerBlock;
    int high = low + Core.ChargeUnitsPerBlock;
    int at = 0;
    foreach (ChargeSegment segment in column.Segments)
    {
      int end = at + segment.Units;
      // `IsFuelCode`, not `!= CokeCode`. The literal answers "this is burden" for a charcoal band, so a
      // charcoal furnace's salvage read would come back as an empty mix and every assertion below it would
      // compare against `default` - silently, and in the direction that looks like lost salvage.
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
  /// rebuilt rather than served from its cache. The headless lang service echoes keys back, so a line
  /// is present exactly when its key appears.
  /// </summary>
  public string CoreInfo()
  {
    _hudClockMs += 5000;
    World.World.ElapsedMilliseconds.Returns(_hudClockMs);
    var sb = new StringBuilder();
    Core.GetBlockInfo(null!, sb);
    return sb.ToString();
  }

  #endregion
}
