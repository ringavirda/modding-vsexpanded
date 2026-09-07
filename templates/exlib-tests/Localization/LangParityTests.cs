using System.IO;
using System.Runtime.CompilerServices;
using ExpandedLib.Testing;
using Xunit;

namespace YourMod.Tests;

/// <summary>The lang-parity rule (<see cref="LangParity"/>) over your mod's shipped lang tree.
/// Trivially true until your mod ships a translated locale alongside <c>en.json</c>.</summary>
public class LangParityTests {
  // "YourModProject" here twice: once for the sibling project folder (renamed to your ModName at
  // `dotnet new` time), once for your asset domain - edit the second if your domain differs from
  // --ModName, the same note GoldenTests.cs makes.
  private static string LangTree([CallerFilePath] string here = "") =>
    Path.GetFullPath(
      Path.Combine(
        Path.GetDirectoryName(here)!,
        "..",
        "..",
        "YourModProject",
        "assets",
        "YourModProject",
        "lang"
      )
    );

  [Fact]
  public void Locales_carry_exactly_the_english_key_set_and_placeholders() {
    var offenders = LangParity.Check(LangTree());
    Assert.True(offenders.Count == 0, string.Join("\n", offenders));
  }
}
