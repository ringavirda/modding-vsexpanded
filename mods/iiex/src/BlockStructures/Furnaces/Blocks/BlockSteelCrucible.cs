using System.Collections.Generic;
using ExpandedLib.Definitions;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace IronIndustryExpanded.BlockStructures.Furnaces.Blocks;

/// <summary>
/// The steel crucible: a fireclay pot that holds a charge of blister steel at 1600 C, which vanilla's own
/// clay crucible cannot. It is a deliberate exception to this mod's clay ceiling - the ceiling governs
/// molds, which are reused indefinitely, while the pot is a consumable that dies from exactly this abuse
/// after <c>CruciblePotFirings</c> heats. See docs/design/machines/crucible-furnace.md.
/// </summary>
/// <remarks>
/// A block rather than an item, and it has to be: only a <see cref="BlockSmeltedContainer"/> can be
/// poured, which is what the molten path accepts - vanilla keeps its own crucible a block for the same
/// reason. Three variants, not two: a clayforming recipe can output only a <c>-raw</c> variant, which the
/// pit kiln fires, so a pot without one would be craftable nowhere.
/// </remarks>
[BlockRegister]
public partial class BlockSteelCrucible
  : BlockSmeltingContainer,
    IExBlockDefProvider {
  #region Code-first definition

  /// <summary>
  /// The burned pot's code, unqualified, for <c>emptiedBlockCode</c> alone: vanilla resolves that one with
  /// <c>AssetLocation.Create(code, Code.Domain)</c>, so a bare path lands in our own domain - and it
  /// throws on a null, so the key is mandatory.
  /// </summary>
  /// <remarks>
  /// A <c>smeltedStack</c> is resolved by the ordinary stack reader instead, which defaults an unqualified
  /// code to <c>game:</c> and would resolve to nothing at all. That one takes <see cref="BurnedStack"/>.
  /// The two keys read the same string two different ways.
  /// </remarks>
  public const string BurnedCode = "steelcrucible-burned";

  /// <summary>The burned pot as a stack reference: domain-qualified, because a stack code is not
  /// resolved against the declaring block's domain.</summary>
  private static string BurnedStack(string domain) => $"{domain}:{BurnedCode}";

  public static IEnumerable<ExBlockDef> Definitions(string domain) =>
    [
      ExBlockDef
        .Create(domain, "steelcrucible", "steelcrucible")
        .VariantGroup("type", "raw", "burned", "smelted")
        // The two vanilla chassis classes, by their bare registered names. A `Class<T>()` naming a
        // Vintagestory type compiles and emits `iiex.BlockSmeltingContainer`, which nobody registered:
        // vanilla registers the bare string in Core.cs, and nothing in the suite checks that a class
        // string resolves. `classByType` also has to be a top-level key, so it goes through RawByType -
        // AttributeByType would file it under `attributes`, where the loader never looks.
        .RootKeyByType(
          "classByType",
          "*-burned",
          nameof(BlockSmeltingContainer)
        )
        // The smelted class must be OURS and must derive from BlockSmeltedContainer: `DoSmelt` casts the
        // block it resolves without checking, so a class that is not one is an InvalidCastException on
        // the server, mid-tick.
        .RootKeyByType(
          "classByType",
          "*-smelted",
          EntityRegistry.KeyFor(domain, typeof(BlockSteelCruciblePour))
        )
        .RootKeyByType("entityClassByType", "*-smelted", "SmeltedContainer")
        .Shape($"{domain}:item/steelcrucible")
        .Material(EnumBlockMaterial.Ceramic)
        // The ground-storable / unplaceable / pick-up trio vanilla's crucible carries. It is a block that
        // behaves as an item, and every one of the three is load-bearing for that.
        .Behavior(
          "GroundStorable",
          new
          {
            layout = "Quadrants",
            placeRemoveSound = "sounds/player/build",
            collisionBox = new
            {
              x1 = 0,
              y1 = 0,
              z1 = 0,
              x2 = 1,
              y2 = 0.4375,
              z2 = 1,
            },
          }
        )
        .Behavior("Unplaceable")
        .Behavior("RightClickPickup")
        // The glow a hot vessel renders with, read only by EntityShapeRenderer.
        .Attribute("tempGlowMode", 1)
        .AttributeByType("shelvableByType", "*", true)
        // Four charge slots, as vanilla's. The dimensions are what a slot will accept, not a capacity.
        .AttributeByType("cookingContainerSlotsByType", "*-burned", 4)
        .AttributeByType("storageTypeByType", "*-burned", 4)
        .AttributeByType(
          "maxContentDimensionsByType",
          "*-burned",
          new
          {
            width = 0.125,
            height = 0.25,
            length = 0.125,
          }
        )
        // Declared at the process temperature for parity with vanilla's own crucible, which states 1200.
        // It changes nothing at runtime: `maxHeatableTemp` has no reader anywhere in the open source, and
        // a firepit caps on `maxTemperature` instead. The heat this pot is built for comes from the
        // crucible furnace, which is this mod's own machine and gates on its own numbers.
        .AttributeByType("maxHeatableTempByType", "*-burned", 1600)
        // Mandatory. Vanilla resolves this off the block's attributes with
        // `AssetLocation.Create(code, Code.Domain)`, which throws on a null - so a smelted pot without it
        // crashes the pour rather than failing it.
        .AttributeByType("emptiedBlockCodeByType", "*-smelted", BurnedCode)
        // The raw pot fires in a pit kiln, which is the only way to obtain one: a clayforming recipe can
        // output nothing but a `-raw` variant.
        .RootKeyByType(
          "combustibleProps",
          "*-raw",
          new
          {
            meltingPoint = 600,
            meltingDuration = 45,
            smeltedRatio = 1,
            smeltingType = "fire",
            smeltedStack = new { type = "block", code = BurnedStack(domain) },
            requiresContainer = false,
          }
        )
        // Deliberately out of reach, exactly as vanilla sets it: without this a player could bake the
        // metal out of a full pot in a firepit and skip the pour entirely.
        .RootKeyByType(
          "combustibleProps",
          "*-smelted",
          new
          {
            meltingPoint = 2400,
            meltingDuration = 45,
            smeltedRatio = 1,
            smeltingType = "bake",
            smeltedStack = new { type = "block", code = BurnedStack(domain) },
            requiresContainer = false,
          }
        )
        // No combustibleProps on the burned variant, and that absence is load-bearing: a firepit refuses
        // an input whose props require a container, so a burned pot carrying any would stop being usable
        // as a crucible at all.
        //
        // One pot per stack, where vanilla stacks four. The firing count lives on the stack, so two pots
        // of different ages must never merge - a full stack would otherwise average away every heat
        // already spent.
        .MaxStackSize(1)
        .Replaceable(300)
        .Resistance(2)
        .LightAbsorption(0)
        .Sound("walk", "game:walk/stone")
        // The smelted pot is not creative-listed, as vanilla's is not: it only ever exists as the output
        // of a heat.
        .CreativeCommon("*-raw", "*-burned")
        .SolidNonOpaque(),
    ];

  #endregion

  #region The firing count

  /// <summary>
  /// Smelts, then carries the pot's age across the swap. <c>base</c> builds a brand-new stack for the
  /// smelted pot (<c>BlockSmeltingContainer.cs:101</c>), so a count left on the pot that went in is
  /// dropped on the floor and the pot lives for ever.
  /// </summary>
  public override void DoSmelt(
    IWorldAccessor world,
    ISlotProvider cookingSlotsProvider,
    ItemSlot inputSlot,
    ItemSlot outputSlot
  ) {
    int firings = CrucibleFiring.Of(inputSlot?.Itemstack);
    base.DoSmelt(world, cookingSlotsProvider, inputSlot, outputSlot);
    if (outputSlot?.Itemstack != null)
      CrucibleFiring.Set(outputSlot.Itemstack, firings + 1);
  }

  #endregion
}

