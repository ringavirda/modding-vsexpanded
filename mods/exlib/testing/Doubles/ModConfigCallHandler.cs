using System.Reflection;
using Newtonsoft.Json.Linq;
using NSubstitute.Core;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ExpandedLib.Testing;

/// <summary>
/// Routes <c>ICoreAPI.LoadModConfig&lt;T&gt;</c>/<c>LoadModConfig(string)</c> and
/// <c>StoreModConfig&lt;T&gt;</c>/<c>StoreModConfig(JsonObject, string)</c> to <see cref="ModConfigFiles"/>
/// by method name off the raw <see cref="ICall"/>, because NSubstitute's ordinary
/// <c>Arg.Any&lt;T&gt;()</c>/<c>Returns</c> pair binds a return-value specification to the one closed
/// generic method it was written against - it cannot answer a call made with a different <c>T</c>.
/// Every other call falls through with <see cref="RouteAction.Continue"/>, so registering this
/// handler on <c>Api</c> leaves every other configured member (<c>Logger</c>, <c>ModLoader</c>, ...)
/// working exactly as before.
/// </summary>
internal sealed class ModConfigCallHandler(ModConfigFiles files) : ICallHandler {
  public RouteAction Handle(ICall call) {
    MethodInfo method = call.GetMethodInfo();
    object?[] args = call.GetArguments();

    switch (method.Name) {
      case nameof(ICoreAPI.StoreModConfig):
        // StoreModConfig<T>(T, filename) and StoreModConfig(JsonObject, filename) both land here;
        // either way the value is args[0] and the filename args[1]. JsonObject wraps a JToken with
        // no public serialisable surface of its own, so it is unwrapped before writing.
        files.Write(
          (string)args[1]!,
          args[0] is JsonObject wrapped ? wrapped.Token : args[0]!
        );
        return RouteAction.Return(null);

      case nameof(ICoreAPI.LoadModConfig): {
        string file = (string)args[0]!;
        // The generic overload's T; the JsonObject overload isn't generic, so it reads back as one.
        if (!method.IsGenericMethod || method.GetGenericArguments()[0] == typeof(JsonObject)) {
          object? raw = files.ReadUntyped(file, typeof(JToken));
          return RouteAction.Return(raw == null ? null : new JsonObject((JToken)raw));
        }
        return RouteAction.Return(
          files.ReadUntyped(file, method.GetGenericArguments()[0])
        );
      }

      default:
        return RouteAction.Continue();
    }
  }
}
