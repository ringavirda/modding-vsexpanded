using NSubstitute;
using Vintagestory.API.Config;

namespace ExpandedLib.Testing;

/// <summary>
/// Stands up a minimal headless <see cref="Lang"/> so production code that formats player-facing
/// strings (block info, HUD, measurement units) can call <see cref="Lang.Get"/> without the game's
/// asset pipeline; otherwise <c>Lang.Get</c> throws, because <c>CurrentLocale</c> is null and the
/// language dictionary is empty. The registered service echoes the key back, the same fallback the
/// real service uses for an untranslated key.
/// </summary>
public static class TestLang {
  private static bool _ready;

  /// <summary>
  /// The registered substitute, exposed so a test needing to distinguish "this key has a translation"
  /// from "it does not" - a domain-fallback lookup such as <c>Lang.GetWithFallback</c> - can restub
  /// <see cref="ITranslationService.HasTranslation(string, bool)"/> for one specific key to
  /// <c>false</c>; every other key keeps echoing <c>true</c> from <see cref="Init"/>.
  /// </summary>
  public static ITranslationService Service { get; private set; } = null!;

  /// <summary>Idempotently registers an echo-the-key translation service for the "en" locale.</summary>
  public static void Init() {
    if (_ready)
      return;
    _ready = true;

    var svc = Substitute.For<ITranslationService>();
    svc.LanguageCode.Returns("en");
    svc.Get(Arg.Any<string>(), Arg.Any<object[]>())
      .Returns(ci => ci.Arg<string>());
    svc.GetIfExists(Arg.Any<string>(), Arg.Any<object[]>())
      .Returns(ci => ci.Arg<string>());
    svc.GetUnformatted(Arg.Any<string>()).Returns(ci => ci.Arg<string>());
    svc.GetMatching(Arg.Any<string>(), Arg.Any<object[]>())
      .Returns(ci => ci.Arg<string>());
    svc.GetMatchingIfExists(Arg.Any<string>(), Arg.Any<object[]>())
      .Returns(ci => ci.Arg<string>());
    svc.HasTranslation(Arg.Any<string>(), Arg.Any<bool>()).Returns(true);
    svc.HasTranslation(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
      .Returns(true);

    Lang.DefaultLocale = "en";
    // AvailableLanguages is a read-only auto-property over a dictionary the engine mutates in place,
    // so the locale is added to it rather than the field replaced.
    Lang.AvailableLanguages["en"] = svc;
    // ChangeLanguage is the engine's path to set the otherwise-getter-only CurrentLocale.
    Lang.ChangeLanguage("en");
    Service = svc;
  }
}
