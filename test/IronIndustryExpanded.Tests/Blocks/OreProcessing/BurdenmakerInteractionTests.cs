using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Helpers;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.OreProcessing.BlockEntities;
using IronIndustryExpanded.BlockStructures.OreProcessing.Blocks;
using NSubstitute;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;
using static IronIndustryExpanded.BlockStructures.OreProcessing.Blocks.BlockBurdenmaker;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Interaction routing for the burdenmaker: a click on a world cell reaches the block-entity call that
/// cell's class means. <see cref="BurdenmakerCellTests"/> covers the cell map itself.
/// <para>
/// Clicks go in through a real block - the principal or a <see cref="BlockStructureFiller"/> on a
/// footprint cell - never through the private <c>HandleInteract</c>, so
/// <see cref="IFillerInteractionTarget"/> forwarding is covered too. Filler positions come from
/// <see cref="StructureFillers.FootprintCells"/> off the shipped def rather than hard-coded coordinates.
/// </para>
/// </summary>
public class BurdenmakerInteractionTests {
  /// <summary>One deposit, small enough to fit one stack and every tank.</summary>
  private const int Load = 20;

  private static readonly BlockPos At = new(64, 110, 64);

  private static readonly string[] Sides = ["n", "e", "s", "w"];

  // Authored cells in the north frame and the class each one carries.
  private static readonly (
    int X,
    int Y,
    int Z,
    BurdenmakerCell Class
  )[] Authored =
  [
    (-1, 1, -1, BurdenmakerCell.OreHopper),
    (0, 1, -1, BurdenmakerCell.OreHopper),
    (1, 1, -1, BurdenmakerCell.FluxHopper),
    (0, 0, 0, BurdenmakerCell.Gate),
    (-1, 0, -1, BurdenmakerCell.Bunker),
    (0, 0, -1, BurdenmakerCell.Bunker),
    (1, 0, -1, BurdenmakerCell.Bunker),
    (-1, 0, 0, BurdenmakerCell.Bunker),
    (1, 0, 0, BurdenmakerCell.Bunker),
  ];

  private static readonly (int X, int Y, int Z) OreCell = (-1, 1, -1);
  private static readonly (int X, int Y, int Z) OtherOreCell = (0, 1, -1);
  private static readonly (int X, int Y, int Z) FluxCell = (1, 1, -1);
  private static readonly (int X, int Y, int Z) GateCell = (0, 0, 0);
  private static readonly (int X, int Y, int Z) BasinCell = (-1, 0, 0);
  private static readonly (int X, int Y, int Z) FarBasinCell = (1, 0, -1);

  #region Fixture

  private sealed record Rig(
    TestWorld World,
    BlockBurdenmaker Block,
    BlockEntityBurdenmaker Be,
    Item Ore,
    Item Lime
  );

  /// <summary>The player's held slot, plus everything the machine handed back during a click.</summary>
  private sealed record Hands(
    IServerPlayer Player,
    DummySlot Held,
    List<ItemStack> Offered,
    List<string> Errors
  );

  private static Rig NewRig(string side = "n", bool constructed = true) {
    var world = new TestWorld();
    world.World.Side.Returns(EnumAppSide.Server);
    world.RegisterItem("iiex:burden");
    world.RegisterItem("game:crushed-iron");
    world.RegisterItem("game:lime");

    var block = TestBlocks.Configure(
      new BlockBurdenmaker(),
      "iiex:burdenmaker-red-" + side,
      140,
      ("brick", "red"),
      ("side", side)
    );
    // Footprint read off the shipped def rather than hand-typed, so it follows the drawing.
    block.Attributes = new JsonObject(
      BlockBurdenmaker.Definitions("iiex").First().ToJson()["attributes"]!
    );
    ReflectionHelpers.SetField(block, "api", world.Api);

    var be = new BlockEntityBurdenmaker { Pos = At.Copy(), Block = block };
    world.Place(At, block, be);
    world.Initialize(be);
    // Construction gate. Applied after Initialize, which re-reads _rcc from the (absent) behaviours.
    if (constructed)
      RccFake.Complete(be);

    var filler = TestBlocks.Configure(
      new BlockStructureFiller(),
      "exlib:structurefiller",
      81
    );
    ReflectionHelpers.SetField(filler, "api", world.Api);
    foreach (
      FillerCell cell in StructureFillers.FootprintCells(
        block,
        At,
        block.StructureAngle
      )
    ) {
      var fillerBe = new BlockEntityStructureFiller {
        Pos = cell.Pos.Copy(),
        Principal = At.Copy(),
      };
      world.Place(cell.Pos, filler, fillerBe);
      world.Attach(fillerBe);
    }

    return new Rig(
      world,
      block,
      be,
      world.World.GetItem(new AssetLocation("game:crushed-iron"))!,
      world.World.GetItem(new AssetLocation("game:lime"))!
    );
  }

