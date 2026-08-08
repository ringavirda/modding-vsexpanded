using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.Items;
using Vintagestory.API.Common;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The <b>code-string</b> half of the charge gates — <c>Burden.IsAnyCode</c> / <c>FamilyOfCode</c> and the
/// core's <c>IsChargeCode</c> / <c>AcceptsChargeCode</c>.
/// <para>
/// <b>Why a second form of a gate that already exists.</b> A <c>ChargeSegment</c> stores its material as an
/// asset-location <em>string</em>, never as an <c>ItemStack</c> — it holds units of a substance, which is the
/// whole reason a layered column can split a band without splitting a stack. Every gate the shaft applies to
/// charge therefore needs a code form, and the shaft asks them on every band it touches.
/// </para>
/// <para>
/// <b>The failure these exist to prevent is silence.</b> If the two forms drift, the same burden is accepted
/// through the pile route and refused through the column route (or the reverse) with nothing throwing, because
/// each answer is individually plausible. So every case below is asserted against <em>both</em> forms rather
/// than against a literal — a drift fails here even when both halves are self-consistent.
/// </para>
/// </summary>
public class ChargeCodeGateTests
{
  #region Harness

  private const string OreCode = "iwex:burden";
  private const string RemeltCode = "iwex:remeltburden";
  private const string Coke = "game:coke";

  /// <summary>What a cupola actually melts: metal, through the <c>scrap</c> material role.
  /// A code, not a hand-written literal in the production class - if the role grant in
  /// <c>materialroles.json</c> and its headless seed drift apart, this is one of the places it shows.</summary>
  private const string PigCode = "iwex:pigchunk";

  /// <summary>The shaft's <b>second</b> fuel. It is not decoration: every fuel assertion in this file
  /// named <see cref="Coke"/> and only <see cref="Coke"/>, so each of them passed identically against a
  /// furnace whose fuel test is the literal <c>"game:coke"</c> - see the loop in
  /// <c>Charge_identity_is_family_blind_and_acceptance_is_not</c> for what that costs a cupola.</summary>
  private const string Charcoal = "game:charcoal";

  /// <summary>A stack of <paramref name="code"/>, so the code form can be compared against the stack form
  /// that has shipped for months rather than against a hand-written expectation.</summary>
  private static ItemStack Stack(string code)
  {
    var loc = new AssetLocation(code);
    return new ItemStack(new Item { Code = loc, ItemId = 8100 }, 1);
  }

  private static BlockEntityBlastFurnaceCold Blast() => new();

  private static BlockEntityCupolaFurnace Cupola() => new();

  private static bool IsCharge(BlockEntityFurnaceCore be, string code) =>
    (bool)ReflectionHelpers.Invoke(be, "IsChargeCode", code)!;

  // `Accepts` / `AcceptsStack` reached `AcceptsChargeCode` / `AcceptsCharge`, both deleted with the
  // family layer they wrapped. Acceptance and identity are one question now, so the cases below
  // ask `IsChargeCode` and `IsChargeItem` directly - one seam, two routes, still asserted as a pair.

  #endregion

  #region Burden reads the same from a code as from a stack

  [Theory]
  [InlineData(OreCode, true)]
  // `iwex:remeltburden` is a retired code, kept here as a negative case rather than removed:
  // a retired code must read as "not burden" forever, and a row asserting that is how a resurrection by
  // copy-paste gets caught.
  [InlineData(RemeltCode, false)]
  [InlineData(Coke, false)]
  [InlineData("game:coalpile", false)]
  [InlineData("", false)]
  public void A_code_is_burden_exactly_when_a_stack_of_it_is(string code, bool expected)
  {
    Assert.Equal(expected, Burden.IsCode(code));
    // ...and the two forms agree. The literal above states what the answer is; this states that the two
    // routes cannot come apart, which is the invariant that actually matters.
    if (code.Length > 0)
      Assert.Equal(Burden.Is(Stack(code)), Burden.IsCode(code));
  }

  // `A_codes_family_matches_the_stack_forms` was retired with the family model, not edited. Its oracle
  // was `Burden.FamilyOfCode`: with one burden item the function had one
  // possible answer, so re-expressing the case would have asserted `"ore" == "ore"` for four inputs -
  // a tautology that still reads like coverage. What it protected (code form ≡ stack form) is asserted
  // above on the predicate that survived.

  [Fact]
  public void Null_is_not_burden_and_answers_rather_than_throws()
  {
    // The column hands over whatever a segment holds, and a defaulted segment holds null. It must answer
    // rather than throw — the alternative is a NullReferenceException on the tick path.
    Assert.False(Burden.IsCode(null));
    Assert.False(Burden.Is(null));
  }

