using System;
using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkMolten;
using IronIndustryExpanded.BlockNetworkMolten.BlockEntities;
using IronIndustryExpanded.BlockNetworkPipe;
using IronIndustryExpanded.BlockNetworkPipe.BlockEntities;
using IronIndustryExpanded.Tests;
using SteelIndustryExpanded.BlockStructures.Converter;
using SteelIndustryExpanded.BlockStructures.Converter.BlockEntities;
using SteelIndustryExpanded.BlockStructures.Converter.Blocks;
using SteelIndustryExpanded.BlockStructures.CowperStove.BlockEntities;
using SteelIndustryExpanded.BlockStructures.CowperStove.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Models the Bessemer steelmaking line (handbook bessemer article) end to end: a converter control fed
/// molten iron from an input canal cell, blown with blast drawn off a live gas network through its intake
/// port, refining the charge to steel and pouring it into an output canal cell. Stands up the three
/// services the converter reads each tick - the input cell, the output cell, and an Air network at or
/// above the blast threshold across the intake. The per-state steps are driven directly, since the gated
/// <c>OnProductionTick</c> also requires an aligned transmission and a constructed vessel.
/// </summary>
internal sealed class ConverterRig {
  // Resolved the way the control resolves them, so a pushed code matches what it reads under a headless
  // registry (game: convention) or a populated one (iiex:/siex:) alike.
  private static string Pig => MetalRegistry.MoltenItemOf("pigiron").ToString();
  private static string Steel =>
    MetalRegistry.MoltenItemOf("bessemersteel").ToString();

  // Structure-local peripheral offsets (mirror the control's private constants).
  private static readonly (int x, int y, int z) InputTapLocal = (1, 1, 2);
  private static readonly (int x, int y, int z) OutputStartLocal = (1, -2, 2);
  private static readonly (int x, int y, int z) GasIntakeLocal = (0, 0, 4);
  private static readonly (int x, int y, int z) TransmissionLocal = (0, -1, 0);

  public readonly TestWorld World;
  public readonly BlockEntityConverterControl Control;
  public readonly BlockEntityMoltenCanal Input;
  public readonly BlockEntityMoltenCanal Output;
  public readonly BlockEntityConverterTransmission Transmission;
  public readonly BlockEntityConverterBessemer Vessel;

  /// <summary>The control's raised footprint; its cells address the service ports.</summary>
  public readonly StructureRig Structure;

  private readonly PipeNetwork _blast;

