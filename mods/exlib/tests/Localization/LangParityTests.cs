using System.IO;
using ExpandedLib.Testing;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The lang-parity rule (<see cref="LangParity"/>) over exlib's own shipped lang tree.
/// </summary>
public class LangParityTests {
  private static string LangTree =>
    Path.Combine(RepoPaths.Assets("exlib"), "lang");

  [Fact]
  public void Exlibs_locales_carry_exactly_the_english_key_set_and_placeholders() {
    var offenders = LangParity.Check(LangTree);
    Assert.True(offenders.Count == 0, string.Join("\n", offenders));
  }

  [Fact]
  public void The_corpus_reaches_a_translated_locale() {
    Assert.NotEmpty(LangParity.LocaleFiles(LangTree));
  }
}