/// <summary>
/// The steel crucible once it holds a heat: the pourable half of the pot, and the class the pot's
/// <c>*-smelted</c> variant names.
/// </summary>
/// <remarks>
/// Derives from <see cref="BlockSmeltedContainer"/> rather than naming it, because
/// <c>BlockSmeltingContainer.DoSmelt</c> casts the block it resolves to that type without checking - a
/// subclass satisfies the cast, a sibling would throw on the server mid-tick.
/// </remarks>
[BlockRegister]
public partial class BlockSteelCruciblePour : BlockSmeltedContainer {
  /// <summary>
  /// Ends the pour, and retires a pot that has given its last heat. Vanilla swaps the slot for a fresh
  /// emptied pot once the metal is gone, so the age has to be carried across that swap - and when there
  /// is none left to carry, the pot goes with the metal instead of coming back new.
  /// </summary>
  /// <remarks>
  /// The count is read before <c>base</c> runs, because the stack it is read from is the one <c>base</c>
  /// replaces. The swap only happens when the last of the metal has gone, so the test afterwards is
  /// whether the slot is now holding the burned pot: a part-poured crucible is still a
  /// <see cref="BlockSteelCruciblePour"/> and must be left alone.
  /// </remarks>
  public override void OnHeldInteractStop(
    float secondsUsed,
    ItemSlot slot,
    EntityAgent byEntity,
    BlockSelection blockSel,
    EntitySelection entitySel
  ) {
    int firings = CrucibleFiring.Of(slot?.Itemstack);
    base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);

    if (slot?.Itemstack?.Block is not BlockSteelCrucible)
      return; // still part full, so nothing was swapped and nothing is owed

    if (!CrucibleFiring.IsSpent(firings)) {
      CrucibleFiring.Set(slot.Itemstack, firings);
      slot.MarkDirty();
      return;
    }

    // Spent. The metal is already out - it left before the swap - so only the pot is lost.
    slot.Itemstack = null;
    slot.MarkDirty();
    byEntity?.World.PlaySoundAt(
      new AssetLocation("game:sounds/block/ceramicbreak"),
      byEntity.Pos.X,
      byEntity.Pos.Y,
      byEntity.Pos.Z
    );
  }
}
