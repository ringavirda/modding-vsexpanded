using System.Collections.Generic;
using System.Linq;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockMigrations;
using IronIndustryExpanded.BlockStructures.Forming;
using Vintagestory.API.Common;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// The stock rename onto the settled forms. A stock item whose code stops resolving is dropped on world
/// load without an error, so every form that declares a name it used to go by has to yield a remap - and
/// the remaps are walked off the registry rather than written out, so a third party renaming its own form
/// gets the same migration free.
/// </summary>
public class StockFormRenameMigrationTests {
  private static Dictionary<AssetLocation, AssetLocation> Remaps() =>
    new StockFormRenameMigration()
      .GetRemaps(new TestWorld().Api)
      .ToDictionary(r => r.oldCode, r => r.newCode);

  [Fact]
  public void Both_shipped_forms_carry_their_old_code_forward() {
    Dictionary<AssetLocation, AssetLocation> remaps = Remaps();

    Assert.Equal(
      new AssetLocation("iiex:stock-shingledbar"),
      remaps[new AssetLocation("iwex:stock-bloom")]
    );
    Assert.Equal(
      new AssetLocation("iiex:stock-shingledslab"),
      remaps[new AssetLocation("iwex:stock-slab")]
    );
  }

  [Fact]
  public void A_form_that_was_never_renamed_yields_nothing() {
    // Nothing is invented for a form with no history: a remap from a code that never existed would claim
    // an item some other mod may own.
    try {
      StockForm.Register(new StockForm("bronzebar", 3f, 3f, 8f, 16f, 0.8f));

      Assert.DoesNotContain(
        Remaps().Keys,
        code => code.Path == "stock-bronzebar"
      );
    } finally {
      StockForm.Unregister("bronzebar");
    }
  }

  [Fact]
  public void A_third_party_rename_is_carried_the_same_way() {
    try {
      StockForm.Register(
        new StockForm("bronzebar", 3f, 3f, 8f, 16f, 0.8f, ["bronzeblank"])
      );

      Assert.Equal(
        new AssetLocation("iiex:stock-bronzebar"),
        Remaps()[new AssetLocation("iwex:stock-bronzeblank")]
      );
    } finally {
      StockForm.Unregister("bronzebar");
    }
  }
}
