using System.Collections.Generic;
using ExpandedLib.Definitions;
using IronworkingExpanded.Items;
using static ExpandedLib.Definitions.ExIngredients;

namespace IronworkingExpanded.Recipes.Grid;

/// <summary>
/// Code-first grid recipes for the <b>mechanical-energy</b> tier: the spur gear, the flywheel that buffers the
/// run, and the shafting that carries it. None of these had a recipe at all, so the whole mpenergy network was
/// creative-only - which meant the rolling mill and the twin-tub blower could not be powered even once they
/// were built.
/// <para>
/// The <b>cast-iron bevel</b> is deliberately absent: it is not crafted but made in world, by adding a
/// <see cref="BevelGearItemDefinitions">bevel gear</see> to a placed shaft (which is also why its blocktype
/// carries no creative entry). The <b>transmission</b> is right-click constructed, so what it needs from here
/// is only the cheap base block - its real cost is in its RCC stages.
/// </para>
/// </summary>
public class EnergyRecipeDefinitions : IExRecipeDefProvider
{
  public static IEnumerable<ExRecipeDef> Definitions(string domain) =>
    [Gears(domain), Flywheels(domain), Shafting(domain)];

  /// <summary>
  /// The spur gear, by two routes to the same item - and the pair is the mechanic, not a convenience.
  /// <para>
  /// <b>Cutting a gear wastes metal; the cupola route wastes less.</b> A player's first gears are hacked out
  /// of bought iron ingots at two ingots apiece, because that is all a fresh world has. Once a cupola is
  /// running, one cast-iron ingot yields <b>two</b> gears - a four-fold improvement - because cast iron is the
  /// cheap bulk metal of the tier and there is no reason to be sparing with it.
  /// </para>
  /// <para>
  /// The iron route must stay, and must not require cast iron: a fresh world must never need a furnace it
  /// has not built yet to build the parts that furnace's plant is made of. See
  /// <see cref="SpurGearItemDefinitions"/>.
  /// </para>
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
  /// The flywheel - the network's energy reservoir (<c>E = ½Iω²</c>), and the block that bridges a vanilla
  /// waterwheel or windmill into mpenergy. It is the first thing an iron-tier player builds on the network and
  /// the reason four machines can share one intermittent water drive.
  /// <para>
  /// Costed in <b>mass</b>, because mass is the whole point of a flywheel: the large variant stores far more,
  /// so it costs proportionally more iron.
  /// </para>
  /// <para>
  /// <b>The rim is cast wheel sections; the web is heavy plate.</b> That is what
  /// <c>castwheelsection</c> was drawn for (<see cref="CastPartItemDefinitions.CastWheelSectionUnits"/>), and
  /// the 3040 u total is a deliberately high cost: the network's whole energy reservoir priced at a couple
  /// of blooms of iron would never read as the capital purchase
  /// it is. The wheel is what buys the efficiency - four machines sharing one intermittent drive - so it is
  /// what should cost.
  /// </para>
  /// <para>
  /// <b>Both grids are diagram-led, and both are a flat material list.</b> Per
  /// <c>docs/design/diagram-crafting.md</c>, a diagram-crafted recipe is <b>the plan plus its bill of
  /// materials</b> - one cell per distinct ingredient carrying its quantity. It is explicitly not a picture
  /// of the product drawn in the grid: spreading the four rim sections across the four
  /// corners of a 3x3 to look like a wheel reads nicely and is exactly the wrong idiom, because the
  /// grid then encodes assembly the diagram is already responsible for. The diagram is <c>.Tool()</c>, so
  /// it is drafted once at the design table and reused - and each size has its <b>own</b> plan, because a
  /// 5×5×2 wheel is a different drawing from a 3×3×1 one even though building it is an upgrade.
  /// </para>
  /// </summary>
  private static ExRecipeDef Flywheels(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "flywheel")
      .Grid(r =>
        r.Name("Flywheel (from diagram)")
          .Pattern("DW,PG")
          .Size(2, 2)
          .Ingredient("D", i => i.Item($"{domain}:diagram-mpenergy-flywheel").Tool())
          // Four arcs close the rim; four plates spoke it to the hub; the gear couples it to the run.
          .Ingredient("W", i => i.Item($"{domain}:castwheelsection").Quantity(4))
          .Ingredient("P", i => i.Item($"{domain}:castplate-heavy").Quantity(4))
          .Ingredient("G", i => i.Item($"{domain}:{SpurGearItemDefinitions.Code}").Quantity(1))
          .OutputBlock($"{domain}:mpenergy-flywheel-normal-ns", 1)
      )
      // The large wheel: the same plan around a heavier rim, so it takes the normal wheel as its core rather
      // than being built from scratch. Upgrading in place is also how a real works grew. Eight sections
      // rather than four because the 5x5 rim has twice the circumference of the 3x3 one.
      .Grid(r =>
        r.Name("Flywheel, Large (from diagram)")
          .Pattern("DSW")
          .Size(3, 1)
          .Ingredient("D", i => i.Item($"{domain}:diagram-mpenergy-flywheellarge").Tool())
          .Ingredient("S", i => i.Item($"{domain}:castwheelsection").Quantity(8))
          .Ingredient("W", i => i.Block($"{domain}:mpenergy-flywheel-normal-*").Quantity(1))
          .OutputBlock($"{domain}:mpenergy-flywheel-large-ns", 1)
      );

  /// <summary>
  /// Line shafting and the transmission base. A shaft is a plain cast-iron run; the transmission is RCC, so
  /// its base is cheap and its gears are charged in its stages.
  /// </summary>
  private static ExRecipeDef Shafting(string domain) =>
    ExRecipeDef
      .Create(domain, "grid", "shafting")
      // Two shafts from a rod and a plate - shafting is the cheapest thing on the network by design, because
      // the run has to be long enough to reach four machines before any of them is worth building.
      .Grid(r =>
        r.Name("Cast Iron Shaft")
          .Pattern("HRP")
          .Size(3, 1)
          .Ingredient("R", Rod(1))
          .Ingredient("P", Plate(1))
          .Ingredient("H", Hammer)
          .OutputBlock($"{domain}:mpenergy-shaft-ns", 2)
      )
      // The RCC base: a bare housing. The two gears and four shaft ingots are charged by its stages, so this
      // is only the frame you place before building it up (the design-table pattern).
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
          .Ingredient("T", i => i.Block($"{domain}:mpenergy-transmission-x2-*").Quantity(1))
          .Ingredient("P", Plate(1))
          .OutputBlock($"{domain}:mpenergy-transmission-x4-n", 1)
      )
      .Grid(r =>
        r.Name("Transmission Housing (Clutch)")
          .Pattern("RTR")
          .Size(3, 1)
          .Ingredient("T", i => i.Block($"{domain}:mpenergy-transmission-x2-*").Quantity(1))
          .Ingredient("R", Rod(1))
          .OutputBlock($"{domain}:mpenergy-transmission-clutch-n", 1)
      );
}
