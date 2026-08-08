using System.Collections.Generic;
using ExpandedLib.Definitions;

namespace IronworkingExpanded.Items;

/// <summary>
/// Code-first itemtype definitions for iwex's slag by-products (migrated from itemtypes/slag.json and
/// itemtypes/powderedslag.json). Both use the vanilla <c>Item</c> class, so a dedicated stand-alone provider
/// carries them (see <see cref="IExItemDefProvider"/>). Slag is a smeltable/grindable gravel-textured lump;
/// powdered slag is its ground form, a phosphate fertiliser.
/// </summary>
public class SlagItemDefinitions : IExItemDefProvider
{
  /// <summary>The cast slag brick's own texture - the coursed face it shows both as an item and laid up.</summary>
  public const string BrickTexture = "iwex:block/slag/slagbrick";

  /// <summary>The code molten slag carries through the canal network and the casting bed's cells (see
  /// <c>assets/iwex/config/metals/slag.json</c>). What tells a slag pour apart from a metal one.</summary>
  public const string MoltenCode = "iwex:slag";

  /// <summary>
  /// Slag units in one cast brick - <b>the same as a pig</b>, because it is cast in the same impression. The
  /// casting bed cuts one mold shape and pours whatever is in the runner into it, so brick and pig share a
  /// cavity by construction; giving them different masses would strand a remainder in every slag pour and
  /// make a bed's yield depend on which metal reached it.
  /// </summary>
  public const int SlagBrickUnits = ItemPig.PigUnits;

  public static IEnumerable<ExItemDef> Definitions(string domain) =>
    [Slag(domain), PowderedSlag(domain), SlagBrick(domain)];

  /// <summary>
  /// Slag's own texture, deliberately not vanilla's phyllite gravel: that is a *lighter and much more
  /// saturated* rock texture (mean luma 124 / chroma 13 against slag's 97 / 5), which reads as ordinary
  /// gravel. The dedicated one is darker and near-neutral, which is what air-cooled furnace
  /// slag actually looks like and, more to the point, what makes it not look like stone.
  /// </summary>
  public const string Texture = "iwex:block/slag/slag";

  // The surface both share: a 64 stack, the slag texture, and the general/iwex creative tabs.
  private static ExItemDef Common(ExItemDef def) =>
    def.MaxStackSize(64).TextureAll(Texture).CreativeCommon("*");

  private static ExItemDef Slag(string domain) =>
    Common(ExItemDef.Create(domain, "slag"))
      .Shape("game:item/ore/ungraded/coke")
      .MaterialDensity(800)
      .GrindingProps(
        new { groundStack = new { type = "item", code = "iwex:powderedslag" } }
      )
      .CombustibleProps(new { meltingPoint = 720 })
      .Attribute("shatteredStack", new { type = "item", code = "iwex:slag" })
      .GuiTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 150, y = -38, z = 0 },
          origin = new { x = 0.5, y = 0.1, z = 0.5 },
          scale = 3.8,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -0.93, y = -0.19, z = -0.77 },
          rotation = new { x = -48, y = -180, z = 23 },
          origin = new { x = 0.5, y = 0.12, z = 0.5 },
          scale = 0.6,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 4.5,
        }
      );

  /// <summary>
  /// A cast slag brick, poured in the sand bed's brick molds and laid up into <c>slagbricks</c> masonry.
  /// <para>
  /// Deliberately a near-copy of vanilla's <c>stonebrick</c> / <c>burnedbrick</c> surface, because a brick is
  /// a well-established <em>kind of thing</em> in this game and players already know what one does: it
  /// stacks on the ground in a pile, it can be thrown, it goes on a shelf, and eight of it around a mortar
  /// makes a block. Matching that idiom is worth more than any originality here - an unfamiliar brick is
  /// just a brick that does not work the way you expect.
  /// </para>
  /// </summary>
  private static ExItemDef SlagBrick(string domain) =>
    ExItemDef
      .Create(domain, "slagbrick")
      .MaxStackSize(64)
      .CreativeCommon("*")
      // Cast slag is stone, not ceramic, so it wears the stone brick's shape rather than the clay one.
      .Shape("game:item/stonebrick")
      .TextureAll(BrickTexture)
      .MaterialDensity(2400)
      .Attribute("materialUnits", SlagBrickUnits)
      // Vanilla's brick behaviours: a thrown brick hurts, and loose bricks stack into a pile on the ground
      // instead of lying as scattered items - which is what makes a bulk building material bearable to move.
      .Raw(
        "behaviors",
        new object[]
        {
          new
          {
            name = "Throwable",
            properties = new
            {
              damage = 1,
              thrownProjectileCode = "thrownitem",
              damageType = "BluntAttack",
              dropOnImpactChance = 1,
            },
          },
          new
          {
            name = "GroundStorable",
            properties = new
            {
              layout = "Stacking",
              placeRemoveSound = "sounds/block/ceramicplace",
              stackingModel = "block/clay/brickpile",
              modelItemsToStackSizeRatio = 1,
              upSolid = true,
              stackingCapacity = 24,
              transferQuantity = 1,
              bulkTransferQuantity = 4,
              collisionBox = new
              {
                x1 = 0,
                y1 = 0,
                z1 = 0,
                x2 = 1,
                y2 = 0.25,
                z2 = 1,
              },
              cbScaleYByLayer = 0.1666666666666667,
            },
          },
        }
      )
      .Attributes(
        new
        {
          displaycaseable = true,
          shelvable = true,
          displayable = new
          {
            shelf = new
            {
              size = new
              {
                width = 6,
                height = 4.5,
                length = 5,
              },
            },
          },
          onDisplayTransform = new
          {
            origin = new
            {
              x = 0.5,
              y = 0,
              z = 0.5,
            },
            scale = 0.7,
          },
        }
      )
      .GuiTransform(
        new
        {
          translation = new { x = 2, y = 0, z = 0 },
          rotation = new { x = 149, y = -36, z = 0 },
          origin = new
          {
            x = 0.5,
            y = 0.256,
            z = 0.5,
          },
          scale = 2.19,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = -17, y = 8, z = -19 },
          origin = new
          {
            x = 0.14,
            y = 0.04,
            z = 0.18,
          },
          scale = 0.46,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new
          {
            x = 0.5,
            y = 0,
            z = 0.5,
          },
          scale = 3,
        }
      );

  private static ExItemDef PowderedSlag(string domain) =>
    Common(ExItemDef.Create(domain, "powderedslag"))
      .Shape("game:item/food/flour")
      .MaterialDensity(500)
      .Attributes(
        new
        {
          dissolveInWater = true,
          fertilizerProps = new
          {
            n = 0,
            p = 20,
            k = 5,
            permaboost = new
            {
              n = 0,
              p = 5,
              k = 0,
              code = "powderedslag",
            },
          },
          fertilizerTextureCode = "potash",
        }
      )
      .GuiTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 149, y = 12, z = 0 },
          origin = new { x = 0.41, y = -0.1, z = 0.8 },
          scale = 2.54,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -1.87, y = -1.25, z = -0.8 },
          rotation = new { x = 70, y = 11, z = -65 },
          scale = 0.41,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0.45, z = 0 },
          rotation = new { x = 0.1, y = 8, z = -0.1 },
          scale = 4.5,
        }
      );
}
