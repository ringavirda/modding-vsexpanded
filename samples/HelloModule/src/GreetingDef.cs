namespace HelloModule;

/// <summary>
/// One entry under <c>config/greetings/</c> - the JSON shape read with
/// <see cref="ExpandedLib.Catalogues.AssetCatalogueLoader.GetMany{T}"/> the way Industry reads its
/// metals, so any domain (not only <c>hellomodule</c>'s own) can contribute a file of greetings.
/// </summary>
public sealed class GreetingDef {
  /// <summary>Short key, used to code the generated item (<c>greeting-&lt;code&gt;</c>).</summary>
  public string Code { get; set; } = "";

  /// <summary>The greeting text <see cref="BlockBehaviorGreeter"/> sends to the player.</summary>
  public string Text { get; set; } = "";
}
