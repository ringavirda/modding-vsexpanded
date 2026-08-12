using System.Linq;
using IronworkingExpanded.BlockStructures.Forming;
using Xunit;

namespace IronworkingExpanded.Tests;

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
    Assert.Contains("bloom", StockForm.All.Keys);
    Assert.Contains("slab", StockForm.All.Keys);
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
  public void The_forms_we_ship_cannot_be_dropped_by_a_reset() {
    // A mod clearing the table would take our stock with it, so there is no clear - only a targeted
    // removal, and re-seeding restores the shipped set.
    StockForm.SeedDefaults();

    Assert.Contains("bloom", StockForm.All.Keys);
    Assert.Equal(2, StockForm.All.Count);
  }
}
