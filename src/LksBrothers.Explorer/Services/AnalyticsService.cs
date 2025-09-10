using System.Collections.Concurrent;

namespace LksBrothers.Explorer.Services
{
    public class AnalyticsService
    {
        private readonly Random _random = new();
        private readonly ConcurrentDictionary<string, List<MetricDataPoint>> _metrics;

        public AnalyticsService()
        {
            _metrics = new ConcurrentDictionary<string, List<MetricDataPoint>>();
            InitializeMetrics();
        }

        private void InitializeMetrics()
        {
            // Initialize with 30 days of historical data
            var metrics = new[] { "tps", "blockTime", "networkActivity", "validatorPerformance", "gasUsage", "activeAddresses" };
            
            foreach (var metric in metrics)
            {
                var dataPoints = new List<MetricDataPoint>();
                for (int i = 720; i >= 0; i--) // 30 days * 24 hours
                {
                    dataPoints.Add(GenerateDataPoint(metric, DateTime.UtcNow.AddHours(-i)));
                }
                _metrics.TryAdd(metric, dataPoints);
            }
        }

        private MetricDataPoint GenerateDataPoint(string metric, DateTime timestamp)
        {
            return metric switch
            {
                "tps" => new MetricDataPoint
                {
                    Timestamp = timestamp,
                    Value = _random.Next(58000, 65000),
                    Label = "Transactions per Second"
                },
                "blockTime" => new MetricDataPoint
                {
                    Timestamp = timestamp,
                    Value = _random.Next(380, 420),
                    Label = "Block Time (ms)"
                },
                "networkActivity" => new MetricDataPoint
                {
                    Timestamp = timestamp,
                    Value = _random.Next(1200, 2500),
                    Label = "Active Connections"
                },
                "validatorPerformance" => new MetricDataPoint
                {
                    Timestamp = timestamp,
                    Value = _random.Next(85, 99),
                    Label = "Validator Performance (%)"
                },
                "gasUsage" => new MetricDataPoint
                {
                    Timestamp = timestamp,
                    Value = _random.Next(15000000, 25000000),
                    Label = "Gas Usage"
                },
                "activeAddresses" => new MetricDataPoint
                {
                    Timestamp = timestamp,
                    Value = _random.Next(50000, 75000),
                    Label = "Active Addresses"
                },
                _ => new MetricDataPoint
                {
                    Timestamp = timestamp,
                    Value = _random.Next(0, 100),
                    Label = "Unknown Metric"
                }
            };
        }

        public IEnumerable<MetricDataPoint> GetMetricData(string metric, string period = "24h")
        {
            if (!_metrics.TryGetValue(metric, out var data))
                return Enumerable.Empty<MetricDataPoint>();

            var cutoff = period switch
            {
                "7d" => DateTime.UtcNow.AddDays(-7),
                "30d" => DateTime.UtcNow.AddDays(-30),
                _ => DateTime.UtcNow.AddHours(-24)
            };

            return data.Where(d => d.Timestamp >= cutoff).OrderBy(d => d.Timestamp);
        }

        public NetworkAnalytics GetNetworkAnalytics()
        {
            var now = DateTime.UtcNow;
            var last24h = now.AddHours(-24);

            return new NetworkAnalytics
            {
                CurrentTps = _random.Next(58000, 65000),
                AverageTps24h = _random.Next(60000, 62000),
                PeakTps24h = _random.Next(63000, 65000),
                CurrentBlockTime = _random.Next(380, 420),
                AverageBlockTime24h = _random.Next(390, 410),
                TotalTransactions24h = _random.Next(5000000, 5500000),
                ActiveAddresses24h = _random.Next(65000, 75000),
                NetworkUtilization = _random.Next(75, 95),
                ConsensusHealth = _random.Next(95, 100),
                CrossChainVolume24h = _random.Next(1000000, 2000000),
                LastUpdated = now
            };
        }

