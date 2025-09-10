using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using LksBrothers.Consensus.Models;
using System.Collections.Concurrent;

namespace LksBrothers.Consensus.Engine;

public class HybridConsensusEngine : IDisposable
{
    private readonly ILogger<HybridConsensusEngine> _logger;
    private readonly ProofOfHistoryEngine _pohEngine;
    private readonly ConsensusEngine _posEngine;
    private readonly HybridConsensusOptions _options;
    private readonly ConcurrentDictionary<ulong, SlotConsensus> _slotConsensus;
    private readonly Timer _slotTimer;
    
    private volatile bool _isRunning;
    private ulong _currentSlot;
    private Address? _currentLeader;

    public HybridConsensusEngine(
        ILogger<HybridConsensusEngine> logger,
        ProofOfHistoryEngine pohEngine,
        ConsensusEngine posEngine,
        IOptions<HybridConsensusOptions> options)
    {
        _logger = logger;
        _pohEngine = pohEngine;
        _posEngine = posEngine;
        _options = options.Value;
        _slotConsensus = new ConcurrentDictionary<ulong, SlotConsensus>();
        
        // Timer for slot transitions (400ms slots)
        _slotTimer = new Timer(AdvanceSlot, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(400));
        
        _logger.LogInformation("Hybrid PoH+PoS consensus engine initialized");
    }

    public async Task StartAsync()
    {
        _isRunning = true;
        _pohEngine.Start();
        await _posEngine.StartAsync();
        
        _currentSlot = _pohEngine.GetCurrentSlot();
        await SelectSlotLeader(_currentSlot);
        
        _logger.LogInformation("Hybrid consensus engine started at slot {Slot}", _currentSlot);
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        _pohEngine.Stop();
        await _posEngine.StopAsync();
        
        _logger.LogInformation("Hybrid consensus engine stopped");
    }

