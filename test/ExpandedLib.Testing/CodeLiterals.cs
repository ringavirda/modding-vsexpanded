using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;

namespace ExpandedLib.Testing;

/// <summary>
/// Finds domain-qualified block-code string literals in a mod's source that <b>can never resolve</b>,
/// because they name a variant-grouped block by its bare base code.
///
/// <para>
/// <b>This is the same defect as a bare lang key, one layer down.</b> A block that declares any
/// variant group is never itself placeable - every real instance carries a suffix - so
/// <c>GetBlock("iwex:molten-barrel")</c> returns <c>null</c> once the barrel gains
/// <c>construction(plated|cast)</c>. And because the call sites are all written defensively
/// (<c>if (barrel != null)</c>), the failure is <b>silent</b>: the barrel simply stops being an
/// accepted container and nothing anywhere says so.
/// </para>
///
/// <para>
/// Only <b>domain-qualified</b> literals are scanned, which deliberately excludes the definitions
/// themselves: a def names its own code un-qualified (<c>Create(domain, "molten-barrel", …)</c>), as do
/// its internal selectors (<c>Handbook("flywheel-*")</c>, <c>ShapeByType("*-normal-ns", …)</c>). Those
/// are the source of truth and must not be flagged.
/// </para>
/// </summary>
public static class CodeLiterals
{
  private static readonly Regex Qualified =
    new(@"""(?<domain>[a-z]+):(?<path>[a-zA-Z0-9_\-/\.\*]+)""", RegexOptions.Compiled);

  /// <summary>A line whose literal addresses an <b>asset</b> (shape, texture, animation) rather than a
  /// block. Both use <c>domain:path</c>, and a shape path can equal a base code exactly.</summary>
  private static readonly Regex AssetReference =
    new(
      @"\b(Shape|Texture|Animation|Sound)\w*\s*\(|\b(shapes|textures)/",
      RegexOptions.Compiled
    );

  /// <summary>Base codes in <paramref name="domain"/> that declare at least one variant group, so
  /// their bare code can never name a placed block.</summary>
  private static HashSet<string> VariantGroupedBaseCodes(string domain, Assembly asm) =>
    DefinitionGoldens
      .Collect(domain, asm)
      .OfType<ExBlockDef>()
      .Where(d => d.ToJson()["variantgroups"] is JArray g && g.Count > 0)
      .Select(d => d.Code)
      .ToHashSet(StringComparer.Ordinal);

  /// <summary>
  /// Every <c>file:line</c> holding a literal that names a variant-grouped block by its bare code.
  /// Empty means none. <paramref name="srcDirRepoRelative"/> is the mod's own source root.
  /// </summary>
  public static IReadOnlyList<string> UnresolvableBareCodes(
    string domain,
    Assembly asm,
    string srcDirRepoRelative
  )
  {
    HashSet<string> bare = VariantGroupedBaseCodes(domain, asm);
    if (bare.Count == 0)
      return [];

    string root = DefinitionGoldens.SolutionRelative(srcDirRepoRelative);
    var findings = new List<string>();

    foreach (
      string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
    )
    {
      string norm = file.Replace('\\', '/');
      // Build output and generated tables are not authored source.
      if (norm.Contains("/bin/") || norm.Contains("/obj/") || norm.Contains("/Generated/"))
        continue;

      string[] lines = File.ReadAllLines(file);
      for (int i = 0; i < lines.Length; i++)
      {
        // A shape or texture path uses the same `domain:path` syntax as a block code, and one can
        // coincide with a base code exactly - `.ShapeByTypePerOrientation("lpex:manualfluidpump", 0)`
        // names assets/lpex/shapes/manualfluidpump.json, not a block. Asset references are addressed by
        // the method they are passed to, so filter on that rather than on the literal.
        if (AssetReference.IsMatch(lines[i]))
          continue;

        foreach (Match m in Qualified.Matches(lines[i]))
        {
          if (m.Groups["domain"].Value != domain)
            continue;

          string path = m.Groups["path"].Value;
          if (!bare.Contains(path))
            continue;

          findings.Add(
            $"{norm[(norm.IndexOf("/src/", StringComparison.Ordinal) + 1)..]}:{i + 1}"
              + $"  \"{domain}:{path}\" - {path} declares variant groups, so this bare code resolves "
              + "to null. Name a variant, or use a wildcard if any will do."
          );
        }
      }
    }

    return findings;
  }
}
