using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Testing;
using Xunit;

namespace HighPressureExpanded.Tests;

/// <summary>
/// No <c>hpex</c> block's base code may be a proper prefix of another's at a <c>-</c> boundary.
/// <para>
/// The idiom this protects is <c>SomeFamily.Code + "*"</c>, which several
/// <c>HpexCodes</c> selectors use to name "any member of this family" - including ones fed straight into
/// multiblock <c>Legend</c>s. A prefix collision silently widens such a wildcard to cover a
/// <b>different block</b>, and a layout would then accept the wrong part in a cell with every code
/// resolving and no test failing. See <see cref="CodePrefixCollision"/>.
/// </para>
/// <para>
/// Found live: the 2026-08-03 naming wave briefly gave <c>iwex:mpenergy</c> (shaft + bevel) a code
/// that prefixed <c>iwex:mpenergy-flywheel</c>. The fix was to put every member of the family in its
/// <c>type</c> variant under one shared code - which is the rule this test enforces in general:
/// <b>either all of a family's members live in <c>type</c>, or none do.</b>
/// </para>
/// </summary>
public class HpexCodePrefixTests
{
  private const string Domain = "hpex";
  private static readonly Assembly Mod = typeof(HighPressureExpanded.HpexConfig).Assembly;

  [Fact]
  public void No_base_code_is_a_prefix_of_another()
  {
    IReadOnlyList<string> collisions = CodePrefixCollision.Collisions(Domain, Mod);

    Assert.True(
      collisions.Count == 0,
      $"{collisions.Count} base-code prefix collision(s) in {Domain}:\n  "
        + string.Join("\n  ", collisions)
    );
  }
}
