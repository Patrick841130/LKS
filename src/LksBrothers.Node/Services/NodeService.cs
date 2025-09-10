using LksBrothers.Consensus.Engine;
using LksBrothers.Core.Models;
using LksBrothers.Execution.Engine;
using LksBrothers.Networking.P2P;
using LksBrothers.Stablecoin.Engine;
using LksBrothers.Node.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MessagePack;

namespace LksBrothers.Node.Services;

/// <summary>
/// Main node service that coordinates all blockchain components
/// </summary>
public class NodeService : BackgroundService
{
    private readonly ILogger<NodeService> _logger;
    private readonly NodeConfiguration _config;
    private readonly IConsensusEngine _consensusEngine;
    private readonly IExecutionEngine _executionEngine;
    private readonly IStablecoinEngine _stablecoinEngine;
    private readonly INetworkManager _networkManager;
    private readonly IBlockchainService _blockchainService;
    private readonly IValidatorService _validatorService;
    private readonly ITransactionPool _transactionPool;
    private readonly IStateService _stateService;
    
    public NodeService(
        ILogger<NodeService> logger,
        IOptions<NodeConfiguration> config,
        IConsensusEngine consensusEngine,
        IExecutionEngine executionEngine,
        IStablecoinEngine stablecoinEngine,
        INetworkManager networkManager,
        IBlockchainService blockchainService,
        IValidatorService validatorService,
        ITransactionPool transactionPool,
        IStateService stateService)
    {
        _logger = logger;
        _config = config.Value;
        _consensusEngine = consensusEngine;
        _executionEngine = executionEngine;
        _stablecoinEngine = stablecoinEngine;
        _networkManager = networkManager;
        _blockchainService = blockchainService;
        _validatorService = validatorService;
        _transactionPool = transactionPool;
        _stateService = stateService;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting LKS Brothers node services");
            
            // Initialize data directory
            await InitializeDataDirectoryAsync();
            
            // Initialize state service
            await _stateService.InitializeAsync();
            
            // Load blockchain state
            await _blockchainService.InitializeAsync();
            
            // Initialize validator if configured
            if (_config.IsValidator)
            {
                await InitializeValidatorAsync();
            }
            
            // Register default stablecoins
            await RegisterDefaultStablecoinsAsync();
            
            // Start core services
            await StartCoreServicesAsync(stoppingToken);
            
            _logger.LogInformation("LKS Brothers node started successfully");
            
            // Keep running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Node service is shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in node service");
            throw;
        }
    }
    
    private async Task InitializeDataDirectoryAsync()
    {
        var dataDir = _config.DataDirectory;
        
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
            _logger.LogInformation("Created data directory: {DataDirectory}", dataDir);
        }
        
        // Create subdirectories
        var subdirs = new[] { "blocks", "state", "transactions", "validators", "logs" };
        foreach (var subdir in subdirs)
        {
            var path = Path.Combine(dataDir, subdir);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
    
    private async Task InitializeValidatorAsync()
    {
        if (string.IsNullOrEmpty(_config.ValidatorKeyFile))
        {
            _logger.LogWarning("Validator mode enabled but no key file specified");
            return;
        }
        
        if (!File.Exists(_config.ValidatorKeyFile))
        {
            _logger.LogError("Validator key file not found: {KeyFile}", _config.ValidatorKeyFile);
            return;
        }
        
        await _validatorService.LoadValidatorKeyAsync(_config.ValidatorKeyFile);
        _logger.LogInformation("Validator initialized successfully");
    }
    
    private async Task RegisterDefaultStablecoinsAsync()
    {
        // Register LKSUSD - the primary stablecoin
        var lksUsd = new LksBrothers.Stablecoin.Engine.StablecoinInfo
        {
            Symbol = "LKSUSD",
            Name = "LKS Brothers USD",
            CollateralRatio = 1.5m, // 150% collateralization
            IsActive = true
        };
        
        await _stablecoinEngine.RegisterStablecoinAsync(lksUsd);
        
        // Register LKSEUR - Euro stablecoin
        var lksEur = new LksBrothers.Stablecoin.Engine.StablecoinInfo
        {
            Symbol = "LKSEUR",
            Name = "LKS Brothers EUR",
            CollateralRatio = 1.5m,
            IsActive = true
        };
        
        await _stablecoinEngine.RegisterStablecoinAsync(lksEur);
        
        _logger.LogInformation("Registered default stablecoins: LKSUSD, LKSEUR");
    }
    
    private async Task StartCoreServicesAsync(CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        
        // Start networking
        tasks.Add(_networkManager.StartAsync(cancellationToken));
        
        // Start consensus engine
        tasks.Add(_consensusEngine.StartAsync(cancellationToken));
        
        _logger.LogInformation("Started core blockchain services");
        
        // Don't await all tasks here as they run indefinitely
        // Just start them and let them run in background
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping node services");
        
        try
        {
            // Stop services in reverse order
            await _consensusEngine.StopAsync(cancellationToken);
            await _networkManager.StopAsync(cancellationToken);
            
            _logger.LogInformation("Node services stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping node services");
        }
        
        await base.StopAsync(cancellationToken);
    }
}

