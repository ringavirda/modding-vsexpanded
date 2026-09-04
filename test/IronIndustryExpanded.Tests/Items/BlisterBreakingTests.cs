using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using IronIndustryExpanded.Items;
using IronIndustryExpanded.Recipes.Smithing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Crushing a cold blister-steel ingot: the mass arithmetic, the anvil geometry it depends on, and the
/// fork that keeps the hot route vanilla's. A 100 u ingot fills 40 voxels at 2.5 u each and comes back as
/// three shed chunks plus the five bits the finished shape hands over - 100 u exactly, which is what the
/// crucible furnace is priced against.
/// </summary>
public class BlisterBreakingTests {
  #region The density rule

  [Fact]
  public void An_ingot_is_worth_exactly_two_and_a_half_units_per_voxel() {
    Assert.Equal(2.5f, BlisterBreaking.UnitsPerVoxel, 4);
  }

  // Asserting the product ties the voxel count to the unit mass; a ratio alone would still pass if both
  // constants drifted together.
  [Fact]
  public void The_voxel_count_and_the_mass_move_together() {
    Assert.Equal(
      BlisterBreaking.IngotUnits,
      BlisterBreaking.IngotVoxels * BlisterBreaking.UnitsPerVoxel,
      4
    );
  }

  /// <summary>
  /// The bit is not this mod's choice of denomination, it is vanilla's: <c>metalbit</c> smelts twenty to
  /// an ingot. Pinned because the recipe pays out in vanilla bits, so a 5 u figure that stopped matching
  /// would quietly mint or destroy metal at the boundary.
  /// </summary>
  [Fact]
  public void The_bit_is_vanillas_own_twentieth_of_an_ingot() {
    Assert.Equal(BlisterBreaking.IngotUnits, BlisterBreaking.BitUnits * 20);
    Assert.Equal(
      BlisterBreaking.ChunkUnits,
      BlisterBreaking.BitUnits * BitsPerChunk
    );
  }

  #endregion

  #region Denomination

  [Theory]
  [InlineData(40, 4)] // the whole ingot at once -> four chunks (100u)
  [InlineData(30, 3)] // the shed voxels -> three chunks (75u)
  [InlineData(10, 1)] // the recipe leftover's worth -> one chunk (25u)
  [InlineData(9, 0)] // 22.5u -> nothing yet (carried)
  public void Emit_pays_out_chunks_for_the_voxels_removed(
    int voxelsRemoved,
    int chunks
  ) {
    float remainder = 0f;
    Assert.Equal(chunks, BlisterBreaking.Emit(voxelsRemoved, ref remainder));
  }

  [Fact]
  public void The_sub_chunk_remainder_carries_between_hits() {
    float remainder = 0f;

    // Nine single-voxel hits accrue 22.5u and pay nothing; the tenth crosses a chunk.
    for (int i = 0; i < 9; i++)
      Assert.Equal(0, BlisterBreaking.Emit(1, ref remainder));
    Assert.Equal(1, BlisterBreaking.Emit(1, ref remainder));
    Assert.Equal(0f, remainder, 4);
  }

  /// <summary>
  /// The whole crush, one helve hit at a time, exactly as the anvil delivers it: thirty single-voxel hits
  /// shed the block down to the recipe shape, and the shape itself is handed back as bits. Three chunks
  /// and five bits, and not one unit of the ingot left behind.
  /// </summary>
  [Fact]
  public void Crushing_a_whole_ingot_one_voxel_at_a_time_conserves_the_mass() {
    float remainder = 0f;
    int chunks = 0;
    for (int i = 0; i < BlisterBreaking.ShedVoxels; i++)
      chunks += BlisterBreaking.Emit(1, ref remainder);

    Assert.Equal(3, chunks);
    Assert.Equal(0f, remainder, 4);

    int paid =
      chunks * BlisterBreaking.ChunkUnits
      + BitsPerChunk * BlisterBreaking.BitUnits;
    Assert.Equal(BlisterBreaking.IngotUnits, paid);
  }

  /// <summary>
  /// The hit that finishes the shape sheds one voxel like any other. Vanilla completes the recipe from
  /// inside the hit and blanks the anvil, so reading what is left there says the whole block was shed -
  /// which would pay a third of the ingot out twice, once as chunks and once as the recipe's own bits.
  /// Skipping the hit instead, as the pig chain does, strands the run's last chunk.
  /// </summary>
  [Fact]
  public void The_hit_that_finishes_the_shape_sheds_one_voxel_like_any_other() {
    const int shape = BlisterBreaking.ShapeVoxels;

    // A hit partway through: one voxel gone off a block still standing.
    Assert.Equal(1, BlisterBreaking.Shed(40, 39, shape, finished: false));

    // The last hit: eleven voxels stood, the anvil now reads empty, and ten of them left as the output.
    Assert.Equal(1, BlisterBreaking.Shed(shape + 1, 0, shape, finished: true));
  }

