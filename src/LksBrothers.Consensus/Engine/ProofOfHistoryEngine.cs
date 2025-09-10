using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LksBrothers.Core.Primitives;
using LksBrothers.Core.Models;
using System.Security.Cryptography;
using System.Collections.Concurrent;

namespace LksBrothers.Consensus.Engine;

public class ProofOfHistoryEngine : IDisposable
{
    private readonly ILogger<ProofOfHistoryEngine> _logger;
    private readonly ProofOfHistoryOptions _options;
    private readonly ConcurrentQueue<HistoryEntry> _historyQueue;
    private readonly Timer _pohTimer;
    private readonly SHA256 _sha256;
    private readonly object _sequenceLock = new();
    
    private ulong _currentSequence;
    private Hash _currentHash;
    private volatile bool _isRunning;
    private ulong _ticksPerSlot;
    private DateTimeOffset _genesisTime;

    public ProofOfHistoryEngine(ILogger<ProofOfHistoryEngine> logger, IOptions<ProofOfHistoryOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _historyQueue = new ConcurrentQueue<HistoryEntry>();
        _sha256 = SHA256.Create();
        
        _currentSequence = 0;
        _currentHash = Hash.ComputeHash("LKS_COIN_GENESIS"u8.ToArray());
        _ticksPerSlot = _options.TicksPerSlot;
        _genesisTime = DateTimeOffset.UtcNow;
        
        // Start PoH timer with microsecond precision
        _pohTimer = new Timer(GeneratePoHSequence, null, TimeSpan.Zero, TimeSpan.FromMicroseconds(_options.TickIntervalMicroseconds));
        
        _logger.LogInformation("Proof of History engine initialized with {TicksPerSlot} ticks per slot", _ticksPerSlot);
    }

    public void Start()
    {
        _isRunning = true;
        _genesisTime = DateTimeOffset.UtcNow;
        _logger.LogInformation("Proof of History engine started at {GenesisTime}", _genesisTime);
    }

    public void Stop()
    {
        _isRunning = false;
        _logger.LogInformation("Proof of History engine stopped");
    }

    public PoHSequenceProof GenerateSequenceProof(Transaction transaction)
    {
        lock (_sequenceLock)
        {
            // Create a verifiable proof that this transaction occurred at this point in history
            var txHash = transaction.Hash;
            var sequenceNumber = _currentSequence;
            var timestamp = DateTimeOffset.UtcNow;
            
            // Mix transaction hash with current PoH state
            var mixedData = new byte[64];
            _currentHash.Bytes.CopyTo(mixedData, 0);
            txHash.Bytes.CopyTo(mixedData, 32);
            
            var proofHash = Hash.ComputeHash(mixedData);
            
            // Advance the sequence
            _currentHash = proofHash;
            _currentSequence++;
            
            var proof = new PoHSequenceProof
            {
                SequenceNumber = sequenceNumber,
                TransactionHash = txHash,
                ProofHash = proofHash,
                PreviousHash = _currentHash,
                Timestamp = timestamp,
                SlotNumber = GetCurrentSlot(),
                TicksInSlot = sequenceNumber % _ticksPerSlot
            };
            
            // Queue for verification
            _historyQueue.Enqueue(new HistoryEntry
            {
                Proof = proof,
                CreatedAt = timestamp
            });
            
            _logger.LogDebug("Generated PoH proof for transaction {TxHash} at sequence {Sequence}", 
                txHash, sequenceNumber);
            
            return proof;
        }
    }