        public IEnumerable<TransactionAnalytics> GetTransactionAnalytics(int days = 7)
        {
            var analytics = new List<TransactionAnalytics>();
            
            for (int i = days - 1; i >= 0; i--)
            {
                var date = DateTime.UtcNow.AddDays(-i).Date;
                analytics.Add(new TransactionAnalytics
                {
                    Date = date,
                    TotalTransactions = _random.Next(4500000, 5500000),
                    TransferTransactions = _random.Next(2000000, 2500000),
                    SmartContractTransactions = _random.Next(1500000, 2000000),
                    StakingTransactions = _random.Next(500000, 750000),
                    CrossChainTransactions = _random.Next(250000, 500000),
                    AverageTransactionSize = _random.Next(150, 300),
                    TotalVolume = _random.NextDouble() * 10000000,
                    UniqueAddresses = _random.Next(45000, 65000)
                });
            }

            return analytics.OrderBy(a => a.Date);
        }

        public ValidatorAnalytics GetValidatorAnalytics()
        {
            return new ValidatorAnalytics
            {
                TotalValidators = 3,
                ActiveValidators = 3,
                AverageUptime = 99.97,
                TotalStaked = 50_000_000_000m,
                AverageCommission = 5.0,
                BlocksProducedToday = _random.Next(200000, 220000),
                MissedBlocksToday = _random.Next(0, 10),
                SlashingEvents = 0,
                LastUpdated = DateTime.UtcNow
            };
        }

        public IEnumerable<GeographicDistribution> GetGeographicDistribution()
        {
            return new[]
            {
                new GeographicDistribution { Country = "United States", ValidatorCount = 1, StakePercentage = 33.33 },
                new GeographicDistribution { Country = "Singapore", ValidatorCount = 1, StakePercentage = 33.33 },
                new GeographicDistribution { Country = "Germany", ValidatorCount = 1, StakePercentage = 33.34 }
            };
        }

        public void AddMetricDataPoint(string metric, double value)
        {
            if (_metrics.TryGetValue(metric, out var data))
            {
                data.Add(new MetricDataPoint
                {
                    Timestamp = DateTime.UtcNow,
                    Value = value,
                    Label = GetMetricLabel(metric)
                });

                // Keep only last 30 days
                var cutoff = DateTime.UtcNow.AddDays(-30);
                data.RemoveAll(d => d.Timestamp < cutoff);
            }
        }

        private string GetMetricLabel(string metric)
        {
            return metric switch
            {
                "tps" => "Transactions per Second",
                "blockTime" => "Block Time (ms)",
                "networkActivity" => "Active Connections",
                "validatorPerformance" => "Validator Performance (%)",
                "gasUsage" => "Gas Usage",
                "activeAddresses" => "Active Addresses",
                _ => "Unknown Metric"
            };
        }
    }

    public class MetricDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class NetworkAnalytics
    {
        public int CurrentTps { get; set; }
        public int AverageTps24h { get; set; }
        public int PeakTps24h { get; set; }
        public int CurrentBlockTime { get; set; }
        public int AverageBlockTime24h { get; set; }
        public int TotalTransactions24h { get; set; }
        public int ActiveAddresses24h { get; set; }
        public int NetworkUtilization { get; set; }
        public int ConsensusHealth { get; set; }
        public int CrossChainVolume24h { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class TransactionAnalytics
    {
        public DateTime Date { get; set; }
        public int TotalTransactions { get; set; }
        public int TransferTransactions { get; set; }
        public int SmartContractTransactions { get; set; }
        public int StakingTransactions { get; set; }
        public int CrossChainTransactions { get; set; }
        public int AverageTransactionSize { get; set; }
        public double TotalVolume { get; set; }
        public int UniqueAddresses { get; set; }
    }

    public class ValidatorAnalytics
    {
        public int TotalValidators { get; set; }
        public int ActiveValidators { get; set; }
        public double AverageUptime { get; set; }
        public decimal TotalStaked { get; set; }
        public double AverageCommission { get; set; }
        public int BlocksProducedToday { get; set; }
        public int MissedBlocksToday { get; set; }
        public int SlashingEvents { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class GeographicDistribution
    {
        public string Country { get; set; } = string.Empty;
        public int ValidatorCount { get; set; }
        public double StakePercentage { get; set; }
    }
}
