using System.ComponentModel;
using System.Linq;
using ExpandedLib.Registries;
using ExpandedLib.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Registries;

/// <summary>
/// Adds <c>/exmod config [&lt;mod&gt; [&lt;value&gt; [&lt;new&gt;]]]</c>: the mod-agnostic switch for the
/// gameplay tunables a mod exposes through <see cref="ExConfigProfiles"/> (a config marked
/// <c>[ExConfigRegister(..., Manageable = true)]</c>). With no argument it lists the registered configs,
/// with a mod code that mod's values and their settings, with a value name that one value; a new value
/// is parsed, validated, set and persisted. Changes apply without a world reload because everything
/// reads through the live accessor. Server-side: the config is host-authoritative and the
/// <c>/exmod</c> root requires <c>controlserver</c>.
/// </summary>
[SubCommandRegister(Side = EnumAppSide.Server)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ConfigSubCommand : RegistrySubCommand<IExConfigAccess> {
  public ConfigSubCommand()
    : base(
      "config",
      "exlib:command-config-desc",
      () => ExConfigProfiles.Codes,
      code => ExConfigProfiles.TryGet(code, out var config) ? config : null
    ) { }

  protected override string NoneRegisteredKey => "exlib:command-config-none";
  protected override string ListHeaderKey => "exlib:command-config-list";
  protected override string UnknownCodeKey => "exlib:command-config-unknown";

  protected override string Describe(IExConfigAccess config) =>
    $"{config.ModId} ({config.FileName})";

  protected override TextCommandResult Set(
    IExConfigAccess config,
    string[] args
  ) {
    if (args.Length == 0)
      return TextCommandResult.Success(ListValues(config));

    string name = args[0];

    // Read: print the current value.
    if (args.Length < 2) {
      if (!config.TryGet(name, out var canonical, out var value))
        return Err("exlib:command-config-novalue", name, config.ModId);
      return Ok("exlib:command-config-current", canonical, value);
    }

    // Write: parse, validate, set and persist (applied immediately).
    string raw = args[1];
    var result = config.Set(name, raw);
    if (result.Status == ExConfigEditStatus.Ok)
      // Push the change to connected players so it takes effect for them without a reconnect.
      Api.ModLoader.GetModSystem<ExConfigSyncModSystem>()
        ?.BroadcastSection(config);

    return result.Status switch {
      ExConfigEditStatus.Ok => Ok(
        "exlib:command-config-set",
        result.Name,
        result.OldValue,
        result.NewValue
      ),
      ExConfigEditStatus.ParseFailed => Err(
        "exlib:command-config-parsefail",
        raw,
        result.Name,
        result.Expected
      ),
      ExConfigEditStatus.OutOfRange => Err(
        "exlib:command-config-range",
        result.Name,
        result.Range
      ),
      _ => Err("exlib:command-config-novalue", name, config.ModId),
    };
  }

  // Hand the framework the lang key plus args (StatusMessage = key, MessageParams = arguments) so the
  // server resolves it once in the caller's language, via Lang.GetL(langCode, StatusMessage,
  // MessageParams). Two constraints on the lang strings: a pre-formatted single-line result is re-run
  // through Lang.Get, so a literal ':' is read back as "domain:key"; and the client wraps
  // CommandSuccess/Notification lines in a <font> tag before VTML parsing, so a bare '<' or '>' breaks
  // the tag stream and blanks the line. Keep both characters out of the command-result strings.
  private static TextCommandResult Ok(string key, params object?[] args) =>
    new() {
      Status = EnumCommandStatus.Success,
      StatusMessage = key,
      MessageParams = args,
    };

  private static TextCommandResult Err(string key, params object?[] args) =>
    new() {
      Status = EnumCommandStatus.Error,
      StatusMessage = key,
      MessageParams = args,
    };

  private static string ListValues(IExConfigAccess config) {
    var lines = config.ValueNames.Select(n =>
      config.TryGet(n, out var canonical, out var value)
        ? $"  {canonical} = {value}"
        : n
    );
    return Lang.Get("exlib:command-config-values", config.ModId)
      + "\n"
      + string.Join("\n", lines);
  }
}
