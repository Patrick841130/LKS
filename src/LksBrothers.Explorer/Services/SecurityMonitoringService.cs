using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace LksBrothers.Explorer.Services;

public class SecurityMonitoringService : BackgroundService
{
    private readonly ILogger<SecurityMonitoringService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, SecurityMetrics> _securityMetrics = new();
    private readonly ConcurrentQueue<SecurityEvent> _securityEvents = new();

    public SecurityMonitoringService(ILogger<SecurityMonitoringService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorSystemHealth();
                await AnalyzeSecurityEvents();
                await CheckForAnomalies();
                await CleanupOldData();
                
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in security monitoring service");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task MonitorSystemHealth()
    {
        var cpuUsage = await GetCpuUsage();
        var memoryUsage = GetMemoryUsage();
        var diskUsage = GetDiskUsage();

        if (cpuUsage > 90 || memoryUsage > 90 || diskUsage > 90)
        {
            _logger.LogWarning("High resource usage detected - CPU: {CPU}%, Memory: {Memory}%, Disk: {Disk}%", 
                cpuUsage, memoryUsage, diskUsage);
            
            await RecordSecurityEvent(new SecurityEvent
            {
                Type = "ResourceExhaustion",
                Severity = "High",
                Description = $"High resource usage - CPU: {cpuUsage}%, Memory: {memoryUsage}%, Disk: {diskUsage}%",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private async Task<double> GetCpuUsage()
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            
            await Task.Delay(1000);
            
            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            
            return cpuUsageTotal * 100;
        }
        catch
        {
            return 0;
        }
    }

    private double GetMemoryUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var totalMemory = GC.GetTotalMemory(false);
            var workingSet = process.WorkingSet64;
            
            return (double)workingSet / (1024 * 1024 * 1024) * 100; // Convert to percentage
        }
        catch
        {
            return 0;
        }
    }

    private double GetDiskUsage()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\");
            var usedSpace = drive.TotalSize - drive.AvailableFreeSpace;
            return (double)usedSpace / drive.TotalSize * 100;
        }
        catch
        {
            return 0;
        }
    }

    private async Task AnalyzeSecurityEvents()
    {
        var recentEvents = new List<SecurityEvent>();
        
        while (_securityEvents.TryDequeue(out var securityEvent))
        {
            recentEvents.Add(securityEvent);
        }

        if (recentEvents.Count == 0) return;

        // Analyze patterns
        var suspiciousIPs = recentEvents
            .Where(e => e.Type == "SuspiciousActivity")
            .GroupBy(e => e.SourceIP)
            .Where(g => g.Count() > 5)
            .Select(g => g.Key)
            .ToList();

        foreach (var ip in suspiciousIPs)
        {
            _logger.LogError("Suspicious activity pattern detected from IP: {IP}", ip);
            await NotifySecurityTeam($"Multiple security events from IP: {ip}");
        }

        // Check for coordinated attacks
        var coordinatedAttack = recentEvents
            .Where(e => e.Timestamp > DateTime.UtcNow.AddMinutes(-10))
            .GroupBy(e => e.Type)
            .Where(g => g.Count() > 10)
            .FirstOrDefault();

        if (coordinatedAttack != null)
        {
            _logger.LogError("Potential coordinated attack detected: {AttackType}", coordinatedAttack.Key);
            await NotifySecurityTeam($"Coordinated {coordinatedAttack.Key} attack detected");
        }
    }

    private async Task CheckForAnomalies()
    {
        // Check for unusual traffic patterns
        var currentHour = DateTime.UtcNow.Hour;
        var expectedTraffic = GetExpectedTraffic(currentHour);
        var actualTraffic = GetCurrentTrafficMetrics();

        if (actualTraffic.RequestsPerMinute > expectedTraffic * 3)
        {
            _logger.LogWarning("Unusual traffic spike detected: {Actual} vs expected {Expected}", 
                actualTraffic.RequestsPerMinute, expectedTraffic);
            
            await RecordSecurityEvent(new SecurityEvent
            {
                Type = "TrafficAnomaly",
                Severity = "Medium",
                Description = $"Traffic spike: {actualTraffic.RequestsPerMinute} requests/min",
                Timestamp = DateTime.UtcNow
            });
        }

        // Check for failed authentication attempts
        if (actualTraffic.FailedLogins > 50)
        {
            _logger.LogError("High number of failed login attempts: {Count}", actualTraffic.FailedLogins);
            await NotifySecurityTeam($"Brute force attack suspected: {actualTraffic.FailedLogins} failed logins");
        }
    }

    private int GetExpectedTraffic(int hour)
    {
        // Business hours (9-17) expect higher traffic
        return hour >= 9 && hour <= 17 ? 100 : 30;
    }

    private TrafficMetrics GetCurrentTrafficMetrics()
    {
        // This would integrate with your actual metrics collection
        return new TrafficMetrics
        {
            RequestsPerMinute = Random.Shared.Next(10, 200),
            FailedLogins = Random.Shared.Next(0, 100),
            UniqueIPs = Random.Shared.Next(10, 500)
        };
    }

    private async Task CleanupOldData()
    {
        var cutoffTime = DateTime.UtcNow.AddDays(-7);
        
        var keysToRemove = _securityMetrics
            .Where(kvp => kvp.Value.LastUpdated < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _securityMetrics.TryRemove(key, out _);
        }

        _logger.LogInformation("Cleaned up {Count} old security metrics", keysToRemove.Count);
    }

    public async Task RecordSecurityEvent(SecurityEvent securityEvent)
    {
        _securityEvents.Enqueue(securityEvent);
        
        // Log high severity events immediately
        if (securityEvent.Severity == "High" || securityEvent.Severity == "Critical")
        {
            _logger.LogError("High severity security event: {Type} - {Description}", 
                securityEvent.Type, securityEvent.Description);
            await NotifySecurityTeam(securityEvent.Description);
        }
    }

    private async Task NotifySecurityTeam(string message)
    {
        // In production, integrate with alerting systems like:
        // - Email notifications
        // - Slack/Teams webhooks
        // - PagerDuty
        // - SMS alerts
        
        _logger.LogCritical("SECURITY ALERT: {Message}", message);
        
        // Example webhook notification
        try
        {
            using var httpClient = new HttpClient();
            var payload = new
            {
                text = $"🚨 LKS NETWORK Security Alert: {message}",
                timestamp = DateTime.UtcNow,
                severity = "high"
            };
            
            // Uncomment and configure your webhook URL
            // await httpClient.PostAsJsonAsync("YOUR_WEBHOOK_URL", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send security notification");
        }
    }

    public SecurityMetrics GetSecurityMetrics(string identifier)
    {
        return _securityMetrics.GetOrAdd(identifier, _ => new SecurityMetrics
        {
            Identifier = identifier,
            LastUpdated = DateTime.UtcNow
        });
    }
}

public class SecurityEvent
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? SourceIP { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SecurityMetrics
{
    public string Identifier { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime LastUpdated { get; set; }
    public List<string> SuspiciousActivities { get; set; } = new();
}

public class TrafficMetrics
{
    public int RequestsPerMinute { get; set; }
    public int FailedLogins { get; set; }
    public int UniqueIPs { get; set; }
}
