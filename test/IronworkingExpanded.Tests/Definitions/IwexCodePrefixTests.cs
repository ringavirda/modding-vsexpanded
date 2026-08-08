using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// No <c>iwex</c> block's base code may be a proper prefix of another's at a <c>-</c> boundary. Several
/// <c>IwexCodes</c> selectors use <c>SomeFamily.Code + "*"</c> for "any member of this family", including
/// ones fed straight into multiblock <c>Legend</c>s, so a prefix collision widens such a wildcard onto a
/// different block and a layout accepts the wrong part with every code still resolving. The rule that
/// keeps this true: either all of a family's members live in the <c>type</c> variant, or none do.
/// See <see cref="CodePrefixCollision"/>.
/// </summary>
public class IwexCodePrefixTests {
  private const string Domain = "iwex";
  private static readonly Assembly Mod =
    typeof(IronworkingExpanded.Recipes.Grid.FurnaceRecipeDefinitions).Assembly;

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
