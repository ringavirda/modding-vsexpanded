using System;

namespace ExpandedLib.Registries;

/// <summary>
/// Declares the asset domain an assembly's registered classes and code-first definitions are keyed
/// under, so <see cref="EntityRegistry.KeyFor"/> can resolve a key from a <see cref="Type"/> alone
/// rather than from the domain of whoever is asking. Assembly-level rather than a registration call
/// because it carries no load-order dependency: a key resolves correctly before the owning mod's
/// <c>Start</c> has run. <see cref="EntityRegistry.RegisterAll"/> records the same mapping as a
/// fallback for an assembly that declares none.
/// </summary>
/// <example><code>
/// [assembly: ExDomain("iiex")]
/// </code></example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ExDomainAttribute(string domain) : Attribute {
  /// <summary>The asset domain every registrable type in this assembly is keyed under - normally the
  /// mod id, and always the domain its <c>assets/&lt;domain&gt;/</c> tree ships as.</summary>
  public string Domain { get; } = domain;
}