  #endregion

  #region The anvil geometry

  /// <summary>
  /// The crushed block sits on the recipe, not beside it. A smithing pattern is centred on the anvil and
  /// transposed as it is laid out, so a block placed where the pattern reads misses it - and the miss is
  /// silent, because the helve conjures metal to fill whatever the recipe wants and is not there. That
  /// would mint steel out of nothing on every ingot.
  /// </summary>
  [Fact]
  public void The_crushed_block_covers_the_recipe_shape_exactly() {
    bool[,,] recipe = RecipeVoxels(RecipeOf("blister"));
    byte[,,] block = null!;
    BlisterBreaking.CreateVoxels(ref block);

    Assert.Equal(BlisterBreaking.IngotVoxels, Occupied(block).Count);
    Assert.Equal(BlisterBreaking.ShapeVoxels, Occupied(recipe).Count);
    Assert.Empty(Uncovered(recipe, block));
    Assert.Equal(
      BlisterBreaking.ShedVoxels,
      Occupied(block).Count - Occupied(recipe).Count
    );
  }

  /// <summary>
  /// The same claim for every smithing chain the mod ships, because the trap is the layout rule rather
  /// than any one recipe: whatever a chain lays on the anvil has to cover the shape it is hammered
  /// towards. The pig and the shingling pile lay theirs through private methods, so this reaches them by
  /// reflection rather than duplicating their arithmetic and agreeing with itself.
  /// </summary>
  [Theory]
  [InlineData("pig")]
  [InlineData("shingle")]
  public void Every_chain_lays_its_metal_over_the_shape_it_is_worked_to(
    string chain
  ) {
    bool[,,] recipe = RecipeVoxels(RecipeOf(chain));
    byte[,,] laid = LaidBy(chain);

    Assert.NotEmpty(Occupied(recipe));
    Assert.Empty(Uncovered(recipe, laid));
  }

  #endregion

  #region The cold fork

  /// <summary>
  /// A hot ingot never reaches the crushing route. The gate is not a temperature of this mod's own: it is
  /// whether vanilla's own placement took the ingot, which it does exactly when the metal is workable. If
  /// this ever passed for an accepted ingot, the mod would be crushing steel vanilla was about to forge
  /// into shear steel.
  /// </summary>
  [Theory]
  [InlineData(true, false, false)] // vanilla took it: the hot route, untouched
  [InlineData(false, true, false)] // an anvil already in use
  [InlineData(false, false, true)] // cold, free anvil: ours
  public void Only_an_ingot_vanilla_refused_is_crushed(
    bool vanillaAccepted,
    bool anvilOccupied,
    bool crushes
  ) {
    Assert.Equal(
      crushes,
      BlisterBreaking.Crushes(
        Ingot(BlisterBreaking.IngotCode),
        vanillaAccepted,
        anvilOccupied
      )
    );
  }

  [Fact]
  public void No_other_ingot_is_crushed() {
    Assert.False(
      BlisterBreaking.Crushes(Ingot("game:ingot-iron"), false, false)
    );
    Assert.False(BlisterBreaking.Crushes(null, false, false));
  }

  /// <summary>
  /// Exactly one route is ever on offer. Leaving both would put the shear-steel recipe in front of a cold
  /// ingot on this mod's own cold-workable work item, and the helve would run it - deleting vanilla's
  /// requirement that blister steel be forged hot, the thing the fork exists to preserve.
  /// </summary>
  [Theory]
  [InlineData(true, false, true)] // cold ingot, crushing recipe: kept
  [InlineData(false, false, false)] // cold ingot, vanilla's recipe: dropped
  [InlineData(true, true, false)] // hot ingot, crushing recipe: dropped
  [InlineData(false, true, true)] // hot ingot, vanilla's recipe: kept
  public void The_two_routes_are_never_offered_together(
    bool crushingRecipe,
    bool vanillaWouldWork,
    bool offered
  ) {
    Assert.Equal(
      offered,
      BlisterBreaking.Offers(crushingRecipe, vanillaWouldWork)
    );
  }

  #endregion

  #region The recipe contract

  /// <summary>
  /// The recipe hands back the leftover shape as five vanilla bits, which is the other half of the payout
  /// and the reason the totals come out whole.
  /// </summary>
  [Fact]
  public void The_finished_shape_is_worth_five_vanilla_bits() {
    JToken body = RecipeOf("blister").ToJson();

    Assert.Equal(BlisterBreaking.BitCode, (string?)body["output"]!["code"]);
    Assert.Equal(BitsPerChunk, (int)body["output"]!["quantity"]!);
    Assert.Equal(
      BlisterBreaking.IngotCode,
      (string?)body["ingredient"]!["code"]
    );
  }

