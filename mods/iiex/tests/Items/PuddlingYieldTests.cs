using System;
using System.Linq;
using System.Reflection;
using ExpandedLib.Registries.Entities;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Forming;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using IronIndustryExpanded.Items;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// What one heat is worth, and that nothing is minted or lost on the way through. Every case is stated as
/// arithmetic rather than as the number it currently comes to: the pig has already been re-massed once
/// (150 to 375) and a literal <c>Assert.Equal(16, ...)</c> would have passed at either, saying nothing.
/// </summary>
public class PuddlingYieldTests {
  #region Harness

  private static BlockEntityPuddlingHearth FullMeltedBed() {
    var world = new TestWorld();
    var bed = new BlockEntityPuddlingHearth {
      Pos = new BlockPos(0, 16, 0),
      Block = TestBlocks.Configure(
        new BlockPuddlingHearth(),
        "iiex:furnace-puddlinghearth-n",
        1,
        ("type", "puddlinghearth"),
        ("side", "north")
      ),
    };
    world.Place(bed.Pos, bed.Block, bed);
    world.Attach(bed);

    foreach (
      HearthRows.Row row in new[]
      {
        HearthRows.Row.Left,
        HearthRows.Row.Right,
        HearthRows.Row.Centre,
      }
    ) {
      bed.TryFettle(row);
      for (int i = 0; i < PuddlingHearthLayout.PigsPerRow; i++)
        bed.TryChargePig(row);
    }
    bed.MeltDown(1f);
    return bed;
  }

  #endregion

  #region The heat balances

  /// <summary>
  /// The whole yield is one integer division: the bath divides into whole balls and the remainder stays
  /// behind as tap cinder. Nothing may vanish between the charge going in and the metal coming out.
  /// </summary>
  [Fact]
  public void Balls_plus_the_cinder_remainder_are_the_charge_that_went_in() {
    BlockEntityPuddlingHearth bed = FullMeltedBed();

    int charged = PuddlingHearthLayout.PigCapacity * ItemPig.PigUnits;
    Assert.Equal(charged, bed.BathUnits);
    Assert.Equal(
      charged,
      bed.BallsRemaining * WroughtBallItemDefinitions.BallUnits
        + bed.CinderUnits
    );
  }

  /// <summary>
  /// The remainder is genuinely a remainder - smaller than one ball. A cinder figure of a ball or more
  /// would mean a whole ball's metal was being raked out as waste.
  /// </summary>
  [Fact]
  public void The_cinder_remainder_is_less_than_one_ball() {
    BlockEntityPuddlingHearth bed = FullMeltedBed();

    Assert.InRange(
      bed.CinderUnits,
      0,
      WroughtBallItemDefinitions.BallUnits - 1
    );
  }

  /// <summary>
  /// A heat is worth gathering: a bath that divided into no balls at all would make the rabble useless
  /// and the whole charge into cinder.
  /// </summary>
  [Fact]
  public void A_full_heat_yields_balls_to_gather() {
    BlockEntityPuddlingHearth bed = FullMeltedBed();

    Assert.True(
      bed.BallsRemaining > 1,
      $"a full heat yields {bed.BallsRemaining} ball(s) - rabbling would not be a verb"
    );
  }

  /// <summary>
  /// Every gathered ball comes out of the same metal: gathering the bath dry must leave exactly the
  /// remainder behind, not a ball's worth more or less.
  /// </summary>
  [Fact]
  public void Gathering_the_bath_dry_leaves_only_the_remainder() {
    BlockEntityPuddlingHearth bed = FullMeltedBed();
    int expected = bed.BallsRemaining;

    int gathered = 0;
    while (bed.TryRabble())
      gathered++;

    Assert.Equal(expected, gathered);
    Assert.True(bed.IsWorkedOut);
    Assert.Equal(
      bed.CinderUnits,
      bed.BathUnits % WroughtBallItemDefinitions.BallUnits
    );
  }

  /// <summary>The clean-out closes the fettle loop exactly: one heat's cinder fettles the next heat's
  /// rows, so a running furnace never wants ore for its bed again.</summary>
  [Fact]
  public void One_heats_cinder_fettles_the_next_heats_rows() {
    Assert.Equal(HearthRows.All.Length, IiexValues.PuddlingCinderPerHeat);
  }

  #endregion

  #region Shingling conserves mass

