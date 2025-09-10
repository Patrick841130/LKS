using LksBrothers.Core.Models;

namespace LksBrothers.Genesis.Services;

public class GenesisConfiguration
{
    public int ChainId { get; set; }
    public string NetworkName { get; set; } = string.Empty;
    public DateTimeOffset GenesisTime { get; set; }
    public UInt256 InitialSupply { get; set; }
    public string FoundationAddress { get; set; } = string.Empty;
    public UInt256 ValidatorStakeRequired { get; set; }
    public TimeSpan SlotDuration { get; set; }
    public ulong EpochLength { get; set; }
    public List<ValidatorInfo> InitialValidators { get; set; } = new();
}

public class ValidatorInfo
{
    public string Address { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public UInt256 Stake { get; set; }
    public double Commission { get; set; }
}

public class ValidatorState
{
    public Address Address { get; set; }
    public string PublicKey { get; set; } = string.Empty;
    public UInt256 Stake { get; set; }
    public double Commission { get; set; }
    public bool IsActive { get; set; }
    public ulong JoinedEpoch { get; set; }
    public ulong LastActiveEpoch { get; set; }
}

public class PoHSequenceProof
{
    public ulong SequenceNumber { get; set; }
    public Hash Hash { get; set; } = Hash.Zero;
    public Hash PreviousHash { get; set; } = Hash.Zero;
    public DateTimeOffset Timestamp { get; set; }
    public ulong TickCount { get; set; }
}
