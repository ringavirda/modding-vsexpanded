using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;
using Cake.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace CakeBuild;

public static class Program
{
  public static int Main(string[] args)
  {
    return new CakeHost().UseContext<BuildContext>().Run(args);
  }
}

/// <summary>One buildable mod project in the monorepo. <paramref name="ModFolder"/> is the mod's own
/// folder under <c>mods/</c> (exlib/iiex/siex); <paramref name="Folder"/> is the project name the
/// csproj/modinfo file inside <c>mods/&lt;ModFolder&gt;/src/</c> is stamped with.</summary>
public record ModProject(string ModFolder, string Folder, string ModId, string Version);

/// <summary>A supported game version to publish for: its TFM, the game version stamped into the
/// packaged modinfo, and whether it's the current (non-legacy) target. Keep in sync with the version
/// manifest in mods/Directory.Build.props (Cake can't read MSBuild, so this is the one duplication -
/// same as the CI workflows).</summary>
public record GameTarget(string Tfm, string GameVersion, bool IsCurrent);

public class BuildContext : FrostingContext
{
  // Build order matters: exlib first (the shared lib every mod references), then iiex (the iron
  // tier, which owns the base pipe block, the networks and the steam plant), and finally siex (the
  // steel tier and the high-pressure leaves, both built on iiex's bases). ModFolder is the mod's own
  // folder under mods/; Folder is the project name the csproj/modinfo inside its src/ carries.
  public static readonly (string ModFolder, string Folder)[] ProjectFolders =
  [
    ("exlib", "ExpandedLib"),
    ("iiex", "IronIndustryExpanded"),
    ("siex", "SteelIndustryExpanded"),
  ];

  // Every supported game version. The legacy ones (IsCurrent=false) build with -p:Legacy=true and
  // land in a per-TFM output path; their packaged modinfo gets its game dependency rewritten.
  public static readonly GameTarget[] GameTargets =
  [
    new GameTarget("net10.0", "1.22.0", true),
    new GameTarget("net8.0", "1.21.0", false),
    new GameTarget("net7.0", "1.20.0", false),
  ];

  // The game dependency the source modinfo.json files declare (= the current version's floor).
  public const string SourceGameVersion = "1.22.0";

  public string BuildConfiguration { get; }
  public bool SkipJsonValidation { get; }
  public List<ModProject> Projects { get; } = [];

  public BuildContext(ICakeContext context)
    : base(context)
  {
    BuildConfiguration = context.Argument("configuration", "Release");
    SkipJsonValidation = context.Argument("skipJsonValidation", false);

    foreach (var (modFolder, folder) in ProjectFolders)
    {
      var modInfo = context.DeserializeJsonFromFile<ModInfo>(
        $"../../mods/{modFolder}/src/modinfo.json"
      );
      Projects.Add(
        new ModProject(modFolder, folder, modInfo.ModID, modInfo.Version)
      );
    }
  }

  /// <summary>The publish output for a project+target. The current version uses the flat
  /// Mods/mod path; legacy targets append their TFM (see the mod csproj OutputPath).</summary>
  public string PublishDir(ModProject project, GameTarget target) =>
    target.IsCurrent
      ? $"../../mods/{project.ModFolder}/src/bin/{BuildConfiguration}/Mods/mod/publish"
      : $"../../mods/{project.ModFolder}/src/bin/{BuildConfiguration}/{target.Tfm}/Mods/mod/publish";
}

[TaskName("ValidateJson")]
public sealed class ValidateJsonTask : FrostingTask<BuildContext>
{
  public override void Run(BuildContext context)
  {
    if (context.SkipJsonValidation)
      return;

    foreach (var project in context.Projects)
    {
      var jsonFiles = context.GetFiles(
        $"../../mods/{project.ModFolder}/assets/**/*.json"
      );
      foreach (var file in jsonFiles)
      {
        try
        {
          JToken.Parse(File.ReadAllText(file.FullPath));
        }
        catch (JsonException ex)
        {
          throw new Exception(
            $"Validation failed for JSON file: {file.FullPath}{Environment.NewLine}{ex.Message}",
            ex
          );
        }
      }
    }
  }
}

