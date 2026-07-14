using System.Collections.Generic;

namespace ExpandedLib.Definitions;

/// <summary>
/// The item-side sibling of <see cref="IExBlockDefProvider"/>: implemented by a class that authors its
/// own code-first item definition(s), co-located with the class they configure (an <see cref="ExItemDef"/>
/// per <c>itemtypes/</c> asset). <see cref="Registries.Entities.EntityRegistry"/> discovers implementors
/// while registering a mod's classes (<c>RegisterAll</c>) and registers each returned def into
/// <see cref="ExDefinitions"/> for injection.
/// <para>
/// As with blocks, a plain item that uses the vanilla <c>Item</c> class (no mod class to hang the
/// definition on) is authored by a dedicated stand-alone provider class - any concrete class carrying a
/// static <c>Definitions</c> factory is discovered, because it is never instantiated (see
/// <see cref="ExDefinitions.ItemDefinitionsOf"/>).
/// </para>
/// </summary>
public interface IExItemDefProvider
{
  /// <summary>Builds this class's itemtype definition(s) in <paramref name="domain"/> (the mod id).</summary>
  static abstract IEnumerable<ExItemDef> Definitions(string domain);
}
