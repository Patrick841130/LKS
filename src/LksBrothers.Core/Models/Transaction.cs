using System.Text.Json.Serialization;
using LksBrothers.Core.Primitives;

namespace LksBrothers.Core.Models;

/// <summary>
/// Represents a blockchain transaction with stablecoin support
/// </summary>
public class Transaction
{
    public Hash Hash { get; set; } = Hash.Zero;
    public Address From { get; set; } = Address.Zero;
    public Address To { get; set; } = Address.Zero;
    public UInt256 Value { get; set; } = UInt256.Zero;
    public UInt256 GasLimit { get; set; } = UInt256.Zero;
    public UInt256 GasPrice { get; set; } = UInt256.Zero;
    public UInt256 MaxFeePerGas { get; set; } = UInt256.Zero;
    public UInt256 MaxPriorityFeePerGas { get; set; } = UInt256.Zero;
    public ulong Nonce { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public byte[] Signature { get; set; } = Array.Empty<byte>();
    
    // Stablecoin-specific fields
    public Address? FeeToken { get; set; } // Token used to pay fees (null = native token)
    public UInt256 FeeAmount { get; set; } = UInt256.Zero; // Amount in fee token
    public decimal FeeDiscount { get; set; } = 0m; // Discount percentage (0-100)
    
    // Settlement fields
    public bool IsSettlement { get; set; }
    public Hash? SettlementBatch { get; set; }
    public ulong SettlementIndex { get; set; }
    
    // Compliance fields
    public bool KycRequired { get; set; }
    public bool KycVerified { get; set; }
    public string? ComplianceData { get; set; } // Encrypted compliance information
    
    // Transaction type
    public TransactionType Type { get; set; } = TransactionType.Transfer;
    
    // Execution result (set after execution)
    public TransactionReceipt? Receipt { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Calculates the transaction hash
    /// </summary>
    public Hash CalculateHash()
    {
        var data = new List<byte>();
        
        data.AddRange(From.ToArray());
        data.AddRange(To.ToArray());
        data.AddRange(Value.ToByteArray());
        data.AddRange(GasLimit.ToByteArray());
        data.AddRange(GasPrice.ToByteArray());
        data.AddRange(BitConverter.GetBytes(Nonce));
        data.AddRange(Data);
        
        if (FeeToken != null)
        {
            data.AddRange(FeeToken.Value.ToArray());
            data.AddRange(FeeAmount.ToByteArray());
        }
        
        return Hash.Compute(data.ToArray());
    }
    
    /// <summary>
    /// Validates transaction structure and compliance
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();
        
        // Basic validation
        if (From == Address.Zero)
            errors.Add("From address cannot be zero");
        
        if (GasLimit.IsZero)
            errors.Add("Gas limit must be greater than zero");
        
        if (GasPrice.IsZero && MaxFeePerGas.IsZero)
            errors.Add("Either gas price or max fee per gas must be set");
        
        // Stablecoin validation
        if (FeeToken != null && FeeAmount.IsZero)
            errors.Add("Fee amount must be greater than zero when using fee token");
        
        if (FeeDiscount < 0 || FeeDiscount > 100)
            errors.Add("Fee discount must be between 0 and 100");
        
        // Compliance validation
        if (KycRequired && !KycVerified)
            errors.Add("KYC verification required but not completed");
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
    
    /// <summary>
    /// Estimates the total transaction cost including fees
    /// </summary>
    public TransactionCost EstimateCost(UInt256 baseFee, Dictionary<Address, decimal>? exchangeRates = null)
    {
        var gasUsed = GasLimit; // Simplified - would be estimated based on transaction type
        var totalGasCost = gasUsed * (baseFee + MaxPriorityFeePerGas);
        
        if (FeeToken == null)
        {
            // Paying in native token
            var discountAmount = totalGasCost * (UInt256)(FeeDiscount / 100m);
            return new TransactionCost
            {
                NativeTokenCost = totalGasCost - discountAmount,
                FeeTokenCost = UInt256.Zero,
                FeeToken = null,
                DiscountApplied = discountAmount
            };
        }
        else
        {
            // Paying in alternative token
            var rate = exchangeRates?.GetValueOrDefault(FeeToken.Value, 1m) ?? 1m;
            var tokenCost = (UInt256)((decimal)totalGasCost * rate);
            var discountAmount = tokenCost * (UInt256)(FeeDiscount / 100m);
            
            return new TransactionCost
            {
                NativeTokenCost = UInt256.Zero,
                FeeTokenCost = tokenCost - discountAmount,
                FeeToken = FeeToken,
                DiscountApplied = discountAmount
            };
        }
    }
}

public enum TransactionType
{
    Transfer,
    ContractCall,
    ContractDeploy,
    StablecoinMint,
    StablecoinBurn,
    Settlement,
    Governance,
    Staking
}

public class TransactionReceipt
{
    public Hash TransactionHash { get; set; } = Hash.Zero;
    public Hash BlockHash { get; set; } = Hash.Zero;
    public ulong BlockNumber { get; set; }
    public ulong TransactionIndex { get; set; }
    public Address From { get; set; } = Address.Zero;
    public Address? To { get; set; }
    public UInt256 GasUsed { get; set; } = UInt256.Zero;
    public UInt256 EffectiveGasPrice { get; set; } = UInt256.Zero;
    public bool Success { get; set; }
    public byte[] Output { get; set; } = Array.Empty<byte>();
    public List<Log> Logs { get; set; } = new();
    public string? ErrorMessage { get; set; }
    
    // Settlement-specific fields
    public Hash? SettlementProof { get; set; }
    public bool SettlementCompleted { get; set; }
}

public class Log
{
    public Address Address { get; set; } = Address.Zero;
    public List<Hash> Topics { get; set; } = new();
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public ulong LogIndex { get; set; }
    public bool Removed { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class TransactionCost
{
    public UInt256 NativeTokenCost { get; set; } = UInt256.Zero;
    public UInt256 FeeTokenCost { get; set; } = UInt256.Zero;
    public Address? FeeToken { get; set; }
    public UInt256 DiscountApplied { get; set; } = UInt256.Zero;
}
