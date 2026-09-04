using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Repo-wide collectible-mapping rule: a block entity that stores an <c>ItemStack</c> must also
/// override <c>OnStoreCollectibleMappings</c> (and, by the same pairing, <c>OnLoadCollectibleMappings</c>).
/// <c>ItemStack.ToBytes</c> serialises the collectible's runtime id, not its code, so a stack pasted into
/// another world via a schematic resolves whatever holds that id there unless the mapping pair fixes it up.
/// See <c>BlockSchematic</c>'s calls into both methods and <c>ItemStack.FixMapping</c>.
/// </summary>
public class CollectibleMappingGuardTests {
  #region Corpus

  private static IEnumerable<string> SourceFiles(string dir) {
    string full = Path.Combine(RepoRoot(), dir);
    if (!Directory.Exists(full))
      yield break;
    foreach (
      string path in Directory.EnumerateFiles(
        full,
        "*.cs",
        SearchOption.AllDirectories
      )
    ) {
      string rel = Rel(path);
      if (
        rel.Contains("/bin/", StringComparison.Ordinal)
        || rel.Contains("/obj/", StringComparison.Ordinal)
        || path.EndsWith(".g.cs", StringComparison.Ordinal)
      )
        continue;
      yield return path;
    }
  }

  private static string Rel(string path) =>
    Path.GetRelativePath(RepoRoot(), path).Replace('\\', '/');

  private static string RepoRoot() {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (
      dir != null
      && !File.Exists(Path.Combine(dir.FullName, "VintageStory.sln"))
    )
      dir = dir.Parent;
    return dir?.FullName
      ?? throw new InvalidOperationException(
        "Could not locate the repo root (VintageStory.sln) from "
          + AppContext.BaseDirectory
      );
  }

  // A stack is stored either directly (SetItemstack) or through one of the two helpers that call it
  // on the caller's behalf: MoltenContents.Write (a static call, so its own name is the literal at
  // the call site) and MoltenCharge.ToTree (an instance call, so only ".ToTree(" appears at the call
  // site - narrowed to files that also name the type, since a bare ".ToTree(" alone also matches
  // unrelated types such as OverPressure or ChargeColumn).
  private static bool StoresAStack(string text) =>
    text.Contains("SetItemstack(")
    || text.Contains("MoltenContents.Write(")
    || (text.Contains("MoltenCharge") && text.Contains(".ToTree("));

  #endregion

  [Fact]
  public void A_block_entity_that_stores_a_stack_maps_its_collectibles() {
    // ItemStack.ToBytes writes the runtime id, so a stack stored without OnStore/OnLoad
    // CollectibleMappings resolves to whatever holds that id in the destination world.
    var offenders = new List<string>();
    foreach (string f in SourceFiles("src")) {
      string text = File.ReadAllText(f);
      if (!text.Contains("class BlockEntity") || !StoresAStack(text))
        continue;
      if (text.Contains("OnStoreCollectibleMappings"))
        continue;
      offenders.Add(Rel(f));
    }

    Assert.True(offenders.Count == 0, string.Join(", ", offenders));
  }
}
