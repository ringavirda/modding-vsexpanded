using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Checks;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks that no block's base code is a proper prefix of another's at a <c>-</c> boundary. Wildcards
/// are routinely built from a base code (<c>SomeFamily.Code + "*"</c>) and used as multiblock
/// <c>Legend</c> entries, so a prefix collision widens such a wildcard onto a foreign block and lets a
/// structure complete with the wrong block in a cell, with every code involved still resolving.
/// <para>
/// A thin wrapper: the rule lives in <see cref="CodePrefixCollisionCheck"/>, which this class hands
/// an <see cref="AssemblyCheckSource"/> built off the given assembly, so every test written against
/// this signature keeps working unchanged.
/// </para>
/// </summary>
public static class CodePrefixCollision {
  /// <summary>
  /// Every pair of distinct base codes where one is a prefix of the other at a <c>-</c> boundary, as
  /// readable lines. Empty means no wildcard built from a base code can stray outside its family.
  /// </summary>
  public static IReadOnlyList<string> Collisions(string domain, Assembly asm) =>
    CodePrefixCollisionCheck
      .Run(new AssemblyCheckSource((domain, asm)), domain)
      .Errors;
}
