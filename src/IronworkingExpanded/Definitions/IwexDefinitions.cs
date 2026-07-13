using ExpandedLib.Definitions;
using IronworkingExpanded.BlockStructures.BlastFurnace.BlockEntities;
using IronworkingExpanded.BlockStructures.BlastFurnace.Blocks;
using Vintagestory.API.Common;

namespace IronworkingExpanded.Definitions;

/// <summary>
/// iwex's code-first block definitions - authored in C# with the exlib <see cref="ExBlockDef"/>
/// builder instead of a hand-written <c>blocktypes/</c> JSON, then injected as synthetic assets by
/// <c>ExDefinitionModSystem</c> so the vanilla loader builds them unchanged. Registered from
/// <see cref="IronworkingExpandedModSystem.Start"/>, which runs before the server's asset-load phase
/// injects them. The <c>Class&lt;T&gt;()</c>/<c>EntityClass&lt;T&gt;()</c> overloads bind to the
/// registered class key by type, so a class rename can't desync the definition.
/// </summary>
public static class IwexDefinitions
{
  private const string Domain = "iwex";

  /// <summary>Registers every iwex code-first block definition into the shared registry.</summary>
  public static void RegisterAll()
  {
    ExDefinitions.RegisterBlock(SolidifiedIron());
  }

  /// <summary>
  /// The solidified-iron block (left when a lit blast furnace is extinguished). Migrated verbatim
  /// from <c>assets/iwex/blocktypes/blastfurnace/solidifiediron.json</c> (2026-07-14).
  /// </summary>
  public static ExBlockDef SolidifiedIron() =>
    ExBlockDef
      .Create(Domain, "solidifiediron")
      .Class<BlockSolidifiedIron>()
      .EntityClass<BlockEntitySolidifiedIron>()
      .Material(EnumBlockMaterial.Metal)
      .CreativeTab("general", "*")
      .CreativeTab("iwex", "*")
      .Shape("game:block/basic/cube")
      .TextureAll("game:block/metal/sheet-plain/iron5")
      .Resistance(45f)
      .MaxStackSize(8)
      .MiningTier(5)
      .MineTool(EnumTool.Pickaxe)
      .Sound("place", "game:block/anvil")
      .Sound("break", "game:block/anvil")
      .Sound("hit", "game:block/anvil")
      .Sound("walk", "game:walk/stone");
}
