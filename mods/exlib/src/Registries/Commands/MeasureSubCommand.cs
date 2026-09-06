using System.ComponentModel;
using System.Linq;
using ExpandedLib.Helpers;
using ExpandedLib.Registries;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Registries;

/// <summary>
/// Attaches <c>.exmod measure [metric|imperial]</c> to the library's shared <c>.exmod</c> root: shows
/// or, with an argument, sets the per-player display unit system, validated against the preference's
/// options and persisted through <see cref="ExPreferences"/>. The preference itself and its effect on
/// the active display unit system live in <see cref="MeasurePreference"/>. Client-only, since the unit
/// system is a client-side display setting. The per-preference display strings
/// (<c>command-measure-desc</c>, <c>pref-measure-label</c>, <c>pref-measure-{value}</c>) and the
/// generic result strings (<c>command-pref-current</c>/<c>-set</c>/<c>-invalid</c>) live in the
/// <c>exlib</c> domain.
/// </summary>
[SubCommandRegister(Side = EnumAppSide.Client)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MeasureSubCommand : IExSubCommand {
  public string ParentName => "exmod";

  public void Register(ICoreAPI api, Mod mod, IChatCommand parent) {
    var capi = (ICoreClientAPI)api;
    var pref = ExPreferences.Find("measure") ?? new MeasurePreference();
    string domain = mod.Info.ModID;

    parent
      .BeginSubCommand(pref.Key)
      .WithDescription(Lang.Get(domain + ":command-" + pref.Key + "-desc"))
      .WithArgs(capi.ChatCommands.Parsers.OptionalWord("value"))
      .HandleWith(args => Dispatch(capi, domain, pref, args[0] as string))
      .EndSubCommand();
  }

  /// <summary>
  /// The command's logic with the framework's fluent arg parsing already stripped away: mirrors
  /// <see cref="RegistrySubCommand{T}.Dispatch"/>. Internal rather than private so a test can drive
  /// it without building a fake <see cref="TextCommandCallingArgs"/>.
  /// </summary>
  internal static TextCommandResult Dispatch(
    ICoreClientAPI api,
    string domain,
    IExPreference pref,
    string? rawWord
  ) {
    string uid = api.World.Player.PlayerUID;
    string? word = rawWord?.ToLowerInvariant();
    string label = Lang.Get(domain + ":pref-" + pref.Key + "-label");

    // No argument: report the current setting.
    if (string.IsNullOrEmpty(word))
      return TextCommandResult.Success(
        Lang.Get(
          "exlib:command-pref-current",
          label,
          ValueLabel(domain, pref, ExPreferences.GetForPlayer(uid, pref.Key))
        )
      );

    if (!pref.Options.Contains(word))
      return TextCommandResult.Error(
        Lang.Get(
          "exlib:command-pref-invalid",
          word,
          label,
          string.Join(", ", pref.Options)
        )
      );

    string previous = ExPreferences.GetForPlayer(uid, pref.Key);
    ExPreferences.SetForPlayer(uid, pref.Key, word);

    // Handbook prose is unit-converted only when its pages are built, so a mid-session switch needs
    // the pages rebuilt to re-read the now-active system (the look-at HUD already updates live).
    if (previous != word)
      HandbookUnitPatch.Rebuild(api);

    return TextCommandResult.Success(
      Lang.Get("exlib:command-pref-set", label, ValueLabel(domain, pref, word))
    );
  }

  private static string ValueLabel(
    string domain,
    IExPreference pref,
    string value
  ) => Lang.Get(domain + ":pref-" + pref.Key + "-" + value);
}
