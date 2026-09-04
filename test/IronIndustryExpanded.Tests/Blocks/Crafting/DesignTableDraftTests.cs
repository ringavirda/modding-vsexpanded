using IronIndustryExpanded.BlockStructures.Crafting.BlockEntities;
using Vintagestory.API.Common;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The design table's draft-input predicates: what counts as a drawing medium (charcoal or any coal) and
/// as parchment (vanilla's <c>paper</c>). The inventory-mutating <c>TryDraft</c> is covered by
/// <see cref="DesignTableBeTests"/>.
/// </summary>
public class DesignTableDraftTests {
  private static ItemStack Stack(string domain, string path) =>
    new(new Item { Code = new AssetLocation(domain, path) });

  #region Drawing medium

  [Theory]
  [InlineData("game", "charcoal", true)]
  [InlineData("game", "coal", true)]
  [InlineData("game", "coal-bituminous", true)] // any coal-* variant
  [InlineData("game", "paper", false)]
  [InlineData("game", "stick", false)]
  public void Charcoal_and_any_coal_are_drawing_media(
    string domain,
    string path,
    bool expected
  ) =>
    Assert.Equal(
      expected,
      BlockEntityDesignTable.IsDrawingMedium(Stack(domain, path))
    );

  #endregion

  #region Parchment

  [Theory]
  [InlineData("game", "paper", true)]
  [InlineData("game", "charcoal", false)]
  [InlineData("game", "papyrus", false)]
  public void Only_vanilla_paper_counts_as_parchment(
    string domain,
    string path,
    bool expected
  ) =>
    Assert.Equal(
      expected,
      BlockEntityDesignTable.IsParchment(Stack(domain, path))
    );

  #endregion

  [Fact]
  public void A_missing_stack_is_neither_medium_nor_parchment() {
    Assert.False(BlockEntityDesignTable.IsDrawingMedium(null));
    Assert.False(BlockEntityDesignTable.IsParchment(null));
  }
}
