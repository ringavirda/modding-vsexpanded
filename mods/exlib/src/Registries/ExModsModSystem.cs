using System.ComponentModel;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries;

/// <summary>
/// Sets <see cref="ExMods.FlagKey"/> to <c>true</c> in <c>api.World.Config</c> for every enabled mod, so
/// a JSON patch's <c>condition</c> can gate on another mod without any C# code. Runs at
/// <see cref="ExecuteOrder"/> 0.0 in both <see cref="StartPre"/> and <see cref="Start"/>, well ahead of
/// the JSON patch loader's <c>AssetsLoaded</c> at 0.05. See <see cref="SetFlags"/> for why no
/// client/server sync is needed and why it runs twice.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class ExModsModSystem : ModSystem {
  public override double ExecuteOrder() => 0.0;

  public override void StartPre(ICoreAPI api) => SetFlags(api);

  public override void Start(ICoreAPI api) => SetFlags(api);

  // api.ModLoader is each side's own local view of the mods enabled on that side - mod discovery
  // completes before any ModSystem lifecycle call runs - so the client computes an identical flag
  // set from its own local mod list here, before its own patch loader runs, rather than through a
  // copy of server data arriving late; no network sync is involved either way. Run from both
  // StartPre and Start (idempotent, negligible cost) because the vendored source stops at
  // interfaces for IWorldAccessor and does not show whether World.Config is guaranteed non-null as
  // early as StartPre - ModJsonPatchLoader defends against a null tree at its own, later, 0.05 read
  // (.compat/Vintagestory/vsessentialsmod/Loading/JsonPatchLoader.cs).
  private static void SetFlags(ICoreAPI api) {
    var config = api.World?.Config;
    if (config == null)
      return;
    foreach (Mod mod in api.ModLoader.Mods)
      config.SetBool(ExMods.FlagKey(mod.Info.ModID), true);
  }
}
