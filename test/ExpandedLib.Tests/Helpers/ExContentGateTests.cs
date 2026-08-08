using System.Collections.Generic;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The generic content-gate mechanism a mod uses to disable registered content: hiding matching
/// blocks/items from the creative inventory and handbook.
/// </summary>
public class ExContentGateTests {
  private static Block Tabbed(string code) =>
    new() {
      Code = new AssetLocation(code),
      CreativeInventoryTabs = ["general"],
    };

  [Fact]
  public void Hiding_clears_creative_tabs_and_stacks_for_matches_only() {
    var world = new TestWorld();
    var target = Tabbed("smex:toolmold-blue-fired-plate");
    var other = Tabbed("smex:testblock-tabbed");
    world.World.Blocks.Returns(new List<Block> { target, other });
    world.World.Items.Returns(new List<Item>());

    int hidden = ExContentGate.HideFromCreativeAndHandbook(
      world.Api,
      obj => obj.Code.Path.EndsWith("-plate")
    );

    Assert.Equal(1, hidden);
    Assert.Null(target.CreativeInventoryTabs); // hidden -> also drops from the handbook
    Assert.Null(target.CreativeInventoryStacks);
    Assert.NotNull(other.CreativeInventoryTabs); // untouched
  }

  [Fact]
  public void Hiding_returns_zero_when_nothing_matches() {
    var world = new TestWorld();
    var block = Tabbed("smex:testblock-tabbed");
    world.World.Blocks.Returns(new List<Block> { block });
    world.World.Items.Returns(new List<Item>());

    int hidden = ExContentGate.HideFromCreativeAndHandbook(
      world.Api,
      obj => obj.Code.Path.EndsWith("-plate")
    );

    Assert.Equal(0, hidden);
    Assert.NotNull(block.CreativeInventoryTabs);
  }
}
