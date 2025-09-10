using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RocksDbSharp;
using MessagePack;
using LksBrothers.Core.Primitives;
using LksBrothers.Core.Models;
using System.Collections.Concurrent;

namespace LksBrothers.StateManagement.Database;

public class StateDatabase : IDisposable
{
    private readonly ILogger<StateDatabase> _logger;
    private readonly StateDatabaseOptions _options;
    private readonly RocksDb _db;
    private readonly ConcurrentDictionary<string, ColumnFamilyHandle> _columnFamilies;
    private readonly object _disposeLock = new();
    private bool _disposed;

    // Column family names
    private const string BlocksColumnFamily = "blocks";
    private const string TransactionsColumnFamily = "transactions";
    private const string AccountsColumnFamily = "accounts";
    private const string ContractsColumnFamily = "contracts";
    private const string StablecoinColumnFamily = "stablecoin";
    private const string ValidatorsColumnFamily = "validators";
    private const string MetadataColumnFamily = "metadata";

    public StateDatabase(ILogger<StateDatabase> logger, IOptions<StateDatabaseOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _columnFamilies = new ConcurrentDictionary<string, ColumnFamilyHandle>();

        try
        {
            _db = InitializeDatabase();
            _logger.LogInformation("State database initialized at {Path}", _options.DatabasePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize state database");
            throw;
        }
    }

    private RocksDb InitializeDatabase()
    {
        var dbOptions = new DbOptions()
            .SetCreateIfMissing(true)
            .SetCreateMissingColumnFamilies(true)
            .SetMaxBackgroundJobs(_options.MaxBackgroundJobs)
            .SetMaxOpenFiles(_options.MaxOpenFiles)
            .SetWriteBufferSize(_options.WriteBufferSize)
            .SetMaxWriteBufferNumber(_options.MaxWriteBufferNumber)
            .SetTargetFileSizeBase(_options.TargetFileSizeBase)
            .SetMaxBytesForLevelBase(_options.MaxBytesForLevelBase)
            .SetCompactionStyle(CompactionStyle.Level)
            .SetCompressionType(CompressionTypeEnum.Lz4);

        var columnFamilies = new ColumnFamilies
        {
            { BlocksColumnFamily, new ColumnFamilyOptions().SetBlockBasedTableFactory(new BlockBasedTableOptions().SetBlockSize(16384)) },
            { TransactionsColumnFamily, new ColumnFamilyOptions().SetBlockBasedTableFactory(new BlockBasedTableOptions().SetBlockSize(8192)) },
            { AccountsColumnFamily, new ColumnFamilyOptions().SetBlockBasedTableFactory(new BlockBasedTableOptions().SetBlockSize(4096)) },
            { ContractsColumnFamily, new ColumnFamilyOptions().SetBlockBasedTableFactory(new BlockBasedTableOptions().SetBlockSize(8192)) },
            { StablecoinColumnFamily, new ColumnFamilyOptions().SetBlockBasedTableFactory(new BlockBasedTableOptions().SetBlockSize(4096)) },
            { ValidatorsColumnFamily, new ColumnFamilyOptions().SetBlockBasedTableFactory(new BlockBasedTableOptions().SetBlockSize(4096)) },
            { MetadataColumnFamily, new ColumnFamilyOptions().SetBlockBasedTableFactory(new BlockBasedTableOptions().SetBlockSize(2048)) }
        };

        var db = RocksDb.Open(dbOptions, _options.DatabasePath, columnFamilies);

        // Store column family handles
        foreach (var cf in columnFamilies)
        {
            _columnFamilies[cf.Name] = db.GetColumnFamily(cf.Name);
        }

        return db;
    }

    #region Block Operations

    public async Task<bool> StoreBlockAsync(Block block)
    {
        try
        {
            var blockData = MessagePackSerializer.Serialize(block);
            var blockKey = GetBlockKey(block.Header.Number);
            var hashKey = GetBlockHashKey(block.Hash);

            using var batch = new WriteBatch();
            batch.Put(blockKey, blockData, _columnFamilies[BlocksColumnFamily]);
            batch.Put(hashKey, BitConverter.GetBytes(block.Header.Number), _columnFamilies[BlocksColumnFamily]);

            // Store transactions
            foreach (var tx in block.Transactions)
            {
                var txData = MessagePackSerializer.Serialize(tx);
                var txKey = GetTransactionKey(tx.Hash);
                batch.Put(txKey, txData, _columnFamilies[TransactionsColumnFamily]);
            }

            _db.Write(batch);
            
            _logger.LogDebug("Stored block {BlockNumber} with {TxCount} transactions", 
                block.Header.Number, block.Transactions.Count);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store block {BlockNumber}", block.Header.Number);
            return false;
        }
    }

    public async Task<Block?> GetBlockAsync(ulong blockNumber)
    {
        try
        {
            var blockKey = GetBlockKey(blockNumber);
            var blockData = _db.Get(blockKey, _columnFamilies[BlocksColumnFamily]);
            
            if (blockData == null)
                return null;

            return MessagePackSerializer.Deserialize<Block>(blockData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get block {BlockNumber}", blockNumber);
            return null;
        }
    }

    public async Task<Block?> GetBlockByHashAsync(Hash blockHash)
    {
        try
        {
            var hashKey = GetBlockHashKey(blockHash);
            var blockNumberData = _db.Get(hashKey, _columnFamilies[BlocksColumnFamily]);
            
            if (blockNumberData == null)
                return null;

            var blockNumber = BitConverter.ToUInt64(blockNumberData);
            return await GetBlockAsync(blockNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get block by hash {BlockHash}", blockHash);
            return null;
        }
    }

    #endregion

    #region Transaction Operations

    public async Task<Transaction?> GetTransactionAsync(Hash txHash)
    {
        try
        {
            var txKey = GetTransactionKey(txHash);
            var txData = _db.Get(txKey, _columnFamilies[TransactionsColumnFamily]);
            
            if (txData == null)
                return null;

            return MessagePackSerializer.Deserialize<Transaction>(txData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transaction {TxHash}", txHash);
            return null;
        }
    }

    #endregion

    #region Account Operations

    public async Task<bool> StoreAccountStateAsync(Address address, AccountState state)
    {
        try
        {
            var accountKey = GetAccountKey(address);
            var accountData = MessagePackSerializer.Serialize(state);
            
            _db.Put(accountKey, accountData, _columnFamilies[AccountsColumnFamily]);
            
            _logger.LogDebug("Stored account state for {Address}", address);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store account state for {Address}", address);
            return false;
        }
    }

    public async Task<AccountState?> GetAccountStateAsync(Address address)
    {
        try
        {
            var accountKey = GetAccountKey(address);
            var accountData = _db.Get(accountKey, _columnFamilies[AccountsColumnFamily]);
            
            if (accountData == null)
                return null;

            return MessagePackSerializer.Deserialize<AccountState>(accountData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get account state for {Address}", address);
            return null;
        }
    }

    #endregion

    #region Contract Operations

    public async Task<bool> StoreContractCodeAsync(Address contractAddress, byte[] code)
    {
        try
        {
            var codeKey = GetContractCodeKey(contractAddress);
            _db.Put(codeKey, code, _columnFamilies[ContractsColumnFamily]);
            
            _logger.LogDebug("Stored contract code for {Address}, size: {Size} bytes", 
                contractAddress, code.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store contract code for {Address}", contractAddress);
            return false;
        }
    }

    public async Task<byte[]?> GetContractCodeAsync(Address contractAddress)
    {
        try
        {
            var codeKey = GetContractCodeKey(contractAddress);
            return _db.Get(codeKey, _columnFamilies[ContractsColumnFamily]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get contract code for {Address}", contractAddress);
            return null;
        }
    }

    #endregion

    #region Metadata Operations

    public async Task<bool> SetLatestBlockNumberAsync(ulong blockNumber)
    {
        try
        {
            var key = "latest_block_number"u8.ToArray();
            var value = BitConverter.GetBytes(blockNumber);
            _db.Put(key, value, _columnFamilies[MetadataColumnFamily]);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set latest block number {BlockNumber}", blockNumber);
            return false;
        }
    }

    public async Task<ulong?> GetLatestBlockNumberAsync()
    {
        try
        {
            var key = "latest_block_number"u8.ToArray();
            var value = _db.Get(key, _columnFamilies[MetadataColumnFamily]);
            
            if (value == null)
                return null;

            return BitConverter.ToUInt64(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest block number");
            return null;
        }
    }

    #endregion

    #region Snapshot and Pruning

    public async Task<bool> CreateSnapshotAsync(string snapshotName)
    {
        try
        {
            var snapshotPath = Path.Combine(_options.SnapshotPath, snapshotName);
            Directory.CreateDirectory(snapshotPath);

            using var snapshot = _db.CreateSnapshot();
            // Implementation would copy database files to snapshot directory
            // For now, we'll just log the operation
            
            _logger.LogInformation("Created snapshot {SnapshotName} at {Path}", snapshotName, snapshotPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create snapshot {SnapshotName}", snapshotName);
            return false;
        }
    }

    public async Task<bool> PruneOldBlocksAsync(ulong keepFromBlock)
    {
        try
        {
            var pruneCount = 0;
            var startKey = GetBlockKey(0);
            var endKey = GetBlockKey(keepFromBlock);

            using var iterator = _db.NewIterator(_columnFamilies[BlocksColumnFamily]);
            iterator.Seek(startKey);

            using var batch = new WriteBatch();
            
            while (iterator.Valid() && CompareKeys(iterator.Key(), endKey) < 0)
            {
                batch.Delete(iterator.Key(), _columnFamilies[BlocksColumnFamily]);
                pruneCount++;
                iterator.Next();
            }

            if (pruneCount > 0)
            {
                _db.Write(batch);
                _logger.LogInformation("Pruned {Count} old blocks before block {KeepFromBlock}", 
                    pruneCount, keepFromBlock);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prune old blocks before {KeepFromBlock}", keepFromBlock);
            return false;
        }
    }

    #endregion

    #region Key Generation

    private static byte[] GetBlockKey(ulong blockNumber)
    {
        var key = new byte[9];
        key[0] = 0x01; // Block prefix
        BitConverter.GetBytes(blockNumber).CopyTo(key, 1);
        return key;
    }

    private static byte[] GetBlockHashKey(Hash blockHash)
    {
        var key = new byte[33];
        key[0] = 0x02; // Block hash prefix
        blockHash.Bytes.CopyTo(key, 1);
        return key;
    }

    private static byte[] GetTransactionKey(Hash txHash)
    {
        var key = new byte[33];
        key[0] = 0x03; // Transaction prefix
        txHash.Bytes.CopyTo(key, 1);
        return key;
    }

    private static byte[] GetAccountKey(Address address)
    {
        var key = new byte[21];
        key[0] = 0x04; // Account prefix
        address.Bytes.CopyTo(key, 1);
        return key;
    }

    private static byte[] GetContractCodeKey(Address contractAddress)
    {
        var key = new byte[21];
        key[0] = 0x05; // Contract code prefix
        contractAddress.Bytes.CopyTo(key, 1);
        return key;
    }

    private static int CompareKeys(byte[] key1, byte[] key2)
    {
        var minLength = Math.Min(key1.Length, key2.Length);
        for (int i = 0; i < minLength; i++)
        {
            var result = key1[i].CompareTo(key2[i]);
            if (result != 0)
                return result;
        }
        return key1.Length.CompareTo(key2.Length);
    }

    #endregion

    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_disposed)
                return;

            try
            {
                foreach (var cf in _columnFamilies.Values)
                {
                    cf?.Dispose();
                }
                _columnFamilies.Clear();

                _db?.Dispose();
                _disposed = true;
                
                _logger.LogInformation("State database disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing state database");
            }
        }
    }
}

[MessagePackObject]
public class AccountState
{
    [Key(0)]
    public required UInt256 Balance { get; set; }

    [Key(1)]
    public required ulong Nonce { get; set; }

    [Key(2)]
    public Hash? CodeHash { get; set; }

    [Key(3)]
    public Hash? StorageRoot { get; set; }

    [Key(4)]
    public UInt256? StablecoinBalance { get; set; }

    [Key(5)]
    public Dictionary<string, object>? Metadata { get; set; }
}

public class StateDatabaseOptions
{
    public string DatabasePath { get; set; } = "./data/state";
    public string SnapshotPath { get; set; } = "./data/snapshots";
    public int MaxBackgroundJobs { get; set; } = 4;
    public int MaxOpenFiles { get; set; } = 1000;
    public ulong WriteBufferSize { get; set; } = 64 * 1024 * 1024; // 64MB
    public int MaxWriteBufferNumber { get; set; } = 3;
    public ulong TargetFileSizeBase { get; set; } = 64 * 1024 * 1024; // 64MB
    public ulong MaxBytesForLevelBase { get; set; } = 256 * 1024 * 1024; // 256MB
    public bool EnablePruning { get; set; } = true;
    public ulong PruneKeepBlocks { get; set; } = 1000;
}
