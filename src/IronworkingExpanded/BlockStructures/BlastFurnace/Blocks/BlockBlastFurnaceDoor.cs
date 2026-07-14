using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries.Entities;
using IronworkingExpanded.BlockStructures.BlastFurnace.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace IronworkingExpanded.BlockStructures.BlastFurnace.Blocks;

/// <summary>
/// The blast furnace door - the anchor block of the furnace multiblock. Handles
/// oriented placement of the structure and routes Ctrl + right-click to the
/// <see cref="BlockEntityBlastFurnace"/> for the show/hide structure outline.
/// </summary>
[BlockRegister]
public partial class BlockBlastFurnaceDoor
  : BlockBeeHiveKilnDoor,
    IExBlockDefProvider
{
  #region Code-first definition

  /// <summary>The blast-furnace door blocktype, authored in C# (migrated from blastfurnace/door.json). The
  /// anchor of the whole furnace multiblock: its ~147-cell structure map is drawn as nine ASCII cross-sections
  /// (one per Y level, y=-3 the hearth floor up to y=5 the charging bell); the vanilla Door width/height/sounds
  /// and the gui/tp/ground hold transforms are per-type attribute maps.</summary>
  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "blastfurnacedoor", "blastfurnace/door")
        .Class<BlockBlastFurnaceDoor>()
        .EntityClass("iwex.BlockEntityBlastFurnace")
        .Behavior("MultiblockStructure")
        .Behavior("Door")
        .Behavior("BlockEntityInteract")
        .Behavior("Lockable")
        .EntityBehavior("Door")
        .AttributeByType("widthByType", "*", 1)
        .AttributeByType("heightByType", "*", 2)
        .AttributeByType(
          "openSoundByType",
          "*",
          "game:sounds/block/cokeovendoor-open"
        )
        .AttributeByType("closeSoundByType", "*", "game:sounds/block/metaldoor")
        .AttributeByType("easingSpeedByType", "*", 2)
        // The furnace footprint, drawn as one top-down cross-section per Y level (rows +Z, cols +X, origin
        // x=-3/z=0). y=-3 hearth floor .. y=5 the charging bell. Legend: # refractory brick, m monolith part,
        // D door (origin), T tap, Y tuyere, P pipe outlet, R reinforced hopper, B bell hopper, c coal/air,
        // a air. Compared as an unordered cell set by DefinitionParity, so the w-numbering is free.
        .MultiblockLayout(s =>
          s.Origin(-3, 0)
            .Legend('#', "game:refractorybricks-good-tier3")
            .Legend('m', "game:multiblock-monolithic-0-p1-0")
            .Legend('D', "iwex:blastfurnacedoor*")
            .Legend('T', "iwex:blastfurnacetap*")
            .Legend('Y', "iwex:blastfurnace-tuyere*")
            .Legend('P', "ppex:pipe-outlet*")
            .Legend('R', "iwex:hopperreinforced*")
            .Legend('B', "iwex:hopperbell*")
            .Legend('c', "@(air|coalpile)")
            .Legend('a', "game:air")
            .Layer(
              -3,
              """
              . . . . # .
              # # # # # #
              # # # # # #
              # # # # # #
              . . . . # .
              """
            )
            .Layer(
              -2,
              """
              . . . . # .
              # . # Y # #
              . . # c c T
              # . # Y # #
              . . . . # .
              """
            )
            .Layer(
              -1,
              """
              . . # # # .
              # # c c c #
              . T c c c #
              # # c c c #
              . . # # # .
              """
            )
            .Layer(
              0,
              """
              . . # D # .
              # # c c c #
              . # c c c #
              # # c c c #
              . . # # # .
              """
            )
            .Layer(
              1,
              """
              . . # m # .
              . # c c c #
              . # c c c #
              . # c c c #
              . . # # # .
              """
            )
            .Layer(
              2,
              """
              . . # # # .
              . # c c c #
              . # c c c #
              . # c c c #
              . . # # # .
              """
            )
            .Layer(
              3,
              """
              . . . . . .
              . . # P # .
              . . # a # .
              . . # P # .
              . . . . . .
              """
            )
            .Layer(
              4,
              """
              . . . . . .
              . . # # # .
              . . # B # .
              . . # # # .
              . . . . . .
              """
            )
            .Layer(
              5,
              """
              . . . . . .
              . . . # . .
              . . # R # .
              . . . # . .
              . . . . . .
              """
            )
        )
        .CreativeCommon("*")
        .Shape("iwex:blastfurnace/door")
        .RenderPass("OpaqueNoCull")
        .FaceCullMode("NeverCull")
        .Material(EnumBlockMaterial.Ceramic)
        .MaxStackSize(1)
        .SideAo(false)
        .HeldTpIdleAnimation("holdunderarm")
        .Replaceable(500)
        .Resistance(3.5f)
        .LightAbsorption(0)
        .NonSolid()
        .RawByType(
          "guiTransformByType",
          "*",
          new
          {
            origin = new
            {
              x = 0.49,
              y = 1,
              z = 0.8,
            },
            scale = 0.73,
          }
        )
        .RawByType(
          "tpHandTransformByType",
          "*",
          new
          {
            translation = new
            {
              x = -0.74,
              y = -1.22,
              z = -1.3,
            },
            rotation = new
            {
              x = 8,
              y = 11,
              z = 59,
            },
            origin = new
            {
              x = 0.5,
              y = 1,
              z = 1,
            },
            scale = 0.71,
          }
        )
        .RawByType(
          "groundTransformByType",
          "*",
          new
          {
            translation = new
            {
              x = 0,
              y = 0,
              z = 0,
            },
            rotation = new
            {
              x = -90,
              y = 0,
              z = 0,
            },
            origin = new
            {
              x = 0.5,
              y = 1,
              z = 0.85,
            },
            scale = 3,
          }
        )
        .SingleSelectionBox(0, 0, 0.6875f, 1, 1, 0.9375f)
        .SingleCollisionBox(0, 0, 0.6875f, 1, 1, 0.9375f)
        .Sounds(
          "game:block/metaldoor-place",
          "game:block/metaldoor-place",
          "game:block/metaldoor-place",
          "game:walk/stone*"
        )
        .MaterialDensity(2000),
    ];

  #endregion

  public override bool TryPlaceBlock(
    IWorldAccessor world,
    IPlayer byPlayer,
    ItemStack itemstack,
    BlockSelection blockSel,
    ref string failureCode
  )
  {
    BlockPos position = blockSel.Position;
    IBlockAccessor blockAccessor = world.BlockAccessor;

    return blockAccessor.GetBlock(position, 1).Id == 0
      && CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)
      && PlaceBFDoor(
        world,
        byPlayer,
        itemstack,
        blockSel,
        position,
        blockAccessor
      );
  }

  private bool PlaceBFDoor(
    IWorldAccessor world,
    IPlayer byPlayer,
    ItemStack itemstack,
    BlockSelection blockSel,
    BlockPos pos,
    IBlockAccessor ba
  )
  {
    ba.SetBlock(BlockId, pos);
    var beBlastFurnace = ba.GetBlockEntity<BlockEntityBlastFurnace>(pos);
    var beBehaviorDoor = beBlastFurnace.GetBehavior<BEBehaviorDoor>();

    beBehaviorDoor.RotateYRad = BEBehaviorDoor.getRotateYRad(
      byPlayer,
      blockSel
    );
    beBehaviorDoor.RotateYRad +=
      (beBehaviorDoor.RotateYRad == -MathF.PI) ? -MathF.PI : MathF.PI;

    // Triggers the immediate rotation math in the BE so holograms work on tick 1
    beBlastFurnace.Init();

    if (world.Side == EnumAppSide.Server)
    {
      GetBehavior<BlockBehaviorDoor>().placeMultiblockParts(world, pos);
      beBlastFurnace.MarkDirty(true);
    }

    return true;
  }

  /// <summary>
  /// Replicates the standard <see cref="Block.GetDrops"/> logic instead of calling
  /// <c>base</c>. The inherited <see cref="BlockBeeHiveKilnDoor.GetDrops"/> looks up a
  /// <c>BlockEntityBeeHiveKiln</c> to stamp <c>totalHoursHeatReceived</c> onto the drop;
  /// our block entity is a <see cref="BlockEntityBlastFurnace"/>, so that lookup returns
  /// null and the vanilla method NREs server-side, disconnecting the player who breaks
  /// the door. The blast furnace has no firing progress to preserve on the dropped item,
  /// so we simply return the normal drops.
  /// </summary>
  // CS8603: this faithfully mirrors Block.GetDrops, which returns null to mean
  // "no drops" - the caller (SpawnDropsAndRemoveBlock) null-guards the result.
