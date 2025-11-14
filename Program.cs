// Copyright (c) November 2025 Félix-Olivier Dumas. All rights reserved.
// Licensed under the terms described in the LICENSE file

using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static class EntityId {
    private static int _nextId = 0;

    public static int Next() => _nextId++;
}

public readonly struct Entity {
    public readonly int Value;

    public Entity() => Value = EntityId.Next();
}

[Flags]
public enum ComponentMask : short { // autant de memoire qu'un int, perf++ (4 bit)
    None = 0,
    Position = 1 << 0,
    Velocity = 1 << 1,
    Rotation = 1 << 2,
    Scale    = 1 << 3,
    Color    = 1 << 4,

    Name     = 1 << 5

        // possiblement faire un set qui contient les valeurs les plus probables
        // genre statistiquement, je peux lui ajouter ca et ca m'évitera d'autres ajouts

}

public static class ComponentMaskMapper {
    public static ComponentMask FromTypeOf(Type type) => type switch {
        Type t when t == typeof(Name) => ComponentMask.Name,
        Type t when t == typeof(Position) => ComponentMask.Position,
        Type t when t == typeof(Velocity) => ComponentMask.Velocity,
        Type t when t == typeof(Rotation) => ComponentMask.Rotation,
        Type t when t == typeof(Scale) => ComponentMask.Scale,
        Type t when t == typeof(Color) => ComponentMask.Color,
        _ => throw new Exception("Unknown type")
    };
}


// en gros, un entité a une liste de component.
// chaque component a une valeur bitmask ComponentTypes
// on peux combiner les types en une seule valeur, optimisé.

// userPerms |= Permissions.Read | Permissions.Write; (ajouter)
// if ((userPerms & Permissions.Write) != 0) (tester s'il peut)
// userPerms &= ~Permissions.Read; (retirer)
// userPerms ^= Permissions.Execute; (inverser)
// bool canReadAndWrite = (userPerms & (Permissions.Read | Permissions.Write)) == (Permissions.Read | Permissions.Write);

//faire bitmask des types du système
//ulong mask0; // types 0-63
//ulong mask1; // types 64-127
//ulong mask2; // types 128-191

// STOCKER PLUSIEURS TYPES PAR ENTITÉ, CRISS, C'EST CA QU'IL FAUT FAIRE

