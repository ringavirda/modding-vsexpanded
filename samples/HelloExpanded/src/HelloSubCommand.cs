using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace HelloExpanded;

/// <summary>The whole command walk: <c>/exmod hello</c> prints the live config value.</summary>
[SubCommandRegister(Side = EnumAppSide.Server)]
public sealed class HelloSubCommand : IExSubCommand {
  public string ParentName => "exmod";

  public void Register(ICoreAPI api, Mod mod, IChatCommand parent) {
    parent
      .BeginSubCommand("hello")
      .WithDescription(Lang.Get("helloexpanded:command-hello-desc"))
      .HandleWith(args =>
        TextCommandResult.Success(
          Lang.Get("helloexpanded:command-hello-result", HelloValues.TickIntervalMs)
        )
      )
      .EndSubCommand();
  }
}
