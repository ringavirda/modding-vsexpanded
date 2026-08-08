using System.Collections.Generic;

namespace ExpandedLib.Definitions;

/// <summary>
/// Implemented by a block class that authors its own code-first definition(s), co-located with the
/// class they configure instead of in a separate central list.
/// <see cref="Registries.Entities.EntityRegistry"/> discovers implementors while registering a mod's
/// classes (<c>RegisterAll</c>) and registers each returned <see cref="ExBlockDef"/> into
/// <see cref="ExDefinitions"/> for injection - so declaring the def(s) and getting them injected is a
/// single, local act on the block class.
/// <para>
/// A class returns <b>one or more</b> defs because one C# class often backs several blocktype assets
/// (e.g. the pipe class backs <c>pipe/straight</c>, <c>pipe/bend</c>, … - same <c>code</c>, distinct
/// asset paths). The def(s) are built by a <b>static</b> factory (they exist before any block instance;
/// the instances are created from them). The <paramref name="domain"/> passed in is the mod id, so
/// <see cref="ExBlockDef.Create"/> and the type-safe <c>Class&lt;T&gt;()</c> bind to the right asset
/// domain / registered-class key.
/// </para>
/// </summary>
public interface IExBlockDefProvider
{
  /// <summary>Builds this class's blocktype definition(s) in <paramref name="domain"/> (the mod id).</summary>
  static abstract IEnumerable<ExBlockDef> Definitions(string domain);
}
