using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json;
using LksBrothers.IpPatent.Services;

namespace LksBrothers.IpPatent.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly IAuditTrailService _auditService;
        private readonly RateLimitConfiguration _config;

        public RateLimitingMiddleware(
            RequestDelegate next,
            IMemoryCache cache,
            ILogger<RateLimitingMiddleware> logger,
            IConfiguration configuration,
            IAuditTrailService auditService)
        {
            _next = next;
            _cache = cache;
            _logger = logger;
            _auditService = auditService;
            
            _config = new RateLimitConfiguration();
            configuration.GetSection("RateLimit").Bind(_config);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.Request.Path.Value?.ToLower();
            var method = context.Request.Method;
            var clientId = GetClientIdentifier(context);

            // Skip rate limiting for certain endpoints
            if (ShouldSkipRateLimit(endpoint))
            {
                await _next(context);
                return;
            }

            var rateLimitRule = GetRateLimitRule(endpoint, method);
            if (rateLimitRule == null)
            {
                await _next(context);
                return;
            }

            var key = $"rate_limit:{clientId}:{rateLimitRule.Key}";
            var requestCount = await GetRequestCountAsync(key);

            if (requestCount >= rateLimitRule.MaxRequests)
            {
                await HandleRateLimitExceeded(context, clientId, rateLimitRule, requestCount);
                return;
            }

            // Increment request count
            await IncrementRequestCountAsync(key, rateLimitRule.WindowMinutes);

            // Add rate limit headers
            AddRateLimitHeaders(context, rateLimitRule, requestCount);

            await _next(context);
        }

        private string GetClientIdentifier(HttpContext context)
        {
            // Try to get user ID first
            var userId = context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }

            // Try API key
            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKey))
            {
                return $"api:{apiKey.Substring(0, Math.Min(8, apiKey.Length))}";
            }

            // Fall back to IP address
            var ipAddress = GetClientIpAddress(context);
            return $"ip:{ipAddress}";
        }

        private string GetClientIpAddress(HttpContext context)
        {
            var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                return xForwardedFor.Split(',')[0].Trim();
            }

            var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp))
            {
                return xRealIp;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private bool ShouldSkipRateLimit(string endpoint)
        {
            var skipEndpoints = new[]
            {
                "/health",
                "/metrics",
                "/swagger",
                "/api/ippatent/status"
            };

            return skipEndpoints.Any(skip => endpoint?.StartsWith(skip) == true);
        }

        private RateLimitRule GetRateLimitRule(string endpoint, string method)
        {
            // Define rate limit rules for different endpoints
            var rules = new Dictionary<string, RateLimitRule>
            {
                // Authentication endpoints - stricter limits
                { "POST:/api/auth/login", new RateLimitRule("auth_login", 5, 15, "Authentication") },
                { "POST:/api/auth/register", new RateLimitRule("auth_register", 3, 60, "Registration") },
                { "POST:/api/auth/reset-password", new RateLimitRule("auth_reset", 3, 60, "Password Reset") },

                // IP PATENT submission endpoints
                { "POST:/api/ippatent/submit", new RateLimitRule("ip_submit", 10, 60, "IP Submission") },
                { "GET:/api/ippatent/search", new RateLimitRule("ip_search", 100, 60, "IP Search") },
                { "GET:/api/ippatent/portfolio", new RateLimitRule("ip_portfolio", 50, 60, "Portfolio Access") },

                // Review endpoints - admin only, higher limits
                { "POST:/api/ippatent/review/approve", new RateLimitRule("review_approve", 50, 60, "Review Approval") },
                { "POST:/api/ippatent/review/reject", new RateLimitRule("review_reject", 50, 60, "Review Rejection") },

                // Payment endpoints - moderate limits
                { "POST:/api/payment/process", new RateLimitRule("payment_process", 20, 60, "Payment Processing") },
                { "POST:/api/payment/subscription", new RateLimitRule("payment_subscription", 5, 60, "Subscription Management") },

                // Backup endpoints - very strict limits
                { "POST:/api/ippatent/backup/full", new RateLimitRule("backup_full", 1, 1440, "Full Backup") },
                { "POST:/api/ippatent/backup/incremental", new RateLimitRule("backup_incremental", 5, 60, "Incremental Backup") },
                { "POST:/api/ippatent/backup/restore", new RateLimitRule("backup_restore", 1, 1440, "System Restore") },

                // General API endpoints
                { "GET:/api/ippatent", new RateLimitRule("api_general", 200, 60, "General API") },
                { "POST:/api/ippatent", new RateLimitRule("api_post", 50, 60, "API POST") },
                { "PUT:/api/ippatent", new RateLimitRule("api_put", 50, 60, "API PUT") },
                { "DELETE:/api/ippatent", new RateLimitRule("api_delete", 20, 60, "API DELETE") }
            };

            var key = $"{method}:{endpoint}";
            
            // Try exact match first
            if (rules.TryGetValue(key, out var exactRule))
            {
                return exactRule;
            }

            // Try pattern matching
            foreach (var rule in rules)
            {
                if (endpoint?.StartsWith(rule.Key.Split(':')[1]) == true && 
                    method == rule.Key.Split(':')[0])
                {
                    return rule.Value;
                }
            }

            // Default rule for unmatched endpoints
            return new RateLimitRule("default", _config.DefaultMaxRequests, _config.DefaultWindowMinutes, "Default");
        }

        private async Task<int> GetRequestCountAsync(string key)
        {
            return _cache.Get<int>(key);
        }

        private async Task IncrementRequestCountAsync(string key, int windowMinutes)
        {
            var currentCount = _cache.Get<int>(key);
            var newCount = currentCount + 1;
            
            var expiry = TimeSpan.FromMinutes(windowMinutes);
            _cache.Set(key, newCount, expiry);
        }

        private async Task HandleRateLimitExceeded(HttpContext context, string clientId, RateLimitRule rule, int requestCount)
        {
            _logger.LogWarning($"Rate limit exceeded for {clientId}: {rule.Description} - {requestCount}/{rule.MaxRequests} requests in {rule.WindowMinutes} minutes");

            // Log security event
            await _auditService.LogEventAsync(new AuditEvent
            {
                EventType = "RATE_LIMIT_EXCEEDED",
                EntityType = "Security",
                EntityId = rule.Key,
                UserId = clientId,
                Description = $"Rate limit exceeded: {rule.Description}",
                IpAddress = GetClientIpAddress(context),
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                Details = new 
                { 
                    Rule = rule.Key,
                    MaxRequests = rule.MaxRequests,
                    WindowMinutes = rule.WindowMinutes,
                    ActualRequests = requestCount,
                    Endpoint = context.Request.Path.Value,
                    Method = context.Request.Method
                }
            });

            // Check if this client should be temporarily blocked
            await CheckForTemporaryBlock(clientId, rule);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "Rate limit exceeded",
                message = $"Too many requests for {rule.Description}. Maximum {rule.MaxRequests} requests per {rule.WindowMinutes} minutes.",
                retryAfter = rule.WindowMinutes * 60,
                limit = rule.MaxRequests,
                window = rule.WindowMinutes,
                current = requestCount
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }

        private async Task CheckForTemporaryBlock(string clientId, RateLimitRule rule)
        {
            var violationKey = $"violations:{clientId}";
            var violations = _cache.Get<int>(violationKey);
            violations++;

            // Block client temporarily after multiple violations
            if (violations >= _config.MaxViolationsBeforeBlock)
            {
                var blockKey = $"blocked:{clientId}";
                _cache.Set(blockKey, true, TimeSpan.FromMinutes(_config.BlockDurationMinutes));
                
                _logger.LogWarning($"Client {clientId} temporarily blocked for {_config.BlockDurationMinutes} minutes due to {violations} rate limit violations");
                
                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = "CLIENT_TEMPORARILY_BLOCKED",
                    EntityType = "Security",
                    EntityId = clientId,
                    UserId = clientId,
                    Description = $"Client temporarily blocked due to {violations} rate limit violations",
                    Details = new { Violations = violations, BlockDurationMinutes = _config.BlockDurationMinutes }
                });
            }

            _cache.Set(violationKey, violations, TimeSpan.FromHours(1));
        }

        private void AddRateLimitHeaders(HttpContext context, RateLimitRule rule, int currentCount)
        {
            context.Response.Headers.Add("X-RateLimit-Limit", rule.MaxRequests.ToString());
            context.Response.Headers.Add("X-RateLimit-Remaining", Math.Max(0, rule.MaxRequests - currentCount - 1).ToString());
            context.Response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddMinutes(rule.WindowMinutes).ToUnixTimeSeconds().ToString());
            context.Response.Headers.Add("X-RateLimit-Window", (rule.WindowMinutes * 60).ToString());
        }
    }

    public class RateLimitRule
    {
        public string Key { get; set; }
        public int MaxRequests { get; set; }
        public int WindowMinutes { get; set; }
        public string Description { get; set; }

        public RateLimitRule(string key, int maxRequests, int windowMinutes, string description)
        {
            Key = key;
            MaxRequests = maxRequests;
            WindowMinutes = windowMinutes;
            Description = description;
        }
    }

    public class RateLimitConfiguration
    {
        public int DefaultMaxRequests { get; set; } = 100;
        public int DefaultWindowMinutes { get; set; } = 60;
        public int MaxViolationsBeforeBlock { get; set; } = 5;
        public int BlockDurationMinutes { get; set; } = 60;
        public bool EnableRateLimit { get; set; } = true;
        public List<string> WhitelistedIPs { get; set; } = new List<string>();
        public List<string> BlacklistedIPs { get; set; } = new List<string>();
    }
}
