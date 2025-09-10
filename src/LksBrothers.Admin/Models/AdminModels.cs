using LksBrothers.Infrastructure.Models;

namespace LksBrothers.Admin.Models;

public class AdminDashboard
{
    public InfrastructureMetrics Metrics { get; set; } = new();
    public List<AlertInfo> Alerts { get; set; } = new();
    public SystemStatus SystemStatus { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class CreateNodeRequest
{
    public string? DataDirectory { get; set; }
    public bool IsValidator { get; set; }
    public string? ValidatorKeyPath { get; set; }
    public string? NetworkId { get; set; }
    public int? Port { get; set; }
    public int? RpcPort { get; set; }
    public List<string>? BootstrapNodes { get; set; }
    public Dictionary<string, object>? CustomSettings { get; set; }
}

public class ScaleNodesRequest
{
    public int TargetCount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class MaintenanceRequest
{
    public string Reason { get; set; } = string.Empty;
    public DateTime? ScheduledStart { get; set; }
    public TimeSpan? EstimatedDuration { get; set; }
}

public class EmergencyStopRequest
{
    public string Reason { get; set; } = string.Empty;
    public string AuthorizedBy { get; set; } = string.Empty;
}

public class SystemHealthReport
{
    public SystemStatus OverallStatus { get; set; }
    public HealthStatus NodeHealth { get; set; }
    public HealthStatus UserLoadHealth { get; set; }
    public HealthStatus ResourceHealth { get; set; }
    public List<AlertInfo> CriticalAlerts { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public enum SystemStatus
{
    Healthy,
    Warning,
    Critical,
    Down,
    Maintenance
}

public enum HealthStatus
{
    Healthy,
    Warning,
    Critical
}
