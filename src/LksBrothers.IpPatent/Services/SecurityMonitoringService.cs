using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LksBrothers.IpPatent.Configuration;

namespace LksBrothers.IpPatent.Services
{
    public interface ISecurityMonitoringService
    {
        Task<SecurityMetrics> GetSecurityMetricsAsync();
        Task<List<SecurityAlert>> GetActiveAlertsAsync();
        Task<bool> AcknowledgeAlertAsync(string alertId, string acknowledgedBy);
        Task LogSecurityEventAsync(SecurityAlert alert);
        Task<bool> IsIpBlockedAsync(string ipAddress);
        Task BlockIpAsync(string ipAddress, string reason, TimeSpan? duration = null);
        Task UnblockIpAsync(string ipAddress);
        Task<List<string>> GetBlockedIpsAsync();
        Task<Dictionary<string, object>> GetThreatIntelligenceAsync();
        Task CleanupOldAlertsAsync();
    }

    public class SecurityMonitoringService : BackgroundService, ISecurityMonitoringService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SecurityMonitoringService> _logger;
        private readonly IAuditTrailService _auditService;
        private readonly IEmailService _emailService;
        private readonly SecurityConfiguration _securityConfig;
        
        private readonly ConcurrentDictionary<string, SecurityMetrics> _metrics;
        private readonly ConcurrentDictionary<string, SecurityAlert> _activeAlerts;
        private readonly ConcurrentDictionary<string, BlockedIpInfo> _blockedIps;
        private readonly ConcurrentDictionary<string, List<SecurityEvent>> _recentEvents;
        
        private readonly Timer _metricsTimer;
        private readonly Timer _cleanupTimer;

        public SecurityMonitoringService(
            IConfiguration configuration,
            ILogger<SecurityMonitoringService> logger,
            IAuditTrailService auditService,
            IEmailService emailService)
        {
            _configuration = configuration;
            _logger = logger;
            _auditService = auditService;
            _emailService = emailService;
            _securityConfig = configuration.GetSection("Security").Get<SecurityConfiguration>() ?? new SecurityConfiguration();
            
            _metrics = new ConcurrentDictionary<string, SecurityMetrics>();
            _activeAlerts = new ConcurrentDictionary<string, SecurityAlert>();
            _blockedIps = new ConcurrentDictionary<string, BlockedIpInfo>();
            _recentEvents = new ConcurrentDictionary<string, List<SecurityEvent>>();
            
            // Initialize metrics collection timer (every minute)
            _metricsTimer = new Timer(CollectMetrics, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            
            // Initialize cleanup timer (every hour)
            _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Security Monitoring Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await MonitorSecurityEvents();
                    await AnalyzeThreatPatterns();
                    await CheckForAnomalies();
                    
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in security monitoring loop");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        public async Task<SecurityMetrics> GetSecurityMetricsAsync()
        {
            var currentMetrics = new SecurityMetrics
            {
                LastUpdated = DateTime.UtcNow
            };

            // Aggregate metrics from all sources
            foreach (var metric in _metrics.Values)
            {
                currentMetrics.TotalRequests += metric.TotalRequests;
                currentMetrics.BlockedRequests += metric.BlockedRequests;
                currentMetrics.RateLimitViolations += metric.RateLimitViolations;
                currentMetrics.SecurityThreats += metric.SecurityThreats;
                currentMetrics.FailedAuthentications += metric.FailedAuthentications;
                currentMetrics.ApiKeyViolations += metric.ApiKeyViolations;

                foreach (var threat in metric.ThreatsByType)
                {
                    currentMetrics.ThreatsByType[threat.Key] = 
                        currentMetrics.ThreatsByType.GetValueOrDefault(threat.Key, 0) + threat.Value;
                }

                foreach (var ip in metric.BlockedIps)
                {
                    currentMetrics.BlockedIps[ip.Key] = 
                        currentMetrics.BlockedIps.GetValueOrDefault(ip.Key, 0) + ip.Value;
                }
            }

            return currentMetrics;
        }

        public async Task<List<SecurityAlert>> GetActiveAlertsAsync()
        {
            return _activeAlerts.Values
                .Where(a => !a.Acknowledged)
                .OrderByDescending(a => a.Timestamp)
                .ToList();
        }

        public async Task<bool> AcknowledgeAlertAsync(string alertId, string acknowledgedBy)
        {
            if (_activeAlerts.TryGetValue(alertId, out var alert))
            {
                alert.Acknowledged = true;
                alert.AcknowledgedBy = acknowledgedBy;
                alert.AcknowledgedAt = DateTime.UtcNow;

                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = "SECURITY_ALERT_ACKNOWLEDGED",
                    EntityType = "SecurityAlert",
                    EntityId = alertId,
                    UserId = acknowledgedBy,
                    Description = $"Security alert acknowledged: {alert.Type}",
                    Details = new { AlertType = alert.Type, Severity = alert.Severity }
                });

                return true;
            }

            return false;
        }