#pragma warning disable CS8603
  public override ItemStack[] GetDrops(
    IWorldAccessor world,
    BlockPos pos,
    IPlayer byPlayer,
    float dropQuantityMultiplier = 1f
  )
  {
    bool preventDefault = false;
    List<ItemStack> behaviorDrops = [];

    foreach (BlockBehavior behavior in BlockBehaviors)
    {
      EnumHandling handling = EnumHandling.PassThrough;
      ItemStack[]? drops = behavior.GetDrops(
        world,
        pos,
        byPlayer,
        ref dropQuantityMultiplier,
        ref handling
      );
      if (drops != null)
        behaviorDrops.AddRange(drops);

      if (handling == EnumHandling.PreventSubsequent)
        return drops;
      if (handling == EnumHandling.PreventDefault)
        preventDefault = true;
    }

    if (preventDefault)
      return behaviorDrops.ToArray();

    if (Drops == null)
      return null;

    List<ItemStack> result = [];
    foreach (BlockDropItemStack drop in Drops)
    {
      ItemStack? stack = drop.ToRandomItemstackForPlayer(
        byPlayer,
        world,
        dropQuantityMultiplier
      );
      if (stack != null)
      {
        result.Add(stack);
        if (drop.LastDrop)
          break;
      }
    }

    result.AddRange(behaviorDrops);
    return result.ToArray();
  }
#pragma warning restore CS8603
}
