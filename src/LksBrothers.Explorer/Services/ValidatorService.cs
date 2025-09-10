using System.Collections.Concurrent;

namespace LksBrothers.Explorer.Services
{
    public class ValidatorService
    {
        private readonly ConcurrentDictionary<string, ValidatorInfo> _validators;
        private readonly Random _random = new();

        public ValidatorService()
        {
            _validators = new ConcurrentDictionary<string, ValidatorInfo>();
            InitializeValidators();
        }

        private void InitializeValidators()
        {
            var validators = new[]
            {
                new ValidatorInfo
                {
                    Address = "lks1validator1staking7q8r",
                    Name = "LKS Validator 1",
                    Stake = 16_666_666_667m,
                    Commission = 0.05m,
                    Uptime = 99.98m,
                    BlocksProduced = 949_464,
                    Status = "Active",
                    VotingPower = 33.33m,
                    Location = "United States",
                    Website = "https://validator1.lksnetwork.io",
                    Identity = "LKS1",
                    LastSeen = DateTime.UtcNow
                },
                new ValidatorInfo
                {
                    Address = "lks1validator2staking9k3t",
                    Name = "LKS Validator 2",
                    Stake = 16_666_666_667m,
                    Commission = 0.05m,
                    Uptime = 99.97m,
                    BlocksProduced = 949_128,
                    Status = "Active",
                    VotingPower = 33.33m,
                    Location = "Singapore",
                    Website = "https://validator2.lksnetwork.io",
                    Identity = "LKS2",
                    LastSeen = DateTime.UtcNow
                },
                new ValidatorInfo
                {
                    Address = "lks1validator3staking2m7p",
                    Name = "LKS Validator 3",
                    Stake = 16_666_666_666m,
                    Commission = 0.05m,
                    Uptime = 99.96m,
                    BlocksProduced = 948_800,
                    Status = "Active",
                    VotingPower = 33.34m,
                    Location = "Germany",
                    Website = "https://validator3.lksnetwork.io",
                    Identity = "LKS3",
                    LastSeen = DateTime.UtcNow
                }
            };

            foreach (var validator in validators)
            {
                _validators.TryAdd(validator.Address, validator);
            }
        }

        public IEnumerable<ValidatorInfo> GetAllValidators()
        {
            return _validators.Values.OrderByDescending(v => v.VotingPower);
        }

        public ValidatorInfo? GetValidator(string address)
        {
            _validators.TryGetValue(address, out var validator);
            return validator;
        }

        public ValidatorStats GetValidatorStats()
        {
            var validators = _validators.Values;
            return new ValidatorStats
            {
                TotalValidators = validators.Count(),
                ActiveValidators = validators.Count(v => v.Status == "Active"),
                TotalStake = validators.Sum(v => v.Stake),
                AverageUptime = validators.Average(v => v.Uptime),
                TotalBlocksProduced = validators.Sum(v => v.BlocksProduced),
                LastUpdated = DateTime.UtcNow
            };
        }

        public IEnumerable<ValidatorPerformance> GetValidatorPerformance(string? address = null, int days = 7)
        {
            var validators = address != null 
                ? _validators.Values.Where(v => v.Address == address)
                : _validators.Values;

            var performance = new List<ValidatorPerformance>();

            foreach (var validator in validators)
            {
                for (int i = days - 1; i >= 0; i--)
                {
                    var date = DateTime.UtcNow.AddDays(-i).Date;
                    performance.Add(new ValidatorPerformance
                    {
                        ValidatorAddress = validator.Address,
                        Date = date,
                        BlocksProduced = _random.Next(800, 1200),
                        BlocksMissed = _random.Next(0, 5),
                        Uptime = Math.Round(99.5 + _random.NextDouble() * 0.5, 2),
                        Rewards = Math.Round(_random.NextDouble() * 100, 2),
                        Slashes = 0
                    });
                }
            }

            return performance.OrderBy(p => p.Date);
        }

        public void UpdateValidatorStatus(string address, string status)
        {
            if (_validators.TryGetValue(address, out var validator))
            {
                validator.Status = status;
                validator.LastSeen = DateTime.UtcNow;
            }
        }

        public void UpdateValidatorMetrics(string address, decimal uptime, long blocksProduced)
        {
            if (_validators.TryGetValue(address, out var validator))
            {
                validator.Uptime = uptime;
                validator.BlocksProduced = blocksProduced;
                validator.LastSeen = DateTime.UtcNow;
            }
        }
    }

    public class ValidatorInfo
    {
        public string Address { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Stake { get; set; }
        public decimal Commission { get; set; }
        public decimal Uptime { get; set; }
        public long BlocksProduced { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal VotingPower { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public string Identity { get; set; } = string.Empty;
        public DateTime LastSeen { get; set; }
    }

    public class ValidatorStats
    {
        public int TotalValidators { get; set; }
        public int ActiveValidators { get; set; }
        public decimal TotalStake { get; set; }
        public double AverageUptime { get; set; }
        public long TotalBlocksProduced { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class ValidatorPerformance
    {
        public string ValidatorAddress { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int BlocksProduced { get; set; }
        public int BlocksMissed { get; set; }
        public double Uptime { get; set; }
        public double Rewards { get; set; }
        public int Slashes { get; set; }
    }
}
