using System.Collections.Generic;
using System.IO;
using ExpandedLib.Testing;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The lang-parity rule (<see cref="LangParity"/>) over iiex's own shipped lang tree and the shared
/// <c>game</c> lang overlay it ships alongside it.
/// </summary>
public class LangParityTests {
  private static readonly string[] Trees =
  [
    Path.Combine(RepoPaths.Assets("iiex"), "lang"),
    Path.Combine(RepoPaths.Assets("game"), "lang"),
  ];

  [Fact]
  public void Iiexs_locales_carry_exactly_the_english_key_set_and_placeholders() {
    var offenders = new List<string>();
    foreach (string tree in Trees)
      offenders.AddRange(LangParity.Check(tree));

    Assert.True(offenders.Count == 0, string.Join("\n", offenders));
  }

  [Fact]
  public void The_corpus_reaches_a_translated_locale() {
    var locales = new List<string>();
    foreach (string tree in Trees)
      locales.AddRange(LangParity.LocaleFiles(tree));

    Assert.NotEmpty(locales);
  }
}
