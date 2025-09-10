using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LksBrothers.IpPatent.Services;

namespace LksBrothers.IpPatent.Middleware
{
    public class SecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityMiddleware> _logger;
        private readonly IAuditTrailService _auditService;
        private readonly SecurityConfiguration _config;

        public SecurityMiddleware(
            RequestDelegate next,
            ILogger<SecurityMiddleware> logger,
            IConfiguration configuration,
            IAuditTrailService auditService)
        {
            _next = next;
            _logger = logger;
            _auditService = auditService;
            
            _config = new SecurityConfiguration();
            configuration.GetSection("Security").Bind(_config);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = GetClientIpAddress(context);
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var endpoint = context.Request.Path.Value;

            try
            {
                // 1. IP Whitelist/Blacklist Check
                if (!await ValidateClientIpAsync(context, clientIp))
                    return;

                // 2. Request Size Validation
                if (!await ValidateRequestSizeAsync(context))
                    return;

                // 3. Security Headers Validation
                if (!await ValidateSecurityHeadersAsync(context))
                    return;

                // 4. Suspicious Activity Detection
                if (!await ValidateSuspiciousActivityAsync(context, clientIp, userAgent))
                    return;

                // 5. API Key Validation (if present)
                if (!await ValidateApiKeyAsync(context))
                    return;

                // 6. CORS Validation
                if (!await ValidateCorsAsync(context))
                    return;

                // Add security headers to response
                AddSecurityHeaders(context);

                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Security middleware error for {clientIp} accessing {endpoint}");
                await HandleSecurityError(context, "Internal security error");
            }
        }

