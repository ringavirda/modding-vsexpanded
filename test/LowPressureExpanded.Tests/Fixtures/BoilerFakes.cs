using System.Linq;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using LowPressureExpanded.BlockStructures.Boiler.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using ModBoiler = LowPressureExpanded.BlockStructures.Boiler.BlockEntityBoiler;

namespace LowPressureExpanded.Tests;

/// <summary>
/// The wiring that makes a boiler's gated production tick run headlessly: a raised multiblock shell, a
/// finished right-click construction, and a burning firebox. Shared by the unit-level
/// <see cref="BoilerRig"/> and the integration-level <c>BoilerFixture</c>, so it lives in one place.
/// <para>
/// The two gates are genuinely different things and only one of them can be faked honestly. The
/// <b>shell</b> is a multiblock the player lays brick by brick, so it is <see cref="Commission">built</see>
/// - <see cref="StructureRig"/> raises the shipped layout and the boiler's own monitor tick completes it.
/// The <b>construction</b> is a right-click build-up whose stages consume items from a player's hotbar;
/// there is no headless equivalent, so that one stays a fake.
/// </para>
/// </summary>
internal static class BoilerFakes
{
  /// <summary>The Cornish boiler's shipped definition - the source of its footprint.</summary>
  public static ExBlockDef CornishDef =>
    BlockBoilerCornish.Definitions("lpex").Single();

  /// <summary>
  /// Raises <paramref name="be"/>'s real footprint and marks its right-click construction finished, so
  /// the production tick's two gates are open. Returns the rig, whose cells address the boiler's own
  /// fittings.
  /// <para>
  /// The boiler body extends along local +z and is deliberately raised <em>away</em> from the player, so
  /// its structure angle is <c>side + 180</c> - passing the plain side angle lands every cell a
  /// half-turn out and the shell never closes.
  /// </para>
  /// </summary>
  public static StructureRig Commission(
    TestWorld world,
    ModBoiler be,
    ExBlockDef def,
    int angle
  )
  {
    StructureRig rig = StructureRig.Around(world, be, def, angle);
    rig.Raise();
    world.Initialize(be);
    // Initialize re-reads _rcc off the (absent) behaviors and would clear a construction faked before
    // it, so the fake goes in afterwards. The structure monitor then has both gates to observe.
    ForceConstructed(be);
    rig.AwaitCompletion();
    return rig;
  }

  /// <summary>
  /// Marks <paramref name="be"/>'s right-click construction finished. Only the construction - the
  /// multiblock shell is raised for real by <see cref="Commission"/>. Order matters when the entity
  /// will also be Initialized: a real <c>Initialize</c> re-reads <c>_rcc</c> from the (absent)
  /// behaviors and would clear it, so callers that Initialize must call this again afterwards.
  /// </summary>
  public static void ForceConstructed(ModBoiler be) => RccFake.Complete(be);

  /// <summary>A lit coal pile holding fuel, for the firebox cell.</summary>
  public static BlockEntityCoalPile BurningPile(BlockPos pos)
  {
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
