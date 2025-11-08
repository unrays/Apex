<div align="center">

<h1>Apex</h1>
<h3><em>A C# ECS framework for experimentation and performance testing</em></h3>

<p>
  <a href="https://docs.microsoft.com/en-us/dotnet/csharp/">
    <img src="https://img.shields.io/badge/C%23-11.0-239120?style=flat-square&logo=c-sharp&logoColor=white" alt="C#">
  </a>
  <a href="https://dotnet.microsoft.com/">
    <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET">
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/License-Custom-orange?style=flat-square" alt="License">
  </a>
</p>

<p>
Apex is a small ECS framework used to explore different design approaches, benchmark performance, and document trade-offs.  
It shows step-by-step iterations, memory access patterns, and system designs to understand how each choice affects efficiency and maintainability.
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

**First iteration** – Super performant and simple
*Fast, lean, and easy to understand.*

```console
Setup 100000 entities with 3 components each: 29 ms
Accessed and modified Name components: 21 ms
Accessed and modified Position and Velocity components: 26 ms
CountComponents lookup for 1 entity: 1963 ticks
Random entity Name component: Test50000
```

```csharp
// Copyright (c) October 2025 Félix-Olivier Dumas. All rights reserved.
// Licensed under the terms described in the LICENSE file

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

class Entity {
    private static UInt32 nextId;
    private readonly UInt32 id;

    public Entity() => this.id = nextId++;
    public UInt32 getId() => id;
}

class Component {
    public Component() { }

    public void print() => Console.WriteLine(this.GetType().Name);
}

class Movement : Component {
    public float SpeedX { get; set; }
    public float SpeedY { get; set; }
    public (float X, float Y) Direction { get; set; } = (0, 0);

    public void SetDirection(float x, float y) {
        var length = MathF.Sqrt(x * x + y * y);
        Direction = length == 0 ? (0, 0) : (x / length, y / length);
    }
}

class Name : Component {
    public string? name { get; set; }
}

class EntityManager<E, C> where E : Entity where C : Component {
    private readonly Dictionary<E, List<C>> registry = new Dictionary<E, List<C>>();

    public EntityManager() { }

    public void AddComponent<CC>(E e) where CC : C, new() {
        if (!registry.TryGetValue(e, value: out var components)) {
            components = new List<C>(); registry[e] = components;
        }
        components.Add(new CC()); // aucune verif si un component du meme type est deja présent
    }

    public CC? getComponent<CC>(E e) where CC : C, new() {
        if (registry.TryGetValue(e, value: out var components)) {
            var corresponding = components.OfType<CC>().FirstOrDefault();
            return corresponding;
        }
        return null;
    }

    public UInt32 countComponents(E e) => (UInt32)registry[e].Count();

    public Boolean hasComponents(E e) => registry[e].Count() is not 0;

    public List<string> getComponentNames(E e) {
        var names = new List<string>();
        registry[e].ForEach(obj => names.Add(obj.GetType().Name));
        return names;
    }
}
class Position : Component {
    public int X { get; set; }
    public int Y { get; set; }
}

class Velocity : Component {
    public int X { get; set; }
    public int Y { get; set; }
}

class Program {
    static void Main(string[] args) {
        var entityManager = new EntityManager<Entity, Component>();
        UInt32 entityCount = 100_000;
        int componentsPerEntity = 3;

        var entities = new List<Entity>((int)entityCount);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < entityCount; i++) {
            var e = new Entity();
            entities.Add(e);
            entityManager.AddComponent<Name>(e);
            entityManager.AddComponent<Position>(e);
            entityManager.AddComponent<Velocity>(e);
        }
        sw.Stop();
        Console.WriteLine($"Setup {entityCount} entities with {componentsPerEntity} components each: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        for (int i = 0; i < entityCount; i++) {
            var e = entities[i];
            var name = entityManager.getComponent<Name>(e);
            if (name != null) name.name = "Test" + i;
        }
        sw.Stop();
        Console.WriteLine($"Accessed and modified Name components: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        for (int i = 0; i < entityCount; i++) {
            var e = entities[i];
            var pos = entityManager.getComponent<Position>(e);
            var vel = entityManager.getComponent<Velocity>(e);
            if (pos != null) { pos.X = i; pos.Y = i * 2; }
            if (vel != null) { vel.X = i; vel.Y = i * 2; }
        }
        sw.Stop();
        Console.WriteLine($"Accessed and modified Position and Velocity components: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        var testEntity = entities[0];
        var count = entityManager.countComponents(testEntity);
        sw.Stop();
        Console.WriteLine($"CountComponents lookup for 1 entity: {sw.ElapsedTicks} ticks");

        var randomEntity = entities[(int)(entityCount / 2)];
        var nameRandom = entityManager.getComponent<Name>(randomEntity);
        Console.WriteLine($"Random entity Name component: {nameRandom?.name}");
    }
}
```

