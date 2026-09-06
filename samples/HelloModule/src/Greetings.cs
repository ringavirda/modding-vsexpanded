using System.Collections.Generic;
using ExpandedLib.Catalogues;
using Vintagestory.API.Common;

namespace HelloModule;

/// <summary>
/// The loaded greeting catalogue. Populated by <see cref="HelloModule.AssetsFinalize"/> on both
/// sides identically, so <see cref="BlockBehaviorGreeter"/> can read it on the server without a
/// round trip; empty until then.
/// </summary>
public static class Greetings {
  private static List<GreetingDef> _all = [];

  /// <summary>Every greeting loaded so far, in asset order.</summary>
  public static IReadOnlyList<GreetingDef> All => _all;

  /// <summary>Reads every <c>config/greetings/</c> entry across every domain. Must run at
  /// <c>AssetsFinalize</c>, after the patch pipeline has merged the raw JSON.</summary>
  public static void Load(ICoreAPI api) =>
    _all = AssetCatalogueLoader.GetMany<GreetingDef>(api, "config/greetings/");
}
