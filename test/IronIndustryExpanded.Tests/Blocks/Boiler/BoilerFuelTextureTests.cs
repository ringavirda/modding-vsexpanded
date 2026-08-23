using System.Collections.Generic;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Boiler.BlockEntities;
using IronIndustryExpanded.BlockStructures.Furnaces;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The pure half of the boiler's fuel-texture resolver: bed <c>FuelCode</c> to the charged item's own
/// texture path (<see cref="BlockEntityBoiler.FuelTexturePath"/>), the seam the
/// <c>ITexPositionSource</c> indexer sits on top of. The indexer itself needs a live
/// <c>ICoreClientAPI</c> and a baked texture atlas that no fixture in this suite stands up -
/// <c>StorageRackTests</c> exercises <c>BlockEntityStorageRack</c>'s identical seam no further than
/// this either, since every one of its scenes runs server-side. What is proven here: a charged fuel the
/// boiler's blocktype never declared resolves to its own texture path, and an empty or unresolvable bed
/// answers no path at all - the indexer's documented trigger for its pink-avoiding default. What is NOT
/// proven here: the atlas lookup, the insert-on-demand, or that the default itself ever draws.
/// </summary>
public class BoilerFuelTextureTests {
  private static BlockEntityBoilerCornish Boiler(TestWorld world) {
    var be = new BlockEntityBoilerCornish {
      Pos = new BlockPos(0, 0, 0),
      Block = TestBlocks.Configure(new Block(), "iiex:boilercornish-n", 1),
    };
    world.Attach(be);
    return be;
  }

  /// <summary>Hosts a bare fuel bed on <paramref name="be"/>, exactly as <c>FireboxConfigTests</c>
  /// stands one up - added to <c>Behaviors</c> directly, since no shipped boiler blocktype declares the
  /// behaviour yet (a later task wires it in).</summary>
  private static BEBehaviorFirebox Bed(BlockEntityBoilerCornish be) {
    var bed = new BEBehaviorFirebox(be);
    be.Behaviors.Add(bed);
    return bed;
  }

  [Fact]
  public void A_bed_holding_a_coal_the_boiler_never_declared_resolves_to_its_own_texture() {
    var world = new TestWorld();
    BlockEntityBoilerCornish boiler = Boiler(world);
    BEBehaviorFirebox bed = Bed(boiler);

    // game:ore-lignite: a coal BlockBoiler.BoilerShell has no .Texture(...) call to declare - the exact
    // scenario CB5 exists for.
    Item lignite = world.RegisterItem("game:ore-lignite");
    var lignitePath = new AssetLocation("game:item/resource/ore/lignite1");
    lignite.Textures = new Dictionary<string, CompositeTexture> {
      ["all"] = new CompositeTexture(lignitePath),
    };
    bed.TryAdd(new ItemStack(lignite, 4), 4);

    Assert.Equal(lignitePath, boiler.FuelTexturePath());
  }

  [Fact]
  public void An_empty_bed_answers_no_path() {
    var world = new TestWorld();
    BlockEntityBoilerCornish boiler = Boiler(world);
    Bed(boiler); // hosted, never charged

    Assert.Null(boiler.FuelTexturePath());
  }

  [Fact]
  public void A_fuel_code_that_no_longer_resolves_answers_no_path() {
    var world = new TestWorld();
    BlockEntityBoilerCornish boiler = Boiler(world);
    BEBehaviorFirebox bed = Bed(boiler);

    // Never registered with the world - models the mod that shipped this fuel being removed while a
    // save with a charged bed still exists: the bed's own code round-trips, but nothing resolves it.
    var ghost = new Item {
      Code = new AssetLocation("othermod:ghostcoal"),
      ItemId = 999,
      CombustibleProps = new CombustibleProperties { BurnTemperature = 1200 },
    };
    bed.TryAdd(new ItemStack(ghost, 2), 2);

    Assert.Null(boiler.FuelTexturePath());
  }
}
