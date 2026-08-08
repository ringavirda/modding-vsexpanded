using ExpandedLib.Registries.Commands;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace ExpandedLib.Commands;

/// <summary>
/// The shared <c>exmod</c> root command for every Fallenstar Expanded mod. exlib always registers it,
/// created via <see cref="Vintagestory.API.Common.IChatCommandApi.GetOrCreate(string)"/> so
/// sub-commands can attach in any load order; on its own it prints help. Dependent mods hang their
/// options off it as <see cref="IExSubCommand"/>s. Registered on <see cref="EnumAppSide.Universal"/>,
/// so it exists as two independent commands - client-side <c>.exmod</c>, which runs on the player's
/// machine, and server-side <c>/exmod</c>, which runs on the world host. Each sub-command declares its
/// own side and attaches only to the matching root.
/// </summary>
[CommandRegister(Side = EnumAppSide.Universal)]
public sealed class ExmodCommand : IExCommand {
  public void Register(ICoreAPI api, Mod mod) {
    bool isClient = api.Side == EnumAppSide.Client;
    string descKey = isClient
      ? "command-exmod-desc-client"
      : "command-exmod-desc-server";
    string helpKey = isClient
      ? "command-exmod-help-client"
      : "command-exmod-help-server";

    // ChatCommands is per-side: the client registry hosts ".exmod", the server registry "/exmod". The
    // server requires a privilege before a command is valid, the client side does not. Client .exmod
    // hosts per-player display options, so chat (held by everyone) is enough; server /exmod is an
    // operator tool, gated on controlserver.
    string privilege = isClient ? Privilege.chat : Privilege.controlserver;

    api.ChatCommands.GetOrCreate("exmod")
      .RequiresPrivilege(privilege)
      .WithDescription(Lang.Get($"exlib:{descKey}"))
      .HandleWith(_ => TextCommandResult.Success(Lang.Get($"exlib:{helpKey}")));
  }
}
