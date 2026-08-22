using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Metals;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Casting.BlockEntities;
using IronIndustryExpanded.BlockStructures.Casting.Blocks;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;
using static IronIndustryExpanded.Tests.FurnaceLayoutRig;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// U9's gate: bituminous coal to a crucible-steel ingot, through every machine the unit built. The coke
/// oven bakes the fuel, a cold blister ingot is crushed to charge, a fireclay pot takes it, the crucible
/// furnace preheats and melts it on a chimney the player built, and the pot pours into a cast-iron mould.
/// Nothing forces <c>StructureComplete</c> and nothing fast-forwards a state: both furnaces stand their
/// real footprints and do the work on their own ticks.
/// </summary>
/// <remarks>
/// Two links are arithmetic rather than live, and are marked where they appear: the helve crush needs an
/// anvil the harness cannot drive, and the pour is driven through the sink's own
/// <c>ReceiveLiquidMetal</c> - vanilla's held-item interaction is what calls it in game, and that is the
/// part not exercised here.
/// </remarks>
[Collection(FurnaceConfigCollection.Name)]
public class CrucibleSteelScenarioTests {
  #region Harness

  private static readonly BlockPos Anchor = new(0, 16, 0);

  private const string Bituminous = "game:ore-bituminouscoal";
  private const string Coke = "game:coke";
  private const string CrucibleIngot = "iiex:ingot-cruciblesteel";

  /// <summary>
  /// A crucible furnace stood facing north, with a real hearth, a real damper, and
  /// <paramref name="courses"/> of chimney built over its drawn stack base.
  /// </summary>
  private sealed class Scene {
    public readonly BlockEntityCrucibleFurnace Furnace;
    public readonly BlockEntityCrucibleHearth Hearth;
    public readonly BlockEntityPuddlingChimneyCap Damper;
    public readonly StructureRig Rig;

    public Scene(int courses) {
      Furnace = new BlockEntityCrucibleFurnace();
      Rig = Stand(
        Furnace,
        BlockCrucibleFurnaceCore.Definitions("iiex").Single(),
        Anchor,
        "iiex:furnace-cruciblecore-tier1",
        "north"
      );
      SeatCrucibleHearth(Rig, Furnace, unitsPerCell: 0);
      Hearth = Furnace.Hearth!;

      Rig.World.RegisterItem(Coke);
      Rig.World.RegisterItem(BlisterBreaking.ChunkCode);
      Rig.World.RegisterItem(BlisterBreaking.BitCode);
      Rig.World.RegisterItem(CrucibleIngot, 1600f);
      Rig.World.Register(
        TestBlocks.Configure(
          new BlockSteelCrucible(),
          "iiex:steelcrucible-burned",
          910,
          ("type", "burned")
        )
      );
      Rig.World.Register(
        TestBlocks.Configure(
          new BlockSteelCruciblePour(),
          "iiex:steelcrucible-smelted",
          911,
          ("type", "smelted")
        )
      );

      BlockPos cell = Furnace.CellsWithRole(CellRole.Damper).Single();
      Damper = new BlockEntityPuddlingChimneyCap { Pos = cell.Copy() };
      Rig.Occupy(
        cell,
        TestBlocks.Configure(
          new Block(),
          "iiex:furnace-puddlingchimneycap-s",
          906,
          ("side", "south")
        ),
        Damper
      );

      Build(courses);
    }

    /// <summary>Lays <paramref name="courses"/> brick rings over the drawn stack base.</summary>
    public void Build(int courses) {
      var brick = TestBlocks.Configure(
        new Block(),
        "game:brickcourse-four-running-black",
        907
      );
      BlockPos above = Furnace
        .CellsWithRole(CellRole.Flue)
        .Aggregate((a, b) => b.Y > a.Y ? b : a)
        .UpCopy();

      for (int i = 0; i < courses; i++) {
        foreach (BlockFacing side in BlockFacing.HORIZONTALS)
          Rig.World.Place(above.AddCopy(side), brick);
        above = above.UpCopy();
      }
    }

