# exlib headless test project

A `dotnet new` template for a headless xUnit test project against `ExpandedLib.Testing` - see the
wiki's [Testing Harness](https://github.com/ringavirda/modding-vsexpanded/wiki/Testing-Harness).

```
dotnet new install ./templates/exlib-tests
dotnet new exlib-tests -n Demo.Tests -o Demo.Tests --ModName Demo
dotnet test Demo.Tests
```

`--ModName` is your mod project's own name (a sibling folder, `../Demo/Demo.csproj` relative to the
generated project); `--GamePath` overrides the Vintage Story install path baked in (defaults to the
`VINTAGE_STORY` environment variable at generation time). Both content checks
(`Definitions/GoldenTests.cs`, `Invariants/ShippedAssetJsonTests.cs`) pass vacuously until your mod
has code-first definitions or an `assets/` tree to check.

Uninstall with `dotnet new uninstall ./templates/exlib-tests` (or by identity,
`ExpandedLib.Templates.Tests`) when you're done trying it.
