using System.Runtime.CompilerServices;

// The unit-test assembly may drive internal seams that must not be part of the mod's public API
// surface. Prefer internal accessors over string-keyed reflection in new tests (see test/README.md).
[assembly: InternalsVisibleTo("HighPressureExpanded.Tests")]
