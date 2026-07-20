using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Items;

/// <summary>
/// Code-first itemtype definitions for cast iron - the cupola's product (see docs/design/materials.md).
/// All three bind VANILLA item classes by name, so there is no mod type to host
/// <see cref="IExBlockDefProvider"/>'s item sibling; this stand-alone provider carries them, exactly like
/// <see cref="SlagItemDefinitions"/>.
/// <para>
/// Cast iron is deliberately NOT a variant of vanilla's <c>block/metal</c> worldproperty. Adding it there
/// would auto-generate <c>workitem-castiron</c> off the seven vanilla itemtypes that load from it - i.e.
/// cast iron would become anvil-forgeable, contradicting materials.md's "castable, brittle". The absence of
/// <c>workableTemperature</c> / <c>requiresAnvilTier</c> / <c>carburizableProps</c> here is what enforces
/// that: cast iron is melted and poured, never beaten.
/// </para>
/// <para>
/// The 1200 °C melting point (vs vanilla iron's 1482) is the mechanical point of the cupola: a ~3 % carbon
/// near-eutectic melts far below wrought iron, so a modest cupola can remelt it. It lives in
/// <c>combustibleProps</c> because <c>MoltenMetal.MeltingPointOf</c> reads <c>Collectible.GetMeltingPoint</c>;
/// <c>MetalDef.MeltingPointOverride</c> is documented as an escape hatch, not the home. Paired with the
/// 0.75 liquid threshold in <c>assets/iwex/config/metals/castiron.json</c>, cast iron stays fluid down to
/// 900 °C - the headroom the cupola -> canal -> mold run needs to not freeze in transit.
/// </para>
/// </summary>
public class CastIronItemDefinitions : IExItemDefProvider
{
  // Every asset reference below is game:-qualified on purpose. An unqualified path on a mod-domain item
  // resolves into THAT mod's domain (iwex:), where none of these vanilla shapes/textures/sounds exist.
  private const string Texture = "game:block/metal/tarnished/iron";
  private const string IngotCode = "iwex:ingot-castiron";

  // Real grey cast iron. Vanilla puts iron at 7870 and steel at 7820, so this sits correctly below both.
  private const int Density = 7200;

