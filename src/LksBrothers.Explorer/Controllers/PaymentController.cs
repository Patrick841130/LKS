using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace LksBrothers.Explorer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly ILogger<PaymentController> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public PaymentController(ILogger<PaymentController> logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
    }

    [HttpPost("send-xrp")]
    [Authorize]
    public async Task<IActionResult> SendXrpPayment([FromBody] XrpPaymentRequest request)
    {
        try
        {
            var nodeBackendUrl = _configuration["NodeBackend:BaseUrl"] ?? "http://localhost:3000";
            
            var payload = new
            {
                sourceAddress = request.SourceAddress,
                destAddress = request.DestinationAddress,
                secret = request.Secret
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{nodeBackendUrl}/send-payment", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("XRP payment sent successfully: {Response}", responseContent);
                return Ok(JsonSerializer.Deserialize<object>(responseContent));
            }
            else
            {
                _logger.LogError("XRP payment failed: {Response}", responseContent);
                return BadRequest(new { error = "Payment failed", details = responseContent });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing XRP payment");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpGet("balance/{address}")]
    public async Task<IActionResult> GetXrpBalance(string address)
    {
        try
        {
            var nodeBackendUrl = _configuration["NodeBackend:BaseUrl"] ?? "http://localhost:3000";
            var response = await _httpClient.GetAsync($"{nodeBackendUrl}/balance/{address}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return Ok(JsonSerializer.Deserialize<object>(responseContent));
            }
            else
            {
                return BadRequest(new { error = "Failed to get balance", details = responseContent });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting XRP balance");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }
}

public class XrpPaymentRequest
{
    public string SourceAddress { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public decimal Amount { get; set; } = 10;
}
