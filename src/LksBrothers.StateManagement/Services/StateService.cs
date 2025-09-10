using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using LksBrothers.StateManagement.Database;
using System.Threading.Channels;

namespace LksBrothers.StateManagement.Services;

public class StateService : BackgroundService
{
    private readonly ILogger<StateService> _logger;
    private readonly StateDatabase _database;
    private readonly IMemoryCache _cache;
    private readonly StateServiceOptions _options;
    private readonly Channel<StateOperation> _operationQueue;
    private readonly ChannelWriter<StateOperation> _operationWriter;
    private readonly ChannelReader<StateOperation> _operationReader;

    public StateService(
        ILogger<StateService> logger,
        StateDatabase database,
        IMemoryCache cache,
        IOptions<StateServiceOptions> options)
    {
        _logger = logger;
        _database = database;
        _cache = cache;
        _options = options.Value;

        var channel = Channel.CreateUnbounded<StateOperation>();
        _operationQueue = channel;
        _operationWriter = channel.Writer;
        _operationReader = channel.Reader;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("State service starting");

        try
        {
            await foreach (var operation in _operationReader.ReadAllAsync(stoppingToken))
            {
                await ProcessOperationAsync(operation);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("State service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "State service encountered an error");
        }
    }

    #region Public API

    public async Task<bool> StoreBlockAsync(Block block)
    {
        var operation = new StateOperation
        {
            Type = StateOperationType.StoreBlock,
            Block = block,
            CompletionSource = new TaskCompletionSource<bool>()
        };

        await _operationWriter.WriteAsync(operation);
        return await operation.CompletionSource.Task;
    }

    public async Task<Block?> GetBlockAsync(ulong blockNumber)
    {
        // Try cache first
        var cacheKey = $"block:{blockNumber}";
        if (_cache.TryGetValue(cacheKey, out Block? cachedBlock))
        {
            return cachedBlock;
        }

        var block = await _database.GetBlockAsync(blockNumber);
        if (block != null)
        {
            _cache.Set(cacheKey, block, TimeSpan.FromMinutes(_options.CacheExpiryMinutes));
        }

        return block;
    }

    public async Task<Block?> GetBlockByHashAsync(Hash blockHash)
    {
        var cacheKey = $"block_hash:{blockHash}";
        if (_cache.TryGetValue(cacheKey, out Block? cachedBlock))
        {
            return cachedBlock;
        }

        var block = await _database.GetBlockByHashAsync(blockHash);
        if (block != null)
        {
            _cache.Set(cacheKey, block, TimeSpan.FromMinutes(_options.CacheExpiryMinutes));
        }

        return block;
    }

    public async Task<Transaction?> GetTransactionAsync(Hash txHash)
    {
        var cacheKey = $"tx:{txHash}";
        if (_cache.TryGetValue(cacheKey, out Transaction? cachedTx))
        {
            return cachedTx;
        }

        var transaction = await _database.GetTransactionAsync(txHash);
        if (transaction != null)
        {
            _cache.Set(cacheKey, transaction, TimeSpan.FromMinutes(_options.CacheExpiryMinutes));
        }

        return transaction;
    }

    public async Task<bool> UpdateAccountStateAsync(Address address, AccountState state)
    {
        var operation = new StateOperation
        {
            Type = StateOperationType.UpdateAccount,
            Address = address,
            AccountState = state,
            CompletionSource = new TaskCompletionSource<bool>()
        };

        await _operationWriter.WriteAsync(operation);
        return await operation.CompletionSource.Task;
    }

    public async Task<AccountState?> GetAccountStateAsync(Address address)
    {
        var cacheKey = $"account:{address}";
        if (_cache.TryGetValue(cacheKey, out AccountState? cachedState))
        {
            return cachedState;
        }

        var state = await _database.GetAccountStateAsync(address);
        if (state != null)
        {
            _cache.Set(cacheKey, state, TimeSpan.FromMinutes(_options.CacheExpiryMinutes));
        }

        return state;
    }

    public async Task<UInt256> GetBalanceAsync(Address address)
    {
        var accountState = await GetAccountStateAsync(address);
        return accountState?.Balance ?? UInt256.Zero;
    }

    public async Task<ulong> GetNonceAsync(Address address)
    {
        var accountState = await GetAccountStateAsync(address);
        return accountState?.Nonce ?? 0;
    }

    public async Task<UInt256> GetStablecoinBalanceAsync(Address address)
    {
        var accountState = await GetAccountStateAsync(address);
        return accountState?.StablecoinBalance ?? UInt256.Zero;
    }

    public async Task<bool> StoreContractCodeAsync(Address contractAddress, byte[] code)
    {
        var operation = new StateOperation
        {
            Type = StateOperationType.StoreContractCode,
            Address = contractAddress,
            ContractCode = code,
            CompletionSource = new TaskCompletionSource<bool>()
        };

        await _operationWriter.WriteAsync(operation);
        return await operation.CompletionSource.Task;
    }

    public async Task<byte[]?> GetContractCodeAsync(Address contractAddress)
    {
        var cacheKey = $"code:{contractAddress}";
        if (_cache.TryGetValue(cacheKey, out byte[]? cachedCode))
        {
            return cachedCode;
        }

        var code = await _database.GetContractCodeAsync(contractAddress);
        if (code != null)
        {
            _cache.Set(cacheKey, code, TimeSpan.FromHours(1)); // Contract code rarely changes
        }

        return code;
    }

    public async Task<ulong?> GetLatestBlockNumberAsync()
    {
        var cacheKey = "latest_block_number";
        if (_cache.TryGetValue(cacheKey, out ulong cachedNumber))
        {
            return cachedNumber;
        }

        var blockNumber = await _database.GetLatestBlockNumberAsync();
        if (blockNumber.HasValue)
        {
            _cache.Set(cacheKey, blockNumber.Value, TimeSpan.FromSeconds(30)); // Short cache for latest block
        }

        return blockNumber;
    }

    public async Task<StateSnapshot> CreateSnapshotAsync(string name)
    {
        var latestBlock = await GetLatestBlockNumberAsync();
        var success = await _database.CreateSnapshotAsync(name);

        return new StateSnapshot
        {
            Name = name,
            BlockNumber = latestBlock ?? 0,
            Timestamp = DateTimeOffset.UtcNow,
            Success = success
        };
    }

    public async Task<bool> PruneOldDataAsync(ulong keepFromBlock)
    {
        if (!_options.EnablePruning)
        {
            _logger.LogWarning("Pruning is disabled in configuration");
            return false;
        }

        _logger.LogInformation("Starting pruning of blocks before {KeepFromBlock}", keepFromBlock);
        
        var success = await _database.PruneOldBlocksAsync(keepFromBlock);
        
        if (success)
        {
            // Clear relevant cache entries
            _cache.Remove("latest_block_number");
            _logger.LogInformation("Pruning completed successfully");
        }

        return success;
    }

    #endregion

    #region Private Methods

    private async Task ProcessOperationAsync(StateOperation operation)
    {
        try
        {
            bool result = operation.Type switch
            {
                StateOperationType.StoreBlock => await ProcessStoreBlockAsync(operation.Block!),
                StateOperationType.UpdateAccount => await ProcessUpdateAccountAsync(operation.Address!, operation.AccountState!),
                StateOperationType.StoreContractCode => await ProcessStoreContractCodeAsync(operation.Address!, operation.ContractCode!),
                _ => throw new InvalidOperationException($"Unknown operation type: {operation.Type}")
            };

            operation.CompletionSource!.SetResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing state operation {OperationType}", operation.Type);
            operation.CompletionSource!.SetException(ex);
        }
    }

    private async Task<bool> ProcessStoreBlockAsync(Block block)
    {
        var success = await _database.StoreBlockAsync(block);
        
        if (success)
        {
            // Update latest block number
            await _database.SetLatestBlockNumberAsync(block.Header.Number);
            
            // Update cache
            var blockCacheKey = $"block:{block.Header.Number}";
            var hashCacheKey = $"block_hash:{block.Hash}";
            
            _cache.Set(blockCacheKey, block, TimeSpan.FromMinutes(_options.CacheExpiryMinutes));
            _cache.Set(hashCacheKey, block, TimeSpan.FromMinutes(_options.CacheExpiryMinutes));
            _cache.Set("latest_block_number", block.Header.Number, TimeSpan.FromSeconds(30));

            // Cache transactions
            foreach (var tx in block.Transactions)
            {
                var txCacheKey = $"tx:{tx.Hash}";
                _cache.Set(txCacheKey, tx, TimeSpan.FromMinutes(_options.CacheExpiryMinutes));
            }

            _logger.LogDebug("Stored and cached block {BlockNumber}", block.Header.Number);
        }

        return success;
    }

    private async Task<bool> ProcessUpdateAccountAsync(Address address, AccountState state)
    {
        var success = await _database.StoreAccountStateAsync(address, state);
        
        if (success)
        {
            var cacheKey = $"account:{address}";
            _cache.Set(cacheKey, state, TimeSpan.FromMinutes(_options.CacheExpiryMinutes));
            _logger.LogDebug("Updated and cached account state for {Address}", address);
        }

        return success;
    }

    private async Task<bool> ProcessStoreContractCodeAsync(Address contractAddress, byte[] code)
    {
        var success = await _database.StoreContractCodeAsync(contractAddress, code);
        
        if (success)
        {
            var cacheKey = $"code:{contractAddress}";
            _cache.Set(cacheKey, code, TimeSpan.FromHours(1));
            _logger.LogDebug("Stored and cached contract code for {Address}", contractAddress);
        }

        return success;
    }

    #endregion

    public override void Dispose()
    {
        _operationWriter.Complete();
        base.Dispose();
    }
}

public enum StateOperationType
{
    StoreBlock,
    UpdateAccount,
    StoreContractCode
}

public class StateOperation
{
    public required StateOperationType Type { get; set; }
    public Block? Block { get; set; }
    public Address? Address { get; set; }
    public AccountState? AccountState { get; set; }
    public byte[]? ContractCode { get; set; }
    public TaskCompletionSource<bool>? CompletionSource { get; set; }
}

public class StateSnapshot
{
    public required string Name { get; set; }
    public required ulong BlockNumber { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
    public required bool Success { get; set; }
}

public class StateServiceOptions
{
    public int CacheExpiryMinutes { get; set; } = 15;
    public bool EnablePruning { get; set; } = true;
    public ulong PruneKeepBlocks { get; set; } = 1000;
    public int MaxCacheSize { get; set; } = 1000;
}
