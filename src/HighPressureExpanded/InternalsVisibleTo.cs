using System.Runtime.CompilerServices;

// Lets the unit-test assembly drive internal seams that are not part of the mod's public API
// surface. Tests prefer internal accessors over string-keyed reflection (see test/README.md).
[assembly: InternalsVisibleTo("HighPressureExpanded.Tests")]
