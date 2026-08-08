using System;
using System.Collections.Generic;
using ExpandedLib.Definitions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace ExpandedLib.Metals;

/// <summary>
/// Generates the derived resource item family (ingot / plate / bits / rod / nails) for every
/// <see cref="MetalDef"/> that opts in via <see cref="MetalDef.GenerateItemFamily"/>, so a mod-added
/// alloy gets its build-recipe forms without hand-authoring one itemtype per form. Each item is an
/// <see cref="ExItemDef"/> in the metal's owning domain (the <see cref="MetalDef.MoltenItem"/> domain)
/// under the code convention <c>{form}-{metalcode}</c> - e.g. cast iron yields
/// <c>iwex:ingot-castiron</c> / <c>metalplate-castiron</c> / … exactly the codes the cupola and the
/// solidified-cast-iron block already resolve.
/// <para>
/// The templates are lifted straight from the vanilla resource itemtypes (shapes/textures/transforms as
/// <c>game:</c> refs), parameterised by the metal's texture / density / melting point, so a generated
/// ingot is behaviour-identical to the hand-authored one it replaces. The generator is a pure function
/// of its <see cref="MetalDef"/> input - the runtime path (<see cref="ExDefinitionModSystem"/>) feeds it
/// the live <c>config/metals</c> catalogue, the golden harness feeds it the same JSON from the source tree.
/// </para>
/// <para>
/// <see cref="Emit"/> also yields a metal's tools, which come from the companion
/// <see cref="MetalToolEmitter"/> - the presets, per-type templates and tool builder live there so this
/// file stays the resource half. Tools are opt-in per metal via <see cref="MetalDef.Tools"/>; a metal
/// that declares none (a feedstock like pig iron) contributes resource forms only.
/// </para>
/// <para>
/// A generated metal is deliberately kept off the vanilla <c>block/metal</c> worldproperty: registering
/// it there would auto-create an anvil-forgeable <c>workitem-&lt;metal&gt;</c> off the seven itemtypes
/// that load from it, contradicting materials.md's "castable, brittle" cast iron. Authoring the family
/// here keeps full control and never leaks a forge path.
/// </para>
/// </summary>
public static class MetalFamilyEmitter
{
  // Fallbacks for a metal that opts in but leaves a field null. None of the shipped metals rely on
  // these (all state texture/density/melt explicitly); they keep the emitter total so a terse def that
  // only flips the flag still produces sane, iron-like items rather than crashing.
  private const int DefaultDensity = 7870;
  private const int DefaultMeltingPoint = 1482;
  private const string DefaultTexture = "game:block/metal/ingot/iron";

  // The forms a buildable metal gets when ItemForms is left null - the set the iron-substitution recipes
  // reference (ingot + the three build stocks). "bits" is opt-in, not a default, because a produced alloy
  // usually sheds shared vanilla scrap rather than its own bit.
  private static readonly string[] DefaultForms = ["ingot", "plate", "rod", "nails"];

  // form token -> (metal, owning domain) -> item def. The token is the JSON authoring key; the built
  // code/asset-path prefix (metalbit for "bits", metalnailsandstrips for "nails") lives inside each factory.
  private static readonly IReadOnlyDictionary<
    string,
    System.Func<MetalDef, string, ExItemDef>
  > Builders = new Dictionary<string, System.Func<MetalDef, string, ExItemDef>>
  {
    ["ingot"] = Ingot,
    ["plate"] = Plate,
    ["bits"] = Bits,
    ["rod"] = Rod,
    ["nails"] = Nails,
  };

  /// <summary>The form tokens the emitter can build; an <see cref="MetalDef.ItemForms"/> entry outside
  /// this set is silently skipped (a metal cannot conjure a form the emitter has no template for).</summary>
  public static IEnumerable<string> KnownForms => Builders.Keys;

  /// <summary>The tool tokens the emitter can build, from its <see cref="MetalToolEmitter"/> half; a
  /// requested type outside this set is silently skipped, exactly like an unknown resource form.</summary>
  public static IEnumerable<string> KnownTools => MetalToolEmitter.KnownTools;

