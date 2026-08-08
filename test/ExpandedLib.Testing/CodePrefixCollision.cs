using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks that no block's <b>base code</b> is a proper prefix of another's at a <c>-</c> boundary.
///
/// <para>
/// <b>Why this is not a style rule.</b> A shared base code is how a family names its members
/// (<c>pipe</c> + <c>type(straight|bend|…)</c>), and the idiom that goes with it is building a wildcard
/// from the code: <c>IwexBlocks.SomeFamily.Code + "*"</c>. That wildcard is correct exactly as long as no
/// <i>other</i> block's code starts with the same string. The moment one does, the wildcard silently widens
/// to cover it.
/// </para>
///
/// <para>
/// <b>The failure lands in multiblock layouts.</b> <c>IwexCodes.Tuyere</c> is
/// <c>FurnaceTuyere.Code + "*"</c> and is used as a <c>Legend</c> in all three shaft-furnace drawings. If
/// the tuyere's base code were <c>furnace</c> while a charge door's were <c>furnace-chargedoor</c>, the
/// legend would accept <b>a charge door in a tuyere cell</b> and the structure would complete wrong - with
/// no test failing, because every code involved is real and every block resolves.
/// </para>
///
/// <para>
/// The rule is a boundary rule, not a substring rule: <c>slag-path</c> and <c>slag-pathslab</c> are fine
/// because the character after <c>slag-path</c> is <c>s</c>, not <c>-</c>, so <c>slag-path-*</c> cannot
/// match the slab. That is the same accident <c>CostSelectorOverlap</c> documents - and the same reason it
/// must not be relied on.
/// </para>
///
/// <para>
/// The practical consequence for a family: <b>either every member lives in a <c>type</c> variant under one
/// shared code, or no member does.</b> A half-and-half family is what produces the collision.
/// </para>
/// </summary>
public static class CodePrefixCollision
{
  /// <summary>
  /// Every pair of distinct base codes where one is a prefix of the other at a <c>-</c> boundary, as
  /// readable lines. Empty means no wildcard built from a base code can stray outside its family.
  /// </summary>
  public static IReadOnlyList<string> Collisions(string domain, Assembly asm)
  {
    var codes = DefinitionGoldens
      .Collect(domain, asm)
      .OfType<ExBlockDef>()
      .Select(d => d.Code)
      .Distinct()
      .OrderBy(c => c)
      .ToList();

    var findings = new List<string>();
    foreach (string shorter in codes)
      foreach (string longer in codes)
      {
        if (shorter == longer || !longer.StartsWith(shorter + "-"))
          continue;

        findings.Add(
          $"'{domain}:{shorter}' is a prefix of '{domain}:{longer}' - a wildcard "
            + $"'{domain}:{shorter}-*' built from the shorter code also matches the longer one"
        );
      }

    return findings;
  }
}
