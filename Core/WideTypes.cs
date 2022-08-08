namespace OkTools.Core;

// the purpose of these types is programmer convenience, not SIMD. if you want SIMD types,
// look at Unity Burst or System.Numerics.

public struct Int2 : IEquatable<Int2>
{
    public int X, Y;

    public Int2(int x, int y) =>
        (X, Y) = (x, y);
    public Int2(int v) =>
        (X, Y) = (v, v);
    public Int2((int, int) xy) =>
        (X, Y) = xy;

    public unsafe ref int this[int index]
    {
        get
        {
            if (index < 0 || index > 1)
                throw new ArgumentOutOfRangeException(nameof(index));
            fixed (int* i = &X) { return ref i[index]; }
        }
    }

    public void Deconstruct(out int x, out int y) =>
        (x, y) = (X, Y);

    public bool Equals(Int2 other) =>
        X == other.X && Y == other.Y;

    public override bool Equals(object? obj) =>
        !ReferenceEquals(obj, null) && obj is Int2 other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(X, Y);

    public static Bool2 operator ==(in Int2 left, in Int2 right) =>
        new(left.X == right.X, left.Y == right.Y);
    public static Bool2 operator !=(in Int2 left, in Int2 right) =>
        new(left.X != right.X, left.Y != right.Y);

    public override string ToString() => $"{X}, {Y}";
    public object ToDump() => ToString(); // linqpad

    static readonly Int2 k_Zero = new(0), k_One = new(1);
    static readonly Int2 k_MaxValue = new(int.MaxValue, int.MaxValue);
    static readonly Int2 k_MinValue = new(int.MinValue, int.MinValue);

    public static ref readonly Int2 Zero => ref k_Zero;
    public static ref readonly Int2 One => ref k_One;
    public static ref readonly Int2 MaxValue => ref k_MaxValue;
    public static ref readonly Int2 MinValue => ref k_MinValue;

    public bool IsZero => Equals(Zero);
    public bool IsOne  => Equals(One);

    public static Int2 operator-(in Int2 i) =>
        new(-i.X, -i.Y);

    public static Int2 operator +(in Int2 a, in Int2 b) =>
        new(a.X + b.X, a.Y + b.Y);
    public static Int2 operator +(in Int2 a, int d) =>
        new(a.X + d, a.Y + d);
    public static Int2 operator -(in Int2 a, in Int2 b) =>
        new(a.X - b.X, a.Y - b.Y);
    public static Int2 operator -(in Int2 a, int d) =>
        new(a.X - d, a.Y - d);
    public static Int2 operator *(in Int2 a, in Int2 b) =>
        new(a.X * b.X, a.Y * b.Y);
    public static Int2 operator *(in Int2 a, int d) =>
        new(a.X * d, a.Y * d);
    public static Int2 operator /(in Int2 a, in Int2 b) =>
        new(a.X / b.X, a.Y / b.Y);
    public static Int2 operator /(in Int2 a, int d) =>
        new(a.X / d, a.Y / d);

    public static Bool2 operator <(in Int2 a, in Int2 b) =>
        new(a.X < b.X, a.Y < b.Y);
    public static Bool2 operator <(in Int2 a, int b) =>
        new(a.X < b, a.Y < b);
    public static Bool2 operator <(int a, in Int2 b) =>
        new(a < b.X, a < b.Y);

    public static Bool2 operator <=(in Int2 a, in Int2 b) =>
        new(a.X <= b.X, a.Y <= b.Y);
    public static Bool2 operator <=(in Int2 a, int b) =>
        new(a.X <= b, a.Y <= b);
    public static Bool2 operator <=(int a, in Int2 b) =>
        new(a <= b.X, a <= b.Y);

    public static Bool2 operator >(in Int2 a, in Int2 b) =>
        new(a.X > b.X, a.Y > b.Y);
    public static Bool2 operator >(in Int2 a, int b) =>
        new(a.X > b, a.Y > b);
    public static Bool2 operator >(int a, in Int2 b) =>
        new(a > b.X, a > b.Y);

    public static Bool2 operator >=(in Int2 a, in Int2 b) =>
        new(a.X >= b.X, a.Y >= b.Y);
    public static Bool2 operator >=(in Int2 a, int b) =>
        new(a.X >= b, a.Y >= b);
    public static Bool2 operator >=(int a, in Int2 b) =>
        new(a >= b.X, a >= b.Y);
}

public struct Int3 : IEquatable<Int3>
{
    public int X, Y, Z;

    public Int3(int x, int y, int z) =>
        (X, Y, Z) = (x, y, z);
    public Int3(int v) =>
        (X, Y, Z) = (v, v, v);
    public Int3((int, int, int) xyz) =>
        (X, Y, Z) = xyz;

