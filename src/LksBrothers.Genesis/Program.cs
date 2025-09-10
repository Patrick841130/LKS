using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LksBrothers.Genesis.Services;
using LksBrothers.Core.Models;
using LksBrothers.StateManagement.Services;
using LksBrothers.Consensus.Engine;
using LksBrothers.Hooks.Engine;

namespace LksBrothers.Genesis;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 LKS COIN MAINNET GENESIS BLOCK CREATOR 🚀");
        Console.WriteLine("============================================");
        Console.WriteLine();

        var host = CreateHostBuilder(args).Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var genesisService = host.Services.GetRequiredService<GenesisService>();

        try
        {
            logger.LogInformation("Starting LKS COIN mainnet genesis block creation...");
            
            // Create genesis configuration
            var genesisConfig = new GenesisConfiguration
            {
                ChainId = 1000,
                NetworkName = "LKS COIN Mainnet",
                GenesisTime = DateTimeOffset.UtcNow,
                InitialSupply = UInt256.Parse("50000000000000000000000000000"), // 50B LKS
                FoundationAddress = "lks1foundation000000000000000000000000000000",
                ValidatorStakeRequired = UInt256.Parse("1000000000000000000000000"), // 1M LKS
                SlotDuration = TimeSpan.FromMilliseconds(400),
                EpochLength = 432000, // ~5 days at 400ms slots
                InitialValidators = new List<ValidatorInfo>
                {
                    new ValidatorInfo
                    {
                        Address = "lks1validator1000000000000000000000000000000",
                        PublicKey = "validator1_public_key_placeholder",
                        Stake = UInt256.Parse("5000000000000000000000000"), // 5M LKS
                        Commission = 0.05 // 5%
                    },
                    new ValidatorInfo
                    {
                        Address = "lks1validator2000000000000000000000000000000",
                        PublicKey = "validator2_public_key_placeholder", 
                        Stake = UInt256.Parse("3000000000000000000000000"), // 3M LKS
                        Commission = 0.07 // 7%
                    },
                    new ValidatorInfo
                    {
                        Address = "lks1validator3000000000000000000000000000000",
                        PublicKey = "validator3_public_key_placeholder",
                        Stake = UInt256.Parse("2000000000000000000000000"), // 2M LKS
                        Commission = 0.08 // 8%
                    }
                }
            };

            // Generate genesis block
            var genesisBlock = await genesisService.CreateGenesisBlockAsync(genesisConfig);
            
            // Save genesis data
            await genesisService.SaveGenesisDataAsync(genesisBlock, genesisConfig);
            
            // Display results
            DisplayGenesisResults(genesisBlock, genesisConfig);
            
            logger.LogInformation("✅ LKS COIN mainnet genesis block created successfully!");
            
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to create genesis block");
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddLogging();
                services.AddSingleton<GenesisService>();
                services.AddSingleton<StateService>();
                services.AddSingleton<ProofOfHistoryEngine>();
                services.AddSingleton<HookExecutor>();
            });

    static void DisplayGenesisResults(Block genesisBlock, GenesisConfiguration config)
    {
        Console.WriteLine();
        Console.WriteLine("🎉 GENESIS BLOCK CREATED SUCCESSFULLY! 🎉");
        Console.WriteLine("==========================================");
        Console.WriteLine();
        Console.WriteLine($"📋 Network: {config.NetworkName}");
        Console.WriteLine($"🆔 Chain ID: {config.ChainId}");
        Console.WriteLine($"🕐 Genesis Time: {config.GenesisTime:yyyy-MM-dd HH:mm:ss UTC}");
        Console.WriteLine($"🔗 Genesis Hash: {genesisBlock.Hash}");
        Console.WriteLine($"📦 Block Number: {genesisBlock.Number}");
        Console.WriteLine($"💰 Initial Supply: {FormatLKS(config.InitialSupply)}");
        Console.WriteLine($"🏛️ Foundation Address: {config.FoundationAddress}");
        Console.WriteLine($"⏱️ Slot Duration: {config.SlotDuration.TotalMilliseconds}ms");
        Console.WriteLine($"📊 Epoch Length: {config.EpochLength:N0} slots");
        Console.WriteLine($"✅ Initial Validators: {config.InitialValidators.Count}");
        Console.WriteLine();
        
        Console.WriteLine("🔐 INITIAL VALIDATORS:");
        Console.WriteLine("=====================");
        foreach (var validator in config.InitialValidators)
        {
            Console.WriteLine($"  • {validator.Address}");
            Console.WriteLine($"    Stake: {FormatLKS(validator.Stake)}");
            Console.WriteLine($"    Commission: {validator.Commission:P1}");
            Console.WriteLine();
        }
        
        Console.WriteLine("💎 TOKEN DISTRIBUTION:");
        Console.WriteLine("======================");
        Console.WriteLine($"  • Foundation Reserve: {FormatLKS(config.InitialSupply * 40 / 100)} (40%)");
        Console.WriteLine($"  • Public Distribution: {FormatLKS(config.InitialSupply * 30 / 100)} (30%)");
        Console.WriteLine($"  • Validator Rewards: {FormatLKS(config.InitialSupply * 15 / 100)} (15%)");
        Console.WriteLine($"  • Development Fund: {FormatLKS(config.InitialSupply * 10 / 100)} (10%)");
        Console.WriteLine($"  • Strategic Partners: {FormatLKS(config.InitialSupply * 5 / 100)} (5%)");
        Console.WriteLine();
        
        Console.WriteLine("🚀 MAINNET READY FOR LAUNCH! 🚀");
    }

    static string FormatLKS(UInt256 wei)
    {
        var lks = (double)wei / 1000000000000000000.0;
        if (lks >= 1000000000) return $"{lks / 1000000000:F1}B LKS";
        if (lks >= 1000000) return $"{lks / 1000000:F1}M LKS";
        if (lks >= 1000) return $"{lks / 1000:F1}K LKS";
        return $"{lks:F2} LKS";
    }
}
