using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExpandedLib.Structures;
using ExpandedLib.Industry.Molten;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="EntityRegistry.KeyFor"/> is the single source of the class-string↔type binding, shared by
/// the class registration (<c>RegisterAll</c>) and the code-first builder's type-safe <c>Class&lt;T&gt;()</c>.
/// These pin the exact key it produces for each attribute shape so the two consumers can never desync.
/// </summary>
public class RegistrationKeyTests {
  [BlockRegister]
  private sealed class ConventionBlock : Block { }

  [BlockRegister("customcode")]
  private sealed class CodedBlock : Block { }

  [BlockRegister("Vanilla", PrefixModId = false)]
  private sealed class BareBlock : Block { }

  private sealed class NoAttributeBlock : Block { }

  [Fact]
  public void Default_convention_is_modid_dot_classname() {
    Assert.Equal(
      "iiex.ConventionBlock",
      EntityRegistry.KeyFor("iiex", typeof(ConventionBlock))
    );
  }

  [Fact]
  public void Explicit_code_replaces_the_class_name_but_keeps_the_prefix() {
    Assert.Equal(
      "iiex.customcode",
      EntityRegistry.KeyFor("iiex", typeof(CodedBlock))
    );
  }

  [Fact]
  public void PrefixModId_false_registers_under_a_bare_key() {
    // As used when replacing a vanilla class - no {modid}. prefix.
    Assert.Equal("Vanilla", EntityRegistry.KeyFor("iiex", typeof(BareBlock)));
  }

  [Fact]
  public void A_type_without_a_register_attribute_falls_back_to_the_convention() {
    // The types above live in this test assembly, which declares no [assembly: ExDomain] and is never
    // registered, so the caller's domain is the only answer available.
    Assert.Equal(
      "iiex.NoAttributeBlock",
      EntityRegistry.KeyFor("iiex", typeof(NoAttributeBlock))
    );
  }

  #region Cross-assembly resolution

  [Fact]
  public void A_type_from_an_assembly_that_declares_a_domain_ignores_the_caller_s() {
    // The defect this replaced: a content mod naming an exlib behaviour through Class<T>()/Behavior<T>()
    // got "{their domain}.BEBehaviorMoltenCell", a key nobody registered. It compiles, and the block
    // half of the failure is not logged, so the machine places and silently does nothing. Every
    // cross-assembly site worked around it with a hand-typed string, which a rename then broke.
    Assert.Equal(
      "exlib.BEBehaviorMoltenCell",
      EntityRegistry.KeyFor("iiex", typeof(BEBehaviorMoltenCell))
    );
  }

  [Fact]
  public void DomainOf_prefers_the_declared_domain_over_the_fallback() {
    Assert.Equal(
      "exlib",
      EntityRegistry.DomainOf(typeof(BEBehaviorMoltenCell).Assembly, "iiex")
    );
  }

  [Fact]
  public void DomainOf_falls_back_when_an_assembly_declares_none() {
    Assert.Equal(
      "iiex",
      EntityRegistry.DomainOf(typeof(ConventionBlock).Assembly, "iiex")
    );
  }

  [Fact]
  public void Every_mod_assembly_declares_the_domain_its_modinfo_names() {
    // Keeps the two in step: the attribute is what resolves a key, modinfo.json is what names the
    // shipped asset tree, and a mod that adds one without the other resolves keys into a domain it does
    // not ship.
    var missing = new List<string>();
    string mods = Path.Combine(RepoRoot(), "mods");

    foreach (
      string modinfo in Directory
        .EnumerateDirectories(mods)
        .Select(d => Path.Combine(d, "src", "modinfo.json"))
        .Where(File.Exists)
    ) {
      string folder = Path.GetDirectoryName(modinfo)!;
      string modId = Regex
        .Match(File.ReadAllText(modinfo), @"""modid""\s*:\s*""([^""]+)""")
        .Groups[1]
        .Value;

      string declared = string.Join(
        "\n",
        Directory
          .EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
          .Where(f =>
            !f.Contains(
              $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"
            )
          )
          .Select(File.ReadAllText)
      );

      if (!declared.Contains($"[assembly: ExDomain(\"{modId}\")]"))
        missing.Add($"{Path.GetFileName(folder)} (modid {modId})");
    }

    // Five mod projects ship one each; an empty corpus means the discovery broke, not that the rule
    // holds vacuously.
    Assert.NotEmpty(Directory.EnumerateDirectories(mods));
    Assert.True(
      missing.Count == 0,
      "every mod assembly must carry [assembly: ExDomain(\"<its modid>\")]: "
        + string.Join("; ", missing)
    );
  }

  private static string RepoRoot() {
    DirectoryInfo? dir = new(AppContext.BaseDirectory);
    while (
      dir != null
      && !File.Exists(Path.Combine(dir.FullName, "VintageStory.sln"))
    )
      dir = dir.Parent;
    Assert.True(dir != null, "could not locate repo root (VintageStory.sln)");
    return dir!.FullName;
  }

  #endregion
}
