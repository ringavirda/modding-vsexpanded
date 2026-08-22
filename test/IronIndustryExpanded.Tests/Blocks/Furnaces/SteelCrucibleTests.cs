using System.Linq;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkMolten.Blocks;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.Blocks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The steel crucible: a fireclay pot that holds a heat at 1600 °C and dies of it after three. What is
/// pinned here is the contract with vanilla's own crucible chassis, because almost every way of getting it
/// wrong fails silently or on a server tick rather than at load.
/// </summary>
public class SteelCrucibleTests {
  private static ExBlockDefShim Def =>
    new(BlockSteelCrucible.Definitions("iiex").Single().ToJson());

  /// <summary>A thin reader over the emitted blocktype, so the cases below read as claims about the
  /// shipped JSON rather than as JObject navigation.</summary>
  private sealed record ExBlockDefShim(JToken Json) {
    public JToken? At(string path) => Json.SelectToken(path);

    public string? Str(string path) => (string?)At(path);
  }

  #region The vanilla chassis contract

  /// <summary>
  /// Three variants under a group named exactly <c>type</c>, with a state named exactly <c>smelted</c>.
  /// <c>BlockSmeltingContainer.DoSmelt</c> hard-codes <c>CodeWithVariant("type", "smelted")</c>, so both
  /// names are a contract with vanilla rather than a naming choice: getting either wrong resolves to a
  /// null block inside a firepit tick.
  /// </summary>
  [Fact]
  public void The_variant_group_and_states_are_the_ones_vanilla_hard_codes() {
    Assert.Equal("type", Def.Str("variantgroups[0].code"));
    Assert.Equal(
      new[] { "raw", "burned", "smelted" },
      Def.At("variantgroups[0].states")!.Select(s => (string)s!).ToArray()
    );
  }

  /// <summary>
  /// The smelted variant's class must be ours and must derive from <see cref="BlockSmeltedContainer"/>:
  /// <c>DoSmelt</c> casts the block it resolves to that type <c>without checking</c>, so a sibling class
  /// is an <c>InvalidCastException</c> on the server, mid-tick, rather than a load error.
  /// </summary>
  [Fact]
  public void The_smelted_class_is_ours_and_satisfies_vanillas_unchecked_cast() {
    Assert.True(
      typeof(BlockSmeltedContainer).IsAssignableFrom(
        typeof(BlockSteelCruciblePour)
      ),
      "DoSmelt casts the smelted block to BlockSmeltedContainer without checking"
    );
    Assert.Contains(
      nameof(BlockSteelCruciblePour),
      Def.Str("classByType.*-smelted")
    );
  }

  /// <summary>
  /// The burned variant names vanilla's smelting container by its bare registered key. A
  /// <c>Class&lt;T&gt;()</c> naming a Vintagestory type compiles and emits a domain-qualified key nobody
  /// registered, and nothing in the suite checks that a class string resolves - it would simply arrive as
  /// a plain block in game.
  /// </summary>
  [Fact]
  public void The_burned_class_is_vanillas_bare_registered_name() {
    Assert.Equal("BlockSmeltingContainer", Def.Str("classByType.*-burned"));
    Assert.Equal("SmeltedContainer", Def.Str("entityClassByType.*-smelted"));
  }

  /// <summary>
  /// The smelted pot declares where it goes back to when the metal has gone. Vanilla resolves this with
  /// <c>AssetLocation.Create(code, Code.Domain)</c>, which throws on a null - so omitting it does not
  /// disable the pour, it crashes it.
  /// </summary>
  [Fact]
  public void The_smelted_pot_names_the_pot_it_empties_into() {
    Assert.Equal(
      BlockSteelCrucible.BurnedCode,
      Def.Str("attributes.emptiedBlockCodeByType.*-smelted")
    );
  }

  /// <summary>
  /// The raw pot fires in a pit kiln into the burned one, and that is the only route to a pot at all: a
  /// clayforming surface can output nothing but a <c>-raw</c> variant.
  /// </summary>
  [Fact]
  public void The_raw_pot_fires_into_the_burned_one() {
    Assert.Equal("fire", Def.Str("combustibleProps.*-raw.smeltingType"));
    // Domain-qualified, and that is not decoration: a `smeltedStack` code is read by the ordinary stack
    // resolver, which defaults an unqualified path to `game:` and would find nothing. `emptiedBlockCode`
    // below is the opposite - vanilla resolves that one against the block's own domain, so it must stay
    // bare. The same string, read two different ways by two different readers.
    Assert.Equal(
      $"iiex:{BlockSteelCrucible.BurnedCode}",
      Def.Str("combustibleProps.*-raw.smeltedStack.code")
    );
    Assert.DoesNotContain(
      ":",
      Def.Str("attributes.emptiedBlockCodeByType.*-smelted")!
    );
  }

