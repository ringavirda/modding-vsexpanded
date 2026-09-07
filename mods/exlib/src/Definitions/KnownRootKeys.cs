using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Vintagestory.ServerMods.NoObf;

namespace ExpandedLib.Definitions;

/// <summary>
/// The top-level keys the game's object loader reads for a block or item type, taken by reflection
/// from the loader's target types so the set follows the installed game version.
/// </summary>
public static class KnownRootKeys {
  /// <summary>Every top-level key the loader reads for a <c>blocktypes/</c> asset, taken from
  /// <c>BlockType</c> and its bases <c>CollectibleType</c>/<c>RegistryObjectType</c>.</summary>
  public static IReadOnlySet<string> Block { get; } = KeysOf(typeof(BlockType));

  /// <summary>Every top-level key the loader reads for an <c>itemtypes/</c> asset, taken from
  /// <c>ItemType</c> and its bases <c>CollectibleType</c>/<c>RegistryObjectType</c>.</summary>
  public static IReadOnlySet<string> Item { get; } = KeysOf(typeof(ItemType));

  /// <summary>Whether <paramref name="key"/> is a root key the block loader reads.</summary>
  public static bool IsKnownBlockKey(string key) => Block.Contains(key);

  /// <summary>Whether <paramref name="key"/> is a root key the item loader reads.</summary>
  public static bool IsKnownItemKey(string key) => Item.Contains(key);

  // Every instance field and property of `type` and every base type, public or not - a fair few of
  // the loader's own JSON-bound members are private with a JsonProperty(name), e.g. CollisionBox and
  // the deprecated heldTpIdleAnimation - named by JsonProperty(name) where the loader's own type
  // carries one, else the member name with its first letter lower-cased - the same spelling the
  // loader's own JSON keys use. Compared case-insensitively, since Json.NET matches member names that
  // way regardless of the case actually written in the asset. Private members are not inherited by
  // reflection, so each type in the hierarchy is walked and queried with DeclaredOnly.
  private static IReadOnlySet<string> KeysOf(Type type) {
    var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    const BindingFlags flags =
      BindingFlags.Public
      | BindingFlags.NonPublic
      | BindingFlags.Instance
      | BindingFlags.DeclaredOnly;

    for (Type? level = type; level != null; level = level.BaseType) {
      foreach (FieldInfo field in level.GetFields(flags))
        keys.Add(
          KeyName(field.Name, field.GetCustomAttribute<JsonPropertyAttribute>())
        );
      foreach (PropertyInfo property in level.GetProperties(flags))
        keys.Add(
          KeyName(
            property.Name,
            property.GetCustomAttribute<JsonPropertyAttribute>()
          )
        );
    }

    return keys;
  }

  private static string KeyName(
    string memberName,
    JsonPropertyAttribute? attribute
  ) =>
    !string.IsNullOrEmpty(attribute?.PropertyName)
      ? attribute!.PropertyName!
      : char.ToLowerInvariant(memberName[0]) + memberName[1..];
}
