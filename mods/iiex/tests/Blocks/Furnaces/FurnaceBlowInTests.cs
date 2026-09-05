using System.Linq;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The blow-in: a shaft furnace does not catch by itself. A charged, blown, structurally sound shaft sits
/// dark until a player reaches a flame through an open tap-hole, and going out ends the blow-in with
/// everything else it ends, so lighting is once per campaign.
/// <para>
/// Driven through the tap block's own interaction wherever the gesture is the subject; the flame is handed
/// to the furnace directly only where the case is about what the furnace does with it. The firebox
/// machines keep lighting themselves - see <c>FireboxChargeTests</c> - which is the ruled split.
/// </para>
/// </summary>
[Collection(FurnaceConfigCollection.Name)]
public class FurnaceBlowInTests {
  #region The gate

  [Fact]
  public void A_charged_blown_furnace_that_nobody_lit_stays_dark() {
    // The premise of the whole unit, and the case every scene's BlowIn() step exists because of. The rig's
    // own factories light their furnaces; this one is built without that.
    var scene = new ColdBlastFurnaceRig(
      charge: 2 * 320,
      burden: ColdBlastFurnaceScenes.HighCoke
    ).PressuriseBlast();

    Assert.True(
      scene.Core.StructureComplete,
      "the furnace should have completed its own structure"
    );
    Assert.False(scene.Core.BlownIn);

    bool everLit = false;
    scene.RunLive(60, s => everLit |= s.State != FurnaceState.Idle);

    Assert.False(
      everLit,
      "a furnace nobody has lit must never catch, however well charged and blown"
    );
  }

  [Fact]
  public void A_torch_through_an_open_tap_lights_it() {
    // The whole ritual through the block's own interaction: break the plug, reach the flame in, re-plug.
    var scene = new ColdBlastFurnaceRig(
      charge: 2 * 320,
      burden: ColdBlastFurnaceScenes.HighCoke
    ).PressuriseBlast();

    scene.BlowIn();

    Assert.True(scene.Core.BlownIn);
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Firing, 30) > 0,
      "a lit furnace should catch on the next tick"
    );
    // Re-plugged by the ritual, which is what makes the blow-in cost a plug rather than being free.
    Assert.True(scene.IronTap.IsPlugged);
  }

  [Fact]
  public void A_plugged_tap_cannot_be_lit_through() {
    // The gate that makes the sequence cost a plug: the flame has to go through an open hole, so a player
    // who wants to light a furnace has to break its plug first and buy a new one afterwards.
    var scene = new ColdBlastFurnaceRig(
      charge: 2 * 320,
      burden: ColdBlastFurnaceScenes.HighCoke
    ).PressuriseBlast();

    Assert.True(scene.IronTap.IsPlugged); // premise: a fresh tap arrives stopped
    scene.TorchIronTap();

    Assert.False(scene.Core.BlownIn);
    bool everLit = false;
    scene.RunLive(30, s => everLit |= s.State != FurnaceState.Idle);
    Assert.False(everLit);
  }

  #endregion

  #region What lighting does and does not decide

  [Fact]
  public void A_lit_furnace_with_no_carbon_at_its_raceway_still_does_not_catch() {
    // The flame does not overrule the drawing. "Will it light" and "is it still alight" are one question
    // asked in one place, so a torch on a shaft with no fuel in front of its tuyeres buys nothing - which
    // is why the torch asks none of DeriveState's questions itself.
    var scene = ColdBlastFurnaceScenes.NoCokeAtTheRaceway();

    Assert.True(
      scene.Core.BlownIn,
      "the scene should have performed the ritual"
    );

    bool everLit = false;
    scene.RunLive(60, s => everLit |= s.State != FurnaceState.Idle);

    Assert.False(everLit);
  }

  [Fact]
  public void Lighting_twice_is_refused_rather_than_repeated() {
    var scene = ColdBlastFurnaceScenes.Complete();
    Assert.True(scene.Core.BlownIn);

    Assert.False(
      scene.Core.TryLightFromTap(scene.Core.Pos),
      "an already-lit furnace should refuse a second flame"
    );
  }

  [Fact]
  public void A_hearth_cannot_be_lit_through_a_tap_because_it_has_none() {
    // The ruled split, stated on the base type the tap actually calls: a firebox machine answers no to the
    // tap route and goes on catching by itself when it is loaded.
    var puddling = new BlockEntityPuddlingFurnace();
    Assert.False(puddling.TryLightFromTap(new BlockPos(0, 16, 0)));
  }

  #endregion

  #region Once per campaign

  [Fact]
  public void Going_out_ends_the_blow_in_so_a_recharged_shaft_has_to_be_lit_again() {
    // The reason a stored bit is safe here: the residue burn takes the carbon at every raceway with it, so
    // there is nothing for a stale flag to relight - and clearing it is what makes recharging a spent
    // furnace cost another plug rather than restarting it for free.
    var scene = ColdBlastFurnaceScenes.StandardBurden();

    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Firing, 30) > 0,
      "the furnace should have lit"
    );
    Assert.True(
      scene.RunUntil(s => s.State == FurnaceState.Idle, 2400) > 0,
      "a chilled furnace should go out"
    );

    Assert.False(scene.Core.BlownIn);

    // Recharged and still dark: the charge is back and the flame is not.
    scene.Recharge(2 * 320);
    bool everLit = false;
    scene.RunLive(60, s => everLit |= s.State != FurnaceState.Idle);
    Assert.False(
      everLit,
      "a recharged furnace must not relight itself off a blow-in that is over"
    );
  }

  [Fact]
  public void The_blow_in_round_trips_through_the_tree() {
    var scene = ColdBlastFurnaceScenes.Complete();
    Assert.True(scene.Core.BlownIn);

    var tree = new TreeAttribute();
    scene.Core.ToTreeAttributes(tree);
    Assert.True(tree.GetBool("blownIn"));

    ColdBlastFurnaceRig reloaded = ColdBlastFurnaceScenes.Complete();
    reloaded.Core.FromTreeAttributes(tree, reloaded.World.World);
    Assert.True(reloaded.Core.BlownIn);
  }

  [Fact]
  public void A_furnace_saved_before_the_blow_in_existed_keeps_burning() {
    // A running furnace in an existing world carries no `blownIn` key, and reading that as "never lit"
    // would put every one of them out on load - silently, because the state is derived and the next tick
    // would simply say Idle. The fallback reads the state it was saved in instead: burning stays lit, and
    // a furnace that was already out is not handed a blow-in it never had.
    ColdBlastFurnaceRig scene = ColdBlastFurnaceScenes.Complete();

    var burning = new TreeAttribute();
    burning.SetInt("bfState", (int)FurnaceState.Melting);
    scene.Core.FromTreeAttributes(burning, scene.World.World);
    Assert.True(scene.Core.BlownIn);

    var dark = new TreeAttribute();
    dark.SetInt("bfState", (int)FurnaceState.Idle);
    scene.Core.FromTreeAttributes(dark, scene.World.World);
    Assert.False(scene.Core.BlownIn);
  }

  #endregion
}