// Supporting service interfaces and implementations
public interface IBlockchainService
{
    Task InitializeAsync();
    Task<Block?> GetLatestBlockAsync();
    Task<Block?> GetBlockAsync(ulong number);
    Task<bool> AddBlockAsync(Block block);
}

public interface IValidatorService
{
    Task LoadValidatorKeyAsync(string keyFile);
    Task<bool> IsValidatorAsync();
    Task<LksBrothers.Consensus.Models.Validator?> GetValidatorAsync();
}

public interface ITransactionPool
{
    Task<bool> AddTransactionAsync(Transaction transaction);
    Task<List<Transaction>> GetPendingTransactionsAsync(int maxCount = 1000);
    Task RemoveTransactionsAsync(List<Transaction> transactions);
}

public interface IStateService
{
    Task InitializeAsync();
    Task<byte[]?> GetStateAsync(string key);
    Task SetStateAsync(string key, byte[] value);
    Task CommitAsync();
}

public class BlockchainService : IBlockchainService
{
    private readonly ILogger<BlockchainService> _logger;
    private readonly IStateService _stateService;
    private Block? _latestBlock;
    
    public BlockchainService(ILogger<BlockchainService> logger, IStateService stateService)
    {
        _logger = logger;
        _stateService = stateService;
    }
    
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing blockchain service");
        
