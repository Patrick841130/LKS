using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using System.Text.Json;

namespace LksBrothers.Rpc.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("DefaultPolicy")]
public class BlockchainController : ControllerBase
{
    private readonly ILogger<BlockchainController> _logger;
    private readonly IBlockchainService _blockchainService;

    public BlockchainController(
        ILogger<BlockchainController> logger,
        IBlockchainService blockchainService)
    {
        _logger = logger;
        _blockchainService = blockchainService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var status = await _blockchainService.GetStatusAsync();
            return Ok(new
            {
                chainId = status.ChainId,
                blockHeight = status.BlockHeight,
                blockHash = status.LatestBlockHash.ToString(),
                peers = status.PeerCount,
                syncing = status.IsSyncing,
                version = status.Version
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blockchain status");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("block/{blockNumber}")]
    public async Task<IActionResult> GetBlock(ulong blockNumber)
    {
        try
        {
            var block = await _blockchainService.GetBlockAsync(blockNumber);
            if (block == null)
                return NotFound(new { error = "Block not found" });

            return Ok(new
            {
                number = block.Header.Number,
                hash = block.Hash.ToString(),
                parentHash = block.Header.ParentHash.ToString(),
                timestamp = block.Header.Timestamp,
                gasUsed = block.Header.GasUsed,
                gasLimit = block.Header.GasLimit,
                transactionCount = block.Transactions.Count,
                transactions = block.Transactions.Select(tx => tx.Hash.ToString()).ToArray(),
                stablecoinSettlements = block.StablecoinSettlements?.Count ?? 0,
                complianceEvents = block.ComplianceEvents?.Count ?? 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting block {BlockNumber}", blockNumber);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("block/latest")]
    public async Task<IActionResult> GetLatestBlock()
    {
        try
        {
            var block = await _blockchainService.GetLatestBlockAsync();
            if (block == null)
                return NotFound(new { error = "No blocks found" });

            return Ok(new
            {
                number = block.Header.Number,
                hash = block.Hash.ToString(),
                parentHash = block.Header.ParentHash.ToString(),
                timestamp = block.Header.Timestamp,
                gasUsed = block.Header.GasUsed,
                gasLimit = block.Header.GasLimit,
                transactionCount = block.Transactions.Count,
                transactions = block.Transactions.Select(tx => tx.Hash.ToString()).ToArray()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest block");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("transaction/{txHash}")]
    public async Task<IActionResult> GetTransaction(string txHash)
    {
        try
        {
            if (!Hash.TryParse(txHash, out var hash))
                return BadRequest(new { error = "Invalid transaction hash format" });

            var transaction = await _blockchainService.GetTransactionAsync(hash);
            if (transaction == null)
                return NotFound(new { error = "Transaction not found" });

            return Ok(new
            {
                hash = transaction.Hash.ToString(),
                from = transaction.From.ToString(),
                to = transaction.To?.ToString(),
                value = transaction.Value.ToString(),
                gasLimit = transaction.GasLimit,
                gasPrice = transaction.GasPrice.ToString(),
                nonce = transaction.Nonce,
                data = Convert.ToHexString(transaction.Data),
                stablecoinFee = transaction.StablecoinFee?.ToString(),
                settlementId = transaction.SettlementId?.ToString(),
                complianceFlags = transaction.ComplianceFlags
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction {TxHash}", txHash);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("transaction")]
    public async Task<IActionResult> SendTransaction([FromBody] SendTransactionRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var transaction = new Transaction
            {
                From = Address.Parse(request.From),
                To = request.To != null ? Address.Parse(request.To) : null,
                Value = UInt256.Parse(request.Value),
                GasLimit = request.GasLimit,
                GasPrice = UInt256.Parse(request.GasPrice),
                Data = Convert.FromHexString(request.Data ?? ""),
                StablecoinFee = request.StablecoinFee != null ? UInt256.Parse(request.StablecoinFee) : null
            };

            var txHash = await _blockchainService.SubmitTransactionAsync(transaction);
            
            return Ok(new { transactionHash = txHash.ToString() });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transaction");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("balance/{address}")]
    public async Task<IActionResult> GetBalance(string address)
    {
        try
        {
            if (!Address.TryParse(address, out var addr))
                return BadRequest(new { error = "Invalid address format" });

            var balance = await _blockchainService.GetBalanceAsync(addr);
            
            return Ok(new { 
                address = addr.ToString(),
                balance = balance.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting balance for {Address}", address);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("nonce/{address}")]
    public async Task<IActionResult> GetNonce(string address)
    {
        try
        {
            if (!Address.TryParse(address, out var addr))
                return BadRequest(new { error = "Invalid address format" });

            var nonce = await _blockchainService.GetNonceAsync(addr);
            
            return Ok(new { 
                address = addr.ToString(),
                nonce = nonce
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting nonce for {Address}", address);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public class SendTransactionRequest
{
    public required string From { get; set; }
    public string? To { get; set; }
    public required string Value { get; set; }
    public required ulong GasLimit { get; set; }
    public required string GasPrice { get; set; }
    public string? Data { get; set; }
    public string? StablecoinFee { get; set; }
}

public interface IBlockchainService
{
    Task<BlockchainStatus> GetStatusAsync();
    Task<Block?> GetBlockAsync(ulong blockNumber);
    Task<Block?> GetLatestBlockAsync();
    Task<Transaction?> GetTransactionAsync(Hash txHash);
    Task<Hash> SubmitTransactionAsync(Transaction transaction);
    Task<UInt256> GetBalanceAsync(Address address);
    Task<ulong> GetNonceAsync(Address address);
}

public class BlockchainStatus
{
    public required string ChainId { get; set; }
    public required ulong BlockHeight { get; set; }
    public required Hash LatestBlockHash { get; set; }
    public required int PeerCount { get; set; }
    public required bool IsSyncing { get; set; }
    public required string Version { get; set; }
}
