using ExpandedLib.Testing;
using Vintagestory.API.Common;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>
/// The convenience doubles a first-time consumer reaches for: a player with a real hotbar, a
/// recording logger, a mod loader, a config-file round-trip and the world-config tree - each wired
/// into <see cref="TestWorld"/> with no NSubstitute knowledge required to use it.
/// </summary>
public class DoublesTests {
  private sealed class FakeConfig {
    public int Value { get; set; }
    public string Name { get; set; } = "";
  }

  #region TestPlayer

  [Fact]
  public void Held_stack_reads_back_through_the_players_active_hotbar_slot() {
    using var world = new TestWorld();
    TestPlayer player = world.Player();
    var stack = new ItemStack(new Item { Code = new AssetLocation("game:pick-iron") });

    player.Hold(stack);

    Assert.Same(stack, player.Player.InventoryManager.ActiveHotbarSlot.Itemstack);
  }

  [Fact]
  public void Sneaking_is_reflected_in_the_entitys_own_controls() {
    using var world = new TestWorld();
    TestPlayer player = world.Player();

    player.Sneaking = true;

    Assert.True(player.Entity.Controls.Sneak);
    Assert.True(player.Sneaking);
  }

  #endregion

  #region ModConfig

  [Fact]
  public void A_config_class_round_trips_through_store_and_load() {
    using var world = new TestWorld();
    var config = new FakeConfig { Value = 42, Name = "furnace" };

    world.Api.StoreModConfig(config, "fake.json");
    FakeConfig? loaded = world.Api.LoadModConfig<FakeConfig>("fake.json");

    Assert.NotNull(loaded);
    Assert.Equal(config.Value, loaded!.Value);
    Assert.Equal(config.Name, loaded.Name);
  }

  [Fact]
  public void LoadModConfig_answers_null_for_a_file_never_stored() {
    using var world = new TestWorld();

    Assert.Null(world.Api.LoadModConfig<FakeConfig>("never-written.json"));
  }

  #endregion

  #region TestModLoader

  [Fact]
  public void IsModEnabled_and_every_alias_report_every_id_enabled() {
    using var world = new TestWorld();

    // No real mod list to consult, so any id - added or not - reports enabled; see TestModLoader's
    // own doc.
    Assert.True(world.Api.ModLoader.IsModEnabled("exlib"));
    Assert.True(world.Mods.IsModLoaded("exlib"));
    Assert.True(world.Mods.HasMod("exlib"));
    Assert.True(world.Mods.HasModId("exlib"));

    Assert.True(world.Api.ModLoader.IsModEnabled("nonexistent"));
    Assert.True(world.Mods.IsModLoaded("nonexistent"));
  }

  [Fact]
  public void A_mod_added_disabled_is_absent_from_GetMod_but_still_reports_enabled() {
    using var world = new TestWorld();

    world.Mods.Add("offmod", "1.0.0", enabled: false);

    Assert.True(world.Api.ModLoader.IsModEnabled("offmod"));
    Assert.Null(world.Api.ModLoader.GetMod("offmod"));
  }

  #endregion

  #region RecordingLogger

  [Fact]
  public void A_logged_error_is_retrievable_through_worldLog() {
    using var world = new TestWorld();

    world.Api.Logger.Error("Something went wrong at {0}", "the furnace");

    Assert.Contains(world.Log.Errors, m => m.Contains("the furnace"));
  }

  #endregion

  #region WorldConfigBag

  [Fact]
  public void World_config_is_readable_and_writable_through_the_api() {
    using var world = new TestWorld();

    world.Api.World.Config.SetString("some-key", "some-value");

    Assert.Equal("some-value", world.Api.World.Config.GetString("some-key"));
    Assert.Same(world.Config.Tree, world.Api.World.Config);
  }

  #endregion
}
