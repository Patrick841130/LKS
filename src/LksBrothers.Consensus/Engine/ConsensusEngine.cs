using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using LksBrothers.Consensus.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace LksBrothers.Consensus.Engine;

/// <summary>
/// Main consensus engine implementing Proof of Stake with BFT finality
/// </summary>
public class ConsensusEngine : IConsensusEngine
{
    private readonly ILogger<ConsensusEngine> _logger;
    private readonly IValidatorSet _validatorSet;
    private readonly ISlotScheduler _slotScheduler;
    private readonly IBftFinalizer _bftFinalizer;
    private readonly Channel<ConsensusMessage> _messageChannel;
    
    private readonly ConcurrentDictionary<Hash, Block> _proposedBlocks = new();
    private readonly ConcurrentDictionary<ulong, List<Attestation>> _attestations = new();
    private readonly ConcurrentDictionary<Hash, BftVote> _bftVotes = new();
    
    private Block? _headBlock;
    private ulong _currentSlot;
    private ulong _currentEpoch;
    private bool _isRunning;
    
    public event EventHandler<BlockProposedEventArgs>? BlockProposed;
    public event EventHandler<BlockFinalizedEventArgs>? BlockFinalized;
    public event EventHandler<ValidatorSlashedEventArgs>? ValidatorSlashed;
    
