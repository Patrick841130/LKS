using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace LksBrothers.Tests.IntegrationTests;

public class ExplorerApiTests : IClassFixture<WebApplicationFactory<LksBrothers.Explorer.Program>>
{
    private readonly WebApplicationFactory<LksBrothers.Explorer.Program> _factory;
    private readonly HttpClient _client;

    public ExplorerApiTests(WebApplicationFactory<LksBrothers.Explorer.Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetStats_ReturnsValidResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/explorer/stats");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<dynamic>(content);
        
        Assert.NotNull(stats);
    }

    [Fact]
    public async Task GetLatestBlocks_ReturnsBlockList()
    {
        // Act
        var response = await _client.GetAsync("/api/explorer/blocks/latest?count=10");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var blocks = JsonSerializer.Deserialize<dynamic[]>(content);
        
        Assert.NotNull(blocks);
        Assert.True(blocks.Length <= 10);
    }

    [Fact]
    public async Task GetBlock_WithValidNumber_ReturnsBlock()
    {
        // Arrange
        var blockNumber = 1;

        // Act
        var response = await _client.GetAsync($"/api/explorer/blocks/{blockNumber}");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var block = JsonSerializer.Deserialize<dynamic>(content);
        
        Assert.NotNull(block);
    }

    [Fact]
    public async Task GetTransactions_ReturnsTransactionList()
    {
        // Act
        var response = await _client.GetAsync("/api/explorer/transactions?page=1&pageSize=20");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var transactions = JsonSerializer.Deserialize<dynamic>(content);
        
        Assert.NotNull(transactions);
    }

    [Fact]
    public async Task SearchTransaction_WithValidHash_ReturnsTransaction()
    {
        // Arrange
        var txHash = "0x1234567890abcdef";

        // Act
        var response = await _client.GetAsync($"/api/explorer/search?query={txHash}");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        
        Assert.Contains("Healthy", content);
    }
}