  private static Hands PlayerWith(
    ItemStack? held,
    bool ctrl = false,
    bool inventoryFull = false
  ) {
    DummySlot slot = held == null ? new DummySlot() : new DummySlot(held);
    var offered = new List<ItemStack>();
    var errors = new List<string>();

    var player = Substitute.For<IServerPlayer>();
    var entity = Substitute.For<EntityPlayer>();
    // Ctrl, not sneak - vanilla ground-storage placement owns sneak+right-click with a held item.
    // Controls is a real field on the proxy, so this sets the value the production path reads.
    entity.Controls.CtrlKey = ctrl;
    player.Entity.Returns(entity);

    var inventory = Substitute.For<IPlayerInventoryManager>();
    inventory.ActiveHotbarSlot.Returns(slot);
    inventory
      .TryGiveItemstack(Arg.Any<ItemStack>())
      .Returns(ci => {
        offered.Add(ci.Arg<ItemStack>());
        return !inventoryFull;
      });
    player.InventoryManager.Returns(inventory);

    // Errors are captured as codes: the convention is SendIngameError(code) with the text in lang.
    player
      .When(p => p.SendIngameError(Arg.Any<string>(), Arg.Any<string>()))
      .Do(ci => errors.Add(ci.ArgAt<string>(0)));

    return new Hands(player, slot, offered, errors);
  }

  /// <summary>
  /// Right-clicks the world cell at the authored offset <paramref name="cell"/>, through whichever real
  /// block stands there - the principal for the gate, a filler for everything else. Returns whether the
  /// click was consumed.
  /// </summary>
  private static bool Click(Rig rig, (int X, int Y, int Z) cell, Hands hands) {
    BlockPos pos = ExOrientation.GlobalPos(
      rig.Be.Pos,
      cell.X,
      cell.Y,
      cell.Z,
      rig.Block.StructureAngle
    );
    Block under = rig.World.GetBlock(pos);

    // Premise: a real cell of this machine is under the cursor. Clicking air returns quietly, which
    // would make every "nothing moved" assertion below pass for the wrong reason.
    Assert.True(
      ReferenceEquals(under, rig.Block) || under is BlockStructureFiller,
      $"no burdenmaker cell at local ({cell.X},{cell.Y},{cell.Z}) "
        + $"for side '{rig.Block.Variant["side"]}' - found '{under.Code}'"
    );

    var selection = new BlockSelection {
      Position = pos,
      Face = BlockFacing.UP,
    };
    return under.OnBlockInteractStart(rig.World.World, hands.Player, selection);
  }

  /// <summary>What a full-handed click on <paramref name="cell"/> may move: each hopper takes its own
  /// material and nothing else, and neither the gate nor the basin accepts a deposit at all.</summary>
  private static (int Ore, int Flux) Expected(
    BurdenmakerCell cell,
    bool holdingOre
  ) =>
    cell switch {
      BurdenmakerCell.OreHopper when holdingOre => (Load, 0),
      BurdenmakerCell.FluxHopper when !holdingOre => (0, Load),
      _ => (0, 0),
    };

  /// <summary>What is left in the player's hand - 0 once the slot has been emptied.</summary>
  private static int Held(Hands hands) =>
    hands.Held.Empty ? 0 : hands.Held.StackSize;

