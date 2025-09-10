using System.Security.Cryptography;
using System.Text;

namespace LksBrothers.Core.Primitives;

/// <summary>
/// Represents a blockchain address (20 bytes, Ethereum-compatible)
/// </summary>
public readonly struct Address : IEquatable<Address>, IComparable<Address>
{
    public const int SIZE = 20; // 160-bit address
    
    private readonly byte[] _bytes;
    
    public Address(byte[] bytes)
    {
        if (bytes.Length != SIZE)
            throw new ArgumentException($"Address must be exactly {SIZE} bytes");
        
        _bytes = new byte[SIZE];
        Array.Copy(bytes, _bytes, SIZE);
    }
    
    public Address(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != SIZE)
            throw new ArgumentException($"Address must be exactly {SIZE} bytes");
        
        _bytes = new byte[SIZE];
        bytes.CopyTo(_bytes);
    }
    
    public static Address Zero => new(new byte[SIZE]);
    
    public byte[] ToArray()
    {
        var result = new byte[SIZE];
        Array.Copy(_bytes, result, SIZE);
        return result;
    }
    
    public ReadOnlySpan<byte> AsSpan() => _bytes.AsSpan();
    
    public override string ToString() => "0x" + Convert.ToHexString(_bytes).ToLowerInvariant();
    
    public static Address FromHex(string hex)
    {
        if (hex.StartsWith("0x"))
            hex = hex[2..];
        
        if (hex.Length != SIZE * 2)
            throw new ArgumentException($"Hex string must be exactly {SIZE * 2} characters");
        
        return new Address(Convert.FromHexString(hex));
    }
    
    /// <summary>
    /// Creates an address from a public key using Ethereum's method (Keccak256 hash, take last 20 bytes)
    /// </summary>
    public static Address FromPublicKey(ReadOnlySpan<byte> publicKey)
    {
        // For now using SHA256 as placeholder - in production would use Keccak256
        var hash = SHA256.HashData(publicKey);
        return new Address(hash[^SIZE..]);
    }
    
    /// <summary>
    /// Creates a contract address using CREATE opcode method
    /// </summary>
    public static Address CreateContractAddress(Address deployer, ulong nonce)
    {
        // Simplified implementation - in production would use RLP encoding
        var data = new byte[SIZE + 8];
        deployer._bytes.CopyTo(data, 0);
        BitConverter.GetBytes(nonce).CopyTo(data, SIZE);
        
        var hash = SHA256.HashData(data);
        return new Address(hash[^SIZE..]);
    }
    
    /// <summary>
    /// Creates a contract address using CREATE2 opcode method
    /// </summary>
    public static Address Create2ContractAddress(Address deployer, Hash salt, Hash codeHash)
    {
        var data = new byte[1 + SIZE + Hash.SIZE + Hash.SIZE];
        data[0] = 0xff; // CREATE2 prefix
        deployer._bytes.CopyTo(data, 1);
        salt.AsSpan().CopyTo(data.AsSpan(1 + SIZE));
        codeHash.AsSpan().CopyTo(data.AsSpan(1 + SIZE + Hash.SIZE));
        
        var hash = SHA256.HashData(data);
        return new Address(hash[^SIZE..]);
    }
    
    public bool Equals(Address other) => _bytes.AsSpan().SequenceEqual(other._bytes.AsSpan());
    
    public override bool Equals(object? obj) => obj is Address other && Equals(other);
    
    public override int GetHashCode() => BitConverter.ToInt32(_bytes, 0);
    
    public int CompareTo(Address other)
    {
        for (int i = 0; i < SIZE; i++)
        {
            var comparison = _bytes[i].CompareTo(other._bytes[i]);
            if (comparison != 0)
                return comparison;
        }
        return 0;
    }
    
    public static bool operator ==(Address left, Address right) => left.Equals(right);
    public static bool operator !=(Address left, Address right) => !left.Equals(right);
    public static bool operator <(Address left, Address right) => left.CompareTo(right) < 0;
    public static bool operator >(Address left, Address right) => left.CompareTo(right) > 0;
    public static bool operator <=(Address left, Address right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Address left, Address right) => left.CompareTo(right) >= 0;
}