    /// <summary>Charges the hearth's bed with <paramref name="units"/> of fuel per cell.</summary>
    public void Fuel(int units) {
      Item coke = Rig.World.World.GetItem(new AssetLocation(Coke));
      foreach (BlockPos cell in Furnace.FireboxCells)
        if (
          Rig.World.GetBlockEntity(cell) is BlockEntityCrucibleHearth bed
          && bed.Bed is { } fire
        )
          fire.TryAdd(new ItemStack(coke, units), units);
    }

    /// <summary>Seats one pot and fills it, in the denominations the crush pays out in.</summary>
    public void LoadPot(int chunks, int bits) {
      Hearth.Seat(
        new ItemStack(
          Rig.World.World.GetBlock(
            new AssetLocation("iiex:steelcrucible-burned")
          )
        )
      );
      Hearth.Charge(
        new ItemStack(
          Rig.World.World.GetItem(new AssetLocation(BlisterBreaking.ChunkCode)),
          chunks
        )
      );
      Hearth.Charge(
        new ItemStack(
          Rig.World.World.GetItem(new AssetLocation(BlisterBreaking.BitCode)),
          bits
        )
      );
    }

    public void Tick(int seconds) {
      for (int i = 0; i < seconds; i++)
        ReflectionHelpers.Invoke(Furnace, "OnProductionTick", 1f);
    }

    /// <summary>Ticks until <paramref name="until"/> holds, up to <paramref name="ceiling"/> seconds.</summary>
    public int RunUntil(System.Func<bool> until, int ceiling) {
      for (int t = 1; t <= ceiling; t++) {
        ReflectionHelpers.Invoke(Furnace, "OnProductionTick", 1f);
        if (until())
          return t;
      }
      return 0;
    }

    public float Temp =>
      (float)ReflectionHelpers.GetField(Furnace, "_internalTemp")!;
  }

  #endregion

  #region The whole chain

