using System.Linq;
using ExpandedLib.Catalogues;
using ExpandedLib.Industry.MechanicalPower;
using ExpandedLib.Networks;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Forming;
using IronIndustryExpanded.BlockStructures.Forming.BlockEntities;
using IronIndustryExpanded.BlockStructures.Forming.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The shear as a placed machine: the footprint it reserves, the tooling it holds, and the stroke that
/// turns stock into one product plus a remainder. <see cref="ShearFeedTests"/> covers the decision itself;
/// everything here is what the block and the block entity do with it.
/// See docs/design/machines/shear.md.
/// </summary>
public class ShearStationTests {
  private const string StockCode = "iiex:stock-shingledbar";
  private const string ProductCode = "iiex:rolledplate";

  private static (TestWorld World, BlockEntityShear Shear, BlockPos Pos) Shear(
    ProcessJobRegistry? jobs = null
  ) {
    var world = new TestWorld();
    world.RegisterNetwork("mpenergy", n => new MpEnergyNetwork(n));
    world.RegisterItem(StockCode);
    world.RegisterItem(ProductCode);

    Block block = TestBlocks.Configure(
      new BlockShear(),
      "iiex:forming-shear-ns",
      1,
      ("type", "shear"),
      ("orientation", "ns")
    );
    var pos = new BlockPos(0, 1, 0);
    var shear = new TestShear(jobs);
    world.Place(pos, block, shear);
    world.Initialize(shear);
    return (world, shear, pos);
  }

  // Lets a test stand a crop up without writing into the process-wide catalogue, which every other
  // suite in this assembly also reads.
  private sealed class TestShear : BlockEntityShear {
    internal TestShear(ProcessJobRegistry? jobs) {
      if (jobs != null)
        Jobs = jobs;
    }
  }

  private static ProcessJobRegistry Registry(
    int count = 4,
    int minTier = 0,
    float minTorque = 0f
  ) {
    var registry = new ProcessJobRegistry();
    registry.Contribute(
      new ProcessJobSet(
        ProcessJobSet.CurrentSchema,
        BlockEntityShear.MachineKey,
        [
          new ProcessJob(
            StockCode,
            ProductCode,
            count,
            Stage: 1.0f,
            Family: "flat",
            MinTorque: minTorque,
            MinTier: minTier,
            Seconds: 1f
          ),
        ]
      )
    );
    return registry;
  }

  private static ItemStack Stock(TestWorld world, int cropped = 0) {
    var stack = new ItemStack(world.GetItem(new AssetLocation(StockCode))!);
    new WorkPiece(
      StockForm.ShingledBar,
      1.0f,
      0f,
      [false],
      "flat",
      cropped
    ).ToStack(stack);
    return stack;
  }

  private static ItemStack Blades(TestWorld world, int tier) {
    Item item = world.RegisterItem($"iiex:shearblade-tier{tier}");
    item.Attributes = new Vintagestory.API.Datastructures.JsonObject(
      Newtonsoft.Json.Linq.JToken.Parse(
        $$"""{ "machinetool": { "tier": {{tier}} } }"""
      )
    );
    return new ItemStack(item);
  }

  #region The footprint is the owner's layout

  [Fact]
  public void The_shear_reserves_three_cells_capped_by_three_floor_slabs() {
    // Read off the shipped definition, not off a placed block: the test fixture builds a bare Block with
    // no attributes, so a stub would report an empty footprint and pass this by having nothing to check.
    Newtonsoft.Json.Linq.JToken? offsets = BlockShear
      .Definitions("iiex")
      .Single()
      .ToJson()["attributes"]
      ?["fillerOffsets"];
    Assert.NotNull(offsets);

    var cells = StructureFillers
      .ReadOffsets(new JsonObject(offsets))
      .ToDictionary(c => (c.Offset.X, c.Offset.Y, c.Offset.Z));

    // Five fillers around the principal: the blade nest and the gear cell either side, and the row of
    // three above them. Anything else means the layout in machines.txt was read wrong.
    Assert.Equal(5, cells.Count);
    Assert.Null(cells[(-1, 0, 0)].CollisionBoxes);
    Assert.Null(cells[(1, 0, 0)].CollisionBoxes);

    foreach (int x in new[] { -1, 0, 1 }) {
      Cuboidf[]? box = cells[(x, 1, 0)].CollisionBoxes;
      Assert.NotNull(box);
      // A floor slab: the bottom half of the cell, leaving the space above it open. A full cube here
      // would wall the machine in without any visible sign of it.
      Assert.Equal(0f, Assert.Single(box).Y1);
      Assert.Equal(0.5f, box[0].Y2);
    }
  }