  /// <summary>
  /// Two balls make a bar and six make a slab, with the metal conserved exactly. These are not free
  /// numbers: the helve accumulates whole balls, so a stock mass that is not a multiple of the ball's
  /// would mint or lose metal on every piece.
  /// </summary>
  [Theory]
  [InlineData("shingledbar", 2)]
  [InlineData("shingledslab", 6)]
  public void A_shingled_piece_weighs_exactly_the_balls_it_was_piled_from(
    string form,
    int balls
  ) {
    Assert.True(
      StockForm.All.ContainsKey(form),
      $"{form} is not a registered stock form"
    );

    Assert.Equal(
      balls * WroughtBallItemDefinitions.BallUnits,
      StockItemDefinitions.UnitsOf(form)
    );
  }

  /// <summary>
  /// The pile the balls lay and the shape the helve beats them into are the same voxel count, so
  /// <c>FullyWorkable</c> sheds nothing. A target smaller than the pile would quietly lose iron on every
  /// bar; larger, and the bar could never be finished.
  /// </summary>
  [Fact]
  public void The_pile_and_the_recipe_shape_are_the_same_voxel_count() {
    Assert.Equal(
      Shingling.BallsPerBar * Shingling.BallVoxels,
      Shingling.BarVoxels
    );
    Assert.Equal(
      Shingling.BarVoxels,
      Shingling.Layers * Shingling.Depth * Shingling.Width
    );

    JToken pattern = ShingleRecipe()["pattern"]!;
    int voxels = pattern
      .SelectMany(layer => layer!)
      .Sum(row => row!.Value<string>()!.Count(c => c == '#'));

    Assert.Equal(Shingling.BarVoxels, voxels);
  }

  /// <summary>
  /// A ball's voxels are its metal at the density the pig already sets, so the two items weigh the same
  /// per voxel and nothing is minted by moving metal between them.
  /// </summary>
  [Fact]
  public void A_balls_voxels_are_its_metal_at_the_mods_own_density() {
    Assert.Equal(
      PigBreaking.UnitsPerVoxel,
      WroughtBallItemDefinitions.BallUnits / (float)Shingling.BallVoxels,
      4
    );
  }

  /// <summary>
  /// The helve needs no dialog because the pile matches exactly one recipe. A second recipe taking the
  /// same ingredient would put a mode switch in front of the one machine whose whole point is not having
  /// one.
  /// </summary>
  [Fact]
  public void A_pile_of_balls_matches_exactly_one_smithing_recipe() {
    string ball = $"iiex:{WroughtBallItemDefinitions.Code}";
    var matching = DefinitionGoldens
      .Collect("iiex", Mod)
      .Where(d =>
        d.Location.Path.StartsWith(
          "recipes/smithing/",
          StringComparison.Ordinal
        )
      )
      .Select(d => d.ToJson())
      .Where(j =>
        j is JObject o && o["ingredient"]?["code"]?.Value<string>() == ball
      )
      .ToArray();

    Assert.Single(matching);
  }

  private static readonly Assembly Mod =
    typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

  private static JObject ShingleRecipe() =>
    (JObject)
      DefinitionGoldens
        .Collect("iiex", Mod)
        .Single(d => d.Location.Path == "recipes/smithing/shingle.json")
        .ToJson();

  #endregion

  #region Shingling under the helve

  /// <summary>
  /// The helve asks the standing work item, not the ball, whether it may work, and vanilla's work item
  /// answers NotWorkable for any recipe named neither plate nor blistersteel. The pile's own class
  /// answers FullyWorkable through the interface the anvil resolves it by, without consulting the
  /// recipe, which is why no anvil is needed here.
  /// </summary>
  [Fact]
  public void The_pile_is_fully_workable_under_the_helve() {
    var pile = new ItemShingleWorkItem {
      Code = new AssetLocation(Shingling.WorkItemCode),
    };
    IAnvilWorkable workable = pile;

    Assert.Equal(
      EnumHelveWorkableMode.FullyWorkable,
      workable.GetHelveWorkableMode(new ItemStack(pile), null!)
    );
  }

  /// <summary>
  /// The ball resolves the work item by <see cref="Shingling.WorkItemCode"/>, so the def's code and its
  /// one metal variant must spell that code, and the class it names must be the pile's own.
  /// </summary>
  [Fact]
  public void The_ball_names_the_work_item_the_def_emits() {
    var def = (JObject)
      DefinitionGoldens
        .Collect("iiex", Mod)
        .Single(d => d.Location.Path == "itemtypes/shingleworkitem.json")
        .ToJson();
    string code = def["code"]!.Value<string>()!;
    string[] states =
    [
      .. def["variantgroups"]![0]!["states"]!.Values<string>()!,
    ];

    Assert.Equal(Shingling.WorkItemCode, $"iiex:{code}-{Assert.Single(states)}");
    Assert.Equal(
      EntityRegistry.KeyFor("iiex", typeof(ItemShingleWorkItem)),
      def["class"]!.Value<string>()
    );
  }

  #endregion
}
