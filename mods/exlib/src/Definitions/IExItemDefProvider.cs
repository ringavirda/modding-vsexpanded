using System.Collections.Generic;

namespace ExpandedLib.Definitions;

/// <summary>
/// The item-side sibling of <see cref="IExBlockDefProvider"/>: implemented by a class that authors its
/// own code-first item definitions, one <see cref="ExItemDef"/> per <c>itemtypes/</c> asset, co-located
/// with the class they configure. <see cref="Registries.EntityRegistry"/> discovers implementors
/// while registering a mod's classes (<c>RegisterAll</c>) and registers each returned def into
/// <see cref="ExDefinitions"/> for injection. A plain item on the vanilla <c>Item</c> class has no mod
/// class to hang the definition on and uses a stand-alone provider class instead; any concrete class
/// carrying a static <c>Definitions</c> factory is discovered without being instantiated (see
/// <see cref="ExDefinitions.ItemDefinitionsOf"/>).
/// </summary>
public interface IExItemDefProvider {
  /// <summary>Builds this class's itemtype definition(s) in <paramref name="domain"/> (the mod id).</summary>
  static abstract IEnumerable<ExItemDef> Definitions(string domain);
}
