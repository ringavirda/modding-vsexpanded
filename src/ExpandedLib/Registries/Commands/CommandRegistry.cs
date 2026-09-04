using System;
using System.Reflection;
using Vintagestory.API.Common;

namespace ExpandedLib.Registries.Commands;

/// <summary>
/// Reflection-driven chat-command registration, the command-side counterpart to
/// <see cref="Entities.EntityRegistry"/>. Scans an assembly for <see cref="IExCommand"/> classes
/// carrying <see cref="CommandRegisterAttribute"/> (top-level commands) and
/// <see cref="IExSubCommand"/> classes carrying <see cref="SubCommandRegisterAttribute"/> (options
/// attaching to an existing command), and builds each one.
/// </summary>
public static class CommandRegistry {
  /// <summary>
  /// Registers every <see cref="CommandRegisterAttribute"/>-decorated <see cref="IExCommand"/> and
  /// every <see cref="SubCommandRegisterAttribute"/>-decorated <see cref="IExSubCommand"/> in
  /// <paramref name="asm"/> (default: the calling assembly) whose declared side matches
  /// <paramref name="api"/>. Safe to call from both <c>ModSystem.Start</c> and
  /// <c>StartClientSide</c>: the declared side keeps each command to a single registration.
  /// Sub-commands resolve their parent through
  /// <see cref="Vintagestory.API.Common.IChatCommandApi.GetOrCreate(string)"/>, so the parent need
  /// not already exist or belong to the same mod.
  /// </summary>
  public static void RegisterAll(ICoreAPI api, Mod mod, Assembly? asm = null) {
    asm ??= Assembly.GetCallingAssembly();
    string modId = mod.Info.ModID;

    foreach (Type type in ReflectionScan.GetCandidateTypes(asm)) {
      var attr = type.GetCustomAttribute<CommandRegisterAttribute>();
      if (attr != null) {
        if (attr.Side != EnumAppSide.Universal && attr.Side != api.Side)
          continue;

        if (
          !ReflectionScan.TryActivate<IExCommand>(
            api,
            modId,
            type,
            out var command
          )
        )
          continue;

        command.Register(api, mod);
        continue;
      }

      var subAttr = type.GetCustomAttribute<SubCommandRegisterAttribute>();
      if (subAttr != null) {
        if (subAttr.Side != EnumAppSide.Universal && subAttr.Side != api.Side)
          continue;

        if (
          !ReflectionScan.TryActivate<IExSubCommand>(
            api,
            modId,
            type,
            out var sub
          )
        )
          continue;

        IChatCommand parent = api.ChatCommands.GetOrCreate(sub.ParentName);
        sub.Register(api, mod, parent);
      }
    }
  }
}
