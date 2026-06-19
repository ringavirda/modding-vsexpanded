using ExpandedLib.Registries.Commands;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace PipesAndPowerExpanded.Commands;

/// <summary>
/// Attaches <c>/exmod steam &lt;normal|cheap&gt;</c> to the library's shared server <c>/exmod</c> root
/// (admins only - the root requires <c>controlserver</c>). With no argument it reports the current
/// steam-machine recipe cost level; with one it sets <see cref="PpexConfig.RecipeLevel"/> and persists
/// it. The per-recipe numbers live in <c>ppex_recipes.json</c>; the change applies on the next world
/// reload. Server-side, since recipe costs are host-authoritative.
/// </summary>
[SubCommandRegister(Side = EnumAppSide.Server)]
public sealed class SteamSubCommand : IExSubCommand
{
  public string ParentName => "exmod";

  public void Register(ICoreAPI api, Mod mod, IChatCommand parent)
  {
    string domain = mod.Info.ModID;

    parent
      .BeginSubCommand("steam")
      .WithDescription(Lang.Get(domain + ":command-steam-desc"))
      .WithArgs(
        api.ChatCommands.Parsers.OptionalWordRange("level", "normal", "cheap")
      )
      .HandleWith(args => OnCommand(domain, args))
      .EndSubCommand();
  }

  private static TextCommandResult OnCommand(
    string domain,
    TextCommandCallingArgs args
  )
  {
    string? level = (args[0] as string)?.ToLowerInvariant();

    if (level == null)
      return TextCommandResult.Success(
        Lang.Get(domain + ":command-steam-status", LevelLabel(domain))
      );

    if (level == PpexValues.RecipeLevel)
      return TextCommandResult.Success(
        Lang.Get(domain + ":command-steam-retain", LevelLabel(domain))
      );

    PpexValues.Edit(c => c.RecipeLevel = level);
    return TextCommandResult.Success(
      Lang.Get(domain + ":command-steam-set", LevelLabel(domain))
    );
  }

  private static string LevelLabel(string domain) =>
    Lang.Get(domain + ":recipe-level-" + PpexValues.RecipeLevel);
}
