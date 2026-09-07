using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using ExpandedLib.Industry;
using ExpandedLib.Registries;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Every class string an iiex def emits resolves to a class something registers. A domain-prefixed key
/// is what <see cref="EntityRegistry.KeyFor(string, Type)"/> derives for a register-attributed type in
/// the assembly declaring that domain; a bare key names a vanilla class. Nothing catches the other case
/// at load: the game logs one line, instantiates the plain base class, and the missing behaviour
/// surfaces at first use as a cast to an interface the plain class does not implement.
/// </summary>
public class IiexCollectibleClassTests {
  private static readonly Assembly Iiex = typeof(IiexConfig).Assembly;

  /// <summary>The assemblies whose register attributes an iiex def may name.</summary>
  private static readonly Assembly[] Mods =
  [
    Iiex,
    typeof(EntityRegistry).Assembly,
    // exlib ships two assemblies from one mod folder, and the pipe blocks live in the second one.
    // A def naming exlib.BlockPipe resolves against the domain layer, not the framework.
    typeof(IndustryModule).Assembly,
  ];

  /// <summary>Where a bare, unprefixed key may resolve: vanilla's content and API assemblies.</summary>
  private static readonly Assembly[] Vanilla =
  [
    typeof(ItemWorkItem).Assembly,
    typeof(Item).Assembly,
  ];

  private static IReadOnlyList<IExDef> Defs() =>
    DefinitionGoldens.Collect("iiex", Iiex);

  #region Items

  [Fact]
  public void Every_item_class_an_iiex_def_names_is_registered() {
    var live = RegisteredKeys<ItemRegisterAttribute>();

    var orphans = Defs()
      .OfType<ExItemDef>()
      .Select(d => (d.Location, Class: ClassOf(d, "class")))
      .Where(x => x.Class != null && !Resolves(x.Class, live, typeof(Item)))
      .Select(x => $"{x.Location}: {x.Class}")
      .OrderBy(s => s, StringComparer.Ordinal)
      .ToList();

    Assert.True(
      orphans.Count == 0,
      "item defs naming a class nothing registers:\n  "
        + string.Join("\n  ", orphans)
    );
  }

  #endregion

  #region Blocks

  [Fact]
  public void Every_block_class_an_iiex_def_names_is_registered() {
    var live = RegisteredKeys<BlockRegisterAttribute>();

    var orphans = Defs()
      .OfType<ExBlockDef>()
      .Select(d => (d.Location, Class: ClassOf(d, "class")))
      .Where(x => x.Class != null && !Resolves(x.Class, live, typeof(Block)))
      .Select(x => $"{x.Location}: {x.Class}")
      .OrderBy(s => s, StringComparer.Ordinal)
      .ToList();

    Assert.True(
      orphans.Count == 0,
      "block defs naming a class nothing registers:\n  "
        + string.Join("\n  ", orphans)
    );
  }

  [Fact]
  public void Every_block_entity_class_an_iiex_def_names_is_registered() {
    var live = RegisteredKeys<BlockEntityRegisterAttribute>();

    var orphans = Defs()
      .OfType<ExBlockDef>()
      .Select(d => (d.Location, Class: ClassOf(d, "entityClass")))
      .Where(x =>
        x.Class != null && !Resolves(x.Class, live, typeof(BlockEntity))
      )
      .Select(x => $"{x.Location}: {x.Class}")
      .OrderBy(s => s, StringComparer.Ordinal)
      .ToList();

    Assert.True(
      orphans.Count == 0,
      "block defs naming an entity class nothing registers:\n  "
        + string.Join("\n  ", orphans)
    );
  }

  #endregion

  #region The guard can fail

  [Fact]
  public void A_vanilla_class_keyed_under_the_mods_domain_is_an_orphan() {
    // The shape of the mistake: naming a vanilla type through Class<T>() keys it under the caller's
    // domain, where nothing registers it. A bare vanilla name and a mod's own registered type resolve.
    var live = RegisteredKeys<ItemRegisterAttribute>();

    Assert.False(
      Resolves(
        EntityRegistry.KeyFor("iiex", typeof(ItemWorkItem)),
        live,
        typeof(Item)
      )
    );
    Assert.True(Resolves("ItemIngot", live, typeof(Item)));
    Assert.True(
      Resolves(
        EntityRegistry.KeyFor("iiex", typeof(Items.ItemPigWorkItem)),
        live,
        typeof(Item)
      )
    );
  }

  #endregion

  #region Resolution

  private static string? ClassOf(IExDef def, string key) =>
    def.ToJson() is JObject o ? o[key]?.Value<string>() : null;

  /// <summary>
  /// The keys <see cref="EntityRegistry"/> registers for every type carrying
  /// <typeparamref name="TAttr"/> in <see cref="Mods"/>, plus the short-name aliases a
  /// <c>BlockEntityXxx</c> under the default convention gains.
  /// </summary>
  private static IReadOnlySet<string> RegisteredKeys<TAttr>()
    where TAttr : RegisterAttribute {
    var keys = new HashSet<string>(StringComparer.Ordinal);
    foreach (Assembly asm in Mods)
      foreach (Type type in ReflectionScan.GetCandidateTypes(asm)) {
        if (type.GetCustomAttribute<TAttr>() is not { } attr)
          continue;
        keys.Add(EntityRegistry.KeyFor("iiex", type));

        const string prefix = "BlockEntity";
        if (
          typeof(TAttr) != typeof(BlockEntityRegisterAttribute)
          || attr.Code != null
          || !type.Name.StartsWith(prefix, StringComparison.Ordinal)
        )
          continue;
        string shortId = type.Name[prefix.Length..];
        keys.Add($"{EntityRegistry.DomainOf(type.Assembly, "iiex")}.{shortId}");
        keys.Add(shortId);
        keys.Add(shortId.ToLowerInvariant());
      }
    return keys;
  }

  /// <summary>A prefixed key must be registered; a bare key must name a vanilla type deriving from
  /// <paramref name="vanillaBase"/>.</summary>
  private static bool Resolves(
    string key,
    IReadOnlySet<string> live,
    Type vanillaBase
  ) =>
    live.Contains(key)
    || (
      !key.Contains('.')
      && Vanilla.Any(asm =>
        ReflectionScan
          .GetCandidateTypes(asm)
          .Any(t => t.Name == key && vanillaBase.IsAssignableFrom(t))
      )
    );

  #endregion
}
