using System.Collections.Generic;

namespace ExpandedLib.Definitions;

/// <summary>
/// The recipe-side sibling of <see cref="IExBlockDefProvider"/> / <see cref="IExItemDefProvider"/>:
/// implemented by a class that authors its own code-first recipe file(s) - one <see cref="ExRecipeDef"/>
/// per <c>recipes/{category}/</c> asset. <see cref="Registries.Entities.EntityRegistry"/> discovers
/// implementors while registering a mod's classes (<c>RegisterAll</c>) and registers each returned def into
/// <see cref="ExDefinitions"/> for injection.
/// <para>
/// Recipes have no natural block/item class to hang on, so they are authored by dedicated stand-alone
/// provider classes (the same pattern as the tool-mold blocks and the gear items). Any concrete class
/// carrying a static <c>Definitions</c> factory is discovered without being instantiated (see
/// <see cref="ExDefinitions.RecipeDefinitionsOf"/>).
/// </para>
/// </summary>
public interface IExRecipeDefProvider
{
  /// <summary>Builds this class's recipe file(s) in <paramref name="domain"/> (the mod id).</summary>
  static abstract IEnumerable<ExRecipeDef> Definitions(string domain);
}
