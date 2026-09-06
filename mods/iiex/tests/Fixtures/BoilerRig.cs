using System.Linq;
using BoilerState = IronIndustryExpanded.BlockStructures.Boiler.BlockEntityBoiler.BoilerState;
using ExpandedLib.Industry.Pipes;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Boiler;
using IronIndustryExpanded.BlockStructures.Boiler.BlockEntities;
using IronIndustryExpanded.BlockStructures.Boiler.Blocks;
using IronIndustryExpanded.BlockStructures.Furnaces;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Assembles a fully-constructed, fired Cornish boiler whose production tick runs. The vessel is
/// self-contained, so the rig stands up only what it really has:
/// <list type="bullet">
/// <item>a real <see cref="BlockBoilerCornish"/> carrying its shipped attributes, so the geometry
/// offsets and the footprint are the ones that ship;</item>
/// <item>a complete <see cref="ExpandedLib.Blocks.ExRightClickConstructable"/>, so
/// <c>IsConstructed</c> is true;</item>
/// <item>the fuel bed its blocktype declares, charged and lit, so the fire is on.</item>
/// </list>
/// Drive it with <see cref="Tick"/> and prime operating state with the <c>Set*</c> helpers.
/// </summary>
internal sealed class BoilerRig {
  /// <summary>Fuel the bed is charged with unless a test names another.</summary>
  public const string DefaultFuel = "game:ore-bituminouscoal";

  public readonly TestWorld World;
  public readonly BlockEntityBoilerCornish Be;
  public readonly BlockBoilerCornish Block;

  public BoilerRig(string fuelCode = DefaultFuel) {
    World = new TestWorld();
    World.RegisterNetwork("pipe", sys => new PipeNetwork(sys));

    Block = TestBlocks.Configure(
      new BlockBoilerCornish(),
      "iiex:boilercornish-n",
      1,
      ("side", "north")
    );
    Be = new BlockEntityBoilerCornish {
      Pos = new BlockPos(0, 8, 0),
      Block = Block,
    };
    World.Place(Be.Pos, Block, Be);
    BoilerFakes.Commission(World, Be, BoilerFakes.CornishDef);

    // The premise, asserted rather than assumed: a stub block or a blocktype that stopped declaring
    // the bed would leave every test below green against a boiler that can never be fired.
    Assert.IsType<BlockBoilerCornish>(Be.Block);
    Assert.NotNull(Bed);

    ChargeBed(fuelCode);
    RelightFire();
  }

  /// <summary>The boiler's own fuel bed, hosted from its shipped blocktype declaration.</summary>
  public BEBehaviorFirebox Bed => Be.Bed!;

  /// <summary>Whether the fire is alight.</summary>
  public bool Lit => (bool)ReflectionHelpers.GetField(Be, "_lit")!;

  /// <summary>Runs the production tick directly, bypassing the tick-listener scheduling.</summary>
  public void Tick(float dt = 1f, int times = 1) {
    for (int i = 0; i < times; i++)
      Be.DriveProductionTick(dt);
  }

  public BoilerState State =>
    (BoilerState)ReflectionHelpers.GetField(Be, "_state")!;

  public float WaterVolume =>
    (float)ReflectionHelpers.GetField(Be, "_waterVolume")!;

  public float SteamVolume =>
    (float)ReflectionHelpers.GetField(Be, "_steamVolume")!;

  public BoilerRig SetState(BoilerState state) {
    ReflectionHelpers.SetField(Be, "_state", state);
    return this;
  }

  public BoilerRig SetWater(float litres) {
    ReflectionHelpers.SetField(Be, "_waterVolume", litres);
    return this;
  }

  public BoilerRig SetSteam(float litres) {
    ReflectionHelpers.SetField(Be, "_steamVolume", litres);
    return this;
  }

  public BoilerRig SetHeatingSeconds(float seconds) {
    ReflectionHelpers.SetField(Be, "_heatingSeconds", seconds);
    return this;
  }

  public BoilerRig SetShutdownSeconds(float seconds) {
    ReflectionHelpers.SetField(Be, "_shutdownSeconds", seconds);
    return this;
  }

  /// <summary>
  /// Charges the bed with <paramref name="fuelCode"/>, registering it with the world first so it
  /// resolves as a real burnable item. <paramref name="units"/> defaults to filling the bed, which is
  /// what the firing gesture requires.
  /// </summary>
  public BoilerRig ChargeBed(string fuelCode, int units = 0) {
    Item fuel = World.RegisterItem(fuelCode);
    int want = units > 0 ? units : Bed.CellCapacity;
    Bed.Clear();
    Bed.TryAdd(new ItemStack(fuel, want), want);
    return this;
  }

  /// <summary>Empties the bed, leaving the vessel charged with nothing.</summary>
  public BoilerRig EmptyBed() {
    Bed.Clear();
    return this;
  }

  /// <summary>
  /// The element set the shipped construction stages raise, in the form the construction behaviour hands
  /// the animator: one <c>elem/*</c> selector per element a stage adds. Read off the definition rather
  /// than listed here, so a stage that stops raising a group takes this with it.
  /// </summary>
  public static string[] BuiltElements {
    get {
      JObject? construction = (
        BoilerFakes.CornishDef.ToJson()["entityBehaviors"] as JArray
      )
        ?.OfType<JObject>()
        .FirstOrDefault(b => (string?)b["name"] == ConstructionClass);
      Assert.True(
        construction != null,
        $"The Cornish definition declares no '{ConstructionClass}' behavior, so "
          + "the fixture would raise no shape elements at all."
      );
      return
      [
        .. (construction!["properties"]?["stages"] as JArray ?? [])
          .Select(stage => stage["addElements"] as JArray)
          .Where(added => added != null)
          .SelectMany(added => added!.Select(e => (string?)e + "/*"))
          .Distinct(),
      ];
    }
  }

  /// <summary>The registered class code the shipped definition names its construction behaviour by.</summary>
  private const string ConstructionClass = "ExRightClickConstructable";

  /// <summary>
  /// Plants <paramref name="built"/> as the element set the faked construction has raised - the set the
  /// drawn mesh is composed from. Defaults to what the shipped stages raise.
  /// </summary>
  public BoilerRig Raise(string[]? built = null) {
    object animator = ReflectionHelpers.GetField(Be, "_animator")!;
    object rcc = ReflectionHelpers.GetField(animator, "_rcc")!;
    ReflectionHelpers.SetProperty(
      rcc,
      "shape",
      new CompositeShape { SelectiveElements = built ?? BuiltElements }
    );
    return this;
  }

  /// <summary>Snuffs the fire so the next tick sees no flame; the bed keeps its fuel.</summary>
  public BoilerRig ExtinguishFire() {
    ReflectionHelpers.SetField(Be, "_lit", false);
    return this;
  }

  /// <summary>Lights the bed again for a re-fire cycle.</summary>
  public BoilerRig RelightFire() {
    ReflectionHelpers.SetField(Be, "_lit", true);
    ReflectionHelpers.SetField(Be, "_fuelSeconds", 0f);
    return this;
  }
}
