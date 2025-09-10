using Microsoft.Extensions.Logging;
using LksBrothers.Core.Models;
using LksBrothers.Core.Cryptography;
using LksBrothers.Consensus.Engine;
using LksBrothers.StateManagement.Services;
using LksBrothers.Hooks.Engine;
using MessagePack;
using System.Text.Json;

namespace LksBrothers.Genesis.Services;

public class GenesisService
{
    private readonly ILogger<GenesisService> _logger;
    private readonly StateService _stateService;
    private readonly ProofOfHistoryEngine _pohEngine;
    private readonly HookExecutor _hookExecutor;

    public GenesisService(
        ILogger<GenesisService> logger,
        StateService stateService,
        ProofOfHistoryEngine pohEngine,
        HookExecutor hookExecutor)
    {
        _logger = logger;
        _stateService = stateService;
        _pohEngine = pohEngine;
        _hookExecutor = hookExecutor;
    }

    public async Task<Block> CreateGenesisBlockAsync(GenesisConfiguration config)
    {
        _logger.LogInformation("Creating genesis block for LKS COIN mainnet...");

        // Create genesis transactions
        var genesisTransactions = await CreateGenesisTransactionsAsync(config);
        
        // Create initial PoH sequence
        var pohProofs = await CreateInitialPoHSequenceAsync();
        
        // Create genesis block
        var genesisBlock = new Block
        {
            Number = 0,
            PreviousHash = Hash.Zero,
            Timestamp = config.GenesisTime,
            Transactions = genesisTransactions,
            SlotNumber = 0,
            ProposerAddress = Address.Parse(config.FoundationAddress),
            PoHProofs = pohProofs,
            StateRoot = Hash.Zero, // Will be calculated after state initialization
            TransactionRoot = CalculateTransactionRoot(genesisTransactions),
            ReceiptRoot = Hash.Zero,
            Difficulty = UInt256.One,
            GasLimit = 30000000,
            GasUsed = 0,
            ExtraData = System.Text.Encoding.UTF8.GetBytes("LKS COIN Genesis Block - The Future of Zero-Fee Blockchain"),
            Nonce = 0
        };

        // Calculate block hash
        genesisBlock.Hash = CalculateBlockHash(genesisBlock);
        
        // Initialize state with genesis data
        await InitializeGenesisStateAsync(genesisBlock, config);
        
        // Update state root
        genesisBlock.StateRoot = await _stateService.GetStateRootAsync();

        // Recalculate hash with final state root
        genesisBlock.Hash = CalculateBlockHash(genesisBlock);

        _logger.LogInformation("Genesis block created with hash: {Hash}", genesisBlock.Hash);
        return genesisBlock;
    }

