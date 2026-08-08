using System.Linq;
using System.Collections.Generic;
using ExpandedLib.Metals;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockNetworkMolten;
using IronworkingExpanded.BlockNetworkMolten.BlockEntities;
using IronworkingExpanded.BlockNetworkMolten.Blocks;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace IronworkingExpanded.Tests;

/// <summary>
/// Models the casting line (handbook casting article): a furnace tap pours molten iron into a molten
/// canal run that carries it - cell to cell, flow simulated - to a fitting at the far end, which casts
/// it. Here the head cell stands in for the furnace tap (the metal is pushed straight in); the run
/// ends in a mold pedestal casting into a tool mold and/or a canal tap draining into a parked barrel.
/// The molten network flows the metal; the fittings drain it each server tick.
/// </summary>
internal sealed class CastingLine
{
  private const string Iron = "game:ingot-iron";

  public readonly BlockEntityMoltenCanal Head;
  public readonly BlockEntityMoltenCanalMoldPedestal? Pedestal;
  public readonly BlockEntityMoltenCanalTap? Tap;

  private readonly Scene _scene;
  private readonly BlockEntityMoltenCanal[] _cells;

  /// <summary>
  /// Builds an N-cell ns canal run at <paramref name="origin"/>. The last cell is a mold pedestal
  /// (when <paramref name="endsInPedestal"/>) or a canal tap draining a barrel; the rest are straights.
  /// The head (origin) cell stands in for the furnace tap that pours metal into the run.
  /// </summary>
  public CastingLine(
    Scene scene,
    BlockPos origin,
    int length,
    bool endsInPedestal
  )
  {
    _scene = scene;
    scene.World.RegisterItem(Iron, 1500f);

    _cells = new BlockEntityMoltenCanal[length];
    for (int i = 0; i < length; i++)
    {
      BlockPos pos = origin.AddCopy(0, 0, i);
      bool last = i == length - 1;
      var block = CanalBlock(scene, i + 1);

      if (last && endsInPedestal)
      {
        Pedestal = new BlockEntityMoltenCanalMoldPedestal
        {
          Pos = pos.Copy(),
          Block = block,
        };
        scene.Node(pos, block, Pedestal, "molten");
        _cells[i] = Pedestal;
      }
      else if (last)
      {
        Tap = new BlockEntityMoltenCanalTap { Pos = pos.Copy(), Block = block };
        scene.Node(pos, block, Tap, "molten");
        Tap.TryTogglePouring(); // default is closed (severs); open it so the run reaches it
        _cells[i] = Tap;
      }
      else
      {
        _cells[i] = new BlockEntityMoltenCanal
        {
          Pos = pos.Copy(),
          Block = block,
        };
        scene.Node(pos, block, _cells[i], "molten");
      }
    }
    Head = _cells[0];
  }

  private static BlockMoltenCanal CanalBlock(Scene scene, int id)
  {
    var item = new Item { Code = new AssetLocation("game:ingot-iron") };
    scene.World.World.GetItem(Arg.Any<AssetLocation>()).Returns(item);

    var block = TestBlocks.Configure(
      new BlockMoltenCanal(),
      "smex:moltencanal-straight-ns",
      id,
      ("type", "straight"),
      ("orientation", "ns")
    );
    ReflectionHelpers.SetProperty(block, "Type", "straight");
    ReflectionHelpers.SetProperty(block, "Orientation", "ns");
    return block;
  }

  /// <summary>Pours <paramref name="units"/> of hot molten iron into the head cell (the furnace tap).</summary>
  public CastingLine PourIn(int units, float temp = 1700f)
  {
    var metal = MoltenMetal.CreateStack(_scene.World.World, Iron, temp)!;
    Head.PushMetal(units, metal, _scene.World.World);
    return this;
  }

  /// <summary>Casts a parked barrel onto the tap at the given drain speed (units/tick).</summary>
  public CastingLine ParkBarrel(float drainSpeed)
  {
    Tap!.IsBarrel = true;
    // The tap reads its drain speed live from the block's "drainSpeed" attribute, so prime it there.
    Tap.Block.Attributes = new JsonObject(
      new JObject { ["drainSpeed"] = drainSpeed }
    );
    return this;
  }

  /// <summary>Sets an empty tool mold on the pedestal.</summary>
  public CastingLine SetMold()
  {
    Pedestal!.IsMold = true;
    return this;
  }

  /// <summary>
  /// Advances the line: each tick the molten network flows metal toward the fitting, then the fitting
  /// drains its cell into the mold/barrel. Fittings are attached (not Initialized) so their drain tick
  /// is driven here rather than by the scene's listener pump - keeping the order explicit.
  /// </summary>
  public CastingLine Run(int ticks)
  {
    for (int i = 0; i < ticks; i++)
    {
      _scene.Step(1); // molten network flow + cooling
      if (Pedestal != null)
        ReflectionHelpers.Invoke(Pedestal, "OnServerTick", 1f);
      if (Tap != null)
        ReflectionHelpers.Invoke(Tap, "OnServerTick", 1f);
    }
    return this;
  }

  /// <summary>Total liquid metal still standing in the canal cells (excludes what's cast into the mold/barrel).</summary>
  public int TotalInRun
  {
    get
    {
      int t = 0;
      foreach (var c in _cells)
        t += c.CellAmount;
      return t;
    }
  }
}

