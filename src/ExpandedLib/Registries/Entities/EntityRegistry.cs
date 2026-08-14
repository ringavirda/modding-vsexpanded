using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries.Entities;

/// <summary>
/// Reflection-driven class registration for mods built on ExpandedLib. Scans an assembly for types
/// carrying a <see cref="RegisterAttribute"/> (the kind-specific <c>[BlockRegister]</c>,
/// <c>[ItemRegister]</c>, <c>[BlockEntityRegister]</c>, <c>[BlockBehaviorRegister]</c>,
/// <c>[BlockEntityBehaviorRegister]</c>, <c>[CollectibleBehaviorRegister]</c>) and registers each
/// with the game under the matching registry, keyed <c>{modid}.{ClassName}</c> by convention.
/// </summary>
public static class EntityRegistry {
  /// <summary>
  /// Registers every <see cref="RegisterAttribute"/>-decorated class in <paramref name="asm"/>
  /// (default: the calling mod's own assembly). Call once from <c>ModSystem.Start</c>.
  /// </summary>
  public static void RegisterAll(ICoreAPI api, Mod mod, Assembly? asm = null) {
    asm ??= Assembly.GetCallingAssembly();
    string modId = mod.Info.ModID;

    // Recorded before the scan so KeyFor can answer "which domain owns this type" for an assembly that
    // declares no [assembly: ExDomain]. The attribute is preferred because it needs no prior call;
    // this map only helps once the owning mod's Start has run, which is a load-order dependency the
    // attribute exists to avoid.
    _domainByAssembly[asm] = modId;

    foreach (Type type in ReflectionScan.GetCandidateTypes(asm)) {
      var attr = type.GetCustomAttributes()
        .OfType<RegisterAttribute>()
        .FirstOrDefault();
      if (attr == null)
        continue;

      string key = KeyFor(modId, type, attr);

      switch (attr) {
        case BlockRegisterAttribute
          when Validate<Block>(api, modId, type, "block"):
          api.RegisterBlockClass(key, type);
          break;
        case ItemRegisterAttribute
          when Validate<Item>(api, modId, type, "item"):
          api.RegisterItemClass(key, type);
          break;
        case BlockEntityRegisterAttribute
          when Validate<BlockEntity>(api, modId, type, "block entity"):
          RegisterBlockEntity(api, modId, key, attr, type);
          break;
        case BlockBehaviorRegisterAttribute
          when Validate<BlockBehavior>(api, modId, type, "block behavior"):
          api.RegisterBlockBehaviorClass(key, type);
          break;
        case BlockEntityBehaviorRegisterAttribute
          when Validate<BlockEntityBehavior>(
            api,
            modId,
            type,
            "block entity behavior"
          ):
          api.RegisterBlockEntityBehaviorClass(key, type);
          break;
        case CollectibleBehaviorRegisterAttribute
          when Validate<CollectibleBehavior>(
            api,
            modId,
            type,
            "collectible behavior"
          ):
          api.RegisterCollectibleBehaviorClass(key, type);
          break;
      }
    }

    // Code-first definitions live next to the classes they describe: a type implementing
    // IExBlockDefProvider / IExItemDefProvider / IExRecipeDefProvider is picked up from the same
    // assembly scan, so no central list registers them.
    ExDefinitions.DiscoverAndRegister(modId, asm);
    ExDefinitions.DiscoverAndRegisterItems(modId, asm);
    ExDefinitions.DiscoverAndRegisterRecipes(modId, asm);
  }

  // Assembly -> the domain its registrable types are keyed under, recorded by RegisterAll. Only a
  // fallback: [assembly: ExDomain] answers the same question with no ordering dependency.
  private static readonly Dictionary<Assembly, string> _domainByAssembly = [];

  /// <summary>
  /// The domain <paramref name="asm"/>'s registrable types are keyed under: its
  /// <see cref="ExDomainAttribute"/> if it declares one, else the modid it was registered with, else
  /// <paramref name="fallback"/>.
  /// </summary>
  public static string DomainOf(Assembly asm, string fallback) =>
    asm.GetCustomAttribute<ExDomainAttribute>()?.Domain
    ?? (
      _domainByAssembly.TryGetValue(asm, out string? recorded)
        ? recorded
        : fallback
    );

  /// <summary>
  /// The registry key a <see cref="RegisterAttribute"/>-decorated <paramref name="type"/> is
  /// registered under: <c>{domain}.{Code ?? ClassName}</c>, or the bare key when
  /// <see cref="RegisterAttribute.PrefixModId"/> is false. Falls back to the convention default
  /// <c>{domain}.{ClassName}</c> when <paramref name="type"/> carries no register attribute. The
  /// code-first definition builder (<c>ExBlockDef</c>'s type-safe <c>Class&lt;T&gt;()</c>) resolves
  /// class strings through here as well, so the two cannot disagree after a rename.
  /// <para>
  /// The domain comes from <paramref name="type"/>'s own assembly (<see cref="DomainOf"/>), not from
  /// <paramref name="callerDomain"/>, so naming a class from a dependency yields the key that mod
  /// registered. Keying off the caller's domain instead produced a key nobody had registered, which
  /// fails at world load and, on the block half, without a log line.
  /// <paramref name="callerDomain"/> is the last resort, for a type whose assembly neither declares a
  /// domain nor has registered one.
  /// </para>
  /// </summary>
  public static string KeyFor(string callerDomain, Type type) =>
    KeyFor(
      DomainOf(type.Assembly, callerDomain),
      type,
      type.GetCustomAttributes().OfType<RegisterAttribute>().FirstOrDefault()
    );

  private static string KeyFor(string modId, Type type, RegisterAttribute? attr) {
    string baseKey = attr?.Code ?? type.Name;
    return (attr?.PrefixModId ?? true) ? $"{modId}.{baseKey}" : baseKey;
  }

  /// <summary>Logs a warning and returns false when <paramref name="type"/> does not derive from the
  /// base type its register attribute implies (a mis-applied attribute), so it is skipped rather than
  /// throwing inside the game's registry.</summary>
  private static bool Validate<TBase>(
    ICoreAPI api,
    string modId,
    Type type,
    string kind
  ) {
    if (typeof(TBase).IsAssignableFrom(type))
      return true;

    api.Logger.Warning(
      "[{0}] EntityRegistry: {1} is marked as a {2} but does not derive from {3}; skipped.",
      modId,
      type.FullName,
      kind,
      typeof(TBase).Name
    );
    return false;
  }

  /// <summary>
  /// Registers a block entity under its primary key, plus the short-name aliases
  /// (<c>{modid}.{ShortId}</c>, <c>{ShortId}</c>, <c>{shortid}</c>) for classes named
  /// <c>BlockEntityXxx</c> using the default convention. Aliases are skipped when an explicit
  /// <see cref="RegisterAttribute.Code"/> is given (e.g. a vanilla override).
  /// </summary>
  private static void RegisterBlockEntity(
    ICoreAPI api,
    string modId,
    string key,
    RegisterAttribute attr,
    Type type
  ) {
    api.RegisterBlockEntityClass(key, type);

    const string prefix = "BlockEntity";
    if (attr.Code != null || !type.Name.StartsWith(prefix))
      return;

    string shortId = type.Name[prefix.Length..];
    api.RegisterBlockEntityClass($"{modId}.{shortId}", type);
    api.RegisterBlockEntityClass(shortId, type);
    api.RegisterBlockEntityClass(shortId.ToLowerInvariant(), type);
  }
}
