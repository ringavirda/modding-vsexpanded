using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Blocks.Migrations;

/// <summary>
/// Optional companion to <see cref="IBlockCodeMigration"/>, implemented on the same class when the
/// block carries block-entity state that must survive the swap (inventory, progress). Without it the
/// swap is a plain block-id replace; with it, the old BE's serialized tree is read before the swap and
/// handed to <see cref="MigrateBlockEntity"/>.
/// </summary>
public interface IBlockEntityMigration {
  /// <summary>Called immediately after the new block is placed, for each migrated position.</summary>
  /// <param name="oldCode">The legacy block code that was found.</param>
  /// <param name="newCode">The replacement block code that was placed.</param>
  /// <param name="oldState">The old block entity's serialized tree, or <c>null</c> if the position had
  /// no block entity.</param>
  /// <param name="newBlockEntity">The just-placed replacement's block entity. Mutate it directly, e.g.
  /// <c>newBlockEntity.FromTreeAttributes(oldState, world)</c> for a verbatim copy; the caller marks it
  /// dirty afterwards.</param>
  /// <param name="world">The world accessor, for resolving stacks during deserialization.</param>
  void MigrateBlockEntity(
    AssetLocation oldCode,
    AssetLocation newCode,
    ITreeAttribute? oldState,
    BlockEntity newBlockEntity,
    IWorldAccessor world
  );
}
