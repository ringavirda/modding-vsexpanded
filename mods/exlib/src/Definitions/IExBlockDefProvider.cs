using System.Collections.Generic;

namespace ExpandedLib.Definitions;

/// <summary>
/// Implemented by a block class that authors its own code-first definitions, co-located with the class
/// they configure. <see cref="Registries.EntityRegistry"/> discovers implementors while
/// registering a mod's classes (<c>RegisterAll</c>) and registers each returned
/// <see cref="ExBlockDef"/> into <see cref="ExDefinitions"/> for injection. A class may return several
/// defs because one C# class can back several blocktype assets (the pipe class backs
/// <c>pipe/straight</c>, <c>pipe/bend</c> and so on: same <c>code</c>, distinct asset paths). The
/// factory is static because the defs exist before any block instance is created from them.
/// </summary>
public interface IExBlockDefProvider {
  /// <summary>Builds this class's blocktype definitions in <paramref name="domain"/> (the mod id), which
  /// binds <see cref="ExBlockDef.Create"/> and <c>Class&lt;T&gt;()</c> to the right asset domain and
  /// registered-class key.</summary>
  static abstract IEnumerable<ExBlockDef> Definitions(string domain);
}
