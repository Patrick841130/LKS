using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LksBrothers.Explorer.Middleware;

public class SecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private static readonly Dictionary<string, List<DateTime>> _requestHistory = new();
    private static readonly Dictionary<string, int> _suspiciousActivity = new();
    private static readonly HashSet<string> _blockedIPs = new();

    public SecurityMiddleware(RequestDelegate next, ILogger<SecurityMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIP = GetClientIP(context);
        
        // Check if IP is blocked
        if (_blockedIPs.Contains(clientIP))
        {
            _logger.LogWarning("Blocked IP attempted access: {IP}", clientIP);
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("Access denied");
            return;
        }

        // Advanced rate limiting
        if (await IsRateLimited(clientIP, context))
        {
            return;
        }

        // Security headers
        AddSecurityHeaders(context);

        // SQL injection detection
        if (DetectSQLInjection(context))
        {
            await HandleSecurityThreat(context, clientIP, "SQL Injection attempt detected");
            return;
        }

        // XSS detection
        if (DetectXSS(context))
        {
            await HandleSecurityThreat(context, clientIP, "XSS attempt detected");
            return;
        }

        // Path traversal detection
        if (DetectPathTraversal(context))
        {
            await HandleSecurityThreat(context, clientIP, "Path traversal attempt detected");
            return;
        }

        // Suspicious user agent detection
        if (DetectSuspiciousUserAgent(context))
        {
            await HandleSecurityThreat(context, clientIP, "Suspicious user agent detected");
            return;
        }

        // Log legitimate requests
        LogRequest(context, clientIP);

        await _next(context);
    }

    private string GetClientIP(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }
        
        var realIP = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIP))
        {
            return realIP;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private async Task<bool> IsRateLimited(string clientIP, HttpContext context)
    {
        var now = DateTime.UtcNow;
        var timeWindow = TimeSpan.FromMinutes(1);
        var maxRequests = GetRateLimit(context.Request.Path);

        if (!_requestHistory.ContainsKey(clientIP))
        {
            _requestHistory[clientIP] = new List<DateTime>();
        }

        var requests = _requestHistory[clientIP];
        
        // Remove old requests
        requests.RemoveAll(r => now - r > timeWindow);
        
        if (requests.Count >= maxRequests)
        {
            _logger.LogWarning("Rate limit exceeded for IP: {IP}, Path: {Path}", clientIP, context.Request.Path);
            
            // Increase suspicious activity counter
            _suspiciousActivity[clientIP] = _suspiciousActivity.GetValueOrDefault(clientIP, 0) + 1;
            
            // Block IP if too many violations
            if (_suspiciousActivity[clientIP] > 10)
            {
                _blockedIPs.Add(clientIP);
                _logger.LogError("IP blocked due to excessive violations: {IP}", clientIP);
            }

            context.Response.StatusCode = 429;
            context.Response.Headers.Add("Retry-After", "60");
            await context.Response.WriteAsync("Rate limit exceeded");
            return true;
        }

        requests.Add(now);
        return false;
    }

    private int GetRateLimit(string path)
    {
        return path.ToLower() switch
        {
            var p when p.Contains("/api/payment") => 5,
            var p when p.Contains("/api/user/login") => 10,
            var p when p.Contains("/api/user/register") => 3,
            var p when p.Contains("/api/admin") => 20,
            _ => 60
        };
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        var response = context.Response;
        
        // Prevent clickjacking
        response.Headers.Add("X-Frame-Options", "DENY");
        
        // Prevent MIME type sniffing
        response.Headers.Add("X-Content-Type-Options", "nosniff");
        
        // XSS protection
        response.Headers.Add("X-XSS-Protection", "1; mode=block");
        
        // Referrer policy
        response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        
        // Content Security Policy
        response.Headers.Add("Content-Security-Policy", 
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "connect-src 'self' wss: ws:; " +
            "frame-ancestors 'none'");
        
        // HSTS
        if (context.Request.IsHttps)
        {
            response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }
        
        // Feature policy
        response.Headers.Add("Permissions-Policy", 
            "geolocation=(), microphone=(), camera=(), payment=()");
    }

    private bool DetectSQLInjection(HttpContext context)
    {
        var patterns = new[]
        {
            @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE)\b)",
            @"(\b(UNION|OR|AND)\b.*\b(SELECT|INSERT|UPDATE|DELETE)\b)",
            @"('|\"|;|--|\*|\|)",
            @"(\b(SCRIPT|JAVASCRIPT|VBSCRIPT)\b)",
            @"(1=1|1=0|'=')",
            @"(\bOR\b.*\b1=1\b)"
        };

        var queryString = context.Request.QueryString.ToString().ToUpper();
        var userAgent = context.Request.Headers.UserAgent.ToString().ToUpper();
        
        foreach (var pattern in patterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(queryString, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(userAgent, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool DetectXSS(HttpContext context)
    {
        var patterns = new[]
        {
            @"<script[^>]*>.*?</script>",
            @"javascript:",
            @"vbscript:",
            @"onload\s*=",
            @"onerror\s*=",
            @"onclick\s*=",
            @"<iframe[^>]*>",
            @"<object[^>]*>",
            @"<embed[^>]*>"
        };

        var queryString = context.Request.QueryString.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();
        
        foreach (var pattern in patterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(queryString, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(userAgent, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool DetectPathTraversal(HttpContext context)
    {
        var path = context.Request.Path.ToString();
        var patterns = new[]
        {
            @"\.\./",
            @"\.\.\\",
            @"%2e%2e%2f",
            @"%2e%2e%5c",
            @"..%2f",
            @"..%5c"
        };

        return patterns.Any(pattern => 
            System.Text.RegularExpressions.Regex.IsMatch(path, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    private bool DetectSuspiciousUserAgent(HttpContext context)
    {
        var userAgent = context.Request.Headers.UserAgent.ToString().ToLower();
        var suspiciousAgents = new[]
        {
            "sqlmap", "nikto", "nmap", "masscan", "zap", "burp", "acunetix",
            "nessus", "openvas", "w3af", "skipfish", "arachni", "wpscan",
            "dirb", "dirbuster", "gobuster", "ffuf", "wfuzz", "hydra"
        };

        return suspiciousAgents.Any(agent => userAgent.Contains(agent));
    }

    private async Task HandleSecurityThreat(HttpContext context, string clientIP, string threatType)
    {
        _logger.LogError("Security threat detected: {ThreatType} from IP: {IP}, Path: {Path}, UserAgent: {UserAgent}", 
            threatType, clientIP, context.Request.Path, context.Request.Headers.UserAgent);

        // Increase suspicious activity
        _suspiciousActivity[clientIP] = _suspiciousActivity.GetValueOrDefault(clientIP, 0) + 5;

        // Block IP if too many threats
        if (_suspiciousActivity[clientIP] > 15)
        {
            _blockedIPs.Add(clientIP);
            _logger.LogError("IP blocked due to security threats: {IP}", clientIP);
        }

        context.Response.StatusCode = 403;
        await context.Response.WriteAsync("Security violation detected");
    }

    private void LogRequest(HttpContext context, string clientIP)
    {
        _logger.LogInformation("Request: {Method} {Path} from {IP} - {UserAgent}", 
            context.Request.Method, 
            context.Request.Path, 
            clientIP, 
            context.Request.Headers.UserAgent.ToString());
    }
}
