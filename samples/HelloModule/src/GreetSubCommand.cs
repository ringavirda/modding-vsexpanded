using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace HelloModule;

/// <summary>The whole command walk: <c>/exmod greet</c> prints how many greetings loaded.</summary>
[SubCommandRegister(Side = EnumAppSide.Server)]
public sealed class GreetSubCommand : IExSubCommand {
  public string ParentName => "exmod";

  public void Register(ICoreAPI api, Mod mod, IChatCommand parent) {
    parent
      .BeginSubCommand("greet")
      .WithDescription(Lang.Get("hellomodule:command-greet-desc"))
      .HandleWith(args =>
        TextCommandResult.Success(
          Lang.Get("hellomodule:command-greet-result", Greetings.All.Count)
        )
      )
      .EndSubCommand();
  }
}
