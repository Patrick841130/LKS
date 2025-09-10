using CommandLine;
using LksBrothers.Consensus.Engine;
using LksBrothers.Execution.Engine;
using LksBrothers.Networking.P2P;
using LksBrothers.Node.Services;
using LksBrothers.Stablecoin.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace LksBrothers.Node;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Parse command line arguments
        var result = Parser.Default.ParseArguments<NodeOptions>(args);
        
        return await result.MapResult(
            async options => await RunNodeAsync(options),
            errors => Task.FromResult(1)
        );
    }
    
    private static async Task<int> RunNodeAsync(NodeOptions options)
    {
        try
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File($"logs/lks-node-{DateTime.UtcNow:yyyyMMdd}.log", 
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();
            
            Log.Information("Starting LKS Brothers Node v1.0.0");
            Log.Information("Node ID: {NodeId}", options.NodeId);
            Log.Information("Network: {Network}", options.Network);
            Log.Information("Listen Port: {Port}", options.Port);
            
            // Build host
            var host = CreateHostBuilder(args, options).Build();
            
            // Start the node
            await host.RunAsync();
            
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Node failed to start");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    
    private static IHostBuilder CreateHostBuilder(string[] args, NodeOptions options)
    {
        return Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false)
                      .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                      .AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Configure options
                services.Configure<NetworkOptions>(context.Configuration.GetSection("Network"));
                services.Configure<NodeConfiguration>(context.Configuration.GetSection("Node"));
                
                // Override with command line options
                services.Configure<NetworkOptions>(netOpts =>
                {
                    netOpts.ListenPort = options.Port;
                    netOpts.NodeId = options.NodeId;
                    netOpts.ChainId = options.Network;
                });
                
                // Register core services
                services.AddSingleton<IConsensusEngine, ConsensusEngine>();
                services.AddSingleton<IExecutionEngine, EvmExecutionEngine>();
                services.AddSingleton<IStablecoinEngine, StablecoinEngine>();
                services.AddSingleton<INetworkManager, NetworkManager>();
                
                // Register node services
                services.AddSingleton<IBlockchainService, BlockchainService>();
                services.AddSingleton<IValidatorService, ValidatorService>();
                services.AddSingleton<ITransactionPool, TransactionPool>();
                services.AddSingleton<IStateService, StateService>();
                
                // Register networking services
                services.AddSingleton<IPeerDiscovery, PeerDiscoveryService>();
                services.AddSingleton<IMessageHandler, MessageHandlerService>();
                services.AddSingleton<IDoSProtection, DoSProtectionService>();
                
                // Register hosted services
                services.AddHostedService<NodeService>();
                services.AddHostedService<NetworkService>();
                services.AddHostedService<ConsensusService>();
                services.AddHostedService<MempoolService>();
            });
    }
}

[Verb("run", isDefault: true, HelpText = "Run the LKS Brothers node")]
public class NodeOptions
{
    [Option('p', "port", Required = false, Default = 30303, HelpText = "P2P listen port")]
    public int Port { get; set; }
    
    [Option('n', "network", Required = false, Default = "mainnet", HelpText = "Network to connect to (mainnet, testnet, devnet)")]
    public string Network { get; set; } = "mainnet";
    
    [Option("node-id", Required = false, HelpText = "Unique node identifier")]
    public string NodeId { get; set; } = Guid.NewGuid().ToString();
    
    [Option("validator", Required = false, Default = false, HelpText = "Run as validator node")]
    public bool IsValidator { get; set; }
    
    [Option("validator-key", Required = false, HelpText = "Validator private key file")]
    public string? ValidatorKeyFile { get; set; }
    
    [Option("data-dir", Required = false, Default = "./data", HelpText = "Data directory")]
    public string DataDirectory { get; set; } = "./data";
    
    [Option("bootstrap", Required = false, HelpText = "Bootstrap node addresses (comma-separated)")]
    public string? BootstrapNodes { get; set; }
    
    [Option("rpc-port", Required = false, Default = 8545, HelpText = "JSON-RPC port")]
    public int RpcPort { get; set; }
    
    [Option("enable-rpc", Required = false, Default = true, HelpText = "Enable JSON-RPC server")]
    public bool EnableRpc { get; set; }
    
    [Option("log-level", Required = false, Default = "Information", HelpText = "Log level (Trace, Debug, Information, Warning, Error, Fatal)")]
    public string LogLevel { get; set; } = "Information";
}

public class NodeConfiguration
{
    public string DataDirectory { get; set; } = "./data";
    public bool IsValidator { get; set; }
    public string? ValidatorKeyFile { get; set; }
    public int RpcPort { get; set; } = 8545;
    public bool EnableRpc { get; set; } = true;
    public string LogLevel { get; set; } = "Information";
}
