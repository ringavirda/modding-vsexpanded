using System;
using System.Collections.Generic;
using System.IO;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
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

/// <summary>One buildable mod project in the monorepo.</summary>
public record ModProject(string Folder, string ModId, string Version);

/// <summary>A supported game version to publish for: its TFM, the game version stamped into the
/// packaged modinfo, and whether it's the current (non-legacy) target. Keep in sync with the version
/// manifest in src/Directory.Build.props (Cake can't read MSBuild, so this is the one duplication -
/// same as the CI workflows).</summary>
public record GameTarget(string Tfm, string GameVersion, bool IsCurrent);

public class BuildContext : FrostingContext
{
  // Build order matters: exlib first (the shared lib both mods reference), then ppex
  // (referenced by smex), then smex.
  public static readonly string[] ProjectFolders =
  [
    "ExpandedLib",
    "PipesAndPowerExpanded",
    "SteelmakingExpanded",
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

    foreach (var folder in ProjectFolders)
    {
      var modInfo = context.DeserializeJsonFromFile<ModInfo>(
        $"../../src/{folder}/modinfo.json"
      );
      Projects.Add(new ModProject(folder, modInfo.ModID, modInfo.Version));
    }
  }

  /// <summary>The publish output for a project+target. The current version uses the flat
  /// Mods/mod path; legacy targets append their TFM (see the mod csproj OutputPath).</summary>
  public string PublishDir(ModProject project, GameTarget target) =>
    target.IsCurrent
      ? $"../../src/{project.Folder}/bin/{BuildConfiguration}/Mods/mod/publish"
      : $"../../src/{project.Folder}/bin/{BuildConfiguration}/{target.Tfm}/Mods/mod/publish";
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
        $"../../src/{project.Folder}/assets/**/*.json"
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
      string csproj = $"../../src/{project.Folder}/{project.Folder}.csproj";
      // Wipe the whole bin so stale per-version outputs can't leak into a package.
      string binDir =
        $"../../src/{project.Folder}/bin/{context.BuildConfiguration}";
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
    context.EnsureDirectoryExists("../Releases");
    context.CleanDirectory("../Releases");

    // One archive per (game version, mod), grouped into a per-version folder:
    //   Releases/<gameVersion>/<modid>_<modVersion>.zip
    foreach (var target in BuildContext.GameTargets)
    {
      foreach (var project in context.Projects)
      {
        string stageDir = $"../Releases/{target.GameVersion}/{project.ModId}";
        context.EnsureDirectoryExists(stageDir);

        context.CopyFiles($"{context.PublishDir(project, target)}/*", stageDir);
        if (context.DirectoryExists($"../../src/{project.Folder}/assets"))
          context.CopyDirectory(
            $"../../src/{project.Folder}/assets",
            $"{stageDir}/assets"
          );
        if (context.FileExists($"../../src/{project.Folder}/modicon.png"))
          context.CopyFile(
            $"../../src/{project.Folder}/modicon.png",
            $"{stageDir}/modicon.png"
          );

        // Authoritative modinfo: the source declares the current game version, so point the game
        // dependency at this target's version (no-op for the current one).
        string modinfo = File.ReadAllText(
            $"../../src/{project.Folder}/modinfo.json"
          )
          .Replace(
            $"\"game\": \"{BuildContext.SourceGameVersion}\"",
            $"\"game\": \"{target.GameVersion}\""
          );
        File.WriteAllText($"{stageDir}/modinfo.json", modinfo);

        context.Zip(
          stageDir,
          $"../Releases/{target.GameVersion}/{project.ModId}_{project.Version}.zip"
        );
      }
    }
  }
}

[TaskName("Default")]
[IsDependentOn(typeof(PackageTask))]
public class DefaultTask : FrostingTask { }
