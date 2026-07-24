using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkPipe.BlockEntities;
using IronworkingExpanded.BlockNetworkPipe.Blocks;
using IronworkingExpanded.Tests;
using LowPressureExpanded.BlockNetworkPipe.BlockEntities;
using LowPressureExpanded.BlockStructures.Engine.BlockEntities;
using LowPressureExpanded.BlockStructures.Engine.Blocks;
using Vintagestory.API.MathTools;
using Xunit;
using BoilerState = LowPressureExpanded.BlockStructures.Boiler.BlockEntityBoiler.BoilerState;

namespace LowPressureExpanded.Tests;

/// <summary>
/// Whole-process scenarios that combine structures and networks the way the in-game setup diagrams do
/// (docs/setups): a steam engine turning a pump to produce water, and a real boiler driving an engine
/// end to end. Each lays the machines + their pipe lines into one <see cref="Scene"/> and advances them
/// together, asserting the emergent result rather than any single component.
/// <para>
/// The Cornish-engine halves of this file (MP generator, air blower) moved to the hpex suite with the
/// engine itself - see <c>HighPressureExpanded.Tests.HpSteamPlantScenarioTests</c>.
/// </para>
/// </summary>
public class SteamPlantScenarioTests
{
  #region Water production (boiler→engine→pump→water)

  [Fact]
  public void Steam_drives_the_pump_to_lift_water_into_the_output_main()
  {
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var plant = new WaterPumpPlant(scene, new BlockPos(0, 8, 0));
    scene.Build();

    plant.FillPond(30f); // pond stocked
    plant.RunWithSteam(3f, 3); // hold the inlet in the Watt band for three ticks

    Assert.True(
      plant.Engine.IsRunning,
      "the engine should engage on band steam"
    );
    Assert.True(
      plant.OutputVolume > 0f,
      "the pump should lift water into the output main"
    );
  }

  [Fact]
  public void Without_steam_the_pump_stays_idle_and_no_water_moves()
  {
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var plant = new WaterPumpPlant(scene, new BlockPos(0, 8, 0));
    scene.Build();

    plant.FillPond(30f); // pond stocked, but no steam at the engine
    scene.Step(3);

    Assert.False(plant.Engine.IsRunning);
    Assert.Equal(0f, plant.OutputVolume, 3);
  }

  [Fact]
  public void Below_the_engage_pressure_the_engine_will_not_drive_the_pump()
  {
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var plant = new WaterPumpPlant(scene, new BlockPos(0, 8, 0));
    scene.Build();

    plant.FillPond(30f);
    plant.RunWithSteam(1f, 3); // 1 atm < the Watt engine's 2 atm engage

    Assert.False(plant.Engine.IsRunning);
    Assert.Equal(0f, plant.OutputVolume, 3);
  }

  #endregion

  #region Real boiler driving an engine over a shared steam main

  [Fact]
  public void A_fired_boiler_charges_the_main_and_runs_an_attached_engine_pump()
  {
    // Boiler steam outlet and the engine's south inlet meet at one bend pipe (down→boiler,
    // north→engine), so the boiler's own steam - not an injected charge - drives the whole chain.
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var boiler = new BoilerFixture(scene, new BlockPos(0, 8, 0));
    BlockPos attach = boiler.SteamPipeAttachPos;

    // The shared steam main: a single junction pipe carrying a down connector (the boiler attaches
    // there) and a north connector (the engine reads across its inlet face).
    EnginePlant.Pipe(scene, attach, "dn", 50);
    // ...and one more cell of main, so the engine house stands clear of the boiler. The down face is
    // sealed by the boiler's own steam-port filler, which its raised shell now occupies for real -
    // dropping a cap block there instead, as this scene used to, overwrites that layout cell and the
    // next structure monitor tick finds the boiler broken.
    EnginePlant.Pipe(scene, attach.AddCopy(0, 0, -1), "ns", 51);

    // Watt engine at the far end of that main, its south inlet face on the last pipe. It has to sit
    // two cells out, not one: the pump's submachine is two further north again and its pond intake
    // one below that, which at the old spacing landed *inside the boiler's brick shell* - a cell no
    // player could build in. Nothing could see that while the shell was never raised.
    var engine = BuildEnginePump(
      scene,
      attach.AddCopy(0, 0, -2),
      out var output,
      out var pond
    );

    scene.Build();
    // Water near the top of the 800 L vessel + a hot steam charge => internal pressure
    // steam/(Capacity-water) = 700/200 = 3.5 atm, which equalises the small main into the Watt band.
    boiler.Prime(BoilerState.Heating, water: 600f, steam: 700f);
    pond.Prime(30f);
    scene.Step(5);

    var steamMain = scene.NetworkAt<PipeNetwork>(attach)!;
    Assert.Equal("Steam", steamMain.State!.MediumType); // boiler charged the main
    Assert.True(
      engine.InletPressure > 0f,
      "the engine should read the boiler's steam"
    );
    Assert.True(
      engine.IsRunning,
      "the boiler's steam should engage the engine"
    );
    Assert.True(output() > 0f, "the boiler-driven pump should produce water");
  }

