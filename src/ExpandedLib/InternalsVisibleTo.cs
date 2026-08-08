using System.Runtime.CompilerServices;

// The unit-test assembly drives internal mutation seams kept out of exlib's public API surface,
// notably the block migrator's ReplaceBlock/RemapInventory and their RemapEntry records.
[assembly: InternalsVisibleTo("ExpandedLib.Tests")]
