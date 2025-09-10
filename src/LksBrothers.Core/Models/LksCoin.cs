using MessagePack;
using LksBrothers.Core.Primitives;

namespace LksBrothers.Core.Models;

[MessagePackObject]
public class LksCoin
{
    [Key(0)]
    public required string Symbol { get; set; } = "LKS";

    [Key(1)]
    public required string Name { get; set; } = "LKS COIN";

    [Key(2)]
    public required UInt256 TotalSupply { get; set; } = new UInt256(50_000_000_000_000_000_000_000_000_000UL); // 50 billion with 18 decimals

    [Key(3)]
    public required byte Decimals { get; set; } = 18;

    [Key(4)]
    public required Address Issuer { get; set; }

    [Key(5)]
    public required UInt256 CirculatingSupply { get; set; }

    [Key(6)]
    public required LksCoinDistribution Distribution { get; set; }

    [Key(7)]
    public required bool ZeroFeesEnabled { get; set; } = true;

    [Key(8)]
    public required Address FoundationAccount { get; set; }

    [Key(9)]
    public Dictionary<string, object>? Metadata { get; set; }

    public UInt256 GetDistributionAmount(DistributionType type)
    {
        return type switch
        {
            DistributionType.FoundationReserve => TotalSupply * 20 / 100, // 20%
            DistributionType.PublicDistribution => TotalSupply * 50 / 100, // 50%
            DistributionType.ValidatorRewards => TotalSupply * 15 / 100, // 15%
            DistributionType.DevelopmentFund => TotalSupply * 10 / 100, // 10%
            DistributionType.StrategicPartners => TotalSupply * 5 / 100, // 5%
            _ => UInt256.Zero
        };
    }

    public bool IsValidTransfer(Address from, Address to, UInt256 amount)
    {
        // Basic validation - can be extended with more complex rules
        return amount > UInt256.Zero && from != to;
    }

    public UInt256 CalculateTransactionFee(TransactionType txType, UInt256 amount)
    {
        if (ZeroFeesEnabled)
            return UInt256.Zero; // Users pay no fees

        // Fallback fee calculation if zero fees are disabled
        return txType switch
        {
            TransactionType.Transfer => UInt256.Zero,
            TransactionType.ContractCall => amount / 1000, // 0.1%
            TransactionType.ContractDeploy => new UInt256(1000000000000000000UL), // 1 LKS
            _ => UInt256.Zero
        };
    }
}

[MessagePackObject]
public class LksCoinDistribution
{
    [Key(0)]
    public required UInt256 FoundationReserve { get; set; }

    [Key(1)]
    public required UInt256 PublicDistribution { get; set; }

    [Key(2)]
    public required UInt256 ValidatorRewards { get; set; }

    [Key(3)]
    public required UInt256 DevelopmentFund { get; set; }

    [Key(4)]
    public required UInt256 StrategicPartners { get; set; }

    [Key(5)]
    public required UInt256 Distributed { get; set; }

    [Key(6)]
    public required Dictionary<Address, UInt256> Allocations { get; set; } = new();

    public UInt256 GetTotalAllocated()
    {
        return FoundationReserve + PublicDistribution + ValidatorRewards + DevelopmentFund + StrategicPartners;
    }

    public UInt256 GetRemainingDistribution(DistributionType type)
    {
        var allocated = GetAllocationByType(type);
        var distributed = GetDistributedByType(type);
        return allocated - distributed;
    }

    private UInt256 GetAllocationByType(DistributionType type)
    {
        return type switch
        {
            DistributionType.FoundationReserve => FoundationReserve,
            DistributionType.PublicDistribution => PublicDistribution,
            DistributionType.ValidatorRewards => ValidatorRewards,
            DistributionType.DevelopmentFund => DevelopmentFund,
            DistributionType.StrategicPartners => StrategicPartners,
            _ => UInt256.Zero
        };
    }

    private UInt256 GetDistributedByType(DistributionType type)
    {
        // This would track actual distributions per type
        // For now, return zero - to be implemented with state tracking
        return UInt256.Zero;
    }
}

public enum DistributionType
{
    FoundationReserve,
    PublicDistribution,
    ValidatorRewards,
    DevelopmentFund,
    StrategicPartners
}

public enum TransactionType
{
    Transfer,
    ContractCall,
    ContractDeploy,
    Governance,
    Staking,
    CrossChain
}

[MessagePackObject]
public class LksCoinTransaction
{
    [Key(0)]
    public required Hash Hash { get; set; }

    [Key(1)]
    public required Address From { get; set; }

    [Key(2)]
    public required Address To { get; set; }

    [Key(3)]
    public required UInt256 Amount { get; set; }

    [Key(4)]
    public required TransactionType Type { get; set; }

    [Key(5)]
    public required UInt256 NetworkFee { get; set; } // Paid by foundation if zero fees enabled

    [Key(6)]
    public required UInt256 UserFee { get; set; } // Always zero for LKS COIN

    [Key(7)]
    public required ulong Timestamp { get; set; }

    [Key(8)]
    public required bool ZeroFeeSponsored { get; set; }

    [Key(9)]
    public string? Memo { get; set; }

    [Key(10)]
    public Dictionary<string, object>? Metadata { get; set; }

    public static LksCoinTransaction CreateTransfer(Address from, Address to, UInt256 amount, Address foundationAccount)
    {
        var hash = Hash.ComputeHash($"{from}{to}{amount}{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"u8.ToArray());
        
        return new LksCoinTransaction
        {
            Hash = hash,
            From = from,
            To = to,
            Amount = amount,
            Type = TransactionType.Transfer,
            NetworkFee = new UInt256(12), // Standard XRPL fee in drops
            UserFee = UInt256.Zero, // User pays nothing
            Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ZeroFeeSponsored = true
        };
    }

    public XrplTransaction ToXrplTransaction()
    {
        return new XrplTransaction
        {
            TransactionType = "Payment",
            Account = From.ToString(),
            Destination = To.ToString(),
            Amount = new XrplAmount
            {
                Currency = "LKS",
                Value = Amount.ToString(),
                Issuer = From.ToString() // This would be the LKS issuer address
            },
            Fee = NetworkFee.ToString(),
            Flags = 2147483648, // tfSetfNoRipple
            Memo = Memo
        };
    }
}

[MessagePackObject]
public class XrplTransaction
{
    [Key(0)]
    public required string TransactionType { get; set; }

    [Key(1)]
    public required string Account { get; set; }

    [Key(2)]
    public required string Destination { get; set; }

    [Key(3)]
    public required XrplAmount Amount { get; set; }

    [Key(4)]
    public required string Fee { get; set; }

    [Key(5)]
    public required uint Flags { get; set; }

    [Key(6)]
    public string? Memo { get; set; }
}

[MessagePackObject]
public class XrplAmount
{
    [Key(0)]
    public required string Currency { get; set; }

    [Key(1)]
    public required string Value { get; set; }

    [Key(2)]
    public required string Issuer { get; set; }
}