  private static void Fill(Rig rig, int ore, int flux) {
    if (ore > 0)
      Assert.True(
        rig.Be.TryLoadOre(new DummySlot(new ItemStack(rig.Ore, ore)), true)
      );
    if (flux > 0)
      Assert.True(
        rig.Be.TryLoadFlux(new DummySlot(new ItemStack(rig.Lime, flux)), true)
      );
  }

  #endregion

  #region Every cell, every facing, both materials

  public static TheoryData<
    string,
    int,
    int,
    int,
    BurdenmakerCell,
    bool
  > EveryCell() {
    var data = new TheoryData<string, int, int, int, BurdenmakerCell, bool>();
    foreach (string side in Sides)
      foreach (var (x, y, z, klass) in Authored)
        foreach (bool holdingOre in new[] { true, false })
          data.Add(side, x, y, z, klass, holdingOre);
    return data;
  }

  [Theory]
  [MemberData(nameof(EveryCell))]
  public void Every_cell_sends_a_deposit_to_the_tank_that_owns_it(
    string side,
    int localX,
    int localY,
    int localZ,
    BurdenmakerCell cell,
    bool holdingOre
  ) {
    // Covers two mutations: swapping the OreHopper and FluxHopper arms of the switch (each hopper
    // refuses the other's material, so a swapped arm moves nothing at all), and hard-coding the angle
    // passed to Classify - StructureAngle is 0 at side "n", so all four facings are needed to see it.
    Rig rig = NewRig(side);
    Item held = holdingOre ? rig.Ore : rig.Lime;
    Hands hands = PlayerWith(new ItemStack(held, Load), ctrl: true);

    bool handled = Click(rig, (localX, localY, localZ), hands);

    (int ore, int flux) = Expected(cell, holdingOre);
    // Every cell of a built machine consumes its click, so nothing is ever placed against its face.
    Assert.True(handled);
    Assert.Equal(ore, rig.Be.OreUnits);
    Assert.Equal(flux, rig.Be.FluxUnits);
    // Nothing reaches the basin except through the gate, whatever cell was clicked.
    Assert.Equal(0, rig.Be.BurdenUnits);
    Assert.Equal(Load - ore - flux, Held(hands));
  }

  [Theory]
  [InlineData("n")]
  [InlineData("e")]
  [InlineData("s")]
  [InlineData("w")]
  public void An_empty_hand_takes_back_from_the_hopper_that_was_clicked(
    string side
  ) {
    // The take arms are unreachable from the deposit table above. The two loads differ in material and
    // in size, so a swapped arm fails on either assertion alone.
    Rig rig = NewRig(side);
    Fill(rig, ore: 20, flux: 7);

    Hands hands = PlayerWith(null);
    Click(rig, OreCell, hands);
    Click(rig, FluxCell, hands);

    Assert.Equal(
      new[] { "crushed-iron", "lime" },
      hands.Offered.Select(s => s.Collectible.Code.Path)
    );
    Assert.Equal(new[] { 20, 7 }, hands.Offered.Select(s => s.StackSize));
    Assert.Equal(0, rig.Be.OreUnits);
    Assert.Equal(0, rig.Be.FluxUnits);
  }

  [Fact]
  public void Both_wide_cells_reach_the_same_hopper() {
    // The wide hopper spans two cells backed by a single tank.
    Rig rig = NewRig();
    Hands first = PlayerWith(new ItemStack(rig.Ore, Load), ctrl: true);
    Hands second = PlayerWith(new ItemStack(rig.Ore, Load), ctrl: true);

    Click(rig, OreCell, first);
    Click(rig, OtherOreCell, second);

    Assert.Equal(2 * Load, rig.Be.OreUnits);
  }

  #endregion

  #region The gate, and what is not the gate

