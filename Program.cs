// Copyright (c) November 2025 Félix-Olivier Dumas. All rights reserved.
// Licensed under the terms described in the LICENSE file

using System.Diagnostics;
using System.Runtime.CompilerServices;

public static class EntityId {
    private static int _nextId = 0;

    public static int Next() => _nextId++;
}

public readonly struct Entity {
    public readonly int Value;

    public Entity() => Value = EntityId.Next();
}

class Component {
    public Component() { }
}

class Name : Component {
    public string name;
}

class Position : Component {
    public int X;
    public int Y;
}

class Velocity : Component {
    public int X;
    public int Y;
}

class EntityManager {
    private const int InitialTypeFlagsCapacity = 8;
    private const int InitialEntityCapacity = 131_072;
    private const int InitialPoolCapacity = 524_288;

    private Component[] _pool = new Component[InitialPoolCapacity];
    private int[][] _jreg = new int[InitialEntityCapacity][];
    private Type[] _typeFlags = new Type[InitialTypeFlagsCapacity];
    private int[] _typeIndex = new int[InitialPoolCapacity];

    private int[] _cCount = new int[InitialEntityCapacity];
    private int _tfCount = 0;
    private int _tiCount = 0;
    private int _eCount = 0;
    private int _pCount = 0;


    public EntityManager() { }


    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public void Add<T>(int eidx) where T : Component, new() {
        int tfCount = _tfCount;
        int tiCount = _tiCount;
        int eCount = _eCount;
        int pCount = _pCount;

        int tfLen = _typeFlags.Length;
        while (_tiCount >= _typeIndex.Length)
            Array.Resize(ref _typeIndex, _typeIndex.Length * 2);

        Type t = typeof(T);
        int foundIndex = -1;
        for (int i = 0; i < tfCount; i++)
            if (_typeFlags[i] == t)
                foundIndex = i;

        if (foundIndex == -1) {
            _typeFlags[tfCount] = t;
            foundIndex = tfCount;
            tfCount++;
        }

        while (eidx >= _jreg.Length) {
            Array.Resize(ref _jreg, _jreg.Length * 2);
            Array.Resize(ref _cCount, _cCount.Length * 2);
        }

        int[] cArr = _jreg[eidx];
        if (cArr == null)
            _jreg[eidx] = cArr = new int[8];

        if (_cCount[eidx] >= cArr.Length) {
            Array.Resize(ref cArr, cArr.Length * 2);
            _jreg[eidx] = cArr;
        }

        if (pCount >= _pool.Length)
            Array.Resize(ref _pool, _pool.Length * 2);

        _typeIndex[pCount] = foundIndex;
        _pool[pCount] = new T();
        cArr[_cCount[eidx]] = pCount;

        _tfCount = tfCount;
        _tiCount = tiCount + 1;
        _pCount = pCount + 1;
        _cCount[eidx]++;
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public T? Get<T>(int eidx) where T : Component, new() {
        int[] cArr = _jreg[eidx];

        Type t = typeof(T);
        if (cArr != null) {
            var span = cArr.AsSpan(0, _cCount[eidx]);
            for (int i = 0; i < _cCount[eidx]; i++) {
                ref int compID = ref cArr[i];
                int typeIndex = _typeIndex[compID];
                if (_typeFlags[typeIndex] == t)
                    return Unsafe.As<Component, T>(ref _pool[compID]);
            }
        } return null;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int Count(int eidx) => _cCount[eidx];


    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool HasComponents(int eidx) => _cCount[eidx] != 0;


    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public List<string> GetComponentNames(int eidx) {
        var names = new List<string>();

        int[] cArr = _jreg[eidx];
        if (cArr != null) {
            Span<int> span = cArr.AsSpan(0, _cCount[eidx]);
            for (int i = 0; i < _cCount[eidx]; i++) {
                names.Add(_pool[span[i]].ToString());
            }
        } return names;
    }
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
            entityManager.Add<Name>(e.Value);
            entityManager.Add<Position>(e.Value);
            entityManager.Add<Velocity>(e.Value);
        }
        sw.Stop();
        Console.WriteLine($"Setup {entityCount} entities with {componentsPerEntity} components each: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        for (int i = 0; i < entityCount; i++) {
            var e = entities[i];
            var name = entityManager.Get<Name>(e.Value);
            if (name != null) name.name = "Test" + i;
        }
        sw.Stop();
        Console.WriteLine($"Accessed and modified Name components: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        for (int i = 0; i < entityCount; i++) {
            var e = entities[i];
            var pos = entityManager.Get<Position>(e.Value);
            var vel = entityManager.Get<Velocity>(e.Value);
            if (pos != null) { pos.X = i; pos.Y = i * 2; }
            if (vel != null) { vel.X = i; vel.Y = i * 2; }
        }
        sw.Stop();
        Console.WriteLine($"Accessed and modified Position and Velocity components: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        var testEntity = entities[0];
        var count = entityManager.Count(testEntity.Value);
        sw.Stop();
        Console.WriteLine($"CountComponents lookup for 1 entity: {sw.ElapsedTicks} ticks");

        var randomEntity = entities[(entityCount / 2)];
        var nameRandom = entityManager.Get<Name>(randomEntity.Value);
        Console.WriteLine($"Random entity Name component: {nameRandom?.name}");
    }
}
