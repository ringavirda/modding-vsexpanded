using ExpandedLib.Registries;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>The shared case-insensitive keyed store backing the config/recipe/(metal/liquid)
/// registries.</summary>
public class ExKeyedRegistryTests {
  private sealed record Item(string Code, int Value);

  [Fact]
  public void Registers_and_looks_up_by_derived_code() {
    var reg = new ExKeyedRegistry<Item>(i => i.Code);
    reg.Register(new Item("lpex", 1));

    Assert.True(reg.TryGet("lpex", out var found));
    Assert.Equal(1, found.Value);
  }

  [Fact]
  public void Lookup_is_case_insensitive() {
    var reg = new ExKeyedRegistry<Item>(i => i.Code);
    reg.Register(new Item("Smex", 7));

    Assert.True(reg.TryGet("SMEX", out var found));
    Assert.Equal(7, found.Value);
  }

  [Fact]
  public void Re_registering_a_code_replaces_the_item() {
    var reg = new ExKeyedRegistry<Item>(i => i.Code);
    reg.Register(new Item("iwex", 1));
    reg.Register(new Item("iwex", 2));

    Assert.True(reg.TryGet("iwex", out var found));
    Assert.Equal(2, found.Value);
    Assert.Single(reg.Codes);
  }

  [Fact]
  public void Missing_code_returns_false() {
    var reg = new ExKeyedRegistry<Item>(i => i.Code);
    Assert.False(reg.TryGet("nope", out _));
  }

  [Fact]
  public void Codes_and_values_enumerate_the_registered_set() {
    var reg = new ExKeyedRegistry<Item>(i => i.Code);
    reg.Register(new Item("a", 1));
    reg.Register(new Item("b", 2));

    Assert.Equal(2, reg.Codes.Count);
    Assert.Contains("a", reg.Codes);
    Assert.Contains("b", reg.Codes);
    Assert.Equal(3, System.Linq.Enumerable.Sum(reg.Values, i => i.Value));
  }
}