  [Fact]
  public void Only_the_principal_pulls_the_gate() {
    // Gate and Bunker share the y = 0 course and the principal sits in the middle of the basin. A
    // basin cell wired to ToggleGate would empty the hoppers when the player reaches for the burden.
    Rig rig = NewRig("e");
    Fill(rig, ore: 30, flux: 10);

    Hands basin = PlayerWith(null);
    Click(rig, BasinCell, basin);

    Assert.False(rig.Be.GateOpen);
    Assert.Equal(30, rig.Be.OreUnits);
    Assert.Equal(10, rig.Be.FluxUnits);
    Assert.Equal(0, rig.Be.BurdenUnits);
    Assert.Empty(basin.Offered); // an empty basin has nothing to hand back

    Hands gate = PlayerWith(null);
    Click(rig, GateCell, gate);

    Assert.True(rig.Be.GateOpen);
    Assert.Equal(0, rig.Be.OreUnits);
    Assert.Equal(0, rig.Be.FluxUnits);
    Assert.Equal(40, rig.Be.BurdenUnits);
    // The gate is a lever, not a hatch: pulling it hands the player nothing.
    Assert.Empty(gate.Offered);
  }

  [Fact]
  public void A_gate_pulled_over_empty_hoppers_reports_the_refusal_by_code() {
    Rig rig = NewRig();
    Hands hands = PlayerWith(null);

    Click(rig, GateCell, hands);

    Assert.Equal(new[] { "iiex-burdenmaker-nothingloaded" }, hands.Errors);
    Assert.False(rig.Be.GateOpen);
  }

  [Fact]
  public void A_gate_pulled_over_a_full_basin_reports_the_other_refusal() {
    // Both refusals route through the same arm; the second shows the code is carried through rather
    // than emitted as a constant.
    Rig rig = NewRig();
    Fill(rig, ore: 30, flux: 10);
    Assert.True(rig.Be.ToggleGate(out _)); // batch made
    Assert.True(rig.Be.ToggleGate(out _)); // lid shut again, basin still full
    Fill(rig, ore: 5, flux: 0);

    Hands hands = PlayerWith(null);
    Click(rig, GateCell, hands);

    Assert.Equal(new[] { "iiex-burdenmaker-emptybunker" }, hands.Errors);
    Assert.Equal(5, rig.Be.OreUnits); // a refusal consumes nothing
  }

  #endregion

  #region The basin is take-only

  [Fact]
  public void A_basin_cell_hands_the_burden_back_and_never_takes_a_deposit() {
    // Take-only whatever is held: the gate is the basin's only inlet, and a full hand must still get
    // the burden back rather than have the click do nothing.
    Rig rig = NewRig("s");
    Fill(rig, ore: 30, flux: 10);
    Assert.True(rig.Be.ToggleGate(out _));

    Hands hands = PlayerWith(new ItemStack(rig.Ore, 16), ctrl: true);
    Click(rig, FarBasinCell, hands);

    Assert.Single(hands.Offered);
    Assert.Equal("burden", hands.Offered[0].Collectible.Code.Path);
    Assert.Equal(40, hands.Offered[0].StackSize);
    Assert.Equal(0, rig.Be.BurdenUnits);
    Assert.Equal(0, rig.Be.OreUnits); // the held ore went nowhere
    Assert.Equal(16, Held(hands)); // ...and is still in the player's hand
  }

  [Fact]
  public void A_full_inventory_drops_the_burden_rather_than_eating_it() {
    Rig rig = NewRig();
    Fill(rig, ore: 30, flux: 10);
    Assert.True(rig.Be.ToggleGate(out _));

    Hands hands = PlayerWith(null, inventoryFull: true);
    Click(rig, BasinCell, hands);

    Assert.Single(rig.World.Drops);
    Assert.Equal("burden", rig.World.Drops[0].Collectible.Code.Path);
    Assert.Equal(40, rig.World.Drops[0].StackSize);
    Assert.Equal(0, rig.Be.BurdenUnits);
  }

  #endregion

  #region The help text says what the cell does

  private const string HelpPrefix = "iiex:burdenmaker-help-";