---

**Second iteration** – Over 10x slower than first iteration  
*Conceptually elegant, great design, but not optimized for speed.*

```console
Setup 100000 entities with 3 components each: 1814 ms
Accessed and modified Name components: 17 ms
Accessed and modified Position and Velocity components: 29 ms
CountComponents lookup for 1 entity: 4663 ticks
Random entity Name component:
```


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

---

**Third iteration** – Significantly more performant (~10-15% faster than first iteration)
*Lean, efficient, and battle-tested.*

```console
Setup 100000 entities with 3 components each: 29 ms
Accessed and modified Name components: 19 ms
Accessed and modified Position and Velocity components: 25 ms
CountComponents lookup for 1 entity: 1616 ticks
Random entity Name component: Test50000
```

```csharp
// Copyright (c) October 2025 Félix-Olivier Dumas. All rights reserved.
// Licensed under the terms described in the LICENSE file

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Schema;

class Entity {
    private static int nextId;
    private readonly int id;

    public Entity() => this.id = nextId++;
    public int getId() => id;
}

class Component {
    public Component() { }

    public void print() => Console.WriteLine(this.GetType().Name);
}

class Movement : Component {
    public float SpeedX { get; set; }
    public float SpeedY { get; set; }
    public (float X, float Y) Direction { get; set; } = (0, 0);

    public void SetDirection(float x, float y) {
        var length = MathF.Sqrt(x * x + y * y);
        Direction = length == 0 ? (0, 0) : (x / length, y / length);
    }
}

class Name : Component {
    public string? name { get; set; }
}

class EntityManager {
    private readonly Dictionary<int, List<Component>> registry = new Dictionary<int, List<Component>>();

    public EntityManager() { }

    public void AddComponent<C>(int e) where C : Component, new() {
        if (!registry.TryGetValue(e, value: out var components)) {
            components = new List<Component>(); registry[e] = components;
        } components.Add(new C());
    }

    public C? getComponent<C>(int e) where C : Component, new() {
        if (registry.TryGetValue(e, value: out var components)) {
            var corresponding = components.OfType<C>().FirstOrDefault();
            return corresponding;
        } return default;
    }

    public int countComponents(int e) => registry[e].Count();

    public bool hasComponents(int e) => registry[e].Count() is not 0;

    public List<string> getComponentNames(int e) {
        var names = new List<string>();
        registry[e].ForEach(obj => names.Add(obj.GetType().Name));
        return names;
    }
}
class Position : Component {
    public int X { get; set; }
    public int Y { get; set; }
}

class Velocity : Component {
    public int X { get; set; }
    public int Y { get; set; }
}

class Program {
    static void Main(string[] args) {
        var entityManager = new EntityManager();
        int entityCount = 100_000;
        int componentsPerEntity = 3;

        var entities = new List<Entity>(entityCount);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < entityCount; i++) {
            var e = new Entity();
            entities.Add(e);
            entityManager.AddComponent<Name>(e.getId());
            entityManager.AddComponent<Position>(e.getId());
            entityManager.AddComponent<Velocity>(e.getId());
        }
        sw.Stop();
        Console.WriteLine($"Setup {entityCount} entities with {componentsPerEntity} components each: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        for (int i = 0; i < entityCount; i++) {
            var e = entities[i];
            var name = entityManager.getComponent<Name>(e.getId());
            if (name != null) name.name = "Test" + i;
        }
        sw.Stop();
        Console.WriteLine($"Accessed and modified Name components: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        for (int i = 0; i < entityCount; i++) {
            var e = entities[i];
            var pos = entityManager.getComponent<Position>(e.getId());
            var vel = entityManager.getComponent<Velocity>(e.getId());
            if (pos != null) { pos.X = i; pos.Y = i * 2; }
            if (vel != null) { vel.X = i; vel.Y = i * 2; }
        }
        sw.Stop();
        Console.WriteLine($"Accessed and modified Position and Velocity components: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        var testEntity = entities[0];
        var count = entityManager.countComponents(testEntity.getId());
        sw.Stop();
        Console.WriteLine($"CountComponents lookup for 1 entity: {sw.ElapsedTicks} ticks");

        var randomEntity = entities[(entityCount / 2)];
        var nameRandom = entityManager.getComponent<Name>(randomEntity.getId());
        Console.WriteLine($"Random entity Name component: {nameRandom?.name}");
    }
}
```

---

**Fourth iteration** – Performance similar to first iteration (~30% faster than first iteration, ~20% faster than second iteration) 
*Highly optimized for runtime, but setup overhead is a bit higher.*

