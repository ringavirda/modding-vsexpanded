using System.Runtime.CompilerServices;

// The unit-test assemblies drive internal mutation seams kept out of exlib's public API surface,
// notably the block migrator's ReplaceBlock/RemapInventory and their RemapEntry records.
[assembly: InternalsVisibleTo("ExpandedLib.Tests")]

// The harness wraps a handful of test-only internal seams (block-entity ticks a test needs to drive
// directly rather than through the game's own scheduler) in public hooks beside the double or rig that
// uses them, so a mod's tests reach them through ExpandedLib.Testing instead of naming exlib's own
// internals.
[assembly: InternalsVisibleTo("ExpandedLib.Testing")]
