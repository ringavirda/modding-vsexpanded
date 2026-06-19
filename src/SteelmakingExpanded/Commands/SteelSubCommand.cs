using ExpandedLib.Registries.Commands;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SteelmakingExpanded.Commands;

/// <summary>
/// Attaches <c>/exmod steel &lt;normal|cheap&gt;</c> to the library's shared server <c>/exmod</c> root
/// (admins only). With no argument it reports the current steelmaking recipe cost level; with one it
/// sets <see cref="SmexConfig.RecipeLevel"/> and persists it. The per-recipe numbers live in
/// <c>smex_recipes.json</c>; the change applies on the next world reload. Server-side.
/// </summary>
[SubCommandRegister(Side = EnumAppSide.Server)]
public sealed class SteelSubCommand : IExSubCommand
{
  public string ParentName => "exmod";

  public void Register(ICoreAPI api, Mod mod, IChatCommand parent)
  {
    string domain = mod.Info.ModID;

    parent
      .BeginSubCommand("steel")
      .WithDescription(Lang.Get(domain + ":command-steel-desc"))
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
        Lang.Get(domain + ":command-steel-status", LevelLabel(domain))
      );

    if (level == SmexValues.RecipeLevel)
      return TextCommandResult.Success(
        Lang.Get(domain + ":command-steel-retain", LevelLabel(domain))
      );

    SmexValues.Edit(c => c.RecipeLevel = level);
    return TextCommandResult.Success(
      Lang.Get(domain + ":command-steel-set", LevelLabel(domain))
    );
  }

  private static string LevelLabel(string domain) =>
    Lang.Get(domain + ":recipe-level-" + SmexValues.RecipeLevel);
}
