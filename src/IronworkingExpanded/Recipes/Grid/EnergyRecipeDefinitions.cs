using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronworkingExpanded.Items;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the mechanical-energy tier: the spur gear, the flywheel that buffers the run,
/// and the shafting that carries it. The cast-iron bevel has no grid recipe - it is made in world by adding a
/// <see cref="BevelGearItemDefinitions">bevel gear</see> to a placed shaft, which is also why its blocktype
/// carries no creative entry. The transmission is right-click constructed, so only its cheap base block is
/// crafted here; its real cost is in the RCC stages.
/// </summary>
public class EnergyRecipeDefinitions : IExRecipeDefProvider {
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [Gears(domain), Flywheels(domain), Shafting(domain)];

  /// <summary>
  /// The spur gear, by two routes to the same item. The bootstrap route cuts one gear from two iron ingots
  /// and must stay reachable without cast iron, so a fresh world can build the plant that later makes it.
  /// Once a cupola runs, one cast-iron ingot yields two gears, a four-fold improvement, cast iron being the
  /// tier's cheap bulk metal. See <see cref="SpurGearItemDefinitions"/>.
  /// </summary>
  private static ExRecipeDef Gears(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "spurgear")
      // Bootstrap route: chisel the teeth out of a pair of iron ingots. Wasteful on purpose.
      .Grid(r =>
        r.Name("Spur Gear")
          .Pattern("_K_,III")
          .Size(3, 2)
          .Ingredient("I", i => i.Item("game:ingot-iron").Quantity(1))
          .Ingredient("K", Chisel)
          .OutputItem($"{domain}:{SpurGearItemDefinitions.Code}", 1)
      )
      // Cupola route: cast iron is cheap, so the same work yields twice as much.
      .Grid(r =>
        r.Name("Spur Gear (Cast Iron)")
          .Pattern("_K_,_C_")
          .Size(3, 2)
          .Ingredient("C", i => i.Item($"{domain}:ingot-castiron").Quantity(1))
          .Ingredient("K", Chisel)
          .OutputItem($"{domain}:{SpurGearItemDefinitions.Code}", 2)
      );

  /// <summary>
  /// The flywheel - the network's energy reservoir (<c>E = ½Iω²</c>) and the block that bridges a vanilla
  /// waterwheel or windmill into mpenergy. Costed in mass: the rim is cast wheel sections
  /// (<see cref="CastPartItemDefinitions.CastWheelSectionUnits"/>) and the web heavy plate, 3040 u in total,
  /// with the large variant costing proportionally more for the energy it stores.
  /// <para>
  /// Both grids are diagram-led, so each is the plan plus a flat bill of materials - one cell per distinct
  /// ingredient carrying its quantity, not a picture of the product laid out in the grid. The diagram is
  /// <c>.Tool()</c>, drafted once at the design table and reused, and each size has its own.
  /// See <c>docs/design/diagram-crafting.md</c>.
  /// </para>
  /// </summary>
  private static ExRecipeDef Flywheels(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "flywheel")
      .Grid(r =>
        r.Name("Flywheel (from diagram)")
          .Pattern("DW,PG")
          .Size(2, 2)
          .Ingredient(
            "D",
            i => i.Item($"{domain}:diagram-mpenergy-flywheel").Tool()
          )
          // Four arcs close the rim; four plates spoke it to the hub; the gear couples it to the run.
          .Ingredient(
            "W",
            i => i.Item($"{domain}:castwheelsection").Quantity(4)
          )
          .Ingredient("P", i => i.Item($"{domain}:castplate-heavy").Quantity(4))
          .Ingredient(
            "G",
            i => i.Item($"{domain}:{SpurGearItemDefinitions.Code}").Quantity(1)
          )
          .OutputBlock($"{domain}:mpenergy-flywheel-normal-ns", 1)
      )
      // The large wheel is an upgrade, so it takes the normal wheel as its core rather than being built
      // from scratch. Eight sections rather than four: the 5x5 rim has twice the circumference of the 3x3.
      .Grid(r =>
        r.Name("Flywheel, Large (from diagram)")
          .Pattern("DSW")
          .Size(3, 1)
          .Ingredient(
            "D",
            i => i.Item($"{domain}:diagram-mpenergy-flywheellarge").Tool()
          )
          .Ingredient(
            "S",
            i => i.Item($"{domain}:castwheelsection").Quantity(8)
          )
          .Ingredient(
            "W",
            i => i.Block($"{domain}:mpenergy-flywheel-normal-*").Quantity(1)
          )
          .OutputBlock($"{domain}:mpenergy-flywheel-large-ns", 1)
      );

  /// <summary>
  /// Line shafting and the transmission base. A shaft is a plain cast-iron run; the transmission is RCC, so
  /// its base is cheap and its gears are charged in its stages.
  /// </summary>
  private static ExRecipeDef Shafting(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "shafting")
      // Two shafts from a rod and a plate. Shafting is the cheapest block on the network: a run has to be
      // long enough to reach several machines before any of them is worth building.
      .Grid(r =>
        r.Name("Cast Iron Shaft")
          .Pattern("HRP")
          .Size(3, 1)
          .Ingredient("R", Rod(1))
          .Ingredient("P", Plate(1))
          .Ingredient("H", Hammer)
          .OutputBlock($"{domain}:mpenergy-shaft-ns", 2)
      )
      // The RCC base: a bare housing. Its two gears and four shaft ingots are charged by the construction
      // stages, so this recipe covers only the frame.
      .Grid(r =>
        r.Name("Transmission Housing")
          .Pattern("P_P,PHP")
          .Size(3, 2)
          .Ingredient("P", Plate(1))
          .Ingredient("H", Hammer)
          .OutputBlock($"{domain}:mpenergy-transmission-x2-n", 1)
      )
      // x4 and clutch are the same housing re-cased; each is built up by its own RCC stages afterwards.
      .Grid(r =>
        r.Name("Transmission Housing (Four Way)")
          .Pattern("PTP")
          .Size(3, 1)
          .Ingredient(
            "T",
            i => i.Block($"{domain}:mpenergy-transmission-x2-*").Quantity(1)
          )
          .Ingredient("P", Plate(1))
          .OutputBlock($"{domain}:mpenergy-transmission-x4-n", 1)
      )
      .Grid(r =>
        r.Name("Transmission Housing (Clutch)")
          .Pattern("RTR")
          .Size(3, 1)
          .Ingredient(
            "T",
            i => i.Block($"{domain}:mpenergy-transmission-x2-*").Quantity(1)
          )
          .Ingredient("R", Rod(1))
          .OutputBlock($"{domain}:mpenergy-transmission-clutch-n", 1)
      );
}
