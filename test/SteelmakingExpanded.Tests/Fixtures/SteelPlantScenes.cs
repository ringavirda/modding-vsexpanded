using System;
using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Metals;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkMolten;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using LowPressureExpanded.BlockNetworkPipe;
using LowPressureExpanded.BlockNetworkPipe.BlockEntities;
using LowPressureExpanded.Tests;
using SteelmakingExpanded.BlockStructures.Converter;
using SteelmakingExpanded.BlockStructures.Converter.BlockEntities;
using SteelmakingExpanded.BlockStructures.Converter.Blocks;
using SteelmakingExpanded.BlockStructures.CowperStove.BlockEntities;
using SteelmakingExpanded.BlockStructures.CowperStove.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// Models the Bessemer steelmaking line (handbook bessemer article) end to end: a converter control
/// fed molten iron from an input canal cell, blown with real <strong>blast</strong> drawn off a live
/// gas network through its intake port, refining the charge to steel and pouring it into an output
/// canal cell. It stands up the three services the converter actually reads each tick - the input
/// cell, the output cell, and an Air≥blast-threshold pipe network across the intake - so the refining
/// process runs against a real blast supply (the per-state steps are driven directly, as the gated
/// <c>OnProductionTick</c> also requires an aligned transmission + constructed vessel).
/// </summary>
internal sealed class ConverterRig
{
  // Resolved the way the control resolves them so what we push matches what it reads, headless registry
  // (game: convention) or populated (iwex:/smex:) alike.
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

  /// <summary>The control's raised footprint - its cells address the service ports.</summary>
  public readonly StructureRig Structure;

  private readonly PipeNetwork _blast;

