using System.Linq;
using IronIndustryExpanded.BlockStructures.Forming;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Stock forms are a registry, not a closed table. This was named the literal wall on mill extensibility:
/// a third party's roll set could declare it accepts their own stock, and the piece would still dead-end at
/// <c>WrongForm</c> because nothing could add the form itself.
/// See docs/design/mechanics/machining-line.md and process-extension.md.
/// </summary>
public class StockFormRegistryTests {
  // Registered into the shared table like a mod would, then taken back out. Serial collections make this
  // safe; leaving it in would change the shipped-corpus counts other suites assert.
  private static StockForm Foreign() => new("bronzebar", 3f, 3f, 8f, 16f, 0.8f);

  [Fact]
  public void The_forms_we_ship_are_registered_rather_than_hard_coded() {
    Assert.Contains("shingledbar", StockForm.All.Keys);
    Assert.Contains("shingledslab", StockForm.All.Keys);
  }

  [Fact]
  public void A_mod_registers_a_form_of_its_own_and_the_mill_can_find_it() {
    try {
      StockForm.Register(Foreign());

      Assert.True(StockForm.TryGet("bronzebar", out StockForm? found));
      Assert.Equal(8f, found!.MaxWidth);
    } finally {
      StockForm.Unregister("bronzebar");
    }
  }

  [Fact]
  public void An_unregistered_form_is_absent_rather_than_a_throw() {
    Assert.False(StockForm.TryGet("nosuchform", out _));
    Assert.False(StockForm.TryGet(null, out _));
  }

  [Fact]
  public void Registering_a_form_twice_replaces_it_rather_than_doubling_it() {
    try {
      StockForm.Register(Foreign());
      int before = StockForm.All.Count;
      StockForm.Register(Foreign() with { MaxWidth = 12f });

      Assert.Equal(before, StockForm.All.Count);
      Assert.Equal(12f, StockForm.All["bronzebar"].MaxWidth);
    } finally {
      StockForm.Unregister("bronzebar");
    }
  }

  [Fact]
  public void A_form_that_was_renamed_still_answers_to_the_name_it_had() {
    // A piece already in a world carries its form name on its own stack. Without this the mill reads a form
    // nobody registered and refuses the piece as WrongForm - the rename would eat the player's stock.
    Assert.True(StockForm.TryGet("bloom", out StockForm? bar));
    Assert.Equal("shingledbar", bar!.Name);
    Assert.True(StockForm.TryGet("slab", out StockForm? slab));
    Assert.Equal("shingledslab", slab!.Name);
  }

  [Fact]
  public void A_former_name_is_not_a_form_of_its_own() {
    // It resolves a piece and nothing more: listed as a form it would double the shipped corpus every guard
    // counts, and a roll set could declare it accepts a name that no longer exists.
    Assert.DoesNotContain("bloom", StockForm.All.Keys);
    Assert.DoesNotContain("slab", StockForm.All.Keys);
  }

  [Fact]
  public void Unregistering_a_form_takes_its_former_names_with_it() {
    StockForm.Register(Foreign() with { FormerNames = ["bronzeblank"] });
    StockForm.Unregister("bronzebar");

    Assert.False(StockForm.TryGet("bronzeblank", out _));
  }

  [Fact]
  public void The_forms_we_ship_cannot_be_dropped_by_a_reset() {
    // A mod clearing the table would take our stock with it, so there is no clear - only a targeted
    // removal, and re-seeding restores the shipped set. Asserted as the whole set rather than as a count,
    // so a seed that forgets one and a seed that gains one read differently.
    StockForm.SeedDefaults();

    Assert.Equal(
      ["beam", "heavyplate", "rod", "shingledbar", "shingledslab"],
      StockForm.All.Keys.Order()
    );
  }

  [Fact]
  public void The_mill_admits_a_vanilla_rod_and_it_enters_as_our_stock() {
    // The re-rollable rod stays vanilla's, so the mill converts at the deck rather than the design minting
    // a second rod. Without the row a player holding `game:rod-iron` gets WrongForm at a mill that the
    // design says takes it.
    StockForm.SeedDefaults();

    Assert.Equal("iiex:stock-rod", StockForm.EntersAs("game:rod-iron"));
    Assert.True(StockForm.TryGet("rod", out _));
  }

  [Fact]
  public void Stock_is_never_feedstock_for_itself() {
    // Admission is for pieces that are not work pieces yet. A stock item listed here would be converted
    // into a fresh one on every feed, silently discarding the gauge it had been rolled to.
    StockForm.SeedDefaults();

    foreach (string entersAs in StockForm.Feedstock.Values)
      Assert.Null(StockForm.EntersAs(entersAs));
  }
}
