using System.Collections.Concurrent;
using System.Net;

namespace LksBrothers.Explorer.Middleware;

public class DDoSProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DDoSProtectionMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, ClientRequestInfo> _clients = new();
    private static readonly ConcurrentDictionary<string, DateTime> _blockedIPs = new();
    private static readonly object _lockObject = new object();

    // Configuration
    private const int MAX_REQUESTS_PER_MINUTE = 100;
    private const int MAX_REQUESTS_PER_SECOND = 10;
    private const int BLOCK_DURATION_MINUTES = 15;
    private const int SUSPICIOUS_THRESHOLD = 200;

    public DDoSProtectionMiddleware(RequestDelegate next, ILogger<DDoSProtectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        
        // Start cleanup task
        _ = Task.Run(CleanupTask);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIP = GetClientIP(context);
        var now = DateTime.UtcNow;

        // Check if IP is currently blocked
        if (_blockedIPs.TryGetValue(clientIP, out var blockedUntil))
        {
            if (now < blockedUntil)
            {
                _logger.LogWarning("Blocked IP attempted access: {IP}", clientIP);
                context.Response.StatusCode = 429;
                context.Response.Headers.Add("Retry-After", ((int)(blockedUntil - now).TotalSeconds).ToString());
                await context.Response.WriteAsync("IP temporarily blocked due to suspicious activity");
                return;
            }
            else
            {
                _blockedIPs.TryRemove(clientIP, out _);
            }
        }

        // Get or create client info
        var clientInfo = _clients.GetOrAdd(clientIP, _ => new ClientRequestInfo
        {
            IP = clientIP,
            FirstRequest = now,
            LastRequest = now
        });

        lock (_lockObject)
        {
            // Update request tracking
            clientInfo.LastRequest = now;
            clientInfo.RequestsThisMinute++;
            clientInfo.RequestsThisSecond++;
            clientInfo.TotalRequests++;

            // Reset counters if needed
            if ((now - clientInfo.LastMinuteReset).TotalMinutes >= 1)
            {
                clientInfo.RequestsThisMinute = 1;
                clientInfo.LastMinuteReset = now;
            }

            if ((now - clientInfo.LastSecondReset).TotalSeconds >= 1)
            {
                clientInfo.RequestsThisSecond = 1;
                clientInfo.LastSecondReset = now;
            }
        }

        // Check for DDoS patterns
        if (IsDDoSAttack(clientInfo, now))
        {
            await BlockIP(clientIP, now, "DDoS attack pattern detected");
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Request rate exceeded. IP blocked.");
            return;
        }

        // Check rate limits
        if (clientInfo.RequestsThisSecond > MAX_REQUESTS_PER_SECOND)
        {
            _logger.LogWarning("Rate limit exceeded (per second): {IP} - {Count} requests", 
                clientIP, clientInfo.RequestsThisSecond);
            
            context.Response.StatusCode = 429;
            context.Response.Headers.Add("Retry-After", "1");
            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }

        if (clientInfo.RequestsThisMinute > MAX_REQUESTS_PER_MINUTE)
        {
            _logger.LogWarning("Rate limit exceeded (per minute): {IP} - {Count} requests", 
                clientIP, clientInfo.RequestsThisMinute);
            
            await BlockIP(clientIP, now, "Rate limit exceeded");
            context.Response.StatusCode = 429;
            context.Response.Headers.Add("Retry-After", "60");
            await context.Response.WriteAsync("Rate limit exceeded. IP temporarily blocked.");
            return;
        }

        // Add rate limiting headers
        context.Response.Headers.Add("X-RateLimit-Limit", MAX_REQUESTS_PER_MINUTE.ToString());
        context.Response.Headers.Add("X-RateLimit-Remaining", 
            Math.Max(0, MAX_REQUESTS_PER_MINUTE - clientInfo.RequestsThisMinute).ToString());
        context.Response.Headers.Add("X-RateLimit-Reset", 
            ((DateTimeOffset)clientInfo.LastMinuteReset.AddMinutes(1)).ToUnixTimeSeconds().ToString());

        await _next(context);
    }

    private bool IsDDoSAttack(ClientRequestInfo clientInfo, DateTime now)
    {
        // Pattern 1: Too many requests in a short time
        if (clientInfo.RequestsThisSecond > MAX_REQUESTS_PER_SECOND * 2)
        {
            return true;
        }

        // Pattern 2: Sustained high traffic
        if (clientInfo.RequestsThisMinute > SUSPICIOUS_THRESHOLD)
        {
            return true;
        }

        // Pattern 3: Rapid consecutive requests
        var timeSinceLastRequest = now - clientInfo.LastRequest;
        if (timeSinceLastRequest.TotalMilliseconds < 50 && clientInfo.ConsecutiveRapidRequests > 20)
        {
            return true;
        }

        // Update rapid request counter
        if (timeSinceLastRequest.TotalMilliseconds < 100)
        {
            clientInfo.ConsecutiveRapidRequests++;
        }
        else
        {
            clientInfo.ConsecutiveRapidRequests = 0;
        }

        return false;
    }

    private async Task BlockIP(string ip, DateTime now, string reason)
    {
        var blockUntil = now.AddMinutes(BLOCK_DURATION_MINUTES);
        _blockedIPs.TryAdd(ip, blockUntil);
        
        _logger.LogError("IP blocked: {IP} until {BlockUntil} - Reason: {Reason}", 
            ip, blockUntil, reason);

        // Log to security monitoring
        await LogSecurityEvent(ip, "IPBlocked", reason);
    }

    private async Task LogSecurityEvent(string ip, string eventType, string description)
    {
        _logger.LogWarning("Security Event: {EventType} - IP: {IP} - {Description}", 
            eventType, ip, description);
        
        // In production, integrate with your security monitoring service
        // await _securityService.LogSecurityEvent(new SecurityEvent { ... });
    }

    private string GetClientIP(HttpContext context)
    {
        // Check for forwarded IP first
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0)
            {
                return ips[0].Trim();
            }
        }

        // Check for real IP
        var realIP = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIP))
        {
            return realIP.Trim();
        }

        // Fallback to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private async Task CleanupTask()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
                
                var now = DateTime.UtcNow;
                var cutoffTime = now.AddHours(-1);

                // Clean up old client data
                var keysToRemove = _clients
                    .Where(kvp => kvp.Value.LastRequest < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _clients.TryRemove(key, out _);
                }

                // Clean up expired blocks
                var expiredBlocks = _blockedIPs
                    .Where(kvp => kvp.Value < now)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredBlocks)
                {
                    _blockedIPs.TryRemove(key, out _);
                }

                if (keysToRemove.Count > 0 || expiredBlocks.Count > 0)
                {
                    _logger.LogInformation("Cleanup completed: {ClientsRemoved} clients, {BlocksExpired} blocks expired", 
                        keysToRemove.Count, expiredBlocks.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DDoS protection cleanup task");
            }
        }
    }
}

public class ClientRequestInfo
{
    public string IP { get; set; } = string.Empty;
    public DateTime FirstRequest { get; set; }
    public DateTime LastRequest { get; set; }
    public int RequestsThisMinute { get; set; }
    public int RequestsThisSecond { get; set; }
    public int TotalRequests { get; set; }
    public int ConsecutiveRapidRequests { get; set; }
    public DateTime LastMinuteReset { get; set; } = DateTime.UtcNow;
    public DateTime LastSecondReset { get; set; } = DateTime.UtcNow;
}
