using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ExpandedLib.Definitions;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Filler accounting for every furnace core layout: the cells a layout declares as
/// <c>exlib:structurefiller</c> must be exactly the cells its own parts emit through
/// <c>fillerOffsets</c>. A declared cell nothing fills is a hole no player can close, so the structure
/// never completes; a filled cell the layout does not declare drops a block outside the footprint.
/// <para>
/// Definition level on purpose: <c>StructureRig.Raise</c> stands a block up in every empty footprint
/// cell, fillers included, so a scenario completes a furnace nobody could build and this defect class is
/// invisible to it.
/// </para>
/// </summary>
public class FurnaceFillerAccountingTests {
  #region Harness

  private static readonly (string Domain, Assembly Assembly)[] Sources =
  [
    ("iiex", typeof(Recipes.Grid.FurnaceRecipeDefinitions).Assembly),
    ("exlib", typeof(ExCodes).Assembly),
  ];

  /// <summary>
  /// Every concrete variant code the mods emit, mapped to the def that emits it. A layout names a
  /// resolved code (<c>iiex:furnace-puddlinghearth-n</c>) while a def carries a family code plus variant
  /// groups, so the product is expanded here rather than matched by prefix - <c>furnace</c> alone is the
  /// code of every furnace part there is.
  /// </summary>
  private static readonly Lazy<IReadOnlyDictionary<string, JObject>> Defs = new(
    BuildConcreteDefs
  );

  private static IReadOnlyDictionary<string, JObject> BuildConcreteDefs() {
    var byCode = new Dictionary<string, JObject>(StringComparer.Ordinal);
    foreach ((string domain, Assembly asm) in Sources)
      foreach (IExDef def in DefinitionGoldens.Collect(domain, asm)) {
        // Recipe defs serialise as an array; indexing one by name throws.
        if (def.ToJson() is not JObject json)
          continue;
        if (json["code"]?.Value<string>() is not { } code)
          continue;
        foreach (string variant in Variants(code, json))
          byCode[$"{domain}:{variant}"] = json;
      }
    return byCode;
  }

  /// <summary>The cartesian product of a def's variant groups, in declaration order.</summary>
  private static IEnumerable<string> Variants(string code, JObject def) {
    IEnumerable<string> codes = [code];
    foreach (JToken group in def["variantgroups"] as JArray ?? []) {
      string[] states =
        (group["states"] as JArray)?.Select(s => s.Value<string>()!).ToArray()
        ?? [];
      if (states.Length == 0)
        continue;
      codes = codes.SelectMany(c => states.Select(s => $"{c}-{s}")).ToArray();
    }
    return codes;
  }

  private readonly record struct Cell(int X, int Y, int Z, string Code) {
    public Vec3i Offset => new(X, Y, Z);

    public override string ToString() => $"({X},{Y},{Z})";
  }

  /// <summary>Every cell of a layout, with its legend code resolved through the block numbers.</summary>
  private static IReadOnlyList<Cell> Cells(JObject def) {
    JObject layout = (JObject)def["attributes"]!["multiblockStructure"]!;
    var codes = ((JObject)layout["blockNumbers"]!)
      .Properties()
      .ToDictionary(p => p.Value.Value<int>(), p => p.Name);
    return
    [
      .. ((JArray)layout["offsets"]!).Select(o => new Cell(
        o["x"]!.Value<int>(),
        o["y"]!.Value<int>(),
        o["z"]!.Value<int>(),
        codes[o["w"]!.Value<int>()]
      )),
    ];
  }

  /// <summary>
  /// The def a layout cell's code names, or null when the code is vanilla's, an alternation group or a
  /// wildcard resolving to several defs that do not agree - none of which a mod def can be held to. The
  /// <c>side</c> variant of the matched code comes back with it, since a part's fillers turn with it.
  /// </summary>
  private static (JObject Def, string Side)? PartAt(
    Cell cell,
    IReadOnlyDictionary<string, JObject> defs
  ) {
    if (defs.TryGetValue(cell.Code, out JObject? exact))
      return (exact, SideOf(cell.Code));
    if (!cell.Code.Contains('*', StringComparison.Ordinal))
      return null;

    var pattern = new Regex(
      "^" + string.Join(".*", cell.Code.Split('*').Select(Regex.Escape)) + "$"
    );
    var matches = defs.Where(e => pattern.IsMatch(e.Key)).ToArray();
    if (matches.Length == 0)
      return null;
    // A wildcarded cell that could resolve to parts with different fillers has no single answer, and the
    // ambiguity is itself the defect. Reported as an unaccountable cell rather than guessed at.
    string[] emitted = matches
      .Select(m => m.Value["attributes"]?["fillerOffsets"]?.ToString() ?? "")
      .Distinct(StringComparer.Ordinal)
      .ToArray();
    return emitted.Length == 1
      ? (matches[0].Value, SideOf(matches[0].Key))
      : null;
  }

