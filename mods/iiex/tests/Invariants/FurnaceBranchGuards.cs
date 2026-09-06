using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpandedLib.Structures;
using ExpandedLib.Definitions;
using ExpandedLib.Industry.Heat;
using ExpandedLib.Testing;
using IronIndustryExpanded.BlockStructures.Furnaces;
using IronIndustryExpanded.BlockStructures.Furnaces.BlockEntities;
using IronIndustryExpanded.Items;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace IronIndustryExpanded.Tests;

/// <summary>
/// Laws that hold for every furnace in the suite, wherever it is declared. Not a test class: xUnit
/// discovers tests per assembly, so a <c>[Fact]</c> in the iiex suite never runs against a downstream
/// mod's furnace. These helpers scan the whole loaded assembly closure and are called from each suite
/// that hosts a furnace leaf.
/// <para>
/// <c>sealed</c> on the branch classes carries the same laws as compile errors, which is stronger.
/// These remain because sealing says nothing about the value a branch picks (see
/// <see cref="NoFireboxAsksForMoreThanItsCellsCanHold"/>) and a new branch class may arrive unsealed.
/// </para>
/// </summary>
public static class FurnaceBranchGuards {
  #region The scan

  /// <summary>
  /// Every assembly reachable from what this test host has loaded. Walked transitively rather than
  /// read off <c>AppDomain.CurrentDomain.GetAssemblies()</c> alone, because references load lazily and
  /// a scan taken before anything touched a mod's types would cover nothing and still pass.
  /// </summary>
  private static Assembly[] LoadedClosure() {
    var seen = new Dictionary<string, Assembly>(StringComparer.Ordinal);
    var pending = new Queue<Assembly>(AppDomain.CurrentDomain.GetAssemblies());

    while (pending.Count > 0) {
      Assembly asm = pending.Dequeue();
      if (
        asm.IsDynamic || !seen.TryAdd(asm.GetName().Name ?? asm.FullName!, asm)
      )
        continue;

      foreach (AssemblyName reference in asm.GetReferencedAssemblies()) {
        try {
          pending.Enqueue(Assembly.Load(reference));
        } catch {
          // A reference this host cannot resolve cannot contain a furnace this host could load either.
        }
      }
    }

    return seen.Values.ToArray();
  }

