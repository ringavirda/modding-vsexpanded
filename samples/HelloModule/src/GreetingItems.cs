using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace HelloModule;

/// <summary>
/// Builds one code-first item per greeting - vanilla shape and texture, the way
/// <c>HelloExpanded</c>'s <c>BlockHello</c> uses vanilla block art rather than authoring a mesh.
/// Pure: no asset reads, so it is unit-testable without a <see cref="Vintagestory.API.Common.ICoreAPI"/>.
/// </summary>
public static class GreetingItems {
  /// <summary>One <see cref="ExItemDef"/> per <paramref name="greetings"/> entry, coded
  /// <c>greeting-&lt;code&gt;</c> in <paramref name="domain"/>.</summary>
  public static IEnumerable<ExItemDef> Emit(
    string domain,
    IEnumerable<GreetingDef> greetings
  ) {
    foreach (GreetingDef greeting in greetings)
      yield return ExItemDef
        .Create(domain, "greeting-" + greeting.Code)
        .Shape("survival:item/lore/scroll-plain")
        .Texture("scrolls-rotten", "survival:item/lore/scrolls-rotten")
        .CreativeCommon("*");
  }
}