    private async Task<List<Transaction>> CreateGenesisTransactionsAsync(GenesisConfiguration config)
    {
        var transactions = new List<Transaction>();

        // 1. Foundation allocation transaction
        var foundationTx = new Transaction
        {
            Hash = Hash.FromString("genesis_foundation_allocation"),
            From = Address.Zero,
            To = Address.Parse(config.FoundationAddress),
            Value = config.InitialSupply * 40 / 100, // 40% to foundation
            Gas = 21000,
            GasPrice = UInt256.Zero, // Zero fees
            Nonce = 0,
            Data = System.Text.Encoding.UTF8.GetBytes("Foundation Reserve Allocation"),
            Timestamp = config.GenesisTime,
            BlockNumber = 0
        };
        transactions.Add(foundationTx);

        // 2. Validator stake transactions
        for (int i = 0; i < config.InitialValidators.Count; i++)
        {
            var validator = config.InitialValidators[i];
            var stakeTx = new Transaction
            {
                Hash = Hash.FromString($"genesis_validator_stake_{i}"),
                From = Address.Zero,
                To = Address.Parse(validator.Address),
                Value = validator.Stake,
                Gas = 21000,
                GasPrice = UInt256.Zero,
                Nonce = (ulong)(i + 1),
                Data = System.Text.Encoding.UTF8.GetBytes($"Validator Stake: {validator.Address}"),
                Timestamp = config.GenesisTime,
                BlockNumber = 0
            };
            transactions.Add(stakeTx);
        }

        // 3. Public distribution allocation
        var publicDistributionTx = new Transaction
        {
            Hash = Hash.FromString("genesis_public_distribution"),
            From = Address.Zero,
            To = Address.Parse("lks1public000000000000000000000000000000000"),
            Value = config.InitialSupply * 30 / 100, // 30% for public
            Gas = 21000,
            GasPrice = UInt256.Zero,
            Nonce = (ulong)(config.InitialValidators.Count + 1),
            Data = System.Text.Encoding.UTF8.GetBytes("Public Distribution Reserve"),
            Timestamp = config.GenesisTime,
            BlockNumber = 0
        };
        transactions.Add(publicDistributionTx);

        // 4. Development fund allocation
        var devFundTx = new Transaction
        {
            Hash = Hash.FromString("genesis_development_fund"),
            From = Address.Zero,
            To = Address.Parse("lks1devfund000000000000000000000000000000000"),
            Value = config.InitialSupply * 10 / 100, // 10% for development
            Gas = 21000,
            GasPrice = UInt256.Zero,
            Nonce = (ulong)(config.InitialValidators.Count + 2),
            Data = System.Text.Encoding.UTF8.GetBytes("Development Fund Allocation"),
            Timestamp = config.GenesisTime,
            BlockNumber = 0
        };
        transactions.Add(devFundTx);

        // 5. Validator rewards pool
        var rewardsTx = new Transaction
        {
            Hash = Hash.FromString("genesis_validator_rewards"),
            From = Address.Zero,
            To = Address.Parse("lks1rewards000000000000000000000000000000000"),
            Value = config.InitialSupply * 15 / 100, // 15% for rewards
            Gas = 21000,
            GasPrice = UInt256.Zero,
            Nonce = (ulong)(config.InitialValidators.Count + 3),
            Data = System.Text.Encoding.UTF8.GetBytes("Validator Rewards Pool"),
            Timestamp = config.GenesisTime,
            BlockNumber = 0
        };
        transactions.Add(rewardsTx);

        // 6. Strategic partners allocation
        var partnersTx = new Transaction
        {
            Hash = Hash.FromString("genesis_strategic_partners"),
            From = Address.Zero,
            To = Address.Parse("lks1partners000000000000000000000000000000000"),
            Value = config.InitialSupply * 5 / 100, // 5% for partners
            Gas = 21000,
            GasPrice = UInt256.Zero,
            Nonce = (ulong)(config.InitialValidators.Count + 4),
            Data = System.Text.Encoding.UTF8.GetBytes("Strategic Partners Allocation"),
            Timestamp = config.GenesisTime,
            BlockNumber = 0
        };
        transactions.Add(partnersTx);

        return transactions;
    }

    private async Task<List<PoHSequenceProof>> CreateInitialPoHSequenceAsync()
    {
        _logger.LogInformation("Creating initial Proof of History sequence...");
        
        var proofs = new List<PoHSequenceProof>();
        
        // Create initial PoH sequence with 10 proofs
        var previousHash = Hash.Zero;
        for (int i = 0; i < 10; i++)
        {
            var proof = new PoHSequenceProof
            {
                SequenceNumber = (ulong)i,
                Hash = Hash.FromString($"genesis_poh_{i}_{previousHash}"),
                PreviousHash = previousHash,
                Timestamp = DateTimeOffset.UtcNow.AddMilliseconds(i * 40), // 40ms intervals
                TickCount = (ulong)(i * 1000) // Simulated tick count
            };
            
            proofs.Add(proof);
            previousHash = proof.Hash;
        }

        return proofs;
    }

