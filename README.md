<div align="center">

<h1>Apex</h1>
<h3><em>A high-performance, modular C# ECS</em></h3>

<p>
  <a href="https://docs.microsoft.com/en-us/dotnet/csharp/">
    <img src="https://img.shields.io/badge/C%23-11.0-239120?style=flat-square&logo=c-sharp&logoColor=white" alt="C#">
  </a>
  <a href="https://dotnet.microsoft.com/">
    <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET">
  </a>
  <a href="https://www.libsdl.org/">
    <img src="https://img.shields.io/badge/SDL2-Enabled-00599C?style=flat-square&logo=steam&logoColor=white" alt="SDL2">
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/License-Custom-orange?style=flat-square" alt="License">
  </a>
  <a href="https://github.com/unrays/Quark">
    <img src="https://img.shields.io/badge/Status-Active%20Development-success?style=flat-square" alt="Status">
  </a>
</p>

<p>
<em>---</em>
</p>

<p>
  <a href="#-features">Features</a> • 
  <a href="#-getting-started">Getting Started</a> • 
  <a href="#-architecture">Architecture</a> • 
  <a href="#-showcase">Showcase</a> • 
  <a href="#-documentation">Documentation</a>
</p>

</div>

---

## Benchmark

```console
Setup 100000 entities with 3 components each: 1864 ms
Accessed and modified Name components: 16 ms
Accessed and modified Position and Velocity components: 30 ms
CountComponents lookup for 1 entity: 8231 ticks
Random entity Name component:
```

---

## Full Code

