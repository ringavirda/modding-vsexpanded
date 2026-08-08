using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Items;

/// <summary>
/// The iron tier's own <b>spur gear</b> - the plain toothed wheel every mpenergy machine is assembled from
/// (the rolling mill's stand, the transmission's shafts). Named to pair with <see cref="BevelGearItemDefinitions"/>:
/// a bevel turns a run through a corner, a spur passes it straight on. Same tier, different job, so two items
/// rather than one with a variant.
/// <para>
/// It exists because the placement rule requires it. Those constructions previously reached <em>upward</em> for
/// <c>lpex:gear-iron</c>, from a mod iwex declares no dependency on, so an iwex-only player could build none of
/// them.
/// </para>
/// <para>
/// <b>Deliberately not gated on cast iron</b>, despite a cast gear being the period-correct part. So the gear
/// is cut from whatever iron the player has, and the cupola route is the <em>cheaper</em> one rather than the
/// only one (see <c>Recipes.Grid.EnergyRecipeDefinitions</c>) - the same "a working shop feeds itself, a new one
/// pays retail" shape the puddling fettle already uses.
/// </para>
/// <para>
/// Note: no gear-to-cupola deadlock exists in the current dependency graph (the burdenmaker needs no
/// power at all), but the rule is kept regardless. "No deadlock today" is a
/// statement about the current graph, and the reason to keep a bootstrap route is that a fresh world
/// should never depend on a furnace it has not built yet.
/// </para>
/// </summary>
public class SpurGearItemDefinitions : IExItemDefProvider
{
  /// <summary>Metal a spur gear is worth, for remelt/scrap. Above the bevel's 40 - a spur wheel is the larger
  /// casting of the two.</summary>
  public const int GearUnits = 60;

  /// <summary>The gear's code, so the two RCC constructions that require it never spell it by hand.</summary>
  public const string Code = "spurgear";

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      ExItemDef
        .Create(domain, Code)
        // Placeholder art. `assets/editable/shapes/item-gear-spur.json` is drawn but not yet exported to
        // the runtime tree; until it is, this borrows vanilla's gear shape with a cast-iron texture over it
        // (the same stopgap RollSetItemDefinitions documents for the roll sets). Swap the Shape call for
        // `iwex:item/gear-spur` when the export lands - nothing else has to change.
        .Shape("game:item/gear-rusty")
        .Texture("rusty-iron", "iwex:block/metal/castiron")
        .MaxStackSize(32)
        .MaterialDensity(7200)
        .Attribute("materialUnits", GearUnits)
        .CreativeCommon("*"),
    ];
}
