using System.Numerics;
using System.Text;

namespace LksBrothers.Core.Primitives;

/// <summary>
/// Represents a 256-bit unsigned integer for blockchain operations
/// </summary>
public readonly struct UInt256 : IEquatable<UInt256>, IComparable<UInt256>
{
    public const int SIZE = 32; // 256 bits = 32 bytes
    
    private readonly BigInteger _value;
    
    public UInt256(BigInteger value)
    {
        if (value < 0)
            throw new ArgumentException("UInt256 cannot be negative");
        
        if (value >= BigInteger.Pow(2, 256))
            throw new ArgumentException("Value exceeds 256-bit range");
        
        _value = value;
    }
    
    public UInt256(byte[] bytes)
    {
        if (bytes.Length > SIZE)
            throw new ArgumentException($"Byte array cannot exceed {SIZE} bytes");
        
        // Ensure big-endian interpretation and pad if necessary
        var paddedBytes = new byte[SIZE + 1]; // +1 to ensure positive BigInteger
        Array.Copy(bytes, 0, paddedBytes, SIZE - bytes.Length + 1, bytes.Length);
        
        _value = new BigInteger(paddedBytes, isUnsigned: true, isBigEndian: true);
    }
    
    public UInt256(ReadOnlySpan<byte> bytes) : this(bytes.ToArray()) { }
    
    public UInt256(ulong value) : this(new BigInteger(value)) { }
    
    public UInt256(string hex) : this(ParseHex(hex)) { }
    
    public static UInt256 Zero => new(BigInteger.Zero);
    public static UInt256 One => new(BigInteger.One);
    public static UInt256 MaxValue => new(BigInteger.Pow(2, 256) - 1);
    
    private static byte[] ParseHex(string hex)
    {
        if (hex.StartsWith("0x"))
            hex = hex[2..];
        
        if (hex.Length % 2 != 0)
            hex = "0" + hex;
        
        if (hex.Length > SIZE * 2)
            throw new ArgumentException($"Hex string too long for UInt256");
        
        return Convert.FromHexString(hex);
    }
    
    public byte[] ToByteArray()
    {
        var bytes = _value.ToByteArray(isUnsigned: true, isBigEndian: true);
        
        if (bytes.Length > SIZE)
            throw new InvalidOperationException("Internal error: value exceeds 256 bits");
        
        if (bytes.Length == SIZE)
            return bytes;
        
        // Pad with leading zeros
        var result = new byte[SIZE];
        Array.Copy(bytes, 0, result, SIZE - bytes.Length, bytes.Length);
        return result;
    }
    
    public override string ToString() => "0x" + Convert.ToHexString(ToByteArray()).ToLowerInvariant();
    
    public string ToString(string format)
    {
        return format.ToLower() switch
        {
            "x" or "hex" => ToString(),
            "d" or "dec" => _value.ToString(),
            _ => ToString()
        };
    }
    
    public bool IsZero => _value.IsZero;
    public bool IsOne => _value.IsOne;
    
    // Arithmetic operations
    public static UInt256 operator +(UInt256 left, UInt256 right)
    {
        var result = left._value + right._value;
        if (result >= BigInteger.Pow(2, 256))
            throw new OverflowException("Addition overflow");
        return new UInt256(result);
    }
    
    public static UInt256 operator -(UInt256 left, UInt256 right)
    {
        if (left._value < right._value)
            throw new OverflowException("Subtraction underflow");
        return new UInt256(left._value - right._value);
    }
    
    public static UInt256 operator *(UInt256 left, UInt256 right)
    {
        var result = left._value * right._value;
        if (result >= BigInteger.Pow(2, 256))
            throw new OverflowException("Multiplication overflow");
        return new UInt256(result);
    }
    
    public static UInt256 operator /(UInt256 left, UInt256 right)
    {
        if (right._value.IsZero)
            throw new DivideByZeroException();
        return new UInt256(left._value / right._value);
    }
    
    public static UInt256 operator %(UInt256 left, UInt256 right)
    {
        if (right._value.IsZero)
            throw new DivideByZeroException();
        return new UInt256(left._value % right._value);
    }
    
    // Bitwise operations
    public static UInt256 operator &(UInt256 left, UInt256 right) => new(left._value & right._value);
    public static UInt256 operator |(UInt256 left, UInt256 right) => new(left._value | right._value);
    public static UInt256 operator ^(UInt256 left, UInt256 right) => new(left._value ^ right._value);
    public static UInt256 operator ~(UInt256 value) => new(~value._value & (BigInteger.Pow(2, 256) - 1));
    
    public static UInt256 operator <<(UInt256 value, int shift)
    {
        if (shift < 0) throw new ArgumentException("Shift cannot be negative");
        if (shift >= 256) return Zero;
        
        var result = value._value << shift;
        if (result >= BigInteger.Pow(2, 256))
            throw new OverflowException("Left shift overflow");
        return new UInt256(result);
    }
    
    public static UInt256 operator >>(UInt256 value, int shift)
    {
        if (shift < 0) throw new ArgumentException("Shift cannot be negative");
        if (shift >= 256) return Zero;
        
        return new UInt256(value._value >> shift);
    }
    
    // Comparison operations
    public bool Equals(UInt256 other) => _value == other._value;
    public override bool Equals(object? obj) => obj is UInt256 other && Equals(other);
    public override int GetHashCode() => _value.GetHashCode();
    
    public int CompareTo(UInt256 other) => _value.CompareTo(other._value);
    
    public static bool operator ==(UInt256 left, UInt256 right) => left.Equals(right);
    public static bool operator !=(UInt256 left, UInt256 right) => !left.Equals(right);
    public static bool operator <(UInt256 left, UInt256 right) => left._value < right._value;
    public static bool operator >(UInt256 left, UInt256 right) => left._value > right._value;
    public static bool operator <=(UInt256 left, UInt256 right) => left._value <= right._value;
    public static bool operator >=(UInt256 left, UInt256 right) => left._value >= right._value;
    
    // Conversion operators
    public static implicit operator UInt256(ulong value) => new(value);
    public static implicit operator UInt256(uint value) => new(value);
    public static explicit operator ulong(UInt256 value) => (ulong)value._value;
    public static explicit operator uint(UInt256 value) => (uint)value._value;
    
    // Utility methods
    public static UInt256 Parse(string value)
    {
        if (value.StartsWith("0x"))
            return new UInt256(value);
        
        if (BigInteger.TryParse(value, out var result))
            return new UInt256(result);
        
        throw new FormatException($"Cannot parse '{value}' as UInt256");
    }
    
    public static bool TryParse(string value, out UInt256 result)
    {
        try
        {
            result = Parse(value);
            return true;
        }
        catch
        {
            result = Zero;
            return false;
        }
    }
}
