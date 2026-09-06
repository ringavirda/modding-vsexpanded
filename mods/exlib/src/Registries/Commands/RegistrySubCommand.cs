using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Registries;

/// <summary>
/// A <c>/exmod &lt;name&gt;</c> sub-command over one keyed registry: no argument lists every code with
/// <see cref="Describe"/>, a code with no further words shows that entry, and any further words are
/// handed to <see cref="Set"/> to interpret (Config's <c>value [newvalue]</c>, Recipes'
/// <c>level</c>...). Derive once - supplying the registry's codes/lookup and the four per-command lang
/// keys and message builders - to get <c>/exmod &lt;name&gt; [code [...]]</c> with no scaffold of its
/// own. <see cref="ConfigSubCommand"/> and <see cref="RecipesSubCommand"/> derive from this;
/// <see cref="MeasureSubCommand"/> does not - it is bound to one preference, not a registry of many,
/// and has no code argument to resolve.
/// </summary>
public abstract class RegistrySubCommand<T> : IExSubCommand
  where T : class {
  private readonly System.Func<IEnumerable<string>> _codes;
  private readonly System.Func<string, T?> _resolve;
  private readonly string _descriptionKey;

  /// <param name="name">The sub-command word (e.g. <c>"config"</c>), attached under <c>exmod</c>.</param>
  /// <param name="descriptionKey">Lang key for <c>WithDescription</c>.</param>
  /// <param name="codes">Every registered code, for the no-argument list.</param>
  /// <param name="resolve">Looks up one entry by code; null for an unregistered code.</param>
  protected RegistrySubCommand(
    string name,
    string descriptionKey,
    System.Func<IEnumerable<string>> codes,
    System.Func<string, T?> resolve
  ) {
    Name = name;
    _descriptionKey = descriptionKey;
    _codes = codes;
    _resolve = resolve;
  }

  /// <summary>The sub-command word.</summary>
  public string Name { get; }

  public string ParentName => "exmod";

  /// <summary>The api <see cref="Register"/> was called with, for a <see cref="Set"/> override that
  /// needs more than the registry (e.g. broadcasting a change to connected clients).</summary>
  protected ICoreAPI Api { get; private set; } = null!;

  /// <summary>One line describing <paramref name="entry"/>, used for the no-argument list.</summary>
  protected abstract string Describe(T entry);

  /// <summary>
  /// Handles a resolved <paramref name="entry"/> against the words typed after its code: empty for
  /// "show this entry", one or more to set something. Owns every per-command lang key and message,
  /// including "show" - there is no separate show hook, since what "show" prints (a single status
  /// line, a full value dump, ...) differs enough between commands that forcing one shape here would
  /// only get in the way.
  /// </summary>
  protected abstract TextCommandResult Set(T entry, string[] args);

  /// <summary>Lang key for "no code was given and nothing is registered".</summary>
  protected abstract string NoneRegisteredKey { get; }

  /// <summary>Lang key for the list's header line, printed above one <see cref="Describe"/> line per
  /// registered code.</summary>
  protected abstract string ListHeaderKey { get; }

  /// <summary>Lang key for "that code is not registered": <c>{0}</c> the code typed, <c>{1}</c> the
  /// known codes joined with <c>", "</c>.</summary>
  protected abstract string UnknownCodeKey { get; }

  public void Register(ICoreAPI api, Mod mod, IChatCommand parent) {
    Api = api;
    var parsers = api.ChatCommands.Parsers;

    parent
      .BeginSubCommand(Name)
      .WithDescription(Lang.Get(_descriptionKey))
      .WithArgs(parsers.OptionalWord("code"), parsers.OptionalAll("rest"))
      .HandleWith(OnCommand)
      .EndSubCommand();
  }

  private TextCommandResult OnCommand(TextCommandCallingArgs args) =>
    Dispatch(args[0] as string, args[1] as string);

  /// <summary>
  /// The command's logic with the framework's fluent arg parsing already stripped away: <c>code</c>
  /// is the first word (null for the bare command), <c>rest</c> everything typed after it. Internal
  /// rather than private so a test can drive list/show/set/unknown-code without building a fake
  /// <see cref="TextCommandCallingArgs"/>.
  /// </summary>
  internal TextCommandResult Dispatch(string? code, string? rest) {
    if (code is not string c)
      return TextCommandResult.Success(ListEntries());

    T? entry = _resolve(c);
    if (entry == null)
      return TextCommandResult.Error(Lang.Get(UnknownCodeKey, c, KnownCodes()));

    string trimmed = rest?.Trim() ?? "";
    string[] tail =
      trimmed.Length == 0
        ? []
        : trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return Set(entry, tail);
  }

  private string ListEntries() {
    string[] codes = _codes().OrderBy(c => c).ToArray();
    if (codes.Length == 0)
      return Lang.Get(NoneRegisteredKey);

    var lines = codes.Select(c =>
      _resolve(c) is { } entry ? $"  {Describe(entry)}" : c
    );
    return Lang.Get(ListHeaderKey) + "\n" + string.Join("\n", lines);
  }

  private string KnownCodes() => string.Join(", ", _codes().OrderBy(c => c));
}
