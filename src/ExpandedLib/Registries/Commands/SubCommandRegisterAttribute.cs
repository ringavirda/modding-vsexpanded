using System;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries.Commands;

/// <summary>
/// Marks an <see cref="IExSubCommand"/> class for automatic registration by
/// <see cref="CommandRegistry.RegisterAll"/>, the sub-command counterpart to
/// <see cref="CommandRegisterAttribute"/>: the registry resolves the
/// <see cref="IExSubCommand.ParentName"/> command and lets the class attach itself.
/// <see cref="Side"/> gates registration, so a client-only option (a display or HUD preference) is
/// skipped on the server and vice versa.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SubCommandRegisterAttribute : Attribute {
  /// <summary>Side(s) this sub-command registers on. <see cref="EnumAppSide.Universal"/> (default)
  /// registers on both client and server.</summary>
  public EnumAppSide Side { get; init; } = EnumAppSide.Universal;
}
