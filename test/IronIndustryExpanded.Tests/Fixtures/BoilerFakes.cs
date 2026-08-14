using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Boiler.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ModBoiler = IronIndustryExpanded.BlockStructures.Boiler.BlockEntityBoiler;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Wiring that makes a boiler's gated production tick run headlessly: a raised multiblock shell, a
/// finished right-click construction, and a burning firebox. Shared by the unit-level
/// <see cref="BoilerRig"/> and the integration-level <c>BoilerFixture</c>. The shell is raised for
/// real by <see cref="StructureRig"/> from the shipped layout and completed by the boiler's own
/// monitor tick; the right-click construction consumes items from a player's hotbar and has no
/// headless equivalent, so it is faked.
/// </summary>
internal static class BoilerFakes {
  /// <summary>The Cornish boiler's shipped definition, the source of its footprint.</summary>
  public static ExBlockDef CornishDef =>
    BlockBoilerCornish.Definitions("iiex").Single();

  /// <summary>
  /// Raises <paramref name="be"/>'s real footprint and marks its right-click construction finished, so
  /// the production tick's two gates are open. Returns the rig, whose cells address the boiler's own
  /// fittings. The body extends along local +z, away from the player, so <paramref name="angle"/> must
  /// be <c>side + 180</c>; the plain side angle lands every cell a half-turn out and the shell never
  /// closes.
  /// </summary>
  public static StructureRig Commission(
    TestWorld world,
    ModBoiler be,
    ExBlockDef def,
    int angle
  ) {
    StructureRig rig = StructureRig.Around(world, be, def, angle);
    rig.Raise();
    world.Initialize(be);
    // Initialize re-reads _rcc off the (absent) behaviors and would clear a construction faked before
    // it, so the fake goes in afterwards.
    ForceConstructed(be);
    rig.AwaitCompletion();
    return rig;
  }

  /// <summary>
  /// Marks <paramref name="be"/>'s right-click construction finished; the multiblock shell is raised
  /// for real by <see cref="Commission"/>. A real <c>Initialize</c> re-reads <c>_rcc</c> from the
  /// (absent) behaviors and clears it, so callers that Initialize must call this again afterwards.
  /// </summary>
  public static void ForceConstructed(ModBoiler be) => RccFake.Complete(be);

  /// <summary>A lit coal pile holding fuel, for the firebox cell.</summary>
  public static BlockEntityCoalPile BurningPile(BlockPos pos) {
    var pile = new BlockEntityCoalPile { Pos = pos.Copy() };
    var inv = new InventoryGeneric(1, "coalpile", "test", null, null);
    inv[0].Itemstack = new ItemStack(
      TestBlocks.Configure(new Block(), "game:coal", 3)
    );
    ReflectionHelpers.SetField(pile, "inventory", inv);
    ReflectionHelpers.SetField(pile, "burning", true);
    return pile;
  }
}
