using System.ComponentModel;
using ExpandedLib.Migrations;
using ExpandedLib.Registries;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Registries;

/// <summary>
/// Adds <c>/exmod heal</c>: sweeps every currently loaded chunk and recreates any orphaned block
/// entity - a block still in the world whose <see cref="BlockEntity"/> was discarded, leaving an inert,
/// often unbreakable block. Repairs already-loaded chunks on demand rather than waiting for the
/// <see cref="BlockEntityHealModSystem"/> automatic on-load pass. Server-side; the <c>/exmod</c> root
/// requires <c>controlserver</c>.
/// </summary>
[SubCommandRegister(Side = EnumAppSide.Server)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class HealSubCommand : IExSubCommand {
  public string ParentName => "exmod";

  public void Register(ICoreAPI api, Mod mod, IChatCommand parent) {
    var healer = api.ModLoader.GetModSystem<BlockEntityHealModSystem>();

    parent
      .BeginSubCommand("heal")
      .WithDescription(Lang.Get("exlib:command-heal-desc"))
      .HandleWith(_ => Dispatch(healer))
      .EndSubCommand();
  }

  /// <summary>
  /// The command's logic with the framework's fluent arg parsing already stripped away: mirrors
  /// <see cref="RegistrySubCommand{T}.Dispatch"/>. Internal rather than private so a test can drive
  /// it without building a fake <see cref="Vintagestory.API.Common.TextCommandCallingArgs"/>.
  /// </summary>
  internal static TextCommandResult Dispatch(BlockEntityHealModSystem healer) =>
    TextCommandResult.Success(
      Lang.Get("exlib:command-heal-result", healer.HealLoadedChunks())
    );
}