  public ConverterRig() {
    World = new TestWorld();
    // Both convention and shipped codes, so the resolved token always finds a real item.
    World.RegisterItem("game:ingot-pigiron", 1150f);
    World.RegisterItem("iiex:ingot-pigiron", 1150f);
    World.RegisterItem("game:ingot-bessemersteel", 1500f);
    World.RegisterItem("siex:ingot-bessemersteel", 1500f);
    World.RegisterItem("game:ingot-iron", 1500f);
    World.RegisterItem("game:ingot-slag", 1200f);
    World.RegisterItem("iiex:slag", 1200f);
    World.RegisterItem("game:metalbit-iron");
    World.RegisterItem("game:metalbit-steel");
    World.RegisterNetwork("pipe", s => new PipeNetwork(s));

    var controlPos = new BlockPos(0, 8, 0);
    Control = new BlockEntityConverterControl {
      Pos = controlPos,
      Block = TestBlocks.Configure(
        new BlockConverterControl(),
        "siex:convertercontrol-n",
        1,
        ("side", "north")
      ),
    };
    World.Place(controlPos, Control.Block, Control);
    World.Attach(Control);

    // The control folds a +180 into its structure angle (it faces opposite its side variant), so the
    // vessel and its service ports stand on -Z of a north-facing control.
    Structure = StructureRig.Around(
      World,
      Control,
      BlockConverterControl.Definitions("siex").Single(),
      angle: (ExOrientation.AngleFromSide("north") + 180) % 360
    );

    // Every service port, wearing the code its own layout cell names. A wrong code here is only visible
    // because the rig raises the real footprint and the completion check matches each cell against it.
    Input = PlaceCanal(InputTapLocal, "iiex:molten-canal-tap-s", "tap", "s", 9);
    Output = PlaceCanal(
      OutputStartLocal,
      "iiex:molten-canal-start-fire-s",
      "start",
      "s",
      10
    );

    BlockPos intakePos = Structure.Cell(
      GasIntakeLocal.x,
      GasIntakeLocal.y,
      GasIntakeLocal.z
    );
    var intakeBlock = TestBlocks.Configure(
      new BlockConverterIntake(),
      "siex:converter-intake-n",
      11,
      ("side", "north")
    );
    Structure.Occupy(intakePos, intakeBlock);

    BlockPos transPos = Structure.Cell(
      TransmissionLocal.x,
      TransmissionLocal.y,
      TransmissionLocal.z
    );
    var transBlock = TestBlocks.Configure(
      new BlockConverterTransmission(),
      "siex:convertertransmission-n",
      13,
      ("side", "north")
    );
    Transmission = new BlockEntityConverterTransmission {
      Pos = transPos.Copy(),
      Block = transBlock,
    };
    Structure.Occupy(transPos, transBlock, Transmission);

    // The vessel is a right-click construction, so being present is not the same as being finished. The
    // control gates on both: StructureComplete for the shell, IsConstructed for this.
    BlockPos vesselPos = Structure.Cell(0, 0, 2);
    Vessel = new BlockEntityConverterBessemer {
      Pos = vesselPos.Copy(),
      Block = TestBlocks.Configure(
        new Block(),
        "siex:converterbessemer-n",
        14,
        ("side", "north")
      ),
    };
    Structure.Occupy(vesselPos, Vessel.Block, Vessel);
    RccFake.Complete(Vessel);

    // Raise the filler body and let the control's own monitor tick complete the structure.
    Structure.Complete();

    // Blast supply: an Air pipe network docked against the intake's connector face.
    BlockFacing connFace = ((BlockConverterIntake)intakeBlock).ConnectorFace;
    BlockPos blastPos = intakePos.AddCopy(connFace);
    // Cast (iiex), not plated (iiex): the converter's blast is a steam-tier service and runs past what
    // the iron tier's pipe will pass, so a plated main here starves the blow.
    var blastPipe = PipeTestWorld.MakePipe(
      material: "steel",
      orientation: "ns",
      id: 12
    );
    var blastBe = new BlockEntityPipe {
      Pos = blastPos.Copy(),
      Block = blastPipe,
    };
    World.Place(blastPos, blastPipe, blastBe);
    World.Attach(blastBe);
    World.AddNode(blastPos, "pipe");
    _blast = (PipeNetwork)World.NetworkAt(blastPos)!;
  }

  /// <summary>
  /// A molten-canal cell at a structure-local offset, coded as the layout wants it there. The block entity
  /// is the generic canal one; the layout checks the block code.
  /// </summary>
  private BlockEntityMoltenCanal PlaceCanal(
    (int x, int y, int z) local,
    string code,
    string type,
    string orientation,
    int id
  ) {
    BlockPos pos = Structure.Cell(local.x, local.y, local.z);
    var cell = new BlockEntityMoltenCanal {
      Pos = pos.Copy(),
      Block = TestBlocks.Configure(
        new Block(),
        code,
        id,
        ("type", type),
        ("orientation", orientation)
      ),
    };
    Structure.Occupy(pos, cell.Block, cell);
    return cell;
  }

  /// <summary>
  /// Drives the transmission's mechanical network at <paramref name="speed"/>, standing in for the
  /// engine-generator-axle chain. Attaches the real MP behavior and a faked turning network so the
  /// control's <see cref="BlockEntityConverterControl.HasPower"/> reads it.
  /// </summary>
  public ConverterRig SetMechPower(float speed) {
    var mp = new BEBehaviorMPConverterTransmission(Transmission);
    MechPower.Attach(Transmission, mp, MechPower.Network(speed));
    return this;
  }

  /// <summary>Whether the converter sees mechanical power from its transmission.</summary>
  public bool HasPower => Control.HasPower();

  /// <summary>
  /// The public production tick the game calls. It runs only with all four gates open: the shell complete,
  /// the vessel constructed, and both the gas intake and the transmission facing the same way as the
  /// control. The per-state <see cref="Refine"/> and <see cref="Fill"/> steps below reach past it to
  /// exercise one state in isolation.
  /// </summary>
  public ConverterRig ProductionTick(int times = 1) {
    for (int i = 0; i < times; i++)
      ReflectionHelpers.Invoke(Control, "OnProductionTick", 1f);
    return this;
  }

  /// <summary>The last status line the control set, as shown on the block.</summary>
  public string Status =>
    (string?)ReflectionHelpers.GetField(Control, "_status") ?? "";

