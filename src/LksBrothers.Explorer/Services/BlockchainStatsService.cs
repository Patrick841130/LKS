using Microsoft.Extensions.Caching.Memory;
using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using LksBrothers.StateManagement.Services;

namespace LksBrothers.Explorer.Services;

public class BlockchainStatsService
{
    private readonly IMemoryCache _cache;
    private readonly StateService _stateService;
    private readonly ILogger<BlockchainStatsService> _logger;

    public BlockchainStatsService(IMemoryCache cache, StateService stateService, ILogger<BlockchainStatsService> logger)
    {
        _cache = cache;
        _stateService = stateService;
        _logger = logger;
    }

    public async Task<BlockchainStats> GetStatsAsync()
    {
        try
        {
            const string cacheKey = "blockchain_stats";
            if (_cache.TryGetValue(cacheKey, out BlockchainStats? cached))
            {
                return cached!;
            }

            var stats = new BlockchainStats
            {
                TotalBlocks = await GetTotalBlocksAsync(),
                TotalTransactions = await GetTotalTransactionsAsync(),
                TotalAddresses = await GetTotalAddressesAsync(),
                CirculatingSupply = await GetCirculatingSupplyAsync(),
                MarketCap = await GetMarketCapAsync(),
                CurrentTPS = await GetCurrentTPSAsync(),
                AverageBlockTime = await GetAverageBlockTimeAsync(),
                NetworkHashRate = await GetNetworkHashRateAsync(),
                ActiveValidators = await GetActiveValidatorsAsync(),
                LastUpdated = DateTimeOffset.UtcNow
            };

            _cache.Set(cacheKey, stats, TimeSpan.FromMinutes(1));
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blockchain stats");
            return GetDefaultStats();
        }
    }

    public async Task<List<ChartDataPoint>> GetBlockTimeChartAsync(int hours = 24)
    {
        try
        {
            var cacheKey = $"block_time_chart_{hours}";
            if (_cache.TryGetValue(cacheKey, out List<ChartDataPoint>? cached))
            {
                return cached!;
            }

            var data = new List<ChartDataPoint>();
            var now = DateTimeOffset.UtcNow;
            var random = new Random();

            for (int i = hours; i >= 0; i--)
            {
                var timestamp = now.AddHours(-i);
                var blockTime = 0.4 + (random.NextDouble() * 0.2); // 0.4-0.6 seconds

                data.Add(new ChartDataPoint
                {
                    Timestamp = timestamp,
                    Value = blockTime,
                    Label = timestamp.ToString("HH:mm")
                });
            }

            _cache.Set(cacheKey, data, TimeSpan.FromMinutes(5));
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting block time chart");
            return new List<ChartDataPoint>();
        }
    }

    public async Task<List<ChartDataPoint>> GetTPSChartAsync(int hours = 24)
    {
        try
        {
            var cacheKey = $"tps_chart_{hours}";
            if (_cache.TryGetValue(cacheKey, out List<ChartDataPoint>? cached))
            {
                return cached!;
            }

            var data = new List<ChartDataPoint>();
            var now = DateTimeOffset.UtcNow;
            var random = new Random();

            for (int i = hours; i >= 0; i--)
            {
                var timestamp = now.AddHours(-i);
                var tps = 1000 + (random.NextDouble() * 4000); // 1000-5000 TPS

                data.Add(new ChartDataPoint
                {
                    Timestamp = timestamp,
                    Value = tps,
                    Label = timestamp.ToString("HH:mm")
                });
            }

            _cache.Set(cacheKey, data, TimeSpan.FromMinutes(5));
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TPS chart");
            return new List<ChartDataPoint>();
        }
    }

    public async Task<List<ChartDataPoint>> GetTransactionVolumeChartAsync(int days = 7)
    {
        try
        {
            var cacheKey = $"tx_volume_chart_{days}";
            if (_cache.TryGetValue(cacheKey, out List<ChartDataPoint>? cached))
            {
                return cached!;
            }

            var data = new List<ChartDataPoint>();
            var now = DateTimeOffset.UtcNow;
            var random = new Random();

            for (int i = days; i >= 0; i--)
            {
                var timestamp = now.AddDays(-i);
                var volume = 50000000 + (random.NextDouble() * 100000000); // 50M-150M LKS

                data.Add(new ChartDataPoint
                {
                    Timestamp = timestamp,
                    Value = volume,
                    Label = timestamp.ToString("MM/dd")
                });
            }

            _cache.Set(cacheKey, data, TimeSpan.FromMinutes(10));
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction volume chart");
            return new List<ChartDataPoint>();
        }
    }

