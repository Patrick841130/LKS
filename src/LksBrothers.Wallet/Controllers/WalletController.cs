using Microsoft.AspNetCore.Mvc;
using LksBrothers.Wallet.Services;
using LksBrothers.Core.Primitives;
using System.Security.Claims;

namespace LksBrothers.Wallet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WalletController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly SimpleAuthService _authService;

    public WalletController(WalletService walletService, SimpleAuthService authService)
    {
        _walletService = walletService;
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginWithGoogleAsync(
            request.GoogleToken, 
            request.Email, 
            request.Name
        );

        if (result.Success)
        {
            return Ok(new { 
                token = result.Token, 
                user = result.User,
                wallet = result.User?.WalletAddress.ToString()
            });
        }

        return BadRequest(new { error = result.Error });
    }

    [HttpGet("info")]
    public async Task<IActionResult> GetWalletInfo()
    {
        var email = GetUserEmail();
        if (email == null) return Unauthorized();

        var user = _authService.GetUser(email);
        if (user == null) return Unauthorized();

        var walletInfo = await _walletService.GetWalletInfoAsync(user.WalletAddress);
        return Ok(walletInfo);
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendLKS([FromBody] SendRequest request)
    {
        var email = GetUserEmail();
        if (email == null) return Unauthorized();

        var user = _authService.GetUser(email);
        if (user == null) return Unauthorized();

        var toAddress = Address.Parse(request.To);
        var amount = UInt256.Parse(request.Amount);

        var result = await _walletService.SendLKSAsync(user.WalletAddress, toAddress, amount);
        
        if (result.Success)
        {
            return Ok(new { 
                success = true, 
                txHash = result.TransactionHash?.ToString(),
                message = result.Message 
            });
        }

        return BadRequest(new { error = result.Message });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions()
    {
        var email = GetUserEmail();
        if (email == null) return Unauthorized();

        var user = _authService.GetUser(email);
        if (user == null) return Unauthorized();

        var transactions = await _walletService.GetTransactionHistoryAsync(user.WalletAddress);
        return Ok(transactions);
    }

    [HttpGet("qr")]
    public IActionResult GetQRCode()
    {
        var email = GetUserEmail();
        if (email == null) return Unauthorized();

        var user = _authService.GetUser(email);
        if (user == null) return Unauthorized();

        var qrData = _walletService.GenerateQRCode(user.WalletAddress);
        return Ok(new { address = user.WalletAddress.ToString(), qrData });
    }

    private string? GetUserEmail()
    {
        return User.FindFirst("email")?.Value;
    }
}

public class LoginRequest
{
    public required string GoogleToken { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
}

public class SendRequest
{
    public required string To { get; set; }
    public required string Amount { get; set; }
}
