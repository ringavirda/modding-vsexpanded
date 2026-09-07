using System;
using System.Collections.Generic;
using System.Linq;
using JsonPatch.Operations;
using JsonPatch.Operations.Abstractions;
using Newtonsoft.Json.Linq;
using Tavis;

namespace ExpandedLib.Verify;

/// <summary>
/// Replays every <c>patches/*.json</c> file the loaded mod domains ship, the same way
/// <c>Vintagestory.ServerMods.NoObf.ModJsonPatchLoader</c> drives it: same dependsOn/condition
/// filtering, same file resolution (including the <c>*</c> wildcard), same <see cref="Tavis"/>
/// operations applied against the real parsed target. The patched result is written back into the
/// <see cref="AssetStore"/> so later checks (recipe/handbook codes) see the mod's assets exactly as
/// the game would after loading finished - not the pre-patch JSON on disk.
/// </summary>
public static class PatchChecker {
  /// <summary>
  /// Applies every patch in <paramref name="patchDomains"/>'s <c>patches/</c> folders against
  /// <paramref name="store"/>, mutating it in place, and returns the findings raised along the way.
  /// </summary>
  /// <param name="store">Every domain's parsed assets - game, the mod under test, and any
  /// <c>--mods</c> - mutated with each successfully-applied patch's result.</param>
  /// <param name="patchDomains">The domains whose own <c>patches/</c> folder is replayed - normally
  /// the mod under test plus every <c>--mods</c> entry; vanilla's own domains never carry one.</param>
  /// <param name="loadedModIds">Every mod id considered "installed" for a patch's <c>dependsOn</c>
  /// check - the mod under test, every <c>--mods</c> entry, and the game's own built-in ids.</param>
  /// <param name="codeDomains">Domains belonging to a mod that ships a compiled assembly. A patch
  /// target missing from one of these is reported as unverifiable rather than as an error: that mod
  /// can inject the asset at load time (see <see cref="ModSource.ShipsCode"/>), and this tool reads
  /// only what is on disk.</param>
  public static List<Finding> Run(
    AssetStore store,
    IReadOnlyList<string> patchDomains,
    IReadOnlySet<string> loadedModIds,
    IReadOnlySet<string> codeDomains
  ) {
    var findings = new List<Finding>();

    foreach (
      string domain in patchDomains.OrderBy(d => d, StringComparer.Ordinal)
    ) {
      foreach (
        (string path, JToken json) in store
          .Under(domain, "patches/")
          .OrderBy(f => f.Path, StringComparer.Ordinal)
      ) {
        if (json is not JArray patches)
          continue;

        for (int i = 0; i < patches.Count; i++) {
          if (patches[i] is not JObject spec)
            continue;
          ApplyOne(
            store,
            domain,
            path,
            i,
            spec,
            loadedModIds,
            codeDomains,
            findings
          );
        }
      }
    }

    return findings;
  }

  private static void ApplyOne(
    AssetStore store,
    string sourceDomain,
    string sourcePath,
    int index,
    JObject spec,
    IReadOnlySet<string> loadedModIds,
    IReadOnlySet<string> codeDomains,
    List<Finding> findings
  ) {
    string source = $"{sourceDomain}:{sourcePath} [{index}]";

    if ((bool?)spec["enabled"] == false)
      return;

    // condition.when has no reliable headless answer - no worldConfig exists outside a running
    // game - so a conditioned patch is never applied, only reported, exactly like an unmet
    // dependsOn: safer to under-check than to guess a condition true or false and report a defect
    // (or a clean bill) that a real load would not have produced.
    if (spec["condition"] is JObject condition) {
      string? when = (string?)condition["when"];
      findings.Add(
        new Finding(
          FindingLevel.Info,
          "PatchCondition",
          source,
          null,
          $"condition.when '{when}' has no known setter outside a running game - patch not evaluated"
        )
      );
      return;
    }

    if (spec["dependsOn"] is JArray dependsOn) {
      var softMissing = new List<string>();
      foreach (JToken dep in dependsOn) {
        string? modid = (string?)dep["modid"];
        if (string.IsNullOrEmpty(modid))
          continue;
        bool invert = (bool?)dep["invert"] == true;
        bool loaded = loadedModIds.Contains(modid!.ToLowerInvariant());
        bool satisfied = loaded ^ invert;
        if (!satisfied) {
          // Invert-blocked ("only if NOT installed", and it is) is a deliberate compatibility
          // guard working as intended - nothing to report. A missing positive dependency is a
          // dependency this run cannot supply, so it is worth naming, but never as an error: the
          // patch may be entirely correct once that mod is actually loaded alongside it.
          if (invert)
            return;
          softMissing.Add(modid);
        }
      }
      if (softMissing.Count > 0) {
        foreach (string modid in softMissing)
          findings.Add(
            new Finding(
              FindingLevel.Info,
              "PatchDependsOn",
              source,
              null,
              $"dependsOn '{modid}' not loaded (pass it with --mods to check this patch) - not evaluated"
            )
          );
        return;
      }
    }

    string? op = (string?)spec["op"];
    string? file = (string?)spec["file"];
    string? path = (string?)spec["path"];
    if (op == null || file == null) {
      findings.Add(
        new Finding(
          FindingLevel.Error,
          "PatchOp",
          source,
          null,
          "patch has no 'op' or 'file'"
        )
      );
      return;
    }

    var (targetDomain, targetPath) = SplitFile(file);

    if (targetPath.EndsWith('*')) {
      string prefix = targetPath[..^1];
      foreach (
        (string foundPath, JToken _) in store
          .Under(targetDomain, prefix)
          .ToList()
      )
        ApplyToTarget(
          store,
          targetDomain,
          foundPath,
          op,
          path,
          spec,
          source,
          findings
        );
      // An empty wildcard match is not an error - GetMany simply returns nothing, and the game
      // does not log that as a problem either.
      return;
    }

    if (store.TryGet(targetDomain, targetPath) == null) {
      bool injectable = codeDomains.Contains(targetDomain);
      findings.Add(
        new Finding(
          injectable ? FindingLevel.Info : FindingLevel.Error,
          "PatchTarget",
          source,
          null,
          injectable
            ? $"target file not on disk: {targetDomain}:{targetPath} - that mod ships code and can "
              + "inject it at load, so this patch is not verifiable headlessly"
            : $"target file not found: {targetDomain}:{targetPath}"
        )
      );
      return;
    }

    ApplyToTarget(
      store,
      targetDomain,
      targetPath,
      op,
      path,
      spec,
      source,
      findings
    );
  }

