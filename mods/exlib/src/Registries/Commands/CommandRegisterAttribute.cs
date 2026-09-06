using System;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries;

/// <summary>
/// Marks an <see cref="IExCommand"/> class for automatic registration by
/// <see cref="CommandRegistry.RegisterAll"/>; the class supplies an
/// <see cref="IExCommand.Register"/> body and needs no wiring in the mod system.
/// <see cref="Side"/> gates registration, so a client-only command (a display or HUD preference) is
/// skipped on the server and vice versa.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandRegisterAttribute : Attribute {
  /// <summary>Side(s) this command registers on. <see cref="EnumAppSide.Universal"/> (default)
  /// registers on both client and server.</summary>
  public EnumAppSide Side { get; init; } = EnumAppSide.Universal;
}
