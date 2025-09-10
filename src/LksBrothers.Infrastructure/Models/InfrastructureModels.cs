using System.Diagnostics;

namespace LksBrothers.Infrastructure.Models;

public class NodeInstance
{
    public string Id { get; set; } = string.Empty;
    public NodeConfiguration Configuration { get; set; } = new();
    public NodeStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastHealthCheck { get; set; }
    public int? ProcessId { get; set; }
    public Process? Process { get; set; }
    public NodeMetrics Metrics { get; set; } = new();
}

public class NodeConfiguration
{
    public string DataDirectory { get; set; } = string.Empty;
    public bool IsValidator { get; set; }
    public string ValidatorKeyPath { get; set; } = string.Empty;
    public string NetworkId { get; set; } = "mainnet";
    public int Port { get; set; } = 8080;
    public int RpcPort { get; set; } = 8545;
    public List<string> BootstrapNodes { get; set; } = new();
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

public class NodeMetrics
{
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; } // MB
    public double NetworkThroughput { get; set; } // bytes/sec
    public double TransactionsPerSecond { get; set; }
    public long BlockHeight { get; set; }
    public int PeerCount { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class UserSession
{
    public string UserId { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime LastActivity { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsActive { get; set; }
    public string UserAgent { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class InfrastructureMetrics
{
    public int ActiveUsers { get; set; }
    public int RunningNodes { get; set; }
    public int TotalNodes { get; set; }
    public double AverageNodeCpuUsage { get; set; }
    public double AverageNodeMemoryUsage { get; set; }
    public double NetworkThroughput { get; set; }
    public double TransactionsPerSecond { get; set; }
    public DateTime Timestamp { get; set; }
}

public class AlertInfo
{
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? NodeId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class InfrastructureOptions
{
    public string NodeExecutablePath { get; set; } = "./LksBrothers.Node";
    public string DataDirectory { get; set; } = "./data";
    public string NetworkId { get; set; } = "mainnet";
    public List<string> BootstrapNodes { get; set; } = new();
    public int MinNodes { get; set; } = 1;
    public int MaxNodes { get; set; } = 10;
    public int OptimalUsersPerNode { get; set; } = 100;
    public int MaxUsersPerNode { get; set; } = 200;
    public int MinUsersPerNode { get; set; } = 20;
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan MetricsCollectionInterval { get; set; } = TimeSpan.FromSeconds(30);
}

public enum NodeStatus
{
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed,
    Maintenance
}

public enum AlertType
{
    NodeFailure,
    HighResourceUsage,
    HighUserLoad,
    NetworkIssue,
    SecurityThreat,
    PerformanceDegradation
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