  private static void ApplyToTarget(
    AssetStore store,
    string domain,
    string path,
    string op,
    string? pointerPath,
    JObject spec,
    string source,
    List<Finding> findings
  ) {
    JToken? target = store.TryGet(domain, path);
    if (target == null)
      return; // Only reachable from the wildcard branch, whose matches always exist.

    Operation? operation = BuildOperation(op, pointerPath, spec);
    if (operation == null) {
      findings.Add(
        new Finding(
          FindingLevel.Error,
          "PatchOp",
          source,
          null,
          $"unknown or incomplete op '{op}' (target {domain}:{path})"
        )
      );
      return;
    }

    JToken working = target.DeepClone();
    var document = new PatchDocument(operation);
    try {
      document.ApplyTo(working);
      store.Set(domain, path, working);
    } catch (PathNotFoundException e) {
      findings.Add(
        new Finding(
          FindingLevel.Error,
          "PatchOp",
          source,
          null,
          $"path '{pointerPath}' does not exist in {domain}:{path}: {e.Message}"
        )
      );
    } catch (Exception e) {
      findings.Add(
        new Finding(
          FindingLevel.Error,
          "PatchOp",
          source,
          null,
          $"failed applying to {domain}:{path}: {e.Message}"
        )
      );
    }
  }

  // Mirrors EnumJsonPatchOp -> Operation exactly the way ModJsonPatchLoader.CreateOperation does.
  // Null return means the op is unknown or is missing the value/from it requires.
  private static Operation? BuildOperation(
    string op,
    string? pointerPath,
    JObject spec
  ) {
    if (pointerPath == null)
      return null;
    var pointer = new JsonPointer(pointerPath);
    JToken? value = spec["value"];
    string? from = (string?)spec["fromPath"] ?? (string?)spec["from"];

    return op.ToLowerInvariant() switch {
      "add" => value == null
        ? null
        : new AddReplaceOperation { Path = pointer, Value = value },
      "addeach" => value == null
        ? null
        : new AddEachOperation { Path = pointer, Value = value },
      "addmerge" => value == null
        ? null
        : new AddMergeOperation { Path = pointer, Value = value },
      "remove" => new RemoveOperation { Path = pointer },
      "replace" => value == null
        ? null
        : new ReplaceOperation { Path = pointer, Value = value },
      "copy" => from == null
        ? null
        : new CopyOperation {
          Path = pointer,
          FromPath = new JsonPointer(from),
        },
      "move" => from == null
        ? null
        : new MoveOperation {
          Path = pointer,
          FromPath = new JsonPointer(from),
        },
      _ => null,
    };
  }

  // AssetLocation's own default domain is "game" for a bare path (no colon); .json is appended if
  // missing, the same normalisation WithPathAppendixOnce("./json") gives a patch's own File.
  private static (string Domain, string Path) SplitFile(string file) {
    int colon = file.IndexOf(':');
    string domain = colon < 0 ? "game" : file[..colon];
    string path = colon < 0 ? file : file[(colon + 1)..];
    if (
      !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
      && !path.EndsWith('*')
    )
      path += ".json";
    return (domain.ToLowerInvariant(), path.ToLowerInvariant());
  }
}
