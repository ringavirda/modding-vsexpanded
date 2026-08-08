using ExpandedLib.Metals;
using ExpandedLib.Testing;
using NSubstitute;
using SteelmakingExpanded.BlockStructures.Converter;
using SteelmakingExpanded.BlockStructures.Converter.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// The Bessemer converter control's persisted state machine and its break handoff. The production
/// tick is gated on a constructed vessel plus four aligned peripherals (transmission MP network, gas
/// blast, molten in and out cells) and is not faked here; these cover the operational state that
/// survives a reload and the charge clearing on break.
/// </summary>
public class ConverterControlBeTests {
  private static readonly TestWorld ResolveWorld = NewResolveWorld();

  private static TestWorld NewResolveWorld() {
    var w = new TestWorld();
    w.RegisterItem("game:ingot-iron"); // so a saved charge stack resolves on reload
    return w;
  }

  // Units on the reflected MoltenCharge, or 0 when there is no charge.
  private static int ChargeUnits(BlockEntityConverterControl be) =>
    (ReflectionHelpers.GetField(be, "_charge") as MoltenCharge)?.Units ?? 0;

  private static BlockEntityConverterControl Control(TestWorld? world = null) {
    var be = new BlockEntityConverterControl {
      Pos = new BlockPos(0, 8, 0),
      Block = TestBlocks.Configure(
        new Block(),
        "smex:converterbessemercontrol-n",
        1,
        ("side", "north")
      ),
    };
    (world ?? ResolveWorld).Attach(be);
    return be;
  }

  private static ItemStack IronCharge(TestWorld world) {
    var item = new Item { Code = new AssetLocation("game:ingot-iron") };
    return new ItemStack(item, 1);
  }

  [Fact]
  public void OpState_defaults_to_normal() {
    Assert.Equal(ConverterOpState.Normal, Control().OpState);
  }

  [Fact]
  public void Operational_state_round_trips_through_the_tree() {
    var src = Control();
    ReflectionHelpers.SetProperty(
      src,
      "OpState",
      ConverterOpState.SteelPouring
    );
    ReflectionHelpers.SetField(
      src,
      "_charge",
      MoltenCharge.Of(IronCharge(ResolveWorld), 30)
    );
    ReflectionHelpers.SetField(src, "_carbon", 0.031f);
    ReflectionHelpers.SetField(src, "_scrapUnits", 45);
    ReflectionHelpers.SetField(src, "_moltenSlag", 12f);
    ReflectionHelpers.SetField(src, "_solidified", true);

    var tree = new TreeAttribute();
    src.ToTreeAttributes(tree);

    var dst = Control();
    dst.FromTreeAttributes(tree, ResolveWorld.World);

    Assert.Equal(ConverterOpState.SteelPouring, dst.OpState);
    Assert.Equal(30, ChargeUnits(dst)); // stack and units both round-trip
    // Carbon fraction, cold scrap and slag pool ride the tree as well.
    Assert.Equal(0.031f, (float)ReflectionHelpers.GetField(dst, "_carbon")!, 4);
    Assert.Equal(45, (int)ReflectionHelpers.GetField(dst, "_scrapUnits")!);
    Assert.Equal(
      12f,
      (float)ReflectionHelpers.GetField(dst, "_moltenSlag")!,
      2
    );
    Assert.True((bool)ReflectionHelpers.GetField(dst, "_solidified")!);
  }

  [Fact]
  public void Breaking_a_solidified_converter_returns_drops_and_clears_the_charge() {
    var world = new TestWorld();
    // The solidified-bits drop resolves the bit item; any non-null item yields a stack.
    world
      .World.GetItem(Arg.Any<AssetLocation>())
      .Returns(new Item { Code = new AssetLocation("game:metalbit-iron") });

    var be = Control(world);
    ReflectionHelpers.SetField(
      be,
      "_charge",
      MoltenCharge.Of(IronCharge(world), 20)
    );
    ReflectionHelpers.SetField(be, "_solidified", true);

    var drops = be.OnConverterBroken();

    Assert.NotNull(drops); // a solid plug scatters recoverable bits
    Assert.Equal(ConverterOpState.Normal, be.OpState);
    Assert.Null(ReflectionHelpers.GetField(be, "_charge")); // charge cleared
  }

  [Fact]
  public void Breaking_a_liquid_converter_clears_the_charge_without_drops() {
    var world = new TestWorld();
    var be = Control(world);
    ReflectionHelpers.SetField(
      be,
      "_charge",
      MoltenCharge.Of(IronCharge(world), 20)
    );
    ReflectionHelpers.SetField(be, "_solidified", false); // still molten - it spills, no solid drop

    var drops = be.OnConverterBroken();

    Assert.Null(drops);
    Assert.Null(ReflectionHelpers.GetField(be, "_charge")); // charge cleared
  }
}