    public ConsensusEngine(
        ILogger<ConsensusEngine> logger,
        IValidatorSet validatorSet,
        ISlotScheduler slotScheduler,
        IBftFinalizer bftFinalizer)
    {
        _logger = logger;
        _validatorSet = validatorSet;
        _slotScheduler = slotScheduler;
        _bftFinalizer = bftFinalizer;
        _messageChannel = Channel.CreateUnbounded<ConsensusMessage>();
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting consensus engine");
        _isRunning = true;
        
        // Start background tasks
        var tasks = new[]
        {
            ProcessMessagesAsync(cancellationToken),
            SlotTimerAsync(cancellationToken),
            AttestationAggregatorAsync(cancellationToken),
            BftFinalizerAsync(cancellationToken)
        };
        
        await Task.WhenAll(tasks);
    }
    
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping consensus engine");
        _isRunning = false;
        _messageChannel.Writer.Complete();
    }
    
    public async Task<bool> ProposeBlockAsync(Block block, Validator proposer)
    {
        try
        {
            // Validate proposer eligibility
            if (!proposer.IsEligibleForProposal(_currentSlot, _currentEpoch))
            {
                _logger.LogWarning("Validator {Address} not eligible for proposal at slot {Slot}", 
                    proposer.Address, _currentSlot);
                return false;
            }
            
            // Validate block
            var validation = block.Validate(_headBlock);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Invalid block proposed: {Errors}", 
                    string.Join(", ", validation.Errors));
                return false;
            }
            
            // Set block metadata
            block.Slot = _currentSlot;
            block.Proposer = proposer.Address;
            block.Hash = block.CalculateHash();
            
            // Store proposed block
            _proposedBlocks[block.Hash] = block;
            
            // Broadcast block proposal
            var message = new BlockProposalMessage
            {
                Block = block,
                Proposer = proposer.Address,
                Slot = _currentSlot,
                Signature = SignBlock(block, proposer)
            };
            
            await _messageChannel.Writer.WriteAsync(message);
            
            _logger.LogInformation("Block proposed by {Proposer} at slot {Slot} with hash {Hash}",
                proposer.Address, _currentSlot, block.Hash);
            
            BlockProposed?.Invoke(this, new BlockProposedEventArgs(block, proposer));
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proposing block");
            return false;
        }
    }
    
    public async Task<bool> AttestAsync(Hash blockHash, Validator validator)
    {
        try
        {
            if (!validator.IsEligibleForAttestation(_currentSlot, _currentEpoch))
            {
                _logger.LogWarning("Validator {Address} not eligible for attestation", validator.Address);
                return false;
            }
            
            var attestation = new Attestation
            {
                BlockHash = blockHash,
                Slot = _currentSlot,
                Validator = validator.Address,
                Signature = SignAttestation(blockHash, validator),
                Timestamp = DateTime.UtcNow
            };
            
            // Store attestation
            if (!_attestations.ContainsKey(_currentSlot))
                _attestations[_currentSlot] = new List<Attestation>();
            
            _attestations[_currentSlot].Add(attestation);
            
            // Broadcast attestation
            var message = new AttestationMessage { Attestation = attestation };
            await _messageChannel.Writer.WriteAsync(message);
            
            _logger.LogDebug("Attestation created by {Validator} for block {BlockHash}",
                validator.Address, blockHash);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating attestation");
            return false;
        }
    }
    
    public async Task<bool> VoteBftAsync(Hash blockHash, BftVoteType voteType, Validator validator)
    {
        try
        {
            var vote = new BftVote
            {
                BlockHash = blockHash,
                VoteType = voteType,
                Validator = validator.Address,
                Epoch = _currentEpoch,
                Signature = SignBftVote(blockHash, voteType, validator),
                Timestamp = DateTime.UtcNow
            };
            
            _bftVotes[Hash.Compute($"{blockHash}:{voteType}:{validator.Address}")] = vote;
            
            var message = new BftVoteMessage { Vote = vote };
            await _messageChannel.Writer.WriteAsync(message);
            
            _logger.LogDebug("BFT vote cast by {Validator} for block {BlockHash}: {VoteType}",
                validator.Address, blockHash, voteType);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error casting BFT vote");
            return false;
        }
    }
    
    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await ProcessMessage(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing consensus message");
            }
        }
    }
    
    private async Task ProcessMessage(ConsensusMessage message)
    {
        switch (message)
        {
            case BlockProposalMessage proposal:
                await ProcessBlockProposal(proposal);
                break;
            case AttestationMessage attestation:
                await ProcessAttestation(attestation);
                break;
            case BftVoteMessage vote:
                await ProcessBftVote(vote);
                break;
        }
    }
    
    private async Task ProcessBlockProposal(BlockProposalMessage proposal)
    {
        // Verify proposer signature
        if (!VerifyBlockSignature(proposal.Block, proposal.Proposer, proposal.Signature))
        {
            _logger.LogWarning("Invalid block signature from {Proposer}", proposal.Proposer);
            return;
        }
        
        // Check if we already have this block
        if (_proposedBlocks.ContainsKey(proposal.Block.Hash))
            return;
        
        // Validate block
        var validation = proposal.Block.Validate(_headBlock);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Invalid block received: {Errors}", 
                string.Join(", ", validation.Errors));
            return;
        }
        
        _proposedBlocks[proposal.Block.Hash] = proposal.Block;
        _logger.LogInformation("Received valid block proposal {Hash} from {Proposer}",
            proposal.Block.Hash, proposal.Proposer);
        
        // Auto-attest if we're a validator
        var ourValidator = await _validatorSet.GetOurValidatorAsync();
        if (ourValidator != null)
        {
            await AttestAsync(proposal.Block.Hash, ourValidator);
        }
    }
    
    private async Task ProcessAttestation(AttestationMessage attestationMsg)
    {
        var attestation = attestationMsg.Attestation;
        
        // Verify attestation signature
        var validator = await _validatorSet.GetValidatorAsync(attestation.Validator);
        if (validator == null || !VerifyAttestationSignature(attestation, validator))
        {
            _logger.LogWarning("Invalid attestation signature from {Validator}", attestation.Validator);
            return;
        }
        
        // Store attestation
        if (!_attestations.ContainsKey(attestation.Slot))
            _attestations[attestation.Slot] = new List<Attestation>();
        
        _attestations[attestation.Slot].Add(attestation);
        
        // Check if we have enough attestations for finalization
        await CheckForFinalization(attestation.BlockHash);
    }
    
    private async Task ProcessBftVote(BftVoteMessage voteMsg)
    {
        var vote = voteMsg.Vote;
        
        // Verify vote signature
        var validator = await _validatorSet.GetValidatorAsync(vote.Validator);
        if (validator == null || !VerifyBftVoteSignature(vote, validator))
        {
            _logger.LogWarning("Invalid BFT vote signature from {Validator}", vote.Validator);
            return;
        }
        
        var voteKey = Hash.Compute($"{vote.BlockHash}:{vote.VoteType}:{vote.Validator}");
        _bftVotes[voteKey] = vote;
        
        // Check for BFT finalization
        await _bftFinalizer.ProcessVoteAsync(vote);
    }
    
    private async Task SlotTimerAsync(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            var nextSlotTime = _slotScheduler.GetNextSlotTime();
            var delay = nextSlotTime - DateTime.UtcNow;
            
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);
            
            await AdvanceSlot();
        }
    }
    
    private async Task AdvanceSlot()
    {
        _currentSlot++;
        _currentEpoch = _currentSlot / 32; // 32 slots per epoch
        
        _logger.LogInformation("Advanced to slot {Slot}, epoch {Epoch}", _currentSlot, _currentEpoch);
        
        // Check if we're the proposer for this slot
        var proposer = await _slotScheduler.GetProposerForSlotAsync(_currentSlot);
        var ourValidator = await _validatorSet.GetOurValidatorAsync();
        
        if (proposer != null && ourValidator != null && proposer.Address == ourValidator.Address)
        {
            _logger.LogInformation("We are the proposer for slot {Slot}", _currentSlot);
            await CreateAndProposeBlockAsync(ourValidator);
        }
    }

    private async Task CreateAndProposeBlockAsync(Validator validator)
    {
        try
        {
            // Get pending transactions from transaction pool
            var pendingTransactions = await GetPendingTransactionsAsync();
            
            // Create new block
            var block = new Block
            {
                Number = _headBlock?.Number + 1 ?? 0,
                ParentHash = _headBlock?.Hash ?? Hash.Zero,
                Timestamp = DateTime.UtcNow,
                GasLimit = new UInt256(15_000_000), // 15M gas limit
                BaseFee = new UInt256(1_000_000_000), // 1 Gwei
                Transactions = pendingTransactions.Take(1000).ToList() // Max 1000 transactions per block
            };
            
            // Calculate block hash
            block.Hash = block.CalculateHash();
            
            // Sign the block
            var signature = SignBlock(block, validator);
            
            // Store proposed block
            _proposedBlocks[block.Hash] = block;
            
            // Broadcast block proposal
            await ProposeBlockAsync(block, validator);
            
            _logger.LogInformation("Proposed block {Number} with hash {Hash} containing {TxCount} transactions",
                block.Number, block.Hash, block.Transactions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating and proposing block");
        }
    }

    private async Task<List<Transaction>> GetPendingTransactionsAsync()
    {
        // In a real implementation, this would get transactions from the transaction pool
        // For now, return empty list
        await Task.CompletedTask;
        return new List<Transaction>();
    }
    
    private async Task AttestationAggregatorAsync(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(6), cancellationToken); // Half slot time
            
            // Aggregate attestations for previous slots
            await AggregateAttestations();
        }
    }
    
    private async Task AggregateAttestations()
    {
        var slotsToProcess = _attestations.Keys
            .Where(slot => slot < _currentSlot)
            .OrderBy(slot => slot)
            .Take(10); // Process up to 10 slots at a time
        
        foreach (var slot in slotsToProcess)
        {
            if (_attestations.TryRemove(slot, out var attestations))
            {
                var groupedAttestations = attestations
                    .GroupBy(a => a.BlockHash)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault();
                
                if (groupedAttestations != null && groupedAttestations.Count() > 0)
                {
                    var blockHash = groupedAttestations.Key;
                    var attestationCount = groupedAttestations.Count();
                    
                    _logger.LogInformation("Aggregated {Count} attestations for block {BlockHash} at slot {Slot}",
                        attestationCount, blockHash, slot);
                    
                    // Check for supermajority (2/3+ of validators)
                    var totalValidators = await _validatorSet.GetActiveValidatorCountAsync();
                    if (attestationCount >= (totalValidators * 2 / 3))
                    {
                        await CheckForFinalization(blockHash);
                    }
                }
            }
        }
    }
    
    private async Task BftFinalizerAsync(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(12), cancellationToken); // One slot time
            
            await _bftFinalizer.CheckFinalizationAsync();
        }
    }
    
    private async Task CheckForFinalization(Hash blockHash)
    {
        if (!_proposedBlocks.TryGetValue(blockHash, out var block))
            return;
        
        var attestationCount = _attestations.Values
            .SelectMany(attestations => attestations)
            .Count(a => a.BlockHash == blockHash);
        
        var totalValidators = await _validatorSet.GetActiveValidatorCountAsync();
        
        if (attestationCount >= (totalValidators * 2 / 3))
        {
            _headBlock = block;
            _logger.LogInformation("Block {Hash} finalized with {Attestations}/{Total} attestations",
                blockHash, attestationCount, totalValidators);
            
            BlockFinalized?.Invoke(this, new BlockFinalizedEventArgs(block, attestationCount));
        }
    }
    
    private byte[] SignBlock(Block block, Validator validator)
    {
        try
        {
            // Create message to sign (block hash + validator address + slot)
            var message = new List<byte>();
            message.AddRange(block.Hash.ToByteArray());
            message.AddRange(validator.Address.ToByteArray());
            message.AddRange(BitConverter.GetBytes(_currentSlot));
            
            // Simulate secp256k1 signature (64 bytes)
            var messageHash = System.Security.Cryptography.SHA256.HashData(message.ToArray());
            var signature = new byte[64];
            
            // Fill with deterministic data based on message hash
            for (int i = 0; i < 32; i++)
            {
                signature[i] = messageHash[i];
                signature[i + 32] = (byte)(messageHash[i] ^ 0xFF);
            }
            
            return signature;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing block");
            return new byte[64];
        }
    }
    
    private byte[] SignAttestation(Hash blockHash, Validator validator)
    {
        try
        {
            // Create attestation message to sign
            var message = new List<byte>();
            message.AddRange(blockHash.ToByteArray());
            message.AddRange(validator.Address.ToByteArray());
            message.AddRange(BitConverter.GetBytes(_currentSlot));
            message.AddRange(System.Text.Encoding.UTF8.GetBytes("ATTESTATION"));
            
            // Generate deterministic signature
            var messageHash = System.Security.Cryptography.SHA256.HashData(message.ToArray());
            var signature = new byte[64];
            
            for (int i = 0; i < 32; i++)
            {
                signature[i] = (byte)(messageHash[i] ^ 0xAA);
                signature[i + 32] = (byte)(messageHash[i] ^ 0x55);
            }
            
            return signature;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing attestation");
            return new byte[64];
        }
    }
    
    private byte[] SignBftVote(Hash blockHash, BftVoteType voteType, Validator validator)
    {
        try
        {
            // Create BFT vote message to sign
            var message = new List<byte>();
            message.AddRange(blockHash.ToByteArray());
            message.AddRange(validator.Address.ToByteArray());
            message.AddRange(BitConverter.GetBytes((int)voteType));
            message.AddRange(BitConverter.GetBytes(_currentSlot));
            message.AddRange(System.Text.Encoding.UTF8.GetBytes("BFT_VOTE"));
            
            // Generate deterministic signature
            var messageHash = System.Security.Cryptography.SHA256.HashData(message.ToArray());
            var signature = new byte[64];
            
            for (int i = 0; i < 32; i++)
            {
                signature[i] = (byte)(messageHash[i] ^ 0xCC);
                signature[i + 32] = (byte)(messageHash[i] ^ 0x33);
            }
            
            return signature;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing BFT vote");
            return new byte[64];
        }
    }
    
    private bool VerifyBlockSignature(Block block, Address proposer, byte[] signature)
    {
        try
        {
            if (signature.Length != 64)
                return false;
            
            // Recreate the message that should have been signed
            var message = new List<byte>();
            message.AddRange(block.Hash.ToByteArray());
            message.AddRange(proposer.ToByteArray());
            message.AddRange(BitConverter.GetBytes(_currentSlot));
            
            var messageHash = System.Security.Cryptography.SHA256.HashData(message.ToArray());
            
            // Verify signature matches expected pattern
            for (int i = 0; i < 32; i++)
            {
                if (signature[i] != messageHash[i] || signature[i + 32] != (byte)(messageHash[i] ^ 0xFF))
                    return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying block signature");
            return false;
        }
    }
    
    private bool VerifyAttestationSignature(Attestation attestation, Validator validator)
    {
        try
        {
            if (attestation.Signature.Length != 64)
                return false;
            
            // Recreate the attestation message
            var message = new List<byte>();
            message.AddRange(attestation.BlockHash.ToByteArray());
            message.AddRange(validator.Address.ToByteArray());
            message.AddRange(BitConverter.GetBytes(attestation.Slot));
            message.AddRange(System.Text.Encoding.UTF8.GetBytes("ATTESTATION"));
            
            var messageHash = System.Security.Cryptography.SHA256.HashData(message.ToArray());
            
            // Verify signature pattern
            for (int i = 0; i < 32; i++)
            {
                if (attestation.Signature[i] != (byte)(messageHash[i] ^ 0xAA) || 
                    attestation.Signature[i + 32] != (byte)(messageHash[i] ^ 0x55))
                    return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying attestation signature");
            return false;
        }
    }
    
    private bool VerifyBftVoteSignature(BftVote vote, Validator validator)
    {
        try
        {
            if (vote.Signature.Length != 64)
                return false;
            
            // Recreate the BFT vote message
            var message = new List<byte>();
            message.AddRange(vote.BlockHash.ToByteArray());
            message.AddRange(validator.Address.ToByteArray());
            message.AddRange(BitConverter.GetBytes((int)vote.VoteType));
            message.AddRange(BitConverter.GetBytes(vote.Slot));
            message.AddRange(System.Text.Encoding.UTF8.GetBytes("BFT_VOTE"));
            
            var messageHash = System.Security.Cryptography.SHA256.HashData(message.ToArray());
            
            // Verify signature pattern
            for (int i = 0; i < 32; i++)
            {
                if (vote.Signature[i] != (byte)(messageHash[i] ^ 0xCC) || 
                    vote.Signature[i + 32] != (byte)(messageHash[i] ^ 0x33))
                    return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying BFT vote signature");
            return false;
        }
    }
}

// Interfaces and supporting classes
public interface IConsensusEngine
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<bool> ProposeBlockAsync(Block block, Validator proposer);
    Task<bool> AttestAsync(Hash blockHash, Validator validator);
    Task<bool> VoteBftAsync(Hash blockHash, BftVoteType voteType, Validator validator);
}

public interface IValidatorSet
{
    Task<Validator?> GetValidatorAsync(Address address);
    Task<Validator?> GetOurValidatorAsync();
    Task<int> GetActiveValidatorCountAsync();
    Task<List<Validator>> GetActiveValidatorsAsync();
}

public interface ISlotScheduler
{
    DateTime GetNextSlotTime();
    Task<Validator?> GetProposerForSlotAsync(ulong slot);
}

public interface IBftFinalizer
{
    Task ProcessVoteAsync(BftVote vote);
    Task CheckFinalizationAsync();
}

public abstract class ConsensusMessage { }

public class BlockProposalMessage : ConsensusMessage
{
    public Block Block { get; set; } = new();
    public Address Proposer { get; set; } = Address.Zero;
    public ulong Slot { get; set; }
    public byte[] Signature { get; set; } = Array.Empty<byte>();
}

public class AttestationMessage : ConsensusMessage
{
    public Attestation Attestation { get; set; } = new();
}

public class BftVoteMessage : ConsensusMessage
{
    public BftVote Vote { get; set; } = new();
}

public class Attestation
{
    public Hash BlockHash { get; set; } = Hash.Zero;
    public ulong Slot { get; set; }
    public Address Validator { get; set; } = Address.Zero;
    public byte[] Signature { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class BftVote
{
    public Hash BlockHash { get; set; } = Hash.Zero;
    public BftVoteType VoteType { get; set; }
    public Address Validator { get; set; } = Address.Zero;
    public ulong Epoch { get; set; }
    public byte[] Signature { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum BftVoteType
{
    Prevote,
    Precommit
}

public class BlockProposedEventArgs : EventArgs
{
    public Block Block { get; }
    public Validator Proposer { get; }
    
    public BlockProposedEventArgs(Block block, Validator proposer)
    {
        Block = block;
        Proposer = proposer;
    }
}

public class BlockFinalizedEventArgs : EventArgs
{
    public Block Block { get; }
    public int AttestationCount { get; }
    
    public BlockFinalizedEventArgs(Block block, int attestationCount)
    {
        Block = block;
        AttestationCount = attestationCount;
    }
}

public class ValidatorSlashedEventArgs : EventArgs
{
    public Validator Validator { get; }
    public string Reason { get; }
    public UInt256 Amount { get; }
    
    public ValidatorSlashedEventArgs(Validator validator, string reason, UInt256 amount)
    {
        Validator = validator;
        Reason = reason;
        Amount = amount;
    }
}
