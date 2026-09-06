using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ExpandedLib.Blocks;

/// <summary>
/// Finds every <see cref="PersistAttribute"/> field or property on a block entity's type and declares
/// each into an <see cref="ExBlockState"/>, so marking a member is enough - no <c>DeclareState</c>
/// override needed for it. The reflection walk runs once per concrete type; every later block entity of
/// that type reuses the compiled accessors.
/// </summary>
public static class PersistScan {
  // One member's contribution: given the block entity instance and the state being built, declares it.
  private delegate void Binder(object instance, ExBlockState state);

  private static readonly ConcurrentDictionary<Type, Binder[]> _cache = new();

  private const BindingFlags MemberFlags =
    BindingFlags.Instance
    | BindingFlags.Public
    | BindingFlags.NonPublic
    | BindingFlags.DeclaredOnly;

  /// <summary>
  /// Declares every <see cref="PersistAttribute"/> member of <paramref name="owner"/>'s type (base
  /// types first) into <paramref name="state"/>. <paramref name="owner"/> is a block entity or a
  /// block-entity behaviour - the scan only ever reflects over its type. Throws
  /// <see cref="NotSupportedException"/>, naming the member, the first time a type carries a
  /// <c>[Persist]</c> member of a type PersistScan does not know how to serialize.
  /// </summary>
  public static void Declare(object owner, ExBlockState state) {
    foreach (Binder binder in _cache.GetOrAdd(owner.GetType(), BuildBinders))
      binder(owner, state);
  }

  private static Binder[] BuildBinders(Type type) {
    // Base types first, matching declaration order a hand-written DeclareState would read top to bottom.
    var chain = new List<Type>();
    for (Type? t = type; t != null && t != typeof(object); t = t.BaseType)
      chain.Add(t);
    chain.Reverse();

    var binders = new List<Binder>();
    foreach (Type t in chain) {
      foreach (FieldInfo field in t.GetFields(MemberFlags))
        if (field.GetCustomAttribute<PersistAttribute>() is { } attr)
          binders.Add(BuildBinder(field, field.FieldType, attr));
      foreach (PropertyInfo prop in t.GetProperties(MemberFlags))
        if (prop.GetCustomAttribute<PersistAttribute>() is { } attr)
          binders.Add(BuildBinder(prop, prop.PropertyType, attr));
    }
    return [.. binders];
  }

  private static Binder BuildBinder(
    MemberInfo member,
    Type memberType,
    PersistAttribute attr
  ) {
    string key = attr.Key ?? StripLeadingUnderscore(member.Name);
    string? legacy = attr.Legacy;
    System.Func<object, object?> getter = CompileGetter(member, memberType);
    // Compiled lazily: an IPersistable member is read in place and never assigned, so a readonly field
    // holding one (the natural way to declare it) must not force a setter that Expression.Assign would
    // refuse to build.
    Lazy<Action<object, object?>> setterLazy = new(
      () => CompileSetter(member, memberType)
    );

    if (typeof(IPersistable).IsAssignableFrom(memberType))
      return (be, state) =>
        state.Tree(
          key,
          t => {
            if (getter(be) is IPersistable p) {
              var sub = new TreeAttribute();
              p.ToTree(sub);
              t[key] = sub;
            }
          },
          (t, world) => {
            if (getter(be) is IPersistable p && t.GetTreeAttribute(key) is { } sub)
              p.FromTree(sub, world);
          }
        );

    Action<object, object?> setter = setterLazy.Value;

    if (memberType == typeof(bool))
      return Scalar(
        key,
        legacy,
        getter,
        setter,
        (t, v) => t.SetBool(key, (bool)v!),
        (t, k) => t.GetBool(k)
      );
    if (memberType == typeof(int))
      return Scalar(
        key,
        legacy,
        getter,
        setter,
        (t, v) => t.SetInt(key, (int)v!),
        (t, k) => t.GetInt(k)
      );
    if (memberType == typeof(long))
      return Scalar(
        key,
        legacy,
        getter,
        setter,
        (t, v) => t.SetLong(key, (long)v!),
        (t, k) => t.GetLong(k)
      );
    if (memberType == typeof(float))
      return Scalar(
        key,
        legacy,
        getter,
        setter,
        (t, v) => t.SetFloat(key, (float)v!),
        (t, k) => t.GetFloat(k)
      );
    if (memberType == typeof(double))
      return Scalar(
        key,
        legacy,
        getter,
        setter,
        (t, v) => t.SetDouble(key, (double)v!),
        (t, k) => t.GetDouble(k)
      );
    if (memberType == typeof(string))
      return Scalar(
        key,
        legacy,
        getter,
        setter,
        (t, v) => {
          if (v is string s)
            t.SetString(key, s);
        },
        (t, k) => t.GetString(k, null)
      );
    if (memberType.IsEnum)
      return Scalar(
        key,
        legacy,
        getter,
        setter,
        (t, v) => t.SetInt(key, Convert.ToInt32(v)),
        (t, k) => Enum.ToObject(memberType, t.GetInt(k))
      );
    if (memberType == typeof(BlockPos))
      return (be, state) =>
        state.Pos(
          key,
          () => (BlockPos?)getter(be),
          v => setter(be, v)
        );
    if (memberType == typeof(ItemStack))
      return (be, state) =>
        state.Stack(
          key,
          () => (ItemStack?)getter(be),
          v => setter(be, v)
        );
    throw new NotSupportedException(
      $"[Persist] on {member.DeclaringType?.Name}.{member.Name} has unsupported type "
        + $"{memberType.Name}."
    );
  }

