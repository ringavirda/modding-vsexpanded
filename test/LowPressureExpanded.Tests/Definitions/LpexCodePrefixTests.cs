using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace LowPressureExpanded.Tests;

/// <summary>
/// No <c>lpex</c> block's base code may be a proper prefix of another's at a <c>-</c> boundary. The
/// idiom this protects is <c>SomeFamily.Code + "*"</c>, used by <c>LpexCodes</c> selectors to name any
/// member of a family and fed straight into multiblock <c>Legend</c>s: a prefix collision widens such
/// a wildcard to cover a different block, so a layout accepts the wrong part in a cell with every code
/// still resolving. The rule that satisfies it is that either all of a family's members live in the
/// <c>type</c> variant or none do. See <see cref="CodePrefixCollision"/>.
/// </summary>
public class LpexCodePrefixTests {
  private const string Domain = "lpex";
  private static readonly Assembly Mod =
    typeof(LowPressureExpanded.LpexConfig).Assembly;

  [Fact]
  public void No_base_code_is_a_prefix_of_another() {
    IReadOnlyList<string> collisions = CodePrefixCollision.Collisions(
      Domain,
      Mod
    );

    Assert.True(
      collisions.Count == 0,
      $"{collisions.Count} base-code prefix collision(s) in {Domain}:\n  "
        + string.Join("\n  ", collisions)
    );
  }
}
