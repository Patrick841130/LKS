using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using LksBrothers.Rpc.Services;
using System.Text.Json;

namespace LksBrothers.Rpc.Controllers;

[ApiController]
[Route("")]
[EnableRateLimiting("JsonRpcPolicy")]
public class JsonRpcController : ControllerBase
{
    private readonly ILogger<JsonRpcController> _logger;
    private readonly JsonRpcService _jsonRpcService;

    public JsonRpcController(
        ILogger<JsonRpcController> logger,
        JsonRpcService jsonRpcService)
    {
        _logger = logger;
        _jsonRpcService = jsonRpcService;
    }

    [HttpPost]
    public async Task<IActionResult> HandleJsonRpc([FromBody] JsonElement body)
    {
        try
        {
            // Handle both single requests and batch requests
            if (body.ValueKind == JsonValueKind.Array)
            {
                return await HandleBatchRequest(body);
            }
            else
            {
                return await HandleSingleRequest(body);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in RPC request");
            return BadRequest(new JsonRpcResponse
            {
                Id = null,
                Error = new JsonRpcError
                {
                    Code = -32700,
                    Message = "Parse error",
                    Data = "Invalid JSON was received by the server"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling JSON-RPC request");
            return StatusCode(500, new JsonRpcResponse
            {
                Id = null,
                Error = new JsonRpcError
                {
                    Code = -32603,
                    Message = "Internal error",
                    Data = "An internal error occurred"
                }
            });
        }
    }

    private async Task<IActionResult> HandleSingleRequest(JsonElement body)
    {
        try
        {
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(body.GetRawText());
            if (request == null)
            {
                return BadRequest(new JsonRpcResponse
                {
                    Id = null,
                    Error = new JsonRpcError
                    {
                        Code = -32600,
                        Message = "Invalid Request",
                        Data = "The JSON sent is not a valid Request object"
                    }
                });
            }

            // Validate JSON-RPC version
            if (request.JsonRpc != "2.0")
            {
                return BadRequest(new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32600,
                        Message = "Invalid Request",
                        Data = "JSON-RPC version must be '2.0'"
                    }
                });
            }

            var response = await _jsonRpcService.ProcessRequestAsync(request);
            return Ok(response);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize JSON-RPC request");
            return BadRequest(new JsonRpcResponse
            {
                Id = null,
                Error = new JsonRpcError
                {
                    Code = -32600,
                    Message = "Invalid Request",
                    Data = "The JSON sent is not a valid Request object"
                }
            });
        }
    }

    private async Task<IActionResult> HandleBatchRequest(JsonElement body)
    {
        try
        {
            var requests = JsonSerializer.Deserialize<JsonRpcRequest[]>(body.GetRawText());
            if (requests == null || requests.Length == 0)
            {
                return BadRequest(new JsonRpcResponse
                {
                    Id = null,
                    Error = new JsonRpcError
                    {
                        Code = -32600,
                        Message = "Invalid Request",
                        Data = "Batch request cannot be empty"
                    }
                });
            }

            // Process all requests in parallel
            var tasks = requests.Select(request => _jsonRpcService.ProcessRequestAsync(request));
            var responses = await Task.WhenAll(tasks);

            // Filter out notification responses (requests with null id)
            var filteredResponses = responses.Where(r => r.Id != null).ToArray();

            return Ok(filteredResponses);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize batch JSON-RPC request");
            return BadRequest(new JsonRpcResponse
            {
                Id = null,
                Error = new JsonRpcError
                {
                    Code = -32600,
                    Message = "Invalid Request",
                    Data = "The JSON sent is not a valid batch Request"
                }
            });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow });
    }

    [HttpOptions]
    public IActionResult Options()
    {
        Response.Headers.Add("Access-Control-Allow-Origin", "*");
        Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
        Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
        return Ok();
    }
}