  /// <summary>
  /// Stands a constructed Watt engine + fluid pump at <paramref name="enginePos"/> with a pond intake
  /// below the pump and a sealed output main on its left, returning the engine and accessors for the
  /// output volume and the pond. The engine's steam inlet is left open for an external main to feed.
  /// </summary>
  private static BlockEntityEngineWatt BuildEnginePump(
    Scene scene,
    BlockPos enginePos,
    out System.Func<float> output,
    out PondAccessor pond
  )
  {
    var engineBlock = TestBlocks.Configure(
      new BlockEngineWatt(),
      "lpex:enginewatt-north",
      60,
      ("side", "north")
    );
    var engine = new BlockEntityEngineWatt
    {
      Pos = enginePos.Copy(),
      Block = engineBlock,
    };
    scene.Machine(enginePos, engineBlock, engine);
    RccFake.Complete(engine);

    BlockPos subPos = engineBlock.SubmachinePos(enginePos);
    var pumpBlock = TestBlocks.Configure(
      new BlockEngineFluidPump(),
      "lpex:enginefluidpump-east",
      61,
      ("side", "east")
    );
    var pump = new BlockEntityEngineFluidPump
    {
      Pos = subPos.Copy(),
      Block = pumpBlock,
    };
    scene.Machine(subPos, pumpBlock, pump);

    // Pond intake below the pump.
    BlockPos pondPos = subPos.DownCopy();
    var intakeBlock = TestBlocks.Configure(
      new LowPressureExpanded.BlockNetworkPipe.Blocks.BlockFluidIntake(),
      "lpex:fluidintake",
      62,
      ("orientation", "u")
    );
    ReflectionHelpers.SetProperty(intakeBlock, "Orientation", "u");
    var intake = new BlockEntityFluidIntake
    {
      Pos = pondPos.Copy(),
      Block = intakeBlock,
    };
    scene.Node(pondPos, intakeBlock, intake, "pipe");
    ReflectionHelpers.SetProperty(intake, nameof(intake.HasWater), true);
    ReflectionHelpers.SetProperty(
      intake,
      nameof(intake.NetworkSystem),
      scene.World.Networks
    );

    // Output main on the pump's left face (rotated with the pump's "east" placement), oriented along
    // that face's axis so it actually connects.
    BlockFacing leftFace = ExpandedLib.Helpers.ExOrientation.RotateFacing(
      BlockFacing.WEST,
      ExpandedLib.Helpers.ExOrientation.AngleFromSide("east")
    );
    BlockPos outPos = subPos.AddCopy(leftFace);
    EnginePlant.Pipe(scene, outPos, EnginePlant.Axis(leftFace), 63);
    scene.Block(outPos.AddCopy(leftFace), LpexScenes.Cap(64));

    output = () => scene.NetworkAt<PipeNetwork>(outPos)!.State?.Volume ?? 0f;
    pond = new PondAccessor(scene, pondPos);
    return engine;
  }

  /// <summary>Lets the combined test stock the pond after Build (when its network exists).</summary>
  internal sealed class PondAccessor
  {
    private readonly Scene _scene;
    private readonly BlockPos _pos;

    public PondAccessor(Scene scene, BlockPos pos)
    {
      _scene = scene;
      _pos = pos;
    }

    public void Prime(float litres) =>
      _scene
        .NetworkAt<PipeNetwork>(_pos)!
        .TryProduceLiquid(litres, 12f, 1f, _scene.World.Accessor);
  }

  #endregion

  #region Multi-layer manifold (exercises SceneDiagram.Stack)

  [Fact]
  public void A_three_layer_riser_with_a_top_arm_is_one_network()
  {
    // An L-shaped 3D run spanning all axes: a two-cell vertical riser (Y) whose top elbow turns east
    // into a horizontal arm (X). Built bottom-to-top with SceneDiagram.Stack. 'I' is an up-down pipe,
    // 'L' a down+east elbow, '=' a west-east pipe, '#' a cap.
    var scene = new Scene().Network("pipe", s => new PipeNetwork(s));
    var ud = PipeTestWorld.MakePipe(orientation: "ud", id: 70);
    var we = PipeTestWorld.MakePipe(orientation: "we", id: 71);
    var elbow = PipeTestWorld.MakePipe(orientation: "de", id: 72); // down + east connectors
    var cap = LpexScenes.Cap(73);
    BlockEntityPipe Be(BlockPos p, BlockPipe b) =>
      new() { Pos = p.Copy(), Block = b };

    new SceneDiagram()
      .On('I', p => scene.Node(p, ud, Be(p, ud), "pipe"))
      .On('=', p => scene.Node(p, we, Be(p, we), "pipe"))
      .On('L', p => scene.Node(p, elbow, Be(p, elbow), "pipe"))
      .On('#', p => scene.Block(p, cap))
      .Stack(
        baseY: 0,
        "I", // y=0: riser foot (charge point)
        "I", // y=1: riser
        "L==#" // y=2: elbow turning east into a capped horizontal arm
      );
    scene.Build();

    var foot = new BlockPos(0, 0, 0);
    var armEnd = new BlockPos(2, 2, 0);
    // The riser + elbow + arm resolve to a single network spanning X and Y.
    Assert.Same(
      scene.NetworkAt<PipeNetwork>(foot),
      scene.NetworkAt<PipeNetwork>(armEnd)
    );

    scene
      .NetworkAt<PipeNetwork>(foot)!
      .TryProduceGas(
        150f,
        150f,
        "Steam",
        scene.World.Accessor,
        maxOutputPressure: 10f
      );
    scene.Step();

    Assert.True(
      scene.EntityAt<BlockEntityPipe>(armEnd)!.Pressure > 0f,
      "steam at the riser foot should reach the far end of the top arm"
    );
  }

  #endregion
}