[TaskName("Build")]
[IsDependentOn(typeof(ValidateJsonTask))]
public sealed class BuildTask : FrostingTask<BuildContext>
{
  public override void Run(BuildContext context)
  {
    foreach (var project in context.Projects)
    {
      string csproj =
        $"../../mods/{project.ModFolder}/src/{project.Folder}.csproj";
      // Wipe the whole bin so stale per-version outputs can't leak into a package.
      string binDir =
        $"../../mods/{project.ModFolder}/src/bin/{context.BuildConfiguration}";
      context.EnsureDirectoryExists(binDir);
      context.CleanDirectory(binDir);

      foreach (var target in BuildContext.GameTargets)
      {
        context.DotNetPublish(
          csproj,
          new DotNetPublishSettings
          {
            Configuration = context.BuildConfiguration,
            Framework = target.Tfm,
            // -p:Legacy=true makes the mod multi-target so the legacy TFMs exist; harmless for the
            // current one (it stays the flat, non-legacy output).
            MSBuildSettings = new DotNetMSBuildSettings().WithProperty(
              "Legacy",
              "true"
            ),
          }
        );

        // ExpandedLib.Industry is not its own ModProject - it is a second assembly inside exlib's
        // zip rather than a mod of its own - so it has no entry in Projects and nothing above
        // builds it. (It does ship as its own NuGet package; that is dotnet pack's business.) Its
        // OutputPath mirrors ExpandedLib.csproj's exactly (same mod-output/publish folder per
        // target), so publishing it here lands exlib.industry.dll right beside exlib.dll with no
        // extra copy step.
        if (project.ModFolder == "exlib")
        {
          context.DotNetPublish(
            "../../mods/exlib/industry/ExpandedLib.Industry.csproj",
            new DotNetPublishSettings
            {
              Configuration = context.BuildConfiguration,
              Framework = target.Tfm,
              MSBuildSettings = new DotNetMSBuildSettings().WithProperty(
                "Legacy",
                "true"
              ),
            }
          );
        }
      }
    }
  }
}

