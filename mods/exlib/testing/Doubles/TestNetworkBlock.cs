using System.Collections.Generic;
using ExpandedLib.Blocks;
using ExpandedLib.Networks;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Testing;

/// <summary>
/// Minimal concrete <see cref="BlockNetworkNode"/> for graph tests: its connector set is the
/// orientation string passed in (e.g. "ns", "we", "nswe"), and its network type is configurable.
/// Bypasses the asset-load pipeline: <see cref="BlockNetworkNode.Orientation"/> and <c>Type</c> are
/// set directly rather than parsed from variants in <c>OnLoaded</c>.
/// </summary>
public sealed class TestNetworkBlock : BlockNetworkNode {
  private readonly string _networkType;

  public override string NetworkType => _networkType;

  public override Dictionary<string, string[]> AllowedOrientations { get; } =
    new() { { "straight", ["ns", "we", "ud"] } };

  protected override string GetFallbackOrientation(string? type) => "ns";

  private TestNetworkBlock(string networkType, string orientation) {
    _networkType = networkType;
    Type = "straight";
    Orientation = orientation;
  }

  /// <summary>
  /// Builds and registers a node block of <paramref name="networkType"/> with the given
  /// <paramref name="orientation"/> connectors, primed with a code and id so it resolves through the
  /// store. <paramref name="code"/> defaults to a unique per-id string.
  /// </summary>
  public static TestNetworkBlock Create(
    string networkType,
    string orientation,
    int id,
    string? code = null
  ) =>
    TestBlocks.Configure(
      new TestNetworkBlock(networkType, orientation),
      code ?? $"test:{networkType}-{orientation}-{id}",
      id
    );

  /// <summary>
  /// One block per token of <paramref name="orientations"/>, sharing the code stem
  /// <paramref name="stem"/> and each carrying its own <see cref="BlockBehaviorExOrientable"/> in
  /// network mode - the shape the engine builds from a def's <c>orientation</c> variant group, and the
  /// one <c>CodeWithVariant</c> and <c>ApplyOrientation</c> walk between. Ids run from
  /// <paramref name="firstId"/> upward.
  /// </summary>
  public static TestNetworkBlock[] Family(
    string networkType,
    string stem,
    string scheme,
    string[] orientations,
    int firstId = 1
  ) {
    var family = new TestNetworkBlock[orientations.Length];

    for (int i = 0; i < orientations.Length; i++) {
      string token = orientations[i];
      TestNetworkBlock block = TestBlocks.Configure(
        new TestNetworkBlock(networkType, token),
        $"{stem}-{token}",
        firstId + i,
        ("orientation", token)
      );

      var behaviour = new BlockBehaviorExOrientable(block);
      behaviour.Initialize(
        new JsonObject(
          JToken.Parse($$"""{"mode":"network","scheme":"{{scheme}}"}""")
        )
      );
      // Both arrays, as the engine's own registration fills them: `GetBehavior<T>` reads
      // CollectibleBehaviors, while the block-behaviour fan-outs walk BlockBehaviors, and a double
      // that fills only one silently answers null to half the production code.
      block.BlockBehaviors = [behaviour];
      block.CollectibleBehaviors = [behaviour];
      family[i] = block;
    }

    return family;
  }
}

/// <summary>Test-only hooks for setting a <see cref="BlockNetworkNode"/>'s shape-family and connector
/// state directly, for a real production node whose asset-load pipeline a fixture bypasses (a
/// <see cref="TestNetworkBlock"/> already takes both through its constructor).</summary>
public static class NetworkNodeTestHooks {
  /// <summary>Sets <see cref="BlockNetworkNode.Type"/> directly, the shape-family answer normally read
  /// off the block's variant map during load.</summary>
  public static void SetNetworkTypeForTest(
    this BlockNetworkNode node,
    string type
  ) => node.SetNetworkTypeForTest(type);

  /// <summary>Sets <see cref="BlockNetworkNode.Orientation"/> directly, the connector-face code
  /// normally resolved from the surrounding network during placement.</summary>
  public static void ApplyOrientationForTest(
    this BlockNetworkNode node,
    string token
  ) => node.ApplyOrientationForTest(token);
}
