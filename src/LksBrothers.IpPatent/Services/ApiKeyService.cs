using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LksBrothers.IpPatent.Services
{
    public interface IApiKeyService
    {
        Task<ApiKeyInfo> GenerateApiKeyAsync(string userId, string description, ApiKeyScope scope = ApiKeyScope.Standard);
        Task<bool> ValidateApiKeyAsync(string apiKey);
        Task<ApiKeyInfo> GetApiKeyInfoAsync(string apiKey);
        Task<bool> RevokeApiKeyAsync(string apiKey, string reason);
        Task<List<ApiKeyInfo>> GetUserApiKeysAsync(string userId);
        Task<bool> UpdateApiKeyAsync(string apiKey, string description, ApiKeyScope scope);
        Task<ApiKeyUsageStats> GetApiKeyUsageAsync(string apiKey, DateTime? fromDate = null, DateTime? toDate = null);
        Task<bool> RotateApiKeyAsync(string oldApiKey);
        Task CleanupExpiredApiKeysAsync();
    }

    public class ApiKeyService : IApiKeyService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiKeyService> _logger;
        private readonly IAuditTrailService _auditService;
        private readonly Dictionary<string, ApiKeyInfo> _apiKeys; // In production, use database
        private readonly Dictionary<string, List<ApiKeyUsage>> _usageStats; // In production, use database

        public ApiKeyService(
            IConfiguration configuration,
            ILogger<ApiKeyService> logger,
            IAuditTrailService auditService)
        {
            _configuration = configuration;
            _logger = logger;
            _auditService = auditService;
            _apiKeys = new Dictionary<string, ApiKeyInfo>();
            _usageStats = new Dictionary<string, List<ApiKeyUsage>>();
        }

        public async Task<ApiKeyInfo> GenerateApiKeyAsync(string userId, string description, ApiKeyScope scope = ApiKeyScope.Standard)
        {
            try
            {
                var apiKey = GenerateSecureApiKey();
                var hashedKey = HashApiKey(apiKey);

                var keyInfo = new ApiKeyInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    HashedKey = hashedKey,
                    UserId = userId,
                    Description = description,
                    Scope = scope,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddYears(1), // Default 1 year expiration
                    IsActive = true,
                    LastUsedAt = null,
                    UsageCount = 0,
                    RateLimitPerHour = GetRateLimitForScope(scope)
                };

                _apiKeys[hashedKey] = keyInfo;
                _usageStats[hashedKey] = new List<ApiKeyUsage>();

                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = "API_KEY_GENERATED",
                    EntityType = "ApiKey",
                    EntityId = keyInfo.Id,
                    UserId = userId,
                    Description = $"API key generated: {description}",
                    Details = new { Scope = scope.ToString(), ExpiresAt = keyInfo.ExpiresAt }
                });

                _logger.LogInformation($"API key generated for user {userId}: {keyInfo.Id}");

                // Return the plain API key only once
                keyInfo.PlainKey = apiKey;
                return keyInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to generate API key for user {userId}");
                throw;
            }
        }

        public async Task<bool> ValidateApiKeyAsync(string apiKey)
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey))
                    return false;

                var hashedKey = HashApiKey(apiKey);
                
                if (!_apiKeys.TryGetValue(hashedKey, out var keyInfo))
                    return false;

                // Check if key is active
                if (!keyInfo.IsActive)
                {
                    await LogApiKeyUsage(hashedKey, "VALIDATION_FAILED", "Key is inactive");
                    return false;
                }

                // Check if key is expired
                if (keyInfo.ExpiresAt <= DateTime.UtcNow)
                {
                    await LogApiKeyUsage(hashedKey, "VALIDATION_FAILED", "Key is expired");
                    return false;
                }

                // Check rate limits
                if (await IsRateLimitExceeded(hashedKey))
                {
                    await LogApiKeyUsage(hashedKey, "RATE_LIMIT_EXCEEDED", "Hourly rate limit exceeded");
                    return false;
                }

                // Update usage statistics
                keyInfo.LastUsedAt = DateTime.UtcNow;
                keyInfo.UsageCount++;

                await LogApiKeyUsage(hashedKey, "VALIDATION_SUCCESS", "Key validated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating API key");
                return false;
            }
        }

        public async Task<ApiKeyInfo> GetApiKeyInfoAsync(string apiKey)
        {
            try
            {
                var hashedKey = HashApiKey(apiKey);
                
                if (_apiKeys.TryGetValue(hashedKey, out var keyInfo))
                {
                    // Don't return the plain key for security
                    var safeKeyInfo = new ApiKeyInfo
                    {
                        Id = keyInfo.Id,
                        UserId = keyInfo.UserId,
                        Description = keyInfo.Description,
                        Scope = keyInfo.Scope,
                        CreatedAt = keyInfo.CreatedAt,
                        ExpiresAt = keyInfo.ExpiresAt,
                        IsActive = keyInfo.IsActive,
                        LastUsedAt = keyInfo.LastUsedAt,
                        UsageCount = keyInfo.UsageCount,
                        RateLimitPerHour = keyInfo.RateLimitPerHour
                    };
                    
                    return safeKeyInfo;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting API key info");
                return null;
            }
        }

        public async Task<bool> RevokeApiKeyAsync(string apiKey, string reason)
        {
            try
            {
                var hashedKey = HashApiKey(apiKey);
                
                if (_apiKeys.TryGetValue(hashedKey, out var keyInfo))
                {
                    keyInfo.IsActive = false;
                    keyInfo.RevokedAt = DateTime.UtcNow;
                    keyInfo.RevocationReason = reason;

                    await _auditService.LogEventAsync(new AuditEvent
                    {
                        EventType = "API_KEY_REVOKED",
                        EntityType = "ApiKey",
                        EntityId = keyInfo.Id,
                        UserId = keyInfo.UserId,
                        Description = $"API key revoked: {reason}",
                        Details = new { Reason = reason, RevokedAt = keyInfo.RevokedAt }
                    });

                    _logger.LogInformation($"API key revoked: {keyInfo.Id}, Reason: {reason}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error revoking API key: {reason}");
                return false;
            }
        }

        public async Task<List<ApiKeyInfo>> GetUserApiKeysAsync(string userId)
        {
            try
            {
                var userKeys = _apiKeys.Values
                    .Where(k => k.UserId == userId)
                    .Select(k => new ApiKeyInfo
                    {
                        Id = k.Id,
                        UserId = k.UserId,
                        Description = k.Description,
                        Scope = k.Scope,
                        CreatedAt = k.CreatedAt,
                        ExpiresAt = k.ExpiresAt,
                        IsActive = k.IsActive,
                        LastUsedAt = k.LastUsedAt,
                        UsageCount = k.UsageCount,
                        RateLimitPerHour = k.RateLimitPerHour,
                        RevokedAt = k.RevokedAt,
                        RevocationReason = k.RevocationReason
                    })
                    .OrderByDescending(k => k.CreatedAt)
                    .ToList();

                return userKeys;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting API keys for user {userId}");
                return new List<ApiKeyInfo>();
            }
        }

        public async Task<bool> UpdateApiKeyAsync(string apiKey, string description, ApiKeyScope scope)
        {
            try
            {
                var hashedKey = HashApiKey(apiKey);
                
                if (_apiKeys.TryGetValue(hashedKey, out var keyInfo))
                {
                    var oldDescription = keyInfo.Description;
                    var oldScope = keyInfo.Scope;

                    keyInfo.Description = description;
                    keyInfo.Scope = scope;
                    keyInfo.RateLimitPerHour = GetRateLimitForScope(scope);
                    keyInfo.UpdatedAt = DateTime.UtcNow;

                    await _auditService.LogEventAsync(new AuditEvent
                    {
                        EventType = "API_KEY_UPDATED",
                        EntityType = "ApiKey",
                        EntityId = keyInfo.Id,
                        UserId = keyInfo.UserId,
                        Description = "API key updated",
                        Details = new 
                        { 
                            OldDescription = oldDescription,
                            NewDescription = description,
                            OldScope = oldScope.ToString(),
                            NewScope = scope.ToString()
                        }
                    });

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating API key");
                return false;
            }
        }

        public async Task<ApiKeyUsageStats> GetApiKeyUsageAsync(string apiKey, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var hashedKey = HashApiKey(apiKey);
                
                if (!_usageStats.TryGetValue(hashedKey, out var usageList))
                    return new ApiKeyUsageStats();

                var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
                var to = toDate ?? DateTime.UtcNow;

                var filteredUsage = usageList
                    .Where(u => u.Timestamp >= from && u.Timestamp <= to)
                    .ToList();

                var stats = new ApiKeyUsageStats
                {
                    TotalRequests = filteredUsage.Count,
                    SuccessfulRequests = filteredUsage.Count(u => u.EventType == "VALIDATION_SUCCESS"),
                    FailedRequests = filteredUsage.Count(u => u.EventType != "VALIDATION_SUCCESS"),
                    RateLimitExceeded = filteredUsage.Count(u => u.EventType == "RATE_LIMIT_EXCEEDED"),
                    FirstUsage = filteredUsage.MinBy(u => u.Timestamp)?.Timestamp,
                    LastUsage = filteredUsage.MaxBy(u => u.Timestamp)?.Timestamp,
                    DailyUsage = filteredUsage
                        .GroupBy(u => u.Timestamp.Date)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    HourlyUsage = filteredUsage
                        .GroupBy(u => u.Timestamp.Hour)
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting API key usage stats");
                return new ApiKeyUsageStats();
            }
        }

        public async Task<bool> RotateApiKeyAsync(string oldApiKey)
        {
            try
            {
                var oldHashedKey = HashApiKey(oldApiKey);
                
                if (!_apiKeys.TryGetValue(oldHashedKey, out var oldKeyInfo))
                    return false;

                // Generate new API key
                var newApiKey = GenerateSecureApiKey();
                var newHashedKey = HashApiKey(newApiKey);

                // Create new key info with same properties
                var newKeyInfo = new ApiKeyInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    HashedKey = newHashedKey,
                    UserId = oldKeyInfo.UserId,
                    Description = oldKeyInfo.Description + " (Rotated)",
                    Scope = oldKeyInfo.Scope,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddYears(1),
                    IsActive = true,
                    LastUsedAt = null,
                    UsageCount = 0,
                    RateLimitPerHour = oldKeyInfo.RateLimitPerHour,
                    PlainKey = newApiKey // Return new key once
                };

                // Add new key
                _apiKeys[newHashedKey] = newKeyInfo;
                _usageStats[newHashedKey] = new List<ApiKeyUsage>();

                // Revoke old key
                await RevokeApiKeyAsync(oldApiKey, "Key rotated");

                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = "API_KEY_ROTATED",
                    EntityType = "ApiKey",
                    EntityId = oldKeyInfo.Id,
                    UserId = oldKeyInfo.UserId,
                    Description = "API key rotated",
                    Details = new { OldKeyId = oldKeyInfo.Id, NewKeyId = newKeyInfo.Id }
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating API key");
                return false;
            }
        }

        public async Task CleanupExpiredApiKeysAsync()
        {
            try
            {
                var expiredKeys = _apiKeys.Values
                    .Where(k => k.ExpiresAt <= DateTime.UtcNow && k.IsActive)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    key.IsActive = false;
                    key.RevokedAt = DateTime.UtcNow;
                    key.RevocationReason = "Expired";
                }

                if (expiredKeys.Any())
                {
                    await _auditService.LogEventAsync(new AuditEvent
                    {
                        EventType = "API_KEYS_EXPIRED",
                        EntityType = "System",
                        EntityId = "CLEANUP",
                        UserId = "SYSTEM",
                        Description = $"Cleaned up {expiredKeys.Count} expired API keys",
                        Details = new { ExpiredCount = expiredKeys.Count }
                    });

                    _logger.LogInformation($"Cleaned up {expiredKeys.Count} expired API keys");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired API keys");
            }
        }

        private string GenerateSecureApiKey()
        {
            const string prefix = "lks_";
            const int keyLength = 32;
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[keyLength];
                rng.GetBytes(bytes);

                var result = new StringBuilder(prefix);
                for (int i = 0; i < keyLength; i++)
                {
                    result.Append(chars[bytes[i] % chars.Length]);
                }

                return result.ToString();
            }
        }

        private string HashApiKey(string apiKey)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private int GetRateLimitForScope(ApiKeyScope scope)
        {
            return scope switch
            {
                ApiKeyScope.ReadOnly => 1000,
                ApiKeyScope.Standard => 500,
                ApiKeyScope.Premium => 2000,
                ApiKeyScope.Admin => 5000,
                _ => 100
            };
        }

        private async Task<bool> IsRateLimitExceeded(string hashedKey)
        {
            if (!_apiKeys.TryGetValue(hashedKey, out var keyInfo))
                return true;

            if (!_usageStats.TryGetValue(hashedKey, out var usageList))
                return false;

            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            var recentUsage = usageList.Count(u => u.Timestamp >= oneHourAgo);

            return recentUsage >= keyInfo.RateLimitPerHour;
        }

        private async Task LogApiKeyUsage(string hashedKey, string eventType, string description)
        {
            if (_usageStats.TryGetValue(hashedKey, out var usageList))
            {
                usageList.Add(new ApiKeyUsage
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = eventType,
                    Description = description
                });

                // Keep only last 10,000 usage records per key
                if (usageList.Count > 10000)
                {
                    usageList.RemoveRange(0, usageList.Count - 10000);
                }
            }
        }
    }

    public class ApiKeyInfo
    {
        public string Id { get; set; }
        public string HashedKey { get; set; }
        public string PlainKey { get; set; } // Only set when generating/rotating
        public string UserId { get; set; }
        public string Description { get; set; }
        public ApiKeyScope Scope { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public long UsageCount { get; set; }
        public int RateLimitPerHour { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string RevocationReason { get; set; }
    }

    public class ApiKeyUsage
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; }
        public string Description { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Endpoint { get; set; }
    }

    public class ApiKeyUsageStats
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public int RateLimitExceeded { get; set; }
        public DateTime? FirstUsage { get; set; }
        public DateTime? LastUsage { get; set; }
        public Dictionary<DateTime, int> DailyUsage { get; set; } = new Dictionary<DateTime, int>();
        public Dictionary<int, int> HourlyUsage { get; set; } = new Dictionary<int, int>();
    }

    public enum ApiKeyScope
    {
        ReadOnly,
        Standard,
        Premium,
        Admin
    }
}
