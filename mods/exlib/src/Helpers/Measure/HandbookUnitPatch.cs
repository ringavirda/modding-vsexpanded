using System.ComponentModel;
using ExpandedLib.Helpers;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace ExpandedLib.Helpers;

/// <summary>
/// Makes handbook page text follow the active <see cref="ExMeasure.System"/>. Converts plain metric
/// unit mentions in the prose ("30 L", "2-4 atm", "160-220 °C") before the page builds its rich-text
/// components. Runs only in imperial mode; metric is the authored text and is left untouched.
/// <c>Init</c> runs only when the handbook loads its entries, so <see cref="Rebuild"/> forces that
/// reload when the player switches units mid-session.
/// </summary>
[HarmonyPatch(typeof(GuiHandbookTextPage), "Init")]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class HandbookUnitPatch {
  /// <summary>
  /// Rebuilds the survival handbook's pages so their prose re-converts to the current
  /// <see cref="ExMeasure.System"/>, taking the same <c>loadEntries</c> path as
  /// <c>.debug reloadhandbook</c>. Reflection is needed because the dialog instance and method are
  /// not public; a failure leaves the handbook on its previous units.
  /// </summary>
  public static void Rebuild(ICoreClientAPI capi) {
    try {
      var handbook = capi.ModLoader.GetModSystem<ModSystemSurvivalHandbook>();
      if (handbook == null)
        return;

      object? dialog = Traverse.Create(handbook).Field("dialog").GetValue();
      if (dialog == null)
        return;

      // loadEntries() re-creates every page, re-running each page's Init and this patch for the
      // now-active unit system.
      Traverse.Create(dialog).Method("loadEntries").GetValue();
    } catch {
      // A display refresh must not break the measure command.
    }
  }

  // Prefix so the text is converted before Init builds it into display components.
  public static void Prefix(GuiHandbookTextPage __instance) {
    if (ExMeasure.System != MeasurementSystem.Imperial)
      return;

    try {
      var field = Traverse.Create(__instance).Field("Text");
      string? raw = field.GetValue<string>();
      if (string.IsNullOrEmpty(raw))
        return;

      // Text may still hold the lang key, which Init would otherwise resolve itself. Re-resolving an
      // already-resolved string is a no-op.
      string resolved = Lang.Get(raw);
      string converted = ExMeasure.ConvertMetricText(resolved);
      if (converted != raw)
        field.SetValue(converted);
    } catch {
      // On failure the page keeps the authored metric text.
    }
  }
}
