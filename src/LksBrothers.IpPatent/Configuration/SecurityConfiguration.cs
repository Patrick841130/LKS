using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LksBrothers.IpPatent.Configuration
{
    public class SecurityConfiguration
    {
        public RateLimitingSettings RateLimiting { get; set; } = new RateLimitingSettings();
        public IpFilteringSettings IpFiltering { get; set; } = new IpFilteringSettings();
        public RequestValidationSettings RequestValidation { get; set; } = new RequestValidationSettings();
        public SecurityHeadersSettings SecurityHeaders { get; set; } = new SecurityHeadersSettings();
        public ApiKeySettings ApiKeys { get; set; } = new ApiKeySettings();
        public CorsSettings Cors { get; set; } = new CorsSettings();
        public ThreatDetectionSettings ThreatDetection { get; set; } = new ThreatDetectionSettings();
        public AuditingSettings Auditing { get; set; } = new AuditingSettings();
    }

    public class RateLimitingSettings
    {
        public bool Enabled { get; set; } = true;
        public int DefaultMaxRequests { get; set; } = 100;
        public TimeSpan DefaultTimeWindow { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan BlockDuration { get; set; } = TimeSpan.FromMinutes(15);
        public List<string> WhitelistedIps { get; set; } = new List<string>();
        public List<string> WhitelistedApiKeys { get; set; } = new List<string>();
        public Dictionary<string, EndpointRateLimit> EndpointLimits { get; set; } = new Dictionary<string, EndpointRateLimit>
        {
            { "/api/submissions", new EndpointRateLimit { MaxRequests = 50, TimeWindow = TimeSpan.FromHours(1) } },
            { "/api/reviews", new EndpointRateLimit { MaxRequests = 200, TimeWindow = TimeSpan.FromHours(1) } },
            { "/api/auth/login", new EndpointRateLimit { MaxRequests = 10, TimeWindow = TimeSpan.FromMinutes(15) } },
            { "/api/auth/register", new EndpointRateLimit { MaxRequests = 5, TimeWindow = TimeSpan.FromHours(1) } },
            { "/api/backup", new EndpointRateLimit { MaxRequests = 5, TimeWindow = TimeSpan.FromHours(24) } },
            { "/api/apikey", new EndpointRateLimit { MaxRequests = 20, TimeWindow = TimeSpan.FromHours(1) } }
        };
        public bool LogViolations { get; set; } = true;
        public bool EnableDistributedCache { get; set; } = true;
        public string RedisConnectionString { get; set; } = "localhost:6379";
    }

    public class EndpointRateLimit
    {
        public int MaxRequests { get; set; }
        public TimeSpan TimeWindow { get; set; }
        public bool PerUser { get; set; } = false;
        public bool PerApiKey { get; set; } = true;
        public bool PerIp { get; set; } = true;
    }

    public class IpFilteringSettings
    {
        public bool Enabled { get; set; } = true;
        public List<string> WhitelistedIps { get; set; } = new List<string>
        {
            "127.0.0.1",
            "::1",
            "10.0.0.0/8",
            "172.16.0.0/12",
            "192.168.0.0/16"
        };
        public List<string> BlacklistedIps { get; set; } = new List<string>();
        public List<string> BlacklistedCountries { get; set; } = new List<string>();
        public bool AllowPrivateNetworks { get; set; } = true;
        public bool LogBlocked { get; set; } = true;
        public bool EnableGeoBlocking { get; set; } = false;
        public string GeoLocationService { get; set; } = "MaxMind";
        public string GeoLocationApiKey { get; set; }
    }

    public class RequestValidationSettings
    {
        public bool Enabled { get; set; } = true;
        public long MaxRequestSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
        public long MaxFileUploadSizeBytes { get; set; } = 50 * 1024 * 1024; // 50MB
        public List<string> AllowedFileExtensions { get; set; } = new List<string>
        {
            ".pdf", ".doc", ".docx", ".txt", ".png", ".jpg", ".jpeg", ".gif"
        };
        public List<string> BlockedUserAgents { get; set; } = new List<string>
        {
            "bot", "crawler", "spider", "scraper", "curl", "wget"
        };
        public List<string> RequiredHeaders { get; set; } = new List<string>
        {
            "User-Agent", "Accept"
        };
        public bool ValidateContentType { get; set; } = true;
        public bool ValidateOrigin { get; set; } = true;
        public bool LogSuspiciousRequests { get; set; } = true;
        public int MaxHeaderCount { get; set; } = 50;
        public int MaxHeaderValueLength { get; set; } = 8192;
        public int MaxQueryStringLength { get; set; } = 2048;
    }

    public class SecurityHeadersSettings
    {
        public bool Enabled { get; set; } = true;
        public bool RemoveServerHeader { get; set; } = true;
        public bool RemovePoweredByHeader { get; set; } = true;
        public HstsSettings Hsts { get; set; } = new HstsSettings();
        public CspSettings ContentSecurityPolicy { get; set; } = new CspSettings();
        public Dictionary<string, string> CustomHeaders { get; set; } = new Dictionary<string, string>
        {
            { "X-Content-Type-Options", "nosniff" },
            { "X-Frame-Options", "DENY" },
            { "X-XSS-Protection", "1; mode=block" },
            { "Referrer-Policy", "strict-origin-when-cross-origin" },
            { "Permissions-Policy", "geolocation=(), microphone=(), camera=()" }
        };
    }

    public class HstsSettings
    {
        public bool Enabled { get; set; } = true;
        public int MaxAgeSeconds { get; set; } = 31536000; // 1 year
        public bool IncludeSubDomains { get; set; } = true;
        public bool Preload { get; set; } = false;
    }

    public class CspSettings
    {
        public bool Enabled { get; set; } = true;
        public string DefaultSrc { get; set; } = "'self'";
        public string ScriptSrc { get; set; } = "'self' 'unsafe-inline'";
        public string StyleSrc { get; set; } = "'self' 'unsafe-inline'";
        public string ImgSrc { get; set; } = "'self' data: https:";
        public string ConnectSrc { get; set; } = "'self'";
        public string FontSrc { get; set; } = "'self'";
        public string ObjectSrc { get; set; } = "'none'";
        public string MediaSrc { get; set; } = "'self'";
        public string FrameSrc { get; set; } = "'none'";
        public bool ReportOnly { get; set; } = false;
        public string ReportUri { get; set; }
    }

    public class ApiKeySettings
    {
        public bool Enabled { get; set; } = true;
        public int DefaultExpirationDays { get; set; } = 365;
        public int MaxKeysPerUser { get; set; } = 10;
        public bool RequireApiKeyForPublicEndpoints { get; set; } = false;
        public List<string> PublicEndpoints { get; set; } = new List<string>
        {
            "/api/health",
            "/api/status",
            "/api/docs"
        };
        public Dictionary<string, int> ScopeRateLimits { get; set; } = new Dictionary<string, int>
        {
            { "ReadOnly", 1000 },
            { "Standard", 500 },
            { "Premium", 2000 },
            { "Admin", 5000 }
        };
        public bool LogKeyUsage { get; set; } = true;
        public bool EnableKeyRotation { get; set; } = true;
        public int KeyRotationWarningDays { get; set; } = 30;
    }

    public class CorsSettings
    {
        public bool Enabled { get; set; } = true;
        public List<string> AllowedOrigins { get; set; } = new List<string>
        {
            "https://localhost:3000",
            "https://localhost:5001",
            "https://lksnetwork.io",
            "https://admin.lksnetwork.io"
        };
        public List<string> AllowedMethods { get; set; } = new List<string>
        {
            "GET", "POST", "PUT", "DELETE", "OPTIONS"
        };
        public List<string> AllowedHeaders { get; set; } = new List<string>
        {
            "Content-Type", "Authorization", "X-API-Key", "X-Requested-With"
        };
        public List<string> ExposedHeaders { get; set; } = new List<string>
        {
            "X-Rate-Limit-Remaining", "X-Rate-Limit-Reset", "X-Total-Count"
        };
        public bool AllowCredentials { get; set; } = true;
        public int PreflightMaxAge { get; set; } = 86400; // 24 hours
        public bool ValidateOrigin { get; set; } = true;
    }

    public class ThreatDetectionSettings
    {
        public bool Enabled { get; set; } = true;
        public SqlInjectionDetection SqlInjection { get; set; } = new SqlInjectionDetection();
        public XssDetection Xss { get; set; } = new XssDetection();
        public BruteForceDetection BruteForce { get; set; } = new BruteForceDetection();
        public DdosDetection Ddos { get; set; } = new DdosDetection();
        public bool LogThreats { get; set; } = true;
        public bool BlockThreats { get; set; } = true;
        public string NotificationEmail { get; set; }
        public bool EnableRealTimeAlerts { get; set; } = true;
    }

    public class SqlInjectionDetection
    {
        public bool Enabled { get; set; } = true;
        public List<string> Patterns { get; set; } = new List<string>
        {
            @"(\b(ALTER|CREATE|DELETE|DROP|EXEC(UTE)?|INSERT|SELECT|UNION|UPDATE)\b)",
            @"(\b(OR|AND)\s+\d+\s*=\s*\d+)",
            @"(\b(OR|AND)\s+['""]?\w+['""]?\s*=\s*['""]?\w+['""]?)",
            @"(--|#|/\*|\*/)",
            @"(\bxp_cmdshell\b|\bsp_executesql\b)"
        };
        public bool CaseSensitive { get; set; } = false;
        public int ThreatScore { get; set; } = 100;
    }

    public class XssDetection
    {
        public bool Enabled { get; set; } = true;
        public List<string> Patterns { get; set; } = new List<string>
        {
            @"<script[^>]*>.*?</script>",
            @"javascript:",
            @"on\w+\s*=",
            @"<iframe[^>]*>.*?</iframe>",
            @"<object[^>]*>.*?</object>",
            @"<embed[^>]*>.*?</embed>"
        };
        public bool CaseSensitive { get; set; } = false;
        public int ThreatScore { get; set; } = 80;
    }

    public class BruteForceDetection
    {
        public bool Enabled { get; set; } = true;
        public int MaxFailedAttempts { get; set; } = 5;
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(30);
        public List<string> MonitoredEndpoints { get; set; } = new List<string>
        {
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/reset-password"
        };
        public bool TrackByIp { get; set; } = true;
        public bool TrackByUser { get; set; } = true;
    }

    public class DdosDetection
    {
        public bool Enabled { get; set; } = true;
        public int MaxRequestsPerSecond { get; set; } = 100;
        public int MaxRequestsPerMinute { get; set; } = 1000;
        public TimeSpan MonitoringWindow { get; set; } = TimeSpan.FromMinutes(5);
        public int SuspiciousThreshold { get; set; } = 500;
        public int BlockThreshold { get; set; } = 1000;
        public TimeSpan BlockDuration { get; set; } = TimeSpan.FromHours(1);
    }

    public class AuditingSettings
    {
        public bool Enabled { get; set; } = true;
        public bool LogAllRequests { get; set; } = false;
        public bool LogSecurityEvents { get; set; } = true;
        public bool LogRateLimitViolations { get; set; } = true;
        public bool LogFailedAuthentication { get; set; } = true;
        public bool LogApiKeyUsage { get; set; } = true;
        public bool LogSuspiciousActivity { get; set; } = true;
        public List<string> SensitiveHeaders { get; set; } = new List<string>
        {
            "Authorization", "X-API-Key", "Cookie", "Set-Cookie"
        };
        public List<string> SensitiveFields { get; set; } = new List<string>
        {
            "password", "token", "secret", "key", "credential"
        };
        public int MaxLogRetentionDays { get; set; } = 90;
        public bool EnableRealTimeMonitoring { get; set; } = true;
        public string LogLevel { get; set; } = "Information";
    }

    public class SecurityMetrics
    {
        public long TotalRequests { get; set; }
        public long BlockedRequests { get; set; }
        public long RateLimitViolations { get; set; }
        public long SecurityThreats { get; set; }
        public long FailedAuthentications { get; set; }
        public long ApiKeyViolations { get; set; }
        public Dictionary<string, long> ThreatsByType { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, long> BlockedIps { get; set; } = new Dictionary<string, long>();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class SecurityAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Endpoint { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool Acknowledged { get; set; } = false;
        public string AcknowledgedBy { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
    }

    public enum SecurityThreatType
    {
        SqlInjection,
        XssAttempt,
        BruteForce,
        DdosAttack,
        SuspiciousUserAgent,
        InvalidApiKey,
        RateLimitExceeded,
        UnauthorizedAccess,
        MaliciousPayload,
        GeoBlocked
    }

    public enum SecuritySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}