    public bool VerifySequenceProof(PoHSequenceProof proof)
    {
        try
        {
            // Verify the cryptographic proof
            var mixedData = new byte[64];
            proof.PreviousHash.Bytes.CopyTo(mixedData, 0);
            proof.TransactionHash.Bytes.CopyTo(mixedData, 32);
            
            var expectedHash = Hash.ComputeHash(mixedData);
            
            if (!expectedHash.Equals(proof.ProofHash))
            {
                _logger.LogWarning("PoH proof verification failed: hash mismatch for sequence {Sequence}", 
                    proof.SequenceNumber);
                return false;
            }
            
            // Verify timing constraints
            var expectedSlot = GetSlotFromTimestamp(proof.Timestamp);
            if (proof.SlotNumber != expectedSlot)
            {
                _logger.LogWarning("PoH proof verification failed: slot mismatch for sequence {Sequence}", 
                    proof.SequenceNumber);
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying PoH proof for sequence {Sequence}", proof.SequenceNumber);
            return false;
        }
    }

    public ulong GetCurrentSlot()
    {
        var elapsed = DateTimeOffset.UtcNow - _genesisTime;
        return (ulong)(elapsed.TotalMicroseconds / (_options.TickIntervalMicroseconds * _ticksPerSlot));
    }

    public ulong GetSlotFromTimestamp(DateTimeOffset timestamp)
    {
        var elapsed = timestamp - _genesisTime;
        return (ulong)(elapsed.TotalMicroseconds / (_options.TickIntervalMicroseconds * _ticksPerSlot));
    }

    public TimeSpan GetSlotDuration()
    {
        return TimeSpan.FromMicroseconds(_options.TickIntervalMicroseconds * _ticksPerSlot);
    }

    public PoHState GetCurrentState()
    {
        lock (_sequenceLock)
        {
            return new PoHState
            {
                CurrentSequence = _currentSequence,
                CurrentHash = _currentHash,
                CurrentSlot = GetCurrentSlot(),
                GenesisTime = _genesisTime,
                IsRunning = _isRunning,
                TicksPerSlot = _ticksPerSlot,
                QueuedEntries = _historyQueue.Count
            };
        }
    }

    public List<PoHSequenceProof> GetRecentProofs(int count = 100)
    {
        var proofs = new List<PoHSequenceProof>();
        var entries = _historyQueue.ToArray();
        
        // Get the most recent proofs
        var recentEntries = entries.TakeLast(count);
        
        foreach (var entry in recentEntries)
        {
            proofs.Add(entry.Proof);
        }
        
        return proofs.OrderByDescending(p => p.SequenceNumber).ToList();
    }

    public PoHBenchmark RunBenchmark(int durationSeconds = 10)
    {
        var startTime = DateTimeOffset.UtcNow;
        var startSequence = _currentSequence;
        
        _logger.LogInformation("Starting PoH benchmark for {Duration} seconds", durationSeconds);
        
        // Let it run for the specified duration
        Thread.Sleep(TimeSpan.FromSeconds(durationSeconds));
        
        var endTime = DateTimeOffset.UtcNow;
        var endSequence = _currentSequence;
        
        var totalTicks = endSequence - startSequence;
        var actualDuration = endTime - startTime;
        var ticksPerSecond = totalTicks / actualDuration.TotalSeconds;
        
        var benchmark = new PoHBenchmark
        {
            StartTime = startTime,
            EndTime = endTime,
            Duration = actualDuration,
            TotalTicks = totalTicks,
            TicksPerSecond = ticksPerSecond,
            TargetTicksPerSecond = 1_000_000.0 / _options.TickIntervalMicroseconds, // 1M microseconds / interval
            Efficiency = ticksPerSecond / (1_000_000.0 / _options.TickIntervalMicroseconds)
        };
        
        _logger.LogInformation("PoH benchmark completed: {TicksPerSecond:F2} ticks/sec (efficiency: {Efficiency:P2})", 
            benchmark.TicksPerSecond, benchmark.Efficiency);
        
        return benchmark;
    }

    private void GeneratePoHSequence(object? state)
    {
        if (!_isRunning) return;
        
        try
        {
            lock (_sequenceLock)
            {
                // Generate next hash in the sequence
                var hashData = _currentHash.Bytes;
                _currentHash = Hash.ComputeHash(hashData);
                _currentSequence++;
                
                // Clean up old entries periodically
                if (_currentSequence % 10000 == 0)
                {
                    CleanupOldEntries();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PoH sequence at tick {Sequence}", _currentSequence);
        }
    }

    private void CleanupOldEntries()
    {
        var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-_options.HistoryRetentionMinutes);
        var removedCount = 0;
        
        while (_historyQueue.TryPeek(out var entry) && entry.CreatedAt < cutoffTime)
        {
            if (_historyQueue.TryDequeue(out _))
            {
                removedCount++;
            }
        }
        
        if (removedCount > 0)
        {
            _logger.LogDebug("Cleaned up {Count} old PoH entries", removedCount);
        }
    }

    public void Dispose()
    {
        _isRunning = false;
        _pohTimer?.Dispose();
        _sha256?.Dispose();
        _logger.LogInformation("Proof of History engine disposed");
    }
}

public class PoHSequenceProof
{
    public required ulong SequenceNumber { get; set; }
    public required Hash TransactionHash { get; set; }
    public required Hash ProofHash { get; set; }
    public required Hash PreviousHash { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
    public required ulong SlotNumber { get; set; }
    public required ulong TicksInSlot { get; set; }
}

public class PoHState
{
    public required ulong CurrentSequence { get; set; }
    public required Hash CurrentHash { get; set; }
    public required ulong CurrentSlot { get; set; }
    public required DateTimeOffset GenesisTime { get; set; }
    public required bool IsRunning { get; set; }
    public required ulong TicksPerSlot { get; set; }
    public required int QueuedEntries { get; set; }
}

public class PoHBenchmark
{
    public required DateTimeOffset StartTime { get; set; }
    public required DateTimeOffset EndTime { get; set; }
    public required TimeSpan Duration { get; set; }
    public required ulong TotalTicks { get; set; }
    public required double TicksPerSecond { get; set; }
    public required double TargetTicksPerSecond { get; set; }
    public required double Efficiency { get; set; }
}

public class HistoryEntry
{
    public required PoHSequenceProof Proof { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}

public class ProofOfHistoryOptions
{
    public double TickIntervalMicroseconds { get; set; } = 1.0; // 1 microsecond per tick
    public ulong TicksPerSlot { get; set; } = 400_000; // 400ms slots
    public int HistoryRetentionMinutes { get; set; } = 60;
    public bool EnableBenchmarking { get; set; } = true;
    public int MaxQueueSize { get; set; } = 100_000;
}
