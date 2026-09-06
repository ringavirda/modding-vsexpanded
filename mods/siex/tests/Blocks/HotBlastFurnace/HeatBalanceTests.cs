using ExpandedLib.Industry.Heat;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using IronIndustryExpanded;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.Items;
using SteelIndustryExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelIndustryExpanded.Tests;

/// <summary>
/// Heat-balance parity between the cold and hot blast furnaces, the one fact that needs both types in
/// scope. The model itself - calibration table, contributors, clamps - is iiex's and is asserted there
/// against the cold furnace's block entity; iiex cannot see the hot type, so their agreement is pinned
/// here.
/// </summary>
[Collection("IiexFurnaceConfig")]
public class HeatBalanceTests {
  private static BlockEntityBlastFurnaceHot HotFurnace() =>
    Stand(new BlockEntityBlastFurnaceHot(), "siex:blastfurnacecore-n");

  private static BlockEntityBlastFurnaceCold ColdFurnace() =>
    Stand(new BlockEntityBlastFurnaceCold(), "iiex:furnace-blastcore-tier1-n");

  // Two concrete overloads rather than one generic: a `where T : BlockEntity` constraint is resolved by
  // xUnit's discovery reflection before the module initializer registers VsAssemblyResolver.

  private static BlockEntityBlastFurnaceHot Stand(
    BlockEntityBlastFurnaceHot be,
    string code
  ) {
    StandUp(be, code);
    return be;
  }

  private static BlockEntityBlastFurnaceCold Stand(
    BlockEntityBlastFurnaceCold be,
    string code
  ) {
    StandUp(be, code);
    return be;
  }

  private static void StandUp(BlockEntityMultiblockStructure be, string code) {
    var world = new TestWorld();
    be.Pos = new BlockPos(0, 16, 0);
    be.Block = TestBlocks.Configure(new Block(), code, 1, ("side", "north"));
    world.Attach(be);
    be.ApplyStructureRotation();
    ReflectionHelpers.Invoke(be, "CacheAttributes");
  }

  private static HeatBalance Balance(
    object be,
    float fuelFrac,
    float blastTemp
  ) =>
    (HeatBalance)
      ReflectionHelpers.Invoke(
        be,
        "ComputeHeatBalance",
        new BurdenMix(1f - 0.05f - fuelFrac, 0.05f, fuelFrac),
        1f,
        blastTemp,
        // The furnace's own capacity, so every row below is the full-charge case, which is what the
        // parity claim needs.
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
  ) {
    // Same charge and same blast into both block entities: they must agree to the degree. What differs
    // in game is the layout they sit in and whether a cowper is on the line, never the model.
    HeatBalance cold = Balance(ColdFurnace(), fuelFrac, blastTemp);
    HeatBalance hot = Balance(HotFurnace(), fuelFrac, blastTemp);

    Assert.Equal(cold, hot);
  }
}
