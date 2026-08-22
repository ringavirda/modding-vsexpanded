using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronIndustryExpanded.Items;

/// <summary>
/// The crushed blister-steel chunk, the lump a helve hammer sheds off a cold ingot and the crucible
/// furnace's charge. Its own provider rather than a row in <c>CastPartItemDefinitions</c>, which is scoped
/// to cast-iron parts that come out of a sand casting cell: this is neither cast nor iron.
/// </summary>
/// <remarks>
/// Only the chunk is defined here. The chain's smaller denomination is vanilla's own
/// <c>game:metalbit-blistersteel</c>, worth 5 u by vanilla's twenty-bits-to-an-ingot ratio, so the crush
/// pays out in something a player can already smelt back. See <see cref="BlisterBreaking"/>.
/// </remarks>
public class BlisterItemDefinitions : IExItemDefProvider {
  private const string BlisterSteel = "game:block/metal/ingot/blistersteel";

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [
      ExItemDef
        .Create(domain, "blisterchunk")
        // metalchunk.json binds its surfaces to the "#iron" texture code, whatever metal wears it.
        .Shape("iiex:item/metalchunk")
        .Texture("iron", BlisterSteel)
        .MaxStackSize(64)
        .MaterialDensity(7720)
        // Vanilla's own figure for the ingot this came off. No smelted stack: a chunk goes in a pot, not
        // a firepit, and the pot reads the melting point rather than a smelting recipe.
        .CombustibleProps(new { meltingPoint = 1602 })
        .Attribute("materialUnits", BlisterBreaking.ChunkUnits)
        .CreativeCommon("*"),
    ];
}
