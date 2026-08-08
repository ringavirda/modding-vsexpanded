using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Definitions;
using ExpandedLib.Testing;
using IronworkingExpanded.BlockStructures.Furnaces;
using IronworkingExpanded.BlockStructures.Furnaces.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Xunit;

namespace IronworkingExpanded.Tests;

/// <summary>
/// The three laws that hold for <b>every</b> furnace in the suite, wherever it is declared - stated once
/// here so a downstream mod's furnace is covered by the same assertion iwex's own leaves are.
/// <para>
/// <b>Why this is not a test class.</b> xUnit discovers tests per assembly, so a <c>[Fact]</c> written
/// in the iwex suite never runs against smex's <c>BlockEntityBlastFurnaceHot</c> - the smex assembly is
/// not even loaded in the iwex test host. The guard that used to live inline in <c>ShaftColumnsTests</c>
/// scanned <c>typeof(BlockEntityFurnaceCore).Assembly</c> and therefore covered iwex alone, while the
/// note it was standing in for (on <c>BlockEntityFurnaceCore</c>) addressed every mod. These helpers scan
/// the whole loaded closure and are invoked from each suite that hosts a furnace leaf.
/// </para>
/// <para>
/// Both laws are also carried by <c>sealed</c> on the branch classes, which makes a violation a compile
/// error rather than a red test. That is the stronger guard; these remain because sealing cannot say
/// anything about the <em>value</em> a branch picks (see
/// <see cref="NoFireboxAsksForMoreThanItsCellsCanHold"/>), and because a new branch class is free to
/// arrive unsealed.
/// </para>
/// </summary>
public static class FurnaceBranchGuards
{
  #region The scan

  /// <summary>
  /// Every assembly reachable from what this test host has loaded. Walked transitively rather than read
  /// off <c>AppDomain.CurrentDomain.GetAssemblies()</c> alone, because references load lazily - a scan
  /// taken before anything touched a mod's types would silently cover nothing and still pass.
  /// </summary>
  private static Assembly[] LoadedClosure()
  {
    var seen = new Dictionary<string, Assembly>(StringComparer.Ordinal);
    var pending = new Queue<Assembly>(AppDomain.CurrentDomain.GetAssemblies());

    while (pending.Count > 0)
    {
      Assembly asm = pending.Dequeue();
      if (
        asm.IsDynamic || !seen.TryAdd(asm.GetName().Name ?? asm.FullName!, asm)
      )
        continue;

      foreach (AssemblyName reference in asm.GetReferencedAssemblies())
      {
        try
        {
          pending.Enqueue(Assembly.Load(reference));
        }
        catch
        {
          // A reference this host cannot resolve cannot contain a furnace this host could load either.
        }
      }
    }

    return seen.Values.ToArray();
  }

  /// <summary>Every concrete furnace in the loaded closure - iwex's own and any downstream mod's.</summary>
  public static Type[] Leaves() =>
    LoadedClosure()
      .SelectMany(asm =>
      {
        try
        {
          return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
          // A half-loadable assembly still tells us about the types that did load.
          return ex.Types.Where(t => t is not null).Select(t => t!).ToArray();
        }
      })
      .Where(t =>
        !t.IsAbstract && typeof(BlockEntityFurnaceCore).IsAssignableFrom(t)
      )
      .Distinct()
      .ToArray();

  #endregion

  #region Law 1 - the branch owns the layered-charge flag

  /// <summary>
  /// Where a furnace keeps its charge belongs to a branch class, never to a leaf: restating it on a
  /// concrete furnace is how it drifted out of step with the class it was declared on in the first place.
  /// Asserted twice over - the flag is declared on an abstract type, <b>and</b> that declaration is
  /// sealed, so no leaf anywhere can restate it.
  /// </summary>
  public static void TheBranchOwnsTheLayeredChargeFlag()
  {
    Type[] leaves = Leaves();
    Assert.NotEmpty(leaves);

    foreach (Type leaf in leaves)
    {
      PropertyInfo? flag = leaf.GetProperty(
        "ShaftHoldsLayeredCharge",
        BindingFlags.Instance | BindingFlags.NonPublic
      );

      Assert.True(
        flag?.DeclaringType is { IsAbstract: true },
        $"{leaf.Name} declares ShaftHoldsLayeredCharge itself; the branch class owns it"
      );
      Assert.True(
        flag!.GetMethod!.IsFinal,
        $"{leaf.Name} inherits ShaftHoldsLayeredCharge from {flag.DeclaringType!.Name}, "
          + "which does not seal it - so a leaf can still opt itself onto the wrong charge model"
      );
    }
  }

