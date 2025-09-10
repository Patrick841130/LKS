using System.Text.Json.Serialization;
using MessagePack;
using LksBrothers.Core.Primitives;

namespace LksBrothers.Core.Models;

/// <summary>
/// Represents a blockchain block with stablecoin settlement support
/// </summary>
public class Block
{
    public Hash Hash { get; set; } = Hash.Zero;
    public Hash ParentHash { get; set; } = Hash.Zero;
    public Hash StateRoot { get; set; } = Hash.Zero;
    public Hash TransactionsRoot { get; set; } = Hash.Zero;
    public Hash ReceiptsRoot { get; set; } = Hash.Zero;
    public ulong Number { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Address Proposer { get; set; } = Address.Zero;
    public UInt256 Difficulty { get; set; } = UInt256.Zero;
    public UInt256 GasLimit { get; set; } = UInt256.Zero;
    public UInt256 GasUsed { get; set; } = UInt256.Zero;
    public UInt256 BaseFee { get; set; } = UInt256.Zero;
    public byte[] ExtraData { get; set; } = Array.Empty<byte>();
    
    // Consensus fields
    public ulong Slot { get; set; } // PoS slot number
    public Hash RandaoReveal { get; set; } = Hash.Zero;
    public List<ValidatorSignature> Attestations { get; set; } = new();
    
    // Stablecoin settlement fields
    public List<SettlementBatch> SettlementBatches { get; set; } = new();
    public Hash SettlementRoot { get; set; } = Hash.Zero;
    
    // Compliance fields
    [Key(8)]
    public List<ComplianceEvent>? ComplianceEvents { get; set; }

    [Key(9)]
    public ulong SlotNumber { get; set; }

    [Key(10)]
    public Address? ProposerAddress { get; set; }

    [Key(11)]
    public List<PoHSequenceProof>? PoHProofs { get; set; } = new();
    public Hash ComplianceRoot { get; set; } = Hash.Zero;
    
    // Oracle data
    public List<OracleUpdate> OracleUpdates { get; set; } = new();
    
    public List<Transaction> Transactions { get; set; } = new();
    
    /// <summary>
    /// Calculates the block hash
    /// </summary>
    public Hash CalculateHash()
    {
        var data = new List<byte>();
        
        data.AddRange(ParentHash.ToArray());
        data.AddRange(StateRoot.ToArray());
        data.AddRange(TransactionsRoot.ToArray());
        data.AddRange(ReceiptsRoot.ToArray());
        data.AddRange(BitConverter.GetBytes(Number));
        data.AddRange(BitConverter.GetBytes(Timestamp.Ticks));
        data.AddRange(Proposer.ToArray());
        data.AddRange(GasLimit.ToByteArray());
        data.AddRange(GasUsed.ToByteArray());
        data.AddRange(BaseFee.ToByteArray());
        data.AddRange(ExtraData);
        data.AddRange(BitConverter.GetBytes(Slot));
        data.AddRange(RandaoReveal.ToArray());
        data.AddRange(SettlementRoot.ToArray());
        data.AddRange(ComplianceRoot.ToArray());
        
        return Hash.Compute(data.ToArray());
    }
    
    /// <summary>
    /// Calculates the Merkle root of transactions
    /// </summary>
    public Hash CalculateTransactionsRoot()
    {
        if (Transactions.Count == 0)
            return Hash.Zero;
        
        var hashes = Transactions.Select(tx => tx.Hash).ToList();
        return CalculateMerkleRoot(hashes);
    }
    
    /// <summary>
    /// Calculates the Merkle root of settlement batches
    /// </summary>
    public Hash CalculateSettlementRoot()
    {
        if (SettlementBatches.Count == 0)
            return Hash.Zero;
        
        var hashes = SettlementBatches.Select(batch => batch.Hash).ToList();
        return CalculateMerkleRoot(hashes);
    }
    
    /// <summary>
    /// Calculates the Merkle root of compliance events
    /// </summary>
    public Hash CalculateComplianceRoot()
    {
        if (ComplianceEvents.Count == 0)
            return Hash.Zero;
        
        var hashes = ComplianceEvents.Select(evt => evt.Hash).ToList();
        return CalculateMerkleRoot(hashes);
    }
    
    private static Hash CalculateMerkleRoot(List<Hash> hashes)
    {
        if (hashes.Count == 0)
            return Hash.Zero;
        
        if (hashes.Count == 1)
            return hashes[0];
        
        var currentLevel = new List<Hash>(hashes);
        
        while (currentLevel.Count > 1)
        {
            var nextLevel = new List<Hash>();
            
            for (int i = 0; i < currentLevel.Count; i += 2)
            {
                if (i + 1 < currentLevel.Count)
                {
                    // Hash pair
                    var combined = new byte[Hash.SIZE * 2];
                    currentLevel[i].AsSpan().CopyTo(combined.AsSpan(0, Hash.SIZE));
                    currentLevel[i + 1].AsSpan().CopyTo(combined.AsSpan(Hash.SIZE, Hash.SIZE));
                    nextLevel.Add(Hash.Compute(combined));
                }
                else
                {
                    // Odd number, hash with itself
                    var combined = new byte[Hash.SIZE * 2];
                    currentLevel[i].AsSpan().CopyTo(combined.AsSpan(0, Hash.SIZE));
                    currentLevel[i].AsSpan().CopyTo(combined.AsSpan(Hash.SIZE, Hash.SIZE));
                    nextLevel.Add(Hash.Compute(combined));
                }
            }
            
            currentLevel = nextLevel;
        }
        
        return currentLevel[0];
    }
    
    /// <summary>
    /// Validates block structure and consensus rules
    /// </summary>
    public ValidationResult Validate(Block? parentBlock = null)
    {
        var errors = new List<string>();
        
        // Basic validation
        if (Number == 0 && ParentHash != Hash.Zero)
            errors.Add("Genesis block must have zero parent hash");
        
        if (Number > 0 && ParentHash == Hash.Zero)
            errors.Add("Non-genesis block must have valid parent hash");
        
        if (parentBlock != null && Number != parentBlock.Number + 1)
            errors.Add("Block number must be parent + 1");
        
        if (GasUsed > GasLimit)
            errors.Add("Gas used cannot exceed gas limit");
        
        if (Timestamp < DateTime.UtcNow.AddMinutes(-10))
            errors.Add("Block timestamp too far in the past");
        
        if (Timestamp > DateTime.UtcNow.AddMinutes(10))
            errors.Add("Block timestamp too far in the future");
        
        // Validate Merkle roots
        if (TransactionsRoot != CalculateTransactionsRoot())
            errors.Add("Invalid transactions root");
        
        if (SettlementRoot != CalculateSettlementRoot())
            errors.Add("Invalid settlement root");
        
        if (ComplianceRoot != CalculateComplianceRoot())
            errors.Add("Invalid compliance root");
        
        // Validate transactions
        foreach (var tx in Transactions)
        {
            var txValidation = tx.Validate();
            if (!txValidation.IsValid)
                errors.AddRange(txValidation.Errors.Select(e => $"Transaction {tx.Hash}: {e}"));
        }
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}

public class ValidatorSignature
{
    public Address Validator { get; set; } = Address.Zero;
    public byte[] Signature { get; set; } = Array.Empty<byte>();
    public ulong Slot { get; set; }
    public Hash BlockHash { get; set; } = Hash.Zero;
}

public class SettlementBatch
{
    public Hash Hash { get; set; } = Hash.Zero;
    public List<Hash> TransactionHashes { get; set; } = new();
    public UInt256 TotalAmount { get; set; } = UInt256.Zero;
    public Address SettlementToken { get; set; } = Address.Zero;
    public DateTime SettlementTime { get; set; } = DateTime.UtcNow;
    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;
    public byte[] Proof { get; set; } = Array.Empty<byte>();
}

public enum SettlementStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public class ComplianceEvent
{
    public Hash Hash { get; set; } = Hash.Zero;
    public ComplianceEventType Type { get; set; }
    public Address Subject { get; set; } = Address.Zero;
    public string Data { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsAlert { get; set; }
}

public enum ComplianceEventType
{
    KycVerification,
    AmlCheck,
    SanctionListCheck,
    SuspiciousActivity,
    LargeTransaction,
    ComplianceViolation
}

public class OracleUpdate
{
    public Address Oracle { get; set; } = Address.Zero;
    public string DataKey { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public byte[] Signature { get; set; } = Array.Empty<byte>();
}
