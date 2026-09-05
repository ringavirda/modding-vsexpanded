using System.Runtime.CompilerServices;

// The unit-test assemblies drive internal mutation seams kept out of exlib's public API surface,
// notably the block migrator's ReplaceBlock/RemapInventory and their RemapEntry records.
[assembly: InternalsVisibleTo("ExpandedLib.Tests")]

// A mod's own tests reach exlib internals too, because the type under test derives from an exlib base:
// the design table is a BlockEntityMachineStation, whose ValidatePickRange seam a headless packet test
// has to turn off (its substitute player is one the engine will never place in range).
[assembly: InternalsVisibleTo("IronIndustryExpanded.Tests")]