  /// <summary>
  /// Builds a binder for a single-key scalar. Without <paramref name="legacy"/> this is just the field
  /// under <paramref name="key"/>; with it, a load checks <paramref name="key"/> first and only falls
  /// back to <paramref name="legacy"/> when <paramref name="key"/> is absent - a save always writes
  /// <paramref name="key"/> alone, so the old name is never reintroduced.
  /// </summary>
  private static Binder Scalar(
    string key,
    string? legacy,
    System.Func<object, object?> getter,
    Action<object, object?> setter,
    Action<ITreeAttribute, object?> write,
    System.Func<ITreeAttribute, string, object?> readAt
  ) {
    if (legacy == null)
      return (be, state) =>
        state.Tree(
          key,
          t => write(t, getter(be)),
          (t, _) => setter(be, readAt(t, key))
        );

    return (be, state) =>
      state.Tree(
        key,
        t => write(t, getter(be)),
        (t, _) => {
          if (t.HasAttribute(key))
            setter(be, readAt(t, key));
          else if (t.HasAttribute(legacy))
            setter(be, readAt(t, legacy));
        }
      );
  }

  private static string StripLeadingUnderscore(string name) =>
    name.StartsWith('_') ? name[1..] : name;

  #region Compiled accessors

  private static System.Func<object, object?> CompileGetter(
    MemberInfo member,
    Type memberType
  ) {
    ParameterExpression instance = Expression.Parameter(
      typeof(object),
      "instance"
    );
    Expression typedInstance = Expression.Convert(
      instance,
      member.DeclaringType!
    );
    Expression access = member switch {
      FieldInfo f => Expression.Field(typedInstance, f),
      PropertyInfo p => Expression.Property(typedInstance, p),
      _ => throw new NotSupportedException(member.Name),
    };
    Expression boxed = Expression.Convert(access, typeof(object));
    return Expression
      .Lambda<System.Func<object, object?>>(boxed, instance)
      .Compile();
  }

  private static Action<object, object?> CompileSetter(
    MemberInfo member,
    Type memberType
  ) {
    ParameterExpression instance = Expression.Parameter(
      typeof(object),
      "instance"
    );
    ParameterExpression value = Expression.Parameter(typeof(object), "value");
    Expression typedInstance = Expression.Convert(
      instance,
      member.DeclaringType!
    );
    Expression typedValue = Expression.Convert(value, memberType);
    Expression assign = member switch {
      FieldInfo f => Expression.Assign(Expression.Field(typedInstance, f), typedValue),
      PropertyInfo p => Expression.Assign(Expression.Property(typedInstance, p), typedValue),
      _ => throw new NotSupportedException(member.Name),
    };
    return Expression
      .Lambda<Action<object, object?>>(assign, instance, value)
      .Compile();
  }

  #endregion
}
