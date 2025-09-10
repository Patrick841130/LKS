using Microsoft.Extensions.Caching.Memory;
using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using LksBrothers.StateManagement.Services;
using System.Text.Json;

namespace LksBrothers.Explorer.Services;

public class ExplorerService
{
    private readonly IMemoryCache _cache;
    private readonly StateService _stateService;
    private readonly ILogger<ExplorerService> _logger;

    public ExplorerService(IMemoryCache cache, StateService stateService, ILogger<ExplorerService> logger)
    {
        _cache = cache;
        _stateService = stateService;
        _logger = logger;
    }

    public async Task<ExplorerBlock> GetBlockAsync(Hash blockHash)
    {
        try
        {
            var cacheKey = $"explorer_block_{blockHash}";
            if (_cache.TryGetValue(cacheKey, out ExplorerBlock? cached))
            {
                return cached!;
            }

            var block = await _stateService.GetBlockAsync(blockHash);
            if (block == null)
            {
                throw new InvalidOperationException($"Block {blockHash} not found");
            }

            var explorerBlock = new ExplorerBlock
            {
                Hash = block.Hash,
                Number = block.Number,
                Timestamp = block.Timestamp,
                ParentHash = block.ParentHash,
                ProposerAddress = block.ProposerAddress,
                SlotNumber = block.SlotNumber,
                TransactionCount = block.Transactions.Count,
                Size = EstimateBlockSize(block),
                GasUsed = CalculateGasUsed(block),
                Transactions = block.Transactions.Select(tx => new ExplorerTransaction
                {
                    Hash = tx.Hash,
                    From = tx.From,
                    To = tx.To,
                    Amount = tx.Amount,
                    Type = tx.Type.ToString(),
                    Status = tx.Status.ToString(),
                    Timestamp = tx.Timestamp,
                    GasUsed = tx.GasUsed,
                    BlockNumber = block.Number
                }).ToList()
            };

            _cache.Set(cacheKey, explorerBlock, TimeSpan.FromMinutes(5));
            return explorerBlock;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting block {BlockHash}", blockHash);
            throw;
        }
    }

    public async Task<ExplorerTransaction> GetTransactionAsync(Hash txHash)
    {
        try
        {
            var cacheKey = $"explorer_tx_{txHash}";
            if (_cache.TryGetValue(cacheKey, out ExplorerTransaction? cached))
            {
                return cached!;
            }

            var transaction = await _stateService.GetTransactionAsync(txHash);
            if (transaction == null)
            {
                throw new InvalidOperationException($"Transaction {txHash} not found");
            }

            var block = await _stateService.GetBlockAsync(transaction.BlockHash ?? Hash.Zero);
            
            var explorerTx = new ExplorerTransaction
            {
                Hash = transaction.Hash,
                From = transaction.From,
                To = transaction.To,
                Amount = transaction.Amount,
                Type = transaction.Type.ToString(),
                Status = transaction.Status.ToString(),
                Timestamp = transaction.Timestamp,
                GasUsed = transaction.GasUsed,
                BlockNumber = block?.Number ?? 0,
                BlockHash = transaction.BlockHash,
                Confirmations = await CalculateConfirmations(transaction.BlockHash)
            };

            _cache.Set(cacheKey, explorerTx, TimeSpan.FromMinutes(5));
            return explorerTx;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction {TxHash}", txHash);
            throw;
        }
    }

    public async Task<ExplorerAddress> GetAddressAsync(Address address)
    {
        try
        {
            var cacheKey = $"explorer_address_{address}";
            if (_cache.TryGetValue(cacheKey, out ExplorerAddress? cached))
            {
                return cached!;
            }

            var account = await _stateService.GetAccountAsync(address);
            var transactions = await GetAddressTransactionsAsync(address, 50);
            
            var explorerAddress = new ExplorerAddress
            {
                Address = address,
                Balance = account?.Balance ?? UInt256.Zero,
                TransactionCount = transactions.Count,
                FirstSeen = transactions.LastOrDefault()?.Timestamp ?? DateTimeOffset.UtcNow,
                LastActivity = transactions.FirstOrDefault()?.Timestamp ?? DateTimeOffset.UtcNow,
                Transactions = transactions
            };

            _cache.Set(cacheKey, explorerAddress, TimeSpan.FromMinutes(1));
            return explorerAddress;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting address {Address}", address);
            throw;
        }
    }

