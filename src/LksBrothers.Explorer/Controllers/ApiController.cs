using Microsoft.AspNetCore.Mvc;
using LksBrothers.Core.Models;
using LksBrothers.Core.Services;
using System.Text.Json;

namespace LksBrothers.Explorer.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly ILogger<ApiController> _logger;
        private readonly Random _random = new();

        public ApiController(ILogger<ApiController> logger)
        {
            _logger = logger;
        }

        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }

        [HttpGet("network/stats")]
        public IActionResult GetNetworkStats()
        {
            var stats = new
            {
                blockHeight = 2_847_392 + _random.Next(0, 100),
                currentTps = _random.Next(58000, 65000),
                totalTransactions = 847_293_847L + _random.Next(0, 10000),
                activeValidators = 3,
                networkHealth = "Excellent",
                avgBlockTime = _random.Next(380, 420),
                totalSupply = "50,000,000,000",
                circulatingSupply = "25,000,000,000",
                marketCap = "$0.00",
                price = "$0.00",
                volume24h = "$0.00",
                lastUpdated = DateTime.UtcNow
            };

            return Ok(stats);
        }

        [HttpGet("blocks/latest")]
        public IActionResult GetLatestBlocks([FromQuery] int limit = 10)
        {
            var blocks = new List<object>();
            var baseHeight = 2_847_392;

            for (int i = 0; i < Math.Min(limit, 50); i++)
            {
                blocks.Add(new
                {
                    height = baseHeight - i,
                    hash = GenerateHash(),
                    timestamp = DateTime.UtcNow.AddSeconds(-i * 0.4),
                    transactions = _random.Next(1500, 3000),
                    validator = $"lks1validator{_random.Next(1, 4)}...{GenerateShortHash()}",
                    size = _random.Next(800, 1200) + " KB",
                    gasUsed = _random.Next(15000000, 25000000),
                    gasLimit = 30000000
                });
            }

            return Ok(blocks);
        }

        [HttpGet("transactions/latest")]
        public IActionResult GetLatestTransactions([FromQuery] int limit = 10)
        {
            var transactions = new List<object>();

            for (int i = 0; i < Math.Min(limit, 50); i++)
            {
                var txTypes = new[] { "Transfer", "Stake", "Unstake", "Smart Contract", "Cross-Chain" };
                var amount = _random.NextDouble() * 10000;
                
                transactions.Add(new
                {
                    hash = GenerateHash(),
                    timestamp = DateTime.UtcNow.AddSeconds(-i * 2),
                    from = $"lks1{GenerateShortHash()}...{GenerateShortHash()}",
                    to = $"lks1{GenerateShortHash()}...{GenerateShortHash()}",
                    amount = $"{amount:F2} LKS",
                    fee = "$0.00",
                    type = txTypes[_random.Next(txTypes.Length)],
                    status = "Confirmed",
                    blockHeight = 2_847_392 - _random.Next(0, 100)
                });
            }

            return Ok(transactions);
        }

        [HttpGet("blocks/{height}")]
        public IActionResult GetBlock(long height)
        {
            var block = new
            {
                height = height,
                hash = GenerateHash(),
                timestamp = DateTime.UtcNow.AddMinutes(-_random.Next(0, 1440)),
                previousHash = GenerateHash(),
                merkleRoot = GenerateHash(),
                validator = $"lks1validator{_random.Next(1, 4)}...{GenerateShortHash()}",
                transactions = _random.Next(1500, 3000),
                size = _random.Next(800, 1200) + " KB",
                gasUsed = _random.Next(15000000, 25000000),
                gasLimit = 30000000,
                difficulty = "0x1bc16d674ec80000",
                nonce = _random.Next(1000000, 9999999),
                reward = "0.00 LKS"
            };

            return Ok(block);
        }

        [HttpGet("transactions/{hash}")]
        public IActionResult GetTransaction(string hash)
        {
            var transaction = new
            {
                hash = hash,
                timestamp = DateTime.UtcNow.AddMinutes(-_random.Next(0, 60)),
                from = $"lks1{GenerateShortHash()}...{GenerateShortHash()}",
                to = $"lks1{GenerateShortHash()}...{GenerateShortHash()}",
                amount = $"{_random.NextDouble() * 10000:F2} LKS",
                fee = "$0.00",
                gasUsed = _random.Next(21000, 500000),
                gasPrice = "0 gwei",
                status = "Confirmed",
                blockHeight = 2_847_392 - _random.Next(0, 100),
                blockHash = GenerateHash(),
                transactionIndex = _random.Next(0, 2999),
                confirmations = _random.Next(1, 100)
            };

            return Ok(transaction);
        }

        [HttpGet("validators")]
        public IActionResult GetValidators()
        {
            var validators = new[]
            {
                new
                {
                    address = "lks1validator1staking7q8r",
                    name = "LKS Validator 1",
                    stake = "16,666,666,667 LKS",
                    commission = "5%",
                    uptime = "99.98%",
                    blocksProduced = 949_464,
                    status = "Active",
                    votingPower = "33.33%"
                },
                new
                {
                    address = "lks1validator2staking9k3t",
                    name = "LKS Validator 2", 
                    stake = "16,666,666,667 LKS",
                    commission = "5%",
                    uptime = "99.97%",
                    blocksProduced = 949_128,
                    status = "Active",
                    votingPower = "33.33%"
                },
                new
                {
                    address = "lks1validator3staking2m7p",
                    name = "LKS Validator 3",
                    stake = "16,666,666,666 LKS",
                    commission = "5%",
                    uptime = "99.96%",
                    blocksProduced = 948_800,
                    status = "Active",
                    votingPower = "33.34%"
                }
            };

            return Ok(validators);
        }

        [HttpGet("charts/tps")]
        public IActionResult GetTpsChart([FromQuery] string period = "24h")
        {
            var dataPoints = period switch
            {
                "7d" => 168, // 7 days * 24 hours
                "30d" => 720, // 30 days * 24 hours
                _ => 24 // 24 hours
            };

            var data = new List<object>();
            var now = DateTime.UtcNow;

            for (int i = dataPoints - 1; i >= 0; i--)
            {
                var timestamp = period switch
                {
                    "7d" => now.AddHours(-i),
                    "30d" => now.AddHours(-i),
                    _ => now.AddHours(-i)
                };

                data.Add(new
                {
                    timestamp = timestamp,
                    tps = _random.Next(58000, 65000),
                    blockTime = _random.Next(380, 420),
                    activeConnections = _random.Next(1200, 2500),
                    validatorPerformance = _random.Next(85, 99)
                });
            }

            return Ok(data);
        }

        [HttpGet("search")]
        public IActionResult Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { error = "Query parameter is required" });
            }

            // Simulate search results
            var results = new
            {
                query = query,
                type = DetermineSearchType(query),
                results = query.Length > 40 ? 
                    new[] { new { type = "transaction", hash = query, found = true } } :
                    query.All(char.IsDigit) ?
                    new[] { new { type = "block", height = long.Parse(query), found = true } } :
                    new[] { new { type = "address", address = query, found = true } }
            };

            return Ok(results);
        }

        private string DetermineSearchType(string query)
        {
            if (query.All(char.IsDigit))
                return "block";
            if (query.Length > 40)
                return "transaction";
            if (query.StartsWith("lks1"))
                return "address";
            return "unknown";
        }

        private string GenerateHash()
        {
            var bytes = new byte[32];
            _random.NextBytes(bytes);
            return "0x" + Convert.ToHexString(bytes).ToLower();
        }

        private string GenerateShortHash()
        {
            var bytes = new byte[4];
            _random.NextBytes(bytes);
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}
