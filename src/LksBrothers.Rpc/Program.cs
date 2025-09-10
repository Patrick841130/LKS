using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using LksBrothers.Rpc.Services;
using LksBrothers.Rpc.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "LKS Brothers Mainnet RPC API", 
        Version = "v1",
        Description = "JSON-RPC and REST API for LKS Brothers blockchain"
    });
});

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    // Default policy for REST endpoints
    options.AddFixedWindowLimiter("DefaultPolicy", configure =>
    {
        configure.PermitLimit = 100;
        configure.Window = TimeSpan.FromMinutes(1);
        configure.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        configure.QueueLimit = 50;
    });

    // Stricter policy for JSON-RPC endpoints
    options.AddFixedWindowLimiter("JsonRpcPolicy", configure =>
    {
        configure.PermitLimit = 200;
        configure.Window = TimeSpan.FromMinutes(1);
        configure.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        configure.QueueLimit = 100;
    });

    options.RejectionStatusCode = 429;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register services
builder.Services.AddScoped<JsonRpcService>();
builder.Services.AddScoped<IBlockchainService, BlockchainServiceStub>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "LKS Brothers RPC API v1");
        c.RoutePrefix = "docs";
    });
}

app.UseRateLimiter();
app.UseCors();
app.UseRouting();

app.MapControllers();

// Health check endpoint
app.MapGet("/", () => new { 
    service = "LKS Brothers Mainnet RPC", 
    version = "1.0.0",
    endpoints = new[] { "/api/blockchain", "/api/stablecoin", "/docs" },
    jsonRpc = "2.0"
});

app.Run();

// Temporary stub implementation until we integrate with the actual blockchain service
public class BlockchainServiceStub : IBlockchainService
{
    public Task<BlockchainStatus> GetStatusAsync()
    {
        return Task.FromResult(new BlockchainStatus
        {
            ChainId = "lks-mainnet",
            BlockHeight = 12345,
            LatestBlockHash = LksBrothers.Core.Primitives.Hash.ComputeHash("latest_block"u8.ToArray()),
            PeerCount = 25,
            IsSyncing = false,
            Version = "1.0.0"
        });
    }

    public Task<LksBrothers.Core.Models.Block?> GetBlockAsync(ulong blockNumber)
    {
        // Return null for now - will be implemented when state management is ready
        return Task.FromResult<LksBrothers.Core.Models.Block?>(null);
    }

    public Task<LksBrothers.Core.Models.Block?> GetLatestBlockAsync()
    {
        return Task.FromResult<LksBrothers.Core.Models.Block?>(null);
    }

    public Task<LksBrothers.Core.Models.Transaction?> GetTransactionAsync(LksBrothers.Core.Primitives.Hash txHash)
    {
        return Task.FromResult<LksBrothers.Core.Models.Transaction?>(null);
    }

    public Task<LksBrothers.Core.Primitives.Hash> SubmitTransactionAsync(LksBrothers.Core.Models.Transaction transaction)
    {
        return Task.FromResult(LksBrothers.Core.Primitives.Hash.ComputeHash("submitted_tx"u8.ToArray()));
    }

    public Task<LksBrothers.Core.Primitives.UInt256> GetBalanceAsync(LksBrothers.Core.Primitives.Address address)
    {
        return Task.FromResult(new LksBrothers.Core.Primitives.UInt256(1000000000000000000)); // 1 ETH equivalent
    }

    public Task<ulong> GetNonceAsync(LksBrothers.Core.Primitives.Address address)
    {
        return Task.FromResult(0UL);
    }
}
