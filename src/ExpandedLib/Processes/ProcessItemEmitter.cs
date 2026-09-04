using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Definitions;
using Vintagestory.API.Common;

namespace ExpandedLib.Processes;

/// <summary>
/// Builds an <see cref="ExItemDef"/> for every stopping point in the stage catalogue, injected through the
/// same path <c>MetalFamilyEmitter</c> uses for metal families. A stage that names a <c>code</c> is a thing
/// the piece becomes, so the product catalogue falls out of the declaration instead of being hand-authored
/// beside it. See docs/design/mechanics/process-extension.md.
/// </summary>
public static class ProcessItemEmitter {
  /// <summary>Domain we never generate into: injecting an itemtype there would replace one of the base
  /// game's own items. A declaration naming a vanilla code is wiring it up, never building it.</summary>
  private const string VanillaDomain = "game";

  // What a declaration that specifies nothing still gets. A generated item has to load and be reachable,
  // or a modder's first attempt is a crash rather than an untextured cube.
  private const string FallbackShape = "game:item/ingot";
  private const int DefaultStackSize = 64;

  /// <summary>
  /// Every item def the catalogue calls for, one per stopping point that opts in. Codes are deduplicated:
  /// a fork can reach one product down two branches, and the object loader would reject a duplicate
  /// itemtype. <paramref name="skipped"/> collects one human-readable message per stage that named a code
  /// and got no item, so a declaration that silently built nothing is visible in the log.
  /// </summary>
  public static IEnumerable<ExItemDef> Emit(
    IEnumerable<ProcessRoute> routes,
    out List<string> skipped
  ) {
    skipped = [];
    return
    [
      .. Buildable(routes, skipped)
        .Select(b => Build(b.Route, b.Stage, b.Code)),
    ];
  }

  /// <summary>The codes <see cref="Emit"/> would build, so a guard can assert no declaration names
  /// something unbuildable and nothing hand-authored collides with a generated code.</summary>
  public static IEnumerable<string> GeneratedCodes(
    IEnumerable<ProcessRoute> routes
  ) => [.. Buildable(routes, []).Select(b => b.Code.ToString())];

  // Every stopping point that opts in and resolves, deduplicated: a fork can reach one product down two
  // branches, and the object loader would reject a duplicate itemtype.
  private static List<(
    ProcessRoute Route,
    ProcessStage Stage,
    AssetLocation Code
  )> Buildable(IEnumerable<ProcessRoute> routes, List<string> skipped) {
    var buildable = new List<(ProcessRoute, ProcessStage, AssetLocation)>();
    var seen = new HashSet<string>();

    foreach (ProcessRoute route in routes)
      foreach (ProcessStage stage in route.Stages) {
        // A render-only intermediate is a state, not a thing; an opted-out one exists already.
        if (stage.Code == null || !stage.Generate)
          continue;

        string where = $"{route.Family} {stage.Thickness}";
        AssetLocation? code = Resolve(stage.Code);
        if (code == null || string.IsNullOrWhiteSpace(code.Path)) {
          skipped.Add($"{where}: '{stage.Code}' is not a usable item code");
          continue;
        }
        if (code.Domain == VanillaDomain) {
          skipped.Add(
            $"{where}: '{stage.Code}' is a vanilla code, so it is wired up rather than built; declare "
              + "\"generate\": false to say so explicitly"
          );
          continue;
        }
        if (!seen.Add(code.ToString()))
          continue;

        buildable.Add((route, stage, code));
      }

    return buildable;
  }

  // A code is a modder's free text, so a malformed one is skipped rather than thrown through the load.
  private static AssetLocation? Resolve(string code) {
    try {
      return new AssetLocation(code);
    } catch {
      return null;
    }
  }

  private static ExItemDef Build(
    ProcessRoute route,
    ProcessStage stage,
    AssetLocation code
  ) {
    ExItemDef def = ExItemDef
      .Create(code.Domain, code.Path)
      .MaxStackSize(DefaultStackSize)
      .CreativeCommon("*");

    // The family's shape file, drawn at this stage's element. A stage with no element is the whole file,
    // which is the right convention for a finished product with a model of its own.
    def = def.Shape(route.Shape ?? FallbackShape);
    if (route.Shape != null && stage.Element != null)
      def = def.ShapeSelectiveElements(stage.Element);

    return def;
  }
}