    public async Task<List<ExplorerBlock>> GetLatestBlocksAsync(int count = 10)
    {
        try
        {
            var cacheKey = $"explorer_latest_blocks_{count}";
            if (_cache.TryGetValue(cacheKey, out List<ExplorerBlock>? cached))
            {
                return cached!;
            }

            var blocks = await _stateService.GetLatestBlocksAsync(count);
            var explorerBlocks = new List<ExplorerBlock>();

            foreach (var block in blocks)
            {
                explorerBlocks.Add(new ExplorerBlock
                {
                    Hash = block.Hash,
                    Number = block.Number,
                    Timestamp = block.Timestamp,
                    ParentHash = block.ParentHash,
                    ProposerAddress = block.ProposerAddress,
                    SlotNumber = block.SlotNumber,
                    TransactionCount = block.Transactions.Count,
                    Size = EstimateBlockSize(block),
                    GasUsed = CalculateGasUsed(block)
                });
            }

            _cache.Set(cacheKey, explorerBlocks, TimeSpan.FromSeconds(30));
            return explorerBlocks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest blocks");
            return new List<ExplorerBlock>();
        }
    }

    public async Task<List<ExplorerTransaction>> GetLatestTransactionsAsync(int count = 20)
    {
        try
        {
            var cacheKey = $"explorer_latest_txs_{count}";
            if (_cache.TryGetValue(cacheKey, out List<ExplorerTransaction>? cached))
            {
                return cached!;
            }

            var transactions = await _stateService.GetLatestTransactionsAsync(count);
            var explorerTxs = new List<ExplorerTransaction>();

            foreach (var tx in transactions)
            {
                explorerTxs.Add(new ExplorerTransaction
                {
                    Hash = tx.Hash,
                    From = tx.From,
                    To = tx.To,
                    Amount = tx.Amount,
                    Type = tx.Type.ToString(),
                    Status = tx.Status.ToString(),
                    Timestamp = tx.Timestamp,
                    GasUsed = tx.GasUsed,
                    BlockHash = tx.BlockHash
                });
            }

            _cache.Set(cacheKey, explorerTxs, TimeSpan.FromSeconds(15));
            return explorerTxs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest transactions");
            return new List<ExplorerTransaction>();
        }
    }