  /// <summary>Every concrete furnace in the loaded closure - iiex's own and any downstream mod's.</summary>
  public static Type[] Leaves() =>
    LoadedClosure()
      .SelectMany(asm => {
        try {
          return asm.GetTypes();
        } catch (ReflectionTypeLoadException ex) {
          // A half-loadable assembly still reports the types that did load.
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
  /// Where a furnace keeps its charge belongs to a branch class, not to a leaf. Asserted twice: the
  /// flag is declared on an abstract type, and that declaration is sealed, so no leaf can restate it.
  /// </summary>
  public static void TheBranchOwnsTheLayeredChargeFlag() {
    Type[] leaves = Leaves();
    Assert.NotEmpty(leaves);

    foreach (Type leaf in leaves) {
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
  /// No furnace in any mod may expose a settable <c>State</c>, at any accessibility. The shaft branch
  /// recomputes the label from the charge every tick (<c>BlockEntityFurnaceCore.DerivesState</c>), so an
  /// assignment is discarded on the next tick without an error. A private setter is not enough:
  /// <c>ReflectionHelpers.SetProperty</c> reaches non-public setters, while with no setter at all the
  /// same call throws. The declaration is pinned to <c>BlockEntityFurnaceCore</c> as well, since a leaf
  /// shadowing <c>State</c> with a <c>new</c> settable property would satisfy a scan through the base.
  /// </summary>
  public static void NoFurnaceExposesASettableState() {
    Type[] leaves = Leaves();
    Assert.NotEmpty(leaves);

    foreach (Type leaf in leaves) {
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
  /// A firebox holds <c>layers x unitsPerLayer</c> units per cell and nothing bypasses that ceiling, so
  /// an ignition threshold above <c>cells x capacity</c> is a furnace that cannot light at any
  /// temperature. The capacity belongs to <c>iiex:furnace-firebox</c>, not to vanilla's coal pile.
  /// <para>
  /// The subject is a (leaf, drawing) pair rather than a leaf. <c>FireboxCellCount</c> reads the
  /// bounding box of the cells the block's own layout marks <c>Firebox</c>, so a block entity holding
  /// no block counts 0 cells. Each leaf is therefore stood on every blocktype that names it, and a leaf
  /// no blocktype names fails rather than passing silently.
  /// </para>
  /// </summary>
  public static void NoFireboxAsksForMoreThanItsCellsCanHold() {
    int perCell = BEBehaviorFirebox.DefaultCellCapacity;

    Type[] fireboxes = Leaves()
      .Where(t => typeof(BlockEntityFireboxFurnace).IsAssignableFrom(t))
      .ToArray();
    Assert.NotEmpty(fireboxes);

    foreach (Type leaf in fireboxes) {
      List<ExBlockDef> layouts = LayoutsNaming(leaf);
      Assert.True(
        layouts.Count > 0,
        $"{leaf.Name} is a firebox furnace that no code-first blocktype in {leaf.Assembly.GetName().Name} "
          + "names as its entityClass, so there is no drawing to read its firebox out of"
      );

      foreach (ExBlockDef def in layouts) {
        var be = (BlockEntityMultiblockStructure)Activator.CreateInstance(leaf)!;
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
            + $"{BEBehaviorFirebox.DefaultLayersPerCell} layers x {BEBehaviorFirebox.DefaultUnitsPerLayer} u) "
            + "- it can never fire"
        );
      }
    }
  }

  #endregion

  #region Law 4 - no firebox counts a disruption it can never clear

  /// <summary>
  /// A lit furnace below <c>DisruptionMixFloor</c> counts a disruption on every tick and goes out when the
  /// extinguish grace expires. A firebox holds <c>cells x capacity</c> units and nothing bypasses that
  /// ceiling, so a floor above its own capacity is a hearth that is at once full enough to light and too
  /// empty to stay lit: it fires and snuffs itself whatever the player does.
  /// <para>
  /// The floor is the shaft's number and the shaft branch no longer reads it - <c>DerivesState</c> makes
  /// burning per column there, so the disruption block runs on the firebox branch alone. Inheriting it
  /// unchanged is what put every reverberatory hearth out of reach.
  /// </para>
  /// </summary>
  public static void NoFireboxCarriesAFloorAboveItsOwnCapacity() {
    Type[] fireboxes = Leaves()
      .Where(t => typeof(BlockEntityFireboxFurnace).IsAssignableFrom(t))
      .ToArray();
    Assert.NotEmpty(fireboxes);

    foreach (Type leaf in fireboxes) {
      List<ExBlockDef> layouts = LayoutsNaming(leaf);
      Assert.True(
        layouts.Count > 0,
        $"{leaf.Name} is a firebox furnace that no code-first blocktype in {leaf.Assembly.GetName().Name} "
          + "names as its entityClass, so there is no drawing to read its firebox out of"
      );

      foreach (ExBlockDef def in layouts) {
        var be = (BlockEntityMultiblockStructure)Activator.CreateInstance(leaf)!;
        FurnaceLayoutRig.OrientWithLayout(
          be,
          def,
          $"{def.Code}-north",
          "north"
        );

        int capacity = (int)
          ReflectionHelpers.GetProperty(be, "ChargeCapacityUnits")!;
        int floor = (int)
          ReflectionHelpers.GetProperty(be, "DisruptionMixFloor")!;

        Assert.True(
          floor <= capacity,
          $"{leaf.Name} on {def.Code} counts a disruption below {floor} u but its firebox holds at most "
            + $"{capacity} u - a full hearth is under the floor on the tick it lights, so it goes out on "
            + "the extinguish grace and the fuel clock is never reached"
        );
      }
    }
  }

  #endregion

  #region Law 5 - every hearth can reach the temperature it works at

  /// <summary>
  /// A furnace whose threshold its own heat balance cannot reach is a machine that lights, holds and never
  /// does anything - which is what the puddling furnace was while it carried iron's melting point. Stated
  /// as reachability rather than as a number: every coefficient in the model is a proposal to calibrate in
  /// play, and an <c>InlineData(1400f)</c> would go red on every retune while saying nothing about whether
  /// the machine still works.
  /// <para>
  /// Read at the machine's best case - a full firebox, the damper open, both doors shut - because that is
  /// the ceiling the player is aiming at. Whether the chimney is what carries it there is a per-machine
  /// fact and not asserted here: it is load-bearing on the puddling furnace and a heat-treatment dial on
  /// the reheat furnace, which works stock far below its own ceiling.
  /// </para>
  /// </summary>
  public static void EveryFireboxReachesItsOwnProcessTemperature() {
    Type[] fireboxes = Leaves()
      .Where(t => typeof(BlockEntityFireboxFurnace).IsAssignableFrom(t))
      .ToArray();
    Assert.NotEmpty(fireboxes);

    foreach (Type leaf in fireboxes)
      foreach (ExBlockDef def in LayoutsNaming(leaf)) {
        var be = (BlockEntityMultiblockStructure)Activator.CreateInstance(leaf)!;
        be.Pos = new BlockPos(0, 16, 0);
        new TestWorld().Attach(be);
        FurnaceLayoutRig.OrientWithLayout(be, def, $"{def.Code}-north", "north");
        ReflectionHelpers.Invoke(be, "CacheAttributes");

        int capacity = (int)
          ReflectionHelpers.GetProperty(be, "ChargeCapacityUnits")!;
        float threshold = (float)
          ReflectionHelpers.GetProperty(be, "MeltingPoint")!;
        int courses = (int)
          ReflectionHelpers.GetProperty(be, "RatedStackCourses")!;

        float withStack = Settles(be, capacity);
        Assert.True(
          withStack >= threshold,
          $"{leaf.Name} on {def.Code} works at {threshold} C but its own {courses}-course stack settles "
            + $"at {withStack} C - it lights, holds and never reaches its process. Move the losses or the "
            + "stack, not the process temperature"
        );
      }
  }

  /// <summary>
  /// Where a full firebox settles at the machine's own best case - its rated stack, the damper open, no
  /// door venting.
  /// </summary>
  /// <remarks>
  /// Stated rather than assumed. Reading the balance as it stands measures the moment: a furnace whose
  /// chimney the player builds has no chimney in a bare rig, and one whose damper defaults shut is being
  /// asked the wrong question. Both were true of the crucible furnace, and the guard passed for the two
  /// hearths only because their absent cap happened to default open.
  /// </remarks>
  private static float Settles(BlockEntity be, int units) =>
    (
      (HeatBalance)
        ReflectionHelpers.Invoke(
          be,
          "ComputeHeatBalanceAt",
          new BurdenMix(0f, 0f, units),
          0f,
          20f,
          units,
          (float)ReflectionHelpers.GetProperty(be, "BestNaturalDraught")!
        )!
    ).TProcess;

  #endregion

  #region Shared

  /// <summary>
  /// Every code-first blocktype in <paramref name="leaf"/>'s own assembly that names it as its
  /// <c>entityClass</c> - the drawing or drawings a furnace of this class is built from.
  /// <para>
  /// Built in a throwaway domain and matched on the type-name suffix, because <c>entityClass</c> is
  /// <c>"{domain}.{TypeName}"</c> (<c>EntityRegistry.KeyFor</c>) and the domain a provider is registered
  /// under is not recoverable from the type. Nothing read here depends on the domain: legend codes are
  /// literals in the drawing, so roles and offsets are the same whichever domain the def is built in.
  /// </para>
  /// </summary>
  private static List<ExBlockDef> LayoutsNaming(Type leaf) {
    const string probeDomain = "branchguardprobe";
    string suffix = "." + leaf.Name;
    var found = new List<ExBlockDef>();

    Type[] types;
    try {
      types = leaf.Assembly.GetTypes();
    } catch (ReflectionTypeLoadException ex) {
      types = [.. ex.Types.Where(t => t is not null).Select(t => t!)];
    }

    foreach (Type type in types) {
      IEnumerable<ExBlockDef> defs;
      try {
        defs = ExDefinitions.DefinitionsOf(type, probeDomain);
      } catch {
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
