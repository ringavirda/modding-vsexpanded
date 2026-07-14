using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// Proves the <c>ExLangKeyGenerator</c> emits the <c>ExlibLang</c> constants with the domain-qualified
/// values the game actually looks up: a bare <c>en.json</c> key <c>k</c> becomes <c>"exlib:k"</c>, and a key
/// that is already domain-qualified passes through verbatim. Compiling this class at all is half the guard
/// (a mistyped member name fails the build - the whole point of the generator); the asserts pin the values.
/// </summary>
public class LangKeyGeneratorTests
{
  [Fact]
  public void Bare_keys_are_domain_qualified()
  {
    Assert.Equal("exlib:network-hi-on", ExlibLang.NetworkHiOn);
    Assert.Equal("exlib:block-structurefiller", ExlibLang.BlockStructurefiller);
    Assert.Equal("exlib:command-recipes-set", ExlibLang.CommandRecipesSet);
  }

  [Fact]
  public void Already_qualified_keys_pass_through_verbatim()
  {
    Assert.Equal(
      "game:placefailure-exlib-noorientation",
      ExlibLang.PlacefailureExlibNoorientation
    );
  }
}