```csharp
// Copyright (c) November 2025 Félix-Olivier Dumas. All rights reserved.
// Licensed under the terms described in the LICENSE file

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

using Entity = Entity<System.UInt32>;
public interface IEntity<T> where T : unmanaged { T Id { get; } }
public readonly struct Entity<T> : IEntity<T> where T : unmanaged, IComparable<T> {
    public T Id { get; }
    public Entity(T id) => Id = id;
    public static explicit operator Entity<T>(T id) => new Entity<T>(id);
    public static implicit operator T(Entity<T> e) => e.Id;
}

public interface IComponent<T> where T : unmanaged { T Id { get; set; } }

public class Component<T> : IComponent<T> where T : unmanaged, IComparable<T> {
    public T Id { get; set; }
    public Component() => Id = default;
    public Component(T id) => Id = id;
}

public class Name : Component<UInt32> {
    public string NameValue { get; set; } = "";
    public Name() : base() { }
    public Name(string name, UInt32 id) : base(id) => NameValue = name;
}

public class Position : Component<UInt32> {
    public int X { get; set; }
    public int Y { get; set; }
    public Position() : base() { }
    public Position(int x, int y, UInt32 id) : base(id) { X = x; Y = y; }
}

public class Velocity : Component<UInt32> {
    public int X { get; set; }
    public int Y { get; set; }
    public Velocity() : base() { }
    public Velocity(int x, int y, UInt32 id) : base(id) { X = x; Y = y; }
}


//class ComponentPool : ComponentPool<Component, UInt32> { }
class ComponentPool<C, T> where C : IComponent<T> where T : unmanaged, INumber<T> {
    private readonly Dictionary<Type, int> _typeIds = new Dictionary<Type, int>();
    private readonly Dictionary<Type, HashSet<T>> _componentsByTypeId = new Dictionary<Type, HashSet<T>>();
    private C[] _components = new C[32];

    public ComponentPool() { }

    private void InternalRegister(C cmp) => InternalRegister(cmp.Id, cmp);
    private void InternalRegister(T idx, C cmp) {
        int uidx = int.CreateTruncating(idx);
        if (uidx >= _components.Length)
            Array.Resize(ref _components, _components.Length * 2);
        _components[uidx] = cmp;

        if (!_typeIds.ContainsKey(cmp.GetType()))
            _typeIds[cmp.GetType()] = _typeIds.Count;
        if (!_componentsByTypeId.TryGetValue(cmp.GetType(), out var set))
            _componentsByTypeId[cmp.GetType()] = set = new HashSet<T>();
        set.Add(idx);
    }

    private C? InternalFetch(T idx) => _components[int.CreateTruncating(idx)] ?? default;

    public void AddAt(T idx, C cmp) => InternalRegister(idx, cmp);

    public T Add<CC>() where CC : C, new() {
        var c = new CC(); InternalRegister(c);
        return c.Id;
    }

    public CC? GetIfPresent<CC>(T id) where CC : C {
        if (_componentsByTypeId.TryGetValue(typeof(CC), out var ids) && ids.Contains(id)) {
            var comp = _components[int.CreateTruncating(id)];
            if (comp is CC typed) return typed;
        } return default;
    }

    public C? GetAt(T idx) => InternalFetch(idx);

    public T GetTypeId<CC>() where CC : C {
        Type type = typeof(CC);
        if (!_typeIds.TryGetValue(type, out var id)) {
            id = _typeIds.Count;
            _typeIds[type] = id;
        } return T.CreateTruncating(id);
    }

    public HashSet<T> GetTypeIds<CC>() where CC : C {
        Type type = typeof(CC);
        if (!_componentsByTypeId.TryGetValue(type, out var set)) {
            set = new HashSet<T>();
            _componentsByTypeId[type] = set;
        } return set;
    }
}

//class EntityManager : EntityManager<Entity, Component, UInt32> { }
class EntityManager32<E, C> : EntityManager<E, C, UInt32>
where C : IComponent<UInt32>, new()
where E : IEntity<UInt32> { }
class EntityManager<E, C, T>
where C : IComponent<T>, new() where T : unmanaged, INumber<T> where E : IEntity<T> {
    private readonly Dictionary<T, HashSet<T>> _registry = new Dictionary<T, HashSet<T>>();
    private readonly Dictionary<T, HashSet<T>> _entityTypeIds = new Dictionary<T, HashSet<T>>();
    private readonly ComponentPool<C, T> _pool = new ComponentPool<C, T>();

    public EntityManager() { }

    public void AddComponent<C0>(E e) where C0 : C, new() {
        if (!_registry.TryGetValue(e.Id, out var components))
            _registry[e.Id] = components = new HashSet<T>();
        if (!_entityTypeIds.TryGetValue(e.Id, out var typeIds))
            _entityTypeIds[e.Id] = typeIds = new HashSet<T>();

        var typeId = _pool.GetTypeId<C0>();
        if (!_entityTypeIds[e.Id].Add(typeId)) {
            Trace.TraceWarning("Component of type has already been added.");
            return;
        }

        if (!_registry[e.Id].Add(_pool.Add<C0>())) {
            Trace.TraceWarning("Unable to add component.");
            return;
        }
    }

    public C0? getComponent<C0>(E e) where C0 : C, new() {
        if (!_entityTypeIds.TryGetValue(e.Id, out var typeIds))
            return default;

        var typeId = _pool.GetTypeId<C0>();
        if (!typeIds.Contains(typeId))
            return default;

        var compIds = _pool.GetTypeIds<C0>();
        foreach (var compId in compIds) {
            if (_registry[e.Id].Contains(compId)) {
                var comp = _pool.GetIfPresent<C0>(compId);
                if (comp != null) return comp;
            }
        } return default;
    }

    public T CountComponents(E e) => T.CreateChecked(_registry[e.Id].Count);

    public Boolean HasComponents(E e) => _registry[e.Id].Count() is not 0;

    public List<string> GetComponentNames(E e) {
        var names = new List<string>();
        if (_registry.TryGetValue(e.Id, out var components)) {
            foreach (var compId in components) {
                var comp = _pool.GetAt(compId);
                if (comp != null)
                    names.Add(comp.GetType().Name);
            }
        }
        Console.WriteLine(e);
        return names;
    }
}

class Program {
    static UInt32 entityCounter = 0;
    static void Main(string[] args) {
        // Tests generated by ChatGPT - OpenAI

        var entityManager = new EntityManager<Entity, Component<UInt32>, UInt32>();
        UInt32 entityCounter = 0;

        UInt32 entityCount = 100_000;
        int componentsPerEntity = 3;

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < entityCount; i++) {
            var e = new Entity(entityCounter++);
            entityManager.AddComponent<Name>(e);
            entityManager.AddComponent<Position>(e);
            entityManager.AddComponent<Velocity>(e);
        }

        sw.Stop();
        Console.WriteLine($"Setup {entityCount} entities with {componentsPerEntity} components each: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        for (int i = 0; i < entityCount; i++) {
            var e = new Entity((UInt32)i);
            var name = entityManager.getComponent<Name>(e);
            if (name != null) name.NameValue = "Test" + i;
        }
        sw.Stop();
        Console.WriteLine($"Accessed and modified Name components: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        for (int i = 0; i < entityCount; i++) {
            var e = new Entity((UInt32)i);
            var pos = entityManager.getComponent<Position>(e);
            var vel = entityManager.getComponent<Velocity>(e);
            if (pos != null) { pos.X = i; pos.Y = i * 2; }
            if (vel != null) { vel.X = i; vel.Y = i * 2; }
        }
        sw.Stop();
        Console.WriteLine($"Accessed and modified Position and Velocity components: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        var testEntity = new Entity(0);
        var count = entityManager.CountComponents(testEntity);
        sw.Stop();
        Console.WriteLine($"CountComponents lookup for 1 entity: {sw.ElapsedTicks} ticks");

        var randomEntity = new Entity(entityCount / 2);
        var nameRandom = entityManager.getComponent<Name>(randomEntity);
        Console.WriteLine($"Random entity Name component: {nameRandom?.NameValue}");
    }
}
```