  /// <summary>The trailing orientation segment of a resolved code, or north when it carries none.</summary>
  private static string SideOf(string code) {
    string last = code[(code.LastIndexOf('-') + 1)..];
    return last is "n" or "e" or "s" or "w" ? last : "n";
  }

  private static IReadOnlyList<Cell> Declared(IReadOnlyList<Cell> cells) =>
    [.. cells.Where(c => c.Code == ExCodes.Filler)];

  /// <summary>
  /// Every cell the layout's parts fill: each part's own <c>fillerOffsets</c>, rotated by the angle its
  /// legend code names and offset by the cell the part stands in.
  /// </summary>
  private static IReadOnlyList<Cell> Supplied(IReadOnlyList<Cell> cells) {
    var filled = new List<Cell>();
    foreach (Cell cell in cells) {
      if (PartAt(cell, Defs.Value) is not { } found)
        continue;
      (JObject part, string side) = found;
      int angle = ExOrientation.AngleFromSide(side);
      foreach (
        JToken off in part["attributes"]?["fillerOffsets"] as JArray ?? []
      ) {
        Vec3i local = ExOrientation.RotateOffset(
          off["x"]!.Value<int>(),
          off["y"]!.Value<int>(),
          off["z"]!.Value<int>(),
          angle
        );
        filled.Add(
          new Cell(
            cell.X + local.X,
            cell.Y + local.Y,
            cell.Z + local.Z,
            cell.Code
          )
        );
      }
    }
    return filled;
  }

  /// <summary>A furnace core def, found by the family member its <c>type</c> variant names.</summary>
  private static JObject Core(string type) =>
    Defs
      .Value.First(e =>
        e.Key.StartsWith($"iiex:furnace-{type}-", StringComparison.Ordinal)
      )
      .Value;

  private static string Render(IEnumerable<Vec3i> cells) =>
    string.Join(
      " ",
      cells
        .OrderBy(c => c.X)
        .ThenBy(c => c.Y)
        .ThenBy(c => c.Z)
        .Select(c => $"({c.X},{c.Y},{c.Z})")
    );

  #endregion

  #region Declared fillers match supplied fillers

  [Theory]
  [InlineData("puddlingcore")]
  [InlineData("heatingcore")]
  [InlineData("blastcore")]
  [InlineData("cupolacore")]
  public void Every_declared_filler_is_supplied_by_a_part(string type) {
    IReadOnlyList<Cell> cells = Cells(Core(type));
    var supplied = Supplied(cells).Select(c => c.Offset).ToHashSet();

    Vec3i[] orphans =
    [
      .. Declared(cells)
        .Select(c => c.Offset)
        .Where(o => !supplied.Contains(o)),
    ];

    Assert.True(
      orphans.Length == 0,
      $"{type} declares filler cells no part fills, so the structure can never complete: "
        + Render(orphans)
    );
  }

  [Theory]
  [InlineData("puddlingcore")]
  [InlineData("heatingcore")]
  [InlineData("blastcore")]
  [InlineData("cupolacore")]
  public void Every_supplied_filler_is_declared_by_the_layout(string type) {
    IReadOnlyList<Cell> cells = Cells(Core(type));
    var declared = Declared(cells).Select(c => c.Offset).ToHashSet();

    string[] strays =
    [
      .. Supplied(cells)
        .Where(c => !declared.Contains(c.Offset))
        .Select(c => $"{c} from {c.Code}")
        .Distinct(StringComparer.Ordinal),
    ];

    Assert.True(
      strays.Length == 0,
      $"{type} parts fill cells the layout does not declare, so a filler lands outside the "
        + "footprint: "
        + string.Join(" ", strays)
    );
  }

  /// <summary>
  /// The premise of both directions above: a layout whose parts emit nothing would pass them while
  /// examining nothing at all.
  /// </summary>
  [Theory]
  [InlineData("puddlingcore")]
  [InlineData("heatingcore")]
  [InlineData("blastcore")]
  [InlineData("cupolacore")]
  public void Every_furnace_core_has_fillers_to_account_for(string type) {
    IReadOnlyList<Cell> cells = Cells(Core(type));

    Assert.NotEmpty(Declared(cells));
    Assert.NotEmpty(Supplied(cells));
  }

  #endregion
}