    public async Task<SearchResult> SearchAsync(string query)
    {
        try
        {
            query = query.Trim();

            // Try to parse as hash (block or transaction)
            if (Hash.TryParse(query, out var hash))
            {
                // Check if it's a block
                try
                {
                    var block = await GetBlockAsync(hash);
                    return new SearchResult
                    {
                        Type = "block",
                        Data = block
                    };
                }
                catch
                {
                    // Try as transaction
                    try
                    {
                        var tx = await GetTransactionAsync(hash);
                        return new SearchResult
                        {
                            Type = "transaction",
                            Data = tx
                        };
                    }
                    catch
                    {
                        // Not found
                    }
                }
            }

            // Try to parse as address
            if (Address.TryParse(query, out var address))
            {
                var addressInfo = await GetAddressAsync(address);
                return new SearchResult
                {
                    Type = "address",
                    Data = addressInfo
                };
            }

            // Try to parse as block number
            if (ulong.TryParse(query, out var blockNumber))
            {
                var blockByNumber = await _stateService.GetBlockByNumberAsync(blockNumber);
                if (blockByNumber != null)
                {
                    var block = await GetBlockAsync(blockByNumber.Hash);
                    return new SearchResult
                    {
                        Type = "block",
                        Data = block
                    };
                }
            }

            return new SearchResult
            {
                Type = "not_found",
                Data = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for {Query}", query);
            return new SearchResult
            {
                Type = "error",
                Data = ex.Message
            };
        }
    }

    private async Task<List<ExplorerTransaction>> GetAddressTransactionsAsync(Address address, int limit)
    {
        // Simulate getting address transactions
        var transactions = new List<ExplorerTransaction>();
        var random = new Random(address.ToString().GetHashCode());
        
        for (int i = 0; i < Math.Min(limit, 20); i++)
        {
            var isIncoming = random.Next(2) == 0;
            var amount = random.Next(1, 1000) * 1000000000000000000L;
            
            transactions.Add(new ExplorerTransaction
            {
                Hash = Hash.ComputeHash($"tx_{address}_{i}_{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                From = isIncoming ? GenerateRandomAddress(random) : address,
                To = isIncoming ? address : GenerateRandomAddress(random),
                Amount = UInt256.Parse(amount.ToString()),
                Type = "Transfer",
                Status = "Confirmed",
                Timestamp = DateTimeOffset.UtcNow.AddHours(-random.Next(1, 168)),
                GasUsed = (ulong)random.Next(21000, 100000),
                BlockNumber = (ulong)random.Next(1000000, 2000000)
            });
        }
        
        return transactions.OrderByDescending(t => t.Timestamp).ToList();
    }

    private Address GenerateRandomAddress(Random random)
    {
        var bytes = new byte[20];
        random.NextBytes(bytes);
        return new Address(bytes);
    }

    private ulong EstimateBlockSize(Block block)
    {
        // Rough estimate of block size in bytes
        return (ulong)(1000 + block.Transactions.Count * 200);
    }

    private ulong CalculateGasUsed(Block block)
    {
        return (ulong)block.Transactions.Sum(tx => (long)tx.GasUsed);
    }

    private async Task<int> CalculateConfirmations(Hash? blockHash)
    {
        if (blockHash == null) return 0;
        
        try
        {
            var block = await _stateService.GetBlockAsync(blockHash.Value);
            var latestBlock = await _stateService.GetLatestBlockAsync();
            
            if (block == null || latestBlock == null) return 0;
            
            return (int)(latestBlock.Number - block.Number);
        }
        catch
        {
            return 0;
        }
    }
}

// Data models for explorer
public class ExplorerBlock
{
    public required Hash Hash { get; set; }
    public required ulong Number { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
    public required Hash ParentHash { get; set; }
    public Address? ProposerAddress { get; set; }
    public ulong SlotNumber { get; set; }
    public required int TransactionCount { get; set; }
    public required ulong Size { get; set; }
    public required ulong GasUsed { get; set; }
    public List<ExplorerTransaction>? Transactions { get; set; }
    
    public string TimeAgo => GetTimeAgo(Timestamp);
    public string SizeFormatted => FormatBytes(Size);
    
    private string GetTimeAgo(DateTimeOffset timestamp)
    {
        var diff = DateTimeOffset.UtcNow - timestamp;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }
    
    private string FormatBytes(ulong bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
        return $"{bytes / (1024 * 1024):F1} MB";
    }
}

public class ExplorerTransaction
{
    public required Hash Hash { get; set; }
    public required Address From { get; set; }
    public Address? To { get; set; }
    public required UInt256 Amount { get; set; }
    public required string Type { get; set; }
    public required string Status { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
    public required ulong GasUsed { get; set; }
    public ulong BlockNumber { get; set; }
    public Hash? BlockHash { get; set; }
    public int Confirmations { get; set; }
    
    public string AmountFormatted => FormatLKS(Amount);
    public string TimeAgo => GetTimeAgo(Timestamp);
    
    private string FormatLKS(UInt256 wei)
    {
        var lks = (double)wei / 1000000000000000000.0;
        return lks.ToString("N4") + " LKS";
    }
    
    private string GetTimeAgo(DateTimeOffset timestamp)
    {
        var diff = DateTimeOffset.UtcNow - timestamp;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }
}

public class ExplorerAddress
{
    public required Address Address { get; set; }
    public required UInt256 Balance { get; set; }
    public required int TransactionCount { get; set; }
    public required DateTimeOffset FirstSeen { get; set; }
    public required DateTimeOffset LastActivity { get; set; }
    public List<ExplorerTransaction>? Transactions { get; set; }
    
    public string BalanceFormatted => FormatLKS(Balance);
    
    private string FormatLKS(UInt256 wei)
    {
        var lks = (double)wei / 1000000000000000000.0;
        return lks.ToString("N4") + " LKS";
    }
}

public class SearchResult
{
    public required string Type { get; set; }
    public object? Data { get; set; }
}
