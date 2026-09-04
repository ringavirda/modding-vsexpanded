using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Blocks.Structures;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Casting;
using IronIndustryExpanded.BlockStructures.Casting.BlockEntities;
using IronIndustryExpanded.BlockStructures.Casting.Blocks;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// A placed 1×1 sand casting cell with a real <see cref="BEBehaviorMoltenCell"/> under it, a player whose
/// hotbar can be loaded with sand or a pattern, and a capture of the errors the cell sends back.
/// Interactions go through <see cref="BlockEntitySandCastingCell.OnInteract"/> rather than the private
/// handlers, so the routing decision (<c>CastingCellLogic.Decide</c>) stays under test. Errors are
/// captured as codes only: the convention is <c>SendIngameError(code)</c> with the text in lang.
/// </summary>
public sealed class CastingCellScenes {
  private const string Sand = "iiex:moldingsand";

  private static readonly BlockPos At = new(32, 8, 32, 0);

  private readonly DummySlot _hotbar = new();
  private readonly List<string> _errors = [];

  private CastingCellScenes(TestWorld world, BlockEntitySandCastingCell cell) {
    World = world;
    Cell = cell;
    Player = BuildPlayer();
  }

  public TestWorld World { get; }

  public BlockEntitySandCastingCell Cell { get; }

  public IPlayer Player { get; }

  /// <summary>The last error code the cell sent, or null if it sent none since the last interaction.</summary>
  public string? LastError => _errors.Count == 0 ? null : _errors[^1];

  #region Building one

  /// <summary>A cell rammed full of green sand and waiting for a pattern, the state every imprint test
  /// starts from.</summary>
  public static CastingCellScenes RammedFull() => Build(SandLevel.Full);

  /// <summary>A bare brick shell with no sand in it.</summary>
  public static CastingCellScenes Empty() => Build(SandLevel.Empty);

  /// <summary>The 1×2 long cell, rammed and waiting for a long-cell pattern. Only the principal is placed:
  /// the filler hosts nothing and reroutes to here, so it has no part in the state machine.</summary>
  public static CastingCellScenes RammedFullLongCell() =>
    Build(SandLevel.Full, longCell: true);

  private static CastingCellScenes Build(SandLevel sand, bool longCell = false) {
    var world = new TestWorld();

    Block block = longCell
      ? TestBlocks.Configure(
        new BlockSandCastingLongCell(),
        "iiex:casting-sandlongcell-fire-n",
        141,
        ("brick", "fire"),
        ("side", "n")
      )
      : TestBlocks.Configure(
        new BlockSandCastingCell(),
        "iiex:casting-sandcell-fire-n",
        140,
        ("material", "fire"),
        ("side", "n")
      );

    BlockEntitySandCastingCell be = longCell
      ? new BlockEntitySandCastingLongCell()
      : new BlockEntitySandCastingCell();
    world.Place(At, block, be);
    world.Attach(be);

    // The molten cell is a behaviour the blocktype declares; a headless entity must be given one or
    // OnInteract returns false before deciding anything.
    var molten = new BEBehaviorMoltenCell(be);
    be.Behaviors.Add(molten);
    molten.ConfigureFromFiller(null, null, new JsonObject(JToken.Parse("{}")));

    ReflectionHelpers.SetField(be, "_sand", sand);

    return new CastingCellScenes(world, be);
  }

  #endregion

  #region Stacks

  /// <summary>
  /// A pattern item carrying a <see cref="MoldSpec"/> for <paramref name="type"/>. The pattern carries the
  /// spec, so everything the cell reads about the cast is declared here.
  /// </summary>
  public ItemStack PatternStack(
    string type,
    string size = "cell",
    int capacity = 100,
    string wood = "oak"
  ) {
    var attributes = JToken.Parse(
      $$"""
      {
        "mold": {
          "size": "{{size}}",
          "shape": "iiex:casting/cell-filling-{{type}}",
          "capacity": {{capacity}},
          "cavity": [{ "x1": 3, "y1": 1, "z1": 3, "x2": 13, "y2": 4, "z2": 13 }],
          "output": { "type": "item", "code": "iiex:cast-{{type}}" },
          "minPourTemp": 1200
        }
      }
      """
    );

    var item = new Item {
      Code = new AssetLocation($"iiex:pattern-{type}-{wood}"),
      ItemId = 900 + type.Length + wood.Length,
      Attributes = new JsonObject(attributes),
      // Imprint calls DamageItem. At the default durability of 0 the first use takes the stack to -1,
      // the tool-breaks path, which nulls the slot and reaches for byEntity.SidedPos.
      Durability = 64,
    };
    return new ItemStack(item);
  }

  /// <summary>A stack of the green moulding sand the cell is rammed with.</summary>
  public ItemStack SandStack() =>
    new(new Item { Code = new AssetLocation(Sand), ItemId = 901 });

  /// <summary>A pattern-shaped item carrying no <c>mold</c> attribute, which the cell must tell apart
  /// from a pattern meant for the other station.</summary>
  public ItemStack SpeclessPatternStack() =>
    new(
      new Item {
        Code = new AssetLocation("iiex:pattern-broken-oak"),
        ItemId = 902,
      }
    );

  #endregion

  #region Driving it

  /// <summary>Puts <paramref name="held"/> in the player's active slot and right-clicks the cell.
  /// Returns whether the cell handled the click.</summary>
  public bool Interact(ItemStack? held) {
    _errors.Clear();
    _hotbar.Itemstack = held;
    return Cell.OnInteract(Player);
  }

  /// <summary>Right-clicks with an empty hand, the shake-out gesture.</summary>
  public bool InteractEmptyHanded() => Interact(null);

  #endregion

  #region Wiring

  private IPlayer BuildPlayer() {
    var player = Substitute.For<IServerPlayer>();
    var inventory = Substitute.For<IPlayerInventoryManager>();
    inventory.ActiveHotbarSlot.Returns(_hotbar);
    player.InventoryManager.Returns(inventory);

    var data = Substitute.For<IWorldPlayerData>();
    data.CurrentGameMode.Returns(EnumGameMode.Survival);
    player.WorldData.Returns(data);

    // DamageItem takes the entity and reads its position on the break path.
    var entity = Substitute.For<EntityPlayer>();
    entity.Pos.SetPos(At.X + 0.5, At.Y + 0.5, At.Z + 1.5);
    player.Entity.Returns(entity);

    player
      .When(p => p.SendIngameError(Arg.Any<string>(), Arg.Any<string>()))
      .Do(ci => _errors.Add(ci.ArgAt<string>(0)));

    return player;
  }

  #endregion
}
