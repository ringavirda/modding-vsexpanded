using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries.Config;

/// <summary>
/// Shared loader and saver for a mod's JSON gameplay tunables. The config POCO's property
/// initialisers are the defaults; a static accessor owns one of these stores. <see cref="Load"/>
/// reads this mod's own section of the shared <see cref="ExConfigDocument"/> under
/// <c>ModConfig/&lt;fileName&gt;</c>, falls back to defaults when absent or invalid, applies any
/// crossed <see cref="ExConfigMigration"/> and stamps the running mod version, so the section is
/// created on first run and gains newly added keys on update.
/// </summary>
/// <typeparam name="TConfig">The mod's config POCO; needs a parameterless constructor whose property
/// initialisers define the defaults, and must record the version it was written under.</typeparam>
public sealed class ExConfigRegister<TConfig> : IExConfigAccess
  where TConfig : class, IExVersionedConfig, new() {
  private readonly string _fileName;
  private readonly string _modId;
  private readonly ExConfigMigration[] _migrations;
  private ICoreAPI? _api;
  private PropertyInfo[]? _editableProps;

  /// <summary>The live config. Holds the coded defaults until <see cref="Load"/> runs (and after a
  /// failed load), so accessors are always safe to read.</summary>
  public TConfig Config { get; private set; } = new();

  /// <summary>The owning mod id (also the code typed in <c>/exmod config &lt;mod&gt;</c>).</summary>
  public string ModId => _modId;

  /// <summary>The config file this store reads/writes under <c>ModConfig</c>.</summary>
  public string FileName => _fileName;

  /// <summary>Former per-mod file names this config was carried over from. On <see cref="Load"/>, if
  /// this mod's section is absent but one of these still exists in <c>ModConfig</c>, its contents
  /// become the section and the old file is renamed to <c>&lt;name&gt;.migrated</c> (first match
  /// wins). Set by the generated accessor from the attribute's <c>LegacyFileNames</c>.</summary>
  public IReadOnlyList<string> LegacyFileNames { get; init; } = [];

  /// <summary>Mod ids whose section of <see cref="FileName"/> this store now owns - the mods it was
  /// renamed from or absorbed. Carried over on <see cref="Load"/>. Set by the generated accessor from
  /// the attribute's <c>LegacySectionIds</c>.</summary>
  public IReadOnlyList<string> LegacySectionIds { get; init; } = [];

  /// <param name="fileName">Shared config document under the game's <c>ModConfig</c> folder (e.g.
  /// <c>"ex_values.json"</c>); this store owns the <paramref name="modId"/> section of it.</param>
  /// <param name="modId">Owning mod id; resolves the running version and tags log lines.</param>
  /// <param name="migrations">Version-driven default resets (see <see cref="ExConfigMigration"/>).</param>
  public ExConfigRegister(
    string fileName,
    string modId,
    params ExConfigMigration[] migrations
  ) {
    _fileName = fileName;
    _modId = modId;
    _migrations = migrations ?? [];
  }

  /// <summary>Loads the config (falling back to defaults), applies version-change resets and stamps
  /// the current mod version. Call once during mod startup, before any value is read. Runs on either
  /// side and each reads its own local copy, but only the server writes the file back: in
  /// singleplayer both sides load this store in one process against one file and would race.</summary>
  public void Load(ICoreAPI api) {
    _api = api;
    var doc = ExConfigDocument.ForFile(api, _fileName);
    // One-time carry-over of the old per-mod file into this mod's section (no-op once it exists).
    doc.FoldLegacy(_modId, LegacyFileNames);
    // And of a section this mod used to be keyed under, for a rename or a merge. Runs after the file
    // fold so a legacy file that already became this section is what the legacy sections merge into.
    doc.FoldLegacySections(_modId, LegacySectionIds);

    // GetSection returns null on a missing or unreadable section, so a corrupt file or a fresh
    // install starts from the coded defaults without throwing.
    TConfig config = doc.GetSection<TConfig>(_modId) ?? new TConfig();

    string current =
      api.ModLoader.GetMod(_modId)?.Info?.Version ?? string.Empty;
    ApplyMigrations(config, current, api.Logger);
    Sanitize(config, api.Logger);
    config.ConfigVersion = current;

    Config = config;
    // Server only: in singleplayer both sides load this register in the same process against the
    // same file, and two writers race over it. The server's copy is the authority anyway.
    if (api.Side == EnumAppSide.Server)
      Save();
  }

  /// <summary>
  /// Resets edited values that would break the sim back to their coded defaults: any numeric tunable
  /// that is NaN, infinite or outside its <see cref="ExConfigRangeAttribute"/> bounds (default:
  /// non-negative), and any reference-typed value set to null. Every reset is named in a warning log
  /// line. Complex and collection properties carry their own repair.
  /// </summary>
  private void Sanitize(TConfig config, ILogger logger) {
    var defaults = new TConfig();
    var reset = new List<string>();

    foreach (
      var p in typeof(TConfig).GetProperties(
        BindingFlags.Public | BindingFlags.Instance
      )
    ) {
      if (
        !p.CanRead
        || !p.CanWrite
        || p.Name == nameof(IExVersionedConfig.ConfigVersion)
      )
        continue;

      object? value = p.GetValue(config);
      bool bad = AsNumber(value) is double n
        ? !InNumericRange(p, n)
        // A nulled-out reference value (a string, or a collection such as a recipe catalogue) would
        // NRE its reader. Guarded on a non-null default so a legitimately optional null stays.
        : value is null
          && !p.PropertyType.IsValueType
          && p.GetValue(defaults) != null;

      if (bad) {
        p.SetValue(config, p.GetValue(defaults));
        reset.Add(p.Name);
      }
    }

    if (reset.Count > 0)
      logger.Warning(
        "[{0}] Config {1}: reset invalid value(s) to defaults: {2}.",
        _modId,
        _fileName,
        string.Join(", ", reset)
      );
  }

  /// <summary>Resets the fields named by every migration whose <see cref="ExConfigMigration.ToVersion"/>
  /// is crossed by the upgrade from the file's stamped version to the running build.</summary>
  private void ApplyMigrations(TConfig config, string current, ILogger logger) {
    string stored = config.ConfigVersion ?? string.Empty;
    if (stored == current)
      return; // same build, nothing to migrate

    var defaults = new TConfig();
    foreach (var m in _migrations.OrderBy(m => ParseVersion(m.ToVersion))) {
      bool crossed =
        CompareVersions(m.ToVersion, stored) > 0
        && CompareVersions(m.ToVersion, current) <= 0
        && (
          m.FromVersion == null || CompareVersions(stored, m.FromVersion) >= 0
        );
      if (crossed)
        ResetFields(config, defaults, m, logger);
    }
  }

  private void ResetFields(
    TConfig config,
    TConfig defaults,
    ExConfigMigration m,
    ILogger logger
  ) {
    var writable = typeof(TConfig)
      .GetProperties(BindingFlags.Public | BindingFlags.Instance)
      .Where(p =>
        p.CanRead
        && p.CanWrite
        && p.Name != nameof(IExVersionedConfig.ConfigVersion)
      );

    IEnumerable<PropertyInfo> toReset;
    if (m.ResetFields is { Length: > 0 }) {
      var wanted = new HashSet<string>(m.ResetFields, StringComparer.Ordinal);
      var byName = writable.ToDictionary(p => p.Name);
      foreach (var name in wanted.Where(n => !byName.ContainsKey(n)))
        logger.Warning(
          "[{0}] Config migration to {1} names unknown field '{2}'; skipped.",
          _modId,
          m.ToVersion,
          name
        );
      toReset = byName.Values.Where(p => wanted.Contains(p.Name));
    } else {
      toReset = writable;
    }

    foreach (var p in toReset)
      p.SetValue(config, p.GetValue(defaults));

    logger.Notification(
      "[{0}] Config: reset {1} to defaults on upgrade to {2}.",
      _modId,
      m.ResetFields is { Length: > 0 }
        ? string.Join(", ", m.ResetFields)
        : "all values",
      m.ToVersion
    );
  }

  /// <summary>Writes the live <see cref="Config"/> back to <c>ModConfig/&lt;fileName&gt;</c>. Called at
  /// the end of <see cref="Load"/>, and public so a runtime command can persist a change made through
  /// <see cref="Config"/>.</summary>
  public void Save() {
    if (_api == null)
      return;
    try {
      var doc = ExConfigDocument.ForFile(_api, _fileName);
      doc.SetSection(_modId, Config);
      doc.Flush();
    } catch (Exception e) {
      _api.Logger.Warning(
        "[{0}] Could not write {1}. {2}",
        _modId,
        _fileName,
        e
      );
    }
  }

  #region IExConfigAccess (runtime /exmod config editing)
  /// <summary>The read-write tunables of simple type (number, bool, string) this store exposes to the
  /// generic config command; the version stamp and any complex or collection property are excluded.
  /// Cached after first use.</summary>
  private PropertyInfo[] EditableProps =>
    _editableProps ??= typeof(TConfig)
      .GetProperties(BindingFlags.Public | BindingFlags.Instance)
      .Where(p =>
        p.CanRead
        && p.CanWrite
        && p.Name != nameof(IExVersionedConfig.ConfigVersion)
        && IsEditableType(p.PropertyType)
      )
      .ToArray();

  /// <inheritdoc/>
  public IReadOnlyList<string> ValueNames =>
    EditableProps.Select(p => p.Name).ToArray();

  /// <inheritdoc/>
  public bool TryGet(string name, out string canonicalName, out string value) {
    var p = FindProp(name);
    if (p == null) {
      canonicalName = name;
      value = string.Empty;
      return false;
    }

    canonicalName = p.Name;
    value = Format(p.GetValue(Config));
    return true;
  }

  /// <inheritdoc/>
  public ExConfigEditResult Set(string name, string raw) {
    var p = FindProp(name);
    if (p == null)
      return new ExConfigEditResult {
        Status = ExConfigEditStatus.UnknownValue,
        Name = name,
      };

    string oldValue = Format(p.GetValue(Config));

    if (!TryParse(p.PropertyType, raw, out object? parsed, out string expected))
      return new ExConfigEditResult {
        Status = ExConfigEditStatus.ParseFailed,
        Name = p.Name,
        OldValue = oldValue,
        Expected = expected,
      };

    if (AsNumber(parsed) is double n && !InNumericRange(p, n))
      return new ExConfigEditResult {
        Status = ExConfigEditStatus.OutOfRange,
        Name = p.Name,
        OldValue = oldValue,
        Range = FormatRange(p),
      };

    p.SetValue(Config, parsed);
    Save();
    return new ExConfigEditResult {
      Status = ExConfigEditStatus.Ok,
      Name = p.Name,
      OldValue = oldValue,
      NewValue = Format(parsed),
    };
  }

  private PropertyInfo? FindProp(string name) =>
    EditableProps.FirstOrDefault(p =>
      string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
    );

  private static bool IsEditableType(Type t) =>
    t == typeof(string)
    || t == typeof(bool)
    || t == typeof(int)
    || t == typeof(long)
    || t == typeof(float)
    || t == typeof(double);

  private static string Format(object? v) =>
    v switch {
      float f => f.ToString(CultureInfo.InvariantCulture),
      double d => d.ToString(CultureInfo.InvariantCulture),
      bool b => b ? "true" : "false",
      null => string.Empty,
      _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty,
    };

  /// <summary>Parses <paramref name="raw"/> into <paramref name="type"/> using invariant culture and
  /// lenient boolean words (true/on/yes/1, false/off/no/0). <paramref name="expected"/> is a short
  /// label of the accepted input for the error message.</summary>
  private static bool TryParse(
    Type type,
    string raw,
    out object? value,
    out string expected
  ) {
    value = null;

    if (type == typeof(string)) {
      expected = "text";
      value = raw;
      return true;
    }
    if (type == typeof(bool)) {
      expected = "true/false";
      switch (raw.Trim().ToLowerInvariant()) {
        case "true" or "on" or "yes" or "1":
          value = true;
          return true;
        case "false" or "off" or "no" or "0":
          value = false;
          return true;
        default:
          return false;
      }
    }
    if (type == typeof(int)) {
      expected = "whole number";
      if (
        int.TryParse(
          raw,
          NumberStyles.Integer,
          CultureInfo.InvariantCulture,
          out int i
        )
      ) {
        value = i;
        return true;
      }
      return false;
    }
    if (type == typeof(long)) {
      expected = "whole number";
      if (
        long.TryParse(
          raw,
          NumberStyles.Integer,
          CultureInfo.InvariantCulture,
          out long l
        )
      ) {
        value = l;
        return true;
      }
      return false;
    }
    if (type == typeof(float)) {
      expected = "number";
      if (
        float.TryParse(
          raw,
          NumberStyles.Float,
          CultureInfo.InvariantCulture,
          out float f
        )
      ) {
        value = f;
        return true;
      }
      return false;
    }
    if (type == typeof(double)) {
      expected = "number";
      if (
        double.TryParse(
          raw,
          NumberStyles.Float,
          CultureInfo.InvariantCulture,
          out double d
        )
      ) {
        value = d;
        return true;
      }
      return false;
    }

    expected = "value";
    return false;
  }

  /// <summary>The numeric value of <paramref name="v"/> as a <see cref="double"/>, or <c>null</c> for a
  /// non-numeric (string/bool) property.</summary>
  private static double? AsNumber(object? v) =>
    v switch {
      float f => f,
      double d => d,
      int i => i,
      long l => l,
      _ => null,
    };

  /// <summary>The inclusive <c>[min, max]</c> a numeric property accepts: its
  /// <see cref="ExConfigRangeAttribute"/> when present, else the baseline non-negative range
  /// <c>[0, +∞)</c>.</summary>
  private static (double Min, double Max) RangeOf(PropertyInfo p) {
    var attr = p.GetCustomAttribute<ExConfigRangeAttribute>();
    return attr != null ? (attr.Min, attr.Max) : (0d, double.PositiveInfinity);
  }

  /// <summary>Whether <paramref name="n"/> is finite and within the property's accepted range, so an
  /// edit cannot set a value the next load would reset.</summary>
  private static bool InNumericRange(PropertyInfo p, double n) {
    if (double.IsNaN(n) || double.IsInfinity(n))
      return false;
    var (min, max) = RangeOf(p);
    return n >= min && n <= max;
  }

  /// <summary>A compact, language-neutral description of the property's accepted range for the edit
  /// error: <c>"0..1"</c> for a bounded range, <c>"0+"</c> for a floor only. Avoids <c>&lt;</c>/<c>&gt;</c>
  /// for VTML safety.</summary>
  private static string FormatRange(PropertyInfo p) {
    var (min, max) = RangeOf(p);
    string lo = min.ToString(CultureInfo.InvariantCulture);
    return double.IsPositiveInfinity(max)
      ? $"{lo}+"
      : $"{lo}..{max.ToString(CultureInfo.InvariantCulture)}";
  }
  #endregion

  /// <summary>Parses a mod version (e.g. <c>"0.9.1"</c>, tolerating a <c>-prerelease</c> suffix) into a
  /// comparable <see cref="Version"/>; unparseable or empty versions sort lowest.</summary>
  private static Version ParseVersion(string? v) {
    if (string.IsNullOrWhiteSpace(v))
      return new Version(0, 0);
    int dash = v.IndexOf('-');
    if (dash >= 0)
      v = v[..dash];
    return Version.TryParse(v, out var parsed) ? parsed : new Version(0, 0);
  }

  private static int CompareVersions(string? a, string? b) =>
    ParseVersion(a).CompareTo(ParseVersion(b));
}
