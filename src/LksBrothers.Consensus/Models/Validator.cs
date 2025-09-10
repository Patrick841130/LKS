using LksBrothers.Core.Primitives;

namespace LksBrothers.Consensus.Models;

/// <summary>
/// Represents a validator in the Proof of Stake consensus
/// </summary>
public class Validator
{
    public Address Address { get; set; } = Address.Zero;
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();
    public UInt256 Stake { get; set; } = UInt256.Zero;
    public UInt256 EffectiveStake { get; set; } = UInt256.Zero;
    public ValidatorStatus Status { get; set; } = ValidatorStatus.Inactive;
    public ulong ActivationEpoch { get; set; }
    public ulong ExitEpoch { get; set; } = ulong.MaxValue;
    public ulong WithdrawableEpoch { get; set; } = ulong.MaxValue;
    
    // Performance metrics
    public double AttestationRate { get; set; } = 1.0;
    public double ProposalRate { get; set; } = 1.0;
    public ulong LastActiveSlot { get; set; }
    public ulong MissedSlots { get; set; }
    public ulong MissedAttestations { get; set; }
    
    // Slashing information
    public bool IsSlashed { get; set; }
    public ulong SlashedEpoch { get; set; }
    public UInt256 SlashedAmount { get; set; } = UInt256.Zero;
    public string? SlashingReason { get; set; }
    
    // Delegation information
    public List<Delegation> Delegations { get; set; } = new();
    public UInt256 TotalDelegated { get; set; } = UInt256.Zero;
    public decimal CommissionRate { get; set; } = 0.1m; // 10% default commission
    
    // Metadata
    public string? Name { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }
    public DateTime RegistrationTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Calculates the validator's voting power based on stake and performance
    /// </summary>
    public UInt256 CalculateVotingPower()
    {
        if (Status != ValidatorStatus.Active || IsSlashed)
            return UInt256.Zero;
        
        var baseStake = Stake + TotalDelegated;
        
        // Apply performance multiplier (0.5 to 1.0 based on attestation rate)
        var performanceMultiplier = Math.Max(0.5, AttestationRate);
        var adjustedStake = (UInt256)((decimal)baseStake * (decimal)performanceMultiplier);
        
        return adjustedStake;
    }
    
    /// <summary>
    /// Checks if validator is eligible for block proposal
    /// </summary>
    public bool IsEligibleForProposal(ulong currentSlot, ulong epoch)
    {
        return Status == ValidatorStatus.Active &&
               !IsSlashed &&
               ActivationEpoch <= epoch &&
               ExitEpoch > epoch &&
               Stake >= GetMinimumStake();
    }
    
    /// <summary>
    /// Checks if validator is eligible for attestation
    /// </summary>
    public bool IsEligibleForAttestation(ulong currentSlot, ulong epoch)
    {
        return Status == ValidatorStatus.Active &&
               !IsSlashed &&
               ActivationEpoch <= epoch &&
               ExitEpoch > epoch;
    }
    
    /// <summary>
    /// Applies slashing penalty
    /// </summary>
    public void ApplySlashing(UInt256 amount, string reason)
    {
        IsSlashed = true;
        SlashedAmount = amount;
        SlashingReason = reason;
        SlashedEpoch = GetCurrentEpoch();
        Status = ValidatorStatus.Slashed;
        
        // Reduce stake
        if (Stake >= amount)
            Stake -= amount;
        else
            Stake = UInt256.Zero;
    }
    
    /// <summary>
    /// Updates performance metrics
    /// </summary>
    public void UpdatePerformance(bool attestationSuccess, bool proposalSuccess = false)
    {
        // Update attestation rate (exponential moving average)
        var alpha = 0.1; // Smoothing factor
        AttestationRate = (1 - alpha) * AttestationRate + alpha * (attestationSuccess ? 1.0 : 0.0);
        
        if (proposalSuccess)
        {
            ProposalRate = (1 - alpha) * ProposalRate + alpha * 1.0;
        }
        
        if (!attestationSuccess)
            MissedAttestations++;
        
        LastActiveSlot = GetCurrentSlot();
    }
    
    private static UInt256 GetMinimumStake() => new(32_000_000_000_000_000_000UL); // 32 LKS tokens
    private static ulong GetCurrentEpoch() => GetCurrentSlot() / 32; // 32 slots per epoch
    private static ulong GetCurrentSlot() => (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond / 12); // 12 second slots
}

public enum ValidatorStatus
{
    Inactive,
    PendingActivation,
    Active,
    PendingExit,
    Exited,
    Slashed
}

public class Delegation
{
    public Address Delegator { get; set; } = Address.Zero;
    public UInt256 Amount { get; set; } = UInt256.Zero;
    public ulong DelegationEpoch { get; set; }
    public ulong UndelegationEpoch { get; set; } = ulong.MaxValue;
    public UInt256 Rewards { get; set; } = UInt256.Zero;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