  /// <summary>
  /// Coal in, a crucible-steel ingot out. Every step is the machine's own code doing it.
  /// </summary>
  [Fact]
  public void Coal_becomes_coke_becomes_a_crucible_steel_ingot() {
    // 1. The coke oven bakes bituminous coal into coke, on its own clock.
    Assert.True(BakeCoke() > 0, "the oven should have made coke");

    // 2. A cold blister ingot crushes to 100 u: three chunks off the helve and the five bits the anvil
    //    hands back. Arithmetic, not live - the harness has no anvil.
    float remainder = 0f;
    int chunks = 0;
    for (int i = 0; i < BlisterBreaking.ShedVoxels; i++)
      chunks += BlisterBreaking.Emit(1, ref remainder);
    const int BitsFromTheShape = 5;
    Assert.Equal(3, chunks);
    Assert.Equal(
      BlisterBreaking.IngotUnits,
      chunks * BlisterBreaking.ChunkUnits
        + BitsFromTheShape * BlisterBreaking.BitUnits
    );

    // 3. The furnace stands its own footprint, with a chimney at the peak of the draught curve.
    var scene = new Scene(courses: BlockEntityCrucibleFurnace.PeakCourses - 2);
    Assert.True(
      scene.Furnace.StructureComplete,
      "the furnace should have completed its own footprint"
    );

    // 4. One pot takes more than one ingot's crush: 110 u is four chunks and two bits, where a crush pays
    //    three and five. Two ingots go in and change comes back out.
    scene.LoadPot(chunks: 4, bits: 2);
    Assert.True(scene.Hearth.Holes[0].Charged);

    // 5. Fuel it and light it. Nothing forces the state: the tick's own ignition does it.
    scene.Fuel(IiexValues.FireboxMixPerCell);
    Assert.True(
      scene.RunUntil(() => scene.Furnace.State != FurnaceState.Idle, 30) > 0,
      "a full hearth of coke should light"
    );

    // 6. Preheat on a shut damper, which is where it starts.
    Assert.False(scene.Damper.IsOpen);
    scene.Tick((int)IiexValues.CruciblePreheatSec);
    Assert.True(
      scene.Hearth.Holes[0].Preheated,
      "a shut damper should bring the pot up"
    );
    Assert.True(
      scene.Temp < IiexValues.CrucibleMeltingPointC,
      "and hold the fire below the melt line while it does"
    );

    // 7. Throw the damper. Full draught, and the heat runs.
    scene.Damper.Toggle();
    Assert.True(
      scene.RunUntil(() => scene.Hearth.Holes[0].Molten, MeltCeilingSeconds)
        > 0,
      $"the pot should melt on a {BlockEntityCrucibleFurnace.PeakCourses}-course "
        + $"chimney; the furnace reached {scene.Temp} C"
    );
    Assert.True(scene.Furnace.StructureComplete);

    // 8. Pull it. The pot comes out pourable, carrying the heat it just gave.
    ItemStack? pot = scene.Hearth.Pull();
    Assert.NotNull(pot);
    Assert.Equal(1, CrucibleFiring.Of(pot));

    // 9. Pour it into a cast-iron ingot mould, two units at a time as vanilla's own pour does.
    BlockEntityCastMold mold = Mold(scene);
    ItemStack metal = Contents(scene, pot!);
    int units = pot!.Attributes.GetInt(CruciblePot.UnitsKey);
    Assert.Equal(IiexValues.CruciblePotYieldUnits, units);

    while (units > 0 && mold.CanReceiveAny) {
      int amount = System.Math.Min(2, units);
      mold.ReceiveLiquidMetal(
        metal,
        ref amount,
        IiexValues.CrucibleMeltingPointC
      );
      units -= 2 - amount;
    }

    // 10. And the mould yields the ingot the whole chain exists for.
    Assert.True(mold.IsFull, "one pot should fill a one-ingot mould exactly");
    Assert.Equal(
      CrucibleIngot,
      mold.GetMoldedStacks(metal).Single().Collectible.Code.ToString()
    );
  }

  #endregion

  #region The chimney is outside the structure

  /// <summary>
  /// The furnace completes and runs with no chimney at all, and simply does not get hot enough. That is
  /// the design - the stack is deliberately outside <c>StructureComplete</c>, because height is a dial the
  /// player turns rather than a part they are refused for missing - and it has to be pinned rather than
  /// "fixed" by someone who reads a cold furnace as a broken one.
  /// </summary>
  [Fact]
  public void The_furnace_completes_and_runs_cold_with_no_chimney() {
    var scene = new Scene(courses: 0);

    Assert.True(scene.Furnace.StructureComplete);

    scene.LoadPot(chunks: 4, bits: 2);
    scene.Fuel(IiexValues.FireboxMixPerCell);
    scene.Damper.Toggle();
    Assert.True(
      scene.RunUntil(() => scene.Furnace.State != FurnaceState.Idle, 30) > 0,
      "it should light with no chimney"
    );

    scene.Tick(120);

    Assert.True(
      scene.Temp < IiexValues.CrucibleMeltingPointC,
      $"a stackless furnace should run cold, was {scene.Temp} C"
    );
    Assert.False(scene.Hearth.Holes[0].Molten);
  }

  /// <summary>
  /// And every course the player lays makes it hotter, up to the peak. This is the trade the machine is
  /// built on: buy operating capability with build complexity.
  /// </summary>
  [Fact]
  public void Every_course_the_player_lays_makes_the_furnace_hotter() {
    var scene = new Scene(courses: 0);
    scene.Fuel(IiexValues.FireboxMixPerCell);
    scene.Damper.Toggle();
    scene.RunUntil(() => scene.Furnace.State != FurnaceState.Idle, 30);

    var reached = new List<float>();
    foreach (int _ in Enumerable.Range(0, 4)) {
      scene.Tick(120);
      reached.Add(scene.Temp);
      scene.Build(courses: 1);
    }

    Assert.Equal(reached.OrderBy(t => t), reached);
    Assert.True(
      reached[^1] > reached[0],
      $"four courses should be hotter than none: {reached[0]} -> {reached[^1]} C"
    );
  }

