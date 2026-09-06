using System.Linq;
using System.Text;
using ExpandedLib.Blocks;
using ExpandedLib.Helpers;
using ExpandedLib.Machines;
using ExpandedLib.Registries;
using Vintagestory.API.Common;

namespace HelloExpanded;

/// <summary>
/// The whole state-and-info walk: a <see cref="PersistAttribute"/>-declared counter ticking on the
/// shared production lifecycle, and a <see cref="GetBlockInfo"/> built from <see cref="ExInfo"/> and
/// <see cref="ExBlockAccess"/> rather than a hand-written <c>StringBuilder</c>/neighbour loop.
/// </summary>
[BlockEntityRegister]
public class BlockEntityHello : BlockEntityProductionMachine {
  /// <summary>Ticks counted since this block was placed; saved automatically by <c>[Persist]</c>.</summary>
  [Persist]
  private int _ticks;

  protected override int ProductionTickMs => HelloValues.TickIntervalMs;

  // The sample has nothing to be un-ready for; a real machine gates this on structure/fuel/etc.
  protected override bool CanRunProduction => true;

  protected override void OnProductionTick(float dt) => _ticks++;

  /// <summary>Resets the counter to zero - called from <see cref="BlockHello"/>'s sneak-click handler.</summary>
  public void ResetTicks() => _ticks = 0;

  public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
    base.GetBlockInfo(forPlayer, dsc);
    dsc.Lang("helloexpanded:ticks", _ticks);
    int neighbours = Api.World.BlockAccessor
      .Neighbours<BlockEntityHello>(Pos)
      .Count();
    dsc.Lang("helloexpanded:neighbours", neighbours);
  }
}