  [Fact]
  public void The_blade_nest_rotates_with_the_placed_orientation() {
    (TestWorld world, _, BlockPos pos) = Shear();
    var authored = (BlockShear)world.GetBlock(pos);

    // ns is the authored frame, so the nest sits one cell west of the principal.
    Assert.Equal(pos.AddCopy(-1, 0, 0), authored.NestCell(pos));

    Block rotated = TestBlocks.Configure(
      new BlockShear(),
      "iiex:forming-shear-we",
      2,
      ("type", "shear"),
      ("orientation", "we")
    );
    Assert.Equal(90, ((BlockShear)rotated).StructureAngle);
    Assert.NotEqual(
      authored.NestCell(pos),
      ((BlockShear)rotated).NestCell(pos)
    );
  }

  #endregion

  #region Tooling

  [Fact]
  public void Blades_fit_and_swap_out_what_was_there() {
    (TestWorld world, BlockEntityShear shear, _) = Shear();
    Assert.False(shear.HasBlades);
    Assert.Equal(-1, shear.BladeTier);

    Assert.True(shear.TryFitBlades(Blades(world, 1), out ItemStack? none));
    Assert.Null(none);
    Assert.Equal(1, shear.BladeTier);

    Assert.True(shear.TryFitBlades(Blades(world, 2), out ItemStack? previous));
    Assert.Equal(2, shear.BladeTier);
    Assert.Equal(1, MachineTool.TierOf(previous));
  }

  [Fact]
  public void Anything_that_is_not_tooling_is_refused() {
    (TestWorld world, BlockEntityShear shear, _) = Shear();

    Assert.False(shear.TryFitBlades(Stock(world), out _));
    Assert.False(shear.HasBlades);
  }

  #endregion

  #region The stroke

  [Fact]
  public void A_crop_yields_one_product_and_the_remainder_with_one_more_tallied() {
    (TestWorld world, BlockEntityShear shear, BlockPos pos) = Shear(Registry());
    shear.TryFitBlades(Blades(world, 1), out _);
    Turning(world, pos);

    ShearDecision decision = shear.TryCrop(Stock(world));
    Assert.True(decision.Accepted);

    Advance(shear, seconds: 1.5f);

    // One product plus the remainder, and the remainder is still stock at the same stage - the
    // crop-not-convert rule. A stroke that returned only the product would be metal destroyed.
    Assert.Equal(2, world.Drops.Count);
    ItemStack product = world.Drops.Single(d =>
      d.Collectible.Code.ToShortString() == ProductCode
    );
    Assert.Equal(1, product.StackSize);

    ItemStack remainder = world.Drops.Single(d =>
      d.Collectible.Code.ToShortString() == StockCode
    );
    WorkPiece? piece = WorkPiece.FromStack(remainder);
    Assert.NotNull(piece);
    Assert.Equal(1, piece.Cropped);
    Assert.Equal(1.0f, piece.Thickness);
  }

  [Fact]
  public void A_worked_out_piece_is_refused_rather_than_cut_again() {
    (TestWorld world, BlockEntityShear shear, BlockPos pos) = Shear(
      Registry(count: 4)
    );
    shear.TryFitBlades(Blades(world, 1), out _);
    Turning(world, pos);

    ShearDecision decision = shear.TryCrop(Stock(world, cropped: 4));

    Assert.Equal(ShearVerdict.Spent, decision.Verdict);
    Assert.False(shear.IsStroking);
  }

  [Fact]
  public void A_stopped_run_holds_the_stroke_rather_than_finishing_it() {
    (TestWorld world, BlockEntityShear shear, BlockPos pos) = Shear(Registry());
    shear.TryFitBlades(Blades(world, 1), out _);
    Turning(world, pos);
    shear.TryCrop(Stock(world));

    Stopped(world, pos);
    Advance(shear, seconds: 5f);

    // Held, not lost: nothing has dropped and the piece is still under the blades, so the stroke
    // resumes when the run does.
    Assert.True(shear.IsStroking);
    Assert.Empty(world.Drops);
  }