```console
Setup 100000 entities with 3 components each: 35 ms 
Accessed and modified Name components: 12 ms
Accessed and modified Position and Velocity components: 12 ms
CountComponents lookup for 1 entity: 1293 ticks
Random entity Name component: Test50000
```

```csharp
// Copyright (c) November 2025 Félix-Olivier Dumas. All rights reserved.
// Licensed under the terms described in the LICENSE file

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Schema;
using static System.Runtime.InteropServices.JavaScript.JSType;

class Entity {
    private static int nextId;
    private readonly int id;

    public Entity() => this.id = nextId++;
    public int getId() => id;
}

class Component {
    public Component() { }

    public void print() => Console.WriteLine(this.GetType().Name);
}

class Movement : Component {
    public float SpeedX { get; set; }
    public float SpeedY { get; set; }
    public (float X, float Y) Direction { get; set; } = (0, 0);

    public void SetDirection(float x, float y) {
        var length = MathF.Sqrt(x * x + y * y);
        Direction = length == 0 ? (0, 0) : (x / length, y / length);
    }
}

class Name : Component {
    public string? name { get; set; }
}

class EntityManager {
    private readonly Dictionary<int, Memory<Component>> _reg = new Dictionary<int, Memory<Component>>();
    private Dictionary<int, int> _count = new Dictionary<int, int>();

    public EntityManager() { }

    public void AddComponent<C>(int eidx) where C : Component, new() {
        if (!_reg.TryGetValue(eidx, out var memc)) {
            Component[] initArr = new Component[10];
            memc = initArr;
            _reg[eidx] = memc;
            _count[eidx] = 0;
        }

        int len = memc.Span.Length;
        int count = _count[eidx];
        if (count == len) {
            Component[] newArray = new Component[len * 2];
            memc.Span.CopyTo(newArray);
            memc = newArray;
            _reg[eidx] = memc;
        }

        Span<Component> span = memc.Span;
        span[count] = new C();
        _count[eidx] = count + 1;
    }

    public C? getComponent<C>(int eidx) where C : Component, new() {
        Span<Component> span = _reg[eidx].Span;
        for (int i = 0; i < _count[eidx]; i++) {
            ref C c = ref Unsafe.As<Component, C>(ref span[i]);
            return c;
        }
        return null;
    }

    public int countComponents(int eidx) => _count[eidx];

    public bool hasComponents(int eidx) => _count[eidx] > 0;

    public List<string> getComponentNames(int eidx) {
        var names = new List<string>();
        if (_reg.TryGetValue(eidx, out var memc)) {
            Span<Component> span = memc.Span;
            for (int i = 0; i < span.Length; i++) {
                ref var c = ref span[i];
                names.Add(item: c.ToString());
            }
        }
        return names;
    }
}
class Position : Component {
    public int X { get; set; }
    public int Y { get; set; }
}

class Velocity : Component {
    public int X { get; set; }
    public int Y { get; set; }
}

class Program {
    static void Main(string[] args) {
        var entityManager = new EntityManager();
        int entityCount = 100_000;
        int componentsPerEntity = 3;

        var entities = new List<Entity>(entityCount);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < entityCount; i++) {
            var e = new Entity();
            entities.Add(e);
            entityManager.AddComponent<Name>(e.getId());
            entityManager.AddComponent<Position>(e.getId());
            entityManager.AddComponent<Velocity>(e.getId());
        }
        sw.Stop();
        Console.WriteLine($"Setup {entityCount} entities with {componentsPerEntity} components each: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        for (int i = 0; i < entityCount; i++) {
            var e = entities[i];
            var name = entityManager.getComponent<Name>(e.getId());
            if (name != null) name.name = "Test" + i;
        }
        sw.Stop();
        Console.WriteLine($"Accessed and modified Name components: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        for (int i = 0; i < entityCount; i++) {
            var e = entities[i];
            var pos = entityManager.getComponent<Position>(e.getId());
            var vel = entityManager.getComponent<Velocity>(e.getId());
            if (pos != null) { pos.X = i; pos.Y = i * 2; }
            if (vel != null) { vel.X = i; vel.Y = i * 2; }
        }
        sw.Stop();
        Console.WriteLine($"Accessed and modified Position and Velocity components: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        var testEntity = entities[0];
        var count = entityManager.countComponents(testEntity.getId());
        sw.Stop();
        Console.WriteLine($"CountComponents lookup for 1 entity: {sw.ElapsedTicks} ticks");

        var randomEntity = entities[(entityCount / 2)];
        var nameRandom = entityManager.getComponent<Name>(randomEntity.getId());
        Console.WriteLine($"Random entity Name component: {nameRandom?.name}");
    }
}
```
