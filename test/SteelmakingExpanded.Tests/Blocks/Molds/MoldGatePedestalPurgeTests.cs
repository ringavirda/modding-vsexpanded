using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using SteelmakingExpanded.Molds;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The one genuinely cross-mod half of the mold-pedestal coverage: the pedestal itself is iwex, but
/// the predicate that decides whether a tool mold is disabled is smex's <see cref="MoldGating"/>,
/// registered into exlib's <see cref="ExMoldGate"/> seam at startup. Only a project that sees both
/// mods can drive it, so it lives here; the rest of the pedestal's behaviour is covered by
/// <c>IronworkingExpanded.Tests.MoltenMoldPedestalTests</c>.
/// </summary>
[Collection(MoldGatingCollection.Name)]
public class MoldGatePedestalPurgeTests
{
  [Fact]
  public void A_disabled_mold_is_cleared_from_the_pedestal_on_the_next_tick()
  {
    var world = new TestWorld();
    var be = new BlockEntityMoltenCanalMoldPedestal
    {
      Pos = new BlockPos(0, 0, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "iwex:moltencanalmoldpedestal-ns",
        4,
        ("type", "moldpedestal"),
        ("orientation", "ns")
      ),
    };
    world.Attach(be);

    var moldBlock = TestBlocks.Configure(
      new Block(),
      "smex:toolmold-blue-fired-plate",
      61
    );
    world.Register(moldBlock);
    be.AddMold(new ItemStack(moldBlock));
    Assert.True(be.IsMold);

    // The pedestal (in iwex) routes its disable check through ExMoldGate; smex wires this predicate
    // in its ModSystem.Start, which the test harness doesn't run, so register it here.
    ExMoldGate.RegisterIsDisabled(MoldGating.IsToolMoldDisabled);
    try
    {
      MoldGating.SetEnabled("plate", false);
      // The pedestal purges its own mold on load (the chunk scan can't reach it).
      ReflectionHelpers.Invoke(be, "OnServerTick", 1f);

      Assert.False(be.IsMold);
      Assert.Null(be.MoldStack);
    }
    finally
    {
      MoldGating.SetEnabled("plate", true);
    }
  }
}
