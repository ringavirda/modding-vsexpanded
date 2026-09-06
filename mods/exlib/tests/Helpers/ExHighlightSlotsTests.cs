using ExpandedLib.Helpers;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>The process-wide highlight-slot handout: stable per key, distinct across keys.</summary>
public class ExHighlightSlotsTests {
  [Fact]
  public void Reserve_returns_the_same_id_for_the_same_key() {
    string key = "test:" + System.Guid.NewGuid();

    int first = ExHighlightSlots.Reserve(key);
    int second = ExHighlightSlots.Reserve(key);

    Assert.Equal(first, second);
  }

  [Fact]
  public void Reserve_returns_distinct_ids_for_distinct_keys() {
    string a = "test:" + System.Guid.NewGuid();
    string b = "test:" + System.Guid.NewGuid();

    Assert.NotEqual(ExHighlightSlots.Reserve(a), ExHighlightSlots.Reserve(b));
  }

  [Fact]
  public void Reserve_never_hands_out_below_the_documented_base() {
    string key = "test:" + System.Guid.NewGuid();
    Assert.True(ExHighlightSlots.Reserve(key) >= ExHighlightSlots.Base);
  }
}
