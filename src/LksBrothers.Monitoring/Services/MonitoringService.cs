using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Net.Http;

namespace LksBrothers.Monitoring.Services;

public class MonitoringService : IMonitoringService
{
    private readonly ILogger<MonitoringService> _logger;
    private readonly MonitoringOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, MetricSeries> _metrics;
    private readonly Timer _alertCheckTimer;
    private readonly Timer _metricsExportTimer;

    public MonitoringService(
        ILogger<MonitoringService> logger,
        IOptions<MonitoringOptions> options,
        HttpClient httpClient)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClient;
        _metrics = new ConcurrentDictionary<string, MetricSeries>();
        
        _alertCheckTimer = new Timer(CheckAlerts, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        _metricsExportTimer = new Timer(ExportMetrics, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    public void RecordMetric(string name, double value, Dictionary<string, string>? tags = null)
    {
        var metricKey = GenerateMetricKey(name, tags);
        var metric = new MetricPoint
        {
            Name = name,
            Value = value,
            Tags = tags ?? new Dictionary<string, string>(),
            Timestamp = DateTime.UtcNow
        };

        _metrics.AddOrUpdate(metricKey, 
            new MetricSeries { Name = name, Points = new List<MetricPoint> { metric } },
            (key, existing) =>
            {
                existing.Points.Add(metric);
                // Keep only last 1000 points
                if (existing.Points.Count > 1000)
                {
                    existing.Points.RemoveRange(0, existing.Points.Count - 1000);
                }
                return existing;
            });
    }

    public void IncrementCounter(string name, Dictionary<string, string>? tags = null)
    {
        RecordMetric($"{name}_total", 1, tags);
    }

    public void RecordHistogram(string name, double value, Dictionary<string, string>? tags = null)
    {
        RecordMetric($"{name}_histogram", value, tags);
        
        // Also record count and sum for histogram
        RecordMetric($"{name}_count", 1, tags);
        RecordMetric($"{name}_sum", value, tags);
    }

    public void SetGauge(string name, double value, Dictionary<string, string>? tags = null)
    {
        RecordMetric($"{name}_gauge", value, tags);
    }

    public async Task<List<MetricPoint>> QueryMetricsAsync(string name, DateTime? start = null, DateTime? end = null)
    {
        var results = new List<MetricPoint>();
        var startTime = start ?? DateTime.UtcNow.AddHours(-1);
        var endTime = end ?? DateTime.UtcNow;

        foreach (var series in _metrics.Values.Where(s => s.Name == name))
        {
            var filteredPoints = series.Points
                .Where(p => p.Timestamp >= startTime && p.Timestamp <= endTime)
                .OrderBy(p => p.Timestamp)
                .ToList();
            
            results.AddRange(filteredPoints);
        }

        return results;
    }

    public async Task SendAlertAsync(AlertLevel level, string message, Dictionary<string, object>? metadata = null)
    {
        var alert = new Alert
        {
            Level = level,
            Message = message,
            Metadata = metadata ?? new Dictionary<string, object>(),
            Timestamp = DateTime.UtcNow,
            Source = "LKS-Network"
        };

        _logger.Log(GetLogLevel(level), "ALERT [{Level}]: {Message}", level, message);

        // Send to external alerting systems
        await SendToSlackAsync(alert);
        await SendToEmailAsync(alert);
        await SendToWebhookAsync(alert);
    }

    public MonitoringStats GetStats()
    {
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);
        
        var recentMetrics = _metrics.Values
            .SelectMany(s => s.Points)
            .Where(p => p.Timestamp >= oneHourAgo)
            .ToList();

        return new MonitoringStats
        {
            TotalMetrics = _metrics.Count,
            RecentMetricsCount = recentMetrics.Count,
            MetricsPerMinute = recentMetrics.Count / 60.0,
            OldestMetric = _metrics.Values.SelectMany(s => s.Points).Min(p => p.Timestamp),
            NewestMetric = _metrics.Values.SelectMany(s => s.Points).Max(p => p.Timestamp),
            MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024)
        };
    }

