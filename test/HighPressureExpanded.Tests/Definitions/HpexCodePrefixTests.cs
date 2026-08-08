using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace HighPressureExpanded.Tests;

/// <summary>
/// No <c>hpex</c> block's base code may be a proper prefix of another's at a <c>-</c> boundary. Several
/// <c>HpexCodes</c> selectors, including ones fed into multiblock <c>Legend</c>s, use
/// <c>SomeFamily.Code + "*"</c> to mean "any member of this family"; a prefix collision widens such a
/// wildcard onto a different block while every code still resolves. In practice the rule is that either
/// all of a family's members live in the <c>type</c> variant, or none do.
/// See <see cref="CodePrefixCollision"/>.
/// </summary>
public class HpexCodePrefixTests {
  private const string Domain = "hpex";
  private static readonly Assembly Mod =
    typeof(HighPressureExpanded.HpexConfig).Assembly;

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