    public async Task<BlockProposalResult> ProposeBlockAsync(List<Transaction> transactions)
    {
        if (!_isRunning)
            return BlockProposalResult.Failed("Consensus engine not running");

        try
        {
            var currentSlot = _pohEngine.GetCurrentSlot();
            var slotConsensus = GetOrCreateSlotConsensus(currentSlot);
            
            // Check if we're the slot leader
            if (_currentLeader == null || !_currentLeader.Equals(_posEngine.GetValidatorAddress()))
            {
                return BlockProposalResult.Failed("Not the slot leader for current slot");
            }

            // Generate PoH proofs for all transactions
            var pohProofs = new List<PoHSequenceProof>();
            foreach (var tx in transactions)
            {
                var proof = _pohEngine.GenerateSequenceProof(tx);
                pohProofs.Add(proof);
            }

            // Create block with PoH ordering
            var block = new Block
            {
                Header = new BlockHeader
                {
                    Number = slotConsensus.BlockNumber,
                    ParentHash = slotConsensus.ParentHash,
                    Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    SlotNumber = currentSlot,
                    ProposerAddress = _currentLeader,
                    PoHProofs = pohProofs
                },
                Transactions = transactions
            };

            // Calculate block hash
            block.Hash = block.CalculateHash();

            // Submit to PoS consensus for validation
            var posResult = await _posEngine.ProposeBlockAsync(block);
            if (!posResult.Success)
            {
                return BlockProposalResult.Failed($"PoS validation failed: {posResult.ErrorMessage}");
            }

            // Update slot consensus
            slotConsensus.ProposedBlock = block;
            slotConsensus.ProposalTime = DateTimeOffset.UtcNow;
            slotConsensus.Status = SlotStatus.BlockProposed;

            _logger.LogInformation("Block proposed for slot {Slot} with {TxCount} transactions", 
                currentSlot, transactions.Count);

            return BlockProposalResult.Success(block, pohProofs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proposing block for slot {Slot}", _pohEngine.GetCurrentSlot());
            return BlockProposalResult.Failed($"Block proposal error: {ex.Message}");
        }
    }

    public async Task<AttestationResult> ProcessAttestationAsync(Attestation attestation)
    {
        try
        {
            // Verify PoH proofs in the attestation
            if (attestation.PoHProofs != null)
            {
                foreach (var proof in attestation.PoHProofs)
                {
                    if (!_pohEngine.VerifySequenceProof(proof))
                    {
                        return AttestationResult.Rejected("Invalid PoH proof in attestation");
                    }
                }
            }

            // Process through PoS consensus
            var posResult = await _posEngine.ProcessAttestationAsync(attestation);
            if (!posResult.Success)
            {
                return AttestationResult.Rejected($"PoS attestation failed: {posResult.ErrorMessage}");
            }

            // Update slot consensus
            var slotConsensus = GetOrCreateSlotConsensus(attestation.Slot);
            slotConsensus.Attestations.Add(attestation);
            
            // Check if we have enough attestations for finality
            await CheckSlotFinality(attestation.Slot);

            return AttestationResult.Accepted(attestation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing attestation for slot {Slot}", attestation.Slot);
            return AttestationResult.Rejected($"Attestation processing error: {ex.Message}");
        }
    }

    public async Task<FinalityResult> CheckFinalityAsync(ulong slot)
    {
        try
        {
            if (!_slotConsensus.TryGetValue(slot, out var slotConsensus))
            {
                return FinalityResult.Pending("Slot consensus not found");
            }

            // Check PoS finality
            var posFinality = await _posEngine.CheckFinalityAsync(slot);
            if (!posFinality.IsFinalized)
            {
                return FinalityResult.Pending("PoS finality not achieved");
            }

            // Verify PoH sequence integrity
            if (slotConsensus.ProposedBlock?.Header.PoHProofs != null)
            {
                foreach (var proof in slotConsensus.ProposedBlock.Header.PoHProofs)
                {
                    if (!_pohEngine.VerifySequenceProof(proof))
                    {
                        return FinalityResult.Failed("PoH sequence verification failed");
                    }
                }
            }

            // Mark as finalized
            slotConsensus.Status = SlotStatus.Finalized;
            slotConsensus.FinalizedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation("Slot {Slot} achieved hybrid finality", slot);
            
            return FinalityResult.Finalized(slotConsensus.ProposedBlock!, slotConsensus.FinalizedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking finality for slot {Slot}", slot);
            return FinalityResult.Failed($"Finality check error: {ex.Message}");
        }
    }

    public HybridConsensusMetrics GetMetrics()
    {
        var pohState = _pohEngine.GetCurrentState();
        var posMetrics = _posEngine.GetMetrics();
        
        return new HybridConsensusMetrics
        {
            CurrentSlot = _currentSlot,
            CurrentLeader = _currentLeader,
            PoHSequence = pohState.CurrentSequence,
            PoHTicksPerSecond = pohState.IsRunning ? 1_000_000.0 : 0, // 1M ticks per second target
            ActiveSlots = _slotConsensus.Count,
            FinalizedSlots = _slotConsensus.Values.Count(s => s.Status == SlotStatus.Finalized),
            PendingSlots = _slotConsensus.Values.Count(s => s.Status == SlotStatus.Pending),
            ValidatorCount = posMetrics.ActiveValidators,
            StakeWeight = posMetrics.TotalStake,
            AverageSlotTime = TimeSpan.FromMilliseconds(400), // Target 400ms
            ThroughputTPS = CalculateThroughput()
        };
    }

    private async Task SelectSlotLeader(ulong slot)
    {
        try
        {
            // Use PoS validator selection with PoH randomness
            var pohState = _pohEngine.GetCurrentState();
            var randomSeed = pohState.CurrentHash.Bytes.Take(8).ToArray();
            var randomValue = BitConverter.ToUInt64(randomSeed);
            
            var leader = await _posEngine.SelectSlotLeaderAsync(slot, randomValue);
            _currentLeader = leader;
            
            var slotConsensus = GetOrCreateSlotConsensus(slot);
            slotConsensus.SlotLeader = leader;
            
            _logger.LogDebug("Selected slot leader {Leader} for slot {Slot}", leader, slot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting slot leader for slot {Slot}", slot);
        }
    }

    private SlotConsensus GetOrCreateSlotConsensus(ulong slot)
    {
        return _slotConsensus.GetOrAdd(slot, _ => new SlotConsensus
        {
            Slot = slot,
            BlockNumber = slot, // Simple mapping for now
            ParentHash = slot > 0 ? GetParentHash(slot - 1) : Hash.Zero,
            Status = SlotStatus.Pending,
            Attestations = new List<Attestation>(),
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private Hash GetParentHash(ulong parentSlot)
    {
        if (_slotConsensus.TryGetValue(parentSlot, out var parentConsensus) && 
            parentConsensus.ProposedBlock != null)
        {
            return parentConsensus.ProposedBlock.Hash;
        }
        return Hash.Zero;
    }

    private async Task CheckSlotFinality(ulong slot)
    {
        if (!_slotConsensus.TryGetValue(slot, out var slotConsensus))
            return;

        // Check if we have enough attestations (2/3+ of stake)
        var totalStake = await _posEngine.GetTotalStakeAsync();
        var attestedStake = UInt256.Zero;
        
        foreach (var attestation in slotConsensus.Attestations)
        {
            var validatorStake = await _posEngine.GetValidatorStakeAsync(attestation.ValidatorAddress);
            attestedStake += validatorStake;
        }

        var threshold = totalStake * 2 / 3;
        if (attestedStake >= threshold)
        {
            await CheckFinalityAsync(slot);
        }
    }

    private void AdvanceSlot(object? state)
    {
        if (!_isRunning) return;

        try
        {
            var newSlot = _pohEngine.GetCurrentSlot();
            if (newSlot > _currentSlot)
            {
                _currentSlot = newSlot;
                _ = Task.Run(() => SelectSlotLeader(newSlot));
                
                // Cleanup old slots
                CleanupOldSlots();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error advancing slot");
        }
    }

    private void CleanupOldSlots()
    {
        var cutoffSlot = _currentSlot > 100 ? _currentSlot - 100 : 0;
        var slotsToRemove = _slotConsensus.Keys.Where(s => s < cutoffSlot).ToList();
        
        foreach (var slot in slotsToRemove)
        {
            _slotConsensus.TryRemove(slot, out _);
        }
    }

    private double CalculateThroughput()
    {
        // Calculate TPS based on recent finalized blocks
        var recentSlots = _slotConsensus.Values
            .Where(s => s.Status == SlotStatus.Finalized && 
                       s.FinalizedAt > DateTimeOffset.UtcNow.AddMinutes(-1))
            .ToList();

        if (recentSlots.Count == 0) return 0;

        var totalTransactions = recentSlots.Sum(s => s.ProposedBlock?.Transactions.Count ?? 0);
        var timeSpan = TimeSpan.FromMinutes(1);
        
        return totalTransactions / timeSpan.TotalSeconds;
    }

    public void Dispose()
    {
        _isRunning = false;
        _slotTimer?.Dispose();
        _pohEngine?.Dispose();
        _posEngine?.Dispose();
        _logger.LogInformation("Hybrid consensus engine disposed");
    }
}

public class SlotConsensus
{
    public required ulong Slot { get; set; }
    public required ulong BlockNumber { get; set; }
    public required Hash ParentHash { get; set; }
    public Address? SlotLeader { get; set; }
    public Block? ProposedBlock { get; set; }
    public DateTimeOffset? ProposalTime { get; set; }
    public required SlotStatus Status { get; set; }
    public required List<Attestation> Attestations { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}

public enum SlotStatus
{
    Pending,
    BlockProposed,
    Attested,
    Finalized,
    Skipped
}

public class BlockProposalResult
{
    public required bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Block? Block { get; set; }
    public List<PoHSequenceProof>? PoHProofs { get; set; }

    public static BlockProposalResult Success(Block block, List<PoHSequenceProof> proofs)
    {
        return new BlockProposalResult
        {
            Success = true,
            Block = block,
            PoHProofs = proofs
        };
    }

    public static BlockProposalResult Failed(string error)
    {
        return new BlockProposalResult
        {
            Success = false,
            ErrorMessage = error
        };
    }
}

public class AttestationResult
{
    public required bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Attestation? Attestation { get; set; }

    public static AttestationResult Accepted(Attestation attestation)
    {
        return new AttestationResult
        {
            Success = true,
            Attestation = attestation
        };
    }

    public static AttestationResult Rejected(string error)
    {
        return new AttestationResult
        {
            Success = false,
            ErrorMessage = error
        };
    }
}

public class FinalityResult
{
    public required bool IsFinalized { get; set; }
    public string? Message { get; set; }
    public Block? FinalizedBlock { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }

    public static FinalityResult Finalized(Block block, DateTimeOffset finalizedAt)
    {
        return new FinalityResult
        {
            IsFinalized = true,
            Message = "Block finalized",
            FinalizedBlock = block,
            FinalizedAt = finalizedAt
        };
    }

    public static FinalityResult Pending(string message)
    {
        return new FinalityResult
        {
            IsFinalized = false,
            Message = message
        };
    }

    public static FinalityResult Failed(string error)
    {
        return new FinalityResult
        {
            IsFinalized = false,
            Message = error
        };
    }
}

public class HybridConsensusMetrics
{
    public required ulong CurrentSlot { get; set; }
    public Address? CurrentLeader { get; set; }
    public required ulong PoHSequence { get; set; }
    public required double PoHTicksPerSecond { get; set; }
    public required int ActiveSlots { get; set; }
    public required int FinalizedSlots { get; set; }
    public required int PendingSlots { get; set; }
    public required int ValidatorCount { get; set; }
    public required UInt256 StakeWeight { get; set; }
    public required TimeSpan AverageSlotTime { get; set; }
    public required double ThroughputTPS { get; set; }
}

public class HybridConsensusOptions
{
    public TimeSpan SlotDuration { get; set; } = TimeSpan.FromMilliseconds(400);
    public int MaxSlotsToKeep { get; set; } = 1000;
    public double FinalityThreshold { get; set; } = 0.67; // 2/3 stake
    public bool EnableMetrics { get; set; } = true;
}
