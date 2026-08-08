using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockNetworkMolten.Blocks;
using IronworkingExpanded.BlockStructures.Casting.Blocks;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The ceramic-tier heat ceiling (<see cref="ClayHeatGate.WouldShatter"/>): a small fired-clay tool
/// mold shatters when metal above <see cref="IwexValues.ClayMoldHeatCeiling"/> is poured into it. Large
/// tool molds (cast at iron temperatures in the tap) and iwex's cast-iron molds are exempt.
/// </summary>
public class ClayHeatGateTests {
  private static Block SmallClayMold() =>
    TestBlocks.Configure(
      new BlockToolMold(),
      "game:toolmold-ingot",
      2001,
      ("tooltype", "ingot")
    );

  private static Block LargeClayMold() =>
    TestBlocks.Configure(
      new BlockToolMold(),
      "game:toolmold-anvil",
      2002,
      ("tooltype", "anvil")
    );

  private static Block CastIronMold() =>
    TestBlocks.Configure(
      new BlockCastMold(),
      "iwex:casting-mold-ingot",
      2003,
      ("tooltype", "plate")
    );

  private static float Ceiling => IwexValues.ClayMoldHeatCeiling;

  #region Small clay molds are gated

  [Fact]
  public void An_iron_hot_pour_shatters_a_small_clay_mold() {
    Assert.True(ClayHeatGate.WouldShatter(SmallClayMold(), 1400f));
  }

  [Fact]
  public void A_bronze_pour_leaves_a_small_clay_mold_intact() {
    // Copper (1084) is the hottest vanilla casting metal and still sits below the ceiling.
    Assert.False(ClayHeatGate.WouldShatter(SmallClayMold(), 1084f));
  }

  [Fact]
  public void Metal_exactly_at_the_ceiling_still_casts() {
    // Strict comparison: at the ceiling the clay holds, one degree over it cracks.
    Assert.False(ClayHeatGate.WouldShatter(SmallClayMold(), Ceiling));
    Assert.True(ClayHeatGate.WouldShatter(SmallClayMold(), Ceiling + 1f));
  }

  #endregion

  #region Exemptions

  [Fact]
  public void A_large_tool_mold_is_exempt_even_when_glowing_hot() {
    // Anvil and helve-hammer molds are cast from molten iron in the canal tap, so gating them would
    // make anvil casting impossible.
    Assert.False(ClayHeatGate.WouldShatter(LargeClayMold(), 1500f));
  }

  [Fact]
  public void Our_own_cast_iron_mold_never_shatters() {
    Assert.False(ClayHeatGate.WouldShatter(CastIronMold(), 1500f));
  }

  [Fact]
  public void A_non_mold_block_and_null_are_ignored() {
    Assert.False(ClayHeatGate.WouldShatter(new Block(), 1500f));
    Assert.False(ClayHeatGate.WouldShatter(null, 1500f));
  }

  #endregion
}
