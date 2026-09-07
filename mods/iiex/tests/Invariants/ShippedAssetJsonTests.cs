using System.Collections.Generic;
using ExpandedLib.Testing;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The per-mod JSON-defect rule (<see cref="ShippedJson"/>) over iiex's own tree and the shared
/// <c>game</c> lang overlay it ships alongside it.
/// </summary>
public class ShippedAssetJsonTests {
  private static readonly string[] Trees =
  [
    RepoPaths.Assets("iiex"),
    RepoPaths.Assets("game"),
  ];

  [Fact]
  public void Iiexs_shipped_json_carries_no_defect() {
    var offenders = new List<string>();
    foreach (string tree in Trees)
      offenders.AddRange(ShippedJson.Check(tree));

    Assert.True(offenders.Count == 0, string.Join("\n", offenders));
  }

  [Fact]
  public void The_patch_corpus_is_not_empty() {
    // The rule above passes trivially if the path filter stops matching - a patches folder renamed or
    // moved would read as "every entry is correct".
    Assert.NotEmpty(ShippedJson.PatchFiles(RepoPaths.Assets("iiex")));
  }
}