  #endregion

  #region Law 3 - nothing anywhere can assign a furnace's state

  /// <summary>
  /// <b>No furnace, in any mod, may expose a settable <c>State</c> - not public, not protected, not
  /// private.</b>
  /// <para>
  /// <b>This law exists because the failure it prevents is invisible.</b> On the shaft branch the label is
  /// recomputed from the charge every tick (<c>BlockEntityFurnaceCore.DerivesState</c>), so a fixture that
  /// arranges a furnace by writing <c>State = Melting</c> is stating a premise the very next tick discards.
  /// Nothing throws, nothing is red, and the case goes green while testing a machine that was never in the
  /// state its own name claims. 27 such call sites once existed across three suites, and every one of
  /// them would have kept passing if the setter had been left in place "so the fixtures compile".
  /// </para>
  /// <para>
  /// <b>A private setter is not good enough, which is the whole point of stating this reflectively.</b>
  /// <c>ReflectionHelpers.SetProperty</c> reaches non-public setters, so <c>private set</c> stops production
  /// code and stops <em>nothing</em> in a fixture. With no setter at all the same call throws, and the
  /// mistake is loud at the first run instead of silent for ever.
  /// </para>
  /// <para>
  /// It also pins the declaration to <c>BlockEntityFurnaceCore</c>: a leaf that shadowed <c>State</c> with
  /// a <c>new</c> settable property of its own would satisfy a naive "no setter" scan through the base while
  /// every call against the derived type wrote the shadow.
  /// </para>
  /// </summary>
  public static void NoFurnaceExposesASettableState()
  {
    Type[] leaves = Leaves();
    Assert.NotEmpty(leaves);

    foreach (Type leaf in leaves)
    {
      PropertyInfo state = leaf.GetProperty(
        nameof(BlockEntityFurnaceCore.State),
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
      )!;

      Assert.True(
        state.DeclaringType == typeof(BlockEntityFurnaceCore),
        $"{leaf.Name} declares its own State (on {state.DeclaringType?.Name}); the label belongs to "
          + "BlockEntityFurnaceCore, and a shadowing property is a second answer to what the furnace is doing"
      );
      Assert.True(
        state.GetSetMethod(nonPublic: true) == null,
        $"{leaf.Name}.State has a setter. A derived branch recomputes the label every tick, so anything "
          + "that assigns it is writing a value the next tick throws away - see the remark on the property"
      );
    }
  }

  #endregion

  #region Law 2 - no firebox asks for more fuel than it can hold