    public async Task<NetworkHealth> GetNetworkHealthAsync()
    {
        try
        {
            const string cacheKey = "network_health";
            if (_cache.TryGetValue(cacheKey, out NetworkHealth? cached))
            {
                return cached!;
            }

            var health = new NetworkHealth
            {
                Status = "Healthy",
                Uptime = 99.98,
                ConsensusHealth = 100.0,
                NetworkLatency = 45.2,
                ValidatorParticipation = 98.5,
                LastUpdated = DateTimeOffset.UtcNow
            };

            _cache.Set(cacheKey, health, TimeSpan.FromMinutes(2));
            return health;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting network health");
            return new NetworkHealth
            {
                Status = "Unknown",
                Uptime = 0,
                ConsensusHealth = 0,
                NetworkLatency = 0,
                ValidatorParticipation = 0,
                LastUpdated = DateTimeOffset.UtcNow
            };
        }
    }

    private async Task<ulong> GetTotalBlocksAsync()
    {
        // Simulate getting total blocks
        await Task.Delay(1);
        return (ulong)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 400); // ~400ms block time
    }

    private async Task<ulong> GetTotalTransactionsAsync()
    {
        await Task.Delay(1);
        var totalBlocks = await GetTotalBlocksAsync();
        return totalBlocks * 150; // ~150 transactions per block
    }

    private async Task<ulong> GetTotalAddressesAsync()
    {
        await Task.Delay(1);
        return 2_500_000; // 2.5M addresses
    }

    private async Task<UInt256> GetCirculatingSupplyAsync()
    {
        await Task.Delay(1);
        return UInt256.Parse("25000000000000000000000000000"); // 25B LKS
    }

    private async Task<decimal> GetMarketCapAsync()
    {
        await Task.Delay(1);
        var supply = await GetCirculatingSupplyAsync();
        var price = 0.85m; // $0.85 per LKS
        return (decimal)supply / 1000000000000000000m * price; // Convert from wei and multiply by price
    }

    private async Task<double> GetCurrentTPSAsync()
    {
        await Task.Delay(1);
        var random = new Random();
        return 2500 + (random.NextDouble() * 2000); // 2500-4500 TPS
    }

    private async Task<double> GetAverageBlockTimeAsync()
    {
        await Task.Delay(1);
        return 0.42; // 420ms average block time
    }

    private async Task<double> GetNetworkHashRateAsync()
    {
        await Task.Delay(1);
        return 1250000; // 1.25 TH/s (simulated)
    }

    private async Task<int> GetActiveValidatorsAsync()
    {
        await Task.Delay(1);
        return 1247; // Active validators
    }

    private BlockchainStats GetDefaultStats()
    {
        return new BlockchainStats
        {
            TotalBlocks = 0,
            TotalTransactions = 0,
            TotalAddresses = 0,
            CirculatingSupply = UInt256.Zero,
            MarketCap = 0,
            CurrentTPS = 0,
            AverageBlockTime = 0,
            NetworkHashRate = 0,
            ActiveValidators = 0,
            LastUpdated = DateTimeOffset.UtcNow
        };
    }
}

public class BlockchainStats
{
    public required ulong TotalBlocks { get; set; }
    public required ulong TotalTransactions { get; set; }
    public required ulong TotalAddresses { get; set; }
    public required UInt256 CirculatingSupply { get; set; }
    public required decimal MarketCap { get; set; }
    public required double CurrentTPS { get; set; }
    public required double AverageBlockTime { get; set; }
    public required double NetworkHashRate { get; set; }
    public required int ActiveValidators { get; set; }
    public required DateTimeOffset LastUpdated { get; set; }

    public string CirculatingSupplyFormatted => FormatLKS(CirculatingSupply);
    public string MarketCapFormatted => $"${MarketCap:N0}";
    public string CurrentTPSFormatted => $"{CurrentTPS:N0} TPS";
    public string AverageBlockTimeFormatted => $"{AverageBlockTime:F2}s";
    public string NetworkHashRateFormatted => $"{NetworkHashRate:N0} H/s";

    private string FormatLKS(UInt256 wei)
    {
        var lks = (double)wei / 1000000000000000000.0;
        if (lks >= 1000000000) return $"{lks / 1000000000:F1}B LKS";
        if (lks >= 1000000) return $"{lks / 1000000:F1}M LKS";
        if (lks >= 1000) return $"{lks / 1000:F1}K LKS";
        return $"{lks:F2} LKS";
    }
}

public class ChartDataPoint
{
    public required DateTimeOffset Timestamp { get; set; }
    public required double Value { get; set; }
    public required string Label { get; set; }
}

public class NetworkHealth
{
    public required string Status { get; set; }
    public required double Uptime { get; set; }
    public required double ConsensusHealth { get; set; }
    public required double NetworkLatency { get; set; }
    public required double ValidatorParticipation { get; set; }
    public required DateTimeOffset LastUpdated { get; set; }

    public string UptimeFormatted => $"{Uptime:F2}%";
    public string ConsensusHealthFormatted => $"{ConsensusHealth:F1}%";
    public string NetworkLatencyFormatted => $"{NetworkLatency:F1}ms";
    public string ValidatorParticipationFormatted => $"{ValidatorParticipation:F1}%";
}
