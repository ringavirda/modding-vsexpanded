using ExpandedLib.Process;
using ExpandedLib.Testing;
using IronworkingExpanded;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using IronworkingExpanded.Items;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The one heat-balance fact that needs both furnaces in scope. The model itself - the calibration table,
/// the contributors, the clamps - is iwex's and is asserted there against the cold furnace's block entity;
/// what cannot be checked from that suite is that smex's furnace agrees, because iwex cannot see the hot
/// type. That parity is the whole point of the merge, so it is pinned here.
/// </summary>
public class HeatBalanceTests
{
  private static BlockEntityBlastFurnaceHot HotFurnace() =>
    Stand(new BlockEntityBlastFurnaceHot(), "smex:blastfurnacecore-n");

  private static BlockEntityBlastFurnaceCold ColdFurnace() =>
    Stand(new BlockEntityBlastFurnaceCold(), "iwex:furnace-blastcore-tier1-n");

  // Note: two concrete overloads rather than one generic - a `where T : BlockEntity` constraint is
  // resolved by xUnit's discovery reflection before the module initializer registers VsAssemblyResolver.

  private static BlockEntityBlastFurnaceHot Stand(
    BlockEntityBlastFurnaceHot be,
    string code
  )
  {
    StandUp(be, code);
    return be;
  }

  private static BlockEntityBlastFurnaceCold Stand(
    BlockEntityBlastFurnaceCold be,
    string code
  )
  {
    StandUp(be, code);
    return be;
  }

  private static void StandUp(BlockEntity be, string code)
  {
    var world = new TestWorld();
    be.Pos = new BlockPos(0, 16, 0);
    be.Block = TestBlocks.Configure(new Block(), code, 1, ("side", "north"));
    world.Attach(be);
    ReflectionHelpers.Invoke(be, "UpdateStructureRotation");
    ReflectionHelpers.Invoke(be, "CacheAttributes");
  }

  private static HeatBalance Balance(object be, float fuelFrac, float blastTemp) =>
    (HeatBalance)
      ReflectionHelpers.Invoke(
        be,
        "ComputeHeatBalance",
        new BurdenMix(1f - 0.05f - fuelFrac, 0.05f, fuelFrac),
        1f,
        blastTemp,
        // The furnace's own capacity, so every row below is the full-charge case - which is what the
        // parity claim against iwex needs. It passed `IwexValues.BlastMixRequiredToFire` (320) until that
        // key was deleted; 320 was a fire threshold, not a capacity, and a hot blast furnace holds far
        // more, so these rows were quietly the "loaded to a quarter" case on both sides of the parity.
        (int)ReflectionHelpers.GetProperty(be, "ChargeCapacityUnits")!
      )!;

  [Theory]
  [InlineData(0.20f, 20f)] // standard burden, cold blast
  [InlineData(0.20f, 950f)] // ... the same charge with a cowper on the line
  [InlineData(0.30f, 20f)] // the coke-rich charge the iron tier runs
  [InlineData(0.10f, 950f)] // the coke-lean charge only the steel tier can blow
  public void Both_furnaces_compute_the_same_balance_from_the_same_conditions(
    float fuelFrac,
    float blastTemp
  )
  {
    // The whole point of the merge: there is one blast furnace. Feed the cold anchor's block entity and
    // the hot anchor's the same charge and the same blast and they must agree to the degree - what
    // differs in game is the layout they sit in and whether a cowper is on the line, never the model.
    // Two independent copies of these formulas is what this replaces.
    HeatBalance cold = Balance(ColdFurnace(), fuelFrac, blastTemp);
    HeatBalance hot = Balance(HotFurnace(), fuelFrac, blastTemp);

    Assert.Equal(cold, hot);
  }
}