        public async Task LogSecurityEventAsync(SecurityAlert alert)
        {
            try
            {
                _activeAlerts[alert.Id] = alert;

                // Add to recent events for pattern analysis
                var eventKey = $"{alert.IpAddress}_{alert.Type}";
                if (!_recentEvents.TryGetValue(eventKey, out var events))
                {
                    events = new List<SecurityEvent>();
                    _recentEvents[eventKey] = events;
                }

                events.Add(new SecurityEvent
                {
                    Timestamp = alert.Timestamp,
                    Type = alert.Type,
                    IpAddress = alert.IpAddress,
                    Severity = alert.Severity
                });

                // Keep only recent events (last 24 hours)
                var cutoff = DateTime.UtcNow.AddHours(-24);
                events.RemoveAll(e => e.Timestamp < cutoff);

                // Update metrics
                await UpdateSecurityMetrics(alert);

                // Check if immediate action is needed
                await ProcessSecurityAlert(alert);

                // Log to audit trail
                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = "SECURITY_EVENT_LOGGED",
                    EntityType = "SecurityAlert",
                    EntityId = alert.Id,
                    Description = $"Security event: {alert.Type}",
                    IpAddress = alert.IpAddress,
                    UserAgent = alert.UserAgent,
                    Details = alert.Details
                });

