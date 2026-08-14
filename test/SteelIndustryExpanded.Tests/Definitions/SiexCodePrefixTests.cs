using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// No <c>siex</c> block's base code may be a proper prefix of another's at a <c>-</c> boundary. Several
/// <c>SiexCodes</c> selectors use <c>SomeFamily.Code + "*"</c> to name any member of a family, including
/// ones fed straight into multiblock <c>Legend</c>s; a prefix collision widens such a wildcard to cover
/// a different block, and a layout then accepts the wrong part in a cell with every code still
/// resolving. Keeping a family safe means either all of its members live in the <c>type</c> variant or
/// none do. See <see cref="CodePrefixCollision"/>.
/// </summary>
public class SiexCodePrefixTests {
  private const string Domain = "siex";
  private static readonly Assembly Mod =
    typeof(SteelIndustryExpanded.SiexConfig).Assembly;

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
