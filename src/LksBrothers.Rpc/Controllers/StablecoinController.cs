using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using LksBrothers.Core.Primitives;
using LksBrothers.Stablecoin.Engine;

namespace LksBrothers.Rpc.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("DefaultPolicy")]
public class StablecoinController : ControllerBase
{
    private readonly ILogger<StablecoinController> _logger;
    private readonly StablecoinEngine _stablecoinEngine;

    public StablecoinController(
        ILogger<StablecoinController> logger,
        StablecoinEngine stablecoinEngine)
    {
        _logger = logger;
        _stablecoinEngine = stablecoinEngine;
    }

    [HttpPost("mint")]
    public async Task<IActionResult> MintStablecoin([FromBody] MintRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _stablecoinEngine.MintAsync(
                Address.Parse(request.To),
                UInt256.Parse(request.Amount),
                request.CollateralAssets.Select(c => new CollateralAsset
                {
                    Asset = Address.Parse(c.Asset),
                    Amount = UInt256.Parse(c.Amount)
                }).ToList(),
                request.KycVerified,
                request.ComplianceData
            );

            if (!result.Success)
                return BadRequest(new { error = result.ErrorMessage });

            return Ok(new
            {
                transactionHash = result.TransactionHash?.ToString(),
                mintedAmount = result.MintedAmount?.ToString(),
                collateralLocked = result.CollateralLocked?.ToString(),
                fees = result.Fees?.ToString()
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error minting stablecoin");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("burn")]
    public async Task<IActionResult> BurnStablecoin([FromBody] BurnRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _stablecoinEngine.BurnAsync(
                Address.Parse(request.From),
                UInt256.Parse(request.Amount),
                request.ReleaseCollateral
            );

            if (!result.Success)
                return BadRequest(new { error = result.ErrorMessage });

            return Ok(new
            {
                transactionHash = result.TransactionHash?.ToString(),
                burnedAmount = result.BurnedAmount?.ToString(),
                collateralReleased = result.CollateralReleased?.ToString(),
                fees = result.Fees?.ToString()
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error burning stablecoin");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("balance/{address}")]
    public async Task<IActionResult> GetStablecoinBalance(string address)
    {
        try
        {
            if (!Address.TryParse(address, out var addr))
                return BadRequest(new { error = "Invalid address format" });

            var balance = await _stablecoinEngine.GetBalanceAsync(addr);
            
            return Ok(new { 
                address = addr.ToString(),
                balance = balance.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stablecoin balance for {Address}", address);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("collateral/{address}")]
    public async Task<IActionResult> GetCollateralInfo(string address)
    {
        try
        {
            if (!Address.TryParse(address, out var addr))
                return BadRequest(new { error = "Invalid address format" });

            var collateral = await _stablecoinEngine.GetCollateralInfoAsync(addr);
            
            return Ok(new
            {
                address = addr.ToString(),
                totalCollateralValue = collateral.TotalValue.ToString(),
                collateralRatio = collateral.Ratio,
                assets = collateral.Assets.Select(a => new
                {
                    asset = a.Asset.ToString(),
                    amount = a.Amount.ToString(),
                    value = a.Value.ToString()
                }).ToArray(),
                liquidationThreshold = collateral.LiquidationThreshold,
                isHealthy = collateral.IsHealthy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting collateral info for {Address}", address);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("settlement")]
    public async Task<IActionResult> ProcessSettlement([FromBody] SettlementRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _stablecoinEngine.ProcessSettlementAsync(
                Hash.Parse(request.SettlementId),
                Address.Parse(request.From),
                Address.Parse(request.To),
                UInt256.Parse(request.Amount),
                request.Reference,
                request.ComplianceData
            );

            if (!result.Success)
                return BadRequest(new { error = result.ErrorMessage });

            return Ok(new
            {
                settlementId = result.SettlementId?.ToString(),
                transactionHash = result.TransactionHash?.ToString(),
                settledAmount = result.SettledAmount?.ToString(),
                fees = result.Fees?.ToString(),
                status = result.Status
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing settlement");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("settlement/{settlementId}")]
    public async Task<IActionResult> GetSettlement(string settlementId)
    {
        try
        {
            if (!Hash.TryParse(settlementId, out var id))
                return BadRequest(new { error = "Invalid settlement ID format" });

            var settlement = await _stablecoinEngine.GetSettlementAsync(id);
            if (settlement == null)
                return NotFound(new { error = "Settlement not found" });

            return Ok(new
            {
                settlementId = settlement.Id.ToString(),
                from = settlement.From.ToString(),
                to = settlement.To.ToString(),
                amount = settlement.Amount.ToString(),
                fees = settlement.Fees.ToString(),
                status = settlement.Status.ToString(),
                timestamp = settlement.Timestamp,
                reference = settlement.Reference,
                transactionHash = settlement.TransactionHash?.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting settlement {SettlementId}", settlementId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("supply")]
    public async Task<IActionResult> GetSupplyInfo()
    {
        try
        {
            var supply = await _stablecoinEngine.GetSupplyInfoAsync();
            
            return Ok(new
            {
                totalSupply = supply.TotalSupply.ToString(),
                circulatingSupply = supply.CirculatingSupply.ToString(),
                totalCollateralValue = supply.TotalCollateralValue.ToString(),
                averageCollateralRatio = supply.AverageCollateralRatio,
                maxSupply = supply.MaxSupply.ToString(),
                utilizationRate = supply.UtilizationRate
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supply info");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public class MintRequest
{
    public required string To { get; set; }
    public required string Amount { get; set; }
    public required List<CollateralAssetRequest> CollateralAssets { get; set; }
    public required bool KycVerified { get; set; }
    public Dictionary<string, object>? ComplianceData { get; set; }
}

public class CollateralAssetRequest
{
    public required string Asset { get; set; }
    public required string Amount { get; set; }
}

public class BurnRequest
{
    public required string From { get; set; }
    public required string Amount { get; set; }
    public bool ReleaseCollateral { get; set; } = true;
}

public class SettlementRequest
{
    public required string SettlementId { get; set; }
    public required string From { get; set; }
    public required string To { get; set; }
    public required string Amount { get; set; }
    public string? Reference { get; set; }
    public Dictionary<string, object>? ComplianceData { get; set; }
}
