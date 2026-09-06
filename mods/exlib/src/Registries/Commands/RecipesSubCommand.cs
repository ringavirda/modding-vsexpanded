using System.ComponentModel;
using System.Linq;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Registries;

/// <summary>
/// Adds <c>/exmod recipes [&lt;mod&gt; [&lt;level&gt;]]</c> over the recipe-cost levels a mod registers
/// through <see cref="ExRecipeProfiles"/>. With no argument it lists the registered mods and their
/// current level; with a mod code it reports that mod's level; with both it sets the level
/// (e.g. <c>/exmod recipes siex cheap</c>) and persists it. The per-recipe numbers live in each mod's
/// <c>*_recipes.json</c> and a change applies on the next world reload. Server-side, since recipe costs
/// are host-authoritative; the <c>/exmod</c> root requires <c>controlserver</c>.
/// </summary>
[SubCommandRegister(Side = EnumAppSide.Server)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RecipesSubCommand : RegistrySubCommand<RecipeProfile> {
  public RecipesSubCommand()
    : base(
      "recipes",
      "exlib:command-recipes-desc",
      () => ExRecipeProfiles.Codes,
      code => ExRecipeProfiles.TryGet(code, out var profile) ? profile : null
    ) { }

  protected override string NoneRegisteredKey => "exlib:command-recipes-none";
  protected override string ListHeaderKey => "exlib:command-recipes-list";
  protected override string UnknownCodeKey => "exlib:command-recipes-unknown";

  protected override string Describe(RecipeProfile profile) =>
    $"{profile.Code}: {profile.GetLevel()}";

  protected override TextCommandResult Set(RecipeProfile profile, string[] args) {
    if (args.Length == 0)
      return TextCommandResult.Success(
        Lang.Get(
          "exlib:command-recipes-status",
          profile.Code,
          profile.GetLevel()
        )
      );

    string level = args[0].ToLowerInvariant();
    if (!profile.Levels.Contains(level))
      return TextCommandResult.Error(
        Lang.Get(
          "exlib:command-recipes-invalid",
          level,
          string.Join(", ", profile.Levels)
        )
      );

    if (level == profile.GetLevel())
      return TextCommandResult.Success(
        Lang.Get("exlib:command-recipes-retain", profile.Code, level)
      );

    profile.SetLevel(level);
    return TextCommandResult.Success(
      Lang.Get("exlib:command-recipes-set", profile.Code, level)
    );
  }
}
