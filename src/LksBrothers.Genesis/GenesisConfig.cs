using System.Numerics;

namespace LksBrothers.Genesis
{
    public class GenesisConfig
    {
        // Updated to 1 billion LKS coins as requested
        public static readonly BigInteger TOTAL_SUPPLY = BigInteger.Parse("1000000000000000000000000000"); // 1 billion LKS with 18 decimals
        
        public static readonly Dictionary<string, BigInteger> INITIAL_DISTRIBUTION = new()
        {
            // Foundation allocation (40% - 400M LKS)
            ["lks1foundation_treasury"] = BigInteger.Parse("400000000000000000000000000"),
            
            // Ecosystem services allocation (30% - 300M LKS)
            ["lks1ecosystem_services"] = BigInteger.Parse("300000000000000000000000000"),
            
            // IP PATENT service allocation (15% - 150M LKS)
            ["lks1ip_patent_service"] = BigInteger.Parse("150000000000000000000000000"),
            
            // Public sale and rewards (10% - 100M LKS)
            ["lks1public_rewards"] = BigInteger.Parse("100000000000000000000000000"),
            
            // Team and advisors (5% - 50M LKS)
            ["lks1team_advisors"] = BigInteger.Parse("50000000000000000000000000")
        };

        public static readonly List<ValidatorInfo> GENESIS_VALIDATORS = new()
        {
            new ValidatorInfo
            {
                Address = "lks1validator_001",
                PublicKey = "validator_001_pubkey",
                Stake = BigInteger.Parse("10000000000000000000000000"), // 10M LKS
                Commission = 0.05m
            },
            new ValidatorInfo
            {
                Address = "lks1validator_002", 
                PublicKey = "validator_002_pubkey",
                Stake = BigInteger.Parse("10000000000000000000000000"), // 10M LKS
                Commission = 0.05m
            },
            new ValidatorInfo
            {
                Address = "lks1validator_003",
                PublicKey = "validator_003_pubkey", 
                Stake = BigInteger.Parse("10000000000000000000000000"), // 10M LKS
                Commission = 0.05m
            }
        };

        public static readonly NetworkConfig NETWORK_CONFIG = new()
        {
            ChainId = "lks-mainnet-1",
            NetworkName = "LKS Network Mainnet",
            GenesisTime = DateTime.UtcNow,
            BlockTime = TimeSpan.FromMilliseconds(400),
            EpochLength = 432000, // ~5 days at 400ms blocks
            ZeroFees = true,
            MaxTps = 65000,
            ConsensusAlgorithm = "Hybrid PoH + PoS"
        };

        public static readonly EcosystemServices ECOSYSTEM_SERVICES = new()
        {
            Services = new Dictionary<string, ServiceConfig>
            {
                ["ip-patent"] = new ServiceConfig
                {
                    Name = "IP PATENT",
                    Description = "Blockchain Intellectual Property Registration",
                    ServiceAddress = "lks1ip_patent_service",
                    AcceptedCurrency = "LKS",
                    ZeroFees = true,
                    SubscriptionPlans = new Dictionary<string, SubscriptionPlan>
                    {
                        ["premium"] = new SubscriptionPlan
                        {
                            Name = "IP PATENT Premium",
                            TotalCostUsd = 5000m,
                            DurationMonths = 12,
                            MonthlyPaymentUsd = 416.67m, // $5000 / 12 months
                            LksAllocation = BigInteger.Parse("50000000000000000000000"), // 50K LKS held in escrow
                            Features = new[]
                            {
                                "Unlimited IP registrations",
                                "Priority blockchain recording",
                                "Premium certificate templates",
                                "Legal document templates",
                                "24/7 support access"
                            }
                        }
                    }
                },
                ["lks-summit"] = new ServiceConfig
                {
                    Name = "LKS SUMMIT",
                    Description = "Event tickets and booth reservations",
                    ServiceAddress = "lks1summit_service",
                    AcceptedCurrency = "LKS",
                    ZeroFees = true
                },
                ["software-factory"] = new ServiceConfig
                {
                    Name = "Software Factory",
                    Description = "Custom software development and payment processing",
                    ServiceAddress = "lks1software_factory",
                    AcceptedCurrency = "LKS",
                    ZeroFees = true
                },
                ["vara"] = new ServiceConfig
                {
                    Name = "Vara",
                    Description = "Advanced cybersecurity services",
                    ServiceAddress = "lks1vara_service",
                    AcceptedCurrency = "LKS",
                    ZeroFees = true
                },
                ["stadium-tackle"] = new ServiceConfig
                {
                    Name = "Stadium Tackle",
                    Description = "Online gaming platform with NFT integration",
                    ServiceAddress = "lks1stadium_tackle",
                    AcceptedCurrency = "LKS",
                    ZeroFees = true
                },
                ["lks-capital"] = new ServiceConfig
                {
                    Name = "LKS Capital",
                    Description = "Crowdfunding platform for innovative projects",
                    ServiceAddress = "lks1capital_service",
                    AcceptedCurrency = "LKS",
                    ZeroFees = true
                }
            }
        };
    }

    public class ValidatorInfo
    {
        public string Address { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
        public BigInteger Stake { get; set; }
        public decimal Commission { get; set; }
    }

    public class NetworkConfig
    {
        public string ChainId { get; set; } = string.Empty;
        public string NetworkName { get; set; } = string.Empty;
        public DateTime GenesisTime { get; set; }
        public TimeSpan BlockTime { get; set; }
        public int EpochLength { get; set; }
        public bool ZeroFees { get; set; }
        public int MaxTps { get; set; }
        public string ConsensusAlgorithm { get; set; } = string.Empty;
    }

    public class EcosystemServices
    {
        public Dictionary<string, ServiceConfig> Services { get; set; } = new();
    }

    public class ServiceConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ServiceAddress { get; set; } = string.Empty;
        public string AcceptedCurrency { get; set; } = "LKS";
        public bool ZeroFees { get; set; } = true;
        public Dictionary<string, SubscriptionPlan> SubscriptionPlans { get; set; } = new();
    }

    public class SubscriptionPlan
    {
        public string Name { get; set; } = string.Empty;
        public decimal TotalCostUsd { get; set; }
        public int DurationMonths { get; set; }
        public decimal MonthlyPaymentUsd { get; set; }
        public BigInteger LksAllocation { get; set; }
        public string[] Features { get; set; } = Array.Empty<string>();
    }
}