  /// <summary>
  /// Every resource item def for every opted-in metal in <paramref name="metals"/>, each created in its
  /// owning domain. A metal with <see cref="MetalDef.GenerateItemFamily"/> false - every vanilla / EM
  /// metal, and every metal today that hasn't opted in - contributes nothing.
  /// </summary>
  public static IEnumerable<ExItemDef> Emit(IEnumerable<MetalDef> metals)
  {
    foreach (MetalDef metal in metals)
    {
      if (
        !metal.GenerateItemFamily
        || string.IsNullOrEmpty(metal.Code)
        || string.IsNullOrEmpty(metal.MoltenItem)
      )
        continue;

      // The owning domain is where the metal's molten item lives (iwex:ingot-castiron -> iwex); every
      // generated form is co-located there so {form}-{code} resolves in the same domain.
      string domain = new AssetLocation(metal.MoltenItem).Domain;
      foreach (
        string token in (IEnumerable<string>?)metal.ItemForms ?? DefaultForms
      )
        if (
          Builders.TryGetValue(
            token,
            out System.Func<MetalDef, string, ExItemDef>? build
          )
        )
          yield return build(metal, domain);

      // Tool family (opt-in per metal via Tools; a feedstock like pig iron leaves Tools null and makes
      // none). The presets, per-type templates and tool builder live in the companion
      // MetalToolEmitter - emitted here, and in the same owning domain, so one pass over the catalogue
      // yields a metal's whole item family.
      foreach (ExItemDef tool in MetalToolEmitter.Emit(metal, domain))
        yield return tool;
    }
  }

  #region Shared surface

  // Internal, not private: the tool half reads the same fallback so a metal that states no texture
  // paints its tools and its ingots identically.
  internal static string TextureOf(MetalDef m) => m.TexturePath ?? DefaultTexture;

  private static int DensityOf(MetalDef m) => m.Density ?? DefaultDensity;

  private static int MeltOf(MetalDef m) => m.MeltingPoint ?? DefaultMeltingPoint;

  // The metal's own ingot - what every non-ingot form smelts back into (mass-honestly recovers the alloy,
  // not vanilla iron).
  private static string IngotCodeOf(MetalDef m, string domain) =>
    domain + ":ingot-" + m.Code;

  // The density/storage/creative-tab surface every form shares, plus the code + asset path. storageFlags 5
  // and the general + owning-mod tabs mirror the vanilla resource itemtypes these are modelled on.
  private static ExItemDef Begin(MetalDef m, string domain, string codePrefix) =>
    ExItemDef
      .Create(domain, codePrefix + "-" + m.Code, m.Code + "/" + codePrefix)
      .MaterialDensity(DensityOf(m))
      .StorageFlags(5)
      .CreativeCommon("*");

  #endregion

  #region Form factories (vanilla templates, parameterised by the metal)

