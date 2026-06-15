using ExpandedLib.Registries.Commands;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Commands;

/// <summary>
/// The shared <c>/exmod</c> root command for every Fallenstar Expanded mod. exlib always registers
/// it (created via <see cref="Vintagestory.API.Common.IChatCommandApi.GetOrCreate(string)"/> so
/// sub-commands can attach in any load order); on its own it just prints help. Dependent mods hang
/// their options off it as <see cref="IExSubCommand"/>s - e.g. exlib's own <c>network</c>
/// visualisation and ppex's <c>measure</c> unit toggle. Client-side, since the options it hosts are
/// client/per-player.
/// </summary>
[CommandRegister(Side = EnumAppSide.Client)]
public sealed class ExmodCommand : IExCommand
{
  public void Register(ICoreAPI api, Mod mod)
  {
    var capi = (ICoreClientAPI)api;
    capi.ChatCommands.GetOrCreate("exmod")
      .WithDescription(Lang.Get("exlib:command-exmod-desc"))
      .HandleWith(_ =>
        TextCommandResult.Success(Lang.Get("exlib:command-exmod-help"))
      );
  }
}
