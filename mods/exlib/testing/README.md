# ExpandedLib.Testing

Headless xUnit harness for [`exlib`](https://www.nuget.org/packages/ExpandedLib): loads the real
Vintage Story assemblies and lets you unit- and integration-test network and block-entity logic
with plain `dotnet test` - no game launch, no rendering, no world save.

Full docs on the wiki: [Testing Harness](https://github.com/ringavirda/modding-vsexpanded/wiki/Testing-Harness),
[Testing API Reference](https://github.com/ringavirda/modding-vsexpanded/wiki/Testing-API-Reference).

You supply the game assemblies (`VintagestoryAPI`, `VSSurvivalMod`, `VSEssentials`) from your own
install and register the harness's assembly resolver and `Lang` stub from a `[ModuleInitializer]`
before any test type is discovered:

```csharp
using System.Runtime.CompilerServices;
using ExpandedLib.Testing;

internal static class ModuleInit {
  [ModuleInitializer]
  internal static void Init() {
    VsAssemblyResolver.Register();
    TestLang.Init();
  }
}
```

`dotnet new install ExpandedLib.Templates.Tests` (or a local checkout's `templates/exlib-tests`)
scaffolds a project with this already wired up.
