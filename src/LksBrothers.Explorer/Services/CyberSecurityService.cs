using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LksBrothers.Explorer.Data;
using LksBrothers.Explorer.Models;

namespace LksBrothers.Explorer.Services;

public class CyberSecurityService
{
    private readonly ILogger<CyberSecurityService> _logger;
    private readonly ExplorerDbContext _context;
    private readonly IConfiguration _configuration;
    private static readonly Dictionary<string, DateTime> _lastBruteForceAttempt = new();
    private static readonly Dictionary<string, int> _bruteForceAttempts = new();
    private static readonly HashSet<string> _honeypotTraps = new();

    public CyberSecurityService(ILogger<CyberSecurityService> logger, ExplorerDbContext context, IConfiguration configuration)
    {
        _logger = logger;
        _context = context;
        _configuration = configuration;
        InitializeHoneypots();
    }

    private void InitializeHoneypots()
    {
        // Create honeypot endpoints that attackers might target
        _honeypotTraps.Add("/admin/config");
        _honeypotTraps.Add("/wp-admin");
        _honeypotTraps.Add("/phpmyadmin");
        _honeypotTraps.Add("/.env");
        _honeypotTraps.Add("/config.php");
        _honeypotTraps.Add("/backup.sql");
        _honeypotTraps.Add("/database.sql");
        _honeypotTraps.Add("/admin/users.txt");
    }

    public async Task<bool> ValidateSecureRequest(HttpContext context)
    {
        var clientIP = GetClientIP(context);
        var path = context.Request.Path.ToString();

        // Check honeypot traps
        if (IsHoneypotTrap(path))
        {
            await LogSecurityIncident(clientIP, "HoneypotAccess", $"Accessed honeypot: {path}");
            return false;
        }

        // Validate request headers for security
        if (!ValidateRequestHeaders(context))
        {
            await LogSecurityIncident(clientIP, "InvalidHeaders", "Suspicious request headers detected");
            return false;
        }

        // Check for brute force attempts
        if (IsBruteForceAttempt(clientIP, context))
        {
            await LogSecurityIncident(clientIP, "BruteForce", "Brute force attempt detected");
            return false;
        }

        return true;
    }

    private bool IsHoneypotTrap(string path)
    {
        return _honeypotTraps.Any(trap => path.ToLower().Contains(trap.ToLower()));
    }

    private bool ValidateRequestHeaders(HttpContext context)
    {
        var headers = context.Request.Headers;

        // Check for missing or suspicious User-Agent
        var userAgent = headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(userAgent) || IsSuspiciousUserAgent(userAgent))
        {
            return false;
        }

        // Check for suspicious Accept headers
        var accept = headers.Accept.ToString();
        if (accept.Contains("application/x-") && !accept.Contains("application/json"))
        {
            return false;
        }

        // Validate Content-Type for POST requests
        if (context.Request.Method == "POST")
        {
            var contentType = headers.ContentType.ToString();
            if (string.IsNullOrEmpty(contentType) || 
                (!contentType.Contains("application/json") && 
                 !contentType.Contains("application/x-www-form-urlencoded") &&
                 !contentType.Contains("multipart/form-data")))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsSuspiciousUserAgent(string userAgent)
    {
        var suspiciousPatterns = new[]
        {
            "bot", "crawler", "spider", "scraper", "scanner", "exploit",
            "hack", "attack", "injection", "payload", "shell", "backdoor"
        };

        return suspiciousPatterns.Any(pattern => 
            userAgent.ToLower().Contains(pattern));
    }

    private bool IsBruteForceAttempt(string clientIP, HttpContext context)
    {
        var path = context.Request.Path.ToString().ToLower();
        
        // Only check login endpoints
        if (!path.Contains("login") && !path.Contains("auth"))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        
        if (_lastBruteForceAttempt.ContainsKey(clientIP))
        {
            var lastAttempt = _lastBruteForceAttempt[clientIP];
            var timeDiff = now - lastAttempt;

            // If less than 2 seconds between attempts, it's suspicious
            if (timeDiff.TotalSeconds < 2)
            {
                _bruteForceAttempts[clientIP] = _bruteForceAttempts.GetValueOrDefault(clientIP, 0) + 1;
                
                if (_bruteForceAttempts[clientIP] > 5)
                {
                    return true;
                }
            }
            else if (timeDiff.TotalMinutes > 10)
            {
                // Reset counter after 10 minutes
                _bruteForceAttempts[clientIP] = 0;
            }
        }

        _lastBruteForceAttempt[clientIP] = now;
        return false;
    }

    public async Task<string> EncryptSensitiveData(string data)
    {
        try
        {
            var key = GetEncryptionKey();
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using var swEncrypt = new StreamWriter(csEncrypt);

            swEncrypt.Write(data);
            swEncrypt.Close();

            var iv = aes.IV;
            var encrypted = msEncrypt.ToArray();
            var result = new byte[iv.Length + encrypted.Length];
            
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(encrypted, 0, result, iv.Length, encrypted.Length);

            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt sensitive data");
            throw;
        }
    }

    public async Task<string> DecryptSensitiveData(string encryptedData)
    {
        try
        {
            var key = GetEncryptionKey();
            var fullCipher = Convert.FromBase64String(encryptedData);

            using var aes = Aes.Create();
            aes.Key = key;

            var iv = new byte[aes.BlockSize / 8];
            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(cipher);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);

            return await srDecrypt.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt sensitive data");
            throw;
        }
    }

    private byte[] GetEncryptionKey()
    {
        var keyString = _configuration["ENCRYPTION_KEY"] ?? "LKS_NETWORK_DEFAULT_KEY_2025_SECURE";
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
    }