  /// <summary>The hint codes shown for a cell, through the same entry points the game uses.</summary>
  private static string[] Help(Rig rig, (int X, int Y, int Z) cell) {
    BlockPos pos = ExOrientation.GlobalPos(
      rig.Be.Pos,
      cell.X,
      cell.Y,
      cell.Z,
      rig.Block.StructureAngle
    );
    var principalSel = new BlockSelection {
      Position = rig.Be.Pos.Copy(),
      Face = BlockFacing.UP,
    };
    IPlayer forPlayer = PlayerWith(null).Player;

    WorldInteraction[] help = pos.Equals(rig.Be.Pos)
      ? rig.Block.GetPlacedBlockInteractionHelp(
        rig.World.World,
        principalSel,
        forPlayer
      )
      : ((IFillerInteractionTarget)rig.Block).GetFillerInteractionHelp(
        rig.World.World,
        principalSel,
        forPlayer,
        pos
      );

    return [.. help.Select(h => h.ActionLangCode)];
  }

  private static string[] ExpectedHelp(BurdenmakerCell cell) =>
    cell switch {
      BurdenmakerCell.OreHopper =>
      [
        HelpPrefix + "addore",
        HelpPrefix + "addore-stack",
        HelpPrefix + "take",
      ],
      BurdenmakerCell.FluxHopper =>
      [
        HelpPrefix + "addflux",
        HelpPrefix + "addflux-stack",
        HelpPrefix + "take",
      ],
      BurdenmakerCell.Gate => [HelpPrefix + "gate"],
      _ => [HelpPrefix + "take"],
    };

  public static TheoryData<string, int, int, int, BurdenmakerCell> HelpCells() {
    var data = new TheoryData<string, int, int, int, BurdenmakerCell>();
    foreach (string side in Sides)
      foreach (var (x, y, z, klass) in Authored)
        data.Add(side, x, y, z, klass);
    return data;
  }

  [Theory]
  [MemberData(nameof(HelpCells))]
  public void Every_cell_advertises_its_own_verb(
    string side,
    int localX,
    int localY,
    int localZ,
    BurdenmakerCell cell
  ) {
    // There is no GUI, so the help overlay is the only thing distinguishing the two hoppers. Help is
    // classified per cell exactly as the click is; forwarding every filler cell to
    // GetPlacedBlockInteractionHelp would advertise the gate on every cell.
    Rig rig = NewRig(side);

    Assert.Equal(ExpectedHelp(cell), Help(rig, (localX, localY, localZ)));
  }

  [Fact]
  public void Before_construction_the_help_belongs_to_the_builder() {
    // Before construction the RCC behaviour advertises the next stage's materials, not the machine's
    // own verbs.
    Rig rig = NewRig(constructed: false);

    Assert.DoesNotContain(
      Help(rig, OreCell).Concat(Help(rig, GateCell)),
      code => code?.StartsWith(HelpPrefix) == true
    );
  }

  #endregion

  #region Sides that must not act

  [Fact]
  public void A_client_side_click_is_swallowed_but_changes_nothing() {
    // The server owns every mutation; the client still consumes the click so no block is placed
    // against the machine's face. Without the guard both sides mutate and the inventory desyncs.
    Rig rig = NewRig();
    rig.World.World.Side.Returns(EnumAppSide.Client);
    Hands hands = PlayerWith(new ItemStack(rig.Ore, Load), ctrl: true);

    Assert.True(Click(rig, OtherOreCell, hands));

    Assert.Equal(0, rig.Be.OreUnits);
    Assert.Equal(Load, Held(hands));
  }

  [Fact]
  public void Before_construction_finishes_the_click_falls_through_to_the_builder() {
    // The machine is raised by right-clicking through five construction stages, so HandleInteract must
    // leave the click unconsumed until construction finishes or the block can never be built.
    Rig rig = NewRig(constructed: false);
    Assert.False(rig.Be.IsConstructed); // the premise

    Hands hands = PlayerWith(new ItemStack(rig.Ore, Load), ctrl: true);

    Assert.False(Click(rig, OtherOreCell, hands));
    Assert.False(Click(rig, GateCell, hands));

    Assert.Equal(0, rig.Be.OreUnits);
    Assert.False(rig.Be.GateOpen);
    Assert.Equal(Load, Held(hands));
  }

  #endregion
}