  /// <summary>
  /// Throws the vessel lever through the same <see cref="BlockEntityConverterControl.TrySetState"/> the
  /// block calls on a right-click, which refuses unless the shell is complete, the vessel is constructed
  /// and the transmission is turning. Throws if the machine refuses.
  /// </summary>
  public ConverterRig SetState(ConverterOpState state) {
    Assert(
      Control.TrySetState(null!, state, out string error),
      $"the converter refused to switch to {state}: {error}"
    );
    return this;
  }

  private static void Assert(bool condition, string message) {
    if (!condition)
      throw new InvalidOperationException(message);
  }

  /// <summary>Whether the control considers itself built: shell, vessel and both ports.</summary>
  public bool IsCommissioned =>
    Control.StructureComplete
    && Control.IsConverterConstructed()
    && Control.IsGasIntakeAligned()
    && Control.IsTransmissionAligned();

  private static ItemStack MetalStack(
    TestWorld world,
    string code,
    float temp
  ) => MoltenMetal.CreateStack(world.World, code, temp)!;

  /// <summary>Pours molten pig iron into the input canal cell, as the blast-furnace tap would.</summary>
  public ConverterRig PourPigToInput(int units, float temp = 1700f) {
    Input.PushMetal(units, MetalStack(World, Pig, temp), World.World);
    return this;
  }

  /// <summary>
  /// Charges <paramref name="units"/> of pig into the vessel, refilling the input canal cell as many times
  /// as its capacity requires, so a heat larger than one cell can be assembled the way a furnace tap
  /// dripping over time would.
  /// </summary>
  public ConverterRig ChargePig(int units, float temp = 1700f) {
    int remaining = units;
    while (remaining > 0) {
      int chunk = System.Math.Min(remaining, Input.MaxUnitCapacity);
      PourPigToInput(chunk, temp);
      Fill();
      remaining -= chunk;
    }
    return this;
  }

  /// <summary>Charges cold steel scrap into the vessel, bypassing the hand interaction.</summary>
  public ConverterRig ChargeScrap(int units) {
    int have = (int)ReflectionHelpers.GetField(Control, "_scrapUnits")!;
    ReflectionHelpers.SetField(Control, "_scrapUnits", have + units);
    return this;
  }

  /// <summary>Charges the intake's gas network with Air at <paramref name="atm"/>; the converter's blast
  /// threshold is 2.5 atm.</summary>
  public ConverterRig ChargeBlast(float atm = 3f) {
    _blast.TryProduceGas(
      atm * 30f,
      20f,
      "Air",
      World.Accessor,
      maxOutputPressure: atm
    );
    return this;
  }

  public ConverterRig Fill() => Invoke("TickFilling");

  /// <summary>One blow tick (consumes blast, holds the bath temperature, oxidises carbon).</summary>
  public ConverterRig Refine() => Invoke("TickNormal");

  public ConverterRig PourSlag() => Invoke("TickSlagPouring");

  public ConverterRig Pour() => Invoke("TickSteelPouring");

  /// <summary>Pours steel until the charge empties or the output cell stops accepting (bounded).</summary>
  public ConverterRig DrainSteel(int maxTicks = 20) {
    for (int i = 0; i < maxTicks && ContentUnits > 0; i++) {
      int before = ContentUnits;
      Pour();
      if (ContentUnits == before)
        break; // output full - no more progress
    }
    return this;
  }

  /// <summary>Pours slag until the slag pool empties or the output cell stops accepting (bounded).</summary>
  public ConverterRig DrainSlag(int maxTicks = 20) {
    for (int i = 0; i < maxTicks && SlagUnits > 0f; i++) {
      float before = SlagUnits;
      PourSlag();
      if (SlagUnits == before)
        break;
    }
    return this;
  }

  // Blows exactly the blast that takes the bath's carbon down to `to`, so a scenario reaches a chosen
  // phase without simulating the whole multi-minute blow tick by tick.
  private void BlowToCarbon(float to) {
    float carbon = Carbon;
    float blast = System.Math.Max(
      0f,
      (carbon - to) / SiexValues.BessemerCarbonPerBlastLitre
    );
    ReflectionHelpers.Invoke(Control, "BlowStep", blast);
  }

  /// <summary>Fully blows the pig charge down into the steel window (mass sheds into slag as it goes).</summary>
  public ConverterRig BlowToSteel() {
    BlowToCarbon(SiexValues.BessemerSteelCarbonTarget / 2f);
    return this;
  }

  /// <summary>Keeps blowing past the over-blow floor, retyping the steel to soft ingot iron.</summary>
  public ConverterRig OverBlowToIron() {
    BlowToCarbon(SiexValues.BessemerOverblowCarbon / 2f);
    return this;
  }

