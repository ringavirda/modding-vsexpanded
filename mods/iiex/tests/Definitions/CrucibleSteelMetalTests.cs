using System.IO;
using System.Linq;
using ExpandedLib.Industry.Metals;
using ExpandedLib.Testing;
using Newtonsoft.Json;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Pins the shipped crucible-steel descriptor (<c>assets/iiex/config/metals/cruciblesteel.json</c>): the
/// top of the mod's metal ladder, melted whole in a sealed pot. The shipped file is read rather than a
/// fixture, because a typo in it binds to null silently and surfaces only in game.
/// </summary>
public class CrucibleSteelMetalTests {
  private static MetalDef Def(string code) {
    MetalDef? def = JsonConvert.DeserializeObject<MetalDef>(
      File.ReadAllText(
        Path.Combine(
          RepoPaths.Assets("iiex"),
          "config",
          "metals",
          $"{code}.json"
        )
      )
    );
    Assert.NotNull(def);
    return def!;
  }

  [Fact]
  public void Crucible_steel_is_a_made_steel_paying_out_in_its_own_bits() {
    MetalDef def = Def("cruciblesteel");

    Assert.Equal("cruciblesteel", def.Code);
    Assert.Equal("iiex:ingot-cruciblesteel", def.MoltenItem);
    Assert.Equal("iiex", def.CastDomain);
    Assert.True(def.IsAlloy);
    // Its own bits, not `game:metalbit-steel`. Paying out in a vanilla bit launders a made metal back
    // into the plain one - the leak pig iron and cast iron both had - and iiex has a guard against it:
    // `ShippedMetalDropTests.No_shipped_metal_pays_out_in_vanilla_bits`. The bit is in the scrap role
    // too, or what a broken casting sheds would be metal nothing accepts.
    Assert.Equal("iiex:metalbit-cruciblesteel", def.SolidDrop);
  }

  /// <summary>
  /// The highest melting point in the mod's catalogue, and by a margin: 1600 °C is what the process
  /// needs, and it is the whole reason the crucible furnace has to out-draught every other natural-draught
  /// machine in the line.
  /// </summary>
  [Fact]
  public void Crucible_steel_melts_hotter_than_anything_else_iiex_ships() {
    MetalDef def = Def("cruciblesteel");
    Assert.Equal(1600, def.MeltingPoint);

    // Slag is deliberately absent: it declares no melting point at all, and a null compares false against
    // any bound in C#, so including it would assert nothing while looking like it asserted something.
    foreach (string other in new[] { "castiron", "pigiron" }) {
      MetalDef lower = Def(other);
      Assert.NotNull(lower.MeltingPoint);
      Assert.True(
        lower.MeltingPoint < def.MeltingPoint,
        $"{other} must melt below crucible steel"
      );
    }
    Assert.Null(Def("slag").MeltingPoint);
  }

  /// <summary>
  /// The full worked family plus a tool set, which is the point of the metal: it exists to be made into
  /// things. One <c>tools</c> entry emits the whole set through <c>MetalToolEmitter</c>.
  /// </summary>
  [Fact]
  public void Crucible_steel_generates_the_worked_family_and_tools() {
    MetalDef def = Def("cruciblesteel");

    Assert.True(def.GenerateItemFamily);
    Assert.Equal(
      new[] { "ingot", "plate", "bits", "rod", "nails" },
      def.ItemForms
    );
    Assert.NotNull(def.Tools);
  }

  /// <summary>
  /// It beats the best steel the suite otherwise makes, and it beats it on durability alone.
  /// <para>
  /// The emitter's preset table tops out at <c>good</c>, which is bessemer steel's, so the difference is
  /// an explicit override riding that preset rather than a fourth preset name with one consumer. Crucible
  /// steel's real advantage was uniformity - no slag stringers, no soft spots - which reads as a tool
  /// that lasts, not one that hits harder or mines deeper, so attack and mining tier are deliberately
  /// left at the preset's.
  /// </para>
  /// </summary>
  [Fact]
  public void Crucible_steel_lasts_longer_than_bessemer_steel() {
    MetalToolSpec tools = Def("cruciblesteel").Tools!;
    MetalDef bessemer = JsonConvert.DeserializeObject<MetalDef>(
      File.ReadAllText(
        Path.Combine(
          RepoPaths.Assets("siex"),
          "config",
          "metals",
          "bessemersteel.json"
        )
      )
    )!;

    // The premise: both ride the same preset, so durability is the only axis that differs.
    Assert.Equal("good", tools.Preset);
    Assert.Equal("good", bessemer.Tools!.Preset);
    Assert.Null(bessemer.Tools.Durability);

    Assert.NotNull(tools.Durability);
    Assert.True(
      tools.Durability > 2600,
      "crucible steel must out-last the `good` preset it rides"
    );
    Assert.Null(tools.AttackPower);
    Assert.Null(tools.MiningTier);
  }

  /// <summary>
  /// Every metal iiex ships is registered under a distinct code, and crucible steel is among them. A
  /// descriptor that parses but never loads is the failure this catches - the file could sit in the
  /// directory unread and every case above would still pass.
  /// </summary>
  [Fact]
  public void The_shipped_catalogue_contains_crucible_steel() {
    string[] codes =
    [
      .. Directory
        .EnumerateFiles(
          Path.Combine(RepoPaths.Assets("iiex"), "config", "metals"),
          "*.json"
        )
        .Select(f => Def(Path.GetFileNameWithoutExtension(f)).Code),
    ];

    Assert.Contains("cruciblesteel", codes);
    Assert.Equal(codes.Length, codes.Distinct().Count());
  }
}