                _logger.LogWarning($"Security event logged: {alert.Type} from {alert.IpAddress}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error logging security event: {alert.Type}");
            }
        }

        public async Task<bool> IsIpBlockedAsync(string ipAddress)
        {
            if (_blockedIps.TryGetValue(ipAddress, out var blockInfo))
            {
                // Check if block has expired
                if (blockInfo.ExpiresAt.HasValue && blockInfo.ExpiresAt <= DateTime.UtcNow)
                {
                    _blockedIps.TryRemove(ipAddress, out _);
                    return false;
                }

                return blockInfo.IsActive;
            }

            return false;
        }

        public async Task BlockIpAsync(string ipAddress, string reason, TimeSpan? duration = null)
        {
            var blockInfo = new BlockedIpInfo
            {
                IpAddress = ipAddress,
                Reason = reason,
                BlockedAt = DateTime.UtcNow,
                ExpiresAt = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : null,
                IsActive = true
            };

            _blockedIps[ipAddress] = blockInfo;

            await _auditService.LogEventAsync(new AuditEvent
            {
                EventType = "IP_BLOCKED",
                EntityType = "IpAddress",
                EntityId = ipAddress,
                UserId = "SYSTEM",
                Description = $"IP address blocked: {reason}",
                IpAddress = ipAddress,
                Details = new { Reason = reason, Duration = duration?.ToString(), ExpiresAt = blockInfo.ExpiresAt }
            });

            _logger.LogWarning($"IP address blocked: {ipAddress}, Reason: {reason}");

            // Send alert if configured
            if (_securityConfig.ThreatDetection.EnableRealTimeAlerts && 
                !string.IsNullOrEmpty(_securityConfig.ThreatDetection.NotificationEmail))
            {
                await _emailService.SendSystemMaintenanceNotificationAsync(
                    _securityConfig.ThreatDetection.NotificationEmail,
                    "Security Alert: IP Address Blocked",
                    $"IP address {ipAddress} has been blocked due to: {reason}");
            }
        }

        public async Task UnblockIpAsync(string ipAddress)
        {
            if (_blockedIps.TryRemove(ipAddress, out var blockInfo))
            {
                await _auditService.LogEventAsync(new AuditEvent
                {
                    EventType = "IP_UNBLOCKED",
                    EntityType = "IpAddress",
                    EntityId = ipAddress,
                    UserId = "SYSTEM",
                    Description = "IP address unblocked",
                    IpAddress = ipAddress,
                    Details = new { OriginalReason = blockInfo.Reason, BlockedAt = blockInfo.BlockedAt }
                });

                _logger.LogInformation($"IP address unblocked: {ipAddress}");
            }
        }

        public async Task<List<string>> GetBlockedIpsAsync()
        {
            return _blockedIps.Keys.ToList();
        }

        public async Task<Dictionary<string, object>> GetThreatIntelligenceAsync()
        {
            var intelligence = new Dictionary<string, object>();

            // Top threat types
            var threatCounts = _activeAlerts.Values
                .Where(a => a.Timestamp >= DateTime.UtcNow.AddHours(-24))
                .GroupBy(a => a.Type)
                .ToDictionary(g => g.Key, g => g.Count());

            intelligence["TopThreatTypes"] = threatCounts.OrderByDescending(t => t.Value).Take(10);

            // Most active IPs
            var ipCounts = _activeAlerts.Values
                .Where(a => a.Timestamp >= DateTime.UtcNow.AddHours(-24) && !string.IsNullOrEmpty(a.IpAddress))
                .GroupBy(a => a.IpAddress)
                .ToDictionary(g => g.Key, g => g.Count());

            intelligence["MostActiveIPs"] = ipCounts.OrderByDescending(i => i.Value).Take(10);

            // Blocked IPs
            intelligence["BlockedIPs"] = _blockedIps.Values.Where(b => b.IsActive).Select(b => new
            {
                b.IpAddress,
                b.Reason,
                b.BlockedAt,
                b.ExpiresAt
            });

            // Recent patterns
            intelligence["RecentPatterns"] = AnalyzeRecentPatterns();

            // System health
            intelligence["SystemHealth"] = new
            {
                ActiveAlerts = _activeAlerts.Count(a => a.Value.Acknowledged == false),
                BlockedIPs = _blockedIps.Count(b => b.Value.IsActive),
                TotalEvents24h = _activeAlerts.Count(a => a.Value.Timestamp >= DateTime.UtcNow.AddHours(-24)),
                LastUpdated = DateTime.UtcNow
            };

            return intelligence;
        }

        public async Task CleanupOldAlertsAsync()
        {
            var cutoff = DateTime.UtcNow.AddDays(-_securityConfig.Auditing.MaxLogRetentionDays);
            var oldAlerts = _activeAlerts.Values.Where(a => a.Timestamp < cutoff).ToList();

            foreach (var alert in oldAlerts)
            {
                _activeAlerts.TryRemove(alert.Id, out _);
            }

            if (oldAlerts.Any())
            {
                _logger.LogInformation($"Cleaned up {oldAlerts.Count} old security alerts");
            }
        }

        private async Task MonitorSecurityEvents()
        {
            // Monitor for suspicious patterns in recent events
            foreach (var eventGroup in _recentEvents)
            {
                var events = eventGroup.Value;
                if (events.Count >= 10) // Threshold for suspicious activity
                {
                    var recentEvents = events.Where(e => e.Timestamp >= DateTime.UtcNow.AddMinutes(-5)).ToList();
                    if (recentEvents.Count >= 5)
                    {
                        await LogSecurityEventAsync(new SecurityAlert
                        {
                            Type = "SuspiciousActivity",
                            Severity = "High",
                            Message = $"High frequency of security events detected",
                            Source = "SecurityMonitoring",
                            IpAddress = events.FirstOrDefault()?.IpAddress,
                            Details = new Dictionary<string, object>
                            {
                                { "EventCount", recentEvents.Count },
                                { "TimeWindow", "5 minutes" },
                                { "EventTypes", recentEvents.Select(e => e.Type).Distinct().ToList() }
                            }
                        });
                    }
                }
            }
        }

        private async Task AnalyzeThreatPatterns()
        {
            // Analyze patterns in security events to identify coordinated attacks
            var recentAlerts = _activeAlerts.Values
                .Where(a => a.Timestamp >= DateTime.UtcNow.AddHours(-1))
                .ToList();

            // Check for distributed attacks (same type from multiple IPs)
            var threatsByType = recentAlerts
                .GroupBy(a => a.Type)
                .Where(g => g.Count() >= 5)
                .ToList();

            foreach (var threatGroup in threatsByType)
            {
                var uniqueIps = threatGroup.Select(t => t.IpAddress).Distinct().Count();
                if (uniqueIps >= 3)
                {
                    await LogSecurityEventAsync(new SecurityAlert
                    {
                        Type = "DistributedAttack",
                        Severity = "Critical",
                        Message = $"Distributed {threatGroup.Key} attack detected",
                        Source = "ThreatAnalysis",
                        Details = new Dictionary<string, object>
                        {
                            { "ThreatType", threatGroup.Key },
                            { "EventCount", threatGroup.Count() },
                            { "UniqueIPs", uniqueIps },
                            { "TimeWindow", "1 hour" }
                        }
                    });
                }
            }
        }

        private async Task CheckForAnomalies()
        {
            // Check for unusual spikes in activity
            var currentHourEvents = _activeAlerts.Values
                .Count(a => a.Timestamp >= DateTime.UtcNow.AddHours(-1));

            var previousHourEvents = _activeAlerts.Values
                .Count(a => a.Timestamp >= DateTime.UtcNow.AddHours(-2) && a.Timestamp < DateTime.UtcNow.AddHours(-1));

            if (currentHourEvents > previousHourEvents * 3 && currentHourEvents > 50)
            {
                await LogSecurityEventAsync(new SecurityAlert
                {
                    Type = "AnomalousActivity",
                    Severity = "High",
                    Message = "Unusual spike in security events detected",
                    Source = "AnomalyDetection",
                    Details = new Dictionary<string, object>
                    {
                        { "CurrentHourEvents", currentHourEvents },
                        { "PreviousHourEvents", previousHourEvents },
                        { "IncreaseRatio", (double)currentHourEvents / Math.Max(previousHourEvents, 1) }
                    }
                });
            }
        }

        private async Task UpdateSecurityMetrics(SecurityAlert alert)
        {
            var metricsKey = DateTime.UtcNow.ToString("yyyy-MM-dd-HH");
            
            if (!_metrics.TryGetValue(metricsKey, out var metrics))
            {
                metrics = new SecurityMetrics();
                _metrics[metricsKey] = metrics;
            }

            metrics.SecurityThreats++;
            metrics.ThreatsByType[alert.Type] = metrics.ThreatsByType.GetValueOrDefault(alert.Type, 0) + 1;
            
            if (!string.IsNullOrEmpty(alert.IpAddress))
            {
                metrics.BlockedIps[alert.IpAddress] = metrics.BlockedIps.GetValueOrDefault(alert.IpAddress, 0) + 1;
            }

            metrics.LastUpdated = DateTime.UtcNow;
        }

        private async Task ProcessSecurityAlert(SecurityAlert alert)
        {
            // Determine if automatic blocking is needed
            if (_securityConfig.ThreatDetection.BlockThreats && 
                (alert.Severity == "Critical" || alert.Severity == "High"))
            {
                if (!string.IsNullOrEmpty(alert.IpAddress) && !await IsIpBlockedAsync(alert.IpAddress))
                {
                    var blockDuration = alert.Severity == "Critical" ? 
                        TimeSpan.FromHours(24) : TimeSpan.FromHours(1);
                    
                    await BlockIpAsync(alert.IpAddress, $"Automatic block due to {alert.Type}", blockDuration);
                }
            }
        }

        private Dictionary<string, object> AnalyzeRecentPatterns()
        {
            var patterns = new Dictionary<string, object>();

            // Analyze time-based patterns
            var hourlyDistribution = _activeAlerts.Values
                .Where(a => a.Timestamp >= DateTime.UtcNow.AddHours(-24))
                .GroupBy(a => a.Timestamp.Hour)
                .ToDictionary(g => g.Key, g => g.Count());

            patterns["HourlyDistribution"] = hourlyDistribution;

            // Analyze geographic patterns (if IP geolocation is available)
            // This would require integration with a geolocation service

            return patterns;
        }

        private void CollectMetrics(object state)
        {
            try
            {
                // This method runs every minute to collect current metrics
                // In a real implementation, you might collect metrics from various sources
                _logger.LogDebug("Collecting security metrics");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting security metrics");
            }
        }

        private void PerformCleanup(object state)
        {
            try
            {
                // Clean up old alerts and expired blocks
                Task.Run(async () =>
                {
                    await CleanupOldAlertsAsync();
                    
                    // Clean up expired IP blocks
                    var expiredBlocks = _blockedIps.Values
                        .Where(b => b.ExpiresAt.HasValue && b.ExpiresAt <= DateTime.UtcNow)
                        .ToList();

                    foreach (var block in expiredBlocks)
                    {
                        _blockedIps.TryRemove(block.IpAddress, out _);
                    }

                    if (expiredBlocks.Any())
                    {
                        _logger.LogInformation($"Cleaned up {expiredBlocks.Count} expired IP blocks");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing security cleanup");
            }
        }

        public override void Dispose()
        {
            _metricsTimer?.Dispose();
            _cleanupTimer?.Dispose();
            base.Dispose();
        }
    }

    public class BlockedIpInfo
    {
        public string IpAddress { get; set; }
        public string Reason { get; set; }
        public DateTime BlockedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class SecurityEvent
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; }
        public string IpAddress { get; set; }
        public string Severity { get; set; }
    }
}