    private async Task InitializeGenesisStateAsync(Block genesisBlock, GenesisConfiguration config)
    {
        _logger.LogInformation("Initializing genesis state...");

        // Initialize accounts with balances
        foreach (var tx in genesisBlock.Transactions)
        {
            if (tx.To != Address.Zero)
            {
                await _stateService.SetBalanceAsync(tx.To, tx.Value);
                _logger.LogDebug("Set balance for {Address}: {Balance}", tx.To, tx.Value);
            }
        }

        // Initialize validator set
        foreach (var validator in config.InitialValidators)
        {
            var validatorState = new ValidatorState
            {
                Address = Address.Parse(validator.Address),
                PublicKey = validator.PublicKey,
                Stake = validator.Stake,
                Commission = validator.Commission,
                IsActive = true,
                JoinedEpoch = 0,
                LastActiveEpoch = 0
            };

            await _stateService.SetValidatorAsync(validatorState.Address, validatorState);
            _logger.LogDebug("Initialized validator: {Address}", validator.Address);
        }

        // Set network parameters
        await _stateService.SetNetworkParameterAsync("chain_id", config.ChainId.ToString());
        await _stateService.SetNetworkParameterAsync("genesis_time", config.GenesisTime.ToUnixTimeSeconds().ToString());
        await _stateService.SetNetworkParameterAsync("slot_duration_ms", config.SlotDuration.TotalMilliseconds.ToString());
        await _stateService.SetNetworkParameterAsync("epoch_length", config.EpochLength.ToString());
        await _stateService.SetNetworkParameterAsync("validator_stake_required", config.ValidatorStakeRequired.ToString());

        _logger.LogInformation("Genesis state initialized successfully");
    }

    private Hash CalculateTransactionRoot(List<Transaction> transactions)
    {
        if (!transactions.Any()) return Hash.Zero;
        
        var hashes = transactions.Select(tx => tx.Hash.ToByteArray()).ToArray();
        return Hash.FromByteArray(Sha256.Hash(hashes.SelectMany(h => h).ToArray()));
    }

    private Hash CalculateBlockHash(Block block)
    {
        var blockData = new
        {
            block.Number,
            block.PreviousHash,
            block.Timestamp,
            block.StateRoot,
            block.TransactionRoot,
            block.ReceiptRoot,
            block.SlotNumber,
            block.ProposerAddress,
            TransactionCount = block.Transactions.Count,
            PoHCount = block.PoHProofs?.Count ?? 0
        };

        var serialized = MessagePackSerializer.Serialize(blockData);
        return Hash.FromByteArray(Sha256.Hash(serialized));
    }

    public async Task SaveGenesisDataAsync(Block genesisBlock, GenesisConfiguration config)
    {
        _logger.LogInformation("Saving genesis data to files...");

        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "genesis_output");
        Directory.CreateDirectory(outputDir);

        // Save genesis block
        var blockJson = JsonSerializer.Serialize(genesisBlock, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Converters = { new UInt256JsonConverter(), new HashJsonConverter(), new AddressJsonConverter() }
        });
        await File.WriteAllTextAsync(Path.Combine(outputDir, "genesis_block.json"), blockJson);

        // Save genesis configuration
        var configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Converters = { new UInt256JsonConverter() }
        });
        await File.WriteAllTextAsync(Path.Combine(outputDir, "genesis_config.json"), configJson);

        // Save validator set
        var validatorsJson = JsonSerializer.Serialize(config.InitialValidators, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Converters = { new UInt256JsonConverter() }
        });
        await File.WriteAllTextAsync(Path.Combine(outputDir, "initial_validators.json"), validatorsJson);

        // Save network parameters
        var networkParams = new
        {
            ChainId = config.ChainId,
            NetworkName = config.NetworkName,
            GenesisHash = genesisBlock.Hash.ToString(),
            GenesisTime = config.GenesisTime,
            SlotDuration = config.SlotDuration,
            EpochLength = config.EpochLength,
            InitialSupply = config.InitialSupply,
            ValidatorStakeRequired = config.ValidatorStakeRequired
        };
        var paramsJson = JsonSerializer.Serialize(networkParams, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Converters = { new UInt256JsonConverter(), new HashJsonConverter() }
        });
        await File.WriteAllTextAsync(Path.Combine(outputDir, "network_parameters.json"), paramsJson);

        _logger.LogInformation("Genesis data saved to: {OutputDir}", outputDir);
    }
}
