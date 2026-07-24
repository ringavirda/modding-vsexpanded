using System.Linq;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using IronworkingExpanded.Tests;
using SteelmakingExpanded.BlockStructures.SmokeStack.BlockEntities;
using SteelmakingExpanded.BlockStructures.SmokeStack.Blocks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SteelmakingExpanded.Tests;

/// <summary>
/// A commissioned smoke stack: the real 72-cell chimney standing on its own footprint, with an exhaust
/// main docked against the face its orientation exposes. The stack is the furnace's safety valve - each
/// production tick it draws <see cref="SmexValues.SmokestackGasIntakeVolume"/> litres of exhaust off the
/// connected network and vents them, so a backed-up line does not choke the furnace.
/// <para>
/// The structure is <b>built, not asserted</b>: <see cref="StructureRig"/> raises the shipped layout and
/// the stack's own monitor tick completes it. That is what pins two things a forced
/// <c>StructureComplete</c> could not: that the anchor block is coded
/// <c>smex:smokestack-intake-*</c> - the code its own layout demands at the origin cell - and that the
/// main approaches along the one face the footprint leaves open. The chimney is a solid 3x3 column
/// through z=0..2, so a main laid across it would be sitting inside the stack; it has to come in through
/// the connector face at z=-1, which is also the only face <see cref="BlockEntitySmokeStack.HasConnectorAt"/>
/// accepts.
/// </para>
/// </summary>
internal sealed class SmokeStackRig
{
  // Clear of the layout's y=-1 foundation course.
  private static readonly BlockPos Anchor = new(0, 1, 0);

  public readonly TestWorld World;
  public readonly BlockEntitySmokeStack Stack;
  public readonly StructureRig Structure;

  /// <summary>The sealed exhaust main the stack vents, with the stack itself as one of its nodes.</summary>
  public readonly PipeNetwork Main;

  /// <param name="mainLength">Pipe cells in the exhaust main, not counting the stack's own node.</param>
  public SmokeStackRig(int mainLength = 4)
  {
    World = new TestWorld();
    World.RegisterNetwork("pipe", s => new PipeNetwork(s));

    Stack = new BlockEntitySmokeStack
    {
      Pos = Anchor.Copy(),
      // The layout's origin cell wants "smex:smokestack-intake*", so the anchor must actually wear that
      // code - a generic "smex:smokestack-north" leaves the structure one cell short forever.
      Block = TestBlocks.Configure(
        new Block(),
        "smex:smokestack-intake-tier3-n",
        70,
        ("type", "intake"),
        ("refractory", "tier3"),
        ("orientation", "n")
      ),
      Orientation = "north",
    };
    World.Place(Anchor, Stack.Block, Stack);
    World.Attach(Stack);

    // The main runs away along -Z from the stack's north face and is capped, so exhaust can build
    // pressure in it rather than leaking. The stack caps the near end itself.
    var pipe = PipeTestWorld.MakePipe(orientation: "ns", id: 71);
    for (int i = 1; i <= mainLength; i++)
      World.Place(Anchor.AddCopy(0, 0, -i), pipe);
    World.Place(
      Anchor.AddCopy(0, 0, -(mainLength + 1)),
      TestBlocks.Configure(new Block(), "game:rock", 99)
    );

    Structure = StructureRig
      .Around(
        World,
        Stack,
        BlockSmokeStackIntake.Definitions("smex").Single(),
        angle: 0
      )
      .Complete();

    World.AddNode(Anchor, "pipe");
    for (int i = 1; i <= mainLength; i++)
      World.AddNode(Anchor.AddCopy(0, 0, -i), "pipe");
    Main = (PipeNetwork)World.NetworkAt(Anchor)!;

    ReflectionHelpers.SetField(Stack, "_system", World.Networks);
  }

  /// <summary>Charges the main with <paramref name="litres"/> of hot exhaust - one furnace tick's spill.</summary>
  public SmokeStackRig SpillExhaust(float litres, float temp = 700f)
  {
    Main.TryProduceGas(
      litres,
      temp,
      "Exhaust",
      World.Accessor,
      maxOutputPressure: 20f
    );
    return this;
  }

  /// <summary>One production tick of the stack: draw its intake off the main and vent it.</summary>
  public SmokeStackRig Tick()
  {
    ReflectionHelpers.Invoke(Stack, "OnProductionTick", 1f);
    return this;
  }

  public float MainVolume => Main.State?.Volume ?? 0f;

  public float LastVented =>
    (float)ReflectionHelpers.GetField(Stack, "_lastConsumedAmount")!;
}
