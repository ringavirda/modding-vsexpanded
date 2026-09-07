using System;

namespace ExpandedLib.Config;

/// <summary>
/// Stamped by <c>ExConfigGenerator</c> on every accessor class it emits for an
/// <see cref="ExConfigRegisterAttribute"/>-decorated config POCO. Never applied by hand: it exists so
/// <see cref="ExConfig.LoadAll"/> can find every generated accessor in an assembly by reflection and
/// call its <c>Load(ICoreAPI)</c>, without a hand-maintained list of config types.
/// </summary>
[AttributeUsage(
  AttributeTargets.Class,
  AllowMultiple = false,
  Inherited = false
)]
public sealed class ExConfigAccessorAttribute : Attribute {
  /// <param name="configType">The config POCO this accessor was generated for.</param>
  public ExConfigAccessorAttribute(Type configType) => ConfigType = configType;

  /// <summary>The config POCO this accessor was generated for.</summary>
  public Type ConfigType { get; }
}
