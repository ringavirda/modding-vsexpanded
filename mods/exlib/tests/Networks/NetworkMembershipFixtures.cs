using System;
using System.Linq;
using ExpandedLib.Networks;
using ExpandedLib.Testing;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ExpandedLib.Tests;

/// <summary>Shared world setup and declaration helpers for the network-membership suite.</summary>
internal static class NetworkMembershipFixtures {
  public static TestWorld NewGraphWorld() {
    var w = new TestWorld();
    w.RegisterNetwork("test", sys => new StubNetwork(sys));
    w.RegisterNetwork("molten", sys => new StubNetwork(sys, "molten"));
    return w;
  }

  /// <summary>Puts a JSON <c>networkType</c> declaration on <paramref name="be"/>'s one membership,
  /// as <c>CreateBehaviors</c> does for a behaviour a block names in its <c>entityBehaviors</c>.</summary>
  public static void Declare(BlockEntity be, string networkType) =>
    Declare(
      NetworkMembership.MembersOf(be).Single(),
      $"{{\"networkType\":\"{networkType}\"}}"
    );

  /// <summary>Puts the JSON body <paramref name="json"/> on <paramref name="member"/> as its
  /// declaration.</summary>
  public static void Declare(BEBehaviorNetworkMember member, string json) =>
    member.properties = new JsonObject(JToken.Parse(json));

  /// <summary>
  /// Asserts the logger recorded an error whose formatted text carries <paramref name="fragment"/>
  /// and mentions every one of <paramref name="mustMention"/>. <see cref="RecordingLogger"/> already
  /// merges format and args, so both checks read off the same rendered string.
  /// </summary>
  public static void AssertErrorLogged(
    TestWorld w,
    string fragment,
    params object[] mustMention
  ) =>
    Assert.Contains(
      w.Log.Errors,
      message =>
        message.Contains(fragment, StringComparison.Ordinal)
        && mustMention.All(m => message.Contains(m.ToString()!, StringComparison.Ordinal))
    );
}