        // Load latest block from state
        var latestBlockData = await _stateService.GetStateAsync("latest_block");
        if (latestBlockData != null)
        {
            try
            {
                // Deserialize block from MessagePack
                _latestBlock = MessagePack.MessagePackSerializer.Deserialize<Block>(latestBlockData);
                _logger.LogInformation("Loaded blockchain state - latest block: {BlockNumber}", _latestBlock.Number);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize latest block, creating genesis block");
                await CreateGenesisBlockAsync();
            }
        }
        else
        {
            // Create genesis block
            await CreateGenesisBlockAsync();
        }
    }
    
    public async Task<Block?> GetLatestBlockAsync()
    {
        return _latestBlock;
    }
    
    public async Task<Block?> GetBlockAsync(ulong number)
    {
        try
        {
            // Get block data from state storage
            var blockKey = $"block_{number}";
            var blockData = await _stateService.GetStateAsync(blockKey);
            
            if (blockData != null)
            {
                return MessagePack.MessagePackSerializer.Deserialize<Block>(blockData);
            }
            
            // Check if requesting latest block
            if (_latestBlock != null && _latestBlock.Number == number)
            {
                return _latestBlock;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving block {Number}", number);
            return null;
        }
    }
    
    public async Task<bool> AddBlockAsync(Block block)
    {
        try
        {
            // Validate block
            var validation = block.Validate(_latestBlock);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Invalid block: {Errors}", string.Join(", ", validation.Errors));
                return false;
            }
            
            // Execute block transactions
            await ExecuteBlockTransactionsAsync(block);
            
            // Update state with new block
            await UpdateBlockchainStateAsync(block);
            
            // Persist block to storage
            await PersistBlockAsync(block);
            
            _latestBlock = block;
            _logger.LogInformation("Added block {Number} with hash {Hash}", block.Number, block.Hash);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding block");
            return false;
        }
    }
    
    private async Task CreateGenesisBlockAsync()
    {
        var genesisBlock = new Block
        {
            Number = 0,
            ParentHash = LksBrothers.Core.Primitives.Hash.Zero,
            Timestamp = DateTime.UtcNow,
            GasLimit = new LksBrothers.Core.Primitives.UInt256(15_000_000), // 15M gas limit
            BaseFee = new LksBrothers.Core.Primitives.UInt256(1_000_000_000), // 1 Gwei
        };
        
        genesisBlock.Hash = genesisBlock.CalculateHash();
        
        await AddBlockAsync(genesisBlock);
        _logger.LogInformation("Created genesis block");
    }

    private async Task ExecuteBlockTransactionsAsync(Block block)
    {
        try
        {
            foreach (var transaction in block.Transactions)
            {
                // Execute transaction logic here
                // For now, just log the transaction
                _logger.LogDebug("Executing transaction {Hash} with amount {Amount}", 
                    transaction.Hash, transaction.Amount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing block transactions");
            throw;
        }
    }

    private async Task UpdateBlockchainStateAsync(Block block)
    {
        try
        {
            // Update latest block in state
            var blockData = MessagePackSerializer.Serialize(block);
            await _stateService.SetStateAsync("latest_block", blockData);
            
            // Update block height
            var heightData = BitConverter.GetBytes(block.Number);
            await _stateService.SetStateAsync("block_height", heightData);
            
            _logger.LogDebug("Updated blockchain state for block {Number}", block.Number);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating blockchain state");
            throw;
        }
    }

    private async Task PersistBlockAsync(Block block)
    {
        try
        {
            // Store block by number
            var blockKey = $"block_{block.Number}";
            var blockData = MessagePackSerializer.Serialize(block);
            await _stateService.SetStateAsync(blockKey, blockData);
            
            // Store block by hash
            var hashKey = $"block_hash_{block.Hash}";
            await _stateService.SetStateAsync(hashKey, blockData);
            
            // Commit state changes
            await _stateService.CommitAsync();
            
            _logger.LogDebug("Persisted block {Number} to storage", block.Number);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting block");
            throw;
        }
    }
}

public class ValidatorService : IValidatorService
{
    private readonly ILogger<ValidatorService> _logger;
    private LksBrothers.Consensus.Models.Validator? _validator;
    
    public ValidatorService(ILogger<ValidatorService> logger)
    {
        _logger = logger;
    }
    
    public async Task LoadValidatorKeyAsync(string keyFile)
    {
        try
        {
            // Load validator private key from file
            var keyData = await File.ReadAllTextAsync(keyFile);
            var keyJson = System.Text.Json.JsonSerializer.Deserialize<ValidatorKeyData>(keyData);
            
            if (keyJson == null || string.IsNullOrEmpty(keyJson.PrivateKey))
            {
                throw new InvalidOperationException("Invalid validator key file format");
            }
            
            // Create validator instance
            _validator = new LksBrothers.Consensus.Models.Validator
            {
                Address = LksBrothers.Core.Primitives.Address.FromString(keyJson.Address),
                PublicKey = Convert.FromHexString(keyJson.PublicKey),
                Stake = LksBrothers.Core.Primitives.UInt256.Parse(keyJson.Stake ?? "32000000000000000000000"), // 32,000 LKS default
                IsActive = true
            };
            
            _logger.LogInformation("Loaded validator key from {KeyFile} - Address: {Address}", keyFile, _validator.Address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load validator key");
            throw;
        }
    }
    
    public async Task<bool> IsValidatorAsync()
    {
        return _validator != null;
    }
    
    public async Task<LksBrothers.Consensus.Models.Validator?> GetValidatorAsync()
    {
        return _validator;
    }
}

public class TransactionPool : ITransactionPool
{
    private readonly ILogger<TransactionPool> _logger;
    private readonly Dictionary<LksBrothers.Core.Primitives.Hash, Transaction> _transactions = new();
    
    public TransactionPool(ILogger<TransactionPool> logger)
    {
        _logger = logger;
    }
    
    public async Task<bool> AddTransactionAsync(Transaction transaction)
    {
        try
        {
            var validation = transaction.Validate();
            if (!validation.IsValid)
            {
                _logger.LogDebug("Invalid transaction: {Errors}", string.Join(", ", validation.Errors));
                return false;
            }
            
            _transactions[transaction.Hash] = transaction;
            _logger.LogDebug("Added transaction {Hash} to pool", transaction.Hash);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding transaction to pool");
            return false;
        }
    }
    
    public async Task<List<Transaction>> GetPendingTransactionsAsync(int maxCount = 1000)
    {
        return _transactions.Values.Take(maxCount).ToList();
    }
    
    public async Task RemoveTransactionsAsync(List<Transaction> transactions)
    {
        foreach (var tx in transactions)
        {
            _transactions.Remove(tx.Hash);
        }
    }
}

public class StateService : IStateService
{
    private readonly ILogger<StateService> _logger;
    private readonly Dictionary<string, byte[]> _state = new();
    
    public StateService(ILogger<StateService> logger)
    {
        _logger = logger;
    }
    
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing state service");
        
        try
        {
            // Load state from persistent storage (file-based for now)
            var stateFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "lks-brothers", "state.json");
            
            if (File.Exists(stateFile))
            {
                var stateJson = await File.ReadAllTextAsync(stateFile);
                var persistedState = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stateJson);
                
                if (persistedState != null)
                {
                    foreach (var kvp in persistedState)
                    {
                        _state[kvp.Key] = Convert.FromBase64String(kvp.Value);
                    }
                    
                    _logger.LogInformation("Loaded {Count} state entries from persistent storage", _state.Count);
                }
            }
            else
            {
                _logger.LogInformation("No existing state file found, starting with empty state");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading state from persistent storage");
        }
    }
    
    public async Task<byte[]?> GetStateAsync(string key)
    {
        _state.TryGetValue(key, out var value);
        return value;
    }
    
    public async Task SetStateAsync(string key, byte[] value)
    {
        _state[key] = value;
    }
    
    public async Task CommitAsync()
    {
        try
        {
            // Persist state changes to file
            var stateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "lks-brothers");
            Directory.CreateDirectory(stateDir);
            
            var stateFile = Path.Combine(stateDir, "state.json");
            
            // Convert binary state to base64 for JSON serialization
            var persistableState = _state.ToDictionary(
                kvp => kvp.Key,
                kvp => Convert.ToBase64String(kvp.Value)
            );
            
            var stateJson = System.Text.Json.JsonSerializer.Serialize(persistableState, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(stateFile, stateJson);
            
            _logger.LogDebug("Committed {Count} state changes to persistent storage", _state.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting state changes");
        }
    }
}