  #endregion

  #region Fixtures

  /// <summary>
  /// Ceiling for one pot's melt, with slack: the preheat is already done, so what is waited for is
  /// `CrucibleMeltSec` at the cadence the core hands out. Waited for, never sampled at a fixed offset.
  /// </summary>
  private const int MeltCeilingSeconds = 2400;

  /// <summary>Runs a real coke oven on bituminous coal and returns the coke it made, in units.</summary>
  private static int BakeCoke() {
    var oven = new BlockEntityCokeOven();
    StructureRig rig = Stand(
      oven,
      BlockCokeOvenCore.Definitions("iiex").Single(),
      new BlockPos(64, 16, 64),
      "iiex:furnace-cokeovencore",
      "north",
      complete: false
    );
    CloseCokeOven(rig);
    rig.Complete();
    rig.World.RegisterItem(Coke);
    LoadFireboxes(rig, oven, IiexValues.FireboxMixPerCell, Bituminous);

    for (float t = 0; t < IiexValues.CokeOvenCycleSec + 60f; t += 60f)
      ReflectionHelpers.Invoke(oven, "SmeltCycle", [new object(), 60f]);

    return oven
      .FireboxCells.Select(c =>
        rig.World.GetBlockEntity(c) as BlockEntityFirebox
      )
      .Where(be => be?.Bed?.FuelCode == Coke)
      .Sum(be => be!.Bed!.Units);
  }

  /// <summary>
  /// A placed cast-iron ingot mould, carrying its own shipped attributes so the cast it yields is resolved
  /// the way the game resolves it rather than by a fixture's guess.
  /// </summary>
  private static BlockEntityCastMold Mold(Scene scene) {
    MetalRegistry.Register(CrucibleSteel());

    JToken attributes = BlockCastMold.Definitions("iiex").Single().ToJson()[
      "attributesByType"
    ]!["casting-mold-ingot"]!;
    Block block = TestBlocks.Configure(
      new BlockCastMold(),
      "iiex:casting-mold-ingot",
      920,
      ("tooltype", "ingot")
    );
    block.Attributes = new JsonObject(attributes);

    var pos = Anchor.AddCopy(8, 0, 0);
    var mold = new BlockEntityCastMold { Pos = pos };
    scene.Rig.World.Place(pos, block, mold);
    scene.Rig.World.Attach(mold);
    // Its capacity is read from the block's own attributes in Initialize, and an uninitialised mold reads
    // as full at zero - so it would refuse the pour and every assertion after it would be measuring an
    // empty tray.
    mold.Initialize(scene.Rig.World.Api);
    Assert.True(mold.MaxUnitAmount > 0);
    return mold;
  }

  /// <summary>The metal a pourable pot says it is carrying.</summary>
  private static ItemStack Contents(Scene scene, ItemStack pot) {
    ItemStack? metal = pot.Attributes.GetItemstack(CruciblePot.ContentsKey);
    Assert.NotNull(metal);
    metal!.ResolveBlockOrItem(scene.Rig.World.World);
    return metal;
  }

  /// <summary>
  /// The shipped crucible-steel descriptor, so the mould resolves its cast in the metal's own cast domain
  /// (<c>iiex</c>) rather than falling back to <c>game:</c>, which has no such ingot.
  /// </summary>
  private static MetalDef CrucibleSteel() =>
    Newtonsoft.Json.JsonConvert.DeserializeObject<MetalDef>(
      System.IO.File.ReadAllText(
        DefinitionGoldens.SolutionRelative(
          "assets/iiex/config/metals/cruciblesteel.json"
        )
      )
    )!;

  #endregion
}
