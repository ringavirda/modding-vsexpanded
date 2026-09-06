using ExpandedLib.Registries;

// Own mod, own domain: hellomodule ships as its own mod folder, so its classes and assets are keyed
// under its own id rather than exlib's, unlike the industry module (which ships inside exlib's mod
// folder and keeps exlib's domain).
[assembly: ExDomain("hellomodule")]

// A third-party-shaped module, hosted by exlib itself (the default Host on ExModuleAttribute) - the
// same [assembly: ExModule] one line any mod adds to become an exlib module. See HelloModule.cs for
// what that buys over a ModSystem.
[assembly: ExModule("hellomodule")]