/// <summary>
/// A casting <em>yard</em> rather than a single line: one furnace tap feeding a canal that forks at a
/// T-junction, so two stations cast from one heat. Where <see cref="CastingLine"/> is a length and a
/// direction - a loop says it better than a picture would - this one's whole point is its shape, so it
/// is laid out with <see cref="SceneDiagram"/> and reads as the thing it models:
/// <code>
///   T--+--P     T tap head, - west-east canal, + junction, P mold pedestal
///      |        | the north-south branch
///      B        B barrel tap
/// </code>
/// Note the two straight glyphs: a canal's connectors come straight off its orientation string, so a
/// west-east cell and a north-south one are different blocks. One glyph for both would lay a run that
/// looks connected on the page and is severed in the world - which is precisely the class of mistake a
/// diagram is supposed to make visible.
/// That is what the ASCII DSL is for. It earns nothing on a straight run, and nothing on a machine's
/// own footprint (which <see cref="StructureRig"/> reads from the shipped definition rather than
/// restating), but a branching network is exactly the case where coordinates hide the topology.
/// </summary>
internal sealed class CastingYard
{
  private const string Iron = "game:ingot-iron";

  public readonly BlockEntityMoltenCanal Head;
  public readonly BlockEntityMoltenCanalMoldPedestal Pedestal;
  public readonly BlockEntityMoltenCanalTap Tap;

  private readonly Scene _scene;
  private readonly List<BlockEntityMoltenCanal> _cells = [];

  public CastingYard(Scene scene, BlockPos origin)
  {
    _scene = scene;
    scene.World.RegisterItem(Iron, 1500f);

    // Placed through the legend below; captured as the diagram lays each one.
    BlockEntityMoltenCanal? head = null;
    BlockEntityMoltenCanalMoldPedestal? pedestal = null;
    BlockEntityMoltenCanalTap? tap = null;
    int nextId = 40;

    new SceneDiagram()
      .On('T', p => head = Straight(p, nextId++, orientation: "we"))
      .On('-', p => Straight(p, nextId++, orientation: "we"))
      .On('|', p => Straight(p, nextId++, orientation: "ns"))
      .On('+', p => Straight(p, nextId++, orientation: "nswe"))
      .On(
        'P',
        p =>
        {
          pedestal = new BlockEntityMoltenCanalMoldPedestal
          {
            Pos = p.Copy(),
            Block = Block(nextId++, "we"),
          };
          scene.Node(p, pedestal.Block, pedestal, "molten");
          _cells.Add(pedestal);
        }
      )
      .On(
        'B',
        p =>
        {
          tap = new BlockEntityMoltenCanalTap
          {
            Pos = p.Copy(),
            Block = Block(nextId++),
          };
          scene.Node(p, tap.Block, tap, "molten");
          tap.TryTogglePouring(); // default is closed (severs the run); open it
          _cells.Add(tap);
        }
      )
      .Layer(
        """
        T--+--P
           |
           B
        """,
        y: origin.Y,
        originX: origin.X,
        originZ: origin.Z
      );

    Head = head!;
    Pedestal = pedestal!;
    Tap = tap!;
  }

  private BlockEntityMoltenCanal Straight(
    BlockPos pos,
    int id,
    string orientation = "ns"
  )
  {
    var cell = new BlockEntityMoltenCanal
    {
      Pos = pos.Copy(),
      Block = Block(id, orientation),
    };
    _scene.Node(pos, cell.Block, cell, "molten");
    _cells.Add(cell);
    return cell;
  }

  private BlockMoltenCanal Block(int id, string orientation = "ns")
  {
    var item = new Item { Code = new AssetLocation(Iron) };
    _scene.World.World.GetItem(Arg.Any<AssetLocation>()).Returns(item);

    var block = TestBlocks.Configure(
      new BlockMoltenCanal(),
      $"iwex:molten-canal-straight-{orientation}",
      id,
      ("type", "straight"),
      ("orientation", orientation)
    );
    ReflectionHelpers.SetProperty(block, "Type", "straight");
    ReflectionHelpers.SetProperty(block, "Orientation", orientation);
    return block;
  }

  /// <summary>
  /// Pours up to <paramref name="units"/> of hot molten iron into the head cell (the furnace tap) and
  /// records what the cell actually took. A canal cell holds 50 u, so a big heat is delivered over
  /// several taps and the accepted total - not the requested one - is what conservation is measured
  /// against.
  /// </summary>
  public CastingYard PourIn(int units, float temp = 1700f)
  {
    int before = Head.CellAmount;
    Head.PushMetal(
      units,
      MoltenMetal.CreateStack(_scene.World.World, Iron, temp)!,
      _scene.World.World
    );
    TotalPoured += Head.CellAmount - before;
    return this;
  }

  /// <summary>Units the head cell has actually accepted across every pour.</summary>
  public int TotalPoured { get; private set; }

  /// <summary>Sets an empty mold on the pedestal and parks a barrel on the tap.</summary>
  public CastingYard OpenBothStations(float drainSpeed = 8f)
  {
    Pedestal.IsMold = true;
    Tap.IsBarrel = true;
    Tap.Block.Attributes = new JsonObject(
      new JObject { ["drainSpeed"] = drainSpeed }
    );
    return this;
  }

  /// <summary>Advances the yard: network flow, then each fitting drains its own cell.</summary>
  public CastingYard Run(int ticks)
  {
    for (int i = 0; i < ticks; i++)
    {
      _scene.Step(1);
      ReflectionHelpers.Invoke(Pedestal, "OnServerTick", 1f);
      ReflectionHelpers.Invoke(Tap, "OnServerTick", 1f);
    }
    return this;
  }

  /// <summary>Total liquid metal still standing in the canal cells.</summary>
  public int TotalInRun => _cells.Sum(c => c.CellAmount);
}
