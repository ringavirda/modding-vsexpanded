using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ExpandedLib.Checks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Registries;

/// <summary>
/// Adds <c>/exmod verify [&lt;mod&gt;]</c>: runs every content check in <see cref="ExlibChecks"/>
/// against the live game state and reports it on demand, the same checks
/// <c>ExpandedLibModSystem.AssetsFinalize</c> already ran once at load (unless
/// <c>ExlibConfig.RunChecksOnLoad</c> was off). With no argument, every domain exlib scopes in on
/// its own (<see cref="AssetCheckSource.Domains"/> - itself plus its dependents) is checked; with a
/// mod code, that one domain is checked regardless of whether exlib depends on it, so long as some
/// loaded mod answers to it. Prints how many checks ran and how many errors they found in total,
/// then the first ten error lines - the full list still goes to the log via the same
/// <see cref="ExlibChecks.Log"/> call.
/// </summary>
[SubCommandRegister(Side = EnumAppSide.Server)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class VerifySubCommand : IExSubCommand {
  public string ParentName => "exmod";

  public void Register(ICoreAPI api, Mod mod, IChatCommand parent) {
    parent
      .BeginSubCommand("verify")
      .WithDescription(Lang.Get("exlib:command-verify-desc"))
      .WithArgs(api.ChatCommands.Parsers.OptionalWord("domain"))
      .HandleWith(args => Dispatch(api, args[0] as string))
      .EndSubCommand();
  }

  /// <summary>
  /// The command's logic with the framework's fluent arg parsing already stripped away: mirrors
  /// <see cref="RegistrySubCommand{T}.Dispatch"/>. Internal rather than private so a test can drive
  /// the domain and no-domain cases without building a fake <see cref="TextCommandCallingArgs"/>.
  /// </summary>
  internal static TextCommandResult Dispatch(ICoreAPI api, string? domain) {
    domain = domain?.ToLowerInvariant();

    var source = new AssetCheckSource(api);
    IReadOnlyList<CheckResult> results;

    if (domain != null) {
      List<string> loaded =
      [
        .. api.ModLoader.Mods.Select(m => m.Info.ModID).OrderBy(d => d),
      ];
      if (!loaded.Contains(domain))
        return TextCommandResult.Error(
          Lang.Get(
            "exlib:command-verify-unknown",
            domain,
            string.Join(", ", loaded)
          )
        );
      results = ExlibChecks.For(source, domain);
    } else {
      results = ExlibChecks.All(source);
    }

    // Logged in full regardless of what the chat window can show, so a modder chasing the tenth
    // error onward reads the server log rather than re-running with a narrower domain.
    ExlibChecks.Log(api.Logger, results);

    List<string> errors = [.. results.SelectMany(r => r.Errors)];
    var lines = new List<string>
    {
      Lang.Get("exlib:command-verify-summary", results.Count, errors.Count),
    };
    lines.AddRange(errors.Take(10));
    if (errors.Count > 10)
      lines.Add(Lang.Get("exlib:command-verify-more", errors.Count - 10));

    return errors.Count == 0
      ? TextCommandResult.Success(string.Join("\n", lines))
      : TextCommandResult.Error(string.Join("\n", lines));
  }
}
