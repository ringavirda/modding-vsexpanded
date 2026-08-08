using System;
using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks the one structural rule a <see cref="BlockNetworkNode"/> definition must obey:
/// <b>it declares at least one <c>type</c> state.</b>
///
/// <para>
/// <b>Why this needs a test rather than a comment.</b> A node's runtime
/// <c>AllowedOrientations</c> comes from <see cref="ExDefinitions.OrientationMap"/>, which has nothing
/// to contribute for a def carrying no <c>type</c> states. A node that loses its <c>type</c> group
/// therefore gets an <b>empty</b> orientation map, so <c>ComputeValidOrientations</c> returns <c>[]</c>,
/// <c>TryPlaceBlock</c> refuses, and the block becomes impossible to place - with no exception, no log
/// line and no failing assertion anywhere. It simply stops existing in play.
/// </para>
///
/// <para>
/// Several states in one group are fine and are mapped individually: they share that def's single
/// orientation group by construction. Requiring <i>exactly</i> one used to be the rule and it dropped
/// <c>iwex:flywheel</c> - <c>type(normal|large)</c> - entirely, which is how an unplaceable live block
/// went unnoticed.
/// </para>
///
/// <para>
/// That is the exact trap waiting in the code-naming rename: five blocks currently "stutter"
/// (<c>iwex:furnace-tuyere-*</c>), and the obvious tidy-up - deleting the redundant single-state
/// <c>type</c> group - is precisely what breaks them. The group is load-bearing; the <i>code</i> is what
/// moves. This check makes that unrepresentable rather than remembered.
/// </para>
/// </summary>
public static class NetworkNodeContract
{
  /// <summary>
  /// Every violation in <paramref name="asm"/>, as a human-readable line. Empty means the contract
  /// holds. Returns the offending def's code and what it declared, so the message names the block.
  /// </summary>
  public static IReadOnlyList<string> Violations(string domain, Assembly asm)
  {
    var problems = new List<string>();

    foreach (Type type in ReflectionScan.GetCandidateTypes(asm))
    {
      if (!typeof(BlockNetworkNode).IsAssignableFrom(type) || type.IsAbstract)
        continue;

      foreach (ExBlockDef def in ExDefinitions.DefinitionsOf(type, domain))
      {
        if (def.VariantStates("type").Length > 0)
          continue;

        problems.Add(
          $"{domain}:{def.Code} ({type.Name}) declares NO `type` variant group - OrientationMap has "
            + "nothing to contribute, so AllowedOrientations is empty, ComputeValidOrientations returns "
            + "[] and TryPlaceBlock refuses. The block can never be placed, silently."
        );
      }
    }

    return problems;
  }
}