    public async Task<bool> ValidateApiKey(string apiKey, string clientIP)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ApiKey == apiKey && u.IsActive);

            if (user == null)
            {
                await LogSecurityIncident(clientIP, "InvalidApiKey", $"Invalid API key used: {apiKey}");
                return false;
            }

            // Check API rate limits
            if (user.ApiCallsToday >= user.ApiCallLimit)
            {
                await LogSecurityIncident(clientIP, "ApiLimitExceeded", $"API limit exceeded for user: {user.Email}");
                return false;
            }

            // Update API usage
            user.ApiCallsToday++;
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating API key");
            return false;
        }
    }

    public async Task PerformSecurityScan()
    {
        _logger.LogInformation("Starting security scan...");

        // Check for suspicious database activities
        await CheckDatabaseSecurity();

        // Validate system configurations
        await ValidateSystemConfiguration();

        // Check for unauthorized access attempts
        await AnalyzeAccessPatterns();

        // Verify encryption integrity
        await VerifyEncryptionIntegrity();

        _logger.LogInformation("Security scan completed");
    }

    private async Task CheckDatabaseSecurity()
    {
        try
        {
            // Check for suspicious user activities
            var suspiciousActivities = await _context.UserActivities
                .Where(a => a.CreatedAt > DateTime.UtcNow.AddHours(-1))
                .GroupBy(a => a.UserId)
                .Where(g => g.Count() > 100)
                .ToListAsync();

            foreach (var activity in suspiciousActivities)
            {
                var user = await _context.Users.FindAsync(activity.Key);
                if (user != null)
                {
                    _logger.LogWarning("Suspicious activity detected for user: {Email}", user.Email);
                    await LogSecurityIncident("system", "SuspiciousUserActivity", 
                        $"User {user.Email} has {activity.Count()} activities in the last hour");
                }
            }

            // Check for failed login attempts
            var failedLogins = await _context.UserActivities
                .Where(a => a.Action == "Failed Login" && a.CreatedAt > DateTime.UtcNow.AddMinutes(-30))
                .CountAsync();

            if (failedLogins > 50)
            {
                _logger.LogError("High number of failed login attempts: {Count}", failedLogins);
                await LogSecurityIncident("system", "BruteForceAttack", 
                    $"{failedLogins} failed login attempts in 30 minutes");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking database security");
        }
    }

    private async Task ValidateSystemConfiguration()
    {
        var issues = new List<string>();

        // Check JWT configuration
        var jwtKey = _configuration["JWT_KEY"];
        if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
        {
            issues.Add("JWT key is too weak or missing");
        }

        // Check database connection security
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Trusted_Connection=false"))
        {
            if (!connectionString.Contains("Encrypt=true"))
            {
                issues.Add("Database connection is not encrypted");
            }
        }

        // Check HTTPS configuration
        var httpsPort = _configuration["ASPNETCORE_HTTPS_PORT"];
        if (string.IsNullOrEmpty(httpsPort))
        {
            issues.Add("HTTPS port not configured");
        }

        foreach (var issue in issues)
        {
            _logger.LogWarning("Security configuration issue: {Issue}", issue);
            await LogSecurityIncident("system", "ConfigurationIssue", issue);
        }
    }

    private async Task AnalyzeAccessPatterns()
    {
        // This would analyze access logs for patterns
        // In a real implementation, you'd integrate with your logging system
        _logger.LogInformation("Analyzing access patterns...");
        
        // Simulate pattern analysis
        await Task.Delay(100);
    }

    private async Task VerifyEncryptionIntegrity()
    {
        try
        {
            // Test encryption/decryption
            var testData = "LKS_NETWORK_SECURITY_TEST";
            var encrypted = await EncryptSensitiveData(testData);
            var decrypted = await DecryptSensitiveData(encrypted);

            if (decrypted != testData)
            {
                _logger.LogError("Encryption integrity check failed");
                await LogSecurityIncident("system", "EncryptionFailure", "Encryption integrity check failed");
            }
            else
            {
                _logger.LogInformation("Encryption integrity verified");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying encryption integrity");
            await LogSecurityIncident("system", "EncryptionError", ex.Message);
        }
    }

    private async Task LogSecurityIncident(string sourceIP, string incidentType, string description)
    {
        try
        {
            var incident = new SecurityIncident
            {
                SourceIP = sourceIP,
                IncidentType = incidentType,
                Description = description,
                Severity = GetIncidentSeverity(incidentType),
                Timestamp = DateTime.UtcNow,
                Status = "Active"
            };

            // In a real implementation, you'd save this to a security incidents table
            _logger.LogError("Security Incident: {Type} from {IP} - {Description}", 
                incidentType, sourceIP, description);

            // Trigger alerts for high-severity incidents
            if (incident.Severity == "High" || incident.Severity == "Critical")
            {
                await TriggerSecurityAlert(incident);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log security incident");
        }
    }

    private string GetIncidentSeverity(string incidentType)
    {
        return incidentType switch
        {
            "BruteForce" => "High",
            "HoneypotAccess" => "High",
            "SqlInjection" => "Critical",
            "XssAttempt" => "High",
            "InvalidApiKey" => "Medium",
            "ConfigurationIssue" => "Medium",
            "EncryptionFailure" => "Critical",
            _ => "Low"
        };
    }

    private async Task TriggerSecurityAlert(SecurityIncident incident)
    {
        // In production, integrate with alerting systems
        _logger.LogCritical("🚨 CRITICAL SECURITY ALERT: {Type} - {Description}", 
            incident.IncidentType, incident.Description);
        
        // Example: Send to security team
        // await NotifySecurityTeam(incident);
    }

    private string GetClientIP(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

public class SecurityIncident
{
    public string SourceIP { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty;
}
