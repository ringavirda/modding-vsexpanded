using ExpandedLib.Helpers;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>The defensive tree-value reader: a corrupt persisted field degrades to a fallback instead
/// of throwing (which would make the engine discard the whole block entity on load).</summary>
public class ExTreeTests
{
  [Fact]
  public void Deserializes_a_well_formed_value()
  {
    string[] result = ExTree.SafeDeserialize<string[]>("[\"ns\",\"ew\"]", []);
    Assert.Equal(new[] { "ns", "ew" }, result);
  }

  [Fact]
  public void Null_or_empty_returns_the_fallback()
  {
    var fallback = new[] { "keep" };
    Assert.Same(fallback, ExTree.SafeDeserialize(null, fallback));
    Assert.Same(fallback, ExTree.SafeDeserialize("", fallback));
  }

  [Fact]
  public void Malformed_json_returns_the_fallback_instead_of_throwing()
  {
    // A corrupted/hand-edited orientation string must not throw out of FromTreeAttributes.
    string[] fallback = [];
    Assert.Same(fallback, ExTree.SafeDeserialize("{ not valid ]", fallback));
    Assert.Same(fallback, ExTree.SafeDeserialize("[\"unterminated", fallback));
  }

  [Fact]
  public void Json_literal_null_returns_the_fallback()
  {
    var fallback = new[] { "keep" };
    Assert.Same(fallback, ExTree.SafeDeserialize("null", fallback));
  }
}