  private const int MeltingPoint = 1200;

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Ingot(domain), MetalBit(domain), MetalPlate(domain)];

  // The surface all three share. storageFlags 5 and the general/iwex tabs mirror the vanilla itemtypes
  // these are modelled on.
  private static ExItemDef Common(ExItemDef def) =>
    def.MaterialDensity(Density).StorageFlags(5).CreativeCommon("*");

  private static ExItemDef Ingot(string domain) =>
    Common(ExItemDef.Create(domain, "ingot-castiron", "castiron/ingot"))
      .Class("ItemIngot")
      .MaxStackSize(16)
      .Shape("game:item/ingot")
      // The shape's own texture code is #metal (item/ingot.json), not "all".
      .Texture("metal", Texture)
      .CombustibleProps(
        new
        {
          meltingPoint = MeltingPoint,
          meltingDuration = 30,
          smeltedRatio = 1,
          smeltedStack = new { type = "item", code = IngotCode },
        }
      )
      // What a shattered mold gives back, and what MoltenChisel recovers through SolidDropOf.
      .Attribute(
        "shatteredStack",
        new { type = "item", code = "iwex:metalbit-castiron" }
      )
      .Raw(
        "behaviors",
        new[]
        {
          new
          {
            name = "GroundStorable",
            properties = new
            {
              layout = "Stacking",
              placeRemoveSound = "game:sounds/block/ingot",
              stackingModel = "game:block/metal/ingotpile",
              stackingTextures = new { metal = Texture },
              modelItemsToStackSizeRatio = 1,
              upSolid = true,
              stackingCapacity = 64,
              transferQuantity = 1,
              bulkTransferQuantity = 4,
              collisionBox = new
              {
                x1 = 0,
                y1 = 0,
                z1 = 0,
                x2 = 1,
                y2 = 0.125,
                z2 = 1,
              },
              cbScaleYByLayer = 0.125,
            },
          },
        }
      )
      .GuiTransform(
        new
        {
          translation = new
          {
            x = 2,
            y = 0,
            z = 0,
          },
          rotation = new
          {
            x = 149,
            y = -36,
            z = 0,
          },
          origin = new
          {
            x = 0.5,
            y = 0.1,
            z = 0.5,
          },
          scale = 3.5,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new
          {
            x = -0.77,
            y = -0.15,
            z = -0.64,
          },
          rotation = new
          {
            x = 0,
            y = -71,
            z = 18,
          },
          origin = new
          {
            x = 0.5,
            y = 0.1,
            z = 0.5,
          },
          scale = 0.75,
        }
      )
      .GroundTransform(
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
            x = 0,
            y = 0,
            z = 0,
          },
          origin = new
          {
            x = 0.5,
            y = 0,
            z = 0.5,
          },
          scale = 4.8,
        }
      );

  private static ExItemDef MetalBit(string domain) =>
    Common(ExItemDef.Create(domain, "metalbit-castiron", "castiron/metalbit"))
      // ItemNugget only exists to show the unit yield in the tooltip - same as vanilla metalbit.
      .Class("ItemNugget")
      .MaxStackSize(128)
      .Shape("game:item/nugget")
      // The shape's own texture code is #ore (item/nugget.json), not "all".
      .Texture("ore", Texture)
      // smeltedRatio 20 copies vanilla metalbit exactly: 20 bits -> 1 ingot. With the shared 5 units per
      // bit that is 100 units per ingot, matching materials.md's unit table, so chiselled cast iron
      // re-melts mass-honestly (R2).
      .CombustibleProps(
        new
        {
          meltingPoint = MeltingPoint,
          meltingDuration = 30,
          smeltedRatio = 20,
          smeltedStack = new { type = "item", code = IngotCode },
        }
      )
      .Raw(
        "behaviors",
        new[]
        {
          new
          {
            name = "GroundStorable",
            properties = new
            {
              layout = "Messy12",
              collisionBox = new
              {
                x1 = 0,
                y1 = 0,
                z1 = 0,
                x2 = 1,
                y2 = 0.125,
                z2 = 1,
              },
              bulkTransferQuantity = 4,
            },
          },
        }
      )
      .GuiTransform(
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
            x = 176,
            y = 132,
            z = -21,
          },
          origin = new
          {
            x = 0.5,
            y = 0.07,
            z = 0.5,
          },
          scale = 5.61,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new
          {
            x = -0.8,
            y = -0.1,
            z = -0.7,
          },
          rotation = new
          {
            x = 5,
            y = 82,
            z = 16,
          },
          origin = new
          {
            x = 0.5,
            y = 0.1,
            z = 0.5,
          },
          scale = 0.7,
        }
      )
      .GroundTransform(
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
            x = 0,
            y = 0,
            z = 0,
          },
          origin = new
          {
            x = 0.5,
            y = 0,
            z = 0.5,
          },
          scale = 5,
        }
      );

  private static ExItemDef MetalPlate(string domain) =>
    Common(
        ExItemDef.Create(domain, "metalplate-castiron", "castiron/metalplate")
      )
      .Class("ItemMetalPlate")
      .MaxStackSize(8)
      .Shape("game:item/plate")
      // The shape's own texture code is #metal (item/plate.json); vanilla expresses this as the singular
      // `texture` form, which is equivalent for a one-code shape.
      .Texture("metal", Texture)
      // 200 units of metal in, one 200-unit plate out - mass-conserving against materials.md's table.
      .CombustibleProps(
        new
        {
          meltingPoint = MeltingPoint,
          meltingDuration = 30,
          smeltedRatio = 1,
          smeltedStack = new
          {
            type = "item",
            code = IngotCode,
            stacksize = 2,
          },
        }
      )
      .Raw(
        "behaviors",
        new[]
        {
          new
          {
            name = "GroundStorable",
            properties = new
            {
              layout = "Stacking",
              upSolid = true,
              placeRemoveSound = "game:sounds/block/plate",
              stackingModel = "game:block/metal/platepile",
              stackingTextures = new { metal = Texture },
              collisionBox = new
              {
                x1 = 0.125,
                y1 = 0,
                z1 = 0.125,
                x2 = 0.875,
                y2 = 0.0625,
                z2 = 0.875,
              },
              cbScaleYByLayer = 1,
              modelItemsToStackSizeRatio = 1,
              stackingCapacity = 16,
              transferQuantity = 1,
              bulkTransferQuantity = 4,
            },
          },
        }
      )
      .GuiTransform(
        new
        {
          translation = new
          {
            x = 3,
            y = 0,
            z = 0,
          },
          rotation = new
          {
            x = -30,
            y = -44,
            z = -180,
          },
          origin = new
          {
            x = 0.5,
            y = 0.0625,
            z = 0.5,
          },
          scale = 1.85,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new
          {
            x = -0.7,
            y = 0.1,
            z = -0.53,
          },
          rotation = new
          {
            x = 94,
            y = 0,
            z = 0,
          },
          origin = new
          {
            x = 0.5,
            y = 0.1,
            z = 0.5,
          },
          scale = 0.75,
        }
      )
      .GroundTransform(
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
            x = 0,
            y = 90,
            z = 0,
          },
          origin = new
          {
            x = 0.5,
            y = 0,
            z = 0.5,
          },
          scale = 3.31,
        }
      );
}
