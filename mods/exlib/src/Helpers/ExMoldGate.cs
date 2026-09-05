using Vintagestory.API.Common;

namespace ExpandedLib.Helpers;

/// <summary>
/// Cross-mod hook for asking whether a tool-mold type is currently disabled by a config gate, without
/// the asker referencing the mod that owns the molds. The tool molds and their availability flags live
/// in siex, which registers its predicate here at startup; iiex queries it to purge a disabled mold
/// from a pedestal. With no predicate registered, nothing is reported disabled.
/// </summary>
public static class ExMoldGate {
  private static System.Func<AssetLocation?, bool>? _isDisabled;

  /// <summary>Registers the predicate that decides whether a mold <c>code</c> is config-disabled.
  /// Called once by the mold-owning mod during startup.</summary>
  public static void RegisterIsDisabled(
    System.Func<AssetLocation?, bool> isDisabled
  ) => _isDisabled = isDisabled;

  /// <summary>Whether the tool mold identified by <paramref name="code"/> is currently disabled.
  /// False when no gate is registered.</summary>
  public static bool IsToolMoldDisabled(AssetLocation? code) =>
    _isDisabled?.Invoke(code) ?? false;
}
