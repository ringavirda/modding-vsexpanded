using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkPipe;
using IronIndustryExpanded.BlockNetworkPipe.BlockEntities;
using IronIndustryExpanded.BlockNetworkPipe.Blocks;
using IronIndustryExpanded.BlockStructures.Boiler.BlockEntities;
using IronIndustryExpanded.BlockStructures.Boiler.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using BoilerState = IronIndustryExpanded.BlockStructures.Boiler.BlockEntityBoiler.BoilerState;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// iiex-specific building blocks for <see cref="Scene"/> integration tests: a pipe-diagram legend and
/// a boiler fixture. They hold the mod knowledge - which glyph is which oriented pipe, how a boiler is
/// stood up - so scenario tests read as layouts plus assertions.
/// </summary>
public static class IiexScenes {
  /// <summary>A cap block that seals a pipe end so a run can pressurise instead of leaking.</summary>
  public static Block Cap(int id = 99) =>
    TestBlocks.Configure(new Block(), "game:rock", id);

  /// <summary>One shared oriented pipe block; the game reuses a single instance across a run.</summary>
  public static BlockPipe Pipe(string orientation, int id) =>
    PipeTestWorld.MakePipe(orientation: orientation, id: id);

  /// <summary>
  /// Registers a pipe-network diagram legend on <paramref name="scene"/>:
  /// <c>=</c> west-east pipe, <c>|</c> north-south pipe, <c>I</c> vertical (up-down) pipe,
  /// <c>#</c> a sealing cap. Each glyph shares one oriented block instance, as the game does.
  /// </summary>
  public static SceneDiagram PipeLegend(Scene scene) {
    var we = Pipe("we", 1);
    var ns = Pipe("ns", 2);
    var ud = Pipe("ud", 3);
    var cap = Cap();

    BlockEntityPipe Be(BlockPos p, BlockPipe block) =>
      new() { Pos = p.Copy(), Block = block };

    return new SceneDiagram()
      .On('=', p => scene.Node(p, we, Be(p, we), "pipe"))
      .On('|', p => scene.Node(p, ns, Be(p, ns), "pipe"))
      .On('I', p => scene.Node(p, ud, Be(p, ud), "pipe"))
      .On('#', p => scene.Block(p, cap));
  }
}

/// <summary>
/// A constructed, fired Cornish boiler placed into a shared <see cref="Scene"/>: the integration-test
/// counterpart of <see cref="BoilerRig"/>, which owns its own world. It registers the real production
/// tick, so <see cref="Scene.Step"/> drives it, and exposes operating state for setup and assertions.
/// </summary>
internal sealed class BoilerFixture {
  public readonly BlockEntityBoilerCornish Be;
  public readonly BlockBoilerCornish Block;

  public BoilerFixture(
    Scene scene,
    BlockPos pos,
    int blockId = 10,
    string fuelCode = "game:ore-bituminouscoal"
  ) {
    Block = TestBlocks.Configure(
      new BlockBoilerCornish(),
      "iiex:boilercornish-n",
      blockId,
      ("side", "north")
    );
    Be = new BlockEntityBoilerCornish { Pos = pos.Copy(), Block = Block };

    scene.World.Place(pos, Block, Be);
    // Attaches the shipped attributes, Initializes, completes the right-click construction and hosts
    // the declared fuel bed: Initialize clears _rcc off the absent behaviors, so the fake follows it.
    BoilerFakes.Commission(scene.World, Be, BoilerFakes.CornishDef);

    var bed = Be.Bed!;
    Item fuel = scene.World.RegisterItem(fuelCode);
    bed.TryAdd(new ItemStack(fuel, bed.CellCapacity), bed.CellCapacity);
    ReflectionHelpers.SetField(Be, "_lit", true);
  }

  public BoilerFixture Prime(BoilerState state, float water, float steam) {
    ReflectionHelpers.SetField(Be, "_state", state);
    ReflectionHelpers.SetField(Be, "_waterVolume", water);
    ReflectionHelpers.SetField(Be, "_steamVolume", steam);
    return this;
  }

  public float SteamVolume =>
    (float)ReflectionHelpers.GetField(Be, "_steamVolume")!;

  /// <summary>World cell of the steam pipe, across the connector cell's own declared port face - read
  /// off the footprint the way the boiler reads it, so a scene cannot plumb a face the vessel does not
  /// push through.</summary>
  public BlockPos SteamPipeAttachPos =>
    Block.SteamPipeWorldPos(Be.Pos).AddCopy(Block.SteamWorldFace!);
}
