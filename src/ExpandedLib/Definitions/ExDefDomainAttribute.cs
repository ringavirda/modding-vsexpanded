using System;

namespace ExpandedLib.Definitions;

/// <summary>
/// Overrides the domain a definition provider's <c>Definitions(string)</c> factory is handed, so one
/// assembly can emit into several domains instead of only its own mod id. Without it a provider is
/// given the registering mod's domain, which is the common case and stays the default.
/// <para>
/// Needed for a mod merge: the absorbing assembly keeps emitting the absorbed mod's codes until they
/// are relocated, so the two halves can land separately rather than as one atomic cut. Also the
/// general form of what <c>MetalFamilyEmitter</c> already does - a metal is emitted into the domain its
/// molten item names, which need not be the folder it ships in.
/// </para>
/// </summary>
/// <example><code>
/// [ExDefDomain("iiex")]
/// public class CastPipeDefinitions : IExBlockDefProvider { }
/// </code></example>
[AttributeUsage(
  AttributeTargets.Class,
  AllowMultiple = false,
  Inherited = false
)]
public sealed class ExDefDomainAttribute(string domain) : Attribute {
  /// <summary>The domain this provider's definitions are created in, regardless of which mod's
  /// assembly scan discovered it.</summary>
  public string Domain { get; } = domain;
}