  private static ExItemDef Ingot(MetalDef m, string domain)
  {
    string texture = TextureOf(m);
    return Begin(m, domain, "ingot")
      .Class("ItemIngot")
      .MaxStackSize(16)
      .Shape("game:item/ingot")
      // The ingot shape's texture code is #metal (item/ingot.json), not "all".
      .Texture("metal", texture)
      .CombustibleProps(
        new
        {
          meltingPoint = MeltOf(m),
          meltingDuration = 30,
          smeltedRatio = 1,
          smeltedStack = new { type = "item", code = IngotCodeOf(m, domain) },
        }
      )
      // What a shattered ingot mold gives back - the metal's shared scrap (SolidDrop), the same bit
      // MoltenChisel recovers via SolidDropOf, so shatter and chisel agree. Convention fallback: the
      // metal's own bit in its domain.
      .Attribute(
        "shatteredStack",
        new
        {
          type = "item",
          code = m.SolidDrop ?? domain + ":metalbit-" + m.Code,
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
              placeRemoveSound = "game:sounds/block/ingot",
              stackingModel = "game:block/metal/ingotpile",
              stackingTextures = new { metal = texture },
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
          translation = new { x = 2, y = 0, z = 0 },
          rotation = new { x = 149, y = -36, z = 0 },
          origin = new { x = 0.5, y = 0.1, z = 0.5 },
          scale = 3.5,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -0.77, y = -0.15, z = -0.64 },
          rotation = new { x = 0, y = -71, z = 18 },
          origin = new { x = 0.5, y = 0.1, z = 0.5 },
          scale = 0.75,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 4.8,
        }
      );
  }

  private static ExItemDef Bits(MetalDef m, string domain) =>
    Begin(m, domain, "metalbit")
      // ItemNugget only exists to show the unit yield in the tooltip - same as vanilla metalbit.
      .Class("ItemNugget")
      .MaxStackSize(128)
      .Shape("game:item/nugget")
      // The nugget shape's texture code is #ore (item/nugget.json), not "all".
      .Texture("ore", TextureOf(m))
      // 20 bits -> 1 ingot, exactly like vanilla metalbit, so chiselled scrap re-melts mass-honestly.
      .CombustibleProps(
        new
        {
          meltingPoint = MeltOf(m),
          meltingDuration = 30,
          smeltedRatio = 20,
          smeltedStack = new { type = "item", code = IngotCodeOf(m, domain) },
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
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 176, y = 132, z = -21 },
          origin = new { x = 0.5, y = 0.07, z = 0.5 },
          scale = 5.61,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -0.8, y = -0.1, z = -0.7 },
          rotation = new { x = 5, y = 82, z = 16 },
          origin = new { x = 0.5, y = 0.1, z = 0.5 },
          scale = 0.7,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 5,
        }
      );

  private static ExItemDef Plate(MetalDef m, string domain)
  {
    string texture = TextureOf(m);
    return Begin(m, domain, "metalplate")
      .Class("ItemMetalPlate")
      .MaxStackSize(8)
      .Shape("game:item/plate")
      .Texture("metal", texture)
      // 200 units of metal in, one 200-unit plate out (-> 2 ingots) - mass-conserving.
      .CombustibleProps(
        new
        {
          meltingPoint = MeltOf(m),
          meltingDuration = 30,
          smeltedRatio = 1,
          smeltedStack = new
          {
            type = "item",
            code = IngotCodeOf(m, domain),
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
              stackingTextures = new { metal = texture },
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
          translation = new { x = 3, y = 0, z = 0 },
          rotation = new { x = -30, y = -44, z = -180 },
          origin = new { x = 0.5, y = 0.0625, z = 0.5 },
          scale = 1.85,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -0.7, y = 0.1, z = -0.53 },
          rotation = new { x = 94, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0.1, z = 0.5 },
          scale = 0.75,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 90, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 3.31,
        }
      );
  }

  private static ExItemDef Rod(MetalDef m, string domain) =>
    // Vanilla part/rod.json: no bound class (plain Item), its own rod-pile ground model.
    Begin(m, domain, "rod")
      .MaxStackSize(16)
      .Shape("game:item/rod")
      .Texture("metal", TextureOf(m))
      .CombustibleProps(
        new
        {
          meltingPoint = MeltOf(m),
          meltingDuration = 30,
          smeltedRatio = 1,
          smeltedStack = new { type = "item", code = IngotCodeOf(m, domain) },
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
              placeRemoveSound = "game:sounds/block/ingot",
              stackingModel = "game:item/rod-pile",
              upSolid = false,
              modelItemsToStackSizeRatio = 1,
              stackingCapacity = 32,
              transferQuantity = 1,
              bulkTransferQuantity = 8,
              collisionBox = new
              {
                x1 = 0,
                y1 = 0,
                z1 = 0,
                x2 = 1,
                y2 = 0.14,
                z2 = 1,
              },
              cbScaleYByLayer = 0.15,
            },
          },
        }
      )
      .GuiTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = -48, y = 134, z = 171 },
          origin = new { x = 0.53, y = 0.2, z = 0.4 },
          scale = 2.8,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -0.9, y = -0.05, z = -0.78 },
          rotation = new { x = 85, y = 0, z = 2 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 0.68,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 4,
        }
      );

  private static ExItemDef Nails(MetalDef m, string domain) =>
    // Vanilla resource/metalnailsandstrips.json: plain Item, Messy12 ground layout. The tong-held shape
    // behaviour is intentionally dropped - a cast/brittle alloy is never tong-worked.
    Begin(m, domain, "metalnailsandstrips")
      .MaxStackSize(32)
      .Shape("game:item/resource/metalnailsandstrips")
      .Texture("metal", TextureOf(m))
      // 4 nails-and-strips -> 1 ingot, like vanilla.
      .CombustibleProps(
        new
        {
          meltingPoint = MeltOf(m),
          meltingDuration = 30,
          smeltedRatio = 4,
          smeltedStack = new
          {
            type = "item",
            code = IngotCodeOf(m, domain),
            stacksize = 1,
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
            },
          },
        }
      )
      .GuiTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = -30, y = -46, z = -180 },
          origin = new { x = 0.58, y = 0.07, z = 0.55 },
          scale = 3.55,
        }
      )
      .TpHandTransform(
        new
        {
          translation = new { x = -0.7, y = -0.2, z = -0.6 },
          rotation = new { x = 94, y = 0, z = 0 },
          origin = new { x = 0.5, y = 0.1, z = 0.5 },
          scale = 0.75,
        }
      )
      .GroundTransform(
        new
        {
          translation = new { x = 0, y = 0, z = 0 },
          rotation = new { x = 0, y = 90, z = 0 },
          origin = new { x = 0.5, y = 0, z = 0.5 },
          scale = 4,
        }
      );

  #endregion

}