  /// <summary>
  /// The recipe's name is the string the fork is read off, and it may not be one of the two vanilla itself
  /// grants helve workability by: a recipe called <c>blistersteel</c> or <c>plate</c> would make every
  /// vanilla work item follow this one too.
  /// </summary>
  [Fact]
  public void The_recipe_is_named_neither_of_vanillas_two_helve_recipes() {
    JToken body = RecipeOf("blister").ToJson();

    Assert.Equal(BlisterBreaking.RecipeName, (string?)body["name"]);
    Assert.NotEqual("blistersteel", BlisterBreaking.RecipeName);
    Assert.NotEqual("plate", BlisterBreaking.RecipeName);
  }

  /// <summary>
  /// The chain's own identifiers, distinct from the pig's. The stack marker is what keeps the helve patch
  /// off every other work item, and a shared one would make each chain pay the other out.
  /// </summary>
  [Fact]
  public void The_chain_is_keyed_apart_from_the_pig_chain() {
    Assert.NotEqual(PigBreaking.MarkerKey, BlisterBreaking.MarkerKey);
    Assert.NotEqual(PigBreaking.WorkItemCode, BlisterBreaking.WorkItemCode);
    Assert.NotEqual(
      (string?)RecipeOf("pig").ToJson()["code"],
      (string?)RecipeOf("blister").ToJson()["code"]
    );
  }

  #endregion

  #region Fixture

  private const int BitsPerChunk = 5;

  private static ExRecipeDef RecipeOf(string chain) =>
    chain switch {
      "blister" => BlisterRecipeDefinitions.Definitions("iiex").Single(),
      "pig" => PigRecipeDefinitions.Definitions("iiex").Single(),
      "shingle" => ShinglingRecipeDefinitions.Definitions("iiex").Single(),
      _ => throw new ArgumentOutOfRangeException(nameof(chain)),
    };

  // Vanilla's own layout pass, run on the shipped pattern: centring and transposition included, so a case
  // cannot pass by agreeing with an arithmetic copy of it that drifted the same way.
  private static bool[,,] RecipeVoxels(ExRecipeDef def) {
    var recipe = new SmithingRecipe {
      Pattern = def.ToJson()["pattern"]!.ToObject<string[][]>()!,
    };
    recipe.GenVoxels();
    return recipe.Voxels;
  }

  private static byte[,,] LaidBy(string chain) =>
    chain switch {
      "pig" => Lay(typeof(ItemPig), "CreatePigVoxels", null),
      // Two balls, because the bar is a two-layer pile and one ball lays one layer.
      "shingle" => Lay(
        typeof(ItemPuddledBall),
        "PileLayer",
        Lay(typeof(ItemPuddledBall), "PileLayer", null, true),
        false
      ),
      _ => throw new ArgumentOutOfRangeException(nameof(chain)),
    };

  // Each layout method takes its grid as `ref byte[,,]`, which reflection writes back into the argument
  // array.
  private static byte[,,] Lay(
    Type type,
    string method,
    byte[,,]? voxels,
    params object?[] rest
  ) {
    MethodInfo mi = type.GetMethod(
      method,
      BindingFlags.NonPublic | BindingFlags.Static
    )!;
    object?[] args = [voxels, .. rest];
    mi.Invoke(null, args);
    return (byte[,,])args[0]!;
  }

  private static ItemStack? Ingot(string? code) =>
    code == null
      ? null
      : new ItemStack(new Item { Code = new AssetLocation(code) });

  private static List<(int X, int Y, int Z)> Occupied(byte[,,] voxels) {
    var cells = new List<(int, int, int)>();
    for (int x = 0; x < voxels.GetLength(0); x++)
      for (int y = 0; y < voxels.GetLength(1); y++)
        for (int z = 0; z < voxels.GetLength(2); z++)
          if (voxels[x, y, z] != 0)
            cells.Add((x, y, z));
    return cells;
  }

  private static List<(int X, int Y, int Z)> Occupied(bool[,,] voxels) {
    var cells = new List<(int, int, int)>();
    for (int x = 0; x < voxels.GetLength(0); x++)
      for (int y = 0; y < voxels.GetLength(1); y++)
        for (int z = 0; z < voxels.GetLength(2); z++)
          if (voxels[x, y, z])
            cells.Add((x, y, z));
    return cells;
  }

  // Recipe cells the laid metal does not reach: every one is a voxel the helve would conjure.
  private static List<(int X, int Y, int Z)> Uncovered(
    bool[,,] recipe,
    byte[,,] laid
  ) => [.. Occupied(recipe).Where(c => laid[c.X, c.Y, c.Z] == 0)];

  #endregion
}
