namespace LksBrothers.Monitoring.Models;

public class MetricPoint
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class MetricSeries
{
    public string Name { get; set; } = string.Empty;
    public List<MetricPoint> Points { get; set; } = new();
}

public class Alert
{
    public AlertLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class MonitoringStats
{
    public int TotalMetrics { get; set; }
    public int RecentMetricsCount { get; set; }
    public double MetricsPerMinute { get; set; }
    public DateTime OldestMetric { get; set; }
    public DateTime NewestMetric { get; set; }
    public long MemoryUsageMB { get; set; }
}

public class HealthCheckResult
{
    public bool IsHealthy { get; set; }
    public List<IndividualHealthCheck> Checks { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class IndividualHealthCheck
{
    public string Name { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
}

public class MonitoringOptions
{
    public string SlackWebhookUrl { get; set; } = string.Empty;
    public string SmtpServer { get; set; } = string.Empty;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string PrometheusEndpoint { get; set; } = string.Empty;
    public string InfluxDbUrl { get; set; } = string.Empty;
    public string InfluxDbDatabase { get; set; } = "lks_network";
    public TimeSpan AlertCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan MetricsExportInterval { get; set; } = TimeSpan.FromMinutes(5);
}

public enum AlertLevel
{
    Info,
    Warning,
    Critical
}
