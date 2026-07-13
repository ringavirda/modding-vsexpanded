using System.Runtime.CompilerServices;

// The unit-test assembly drives internal mutation seams that must not be part of exlib's public API
// surface - notably the block migrator's ReplaceBlock/RemapInventory and their RemapEntry records,
// which carry the highest save-data blast radius and so warrant direct, in-process coverage.
[assembly: InternalsVisibleTo("ExpandedLib.Tests")]
