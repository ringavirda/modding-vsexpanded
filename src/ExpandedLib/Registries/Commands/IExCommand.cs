using Vintagestory.API.Common;

namespace ExpandedLib.Registries.Commands;

/// <summary>
/// A self-contained chat command. Each command lives in its own class, carries a
/// <see cref="CommandRegisterAttribute"/> and builds itself in <see cref="Register"/>, typically via
/// <c>api.ChatCommands.Create(...)</c>. The registry instantiates the class through its
/// parameterless constructor, so it must hold no constructor state: whatever it needs is captured
/// from the arguments of <see cref="Register"/>.
/// </summary>
public interface IExCommand {
  /// <summary>
  /// Builds and registers this command. Called once per applicable side by
  /// <see cref="CommandRegistry.RegisterAll"/>. For client-only commands
  /// (<see cref="EnumAppSide.Client"/>) <paramref name="api"/> is an
  /// <see cref="Vintagestory.API.Client.ICoreClientAPI"/> and can be cast to it.
  /// </summary>
  void Register(ICoreAPI api, Mod mod);
}