        private async Task<bool> ValidateClientIpAsync(HttpContext context, string clientIp)
        {
            // Check blacklist
            if (_config.BlacklistedIPs.Contains(clientIp))
            {
                await LogSecurityEvent(context, "IP_BLACKLISTED", $"Blocked request from blacklisted IP: {clientIp}");
                await HandleSecurityViolation(context, "Access denied", HttpStatusCode.Forbidden);
                return false;
            }

            // Check whitelist (if enabled)
            if (_config.EnableIpWhitelist && _config.WhitelistedIPs.Any() && !_config.WhitelistedIPs.Contains(clientIp))
            {
                await LogSecurityEvent(context, "IP_NOT_WHITELISTED", $"Blocked request from non-whitelisted IP: {clientIp}");
                await HandleSecurityViolation(context, "Access denied", HttpStatusCode.Forbidden);
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateRequestSizeAsync(HttpContext context)
        {
            if (context.Request.ContentLength.HasValue && 
                context.Request.ContentLength.Value > _config.MaxRequestSizeBytes)
            {
                await LogSecurityEvent(context, "REQUEST_TOO_LARGE", 
                    $"Request size {context.Request.ContentLength.Value} exceeds limit {_config.MaxRequestSizeBytes}");
                await HandleSecurityViolation(context, "Request too large", HttpStatusCode.RequestEntityTooLarge);
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateSecurityHeadersAsync(HttpContext context)
        {
            var headers = context.Request.Headers;

            // Validate required security headers for sensitive endpoints
            if (IsSensitiveEndpoint(context.Request.Path.Value))
            {
                if (_config.RequireSecurityHeaders)
                {
                    if (!headers.ContainsKey("X-Requested-With") && 
                        !headers.ContainsKey("Authorization") &&
                        !headers.ContainsKey("X-API-Key"))
                    {
                        await LogSecurityEvent(context, "MISSING_SECURITY_HEADERS", 
                            "Request to sensitive endpoint missing required security headers");
                        await HandleSecurityViolation(context, "Missing required headers", HttpStatusCode.BadRequest);
                        return false;
                    }
                }
            }

            // Check for suspicious headers
            var suspiciousHeaders = new[] { "X-Forwarded-Host", "X-Original-URL", "X-Rewrite-URL" };
            foreach (var header in suspiciousHeaders)
            {
                if (headers.ContainsKey(header))
                {
                    await LogSecurityEvent(context, "SUSPICIOUS_HEADER", $"Suspicious header detected: {header}");
                }
            }

            return true;
        }

        private async Task<bool> ValidateSuspiciousActivityAsync(HttpContext context, string clientIp, string userAgent)
        {
            var endpoint = context.Request.Path.Value;
            var method = context.Request.Method;

            // Check for common attack patterns
            var suspiciousPatterns = new[]
            {
                "../", "..\\", "<script", "javascript:", "vbscript:", "onload=", "onerror=",
                "union select", "drop table", "insert into", "delete from",
                "cmd.exe", "/bin/sh", "powershell", "wget", "curl"
            };

            var queryString = context.Request.QueryString.Value?.ToLower() ?? "";
            var path = endpoint?.ToLower() ?? "";

            foreach (var pattern in suspiciousPatterns)
            {
                if (queryString.Contains(pattern) || path.Contains(pattern))
                {
                    await LogSecurityEvent(context, "SUSPICIOUS_PATTERN", 
                        $"Suspicious pattern '{pattern}' detected in request");
                    await HandleSecurityViolation(context, "Suspicious request pattern", HttpStatusCode.BadRequest);
                    return false;
                }
            }

            // Check for bot/scanner user agents
            var suspiciousUserAgents = new[]
            {
                "sqlmap", "nikto", "nmap", "masscan", "zap", "burp", "acunetix",
                "nessus", "openvas", "w3af", "skipfish", "wpscan"
            };

            var lowerUserAgent = userAgent?.ToLower() ?? "";
            foreach (var suspiciousAgent in suspiciousUserAgents)
            {
                if (lowerUserAgent.Contains(suspiciousAgent))
                {
                    await LogSecurityEvent(context, "SUSPICIOUS_USER_AGENT", 
                        $"Suspicious user agent detected: {userAgent}");
                    await HandleSecurityViolation(context, "Suspicious user agent", HttpStatusCode.Forbidden);
                    return false;
                }
            }

            // Check for rapid sequential requests (simple bot detection)
            if (await DetectRapidRequests(clientIp))
            {
                await LogSecurityEvent(context, "RAPID_REQUESTS", 
                    $"Rapid sequential requests detected from {clientIp}");
                await HandleSecurityViolation(context, "Too many rapid requests", HttpStatusCode.TooManyRequests);
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateApiKeyAsync(HttpContext context)
        {
            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                // Validate API key format
                if (!IsValidApiKeyFormat(apiKey))
                {
                    await LogSecurityEvent(context, "INVALID_API_KEY_FORMAT", 
                        $"Invalid API key format: {apiKey.Substring(0, Math.Min(8, apiKey.Length))}...");
                    await HandleSecurityViolation(context, "Invalid API key format", HttpStatusCode.Unauthorized);
                    return false;
                }

                // Check if API key is revoked or expired
                if (await IsApiKeyRevokedAsync(apiKey))
                {
                    await LogSecurityEvent(context, "REVOKED_API_KEY", 
                        $"Attempt to use revoked API key: {apiKey.Substring(0, Math.Min(8, apiKey.Length))}...");
                    await HandleSecurityViolation(context, "API key revoked", HttpStatusCode.Unauthorized);
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> ValidateCorsAsync(HttpContext context)
        {
            var origin = context.Request.Headers["Origin"].FirstOrDefault();
            
            if (!string.IsNullOrEmpty(origin))
            {
                var allowedOrigins = _config.AllowedOrigins ?? new List<string>();
                
                if (allowedOrigins.Any() && !allowedOrigins.Contains(origin) && !allowedOrigins.Contains("*"))
                {
                    await LogSecurityEvent(context, "CORS_VIOLATION", 
                        $"Request from unauthorized origin: {origin}");
                    await HandleSecurityViolation(context, "Origin not allowed", HttpStatusCode.Forbidden);
                    return false;
                }
            }

            return true;
        }

        private void AddSecurityHeaders(HttpContext context)
        {
            var response = context.Response;

            // Security headers
            response.Headers.Add("X-Content-Type-Options", "nosniff");
            response.Headers.Add("X-Frame-Options", "DENY");
            response.Headers.Add("X-XSS-Protection", "1; mode=block");
            response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
            
            if (_config.EnableHsts)
            {
                response.Headers.Add("Strict-Transport-Security", 
                    $"max-age={_config.HstsMaxAge}; includeSubDomains; preload");
            }

            if (_config.EnableCsp)
            {
                response.Headers.Add("Content-Security-Policy", _config.CspPolicy);
            }

            // Remove server information
            response.Headers.Remove("Server");
            response.Headers.Add("Server", "LKS-BROTHERS");
        }

        private bool IsSensitiveEndpoint(string path)
        {
            var sensitiveEndpoints = new[]
            {
                "/api/ippatent/submit",
                "/api/ippatent/review",
                "/api/payment",
                "/api/auth",
                "/api/ippatent/backup"
            };

            return sensitiveEndpoints.Any(endpoint => path?.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase) == true);
        }

        private async Task<bool> DetectRapidRequests(string clientIp)
        {
            // Simple implementation - in production, use Redis or similar
            // This is a placeholder for rapid request detection logic
            return false;
        }

        private bool IsValidApiKeyFormat(string apiKey)
        {
            // API key should be 32+ characters, alphanumeric with dashes
            if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 32)
                return false;

            return apiKey.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }

        private async Task<bool> IsApiKeyRevokedAsync(string apiKey)
        {
            // Placeholder - implement actual API key validation
            // Check against database of valid/revoked API keys
            return false;
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

        private async Task LogSecurityEvent(HttpContext context, string eventType, string description)
        {
            await _auditService.LogEventAsync(new AuditEvent
            {
                EventType = eventType,
                EntityType = "Security",
                EntityId = "MIDDLEWARE",
                UserId = context.User?.Identity?.Name ?? "ANONYMOUS",
                Description = description,
                IpAddress = GetClientIpAddress(context),
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                Details = new
                {
                    Endpoint = context.Request.Path.Value,
                    Method = context.Request.Method,
                    QueryString = context.Request.QueryString.Value,
                    ContentLength = context.Request.ContentLength,
                    Headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
                }
            });
        }

        private async Task HandleSecurityViolation(HttpContext context, string message, HttpStatusCode statusCode)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "Security violation",
                message = message,
                timestamp = DateTime.UtcNow,
                requestId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }

        private async Task HandleSecurityError(HttpContext context, string message)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "Security error",
                message = message,
                timestamp = DateTime.UtcNow,
                requestId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }

    public class SecurityConfiguration
    {
        public bool EnableIpWhitelist { get; set; } = false;
        public List<string> WhitelistedIPs { get; set; } = new List<string>();
        public List<string> BlacklistedIPs { get; set; } = new List<string>();
        public List<string> AllowedOrigins { get; set; } = new List<string>();
        public long MaxRequestSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
        public bool RequireSecurityHeaders { get; set; } = true;
        public bool EnableHsts { get; set; } = true;
        public int HstsMaxAge { get; set; } = 31536000; // 1 year
        public bool EnableCsp { get; set; } = true;
        public string CspPolicy { get; set; } = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'";
        public int MaxRapidRequestsPerMinute { get; set; } = 60;
        public bool EnableBotDetection { get; set; } = true;
        public bool LogAllRequests { get; set; } = false;
        public bool BlockSuspiciousUserAgents { get; set; } = true;
    }
}
