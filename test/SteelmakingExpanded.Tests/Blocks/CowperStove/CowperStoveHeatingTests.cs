using ExpandedLib.Networks;
using ExpandedLib.Testing;
using LowPressureExpanded.BlockNetworkPipe;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The cowper stove charges its brick core from hot furnace exhaust drawn off the gas network across
/// its connector face. This spans smex (the regenerator) and lpex (the <see cref="PipeNetwork"/>),
/// which is why it lives in the smex suite - the top mod of the two: a sealed exhaust run on the
/// stove's intake face heats the core and is drained; an unfed stove sits cold.
/// <para>
/// Both run on a built <see cref="CowperRig"/>. That matters more here than it looks: the stove used to
/// be stood up with a forced <c>StructureComplete</c>, a hand-poked <c>_connectorFace</c> and six
/// hand-poked tunables, none of which the machine had to agree with. Now the real <c>Initialize</c>
/// derives the face and caches the tunables, and the shell has to stand.
/// </para>
/// </summary>
public class CowperStoveHeatingTests
{
  [Fact]
  public void Hot_exhaust_charges_the_core_and_is_drawn_off()
  {
    var rig = new CowperRig();
    float before = rig.ExhaustVolume;

    rig.ChargeFromExhaust(exhaustTemp: 900f);

    Assert.True(
      rig.CoreTemperature > 20f,
      $"core should have heated, was {rig.CoreTemperature}"
    );
    Assert.True(
      rig.ExhaustVolume < before + 60f,
      "exhaust should have been consumed"
    );
  }

  [Fact]
  public void An_unfed_stove_stays_cold()
  {
    // Built, plumbed, and ticking - with nothing in the exhaust main to draw.
    var rig = new CowperRig();

    rig.Tick();

    Assert.Equal(20f, rig.CoreTemperature, 1);
  }

  [Fact]
  public void A_stove_whose_shell_is_breached_never_commissions()
  {
    // The counterpart the forced flag could never express: pull one brick out of a raised shell and
    // the stove drops back to incomplete on its own next monitor tick, so it stops regenerating.
    var rig = new CowperRig();
    Assert.True(rig.Stove.StructureComplete);

    rig.World.Place(
      rig.Structure.Cell(1, 0, 0),
      TestBlocks.Configure(new Vintagestory.API.Common.Block(), "game:air", 0)
    );
    rig.World.AdvanceBlockEntityTime(3000);

    Assert.False(rig.Stove.StructureComplete);
  }
}
