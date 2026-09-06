using ExpandedLib.Networks;
using ExpandedLib.Registries;
using System.ComponentModel;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Registries;

/// <summary>
/// Adds <c>.exmod network hi</c> and <c>.exmod network unhi</c>: toggles the transparent, per-network
/// coloured highlight of every block network (see <see cref="NetworkHighlightModSystem"/>), showing
/// which blocks share a network and where a run is broken. Client-side; the command flips the toggle
/// and the server, which owns the graph, pushes the highlight.
/// </summary>
[SubCommandRegister(Side = EnumAppSide.Client)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NetworkSubCommand : IExSubCommand {
  public string ParentName => "exmod";

  public void Register(ICoreAPI api, Mod mod, IChatCommand parent) {
    var highlight = api.ModLoader.GetModSystem<NetworkHighlightModSystem>();

    parent
      .BeginSubCommand("network")
      .WithDescription(Lang.Get("exlib:command-network-desc"))
      .BeginSubCommand("hi")
      .WithDescription(Lang.Get("exlib:command-network-hi-desc"))
      .HandleWith(_ => DispatchHi(highlight))
      .EndSubCommand()
      .BeginSubCommand("unhi")
      .WithDescription(Lang.Get("exlib:command-network-unhi-desc"))
      .HandleWith(_ => DispatchUnhi(highlight))
      .EndSubCommand()
      .EndSubCommand();
  }

  /// <summary>
  /// The two handlers with the framework's fluent arg parsing already stripped away: mirrors
  /// <see cref="RegistrySubCommand{T}.Dispatch"/>. Internal rather than private so a test can drive
  /// them without building a fake <see cref="Vintagestory.API.Common.TextCommandCallingArgs"/>.
  /// </summary>
  internal static TextCommandResult DispatchHi(NetworkHighlightModSystem highlight) {
    highlight.SetEnabled(true);
    return TextCommandResult.Success(Lang.Get(ExlibLang.NetworkHiOn));
  }

  internal static TextCommandResult DispatchUnhi(NetworkHighlightModSystem highlight) {
    highlight.SetEnabled(false);
    return TextCommandResult.Success(Lang.Get(ExlibLang.NetworkHiOff));
  }
}