  /// <summary>
  /// The burned pot carries no combustible properties, and the absence is load-bearing: a firepit refuses
  /// an input that declares them, so a burned pot with any would stop working as a crucible. The smelted
  /// pot's are deliberately unreachable, or a player could bake the metal out and skip the pour.
  /// </summary>
  [Fact]
  public void The_burned_pot_is_not_itself_smeltable() {
    Assert.Null(Def.At("combustibleProps.*-burned"));
    Assert.Equal(2400, (int)Def.At("combustibleProps.*-smelted.meltingPoint")!);
  }

  /// <summary>
  /// One pot per stack. The firing count lives on the itemstack, so two pots of different ages merging
  /// would average away every heat already spent - the pot would become effectively immortal by being
  /// stacked.
  /// </summary>
  [Fact]
  public void A_pot_never_stacks() {
    Assert.Equal(1, (int)Def.At("maxstacksize")!);
  }

  #endregion

  #region The clay gate

  /// <summary>
  /// The pot is exempt from the clay heat ceiling, and it is exempt by TYPE: the gate asks
  /// whether a block is a <c>BlockToolMold</c> and a pot never is, so the temperature is not even
  /// compared. Pinned because a future tidy of <c>MoldKinds.FitsPedestal</c> into something
  /// material-shaped would silently start shattering pots.
  /// </summary>
  [Fact]
  public void The_pot_is_not_judged_by_the_clay_heat_ceiling() {
    Block pot = TestBlocks.Configure(
      new BlockSteelCrucible(),
      "iiex:steelcrucible-burned",
      910
    );

    Assert.False(MoldKinds.FitsPedestal(pot));
    Assert.False(ClayHeatGate.WouldShatter(pot, 1600f));
    // And well past it, so the case cannot pass by the ceiling happening to be high.
    Assert.False(ClayHeatGate.WouldShatter(pot, 5000f));
  }

  #endregion

  #region The three heats

  /// <summary>A new pot has spent nothing.</summary>
  [Fact]
  public void A_fresh_pot_has_no_firings_on_it() {
    Assert.Equal(0, CrucibleFiring.Of(null));
    Assert.False(CrucibleFiring.IsSpent(0));
  }

  /// <summary>
  /// Three heats and it is finished - the configured life, read rather than hardcoded, since a player can
  /// retune it in <c>ex_values.json</c>.
  /// </summary>
  [Fact]
  public void A_pot_is_spent_at_the_configured_number_of_firings() {
    int life = IiexValues.CruciblePotFirings;

    Assert.False(CrucibleFiring.IsSpent(life - 1));
    Assert.True(CrucibleFiring.IsSpent(life));
    // Past it too: a pot that somehow got an extra heat is still finished, not wrapped around.
    Assert.True(CrucibleFiring.IsSpent(life + 1));
  }

  /// <summary>
  /// The count survives a round trip through a stack, which is the whole reason it lives there: every
  /// point where vanilla moves a pot between its variants builds a brand-new stack, so the age has to be
  /// re-stamped rather than carried.
  /// </summary>
  [Fact]
  public void The_count_rides_the_stack_it_is_stamped_on() {
    var world = new TestWorld();
    Block pot = TestBlocks.Configure(
      new BlockSteelCrucible(),
      "iiex:steelcrucible-burned",
      910
    );
    var stack = new ItemStack(pot);

    CrucibleFiring.Set(stack, 2);

    Assert.Equal(2, CrucibleFiring.Of(stack));
    Assert.Equal(2, CrucibleFiring.Of(stack.Clone()));
    // A separate pot is a separate age; nothing is stored on the block.
    Assert.Equal(0, CrucibleFiring.Of(new ItemStack(pot)));
    Assert.NotNull(world);
  }

  /// <summary>A negative count is not expressible - it would read as a pot with heats in hand.</summary>
  [Fact]
  public void A_count_never_goes_below_zero() {
    Block pot = TestBlocks.Configure(
      new BlockSteelCrucible(),
      "iiex:steelcrucible-burned",
      910
    );
    var stack = new ItemStack(pot);

    CrucibleFiring.Set(stack, -4);

    Assert.Equal(0, CrucibleFiring.Of(stack));
  }

  #endregion
}
