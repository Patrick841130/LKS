using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace LksBrothers.Infrastructure.Services;

public class InfrastructureManager : IInfrastructureManager
{
    private readonly ILogger<InfrastructureManager> _logger;
    private readonly InfrastructureOptions _options;
    private readonly ConcurrentDictionary<string, NodeInstance> _nodes;
    private readonly ConcurrentDictionary<string, UserSession> _activeSessions;
    private readonly Timer _healthCheckTimer;
    private readonly Timer _metricsCollectionTimer;

    public InfrastructureManager(
        ILogger<InfrastructureManager> logger,
        IOptions<InfrastructureOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _nodes = new ConcurrentDictionary<string, NodeInstance>();
        _activeSessions = new ConcurrentDictionary<string, UserSession>();
        
        // Start background monitoring
        _healthCheckTimer = new Timer(PerformHealthChecks, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        _metricsCollectionTimer = new Timer(CollectMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    public async Task<NodeInstance> CreateNodeAsync(NodeConfiguration config)
    {
        var nodeId = Guid.NewGuid().ToString();
        var node = new NodeInstance
        {
            Id = nodeId,
            Configuration = config,
            Status = NodeStatus.Starting,
            CreatedAt = DateTime.UtcNow,
            LastHealthCheck = DateTime.UtcNow,
            Metrics = new NodeMetrics()
        };

        _nodes.TryAdd(nodeId, node);
        
        try
        {
            // Start node process
            await StartNodeProcessAsync(node);
            node.Status = NodeStatus.Running;
            
            _logger.LogInformation("Node {NodeId} created and started successfully", nodeId);
            return node;
        }
        catch (Exception ex)
        {
            node.Status = NodeStatus.Failed;
            _logger.LogError(ex, "Failed to create node {NodeId}", nodeId);
            throw;
        }
    }

    public async Task<bool> ScaleNodesAsync(int targetCount)
    {
        var currentCount = _nodes.Count(n => n.Value.Status == NodeStatus.Running);
        
        if (targetCount > currentCount)
        {
            // Scale up
            var nodesToCreate = targetCount - currentCount;
            var tasks = new List<Task>();
            
            for (int i = 0; i < nodesToCreate; i++)
            {
                var config = CreateDefaultNodeConfiguration();
                tasks.Add(CreateNodeAsync(config));
            }
            
            await Task.WhenAll(tasks);
            _logger.LogInformation("Scaled up to {TargetCount} nodes", targetCount);
        }
        else if (targetCount < currentCount)
        {
            // Scale down
            var nodesToRemove = currentCount - targetCount;
            var nodesToStop = _nodes.Values
                .Where(n => n.Status == NodeStatus.Running)
                .OrderBy(n => n.CreatedAt)
                .Take(nodesToRemove)
                .ToList();
            
            foreach (var node in nodesToStop)
            {
                await StopNodeAsync(node.Id);
            }
            
            _logger.LogInformation("Scaled down to {TargetCount} nodes", targetCount);
        }
        
        return true;
    }

    public async Task<UserSession> RegisterUserSessionAsync(string userId, string connectionId)
    {
        var session = new UserSession
        {
            UserId = userId,
            ConnectionId = connectionId,
            StartTime = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            IsActive = true
        };

        _activeSessions.TryAdd(connectionId, session);
        
        // Auto-scale based on user load
        await CheckAutoScaling();
        
        _logger.LogInformation("User session registered: {UserId} - {ConnectionId}", userId, connectionId);
        return session;
    }

    public async Task UnregisterUserSessionAsync(string connectionId)
    {
        if (_activeSessions.TryRemove(connectionId, out var session))
        {
            session.IsActive = false;
            session.EndTime = DateTime.UtcNow;
            
            // Check if we can scale down
            await CheckAutoScaling();
            
            _logger.LogInformation("User session ended: {UserId} - {ConnectionId}", session.UserId, connectionId);
        }
    }

    public InfrastructureMetrics GetInfrastructureMetrics()
    {
        var activeUsers = _activeSessions.Count(s => s.Value.IsActive);
        var runningNodes = _nodes.Count(n => n.Value.Status == NodeStatus.Running);
        
        return new InfrastructureMetrics
        {
            ActiveUsers = activeUsers,
            RunningNodes = runningNodes,
            TotalNodes = _nodes.Count,
            AverageNodeCpuUsage = _nodes.Values.Average(n => n.Metrics.CpuUsage),
            AverageNodeMemoryUsage = _nodes.Values.Average(n => n.Metrics.MemoryUsage),
            NetworkThroughput = _nodes.Values.Sum(n => n.Metrics.NetworkThroughput),
            TransactionsPerSecond = _nodes.Values.Sum(n => n.Metrics.TransactionsPerSecond),
            Timestamp = DateTime.UtcNow
        };
    }

    public async Task<bool> UpdateNodeConfigurationAsync(string nodeId, NodeConfiguration newConfig)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
            return false;

        try
        {
            // Stop node
            await StopNodeAsync(nodeId);
            
            // Update configuration
            node.Configuration = newConfig;
            
            // Restart with new config
            await StartNodeProcessAsync(node);
            node.Status = NodeStatus.Running;
            
            _logger.LogInformation("Node {NodeId} configuration updated successfully", nodeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update node {NodeId} configuration", nodeId);
            node.Status = NodeStatus.Failed;
            return false;
        }
    }

    public async Task<List<AlertInfo>> GetActiveAlertsAsync()
    {
        var alerts = new List<AlertInfo>();
        
        // Check node health
        foreach (var node in _nodes.Values)
        {
            if (node.Status == NodeStatus.Failed)
            {
                alerts.Add(new AlertInfo
                {
                    Type = AlertType.NodeFailure,
                    Severity = AlertSeverity.Critical,
                    Message = $"Node {node.Id} has failed",
                    Timestamp = DateTime.UtcNow,
                    NodeId = node.Id
                });
            }
            
            if (node.Metrics.CpuUsage > 90)
            {
                alerts.Add(new AlertInfo
                {
                    Type = AlertType.HighResourceUsage,
                    Severity = AlertSeverity.Warning,
                    Message = $"Node {node.Id} CPU usage is {node.Metrics.CpuUsage:F1}%",
                    Timestamp = DateTime.UtcNow,
                    NodeId = node.Id
                });
            }
        }
        
        // Check user load
        var activeUsers = _activeSessions.Count(s => s.Value.IsActive);
        if (activeUsers > _options.MaxUsersPerNode * _nodes.Count)
        {
            alerts.Add(new AlertInfo
            {
                Type = AlertType.HighUserLoad,
                Severity = AlertSeverity.Warning,
                Message = $"High user load: {activeUsers} active users",
                Timestamp = DateTime.UtcNow
            });
        }
        
        return alerts;
    }

    private async Task StartNodeProcessAsync(NodeInstance node)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.NodeExecutablePath,
            Arguments = $"--config {JsonSerializer.Serialize(node.Configuration)}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start node process");

        node.ProcessId = process.Id;
        node.Process = process;
        
        // Wait for node to be ready
        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    private async Task StopNodeAsync(string nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            try
            {
                node.Process?.Kill();
                node.Status = NodeStatus.Stopped;
                _logger.LogInformation("Node {NodeId} stopped", nodeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping node {NodeId}", nodeId);
            }
        }
    }

    private async Task CheckAutoScaling()
    {
        var activeUsers = _activeSessions.Count(s => s.Value.IsActive);
        var runningNodes = _nodes.Count(n => n.Value.Status == NodeStatus.Running);
        var usersPerNode = runningNodes > 0 ? (double)activeUsers / runningNodes : 0;

        // Scale up if needed
        if (usersPerNode > _options.MaxUsersPerNode && runningNodes < _options.MaxNodes)
        {
            var targetNodes = Math.Min(
                (int)Math.Ceiling((double)activeUsers / _options.OptimalUsersPerNode),
                _options.MaxNodes
            );
            
            if (targetNodes > runningNodes)
            {
                await ScaleNodesAsync(targetNodes);
            }
        }
        // Scale down if possible
        else if (usersPerNode < _options.MinUsersPerNode && runningNodes > _options.MinNodes)
        {
            var targetNodes = Math.Max(
                (int)Math.Ceiling((double)activeUsers / _options.OptimalUsersPerNode),
                _options.MinNodes
            );
            
            if (targetNodes < runningNodes)
            {
                await ScaleNodesAsync(targetNodes);
            }
        }
    }

    private void PerformHealthChecks(object? state)
    {
        foreach (var node in _nodes.Values)
        {
            try
            {
                // Check if process is running
                if (node.Process?.HasExited == true)
                {
                    node.Status = NodeStatus.Failed;
                    _logger.LogWarning("Node {NodeId} process has exited", node.Id);
                    continue;
                }

                // Update health check timestamp
                node.LastHealthCheck = DateTime.UtcNow;
                
                // Collect basic metrics
                if (node.Process != null)
                {
                    node.Metrics.CpuUsage = GetProcessCpuUsage(node.Process);
                    node.Metrics.MemoryUsage = node.Process.WorkingSet64 / (1024 * 1024); // MB
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check for node {NodeId}", node.Id);
            }
        }
    }

    private void CollectMetrics(object? state)
    {
        foreach (var node in _nodes.Values.Where(n => n.Status == NodeStatus.Running))
        {
            try
            {
                // Collect detailed metrics (would integrate with actual node APIs)
                node.Metrics.TransactionsPerSecond = GetNodeTransactionRate(node);
                node.Metrics.NetworkThroughput = GetNodeNetworkThroughput(node);
                node.Metrics.LastUpdated = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics for node {NodeId}", node.Id);
            }
        }
    }

    private NodeConfiguration CreateDefaultNodeConfiguration()
    {
        return new NodeConfiguration
        {
            DataDirectory = Path.Combine(_options.DataDirectory, Guid.NewGuid().ToString()),
            IsValidator = false,
            NetworkId = _options.NetworkId,
            Port = GetAvailablePort(),
            BootstrapNodes = _options.BootstrapNodes
        };
    }

    private int GetAvailablePort()
    {
        // Simple port allocation (in production, use proper port management)
        return 8000 + _nodes.Count;
    }

    private double GetProcessCpuUsage(Process process)
    {
        // Simplified CPU usage calculation
        return Random.Shared.NextDouble() * 100; // Replace with actual CPU monitoring
    }

    private double GetNodeTransactionRate(NodeInstance node)
    {
        // Would query actual node metrics
        return Random.Shared.NextDouble() * 1000;
    }

    private double GetNodeNetworkThroughput(NodeInstance node)
    {
        // Would query actual network metrics
        return Random.Shared.NextDouble() * 1000000; // bytes/sec
    }

    public void Dispose()
    {
        _healthCheckTimer?.Dispose();
        _metricsCollectionTimer?.Dispose();
        
        // Stop all nodes
        foreach (var node in _nodes.Values)
        {
            try
            {
                node.Process?.Kill();
            }
            catch { }
        }
    }
}

public interface IInfrastructureManager : IDisposable
{
    Task<NodeInstance> CreateNodeAsync(NodeConfiguration config);
    Task<bool> ScaleNodesAsync(int targetCount);
    Task<UserSession> RegisterUserSessionAsync(string userId, string connectionId);
    Task UnregisterUserSessionAsync(string connectionId);
    InfrastructureMetrics GetInfrastructureMetrics();
    Task<bool> UpdateNodeConfigurationAsync(string nodeId, NodeConfiguration newConfig);
    Task<List<AlertInfo>> GetActiveAlertsAsync();
}