  [Fact]
  public void Fuel_is_not_burden_which_is_how_the_raceway_is_recognised()
  {
    // The negation is load-bearing: TryIgniteCharge asks "is the lowest band not burden" to find the
    // coke under the column. If coke ever read as burden, a column of pure burden would light itself.
    Assert.False(Burden.IsCode(Coke));
    Assert.False(Burden.IsCode("game:charcoal"));
  }

  #endregion

  #region The furnace gate reads the same from a code as from a stack

  [Fact]
  public void A_blast_furnace_takes_burden_and_refuses_a_retired_code_through_both_routes()
  {
    // The old family gate (burden of both families is charge, only one accepted) went with the second
    // burden family, so acceptance and identity are now the same question, asked through two routes
    // that must not drift.
    BlockEntityBlastFurnaceCold be = Blast();

    Assert.True(IsCharge(be, OreCode));
    Assert.True(IsCharge(be, Coke)); // fuel is charge on a shaft - the two-stream split

    Assert.False(IsCharge(be, RemeltCode));
    Assert.False(IsCharge(be, "game:rock-granite"));

    // The pairing, not the values: the code route and the stack route must return the same answer for
    // the same material, or the same charge is taken through one and refused through the other.
    foreach (string code in new[] { OreCode, Coke, RemeltCode, "game:rock-granite" })
      Assert.Equal(be.IsChargeItem(Stack(code)), IsCharge(be, code));
  }

  [Fact]
  public void A_cupola_takes_scrap_and_refuses_burden_through_both_routes()
  {
    // The cupola takes no burden at all: the ore mixer - remelt burden's only source - is gone, so the
    // cupola charges metal directly and the mirror-image claim is about scrap against burden rather
    // than one burden family against the other.
    BlockEntityCupolaFurnace be = Cupola();

    Assert.True(IsCharge(be, PigCode));
    Assert.False(IsCharge(be, OreCode));
    Assert.False(IsCharge(be, RemeltCode)); // the family it used to eat is a retired code now

    // The pairing, not the values - the two routes must not come apart.
    foreach (string code in new[] { PigCode, OreCode, RemeltCode })
      Assert.Equal(be.IsChargeItem(Stack(code)), IsCharge(be, code));
  }

  [Fact]
  public void A_cupola_still_counts_its_coke_as_charge()
  {
    // The regression this exists for shipped for one build and 192 of 193 lpex tests stayed green over
    // it. The cupola's override was written as scrap-only, dropping the `|| IsFuelCode` half it inherits -
    // and `ReadChargeMix` skips any segment that is not charge, so the coke rounds stopped counting toward
    // the cupola's fullness, its carbon and its heat balance. Nothing threw; the furnace just ran on a
    // charge it could not see.
    BlockEntityCupolaFurnace be = Cupola();

    foreach (string fuel in new[] { Coke, Charcoal })
    {
      Assert.True(IsCharge(be, fuel));
      Assert.True(be.IsChargeItem(Stack(fuel)));
    }
    // ...and the metal it melts, through both routes, so this cannot pass on a furnace that takes only fuel.
    Assert.True(IsCharge(be, PigCode));
    Assert.True(be.IsChargeItem(Stack(PigCode)));
  }

  [Fact]
  public void Fuel_is_charge_on_a_shaft_and_burden_is_not_fuel()
  {
    // With one burden item there is no family layer left, so "family-blind" describes nothing. What
    // this case protects, and the reason it outlived that layer, is the two-stream split: a shaft
    // furnace burns fuel as its own bands rather than as a fraction stamped on the burden, so the hopper
    // has to be allowed to lay fuel and the column has to be allowed to hold it.
    foreach (BlockEntityFurnaceCore be in new BlockEntityFurnaceCore[] { Blast() })
    {
      Assert.True(IsCharge(be, OreCode));

      // Both fuels, deliberately - a single-fuel assertion here would leave `IsFuelCode` uncovered.
      // `IsFuelCode` is a `Roles.Fuel` lookup, so a regression to a string compare against
      // `game:coke` satisfies every other case in every suite: charcoal would simply stop being charge, the
      // hopper would refuse it, a charcoal column would read as burden of no composition, and a charcoal
      // furnace would neither light nor burn out. Two rows, one loop, and that whole class is shut.
      foreach (string fuel in new[] { Coke, Charcoal })
      {
        Assert.True(IsCharge(be, fuel));
        Assert.True(BlockEntityFurnaceCore.IsFuelCode(fuel));
        Assert.False(Burden.IsCode(fuel));
      }
    }

    // This case asserted `Assert.False(IsCharge(be, Coke))` until the cutover, and the inversion is
    // deliberate rather than a relaxation: before coke was chargeable it could only reach a shaft mixed
    // into the burden's stamp, which is the premixed charge `layered-charge.md` rejects - a premixed
    // burden loses the ventilation slits the coke layers are, and the furnace chokes.
  }

  #endregion
}
