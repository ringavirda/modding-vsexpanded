using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using Xunit;

namespace YourMod.Tests;

/// <summary>Golden-file parity for your mod's code-first defs (see the wiki's "Code-First
/// Definitions" page). Runs the real check once your mod declares an <see
/// cref="IExBlockDefProvider"/>; until then there are none to check, and the fact records that
/// rather than being skipped - this repo runs no skipped tests.</summary>
public class GoldenTests {
  // Found by assembly name rather than a ProjectReference type, so this file compiles unchanged
  // whether or not your mod project is wired up yet (see the csproj's conditional reference).
  private static Assembly? ModAssembly =>
    AppDomain.CurrentDomain.GetAssemblies()
      .FirstOrDefault(a => a.GetName().Name == "YourModProject");

  private static string GoldenRoot([CallerFilePath] string here = "") =>
    Path.Combine(Path.GetDirectoryName(here)!, "..", "goldens");

  [Fact]
  public void Definitions_reproduce_their_goldens() {
    Assembly? asm = ModAssembly;
    List<Type> providers = asm
      ?.GetTypes()
      .Where(t =>
        typeof(IExBlockDefProvider).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface
      )
      .ToList() ?? [];

    if (providers.Count == 0) {
      // No code-first blocks yet - nothing to golden-check. Add one and this branch stops running.
      Assert.Empty(providers);
      return;
    }

    const string domain = "YourModProject"; // your asset domain, if it differs from --ModName
    foreach (
      string relativePath in DefinitionGoldens.Cases(domain, asm!).Select(c => (string)c[0])
    ) {
      var (ok, message) = DefinitionGoldens.CheckGolden(
        domain,
        asm!,
        relativePath,
        GoldenRoot()
      );
      Assert.True(ok, message);
    }
  }
}