[TaskName("Package")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PackageTask : FrostingTask<BuildContext>
{
  public override void Run(BuildContext context)
  {
    context.EnsureDirectoryExists("../../dist/Releases");
    context.CleanDirectory("../../dist/Releases");

    // One archive per (game version, mod), grouped into a per-version folder. Legacy
    // targets get a trailing game-version suffix so the files are distinguishable; the
    // current version stays unsuffixed:
    //   dist/Releases/<gameVersion>/<modid>_<modVersion>.zip            (current)
    //   dist/Releases/<gameVersion>/<modid>_<modVersion>_<gameVersion>.zip (legacy)
    foreach (var target in BuildContext.GameTargets)
    {
      foreach (var project in context.Projects)
      {
        string stageDir = $"../../dist/Releases/{target.GameVersion}/{project.ModId}";
        context.EnsureDirectoryExists(stageDir);

        context.CopyFiles($"{context.PublishDir(project, target)}/*", stageDir);
        // Copy assets from the per-target PUBLISH output, NOT from raw source: the csproj applies
        // per-game-version `Content Remove` filtering (e.g. legacy-only patches whose crushed codes
        // don't resolve on newer versions), and only the publish output reflects it. Copying source
        // assets here bypassed that and shipped both versions' files into every package, which
        // crashed clients on world-load when the wrong patch referenced a non-existent stack.
        if (context.DirectoryExists($"{context.PublishDir(project, target)}/assets"))
          context.CopyDirectory(
            $"{context.PublishDir(project, target)}/assets",
            $"{stageDir}/assets"
          );
        if (context.FileExists($"../../mods/{project.ModFolder}/src/modicon.png"))
          context.CopyFile(
            $"../../mods/{project.ModFolder}/src/modicon.png",
            $"{stageDir}/modicon.png"
          );
        // The licence travels inside the zip. A download from ModDB carries no repository context of
        // its own, so this is the only copy the person holding the file has.
        context.CopyFile("../../LICENSE", $"{stageDir}/LICENSE.txt");

        // Authoritative modinfo: the source declares the current game version, so point the game
        // dependency at this target's version (no-op for the current one). A source that declares any
        // other version leaves every legacy zip claiming the wrong floor, and a string replace that
        // matches nothing reports success - so the declaration is checked rather than assumed.
        string source = File.ReadAllText(
          $"../../mods/{project.ModFolder}/src/modinfo.json"
        );
        string declared = $"\"game\": \"{BuildContext.SourceGameVersion}\"";
        if (!source.Contains(declared))
          throw new InvalidOperationException(
            $"mods/{project.ModFolder}/src/modinfo.json must declare {declared} - the packaged game "
              + $"dependency is rewritten from it per target. Found: "
              + $"{GameDependencyOf(source)}."
          );

        string modinfo = source.Replace(
          declared,
          $"\"game\": \"{target.GameVersion}\""
        );
        File.WriteAllText($"{stageDir}/modinfo.json", modinfo);

        string versionSuffix = target.IsCurrent ? "" : $"_{target.GameVersion}";
        context.Zip(
          stageDir,
          $"../../dist/Releases/{target.GameVersion}/{project.ModId}_{project.Version}{versionSuffix}.zip"
        );
      }
    }
  }

  /// <summary>The <c>game</c> dependency a modinfo declares, for the mismatch message.</summary>
  private static string GameDependencyOf(string modinfo) {
    Match m = Regex.Match(modinfo, "\"game\"\\s*:\\s*\"([^\"]*)\"");
    return m.Success ? m.Groups[1].Value : "(none)";
  }
}

[TaskName("PackageTesting")]
[IsDependentOn(typeof(PackageTask))]
public sealed class PackageTestingTask : FrostingTask<BuildContext>
{
  // The headless test harness (mods/exlib/testing) is a DEVELOPER library, not a game mod, so it is
  // not a mod zip. It ships as the ExpandedLib.Testing NuGet package and, for anyone not consuming
  // packages, as a dev bundle attached to the GitHub release: ExpandedLib.Testing.dll plus the exlib.dll it
  // compiles against (exlib's AssemblyName is "exlib"), which a downstream test project references
  // directly (the game assemblies and NSubstitute the consumer supplies - see the wiki
  // "Consuming outside this repo").
  //
  // Not part of the Default chain - built on demand with `dotnet run -- --target=PackageTesting`
  // when a release actually attaches the dev bundle.
  //
  // Built for the CURRENT game version only (net10.0 / 1.22); on 1.20/1.21 reference it from source.
  const string BundleReadme =
    "ExpandedLib dev bundle - headless test harness + source generators\n"
    + "\n"
    + "Built for the current game version (1.22 / net10.0).\n"
    + "\n"
    + "TEST HARNESS. Add ExpandedLib.Testing.dll and exlib.dll to your test project with\n"
    + "<Private>false</Private>, reference VintagestoryAPI/VSSurvivalMod/VSEssentials from your own\n"
    + "game install, add NSubstitute + xUnit from NuGet, and call VsAssemblyResolver.Register() +\n"
    + "TestLang.Init() from a [ModuleInitializer].\n"
    + "\n"
    + "  Helpers that read the source tree (goldens, the block-code table, handbook sync) resolve a\n"
    + "  repo root by looking upward for a .sln, .slnx or .git. If your layout has none, set the\n"
    + "  EXLIB_REPO_ROOT environment variable or DefinitionGoldens.RepoRootOverride.\n"
    + "\n"
    + "SOURCE GENERATORS. analyzers/ExpandedLib.Generators.dll emits the typed config accessor for a\n"
    + "POCO marked [ExConfigRegister] ({Mod}Values) and the typed lang keys ({Domain}Lang). Without it\n"
    + "those names do not exist and you get CS0103. Reference it as an analyzer, not a library:\n"
    + "\n"
    + "  <ItemGroup>\n"
    + "    <Analyzer Include=\"path\\to\\analyzers\\ExpandedLib.Generators.dll\" />\n"
    + "    <AdditionalFiles Include=\"assets\\<yourdomain>\\lang\\en.json\" />\n"
    + "  </ItemGroup>\n"
    + "\n"
    + "  The AdditionalFiles line is what feeds {Domain}Lang; omit it and only the config accessor is\n"
    + "  generated.\n"
    + "\n"
    + "XML docs (exlib.xml, ExpandedLib.Testing.xml) sit beside their DLLs - keep them there for\n"
    + "IntelliSense over the public surface.\n"
    + "\n"
    + "See the wiki: https://github.com/ringavirda/modding-vsexpanded/wiki/Testing-Harness\n";

  public override void Run(BuildContext context)
  {
    var current = Array.Find(BuildContext.GameTargets, t => t.IsCurrent)!;
    var exlib = context.Projects.Find(p => p.Folder == "ExpandedLib")!;

    // Build the harness for the current target (single-TFM => flat bin/<config> output).
    context.DotNetBuild(
      "../../mods/exlib/testing/ExpandedLib.Testing.csproj",
      new DotNetBuildSettings
      {
        Configuration = context.BuildConfiguration,
        Framework = current.Tfm,
      }
    );

    // Hyphen, not a dot, in the basename: GitHub's release-asset uploader sniffs content type from
    // the filename and rejects a dotted name segment (exlib.testing_x.zip) with "we can't process
    // this file". exlib-testing_<version>.zip keeps the _<version> convention and uploads cleanly.
    string stageDir = $"../../dist/Releases/{current.GameVersion}/exlib-testing";
    context.EnsureDirectoryExists(stageDir);

    context.CopyFile(
      $"../../mods/exlib/testing/bin/{context.BuildConfiguration}/ExpandedLib.Testing.dll",
      $"{stageDir}/ExpandedLib.Testing.dll"
    );
    // exlib.dll comes from exlib's own publish output (the harness references it Private=false, so
    // it isn't copied into the harness bin). The assembly file is exlib.dll (AssemblyName "exlib").
    context.CopyFile(
      $"{context.PublishDir(exlib, current)}/exlib.dll",
      $"{stageDir}/exlib.dll"
    );
    // The XML docs beside each dll are what give a consumer IntelliSense over the public surface;
    // without them a referenced assembly shows bare signatures. The mod zips get theirs for free
    // (they copy the whole publish directory), but this bundle names its files, so they are named
    // here too. Copied best-effort: an older build tree may predate GenerateDocumentationFile.
    CopyDocsIfPresent(
      context,
      $"../../mods/exlib/testing/bin/{context.BuildConfiguration}/ExpandedLib.Testing.xml",
      $"{stageDir}/ExpandedLib.Testing.xml"
    );
    CopyDocsIfPresent(
      context,
      $"{context.PublishDir(exlib, current)}/exlib.xml",
      $"{stageDir}/exlib.xml"
    );
    // The source generators, under analyzers/. Without them the wiki's canonical config recipe -
    // [ExConfigRegister] on a POCO, then {Mod}Values.Load(api) - does not compile outside this repo:
    // the generated accessor never appears and the only signal is CS0103 on a name the reader was told
    // would exist. IncludeBuildOutput=false keeps the generator out of the mods' own packages, so it is
    // named here rather than picked up from a publish directory.
    context.DotNetBuild(
      "../../mods/exlib/generators/ExpandedLib.Generators.csproj",
      new DotNetBuildSettings { Configuration = context.BuildConfiguration }
    );
    context.EnsureDirectoryExists($"{stageDir}/analyzers");
    context.CopyFile(
      $"../../mods/exlib/generators/bin/{context.BuildConfiguration}/netstandard2.0/ExpandedLib.Generators.dll",
      $"{stageDir}/analyzers/ExpandedLib.Generators.dll"
    );

    File.WriteAllText($"{stageDir}/README.txt", BundleReadme);
    context.CopyFile("../../LICENSE", $"{stageDir}/LICENSE.txt");

    context.Zip(
      stageDir,
      $"../../dist/Releases/{current.GameVersion}/exlib-testing_{exlib.Version}.zip"
    );
  }

  /// <summary>Copies an XML documentation file when the build produced one, warning rather than
  /// failing when it did not - the bundle is still usable without docs, just poorer.</summary>
  private static void CopyDocsIfPresent(
    BuildContext context,
    string from,
    string to
  ) {
    if (File.Exists(from))
      context.CopyFile(from, to);
    else
      context.Log.Warning(
        $"No XML documentation at {from}; bundling without it."
      );
  }
}

[TaskName("Default")]
[IsDependentOn(typeof(PackageTask))]
public class DefaultTask : FrostingTask { }