class Component {
    public Component() { }
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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Name {
    public fixed char name[32];
}


[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Position {
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Velocity {
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Rotation {
    public int Angle;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Scale {
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Color {
    public int R;
    public int G;
    public int B;
    public int A;
}


class Registry {
    private const int InitialEntityCapacity = 131_072;

    private const int InitialPositionPool = 200_000;
    private const int InitialVelocityPool = 200_000;
    private const int InitialRotationPool = 200_000;
    private const int InitialScalePool = 200_000;
    private const int InitialColorPool = 200_000;
    private const int InitialNamePool = 200_000;

    private ComponentMask[] _cmask = new ComponentMask[InitialEntityCapacity];
    private ComponentMask[] _ctype = new ComponentMask[InitialPositionPool];
    private int _ctCount = 0; private int _cmCount = 0;

    private Position[] _positions = new Position[InitialPositionPool];
    private int[] _entityToPosIndex = new int[InitialEntityCapacity];
    private int _positionCount = 0;

    private Velocity[] _velocities = new Velocity[InitialVelocityPool];
    private int[] _entityToVelIndex = new int[InitialEntityCapacity];
    private int _velocityCount = 0;

    private Rotation[] _rotations = new Rotation[InitialRotationPool];
    private int[] _entityToRotIndex = new int[InitialEntityCapacity];
    private int _rotationCount = 0;

    private Scale[] _scales = new Scale[InitialScalePool];
    private int[] _entityToScaleIndex = new int[InitialEntityCapacity];
    private int _scaleCount = 0;

    private Color[] _colors = new Color[InitialColorPool];
    private int[] _entityToColorIndex = new int[InitialEntityCapacity];
    private int _colorCount = 0;

    private Name[] _names = new Name[InitialNamePool];
    private int[] _entityToNameIndex = new int[InitialEntityCapacity];
    private int _nameCount = 0;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ComponentMask MaskOf<T>() where T : unmanaged {
        if (typeof(T) == typeof(Position)) return ComponentMask.Position;
        if (typeof(T) == typeof(Velocity)) return ComponentMask.Velocity;
        if (typeof(T) == typeof(Rotation)) return ComponentMask.Rotation;
        if (typeof(T) == typeof(Scale)) return ComponentMask.Scale;
        if (typeof(T) == typeof(Color)) return ComponentMask.Color;
        if (typeof(T) == typeof(Name)) return ComponentMask.Name;
        throw new Exception("Component type not supported");
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public void Add<T>(int eidx) where T : unmanaged {
        ComponentMask mask = _cmask[eidx];
        ComponentMask mapped = MaskOf<T>();
        if ((mask & mapped) != 0) {
            Console.Error.WriteLine("Component already present.");
            return;
        }

        int cmLen = _cmask.Length;
        while (_cmCount >= _cmask.Length)
            Array.Resize(ref _cmask, _cmask.Length * 2);
        while (_ctCount >= _ctype.Length)
            Array.Resize(ref _ctype, _ctype.Length * 2);


        switch (mapped) {
            case ComponentMask.Position:
                if (eidx >= _entityToPosIndex.Length)
                    Array.Resize(ref _entityToPosIndex, Math.Max(_entityToPosIndex.Length * 2, eidx + 1));
                while (_positionCount >= _positions.Length)
                    Array.Resize(ref _positions, _positions.Length * 2);

                _entityToPosIndex[eidx] = _positionCount;
                _positions[_positionCount] = default;
                _positionCount++;

                mask |= mapped;

                _cmask[eidx] = mask;
                _cmCount++;
                _ctCount++;
                return;
            case ComponentMask.Velocity:
                if (eidx >= _entityToVelIndex.Length)
                    Array.Resize(ref _entityToVelIndex, Math.Max(_entityToVelIndex.Length * 2, eidx + 1));
                if (_velocityCount >= _velocities.Length)
                    Array.Resize(ref _velocities, _velocities.Length * 2);

                _entityToVelIndex[eidx] = _velocityCount;
                _velocities[_velocityCount] = default;
                _velocityCount++;

                mask |= mapped;

                _cmask[eidx] = mask;
                _cmCount++;
                _ctCount++;
                return;
            case ComponentMask.Rotation:
                while (eidx >= _entityToRotIndex.Length)
                    Array.Resize(ref _entityToRotIndex, Math.Max(_entityToRotIndex.Length * 2, eidx + 1));
                while (_rotationCount >= _rotations.Length)
                    Array.Resize(ref _rotations, _rotations.Length * 2);

                _entityToRotIndex[eidx] = _rotationCount;
                _rotations[_rotationCount] = default;
                _rotationCount++;

                mask |= mapped;

                _cmask[eidx] = mask;
                _cmCount++;
                _ctCount++;
                return;
            case ComponentMask.Scale:
                if (eidx >= _entityToScaleIndex.Length)
                    Array.Resize(ref _entityToScaleIndex, Math.Max(_entityToScaleIndex.Length * 2, eidx + 1));
                while (_scaleCount >= _scales.Length)
                    Array.Resize(ref _scales, _scales.Length * 2);

                _entityToScaleIndex[eidx] = _scaleCount;
                _scales[_scaleCount] = default;
                _scaleCount++;

                mask |= mapped;

                _cmask[eidx] = mask;
                _cmCount++;
                _ctCount++;
                return;
            case ComponentMask.Color:
                if (eidx >= _entityToColorIndex.Length)
                    Array.Resize(ref _entityToColorIndex, Math.Max(_entityToColorIndex.Length * 2, eidx + 1));
                while (_colorCount >= _colors.Length)
                    Array.Resize(ref _colors, _colors.Length * 2);

                _entityToColorIndex[eidx] = _colorCount;
                _colors[_colorCount] = default;
                _colorCount++;

                mask |= mapped;

                _cmask[eidx] = mask;
                _cmCount++;
                _ctCount++;
                return;
            case ComponentMask.Name:
                if (eidx >= _entityToNameIndex.Length)
                    Array.Resize(ref _entityToNameIndex, Math.Max(_entityToNameIndex.Length * 2, eidx + 1));
                while (_nameCount >= _names.Length)
                    Array.Resize(ref _names, _names.Length * 2);

                _entityToNameIndex[eidx] = _nameCount;
                _names[_nameCount] = default;
                _nameCount++;

                mask |= mapped;

                _cmask[eidx] = mask;
                _cmCount++;
                _ctCount++;
                return;
            default:
                throw new Exception("Component type not supported.");
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public ref T Get<T>(int eidx) where T : unmanaged {
        ComponentMask mask = MaskOf<T>();
        if ((_cmask[eidx] & mask) == 0)
            throw new Exception("Component not present");

        if (typeof(T) == typeof(Position))
            return ref Unsafe.As<Position, T>(ref _positions[_entityToPosIndex[eidx]]);
        else if (typeof(T) == typeof(Velocity))
            return ref Unsafe.As<Velocity, T>(ref _velocities[_entityToVelIndex[eidx]]);
        else if (typeof(T) == typeof(Rotation))
            return ref Unsafe.As<Rotation, T>(ref _rotations[_entityToRotIndex[eidx]]);
        else if (typeof(T) == typeof(Scale))
            return ref Unsafe.As<Scale, T>(ref _scales[_entityToScaleIndex[eidx]]);
        else if (typeof(T) == typeof(Color))
            return ref Unsafe.As<Color, T>(ref _colors[_entityToColorIndex[eidx]]);
        else if (typeof(T) == typeof(Name))
            return ref Unsafe.As<Name, T>(ref _names[_entityToNameIndex[eidx]]);
        else
            throw new Exception("Component type not supported");
    }

}

class Program {
        static void Main(string[] args) {
            var entityManager = new Registry();
            int entityCount = 100_000000;
            int componentsPerEntity = 3;

            var entities = new List<Entity>(entityCount);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < entityCount; i++) {
                var e = new Entity();
                entities.Add(e);
                //entityManager.Add<Name>(e.Value);

                entityManager.Add<Position>(e.Value);
                entityManager.Add<Velocity>(e.Value);
                
            }
            sw.Stop();
            Console.WriteLine($"Setup {entityCount} entities with {componentsPerEntity} components each: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        for (int i = 0; i < entityCount; i++) {
            var e = entities[i];
            var pos = entityManager.Get<Position>(e.Value);
            var vel = entityManager.Get<Velocity>(e.Value);
            pos.X = i; pos.Y = i * 2;
            vel.X = i; vel.Y = i * 2;
        }
        sw.Stop();
        Console.WriteLine($"Accessed and modified Position and Velocity components: {sw.Elapsed.TotalMilliseconds:F3} ms");
    }
        
}