    public async Task<HealthCheckResult> PerformHealthCheckAsync()
    {
        var checks = new List<IndividualHealthCheck>();
        
        // Check metrics collection
        checks.Add(await CheckMetricsCollection());
        
        // Check external systems
        checks.Add(await CheckSlackConnection());
        checks.Add(await CheckEmailService());
        
        // Check memory usage
        checks.Add(CheckMemoryUsage());
        
        var overallHealthy = checks.All(c => c.IsHealthy);
        
        return new HealthCheckResult
        {
            IsHealthy = overallHealthy,
            Checks = checks,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<IndividualHealthCheck> CheckMetricsCollection()
    {
        try
        {
            var recentMetrics = _metrics.Values
                .SelectMany(s => s.Points)
                .Where(p => p.Timestamp >= DateTime.UtcNow.AddMinutes(-5))
                .Count();

            var isHealthy = recentMetrics > 0;
            
            return new IndividualHealthCheck
            {
                Name = "MetricsCollection",
                IsHealthy = isHealthy,
                Message = isHealthy ? "Metrics are being collected" : "No recent metrics collected",
                Details = new Dictionary<string, object> { ["RecentMetricsCount"] = recentMetrics }
            };
        }
        catch (Exception ex)
        {
            return new IndividualHealthCheck
            {
                Name = "MetricsCollection",
                IsHealthy = false,
                Message = $"Error checking metrics: {ex.Message}"
            };
        }
    }

    private async Task<IndividualHealthCheck> CheckSlackConnection()
    {
        if (string.IsNullOrEmpty(_options.SlackWebhookUrl))
        {
            return new IndividualHealthCheck
            {
                Name = "SlackConnection",
                IsHealthy = true,
                Message = "Slack not configured"
            };
        }

        try
        {
            // Simple connectivity test
            var response = await _httpClient.GetAsync(_options.SlackWebhookUrl.Replace("/services/", "/api/"));
            return new IndividualHealthCheck
            {
                Name = "SlackConnection",
                IsHealthy = true,
                Message = "Slack connection OK"
            };
        }
        catch (Exception ex)
        {
            return new IndividualHealthCheck
            {
                Name = "SlackConnection",
                IsHealthy = false,
                Message = $"Slack connection failed: {ex.Message}"
            };
        }
    }

    private async Task<IndividualHealthCheck> CheckEmailService()
    {
        return new IndividualHealthCheck
        {
            Name = "EmailService",
            IsHealthy = !string.IsNullOrEmpty(_options.SmtpServer),
            Message = string.IsNullOrEmpty(_options.SmtpServer) ? "Email not configured" : "Email service configured"
        };
    }

    private IndividualHealthCheck CheckMemoryUsage()
    {
        var memoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
        var isHealthy = memoryMB < 500; // 500MB threshold
        
        return new IndividualHealthCheck
        {
            Name = "MemoryUsage",
            IsHealthy = isHealthy,
            Message = $"Memory usage: {memoryMB}MB",
            Details = new Dictionary<string, object> { ["MemoryMB"] = memoryMB }
        };
    }

    private void CheckAlerts(object? state)
    {
        try
        {
            // Check for high error rates
            var errorMetrics = _metrics.Values
                .Where(s => s.Name.Contains("error"))
                .SelectMany(s => s.Points)
                .Where(p => p.Timestamp >= DateTime.UtcNow.AddMinutes(-5))
                .ToList();

            if (errorMetrics.Count > 10)
            {
                _ = SendAlertAsync(AlertLevel.Warning, 
                    $"High error rate detected: {errorMetrics.Count} errors in last 5 minutes");
            }

            // Check for memory usage
            var memoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
            if (memoryMB > 1000)
            {
                _ = SendAlertAsync(AlertLevel.Critical, 
                    $"High memory usage: {memoryMB}MB",
                    new Dictionary<string, object> { ["MemoryMB"] = memoryMB });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during alert checking");
        }
    }

    private void ExportMetrics(object? state)
    {
        try
        {
            if (!string.IsNullOrEmpty(_options.PrometheusEndpoint))
            {
                _ = ExportToPrometheusAsync();
            }
            
            if (!string.IsNullOrEmpty(_options.InfluxDbUrl))
            {
                _ = ExportToInfluxDbAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during metrics export");
        }
    }

    private async Task ExportToPrometheusAsync()
    {
        try
        {
            var prometheusFormat = GeneratePrometheusFormat();
            await _httpClient.PostAsync(_options.PrometheusEndpoint, 
                new StringContent(prometheusFormat, System.Text.Encoding.UTF8, "text/plain"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to Prometheus");
        }
    }

    private async Task ExportToInfluxDbAsync()
    {
        try
        {
            var influxFormat = GenerateInfluxDbFormat();
            await _httpClient.PostAsync($"{_options.InfluxDbUrl}/write?db={_options.InfluxDbDatabase}",
                new StringContent(influxFormat, System.Text.Encoding.UTF8, "text/plain"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to InfluxDB");
        }
    }

    private string GeneratePrometheusFormat()
    {
        var lines = new List<string>();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        foreach (var series in _metrics.Values)
        {
            var latestPoint = series.Points.LastOrDefault();
            if (latestPoint != null)
            {
                var tags = string.Join(",", latestPoint.Tags.Select(t => $"{t.Key}=\"{t.Value}\""));
                var tagsStr = tags.Length > 0 ? $"{{{tags}}}" : "";
                lines.Add($"{series.Name}{tagsStr} {latestPoint.Value} {now}");
            }
        }
        
        return string.Join("\n", lines);
    }

    private string GenerateInfluxDbFormat()
    {
        var lines = new List<string>();
        
        foreach (var series in _metrics.Values)
        {
            var latestPoint = series.Points.LastOrDefault();
            if (latestPoint != null)
            {
                var tags = string.Join(",", latestPoint.Tags.Select(t => $"{t.Key}={t.Value}"));
                var tagsStr = tags.Length > 0 ? $",{tags}" : "";
                var timestamp = ((DateTimeOffset)latestPoint.Timestamp).ToUnixTimeNanoseconds();
                lines.Add($"{series.Name}{tagsStr} value={latestPoint.Value} {timestamp}");
            }
        }
        
        return string.Join("\n", lines);
    }

    private async Task SendToSlackAsync(Alert alert)
    {
        if (string.IsNullOrEmpty(_options.SlackWebhookUrl))
            return;

        try
        {
            var payload = new
            {
                text = $"🚨 LKS Network Alert [{alert.Level}]",
                attachments = new[]
                {
                    new
                    {
                        color = GetSlackColor(alert.Level),
                        fields = new[]
                        {
                            new { title = "Message", value = alert.Message, @short = false },
                            new { title = "Time", value = alert.Timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC"), @short = true },
                            new { title = "Source", value = alert.Source, @short = true }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            await _httpClient.PostAsync(_options.SlackWebhookUrl,
                new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack alert");
        }
    }

    private async Task SendToEmailAsync(Alert alert)
    {
        if (string.IsNullOrEmpty(_options.SmtpServer) || alert.Level < AlertLevel.Critical)
            return;

        try
        {
            // Email implementation would go here
            _logger.LogInformation("Email alert would be sent: {Message}", alert.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email alert");
        }
    }

    private async Task SendToWebhookAsync(Alert alert)
    {
        if (string.IsNullOrEmpty(_options.WebhookUrl))
            return;

        try
        {
            var json = JsonSerializer.Serialize(alert);
            await _httpClient.PostAsync(_options.WebhookUrl,
                new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send webhook alert");
        }
    }

    private string GenerateMetricKey(string name, Dictionary<string, string>? tags)
    {
        if (tags == null || tags.Count == 0)
            return name;
        
        var sortedTags = tags.OrderBy(t => t.Key).Select(t => $"{t.Key}:{t.Value}");
        return $"{name}#{string.Join(",", sortedTags)}";
    }

    private LogLevel GetLogLevel(AlertLevel alertLevel)
    {
        return alertLevel switch
        {
            AlertLevel.Info => LogLevel.Information,
            AlertLevel.Warning => LogLevel.Warning,
            AlertLevel.Critical => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }

    private string GetSlackColor(AlertLevel level)
    {
        return level switch
        {
            AlertLevel.Info => "good",
            AlertLevel.Warning => "warning",
            AlertLevel.Critical => "danger",
            _ => "good"
        };
    }

    public void Dispose()
    {
        _alertCheckTimer?.Dispose();
        _metricsExportTimer?.Dispose();
        _httpClient?.Dispose();
    }
}

public interface IMonitoringService : IDisposable
{
    void RecordMetric(string name, double value, Dictionary<string, string>? tags = null);
    void IncrementCounter(string name, Dictionary<string, string>? tags = null);
    void RecordHistogram(string name, double value, Dictionary<string, string>? tags = null);
    void SetGauge(string name, double value, Dictionary<string, string>? tags = null);
    Task<List<MetricPoint>> QueryMetricsAsync(string name, DateTime? start = null, DateTime? end = null);
    Task SendAlertAsync(AlertLevel level, string message, Dictionary<string, object>? metadata = null);
    MonitoringStats GetStats();
    Task<HealthCheckResult> PerformHealthCheckAsync();
}