  /// <summary>
  /// The assertion whose absence let a shaft's ignition threshold ship on a hearth twice. A firebox holds
  /// <c>layers x unitsPerLayer</c> units per cell and nothing bypasses that, so a threshold above
  /// <c>cells x capacity</c> is a furnace that cannot light at any temperature - and nothing else in the
  /// suite can see it.
  /// <para>
  /// <b>The ceiling is the firebox's own arithmetic.</b> It used to be
  /// <c>BlockEntityCoalPile.MaxStackSize</c> read off vanilla at runtime - a hard 16 nobody chose, because
  /// the pile interaction was the only route fuel into a firebox. That number was the whole of B8's first
  /// cause (160 against a 16-unit cell meant neither hearth could ever light), and 12 fitting under it was
  /// luck rather than design. With <c>iwex:furnace-firebox</c> owning its capacity, 16 is simply the wrong
  /// number and a guard measuring against it measures nothing.
  /// </para>
  /// <para>
  /// The guard is <b>kept</b>, not retired with the pile. It is the assertion whose absence let an
  /// unreachable threshold ship twice; re-expressing it is the point, deleting it would repeat history.
  /// </para>
  /// <para>
  /// <b>The subject of this law is a (leaf, drawing) pair, not a leaf.</b>
  /// <c>FireboxCellCount</c> used to read two hand-declared corners on the class, so a bare
  /// <c>Activator.CreateInstance</c> answered it. It now reads the bounding box of the cells the block's
  /// own layout marks <c>Firebox</c>, so a block entity holding no block counts <b>0</b> cells and lights
  /// on nothing - which this guard correctly flags. Each leaf is therefore stood on every blocktype that
  /// names it, and a leaf no blocktype names fails rather than passing silently.
  /// </para>
  /// </summary>
  public static void NoFireboxAsksForMoreThanItsCellsCanHold()
  {
    int perCell = BEBehaviorFirebox.CellCapacity;

    Type[] fireboxes = Leaves()
      .Where(t => typeof(BlockEntityFireboxFurnace).IsAssignableFrom(t))
      .ToArray();
    Assert.NotEmpty(fireboxes);

    foreach (Type leaf in fireboxes)
    {
      List<ExBlockDef> layouts = LayoutsNaming(leaf);
      Assert.True(
        layouts.Count > 0,
        $"{leaf.Name} is a firebox furnace that no code-first blocktype in {leaf.Assembly.GetName().Name} "
          + "names as its entityClass, so there is no drawing to read its firebox out of"
      );

      foreach (ExBlockDef def in layouts)
      {
        var be = (BlockEntity)Activator.CreateInstance(leaf)!;
        FurnaceLayoutRig.OrientWithLayout(
          be,
          def,
          $"{def.Code}-north",
          "north"
        );

        int cells = (int)ReflectionHelpers.GetProperty(be, "FireboxCellCount")!;
        int threshold = (int)
          ReflectionHelpers.GetProperty(be, "ChargeCapacityUnits")!;

        Assert.True(
          threshold > 0,
          $"{leaf.Name} on {def.Code} lights on an empty firebox (ChargeCapacityUnits = {threshold})"
        );
        Assert.True(
          threshold <= cells * perCell,
          $"{leaf.Name} on {def.Code} wants {threshold} u to light but its {cells}-cell firebox holds at "
            + $"most {cells * perCell} u ({perCell} per cell = "
            + $"{BEBehaviorFirebox.LayersPerCell} layers x {BEBehaviorFirebox.UnitsPerLayer} u) "
            + "- it can never fire"
        );
      }
    }
  }

  /// <summary>
  /// Every code-first blocktype in <paramref name="leaf"/>'s own assembly that names it as its
  /// <c>entityClass</c> - the drawing (or drawings) a furnace of this class is actually built from.
  /// <para>
  /// Built in a throwaway domain and matched on the <b>type-name suffix</b>. <c>entityClass</c> is
  /// <c>"{domain}.{TypeName}"</c> (<c>EntityRegistry.KeyFor</c>), and the domain a provider is registered
  /// under is not recoverable from the type - but nothing this guard reads depends on it: legend codes are
  /// literals in the drawing, so the roles and offsets are identical whatever domain the def is built in.
  /// Matching the suffix is what keeps the scan domain-agnostic and so still covers a downstream mod.
  /// </para>
  /// </summary>
  private static List<ExBlockDef> LayoutsNaming(Type leaf)
  {
    const string probeDomain = "branchguardprobe";
    string suffix = "." + leaf.Name;
    var found = new List<ExBlockDef>();

    Type[] types;
    try
    {
      types = leaf.Assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException ex)
    {
      types = [.. ex.Types.Where(t => t is not null).Select(t => t!)];
    }

    foreach (Type type in types)
    {
      IEnumerable<ExBlockDef> defs;
      try
      {
        defs = ExDefinitions.DefinitionsOf(type, probeDomain);
      }
      catch
      {
        // A provider this host cannot build cannot be the drawing of a furnace it can load either.
        continue;
      }

      foreach (ExBlockDef def in defs)
        if (
          def.ToJson()["entityClass"]?.ToString() is string cls
          && cls.EndsWith(suffix, StringComparison.Ordinal)
        )
          found.Add(def);
    }

    return found;
  }

  #endregion
}