  private ConverterRig Invoke(string method) {
    ReflectionHelpers.Invoke(Control, method, 1f);
    return this;
  }

  public int ContentUnits =>
    (ReflectionHelpers.GetField(Control, "_charge") as MoltenCharge)?.Units
    ?? 0;
  public float Carbon => (float)ReflectionHelpers.GetField(Control, "_carbon")!;
  public float SlagUnits =>
    (float)ReflectionHelpers.GetField(Control, "_moltenSlag")!;
  public float BlastVolume => _blast.State?.Volume ?? 0f;

  public string ContentCode =>
    (
      ReflectionHelpers.GetField(Control, "_charge") as MoltenCharge
    )?.MetalCode.ToString() ?? "";
}

/// <summary>
/// Models the cowper stove's regenerator cycle (handbook hot-blast article): a single stove charges its
/// brick core from hot furnace exhaust piped to its intake, then discharges by passing cool blast air
/// through it, so the air leaves hot for the furnace tuyeres and the core gives up its heat. Wires the
/// exhaust intake used on charge, and the air passthrough and hot-air outlet read on discharge.
/// </summary>
internal sealed class CowperRig {
  public readonly TestWorld World;
  public readonly BlockEntityCowperStove Stove;

  /// <summary>The stove's raised footprint; its cells address the fittings it reads.</summary>
  public readonly StructureRig Structure;

  private readonly PipeNetwork _exhaust;
  private readonly BlockEntityPipePassthrough _airIn;
  private readonly PipeNetwork _airInNet;
  private readonly PipeNetwork _hotOut;

  public CowperRig() {
    World = new TestWorld();
    World.RegisterNetwork("pipe", sys => new PipeNetwork(sys));

    var pos = new BlockPos(0, 8, 0);
    Stove = new BlockEntityCowperStove {
      Pos = pos,
      // The layout's origin cell wants "siex:cowperstove-intake*", so the anchor must wear that code or
      // the stove stays one cell short of complete.
      Block = TestBlocks.Configure(
        new Block(),
        "siex:cowperstove-intake-tier3-n",
        1,
        ("side", "north")
      ),
    };
    World.Place(pos, Stove.Block, Stove);
    World.Attach(Stove);

    // The stove's structure faces opposite its side variant (the +180 convention it shares with the
    // boiler body), so the shell stands on -Z while the exhaust intake face looks out along +Z.
    Structure = StructureRig.Around(
      World,
      Stove,
      BlockCowperStoveIntake.Definitions("siex").Single(),
      angle: (ExOrientation.AngleFromSide("north") + 180) % 360
    );

    // The two fitting cells the stove reads on discharge, placed before the fill with the real iiex
    // fittings their layout cells name. A generic pipe would carry air as a network node but would not
    // satisfy the footprint.
    BlockPos airInPos = Structure.Cell(0, 1, 2);
    var ptBlock = PipeFittings.Passthrough(id: 20);
    _airIn = new BlockEntityPipePassthrough {
      Pos = airInPos.Copy(),
      Block = ptBlock,
    };
    Structure.Occupy(airInPos, ptBlock, _airIn);

    BlockPos hotOutPos = Structure.Cell(0, 1, 0);
    var outBlock = PipeFittings.Outlet(id: 21);
    var outBe = new BlockEntityPipeOutlet {
      Pos = hotOutPos.Copy(),
      Block = outBlock,
    };
    Structure.Occupy(hotOutPos, outBlock, outBe);

    // Raise the rest of the shell, run the real Initialize - which caches the tunables and derives
    // _connectorFace - and let the stove's own monitor tick find its finished structure.
    Structure.Complete();

    World.AddNode(airInPos, "pipe");
    ReflectionHelpers.SetProperty(
      _airIn,
      nameof(_airIn.NetworkSystem),
      World.Networks
    );
    _airInNet = (PipeNetwork)World.NetworkAt(airInPos)!;

    World.AddNode(hotOutPos, "pipe");
    // Attach (not Initialize) leaves NetworkSystem unset; the outlet needs it to produce into its net.
    ReflectionHelpers.SetProperty(
      outBe,
      nameof(outBe.NetworkSystem),
      World.Networks
    );
    _hotOut = (PipeNetwork)World.NetworkAt(hotOutPos)!;

    // Charge side: a sealed exhaust run butted against the stove's exhaust intake face; the stove's own
    // block caps the near end. The face is read back from what the stove derived at Initialize rather than
    // restated, so a change to the +180 convention moves the exhaust run with it.
    var connector = (BlockFacing)
      ReflectionHelpers.GetField(Stove, "_connectorFace")!;
    _exhaust = SealedRunOn(pos, connector, 2);
  }

