using System.Runtime.CompilerServices;
using ExpandedLib.Testing;

namespace LowPressureExpanded.Tests;

internal static class ModuleInit
{
  [ModuleInitializer]
  internal static void Init()
  {
    VsAssemblyResolver.Register();
    TestLang.Init();
  }
}
