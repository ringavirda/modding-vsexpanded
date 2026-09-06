using System;

namespace ExpandedLib.Registries;

/// <summary>
/// Declares an assembly as a module: an extension to the framework, discovered by
/// <see cref="ExModules"/> and driven through the lifecycle of the mod named in
/// <see cref="Host"/>. Distinct from <see cref="ExDomainAttribute"/>, which names the asset domain a
/// module's own classes are keyed under and is optional - a module with none keys under its host's
/// mod id instead.
/// </summary>
/// <example><code>
/// [assembly: ExModule("industry")]
/// </code></example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ExModuleAttribute(string id) : Attribute {
  /// <summary>Unique across every module loaded in the process, lower-case.</summary>
  public string Id { get; } = id;

  /// <summary>The mod id whose lifecycle drives this module. Defaults to <c>"exlib"</c>, the
  /// framework itself; a mod hosting its own modules sets its own id.</summary>
  public string Host { get; set; } = "exlib";

  /// <summary>Module ids this one runs after, within the same host. A missing or cyclic requirement
  /// excludes the module from its host's ordered set; see <see cref="ExModules.Order"/>.</summary>
  public string[] Requires { get; set; } = [];

  /// <summary>When true, the host patches this assembly's uncategorised <c>[HarmonyPatch]</c>
  /// classes under <see cref="ExModuleInfo.HarmonyId"/> at <c>Start</c>, and unpatches them at
  /// <c>Dispose</c>.</summary>
  public bool PatchHarmony { get; set; }
}
