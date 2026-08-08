using System.Runtime.CompilerServices;

// Gives the unit-test assembly access to internal seams that must stay out of the mod's public API
// surface. Tests use internal accessors rather than string-keyed reflection (see test/README.md).
[assembly: InternalsVisibleTo("LowPressureExpanded.Tests")]
