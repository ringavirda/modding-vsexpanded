using Vintagestory.API.Common;

namespace ExpandedLib.Helpers;

/// <summary>
/// Cross-mod hook for asking whether a tool-mold type is currently disabled by a config gate, without
/// the asker referencing the mod that owns the molds. The molten subsystem (iwex) needs to purge a
/// disabled mold from a pedestal, but the tool molds and their availability flags live in the
/// dependent mod (smex); smex registers its predicate here at startup and iwex queries it.
/// <para>When no predicate is registered (smex absent), nothing is ever reported disabled.</para>
/// </summary>
public static class ExMoldGate
{
  private static System.Func<AssetLocation?, bool>? _isDisabled;

  /// <summary>Registers the predicate that decides whether a mold <c>code</c> is config-disabled.
  /// Called once by the mold-owning mod during startup.</summary>
  public static void RegisterIsDisabled(System.Func<AssetLocation?, bool> isDisabled) =>
    _isDisabled = isDisabled;

  /// <summary>Whether the tool mold identified by <paramref name="code"/> is currently disabled.
  /// False when no gate is registered.</summary>
  public static bool IsToolMoldDisabled(AssetLocation? code) =>
    _isDisabled?.Invoke(code) ?? false;
}
