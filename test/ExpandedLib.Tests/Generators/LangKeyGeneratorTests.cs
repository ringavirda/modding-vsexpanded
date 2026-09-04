using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The <c>ExLangKeyGenerator</c> output: a bare <c>en.json</c> key <c>k</c> becomes the
/// <c>ExlibLang</c> constant <c>"exlib:k"</c>, and a key that is already domain-qualified passes
/// through verbatim. A mistyped member name fails the build.
/// </summary>
public class LangKeyGeneratorTests {
  [Fact]
  public void Bare_keys_are_domain_qualified() {
    Assert.Equal("exlib:network-hi-on", ExlibLang.NetworkHiOn);
    Assert.Equal("exlib:block-structurefiller", ExlibLang.BlockStructurefiller);
    Assert.Equal("exlib:command-recipes-set", ExlibLang.CommandRecipesSet);
  }

  [Fact]
  public void Already_qualified_keys_pass_through_verbatim() {
    Assert.Equal(
      "game:placefailure-exlib-noorientation",
      ExlibLang.PlacefailureExlibNoorientation
    );
  }
}