    public unsafe ref int this[int index]
    {
        get
        {
            if (index < 0 || index > 2)
                throw new ArgumentOutOfRangeException(nameof(index));
            fixed (int* i = &X) { return ref i[index]; }
        }
    }

    public void Deconstruct(out int x, out int y, out int z) =>
        (x, y, z) = (X, Y, Z);

    public bool Equals(Int3 other) =>
        X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) =>
        !ReferenceEquals(obj, null) && obj is Int3 other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(X, Y, Z);

    public static Bool3 operator ==(in Int3 left, in Int3 right) =>
        new(left.X == right.X, left.Y == right.Y, left.Z == right.Z);
    public static Bool3 operator !=(in Int3 left, in Int3 right) =>
        new(left.X != right.X, left.Y != right.Y, left.Z != right.Z);

    public override string ToString() => $"{X}, {Y}, {Z}";
    public object ToDump() => ToString(); // linqpad

    static readonly Int3 k_Zero = new(0), k_One = new(1);
    static readonly Int3 k_MaxValue = new(int.MaxValue, int.MaxValue, int.MaxValue);
    static readonly Int3 k_MinValue = new(int.MinValue, int.MinValue, int.MinValue);

    public static ref readonly Int3 Zero => ref k_Zero;
    public static ref readonly Int3 One => ref k_One;
    public static ref readonly Int3 MaxValue => ref k_MaxValue;
    public static ref readonly Int3 MinValue => ref k_MinValue;

    public bool IsZero => Equals(Zero);
    public bool IsOne  => Equals(One);

    public static Int3 operator-(in Int3 i) =>
        new(-i.X, -i.Y, -i.Z);

    public static Int3 operator +(in Int3 a, in Int3 b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Int3 operator +(in Int3 a, int d) =>
        new(a.X + d, a.Y + d, a.Z + d);
    public static Int3 operator -(in Int3 a, in Int3 b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Int3 operator -(in Int3 a, int d) =>
        new(a.X - d, a.Y - d, a.Z - d);
    public static Int3 operator *(in Int3 a, in Int3 b) =>
        new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    public static Int3 operator *(in Int3 a, int d) =>
        new(a.X * d, a.Y * d, a.Z * d);
    public static Int3 operator /(in Int3 a, in Int3 b) =>
        new(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
    public static Int3 operator /(in Int3 a, int d) =>
        new(a.X / d, a.Y / d, a.Z / d);

    public static Bool3 operator <(in Int3 a, in Int3 b) =>
        new(a.X < b.X, a.Y < b.Y, a.Z < b.Z);
    public static Bool3 operator <(in Int3 a, int b) =>
        new(a.X < b, a.Y < b, a.Z < b);
    public static Bool3 operator <(int a, in Int3 b) =>
        new(a < b.X, a < b.Y, a < b.Z);

    public static Bool3 operator <=(in Int3 a, in Int3 b) =>
        new(a.X <= b.X, a.Y <= b.Y, a.Z <= b.Z);
    public static Bool3 operator <=(in Int3 a, int b) =>
        new(a.X <= b, a.Y <= b, a.Z <= b);
    public static Bool3 operator <=(int a, in Int3 b) =>
        new(a <= b.X, a <= b.Y, a <= b.Z);

    public static Bool3 operator >(in Int3 a, in Int3 b) =>
        new(a.X > b.X, a.Y > b.Y, a.Z > b.Z);
    public static Bool3 operator >(in Int3 a, int b) =>
        new(a.X > b, a.Y > b, a.Z > b);
    public static Bool3 operator >(int a, in Int3 b) =>
        new(a > b.X, a > b.Y, a > b.Z);

    public static Bool3 operator >=(in Int3 a, in Int3 b) =>
        new(a.X >= b.X, a.Y >= b.Y, a.Z >= b.Z);
    public static Bool3 operator >=(in Int3 a, int b) =>
        new(a.X >= b, a.Y >= b, a.Z >= b);
    public static Bool3 operator >=(int a, in Int3 b) =>
        new(a >= b.X, a >= b.Y, a >= b.Z);
}

public struct Int4 : IEquatable<Int4>
{
    public int X, Y, Z, W;

    public Int4(int x, int y, int z, int w) =>
        (X, Y, Z, W) = (x, y, z, w);
    public Int4(int v) =>
        (X, Y, Z, W) = (v, v, v, v);
    public Int4((int, int, int, int) xyzw) =>
        (X, Y, Z, W) = xyzw;

    public unsafe ref int this[int index]
    {
        get
        {
            if (index < 0 || index > 3)
                throw new ArgumentOutOfRangeException(nameof(index));
            fixed (int* i = &X) { return ref i[index]; }
        }
    }

    public void Deconstruct(out int x, out int y, out int z, out int w) =>
        (x, y, z, w) = (X, Y, Z, W);

    public bool Equals(Int4 other) =>
        X == other.X && Y == other.Y && Z == other.Z && W == other.W;

    public override bool Equals(object? obj) =>
        !ReferenceEquals(obj, null) && obj is Int4 other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(X, Y, Z, W);

    public static Bool4 operator ==(in Int4 left, in Int4 right) =>
        new(left.X == right.X, left.Y == right.Y, left.Z == right.Z, left.W == right.W);
    public static Bool4 operator !=(in Int4 left, in Int4 right) =>
        new(left.X != right.X, left.Y != right.Y, left.Z != right.Z, left.W != right.W);

    public override string ToString() => $"{X}, {Y}, {Z}, {W}";
    public object ToDump() => ToString(); // linqpad

    static readonly Int4 k_Zero = new(0), k_One = new(1);
    static readonly Int4 k_MaxValue = new(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
    static readonly Int4 k_MinValue = new(int.MinValue, int.MinValue, int.MinValue, int.MinValue);

    public static ref readonly Int4 Zero => ref k_Zero;
    public static ref readonly Int4 One => ref k_One;
    public static ref readonly Int4 MaxValue => ref k_MaxValue;
    public static ref readonly Int4 MinValue => ref k_MinValue;

    public bool IsZero => Equals(Zero);
    public bool IsOne  => Equals(One);

    public static Int4 operator-(in Int4 i) =>
        new(-i.X, -i.Y, -i.Z, -i.W);

    public static Int4 operator +(in Int4 a, in Int4 b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
    public static Int4 operator +(in Int4 a, int d) =>
        new(a.X + d, a.Y + d, a.Z + d, a.W + d);
    public static Int4 operator -(in Int4 a, in Int4 b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);
    public static Int4 operator -(in Int4 a, int d) =>
        new(a.X - d, a.Y - d, a.Z - d, a.W - d);
    public static Int4 operator *(in Int4 a, in Int4 b) =>
        new(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W);
    public static Int4 operator *(in Int4 a, int d) =>
        new(a.X * d, a.Y * d, a.Z * d, a.W * d);
    public static Int4 operator /(in Int4 a, in Int4 b) =>
        new(a.X / b.X, a.Y / b.Y, a.Z / b.Z, a.W / b.W);
    public static Int4 operator /(in Int4 a, int d) =>
        new(a.X / d, a.Y / d, a.Z / d, a.W / d);

    public static Bool4 operator <(in Int4 a, in Int4 b) =>
        new(a.X < b.X, a.Y < b.Y, a.Z < b.Z, a.W < b.W);
    public static Bool4 operator <(in Int4 a, int b) =>
        new(a.X < b, a.Y < b, a.Z < b, a.W < b);
    public static Bool4 operator <(int a, in Int4 b) =>
        new(a < b.X, a < b.Y, a < b.Z, a < b.W);

    public static Bool4 operator <=(in Int4 a, in Int4 b) =>
        new(a.X <= b.X, a.Y <= b.Y, a.Z <= b.Z, a.W <= b.W);
    public static Bool4 operator <=(in Int4 a, int b) =>
        new(a.X <= b, a.Y <= b, a.Z <= b, a.W <= b);
    public static Bool4 operator <=(int a, in Int4 b) =>
        new(a <= b.X, a <= b.Y, a <= b.Z, a <= b.W);

    public static Bool4 operator >(in Int4 a, in Int4 b) =>
        new(a.X > b.X, a.Y > b.Y, a.Z > b.Z, a.W > b.W);
    public static Bool4 operator >(in Int4 a, int b) =>
        new(a.X > b, a.Y > b, a.Z > b, a.W > b);
    public static Bool4 operator >(int a, in Int4 b) =>
        new(a > b.X, a > b.Y, a > b.Z, a > b.W);

    public static Bool4 operator >=(in Int4 a, in Int4 b) =>
        new(a.X >= b.X, a.Y >= b.Y, a.Z >= b.Z, a.W >= b.W);
    public static Bool4 operator >=(in Int4 a, int b) =>
        new(a.X >= b, a.Y >= b, a.Z >= b, a.W >= b);
    public static Bool4 operator >=(int a, in Int4 b) =>
        new(a >= b.X, a >= b.Y, a >= b.Z, a >= b.W);
}

public struct Long2 : IEquatable<Long2>
{
    public long X, Y;

    public Long2(long x, long y) =>
        (X, Y) = (x, y);
    public Long2(long v) =>
        (X, Y) = (v, v);
    public Long2((long, long) xy) =>
        (X, Y) = xy;

    public unsafe ref long this[int index]
    {
        get
        {
            if (index < 0 || index > 1)
                throw new ArgumentOutOfRangeException(nameof(index));
            fixed (long* i = &X) { return ref i[index]; }
        }
    }

    public void Deconstruct(out long x, out long y) =>
        (x, y) = (X, Y);

    public bool Equals(Long2 other) =>
        X == other.X && Y == other.Y;

    public override bool Equals(object? obj) =>
        !ReferenceEquals(obj, null) && obj is Long2 other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(X, Y);

    public static Bool2 operator ==(in Long2 left, in Long2 right) =>
        new(left.X == right.X, left.Y == right.Y);
    public static Bool2 operator !=(in Long2 left, in Long2 right) =>
        new(left.X != right.X, left.Y != right.Y);

    public override string ToString() => $"{X}, {Y}";
    public object ToDump() => ToString(); // linqpad

    static readonly Long2 k_Zero = new(0), k_One = new(1);
    static readonly Long2 k_MaxValue = new(long.MaxValue, long.MaxValue);
    static readonly Long2 k_MinValue = new(long.MinValue, long.MinValue);

    public static ref readonly Long2 Zero => ref k_Zero;
    public static ref readonly Long2 One => ref k_One;
    public static ref readonly Long2 MaxValue => ref k_MaxValue;
    public static ref readonly Long2 MinValue => ref k_MinValue;

    public bool IsZero => Equals(Zero);
    public bool IsOne  => Equals(One);

    public static Long2 operator-(in Long2 i) =>
        new(-i.X, -i.Y);

    public static Long2 operator +(in Long2 a, in Long2 b) =>
        new(a.X + b.X, a.Y + b.Y);
    public static Long2 operator +(in Long2 a, long d) =>
        new(a.X + d, a.Y + d);
    public static Long2 operator -(in Long2 a, in Long2 b) =>
        new(a.X - b.X, a.Y - b.Y);
    public static Long2 operator -(in Long2 a, long d) =>
        new(a.X - d, a.Y - d);
    public static Long2 operator *(in Long2 a, in Long2 b) =>
        new(a.X * b.X, a.Y * b.Y);
    public static Long2 operator *(in Long2 a, long d) =>
        new(a.X * d, a.Y * d);
    public static Long2 operator /(in Long2 a, in Long2 b) =>
        new(a.X / b.X, a.Y / b.Y);
    public static Long2 operator /(in Long2 a, long d) =>
        new(a.X / d, a.Y / d);

    public static Bool2 operator <(in Long2 a, in Long2 b) =>
        new(a.X < b.X, a.Y < b.Y);
    public static Bool2 operator <(in Long2 a, long b) =>
        new(a.X < b, a.Y < b);
    public static Bool2 operator <(long a, in Long2 b) =>
        new(a < b.X, a < b.Y);

    public static Bool2 operator <=(in Long2 a, in Long2 b) =>
        new(a.X <= b.X, a.Y <= b.Y);
    public static Bool2 operator <=(in Long2 a, long b) =>
        new(a.X <= b, a.Y <= b);
    public static Bool2 operator <=(long a, in Long2 b) =>
        new(a <= b.X, a <= b.Y);

    public static Bool2 operator >(in Long2 a, in Long2 b) =>
        new(a.X > b.X, a.Y > b.Y);
    public static Bool2 operator >(in Long2 a, long b) =>
        new(a.X > b, a.Y > b);
    public static Bool2 operator >(long a, in Long2 b) =>
        new(a > b.X, a > b.Y);

    public static Bool2 operator >=(in Long2 a, in Long2 b) =>
        new(a.X >= b.X, a.Y >= b.Y);
    public static Bool2 operator >=(in Long2 a, long b) =>
        new(a.X >= b, a.Y >= b);
    public static Bool2 operator >=(long a, in Long2 b) =>
        new(a >= b.X, a >= b.Y);
}

public struct Long3 : IEquatable<Long3>
{
    public long X, Y, Z;

    public Long3(long x, long y, long z) =>
        (X, Y, Z) = (x, y, z);
    public Long3(long v) =>
        (X, Y, Z) = (v, v, v);
    public Long3((long, long, long) xyz) =>
        (X, Y, Z) = xyz;

    public unsafe ref long this[int index]
    {
        get
        {
            if (index < 0 || index > 2)
                throw new ArgumentOutOfRangeException(nameof(index));
            fixed (long* i = &X) { return ref i[index]; }
        }
    }

    public void Deconstruct(out long x, out long y, out long z) =>
        (x, y, z) = (X, Y, Z);

    public bool Equals(Long3 other) =>
        X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) =>
        !ReferenceEquals(obj, null) && obj is Long3 other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(X, Y, Z);

    public static Bool3 operator ==(in Long3 left, in Long3 right) =>
        new(left.X == right.X, left.Y == right.Y, left.Z == right.Z);
    public static Bool3 operator !=(in Long3 left, in Long3 right) =>
        new(left.X != right.X, left.Y != right.Y, left.Z != right.Z);

    public override string ToString() => $"{X}, {Y}, {Z}";
    public object ToDump() => ToString(); // linqpad

    static readonly Long3 k_Zero = new(0), k_One = new(1);
    static readonly Long3 k_MaxValue = new(long.MaxValue, long.MaxValue, long.MaxValue);
    static readonly Long3 k_MinValue = new(long.MinValue, long.MinValue, long.MinValue);

    public static ref readonly Long3 Zero => ref k_Zero;
    public static ref readonly Long3 One => ref k_One;
    public static ref readonly Long3 MaxValue => ref k_MaxValue;
    public static ref readonly Long3 MinValue => ref k_MinValue;

    public bool IsZero => Equals(Zero);
    public bool IsOne  => Equals(One);

    public static Long3 operator-(in Long3 i) =>
        new(-i.X, -i.Y, -i.Z);

    public static Long3 operator +(in Long3 a, in Long3 b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Long3 operator +(in Long3 a, long d) =>
        new(a.X + d, a.Y + d, a.Z + d);
    public static Long3 operator -(in Long3 a, in Long3 b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Long3 operator -(in Long3 a, long d) =>
        new(a.X - d, a.Y - d, a.Z - d);
    public static Long3 operator *(in Long3 a, in Long3 b) =>
        new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    public static Long3 operator *(in Long3 a, long d) =>
        new(a.X * d, a.Y * d, a.Z * d);
    public static Long3 operator /(in Long3 a, in Long3 b) =>
        new(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
    public static Long3 operator /(in Long3 a, long d) =>
        new(a.X / d, a.Y / d, a.Z / d);

    public static Bool3 operator <(in Long3 a, in Long3 b) =>
        new(a.X < b.X, a.Y < b.Y, a.Z < b.Z);
    public static Bool3 operator <(in Long3 a, long b) =>
        new(a.X < b, a.Y < b, a.Z < b);
    public static Bool3 operator <(long a, in Long3 b) =>
        new(a < b.X, a < b.Y, a < b.Z);

    public static Bool3 operator <=(in Long3 a, in Long3 b) =>
        new(a.X <= b.X, a.Y <= b.Y, a.Z <= b.Z);
    public static Bool3 operator <=(in Long3 a, long b) =>
        new(a.X <= b, a.Y <= b, a.Z <= b);
    public static Bool3 operator <=(long a, in Long3 b) =>
        new(a <= b.X, a <= b.Y, a <= b.Z);

    public static Bool3 operator >(in Long3 a, in Long3 b) =>
        new(a.X > b.X, a.Y > b.Y, a.Z > b.Z);
    public static Bool3 operator >(in Long3 a, long b) =>
        new(a.X > b, a.Y > b, a.Z > b);
    public static Bool3 operator >(long a, in Long3 b) =>
        new(a > b.X, a > b.Y, a > b.Z);

    public static Bool3 operator >=(in Long3 a, in Long3 b) =>
        new(a.X >= b.X, a.Y >= b.Y, a.Z >= b.Z);
    public static Bool3 operator >=(in Long3 a, long b) =>
        new(a.X >= b, a.Y >= b, a.Z >= b);
    public static Bool3 operator >=(long a, in Long3 b) =>
        new(a >= b.X, a >= b.Y, a >= b.Z);
}

public struct Long4 : IEquatable<Long4>
{
    public long X, Y, Z, W;

    public Long4(long x, long y, long z, long w) =>
        (X, Y, Z, W) = (x, y, z, w);
    public Long4(long v) =>
        (X, Y, Z, W) = (v, v, v, v);
    public Long4((long, long, long, long) xyzw) =>
        (X, Y, Z, W) = xyzw;

    public unsafe ref long this[int index]
    {
        get
        {
            if (index < 0 || index > 3)
                throw new ArgumentOutOfRangeException(nameof(index));
            fixed (long* i = &X) { return ref i[index]; }
        }
    }

    public void Deconstruct(out long x, out long y, out long z, out long w) =>
        (x, y, z, w) = (X, Y, Z, W);

    public bool Equals(Long4 other) =>
        X == other.X && Y == other.Y && Z == other.Z && W == other.W;

    public override bool Equals(object? obj) =>
        !ReferenceEquals(obj, null) && obj is Long4 other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(X, Y, Z, W);

    public static Bool4 operator ==(in Long4 left, in Long4 right) =>
        new(left.X == right.X, left.Y == right.Y, left.Z == right.Z, left.W == right.W);
    public static Bool4 operator !=(in Long4 left, in Long4 right) =>
        new(left.X != right.X, left.Y != right.Y, left.Z != right.Z, left.W != right.W);

    public override string ToString() => $"{X}, {Y}, {Z}, {W}";
    public object ToDump() => ToString(); // linqpad

    static readonly Long4 k_Zero = new(0), k_One = new(1);
    static readonly Long4 k_MaxValue = new(long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue);
    static readonly Long4 k_MinValue = new(long.MinValue, long.MinValue, long.MinValue, long.MinValue);

    public static ref readonly Long4 Zero => ref k_Zero;
    public static ref readonly Long4 One => ref k_One;
    public static ref readonly Long4 MaxValue => ref k_MaxValue;
    public static ref readonly Long4 MinValue => ref k_MinValue;

    public bool IsZero => Equals(Zero);
    public bool IsOne  => Equals(One);

    public static Long4 operator-(in Long4 i) =>
        new(-i.X, -i.Y, -i.Z, -i.W);

    public static Long4 operator +(in Long4 a, in Long4 b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
    public static Long4 operator +(in Long4 a, long d) =>
        new(a.X + d, a.Y + d, a.Z + d, a.W + d);
    public static Long4 operator -(in Long4 a, in Long4 b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);
    public static Long4 operator -(in Long4 a, long d) =>
        new(a.X - d, a.Y - d, a.Z - d, a.W - d);
    public static Long4 operator *(in Long4 a, in Long4 b) =>
        new(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W);
    public static Long4 operator *(in Long4 a, long d) =>
        new(a.X * d, a.Y * d, a.Z * d, a.W * d);
    public static Long4 operator /(in Long4 a, in Long4 b) =>
        new(a.X / b.X, a.Y / b.Y, a.Z / b.Z, a.W / b.W);
    public static Long4 operator /(in Long4 a, long d) =>
        new(a.X / d, a.Y / d, a.Z / d, a.W / d);

    public static Bool4 operator <(in Long4 a, in Long4 b) =>
        new(a.X < b.X, a.Y < b.Y, a.Z < b.Z, a.W < b.W);
    public static Bool4 operator <(in Long4 a, long b) =>
        new(a.X < b, a.Y < b, a.Z < b, a.W < b);
    public static Bool4 operator <(long a, in Long4 b) =>
        new(a < b.X, a < b.Y, a < b.Z, a < b.W);

    public static Bool4 operator <=(in Long4 a, in Long4 b) =>
        new(a.X <= b.X, a.Y <= b.Y, a.Z <= b.Z, a.W <= b.W);
    public static Bool4 operator <=(in Long4 a, long b) =>
        new(a.X <= b, a.Y <= b, a.Z <= b, a.W <= b);
    public static Bool4 operator <=(long a, in Long4 b) =>
        new(a <= b.X, a <= b.Y, a <= b.Z, a <= b.W);

    public static Bool4 operator >(in Long4 a, in Long4 b) =>
        new(a.X > b.X, a.Y > b.Y, a.Z > b.Z, a.W > b.W);
    public static Bool4 operator >(in Long4 a, long b) =>
        new(a.X > b, a.Y > b, a.Z > b, a.W > b);
    public static Bool4 operator >(long a, in Long4 b) =>
        new(a > b.X, a > b.Y, a > b.Z, a > b.W);

    public static Bool4 operator >=(in Long4 a, in Long4 b) =>
        new(a.X >= b.X, a.Y >= b.Y, a.Z >= b.Z, a.W >= b.W);
    public static Bool4 operator >=(in Long4 a, long b) =>
        new(a.X >= b, a.Y >= b, a.Z >= b, a.W >= b);
    public static Bool4 operator >=(long a, in Long4 b) =>
        new(a >= b.X, a >= b.Y, a >= b.Z, a >= b.W);
}

public struct Bool2 : IEquatable<Bool2>
{
    public bool X, Y;

    public Bool2(bool x, bool y) =>
        (X, Y) = (x, y);
    public Bool2(bool v) =>
        (X, Y) = (v, v);
    public Bool2((bool, bool) xy) =>
        (X, Y) = xy;

    public unsafe ref bool this[int index]
    {
        get
        {
            if (index < 0 || index > 1)
                throw new ArgumentOutOfRangeException(nameof(index));
            fixed (bool* i = &X) { return ref i[index]; }
        }
    }

    public bool Equals(Bool2 other) =>
        X == other.X && Y == other.Y;

    public override bool Equals(object? obj) =>
        !ReferenceEquals(obj, null) && obj is Bool2 other && Equals(other);

    public override int GetHashCode() =>
        (X?1:0) << 0 | (Y?1:0) << 1;

    public static bool operator ==(in Bool2 left, in Bool2 right) => left.Equals(right);
    public static bool operator !=(in Bool2 left, in Bool2 right) => !left.Equals(right);

    public static Bool2 operator |(in Bool2 left, in Bool2 right) =>
        new(left.X || right.X, left.Y || right.Y);
    public static Bool2 operator &(in Bool2 left, in Bool2 right) =>
        new(left.X && right.X, left.Y && right.Y);

    public override string ToString() => $"{X}, {Y}";
    public object ToDump() => ToString(); // linqpad

    static readonly Bool2 k_False = new(false), k_True = new(true);

    public static ref readonly Bool2 False => ref k_False;
    public static ref readonly Bool2 True => ref k_True;

    public bool All() => X && Y;
    public bool Any() => X || Y;
}

public struct Bool3 : IEquatable<Bool3>
{
    public bool X, Y, Z;

    public Bool3(bool x, bool y, bool z) =>
        (X, Y, Z) = (x, y, z);
    public Bool3(bool v) =>
        (X, Y, Z) = (v, v, v);
    public Bool3((bool, bool, bool) xyz) =>
        (X, Y, Z) = xyz;

    public unsafe ref bool this[int index]
    {
        get
        {
            if (index < 0 || index > 2)
                throw new ArgumentOutOfRangeException(nameof(index));
            fixed (bool* i = &X) { return ref i[index]; }
        }
    }

    public bool Equals(Bool3 other) =>
        X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) =>
        !ReferenceEquals(obj, null) && obj is Bool3 other && Equals(other);

    public override int GetHashCode() =>
        (X?1:0) << 0 | (Y?1:0) << 1 | (Z?1:0) << 2;

    public static bool operator ==(in Bool3 left, in Bool3 right) => left.Equals(right);
    public static bool operator !=(in Bool3 left, in Bool3 right) => !left.Equals(right);

    public static Bool3 operator |(in Bool3 left, in Bool3 right) =>
        new(left.X || right.X, left.Y || right.Y, left.Z || right.Z);
    public static Bool3 operator &(in Bool3 left, in Bool3 right) =>
        new(left.X && right.X, left.Y && right.Y, left.Z && right.Z);

    public override string ToString() => $"{X}, {Y}, {Z}";
    public object ToDump() => ToString(); // linqpad

    static readonly Bool3 k_False = new(false), k_True = new(true);

    public static ref readonly Bool3 False => ref k_False;
    public static ref readonly Bool3 True => ref k_True;

    public bool All() => X && Y && Z;
    public bool Any() => X || Y || Z;
}

public struct Bool4 : IEquatable<Bool4>
{
    public bool X, Y, Z, W;

    public Bool4(bool x, bool y, bool z, bool w) =>
        (X, Y, Z, W) = (x, y, z, w);
    public Bool4(bool v) =>
        (X, Y, Z, W) = (v, v, v, v);
    public Bool4((bool, bool, bool, bool) xyzw) =>
        (X, Y, Z, W) = xyzw;

    public unsafe ref bool this[int index]
    {
        get
        {
            if (index < 0 || index > 3)
                throw new ArgumentOutOfRangeException(nameof(index));
            fixed (bool* i = &X) { return ref i[index]; }
        }
    }

    public bool Equals(Bool4 other) =>
        X == other.X && Y == other.Y && Z == other.Z && W == other.W;

    public override bool Equals(object? obj) =>
        !ReferenceEquals(obj, null) && obj is Bool4 other && Equals(other);

    public override int GetHashCode() =>
        (X?1:0) << 0 | (Y?1:0) << 1 | (Z?1:0) << 2 | (W?1:0) << 3;

    public static bool operator ==(in Bool4 left, in Bool4 right) => left.Equals(right);
    public static bool operator !=(in Bool4 left, in Bool4 right) => !left.Equals(right);

    public static Bool4 operator |(in Bool4 left, in Bool4 right) =>
        new(left.X || right.X, left.Y || right.Y, left.Z || right.Z, left.W || right.W);
    public static Bool4 operator &(in Bool4 left, in Bool4 right) =>
        new(left.X && right.X, left.Y && right.Y, left.Z && right.Z, left.W && right.W);

    public override string ToString() => $"{X}, {Y}, {Z}, {W}";
    public object ToDump() => ToString(); // linqpad

    static readonly Bool4 k_False = new(false), k_True = new(true);

    public static ref readonly Bool4 False => ref k_False;
    public static ref readonly Bool4 True => ref k_True;

    public bool All() => X && Y && Z && W;
    public bool Any() => X || Y || Z || W;
}

public static partial class Utils
{
    public static Int2 Abs(in Int2 a) =>
        new(Math.Abs(a.X), Math.Abs(a.Y));
    public static Int3 Abs(in Int3 a) =>
        new(Math.Abs(a.X), Math.Abs(a.Y), Math.Abs(a.Z));
    public static Int4 Abs(in Int4 a) =>
        new(Math.Abs(a.X), Math.Abs(a.Y), Math.Abs(a.Z), Math.Abs(a.W));

    public static int LengthSq(in Int2 a) =>
         a.X * a.X + a.Y * a.Y;
    public static int LengthSq(in Int2 a, in Int2 b) =>
         (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
    public static int LengthSq(in Int3 a) =>
         a.X * a.X + a.Y * a.Y + a.Z * a.Z;
    public static int LengthSq(in Int3 a, in Int3 b) =>
         (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z);
    public static int LengthSq(in Int4 a) =>
         a.X * a.X + a.Y * a.Y + a.Z * a.Z + a.W * a.W;
    public static int LengthSq(in Int4 a, in Int4 b) =>
         (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z) + (a.W - b.W) * (a.W - b.W);

    public static Int2 Min(in Int2 a, in Int2 b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y));
    public static Int2 Max(in Int2 a, in Int2 b) =>
        new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    public static Int3 Min(in Int3 a, in Int3 b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
    public static Int3 Max(in Int3 a, in Int3 b) =>
        new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
    public static Int4 Min(in Int4 a, in Int4 b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z), Math.Min(a.W, b.W));
    public static Int4 Max(in Int4 a, in Int4 b) =>
        new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z), Math.Max(a.W, b.W));

    public static void Minimize(ref Int2 a, in Int2 b) => a = Min(in a, in b);
    public static void Maximize(ref Int2 a, in Int2 b) => a = Max(in a, in b);
    public static void Minimize(ref Int3 a, in Int3 b) => a = Min(in a, in b);
    public static void Maximize(ref Int3 a, in Int3 b) => a = Max(in a, in b);
    public static void Minimize(ref Int4 a, in Int4 b) => a = Min(in a, in b);
    public static void Maximize(ref Int4 a, in Int4 b) => a = Max(in a, in b);

    public static Int2 Midpoint(in Int2 a, in Int2 b) =>
        new(a.X + (b.X - a.X) / 2, a.Y + (b.Y - a.Y) / 2);
    public static Int2 Midpoint(in Int2 a) =>
        Midpoint(a, Int2.Zero);
    public static Int3 Midpoint(in Int3 a, in Int3 b) =>
        new(a.X + (b.X - a.X) / 2, a.Y + (b.Y - a.Y) / 2, a.Z + (b.Z - a.Z) / 2);
    public static Int3 Midpoint(in Int3 a) =>
        Midpoint(a, Int3.Zero);
    public static Int4 Midpoint(in Int4 a, in Int4 b) =>
        new(a.X + (b.X - a.X) / 2, a.Y + (b.Y - a.Y) / 2, a.Z + (b.Z - a.Z) / 2, a.W + (b.W - a.W) / 2);
    public static Int4 Midpoint(in Int4 a) =>
        Midpoint(a, Int4.Zero);
}

public static partial class Extensions
{
    public static Int2 Abs(this in Int2 a) => Utils.Abs(a);
    public static Int3 Abs(this in Int3 a) => Utils.Abs(a);
    public static Int4 Abs(this in Int4 a) => Utils.Abs(a);

    public static int LengthSq(this in Int2 a) => Utils.LengthSq(a);
    public static int LengthSq(this in Int2 a, in Int2 b) => Utils.LengthSq(a, b);
    public static int LengthSq(this in Int3 a) => Utils.LengthSq(a);
    public static int LengthSq(this in Int3 a, in Int3 b) => Utils.LengthSq(a, b);
    public static int LengthSq(this in Int4 a) => Utils.LengthSq(a);
    public static int LengthSq(this in Int4 a, in Int4 b) => Utils.LengthSq(a, b);

    public static Int2 Min(this in Int2 a, in Int2 b) => Utils.Min(in a, in b);
    public static Int2 Max(this in Int2 a, in Int2 b) => Utils.Max(in a, in b);
    public static Int3 Min(this in Int3 a, in Int3 b) => Utils.Min(in a, in b);
    public static Int3 Max(this in Int3 a, in Int3 b) => Utils.Max(in a, in b);
    public static Int4 Min(this in Int4 a, in Int4 b) => Utils.Min(in a, in b);
    public static Int4 Max(this in Int4 a, in Int4 b) => Utils.Max(in a, in b);

    public static void Minimize(ref this Int2 a, in Int2 b) => Utils.Minimize(ref a, in b);
    public static void Maximize(ref this Int2 a, in Int2 b) => Utils.Maximize(ref a, in b);
    public static void Minimize(ref this Int3 a, in Int3 b) => Utils.Minimize(ref a, in b);
    public static void Maximize(ref this Int3 a, in Int3 b) => Utils.Maximize(ref a, in b);
    public static void Minimize(ref this Int4 a, in Int4 b) => Utils.Minimize(ref a, in b);
    public static void Maximize(ref this Int4 a, in Int4 b) => Utils.Maximize(ref a, in b);

    public static Int2 Midpoint(this in Int2 a, in Int2 b) => Utils.Midpoint(in a, in b);
    public static Int2 Midpoint(this in Int2 a) => Utils.Midpoint(in a);
    public static Int3 Midpoint(this in Int3 a, in Int3 b) => Utils.Midpoint(in a, in b);
    public static Int3 Midpoint(this in Int3 a) => Utils.Midpoint(in a);
    public static Int4 Midpoint(this in Int4 a, in Int4 b) => Utils.Midpoint(in a, in b);
    public static Int4 Midpoint(this in Int4 a) => Utils.Midpoint(in a);
}
