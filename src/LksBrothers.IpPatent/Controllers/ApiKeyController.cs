using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using LksBrothers.IpPatent.Services;

namespace LksBrothers.IpPatent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ApiKeyController : ControllerBase
    {
        private readonly IApiKeyService _apiKeyService;
        private readonly IAuditTrailService _auditService;
        private readonly ILogger<ApiKeyController> _logger;

        public ApiKeyController(
            IApiKeyService apiKeyService,
            IAuditTrailService auditService,
            ILogger<ApiKeyController> logger)
        {
            _apiKeyService = apiKeyService;
            _auditService = auditService;
            _logger = logger;
        }

        /// <summary>
        /// Generate a new API key for the authenticated user
        /// </summary>
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateApiKey([FromBody] GenerateApiKeyRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("User not authenticated");

                if (string.IsNullOrWhiteSpace(request.Description))
                    return BadRequest("Description is required");

                var apiKeyInfo = await _apiKeyService.GenerateApiKeyAsync(
                    userId, 
                    request.Description, 
                    request.Scope);

                var response = new GenerateApiKeyResponse
                {
                    ApiKey = apiKeyInfo.PlainKey, // Only returned once
                    KeyId = apiKeyInfo.Id,
                    Description = apiKeyInfo.Description,
                    Scope = apiKeyInfo.Scope.ToString(),
                    ExpiresAt = apiKeyInfo.ExpiresAt,
                    RateLimitPerHour = apiKeyInfo.RateLimitPerHour
                };

                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = "API_KEY_GENERATED_VIA_API",
                    EntityType = "ApiKey",
                    EntityId = apiKeyInfo.Id,
                    UserId = userId,
                    Description = $"API key generated via API: {request.Description}",
                    IpAddress = GetClientIpAddress(),
                    UserAgent = Request.Headers["User-Agent"].ToString()
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating API key");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get all API keys for the authenticated user
        /// </summary>
        [HttpGet("my-keys")]
        public async Task<IActionResult> GetMyApiKeys()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("User not authenticated");

                var apiKeys = await _apiKeyService.GetUserApiKeysAsync(userId);
                
                var response = apiKeys.Select(k => new ApiKeyResponse
                {
                    KeyId = k.Id,
                    Description = k.Description,
                    Scope = k.Scope.ToString(),
                    CreatedAt = k.CreatedAt,
                    ExpiresAt = k.ExpiresAt,
                    IsActive = k.IsActive,
                    LastUsedAt = k.LastUsedAt,
                    UsageCount = k.UsageCount,
                    RateLimitPerHour = k.RateLimitPerHour,
                    RevokedAt = k.RevokedAt,
                    RevocationReason = k.RevocationReason,
                    MaskedKey = MaskApiKey(k.HashedKey)
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user API keys");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Update an existing API key
        /// </summary>
        [HttpPut("{keyId}")]
        public async Task<IActionResult> UpdateApiKey(string keyId, [FromBody] UpdateApiKeyRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("User not authenticated");

                // Get user's API keys to verify ownership
                var userKeys = await _apiKeyService.GetUserApiKeysAsync(userId);
                var targetKey = userKeys.FirstOrDefault(k => k.Id == keyId);

                if (targetKey == null)
                    return NotFound("API key not found");

                // For update, we need the actual API key, but we don't store it
                // In a real implementation, you'd need to handle this differently
                // For now, return an error indicating the limitation
                return BadRequest("API key updates require the full key. Please revoke and create a new key instead.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating API key {keyId}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Revoke an API key
        /// </summary>
        [HttpDelete("{keyId}")]
        public async Task<IActionResult> RevokeApiKey(string keyId, [FromBody] RevokeApiKeyRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("User not authenticated");

                // Get user's API keys to verify ownership
                var userKeys = await _apiKeyService.GetUserApiKeysAsync(userId);
                var targetKey = userKeys.FirstOrDefault(k => k.Id == keyId);

                if (targetKey == null)
                    return NotFound("API key not found");

                // For revocation, we need the actual API key
                // In a real implementation, you'd store a reference or handle this differently
                return BadRequest("API key revocation requires the full key. Please contact support for key revocation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error revoking API key {keyId}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get usage statistics for an API key
        /// </summary>
        [HttpGet("{keyId}/usage")]
        public async Task<IActionResult> GetApiKeyUsage(string keyId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("User not authenticated");

                // Get user's API keys to verify ownership
                var userKeys = await _apiKeyService.GetUserApiKeysAsync(userId);
                var targetKey = userKeys.FirstOrDefault(k => k.Id == keyId);

                if (targetKey == null)
                    return NotFound("API key not found");

                // For usage stats, we need the actual API key
                // In a real implementation, you'd store usage by key ID
                return BadRequest("Usage statistics require the full key. Feature not available in current implementation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting API key usage for {keyId}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Rotate an API key (generate new key and revoke old one)
        /// </summary>
        [HttpPost("{keyId}/rotate")]
        public async Task<IActionResult> RotateApiKey(string keyId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("User not authenticated");

                // Get user's API keys to verify ownership
                var userKeys = await _apiKeyService.GetUserApiKeysAsync(userId);
                var targetKey = userKeys.FirstOrDefault(k => k.Id == keyId);

                if (targetKey == null)
                    return NotFound("API key not found");

                // For rotation, we need the actual API key
                // In a real implementation, you'd handle this differently
                return BadRequest("API key rotation requires the full key. Please revoke the old key and create a new one.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rotating API key {keyId}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Validate an API key (for testing purposes)
        /// </summary>
        [HttpPost("validate")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateApiKey([FromBody] ValidateApiKeyRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ApiKey))
                    return BadRequest("API key is required");

                var isValid = await _apiKeyService.ValidateApiKeyAsync(request.ApiKey);
                
                if (isValid)
                {
                    var keyInfo = await _apiKeyService.GetApiKeyInfoAsync(request.ApiKey);
                    
                    return Ok(new ValidateApiKeyResponse
                    {
                        IsValid = true,
                        KeyId = keyInfo?.Id,
                        Scope = keyInfo?.Scope.ToString(),
                        UserId = keyInfo?.UserId,
                        RateLimitPerHour = keyInfo?.RateLimitPerHour ?? 0
                    });
                }

                return Ok(new ValidateApiKeyResponse { IsValid = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating API key");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get API key scopes and their descriptions
        /// </summary>
        [HttpGet("scopes")]
        public IActionResult GetApiKeyScopes()
        {
            var scopes = new[]
            {
                new { 
                    Name = "ReadOnly", 
                    Description = "Read-only access to IP PATENT data",
                    RateLimit = 1000,
                    Permissions = new[] { "read:submissions", "read:status" }
                },
                new { 
                    Name = "Standard", 
                    Description = "Standard access for submission and review",
                    RateLimit = 500,
                    Permissions = new[] { "read:submissions", "write:submissions", "read:status" }
                },
                new { 
                    Name = "Premium", 
                    Description = "Premium access with advanced features",
                    RateLimit = 2000,
                    Permissions = new[] { "read:submissions", "write:submissions", "read:status", "write:reviews", "read:analytics" }
                },
                new { 
                    Name = "Admin", 
                    Description = "Administrative access to all features",
                    RateLimit = 5000,
                    Permissions = new[] { "read:*", "write:*", "admin:*" }
                }
            };

            return Ok(scopes);
        }

        private string GetCurrentUserId()
        {
            return User?.Identity?.Name ?? User?.FindFirst("sub")?.Value ?? User?.FindFirst("userId")?.Value;
        }

        private string GetClientIpAddress()
        {
            return Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? 
                   Request.Headers["X-Real-IP"].FirstOrDefault() ?? 
                   HttpContext.Connection.RemoteIpAddress?.ToString() ?? 
                   "Unknown";
        }

        private string MaskApiKey(string hashedKey)
        {
            if (string.IsNullOrEmpty(hashedKey) || hashedKey.Length < 8)
                return "lks_****";

            // Show first 4 and last 4 characters of the hash (not the actual key)
            return $"lks_****{hashedKey.Substring(hashedKey.Length - 4)}";
        }
    }

    // Request/Response DTOs
    public class GenerateApiKeyRequest
    {
        public string Description { get; set; }
        public ApiKeyScope Scope { get; set; } = ApiKeyScope.Standard;
    }

    public class GenerateApiKeyResponse
    {
        public string ApiKey { get; set; }
        public string KeyId { get; set; }
        public string Description { get; set; }
        public string Scope { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int RateLimitPerHour { get; set; }
    }

    public class UpdateApiKeyRequest
    {
        public string Description { get; set; }
        public ApiKeyScope Scope { get; set; }
    }

    public class RevokeApiKeyRequest
    {
        public string Reason { get; set; }
    }

    public class ValidateApiKeyRequest
    {
        public string ApiKey { get; set; }
    }

    public class ValidateApiKeyResponse
    {
        public bool IsValid { get; set; }
        public string KeyId { get; set; }
        public string Scope { get; set; }
        public string UserId { get; set; }
        public int RateLimitPerHour { get; set; }
    }

    public class ApiKeyResponse
    {
        public string KeyId { get; set; }
        public string Description { get; set; }
        public string Scope { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public long UsageCount { get; set; }
        public int RateLimitPerHour { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string RevocationReason { get; set; }
        public string MaskedKey { get; set; }
    }
}
