using System.Linq;
using IronworkingExpanded.Tests;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.BlockEntities;
using SteelmakingExpanded.BlockStructures.HotBlastFurnace.Blocks;
using Vintagestory.API.MathTools;
using Xunit;
using static IronworkingExpanded.Tests.FurnaceLayoutRig;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The hot blast furnace's half of the furnace geometry oracle. The assertions themselves live in iwex's
/// <see cref="FurnaceLayoutRig"/> - a wrong tuyere or tap offset is the same bug on every furnace, and the
/// cells it checks are iwex types - so all that belongs here is the one fact that is genuinely smex's:
/// the hot furnace is the furnace that <b>vents</b>, and its gas outlets must land on the pipe outlets its
/// own layout declares. (iwex's cold blast furnace and cupola assert the opposite, that they have none.)
/// </summary>
public class FurnaceGeometryTests
{
  [Fact]
  public void Hot_furnace_offsets_line_up_with_its_layout()
  {
    var be = new BlockEntityBlastFurnaceHot { Pos = new BlockPos(0, 16, 0) };
    Orient(be, "smex:blastfurnacecore-north", "north");

    Vec3i[] outlets = AssertFurnaceGeometry(
      be,
      BlockBlastFurnaceCoreHot.Definitions("smex").Single(),
      "smex:blastfurnacecore-*",
      "hot furnace"
    );

    // The rig already checked that every declared outlet lands on "lpex:pipe-outlet*". What is specific
    // to this furnace is that there are any at all - it is the only one of the three with a stack.
    Assert.NotEmpty(outlets);
  }
}