  private BlockPos GlobalPos(int x, int y, int z) =>
    (BlockPos)ReflectionHelpers.Invoke(Stove, "GetGlobalPos", x, y, z)!;

  /// <summary>A sealed 2-cell pipe run butted against <paramref name="face"/> of <paramref name="at"/>.</summary>
  private PipeNetwork SealedRunOn(BlockPos at, BlockFacing face, int firstId) {
    // Cast (iiex) for the same reason as the converter's blast main: a cowper's hot-blast and exhaust
    // runs are steam-tier services past the plated tier's throughput.
    var pipe = PipeTestWorld.MakePipe(
      material: "steel",
      orientation: "ns",
      id: firstId
    );
    BlockPos p1 = at.AddCopy(face);
    BlockPos p2 = p1.AddCopy(face);
    World.Place(p1, pipe);
    World.Place(p2, pipe);
    World.Place(
      p2.AddCopy(face),
      TestBlocks.Configure(new Block(), "game:rock", 98)
    ); // far cap
    // No near cap: `at` is the machine itself, which seals that end.
    World.AddNode(p1, "pipe");
    World.AddNode(p2, "pipe");
    return (PipeNetwork)World.NetworkAt(p1)!;
  }

  /// <summary>Charges the exhaust intake with hot furnace exhaust, then runs one production tick.</summary>
  public CowperRig ChargeFromExhaust(float exhaustTemp, float litres = 60f) {
    _exhaust.TryProduceGas(
      litres,
      exhaustTemp,
      "Exhaust",
      World.Accessor,
      maxOutputPressure: 10f
    );
    Tick();
    return this;
  }

  /// <summary>
  /// Feeds cool blast air into the passthrough, then runs one production tick to discharge. The exhaust
  /// line is drained first: a stove only discharges while it is not taking exhaust, which is the two-stove
  /// charge/discharge swap.
  /// </summary>
  public CowperRig DischargeAir(float airTemp = 20f, float litres = 60f) {
    // Valve the exhaust off. One `TryConsumeGas(float.MaxValue)` does not empty a run: the throughput gate
    // moves at most the weakest segment's litres per second per call, so draining takes a loop. A part-full
    // exhaust leaves the stove on charge and it never switches to discharge.
    while (_exhaust.State is { Volume: > 0f })
      if (_exhaust.TryConsumeGas(float.MaxValue, World.Accessor) <= 0f)
        break; // nothing moving - stop rather than spin

    _airInNet.TryProduceGas(
      litres,
      airTemp,
      "Air",
      World.Accessor,
      maxOutputPressure: 3f
    );
    // The stove reads the passthrough's client-synced Volume/Medium/Temperature, so the network state has
    // to be broadcast into those display fields before the tick.
    _airInNet.BroadcastUpdate(World.Accessor);
    Tick();
    return this;
  }

  /// <summary>
  /// Both valves open: pumps fresh blast air into the passthrough and hot exhaust into the intake on the
  /// same tick - the mixing misconfiguration the stove must refuse to charge through.
  /// </summary>
  public CowperRig MixAirAndExhaust(
    float exhaustTemp = 1200f,
    float airLitres = 60f,
    float exhaustLitres = 60f
  ) {
    _airInNet.TryProduceGas(
      airLitres,
      20f,
      "Air",
      World.Accessor,
      maxOutputPressure: 3f
    );
    _airInNet.BroadcastUpdate(World.Accessor);
    _exhaust.TryProduceGas(
      exhaustLitres,
      exhaustTemp,
      "Exhaust",
      World.Accessor,
      maxOutputPressure: 10f
    );
    Tick();
    return this;
  }

  /// <summary>One production tick of the stove with nothing fed in: the idle case.</summary>
  public CowperRig Tick() {
    ReflectionHelpers.Invoke(Stove, "OnProductionTick", 1f);
    return this;
  }

  /// <summary>Litres standing in the exhaust main the stove draws from.</summary>
  public float ExhaustVolume => _exhaust.State?.Volume ?? 0f;

  public float CoreTemperature =>
    (float)ReflectionHelpers.GetField(Stove, "_internalTemperature")!;
  public string HotBlastMedium => _hotOut.State?.MediumType ?? "";
  public float HotBlastTemperature => _hotOut.State?.Temperature ?? 0f;
  public float HotBlastVolume => _hotOut.State?.Volume ?? 0f;
}
