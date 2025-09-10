using System.Security.Cryptography;
using System.Text;

namespace LksBrothers.Core.Primitives;

/// <summary>
/// Represents a cryptographic hash with standardized operations
/// </summary>
public readonly struct Hash : IEquatable<Hash>, IComparable<Hash>
{
    public const int SIZE = 32; // 256-bit hash
    
    private readonly byte[] _bytes;
    
    public Hash(byte[] bytes)
    {
        if (bytes.Length != SIZE)
            throw new ArgumentException($"Hash must be exactly {SIZE} bytes");
        
        _bytes = new byte[SIZE];
        Array.Copy(bytes, _bytes, SIZE);
    }
    
    public Hash(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != SIZE)
            throw new ArgumentException($"Hash must be exactly {SIZE} bytes");
        
        _bytes = new byte[SIZE];
        bytes.CopyTo(_bytes);
    }
    
    public static Hash Zero => new(new byte[SIZE]);
    
    public byte[] ToArray()
    {
        var result = new byte[SIZE];
        Array.Copy(_bytes, result, SIZE);
        return result;
    }
    
    public ReadOnlySpan<byte> AsSpan() => _bytes.AsSpan();
    
    public override string ToString() => Convert.ToHexString(_bytes).ToLowerInvariant();
    
    public static Hash FromHex(string hex)
    {
        if (hex.StartsWith("0x"))
            hex = hex[2..];
        
        if (hex.Length != SIZE * 2)
            throw new ArgumentException($"Hex string must be exactly {SIZE * 2} characters");
        
        return new Hash(Convert.FromHexString(hex));
    }
    
    public static Hash Compute(ReadOnlySpan<byte> data)
    {
        return new Hash(SHA256.HashData(data));
    }
    
    public static Hash Compute(string data)
    {
        return Compute(Encoding.UTF8.GetBytes(data));
    }
    
    public static Hash ComputeDouble(ReadOnlySpan<byte> data)
    {
        var firstHash = SHA256.HashData(data);
        return new Hash(SHA256.HashData(firstHash));
    }
    
    public bool Equals(Hash other) => _bytes.AsSpan().SequenceEqual(other._bytes.AsSpan());
    
    public override bool Equals(object? obj) => obj is Hash other && Equals(other);
    
    public override int GetHashCode() => BitConverter.ToInt32(_bytes, 0);
    
    public int CompareTo(Hash other)
    {
        for (int i = 0; i < SIZE; i++)
        {
            var comparison = _bytes[i].CompareTo(other._bytes[i]);
            if (comparison != 0)
                return comparison;
        }
        return 0;
    }
    
    public static bool operator ==(Hash left, Hash right) => left.Equals(right);
    public static bool operator !=(Hash left, Hash right) => !left.Equals(right);
    public static bool operator <(Hash left, Hash right) => left.CompareTo(right) < 0;
    public static bool operator >(Hash left, Hash right) => left.CompareTo(right) > 0;
    public static bool operator <=(Hash left, Hash right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Hash left, Hash right) => left.CompareTo(right) >= 0;
}
