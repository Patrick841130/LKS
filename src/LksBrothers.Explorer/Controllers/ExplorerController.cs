using Microsoft.AspNetCore.Mvc;
using LksBrothers.Explorer.Services;
using LksBrothers.Core.Primitives;

namespace LksBrothers.Explorer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExplorerController : ControllerBase
{
    private readonly ExplorerService _explorerService;
    private readonly BlockchainStatsService _statsService;

    public ExplorerController(ExplorerService explorerService, BlockchainStatsService statsService)
    {
        _explorerService = explorerService;
        _statsService = statsService;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _statsService.GetStatsAsync();
        return Ok(stats);
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetNetworkHealth()
    {
        var health = await _statsService.GetNetworkHealthAsync();
        return Ok(health);
    }

    [HttpGet("blocks/latest")]
    public async Task<IActionResult> GetLatestBlocks([FromQuery] int count = 10)
    {
        var blocks = await _explorerService.GetLatestBlocksAsync(count);
        return Ok(blocks);
    }

    [HttpGet("blocks/{blockHash}")]
    public async Task<IActionResult> GetBlock(string blockHash)
    {
        try
        {
            var hash = Hash.Parse(blockHash);
            var block = await _explorerService.GetBlockAsync(hash);
            return Ok(block);
        }
        catch (Exception ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("transactions/latest")]
    public async Task<IActionResult> GetLatestTransactions([FromQuery] int count = 20)
    {
        var transactions = await _explorerService.GetLatestTransactionsAsync(count);
        return Ok(transactions);
    }

    [HttpGet("transactions/{txHash}")]
    public async Task<IActionResult> GetTransaction(string txHash)
    {
        try
        {
            var hash = Hash.Parse(txHash);
            var transaction = await _explorerService.GetTransactionAsync(hash);
            return Ok(transaction);
        }
        catch (Exception ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("addresses/{address}")]
    public async Task<IActionResult> GetAddress(string address)
    {
        try
        {
            var addr = Address.Parse(address);
            var addressInfo = await _explorerService.GetAddressAsync(addr);
            return Ok(addressInfo);
        }
        catch (Exception ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { error = "Query parameter 'q' is required" });
        }

        var result = await _explorerService.SearchAsync(q);
        return Ok(result);
    }

    [HttpGet("charts/block-time")]
    public async Task<IActionResult> GetBlockTimeChart([FromQuery] int hours = 24)
    {
        var data = await _statsService.GetBlockTimeChartAsync(hours);
        return Ok(data);
    }

    [HttpGet("charts/tps")]
    public async Task<IActionResult> GetTPSChart([FromQuery] int hours = 24)
    {
        var data = await _statsService.GetTPSChartAsync(hours);
        return Ok(data);
    }

    [HttpGet("charts/volume")]
    public async Task<IActionResult> GetVolumeChart([FromQuery] int days = 7)
    {
        var data = await _statsService.GetTransactionVolumeChartAsync(days);
        return Ok(data);
    }
}
