using System.Collections.Generic;

namespace ExpandedLib.Definitions;

/// <summary>
/// The recipe-side sibling of <see cref="IExBlockDefProvider"/> and <see cref="IExItemDefProvider"/>:
/// implemented by a class that authors its own code-first recipe files, one <see cref="ExRecipeDef"/>
/// per <c>recipes/{category}/</c> asset. <see cref="Registries.EntityRegistry"/> discovers
/// implementors while registering a mod's classes (<c>RegisterAll</c>) and registers each returned def
/// into <see cref="ExDefinitions"/> for injection. Recipes have no block or item class to hang on and so
/// use stand-alone provider classes; any concrete class carrying a static <c>Definitions</c> factory is
/// discovered without being instantiated (see <see cref="ExDefinitions.RecipeDefinitionsOf"/>).
/// </summary>
public interface IExRecipeDefProvider {
  /// <summary>Builds this class's recipe file(s) in <paramref name="domain"/> (the mod id).</summary>
  static abstract IEnumerable<ExRecipeDef> Definitions(string domain);
}
