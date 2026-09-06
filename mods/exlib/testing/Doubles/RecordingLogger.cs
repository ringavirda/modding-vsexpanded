using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ExpandedLib.Testing;

/// <summary>
/// A real <see cref="ILogger"/> (no substitute framework involved) that keeps every entry it is
/// handed, formatted once through <see cref="System.String.Format(string, object[])"/> so a test
/// reads the same text a player would see in a log file. <see cref="TestWorld"/> wires one instance
/// as both <c>Api.Logger</c> and <c>World.Logger</c>; a test that wants to intercept a specific call
/// with NSubstitute's <c>Received()</c> can still replace either property with its own
/// <c>Substitute.For&lt;ILogger&gt;()</c>, since both properties live on substitute objects.
/// </summary>
public sealed class RecordingLogger : LoggerBase {
  /// <summary>Every entry logged so far, oldest first, format and args already merged.</summary>
  public IReadOnlyList<(EnumLogType Type, string Message)> Entries => _entries;

  private readonly List<(EnumLogType Type, string Message)> _entries = [];

  /// <summary>The message text of every <see cref="EnumLogType.Error"/> entry, oldest first.</summary>
  public IEnumerable<string> Errors => Select(EnumLogType.Error);

  /// <summary>The message text of every <see cref="EnumLogType.Warning"/> entry, oldest first.</summary>
  public IEnumerable<string> Warnings => Select(EnumLogType.Warning);

  private IEnumerable<string> Select(EnumLogType type) {
    foreach ((EnumLogType Type, string Message) entry in _entries)
      if (entry.Type == type)
        yield return entry.Message;
  }

  /// <summary>Empties the recorded entries, for a test that wants a clean slate mid-run.</summary>
  public void Clear() => _entries.Clear();

  /// <summary><see cref="LoggerBase"/>'s one abstract member: every other overload (the
  /// message-only ones, the exception ones) already forwards here.</summary>
  protected override void LogImpl(
    EnumLogType logType,
    string format,
    params object[] args
  ) =>
    _entries.Add(
      (logType, args is { Length: > 0 } ? string.Format(format, args) : format)
    );
}
