using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.Items;

/// <summary>
/// The fasteners this mod makes rather than borrows. Nails are vanilla's
/// (<c>game:metalnailsandstrips</c>) and stay vanilla's, for the reason the rod does - a second nail would
/// split every consumer in two - so the only fastener minted here is the rivet.
/// See docs/design/items/fasteners.md and docs/design/mechanics/machining-line.md.
/// </summary>
public class FastenerItemDefinitions : IExItemDefProvider {
  /// <summary>
  /// Metal in one rivet bundle. Half a nail bundle's 25 u, which is the whole of the trade the
  /// substitution rule sells: a rivet route yields 8 bundles per 100 u of iron against the nail
  /// route's 4, and it pays for that with a second machine and a longer schedule at the mill.
  /// <para>
  /// Fractional on purpose. The quantum is set by what feeds it - a rivet rod is 25 u and the riveter
  /// upsets two bundles out of one - so rounding it to 12 or 13 would mint or lose metal on every stroke,
  /// and the ledger that the forming line conserves iron exactly is worth more than a round number.
  /// </para>
  /// </summary>
  public const float RivetUnits = 12.5f;

  /// <summary>Bundles one rivet rod is upset into.</summary>
  public const int RivetsPerRod = 2;

  /// <summary>The rivet's code, without a domain.</summary>
  public const string RivetCode = "rivet";

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Rivet(domain)];

  /// <summary>
  /// The rivet: a bundle, the way vanilla's nails and strips are a bundle, rather than one countable pin.
  /// No metal variant, because nothing makes a rivet of anything but iron - the rivet rod that feeds the
  /// press has no metal axis either, and a steel rivet is siex's to ship the day it has a route.
  /// <para>
  /// It borrows vanilla's nails-and-strips geometry outright. That is a deliberate drop-in and not a
  /// placeholder: a rivet bundle and a nail bundle are the same kind of object at the same scale, and the
  /// rod and the nails this line already trades in do exactly the same thing.
  /// </para>
  /// </summary>
  private static ExItemDef Rivet(string domain) =>
    ExItemDef
      .Create(domain, RivetCode)
      .Shape("game:item/resource/metalnailsandstrips")
      .TextureAll("game:block/metal/plate/iron")
      .MaxStackSize(64)
      .MaterialDensity(7850)
      .Attribute("materialUnits", RivetUnits)
      .CreativeCommon("*");
}