  [Fact]
  public void A_wrench_frees_a_held_piece_with_its_tally_untouched() {
    (TestWorld world, BlockEntityShear shear, BlockPos pos) = Shear(Registry());
    shear.TryFitBlades(Blades(world, 1), out _);
    Turning(world, pos);
    shear.TryCrop(Stock(world, cropped: 1));

    ItemStack? freed = shear.ReleaseStuckPiece();

    Assert.NotNull(freed);
    Assert.False(shear.IsStroking);
    // The crop was never committed, so the piece comes back on the tally it went in with.
    Assert.Equal(1, WorkPiece.FromStack(freed)!.Cropped);
  }

  #endregion

  #region Drive

  [Fact]
  public void Drive_torque_is_recovered_from_the_runs_supply_power() {
    (TestWorld world, BlockEntityShear shear, BlockPos pos) = Shear(
      Registry(minTorque: 0.5f)
    );
    shear.TryFitBlades(Blades(world, 1), out _);

    // The network publishes power, not torque; the shear divides by omega to get back to it. Set a run
    // whose supply is 1.0 N.m at omega 2 and the stroke's 0.5 N.m is carried.
    Turning(world, pos, speed: 2f, supplyPower: 2f);
    Assert.True(shear.TryCrop(Stock(world)).Accepted);

    // Halve the supply at the same speed and the same stroke is refused - which is what proves the
    // division is being done rather than the power being compared directly.
    (TestWorld weak, BlockEntityShear starved, BlockPos weakPos) = Shear(
      Registry(minTorque: 0.5f)
    );
    starved.TryFitBlades(Blades(weak, 1), out _);
    Turning(weak, weakPos, speed: 2f, supplyPower: 0.5f);

    Assert.Equal(
      ShearVerdict.NotEnoughDrive,
      starved.TryCrop(Stock(weak)).Verdict
    );
  }

  [Fact]
  public void A_shear_at_rest_refuses_before_it_asks_about_drive() {
    (TestWorld world, BlockEntityShear shear, BlockPos pos) = Shear(Registry());
    shear.TryFitBlades(Blades(world, 1), out _);
    Stopped(world, pos);

    // NotTurning rather than NotEnoughDrive: no amount of stored energy starts a stroke, and the
    // refusals are ordered by what the player can do about them.
    Assert.Equal(ShearVerdict.NotTurning, shear.TryCrop(Stock(world)).Verdict);
  }

  #endregion

  #region Teardown

  [Fact]
  public void Breaking_the_shear_gives_back_the_blades_and_any_held_piece() {
    (TestWorld world, BlockEntityShear shear, BlockPos pos) = Shear(Registry());
    shear.TryFitBlades(Blades(world, 1), out _);
    Turning(world, pos);
    shear.TryCrop(Stock(world));

    shear.OnBlockBroken();

    Assert.Contains(world.Drops, d => MachineTool.TierOf(d) == 1);
    Assert.Contains(
      world.Drops,
      d => d.Collectible.Code.ToShortString() == StockCode
    );
  }

  #endregion

  #region Helpers

  private static void Turning(
    TestWorld world,
    BlockPos pos,
    float speed = 1f,
    float supplyPower = 100f
  ) {
    var network = (MpEnergyNetwork)world.NetworkAt(pos)!;
    network.RestoreState(
      new MpEnergyNetworkState {
        Speed = speed,
        Inertia = 10f,
        SupplyPower = supplyPower,
      }
    );
  }

  private static void Stopped(TestWorld world, BlockPos pos) =>
    Turning(world, pos, speed: 0f, supplyPower: 0f);

  // Drives the stroke in the 250 ms steps the production clock uses, so a test walks the same path the
  // machine does rather than jumping the whole duration in one call.
  private static void Advance(BlockEntityShear shear, float seconds) {
    for (float t = 0; t < seconds; t += 0.25f)
      shear.AdvanceStroke(0.25f);
  }

  #endregion
}