  public ConverterRig()
  {
    World = new TestWorld();
    // Both convention and shipped codes, so the resolved token always finds a real item.
    World.RegisterItem("game:ingot-pigiron", 1150f);
    World.RegisterItem("iwex:ingot-pigiron", 1150f);
    World.RegisterItem("game:ingot-bessemersteel", 1500f);
    World.RegisterItem("smex:ingot-bessemersteel", 1500f);
    World.RegisterItem("game:ingot-iron", 1500f);
    World.RegisterItem("game:ingot-slag", 1200f);
    World.RegisterItem("iwex:slag", 1200f);
    World.RegisterItem("game:metalbit-iron");
    World.RegisterItem("game:metalbit-steel");
    World.RegisterNetwork("pipe", s => new PipeNetwork(s));

    var controlPos = new BlockPos(0, 8, 0);
    Control = new BlockEntityConverterControl
    {
      Pos = controlPos,
      Block = TestBlocks.Configure(
        new BlockConverterControl(),
        "smex:convertercontrol-north",
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
      BlockConverterControl.Definitions("smex").Single(),
      angle: (ExOrientation.AngleFromSide("north") + 180) % 360
    );

    // Every service port, wearing the code its own layout cell names. All five of these were wrong
    // before the structure was ever raised - `converterbessemercontrol`, `converterintake`,
    // `converterbessemertransmission` and two `smex:moltencanal-straight` cells standing in for a tap
    // and a canal start - and nothing could see it, because a rig that never builds the footprint
    // never checks a single code against it.
    Input = PlaceCanal(InputTapLocal, "iwex:moltencanal-tap-s", "tap", "s", 9);
    Output = PlaceCanal(
      OutputStartLocal,
      "iwex:moltencanal-start-s",
      "start",
      "s",
      10
    );

    BlockPos intakePos = Structure.Cell(GasIntakeLocal.x, GasIntakeLocal.y, GasIntakeLocal.z);
    var intakeBlock = TestBlocks.Configure(
      new BlockConverterIntake(),
      "smex:converter-intake-north",
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
      "smex:convertertransmission-north",
      13,
      ("side", "north")
    );
    Transmission = new BlockEntityConverterTransmission
    {
      Pos = transPos.Copy(),
      Block = transBlock,
    };
    Structure.Occupy(transPos, transBlock, Transmission);

    // The vessel itself - a right-click construction, so being present is not the same as being
    // finished. The control gates on both: StructureComplete for the shell, IsConstructed for this.
    BlockPos vesselPos = Structure.Cell(0, 0, 2);
    Vessel = new BlockEntityConverterBessemer
    {
      Pos = vesselPos.Copy(),
      Block = TestBlocks.Configure(
        new Block(),
        "smex:converterbessemer-north",
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
    var blastPipe = PipeTestWorld.MakePipe(orientation: "ns", id: 12);
    var blastBe = new BlockEntityPipe { Pos = blastPos.Copy(), Block = blastPipe };
    World.Place(blastPos, blastPipe, blastBe);
    World.Attach(blastBe);
    World.AddNode(blastPos, "pipe");
    _blast = (PipeNetwork)World.NetworkAt(blastPos)!;
  }

  /// <summary>
  /// A molten-canal cell at a structure-local offset, coded as the layout wants it there. The canal BE
  /// is the generic one - what the layout checks, and what used to be wrong, is the block code.
  /// </summary>
  private BlockEntityMoltenCanal PlaceCanal(
    (int x, int y, int z) local,
    string code,
    string type,
    string orientation,
    int id
  )
  {
    BlockPos pos = Structure.Cell(local.x, local.y, local.z);
    var cell = new BlockEntityMoltenCanal
    {
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
  /// Drives the transmission's mechanical network at <paramref name="speed"/> (the engine→generator→
  /// axle chain spinning it). Attaches the real MP behavior + a faked turning network so the control's
  /// <see cref="BlockEntityConverterControl.HasPower"/> reads it.
  /// </summary>
  public ConverterRig SetMechPower(float speed)
  {
    var mp = new BEBehaviorMPConverterTransmission(Transmission);
    MechPower.Attach(Transmission, mp, MechPower.Network(speed));
    return this;
  }

  /// <summary>Whether the converter sees mechanical power from its transmission.</summary>
  public bool HasPower => Control.HasPower();

  /// <summary>
  /// The <b>public</b> production tick - the one the game calls, which runs only when all four gates
  /// are open: the shell complete, the vessel constructed, and both the gas intake and the transmission
  /// facing the same way as the control. The per-state <see cref="Refine"/>/<see cref="Fill"/> steps
  /// below reach past it deliberately (they exercise one state in isolation); this is the emergent
  /// path, and it can only be driven now that the rig raises the real structure.
  /// </summary>
  public ConverterRig ProductionTick(int times = 1)
  {
    for (int i = 0; i < times; i++)
      ReflectionHelpers.Invoke(Control, "OnProductionTick", 1f);
    return this;
  }

  /// <summary>The last status line the control set - what a player reads off the block.</summary>
  public string Status =>
    (string?)ReflectionHelpers.GetField(Control, "_status") ?? "";

  /// <summary>
  /// Throws the vessel lever, through the same <see cref="BlockEntityConverterControl.TrySetState"/>
  /// the block calls on a right-click - which refuses unless the shell is complete, the vessel is
  /// constructed and the transmission is turning. Setting the state is itself a gated operation, so
  /// this only works on a machine that is genuinely built and powered.
  /// </summary>
  public ConverterRig SetState(ConverterOpState state)
  {
    Assert(
      Control.TrySetState(null!, state, out string error),
      $"the converter refused to switch to {state}: {error}"
    );
    return this;
  }

  private static void Assert(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }

  /// <summary>Whether the control considers itself buildable-and-built: shell + vessel + both ports.</summary>
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

  /// <summary>Pours molten pig iron into the input canal cell (the blast-furnace tap feeding the converter).</summary>
  public ConverterRig PourPigToInput(int units, float temp = 1700f)
  {
    Input.PushMetal(units, MetalStack(World, Pig, temp), World.World);
    return this;
  }

  /// <summary>
  /// Charges <paramref name="units"/> of pig into the vessel, refilling the 50 u input canal cell as many
  /// times as it takes (one cell only holds a cell's worth), so a heat larger than one canal cell can be
  /// assembled the way a furnace tap dripping over time would.
  /// </summary>
  public ConverterRig ChargePig(int units, float temp = 1700f)
  {
    int remaining = units;
    while (remaining > 0)
    {
      int chunk = System.Math.Min(remaining, Input.MaxUnitCapacity);
      PourPigToInput(chunk, temp);
      Fill();
      remaining -= chunk;
    }
    return this;
  }

  /// <summary>Charges cold steel scrap into the vessel (the temperature gate), bypassing the hand interaction.</summary>
  public ConverterRig ChargeScrap(int units)
  {
    int have = (int)ReflectionHelpers.GetField(Control, "_scrapUnits")!;
    ReflectionHelpers.SetField(Control, "_scrapUnits", have + units);
    return this;
  }

  /// <summary>Charges the intake's gas network with blast (Air at <paramref name="atm"/> ≥ 2.5 atm).</summary>
  public ConverterRig ChargeBlast(float atm = 3f)
  {
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
  public ConverterRig DrainSteel(int maxTicks = 20)
  {
    for (int i = 0; i < maxTicks && ContentUnits > 0; i++)
    {
      int before = ContentUnits;
      Pour();
      if (ContentUnits == before)
        break; // output full - no more progress
    }
    return this;
  }

  /// <summary>Pours slag until the slag pool empties or the output cell stops accepting (bounded).</summary>
  public ConverterRig DrainSlag(int maxTicks = 20)
  {
    for (int i = 0; i < maxTicks && SlagUnits > 0f; i++)
    {
      float before = SlagUnits;
      PourSlag();
      if (SlagUnits == before)
        break;
    }
    return this;
  }

  // Blows exactly the blast that takes the bath's carbon down to `to`, so a scenario reaches a chosen
  // phase (steel / over-blown iron) without simulating the whole multi-minute blow tick by tick.
  private void BlowToCarbon(float to)
  {
    float carbon = Carbon;
    float blast = System.Math.Max(
      0f,
      (carbon - to) / SmexValues.BessemerCarbonPerBlastLitre
    );
    ReflectionHelpers.Invoke(Control, "BlowStep", blast);
  }

  /// <summary>Fully blows the pig charge down into the steel window (mass sheds into slag as it goes).</summary>
  public ConverterRig BlowToSteel()
  {
    BlowToCarbon(SmexValues.BessemerSteelCarbonTarget / 2f);
    return this;
  }

  /// <summary>Keeps blowing past the over-blow floor, retyping the steel to soft ingot iron.</summary>
  public ConverterRig OverBlowToIron()
  {
    BlowToCarbon(SmexValues.BessemerOverblowCarbon / 2f);
    return this;
  }

  private ConverterRig Invoke(string method)
  {
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
/// Models the cowper stove's regenerator cycle (handbook hot-blast article): a single stove
/// <strong>charges</strong> its brick core from hot furnace exhaust piped to its intake, then
/// <strong>discharges</strong> by passing cool blast air through it - the air leaves scorching hot
/// (hot blast for the furnace tuyeres) and the core gives its heat up. Wires the stove's exhaust
/// intake (charge), and the air passthrough + hot-air outlet it reads on discharge.
/// </summary>
internal sealed class CowperRig
{
  public readonly TestWorld World;
  public readonly BlockEntityCowperStove Stove;

  /// <summary>The stove's raised footprint - its cells address the fittings it reads.</summary>
  public readonly StructureRig Structure;

  private readonly PipeNetwork _exhaust;
  private readonly BlockEntityPipePassthrough _airIn;
  private readonly PipeNetwork _airInNet;
  private readonly PipeNetwork _hotOut;

  public CowperRig()
  {
    World = new TestWorld();
    World.RegisterNetwork("pipe", sys => new PipeNetwork(sys));

    var pos = new BlockPos(0, 8, 0);
    Stove = new BlockEntityCowperStove
    {
      Pos = pos,
      // The layout's origin cell wants "smex:cowperstove-intake*", so the anchor has to wear that
      // code - the old "smex:cowperstove-north" would leave the stove one cell short of complete.
      Block = TestBlocks.Configure(
        new Block(),
        "smex:cowperstove-intake-tier3-north",
        1,
        ("side", "north")
      ),
    };
    World.Place(pos, Stove.Block, Stove);
    World.Attach(Stove);

    // The stove's structure faces OPPOSITE its side variant (the +180 convention it shares with the
    // boiler body), so the shell stands on -Z while the exhaust intake face looks out along +Z.
    Structure = StructureRig.Around(
      World,
      Stove,
      BlockCowperStoveIntake.Definitions("smex").Single(),
      angle: (ExOrientation.AngleFromSide("north") + 180) % 360
    );

    // The two fitting cells the stove actually reads on discharge, placed before the fill with the
    // real lpex fittings their layout cells name. A generic pipe here is a network node all the same,
    // which is exactly why it used to pass: it carried air but never satisfied the footprint.
    BlockPos airInPos = Structure.Cell(0, 1, 2);
    var ptBlock = PipeFittings.Passthrough(id: 20);
    _airIn = new BlockEntityPipePassthrough { Pos = airInPos.Copy(), Block = ptBlock };
    Structure.Occupy(airInPos, ptBlock, _airIn);

    BlockPos hotOutPos = Structure.Cell(0, 1, 0);
    var outBlock = PipeFittings.Outlet(id: 21);
    var outBe = new BlockEntityPipeOutlet { Pos = hotOutPos.Copy(), Block = outBlock };
    Structure.Occupy(hotOutPos, outBlock, outBe);

    // Raise the rest of the shell, run the real Initialize - which is what caches the tunables and
    // derives _connectorFace, both of which used to be hand-poked - and let the stove's own monitor
    // tick find its finished structure.
    Structure.Complete();

    World.AddNode(airInPos, "pipe");
    ReflectionHelpers.SetProperty(_airIn, nameof(_airIn.NetworkSystem), World.Networks);
    _airInNet = (PipeNetwork)World.NetworkAt(airInPos)!;

    World.AddNode(hotOutPos, "pipe");
    // Attach (not Initialize) leaves NetworkSystem unset; the outlet needs it to produce into its net.
    ReflectionHelpers.SetProperty(outBe, nameof(outBe.NetworkSystem), World.Networks);
    _hotOut = (PipeNetwork)World.NetworkAt(hotOutPos)!;

    // Charge side: a sealed exhaust run butted against the stove's exhaust intake face. The stove's
    // own block caps the near end - the old rig dropped a rock on that cell, which now would be
    // dropping a rock on the stove.
    // Read the face the stove itself derived at Initialize rather than restating it, so a change to
    // the +180 convention moves the exhaust run with it instead of silently unplumbing the rig.
    var connector = (BlockFacing)
      ReflectionHelpers.GetField(Stove, "_connectorFace")!;
    _exhaust = SealedRunOn(pos, connector, 2);
  }

  private BlockPos GlobalPos(int x, int y, int z) =>
    (BlockPos)ReflectionHelpers.Invoke(Stove, "GetGlobalPos", x, y, z)!;

  /// <summary>A sealed 2-cell pipe run butted against <paramref name="face"/> of <paramref name="at"/>.</summary>
  private PipeNetwork SealedRunOn(BlockPos at, BlockFacing face, int firstId)
  {
    var pipe = PipeTestWorld.MakePipe(orientation: "ns", id: firstId);
    BlockPos p1 = at.AddCopy(face);
    BlockPos p2 = p1.AddCopy(face);
    World.Place(p1, pipe);
    World.Place(p2, pipe);
    World.Place(
      p2.AddCopy(face),
      TestBlocks.Configure(new Block(), "game:rock", 98)
    ); // far cap
    // No near cap: `at` is the machine itself, which seals that end on its own.
    World.AddNode(p1, "pipe");
    World.AddNode(p2, "pipe");
    return (PipeNetwork)World.NetworkAt(p1)!;
  }

  /// <summary>Charges the exhaust intake with hot furnace exhaust, then runs one production tick.</summary>
  public CowperRig ChargeFromExhaust(float exhaustTemp, float litres = 60f)
  {
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
  /// Feeds cool blast air into the passthrough, then runs one production tick (discharge). The exhaust
  /// line is valved off first (drained) - a stove only discharges while it is NOT taking exhaust, the
  /// real two-stove charge/discharge swap.
  /// </summary>
  public CowperRig DischargeAir(float airTemp = 20f, float litres = 60f)
  {
    _exhaust.TryConsumeGas(float.MaxValue, World.Accessor); // valve the exhaust off
    _airInNet.TryProduceGas(
      litres,
      airTemp,
      "Air",
      World.Accessor,
      maxOutputPressure: 3f
    );
    // The stove reads the passthrough's client-synced Volume/Medium/Temperature, so push the network
    // state into those display fields (a broadcast) before the tick.
    _airInNet.BroadcastUpdate(World.Accessor);
    Tick();
    return this;
  }

  /// <summary>
  /// Both valves open: pumps fresh blast air into the passthrough AND hot exhaust into the intake on
  /// the same tick - the genuine "mixing" misconfiguration the stove must refuse to charge through.
  /// </summary>
  public CowperRig MixAirAndExhaust(
    float exhaustTemp = 1200f,
    float airLitres = 60f,
    float exhaustLitres = 60f
  )
  {
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

  /// <summary>One production tick of the stove, with nothing fed in - the idle case.</summary>
  public CowperRig Tick()
  {
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
