using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries.Config;

/// <summary>
/// Shared loader/saver for a mod's JSON gameplay tunables. Replaces the per-mod copy of the same
/// load-or-default-then-write-back logic: each mod keeps a plain config POCO (with property
/// defaults) plus a thin static accessor that owns one of these stores and exposes
/// <see cref="Config"/> through read-only properties.
/// <para>
/// On <see cref="Load"/> the store reads <c>ModConfig/&lt;fileName&gt;</c> (falling back to defaults
/// if absent or invalid), applies any <see cref="ExConfigMigration"/> triggered by a version change
/// since the file was last written, stamps the running mod version, and writes the file back - so it
/// is created on first run and gains newly added keys on update.
/// </para>
/// </summary>
/// <typeparam name="TConfig">The mod's config POCO; needs a parameterless constructor whose property
/// initialisers define the defaults, and must record the version it was written under.</typeparam>
public sealed class ExConfigRegister<TConfig>
  where TConfig : class, IExVersionedConfig, new()
{
  private readonly string _fileName;
  private readonly string _modId;
  private readonly ExConfigMigration[] _migrations;
  private ICoreAPI? _api;

  /// <summary>The live config. Holds the coded defaults until <see cref="Load"/> runs (and after a
  /// failed load), so accessors are always safe to read.</summary>
  public TConfig Config { get; private set; } = new();

  /// <param name="fileName">Config file name written under the game's <c>ModConfig</c> folder
  /// (e.g. <c>"ppex.json"</c>).</param>
  /// <param name="modId">The owning mod id - used to resolve the running version and to tag log lines.</param>
  /// <param name="migrations">Version-driven default resets (see <see cref="ExConfigMigration"/>).</param>
  public ExConfigRegister(
    string fileName,
    string modId,
    params ExConfigMigration[] migrations
  )
  {
    _fileName = fileName;
    _modId = modId;
    _migrations = migrations ?? [];
  }

  /// <summary>Loads the config (falling back to defaults), applies any version-change resets, stamps
  /// the current mod version and writes the file back. Call once during mod startup, before any
  /// value is read. Safe on either side; each side reads its own local copy.</summary>
  public void Load(ICoreAPI api)
  {
    _api = api;

    TConfig config;
    try
    {
      config = api.LoadModConfig<TConfig>(_fileName) ?? new TConfig();
    }
    catch (Exception e)
    {
      api.Logger.Error(
        "[{0}] Failed to read {1}; using defaults. {2}",
        _modId,
        _fileName,
        e
      );
      config = new TConfig();
    }

    string current =
      api.ModLoader.GetMod(_modId)?.Info?.Version ?? string.Empty;
    ApplyMigrations(config, current, api.Logger);
    config.ConfigVersion = current;

    Config = config;
    Save();
  }

  /// <summary>Resets the fields named by every migration whose <see cref="ExConfigMigration.ToVersion"/>
  /// is crossed by the upgrade from the file's stamped version to the running build.</summary>
  private void ApplyMigrations(TConfig config, string current, ILogger logger)
  {
    string stored = config.ConfigVersion ?? string.Empty;
    if (stored == current)
      return; // same build - nothing to migrate.

    var defaults = new TConfig();
    foreach (var m in _migrations.OrderBy(m => ParseVersion(m.ToVersion)))
    {
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
  )
  {
    var writable = typeof(TConfig)
      .GetProperties(BindingFlags.Public | BindingFlags.Instance)
      .Where(p =>
        p.CanRead
        && p.CanWrite
        && p.Name != nameof(IExVersionedConfig.ConfigVersion)
      );

    IEnumerable<PropertyInfo> toReset;
    if (m.ResetFields is { Length: > 0 })
    {
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
    }
    else
    {
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

  private void Save()
  {
    try
    {
      _api?.StoreModConfig(Config, _fileName);
    }
    catch (Exception e)
    {
      _api?.Logger.Warning(
        "[{0}] Could not write {1}. {2}",
        _modId,
        _fileName,
        e
      );
    }
  }

  /// <summary>Parses a mod version (e.g. <c>"0.9.1"</c>, tolerating a <c>-prerelease</c> suffix) into a
  /// comparable <see cref="Version"/>; unparseable or empty versions sort lowest.</summary>
  private static Version ParseVersion(string? v)
  {
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
