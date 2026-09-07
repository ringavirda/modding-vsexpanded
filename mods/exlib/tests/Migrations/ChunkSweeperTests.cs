using ExpandedLib.Migrations;
using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// <see cref="ChunkColumnSweeperModSystem"/>'s own base-class contract: <c>BuildWork</c> runs exactly
/// once no matter how many entry points ask, and <c>ShouldVisit</c>/<c>VisitCell</c> gate which cells a
/// subclass's sweep actually touches. The chunk-by-chunk walk itself
/// (<c>SweepAllLoadedChunks</c>/<c>OnChunkColumnLoaded</c>) reads world data off
/// <c>ICoreServerAPI.WorldManager</c>, which the headless harness does not model - <see cref="TestWorld"/>
/// stands up a block/block-entity store and a chunk-loaded flag per position
/// (<see cref="TestWorld.UnloadChunkAt"/>/<see cref="TestWorld.LoadChunkAt"/>) but no
/// <c>AllLoadedMapchunks</c>/<c>GetChunk</c> - so that walk is exercised through the two real
/// subclasses' own narrower entry points instead (<c>BlockEntityHealModSystem.HealOrphanAt</c> in
/// <see cref="BlockEntityHealTests"/>, a direct <c>VisitCell</c> call in <see cref="BlockRemovalTests"/>),
/// not through this base class.
/// </summary>
public class ChunkSweeperTests {
  private sealed class CountingSweeper : ChunkColumnSweeperModSystem {
    public int BuildWorkCalls;
    public bool HasWork = true;

    protected override bool BuildWork() {
      BuildWorkCalls++;
      return HasWork;
    }

    protected override int VisitCell(
      IBlockAccessor ba,
      BlockPos pos,
      int blockId
    ) => 0;
  }

  [Fact]
  public void EnsureInitialized_builds_the_work_table_exactly_once() {
    var sys = new CountingSweeper();

    bool first = (bool)ReflectionHelpers.Invoke(sys, "EnsureInitialized")!;
    bool second = (bool)ReflectionHelpers.Invoke(sys, "EnsureInitialized")!;

    Assert.True(first);
    Assert.True(second);
    Assert.Equal(1, sys.BuildWorkCalls);
  }

  [Fact]
  public void EnsureInitialized_reports_whatever_BuildWork_answered() {
    var sys = new CountingSweeper { HasWork = false };

    bool hasWork = (bool)ReflectionHelpers.Invoke(sys, "EnsureInitialized")!;

    Assert.False(hasWork);
    Assert.Equal(1, sys.BuildWorkCalls);
  }

  [Fact]
  public void ShouldVisit_admits_every_block_id_by_default() {
    var sys = new CountingSweeper();

    Assert.True((bool)ReflectionHelpers.Invoke(sys, "ShouldVisit", 12345)!);
  }
}
