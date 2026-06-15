using HarmonyLib;
using PipesAndPowerExpanded.Helpers;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace PipesAndPowerExpanded.Patches;

/// <summary>
/// Makes handbook page text follow the active <see cref="ExMeasure.System"/>: plain metric unit
/// mentions in the prose ("30 L", "2-4 atm", "160-220 °C") are converted to the chosen system just
/// before the page builds its rich-text components, so existing metric handbook text reads in
/// imperial without being rewritten. Hooks the survival handbook text page's <c>Init</c> (where the
/// <c>Text</c> field is turned into the displayed components).
/// <para>
/// Only runs in imperial mode (metric is the authored text, left untouched). The page may store the
/// lang <em>key</em> in <c>Text</c> and resolve it inside <c>Init</c>, so we resolve it ourselves
/// via <see cref="Lang.Get(string,object[])"/> first - re-resolving an already-resolved string is a
/// no-op. The conversion is applied once when a page is initialised (after the player's saved choice
/// is applied on join); switching units mid-session shows in the handbook the next time pages are
/// rebuilt (re-opening the game / changing language). The look-at HUD updates instantly.
/// </para>
/// </summary>
[HarmonyPatch(typeof(GuiHandbookTextPage), "Init")]
public static class HandbookUnitPatch
{
  // Prefix so the text is converted before Init builds it into display components.
  public static void Prefix(GuiHandbookTextPage __instance)
  {
    if (ExMeasure.System != MeasurementSystem.Imperial)
      return;

    try
    {
      var field = Traverse.Create(__instance).Field("Text");
      string? raw = field.GetValue<string>();
      if (string.IsNullOrEmpty(raw))
        return;

      // Resolve in case Text still holds the lang key (Init would otherwise resolve it itself).
      string resolved = Lang.Get(raw);
      string converted = ExMeasure.ConvertMetricText(resolved);
      if (converted != raw)
        field.SetValue(converted);
    }
    catch
    {
      // Never let a display tweak break the handbook; fall back to the authored metric text.
    }
  }
}
