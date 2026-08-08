using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The code-string half of the charge gates: <c>Burden.IsCode</c> and the core's <c>IsChargeCode</c>.
/// A <c>ChargeSegment</c> stores its material as an asset-location string, not an <c>ItemStack</c>, so every
/// charge gate needs a code form and the shaft asks it on every band it touches. Each case asserts the code
/// form against the stack form rather than against a literal, so the two routes cannot answer differently
/// for the same material.
/// </summary>
public class ChargeCodeGateTests {
  #region Harness

  private const string OreCode = "iwex:burden";
  private const string RemeltCode = "iwex:remeltburden";
  private const string Coke = "game:coke";

  /// <summary>What a cupola melts: metal, through the <c>scrap</c> material role. A code rather than a
  /// literal in the production class, so a drift between the role grant in <c>materialroles.json</c> and
  /// its headless seed shows up here.</summary>
  private const string PigCode = "iwex:pigchunk";

  /// <summary>The shaft's second fuel. Fuel assertions naming only <see cref="Coke"/> pass identically
  /// against a furnace whose fuel test is the literal <c>"game:coke"</c>, so both fuels are asserted.</summary>
  private const string Charcoal = "game:charcoal";

  /// <summary>A stack of <paramref name="code"/>, so the code form can be compared against the stack
  /// form rather than against a hand-written expectation.</summary>
  private static ItemStack Stack(string code) {
    var loc = new AssetLocation(code);
    return new ItemStack(new Item { Code = loc, ItemId = 8100 }, 1);
  }

  private static BlockEntityBlastFurnaceCold Blast() => new();

  private static BlockEntityCupolaFurnace Cupola() => new();

  private static bool IsCharge(BlockEntityFurnaceCore be, string code) =>
    (bool)ReflectionHelpers.Invoke(be, "IsChargeCode", code)!;

  // Acceptance and identity are the same question, so the cases below ask `IsChargeCode` and
  // `IsChargeItem` directly: one seam, two routes, asserted as a pair.

  #endregion

  #region Burden reads the same from a code as from a stack

  [Theory]
  [InlineData(OreCode, true)]
  // `iwex:remeltburden` is retired and must keep reading as "not burden".
  [InlineData(RemeltCode, false)]
  [InlineData(Coke, false)]
  [InlineData("game:coalpile", false)]
  [InlineData("", false)]
  public void A_code_is_burden_exactly_when_a_stack_of_it_is(
    string code,
    bool expected
  ) {
    Assert.Equal(expected, Burden.IsCode(code));
    // The literal above states the answer; this states that the two routes cannot come apart.
    if (code.Length > 0)
      Assert.Equal(Burden.Is(Stack(code)), Burden.IsCode(code));
  }

  [Fact]
  public void Null_is_not_burden_and_answers_rather_than_throws() {
    // The column hands over whatever a segment holds, and a defaulted segment holds null. The gate runs
    // on the tick path, so it must answer rather than throw.
    Assert.False(Burden.IsCode(null));
    Assert.False(Burden.Is(null));
  }

  [Fact]
  public void Fuel_is_not_burden_which_is_how_the_raceway_is_recognised() {
    // TryIgniteCharge finds the coke under the column by asking whether the lowest band is not burden.
    // If fuel read as burden, a column of pure burden would light itself.
    Assert.False(Burden.IsCode(Coke));
    Assert.False(Burden.IsCode("game:charcoal"));
  }

  #endregion

  #region The furnace gate reads the same from a code as from a stack

  [Fact]
  public void A_blast_furnace_takes_burden_and_refuses_a_retired_code_through_both_routes() {
    BlockEntityBlastFurnaceCold be = Blast();

    Assert.True(IsCharge(be, OreCode));
    Assert.True(IsCharge(be, Coke)); // fuel is charge on a shaft - the two-stream split

    Assert.False(IsCharge(be, RemeltCode));
    Assert.False(IsCharge(be, "game:rock-granite"));

    // The code route and the stack route must return the same answer for the same material.
    foreach (
      string code in new[] { OreCode, Coke, RemeltCode, "game:rock-granite" }
    )
      Assert.Equal(be.IsChargeItem(Stack(code)), IsCharge(be, code));
  }

  [Fact]
  public void A_cupola_takes_scrap_and_refuses_burden_through_both_routes() {
    // The cupola takes no burden at all: it charges metal directly.
    BlockEntityCupolaFurnace be = Cupola();

    Assert.True(IsCharge(be, PigCode));
    Assert.False(IsCharge(be, OreCode));
    Assert.False(IsCharge(be, RemeltCode)); // retired code

    // Both routes must return the same answer.
    foreach (string code in new[] { PigCode, OreCode, RemeltCode })
      Assert.Equal(be.IsChargeItem(Stack(code)), IsCharge(be, code));
  }

  [Fact]
  public void A_cupola_still_counts_its_coke_as_charge() {
    // The cupola's override must keep the inherited `|| IsFuelCode` half. `ReadChargeMix` skips any
    // segment that is not charge, so a scrap-only override drops the coke rounds from the cupola's
    // fullness, carbon and heat balance without throwing.
    BlockEntityCupolaFurnace be = Cupola();

    foreach (string fuel in new[] { Coke, Charcoal }) {
      Assert.True(IsCharge(be, fuel));
      Assert.True(be.IsChargeItem(Stack(fuel)));
    }
    // The metal it melts as well, so this cannot pass on a furnace that takes only fuel.
    Assert.True(IsCharge(be, PigCode));
    Assert.True(be.IsChargeItem(Stack(PigCode)));
  }

  [Fact]
  public void Fuel_is_charge_on_a_shaft_and_burden_is_not_fuel() {
    // The two-stream split: a shaft furnace burns fuel as its own bands rather than as a fraction
    // stamped on the burden, so the hopper must be allowed to lay fuel and the column to hold it.
    foreach (
      BlockEntityFurnaceCore be in new BlockEntityFurnaceCore[] { Blast() }
    ) {
      Assert.True(IsCharge(be, OreCode));

      // Both fuels: `IsFuelCode` is a `Roles.Fuel` lookup, and a regression to a string compare against
      // `game:coke` satisfies every single-fuel assertion in the suite while charcoal stops being charge.
      foreach (string fuel in new[] { Coke, Charcoal }) {
        Assert.True(IsCharge(be, fuel));
        Assert.True(BlockEntityFurnaceCore.IsFuelCode(fuel));
        Assert.False(Burden.IsCode(fuel));
      }
    }

    // Coke is chargeable so it reaches the shaft as its own layers: mixed into the burden's stamp it
    // loses the ventilation the coke bands provide. See docs/design/layered-charge.md.
  }

  #endregion
}
