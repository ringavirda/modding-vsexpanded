using Vintagestory.API.Common;

namespace ExpandedLib.Helpers;

/// <summary>The <c>Api.Side == EnumAppSide.X</c> check every machine writes by hand.</summary>
public static class ExSide {
  /// <summary>True on the server.</summary>
  public static bool IsServer(this ICoreAPI api) => api.Side == EnumAppSide.Server;

  /// <summary>True on the client.</summary>
  public static bool IsClient(this ICoreAPI api) => api.Side == EnumAppSide.Client;

  /// <summary>True on the server.</summary>
  public static bool IsServer(this IWorldAccessor world) =>
    world.Side == EnumAppSide.Server;

  /// <summary>True on the client.</summary>
  public static bool IsClient(this IWorldAccessor world) =>
    world.Side == EnumAppSide.Client;
}
