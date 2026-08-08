using System;
using System.Collections.Generic;
using System.Reflection;
using ExpandedLib.Blocks.Networks;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;

namespace ExpandedLib.Testing;

/// <summary>
/// Checks the structural rule every <see cref="BlockNetworkNode"/> definition must obey: it declares
/// at least one <c>type</c> variant state. Runtime <c>AllowedOrientations</c> comes from
/// <see cref="ExDefinitions.OrientationMap"/>, which contributes nothing for a def carrying no
/// <c>type</c> states, so such a node gets an empty orientation map, <c>ComputeValidOrientations</c>
/// returns an empty set and <c>TryPlaceBlock</c> refuses - with no exception and no log line. A
/// single-state <c>type</c> group is therefore load-bearing even when it looks redundant. Several
/// states in one group are fine: they share that def's single orientation group by construction.
/// </summary>
public static class NetworkNodeContract {
  /// <summary>
  /// Every violation in <paramref name="asm"/>, as a human-readable line; empty means the contract
  /// holds. Each line names the offending def's code and its block type.
  /// </summary>
  public static IReadOnlyList<string> Violations(string domain, Assembly asm) {
    var problems = new List<string>();

    foreach (Type type in ReflectionScan.GetCandidateTypes(asm)) {
      if (!typeof(BlockNetworkNode).IsAssignableFrom(type) || type.IsAbstract)
        continue;

      foreach (ExBlockDef def in ExDefinitions.DefinitionsOf(type, domain)) {
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
