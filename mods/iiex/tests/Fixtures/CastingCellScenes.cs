using System.Collections.Generic;
using ExpandedLib.Industry.Metals;
using ExpandedLib.Industry.Molten;
using ExpandedLib.Structures;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockNetworkMolten.BlockEntities;
using IronIndustryExpanded.BlockNetworkMolten.Blocks;
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
/// hotbar can be loaded with sand or a pattern, a canal on the launder face to pour from, and a capture
/// of the errors the cell sends back. Interactions go through
/// <see cref="BlockEntitySandCastingCell.OnInteract"/> rather than the private handlers, so the routing
/// decision (<c>CastingCellLogic.Decide</c>) stays under test. Errors are captured as codes only: the
/// convention is <c>SendIngameError(code)</c> with the text in lang. Shake-out drops land in
/// <see cref="Harvested"/>, since the player's inventory takes nothing.
/// </summary>
public sealed class CastingCellScenes {
  private const string Sand = "iiex:moldingsand";

  /// <summary>
  /// The pour metal: liquid well below the shipped pour minimum, so a cold pour still fills the cavity
  /// and the misrun rule alone decides the outcome. Cast iron would freeze short first, which is the
  /// other scrap rule.
  /// </summary>
  public const string Metal = "game:ingot-bronze";

  private const float MetalMeltingPoint = 950f;

  private static readonly BlockPos At = new(32, 8, 32, 0);

  private readonly DummySlot _hotbar = new();
  private readonly List<string> _errors = [];
  private BlockEntityMoltenCanal? _feed;

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

  /// <summary>Everything a shake-out produced, in order.</summary>
  public IReadOnlyList<ItemStack> Harvested => World.Drops;

  /// <summary>The item a misrun or short pour of <see cref="Metal"/> comes back as.</summary>
  public string ScrapCode =>
    MetalRegistry.SolidDropOf(new AssetLocation(Metal)).ToString();

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

    world.RegisterItem(Metal, MetalMeltingPoint);
    // What a misrun or short pour shakes out as; resolved through the registry so the fixture follows
    // whatever solid drop the metal has at run time.
    world.RegisterItem(
      MetalRegistry.SolidDropOf(new AssetLocation(Metal)).ToString()
    );

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
    string wood = "oak",
    float minPourTemp = 1150f, // the shipped patterns' minimum
    int outputQuantity = 1
  ) {
    var attributes = JToken.Parse(
      $$"""
      {
        "mold": {
          "size": "{{size}}",
          "shape": "iiex:casting/cell-filling-{{type}}",
          "capacity": {{capacity}},
          "cavity": [{ "x1": 3, "y1": 1, "z1": 3, "x2": 13, "y2": 4, "z2": 13 }],
          "output": { "type": "item", "code": "iiex:cast-{{type}}", "quantity": {{outputQuantity}} },
          "minPourTemp": {{minPourTemp}}
        }
      }
      """
    );
    return new ItemStack(
      PatternItem(
        $"iiex:pattern-{type}-{wood}",
        attributes,
        $"iiex:cast-{type}"
      )
    );
  }

  /// <summary>
  /// A pattern item registered in the world under <paramref name="code"/> and carrying
  /// <paramref name="attributes"/>, with its <paramref name="outputCode"/> registered alongside.
  /// Registered rather than merely held because a reloaded cell re-resolves its spec by the pattern's
  /// code, and shake-out resolves the output against the world.
  /// </summary>
  private Item PatternItem(string code, JToken attributes, string outputCode) {
    Item item =
      World.GetItem(new AssetLocation(code)) ?? World.RegisterItem(code);
    item.Attributes = new JsonObject(attributes);
    // Imprint calls DamageItem. At the default durability of 0 the first use takes the stack to -1,
    // the tool-breaks path, which nulls the slot and reaches for byEntity.SidedPos.
    item.Durability = 64;
    if (World.GetItem(new AssetLocation(outputCode)) == null)
      World.RegisterItem(outputCode);
    return item;
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

  /// <summary>One server tick of the cell: pull from the launder, then cool.</summary>
  public CastingCellScenes Tick() {
    ReflectionHelpers.Invoke(Cell, "OnServerTick", 1f);
    return this;
  }

  /// <summary>
  /// Feeds the impression from a canal cell on the launder face holding <see cref="Metal"/> at
  /// <paramref name="temp"/>, ticking the cell until the cavity is full. The canal is topped up between
  /// ticks, so the whole pour arrives at one temperature however large the impression.
  /// </summary>
  public CastingCellScenes PourUntilFull(float temp) {
    BlockEntityMoltenCanal feed = FeedCanal();
    BEBehaviorMoltenCell cell = Cell.GetBehavior<BEBehaviorMoltenCell>()!;
    for (
      int tick = 0;
      tick < 400 && cell.CellAmount < cell.MaxUnitCapacity;
      tick++
    ) {
      feed.PushMetalRaw(
        feed.MaxUnitCapacity - feed.CellAmount,
        Metal,
        temp,
        World.World
      );
      Tick();
    }
    return this;
  }

  /// <summary>
  /// Lets the cast stand until it has hardened: one tick to stamp the metal's temperature carrier at
  /// the current time, a long calendar jump, and a tick to read the cooled temperature back.
  /// </summary>
  public CastingCellScenes CoolToHardened() {
    Tick();
    World.AdvanceHours(200);
    return Tick();
  }

  /// <summary>The canal cell on the launder face the cell drains, placed on first use.</summary>
  private BlockEntityMoltenCanal FeedCanal() {
    if (_feed != null)
      return _feed;
    BlockPos pos = Cell.Pos.AddCopy(Cell.LaunderFace);
    var block = TestBlocks.Configure(
      new BlockMoltenCanal(),
      "iiex:molten-canal-straight-ns",
      142,
      ("type", "straight"),
      ("orientation", "ns")
    );
    block.SetNetworkTypeForTest("straight");
    block.ApplyOrientationForTest("ns");
    _feed = new BlockEntityMoltenCanal { Pos = pos.Copy(), Block = block };
    World.Place(pos, block, _feed);
    World.Attach(_feed);
    return _feed;
  }

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
