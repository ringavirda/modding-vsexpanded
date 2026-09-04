using ExpandedLib.Blocks.Healing;
using ExpandedLib.Registries.Commands;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ExpandedLib.Commands;

/// <summary>
/// Adds <c>/exmod heal</c>: sweeps every currently loaded chunk and recreates any orphaned block
/// entity - a block still in the world whose <see cref="BlockEntity"/> was discarded, leaving an inert,
/// often unbreakable block. Repairs already-loaded chunks on demand rather than waiting for the
/// <see cref="BlockEntityHealModSystem"/> automatic on-load pass. Server-side; the <c>/exmod</c> root
/// requires <c>controlserver</c>.
/// </summary>
[SubCommandRegister(Side = EnumAppSide.Server)]
public sealed class HealSubCommand : IExSubCommand {
  public string ParentName => "exmod";

  public void Register(ICoreAPI api, Mod mod, IChatCommand parent) {
    var healer = api.ModLoader.GetModSystem<BlockEntityHealModSystem>();

    parent
      .BeginSubCommand("heal")
      .WithDescription(Lang.Get("exlib:command-heal-desc"))
      .HandleWith(_ =>
        TextCommandResult.Success(
          Lang.Get("exlib:command-heal-result", healer.HealLoadedChunks())
        )
      )
      .EndSubCommand();
  }
}
