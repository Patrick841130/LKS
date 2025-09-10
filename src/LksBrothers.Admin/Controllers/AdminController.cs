using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LksBrothers.Infrastructure.Services;
using LksBrothers.Infrastructure.Models;
using LksBrothers.Admin.Models;

namespace LksBrothers.Admin.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IInfrastructureManager _infrastructureManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IInfrastructureManager infrastructureManager,
        ILogger<AdminController> logger)
    {
        _infrastructureManager = infrastructureManager;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboard>> GetDashboard()
    {
        try
        {
            var metrics = _infrastructureManager.GetInfrastructureMetrics();
            var alerts = await _infrastructureManager.GetActiveAlertsAsync();

            var dashboard = new AdminDashboard
            {
                Metrics = metrics,
                Alerts = alerts,
                SystemStatus = DetermineSystemStatus(metrics, alerts),
                LastUpdated = DateTime.UtcNow
            };

            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving admin dashboard");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("nodes")]
    public async Task<ActionResult<NodeInstance>> CreateNode([FromBody] CreateNodeRequest request)
    {
        try
        {
            var config = new NodeConfiguration
            {
                DataDirectory = request.DataDirectory ?? Path.Combine("./data", Guid.NewGuid().ToString()),
                IsValidator = request.IsValidator,
                ValidatorKeyPath = request.ValidatorKeyPath ?? "",
                NetworkId = request.NetworkId ?? "mainnet",
                Port = request.Port ?? GetNextAvailablePort(),
                RpcPort = request.RpcPort ?? GetNextAvailableRpcPort(),
                BootstrapNodes = request.BootstrapNodes ?? new List<string>(),
                CustomSettings = request.CustomSettings ?? new Dictionary<string, object>()
            };

            var node = await _infrastructureManager.CreateNodeAsync(config);
            _logger.LogInformation("Node created via admin API: {NodeId}", node.Id);
            
            return Ok(node);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating node");
            return BadRequest($"Failed to create node: {ex.Message}");
        }
    }

    [HttpPost("nodes/scale")]
    public async Task<ActionResult> ScaleNodes([FromBody] ScaleNodesRequest request)
    {
        try
        {
            if (request.TargetCount < 1 || request.TargetCount > 50)
                return BadRequest("Target count must be between 1 and 50");

            var success = await _infrastructureManager.ScaleNodesAsync(request.TargetCount);
            if (success)
            {
                _logger.LogInformation("Nodes scaled to {TargetCount} via admin API", request.TargetCount);
                return Ok(new { message = $"Successfully scaled to {request.TargetCount} nodes" });
            }
            
            return BadRequest("Failed to scale nodes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scaling nodes");
            return BadRequest($"Failed to scale nodes: {ex.Message}");
        }
    }

    [HttpPut("nodes/{nodeId}/config")]
    public async Task<ActionResult> UpdateNodeConfig(string nodeId, [FromBody] NodeConfiguration config)
    {
        try
        {
            var success = await _infrastructureManager.UpdateNodeConfigurationAsync(nodeId, config);
            if (success)
            {
                _logger.LogInformation("Node {NodeId} configuration updated via admin API", nodeId);
                return Ok(new { message = "Node configuration updated successfully" });
            }
            
            return NotFound("Node not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating node configuration for {NodeId}", nodeId);
            return BadRequest($"Failed to update node configuration: {ex.Message}");
        }
    }

    [HttpGet("metrics")]
    public ActionResult<InfrastructureMetrics> GetMetrics()
    {
        try
        {
            var metrics = _infrastructureManager.GetInfrastructureMetrics();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<List<AlertInfo>>> GetAlerts()
    {
        try
        {
            var alerts = await _infrastructureManager.GetActiveAlertsAsync();
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving alerts");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("maintenance")]
    public async Task<ActionResult> EnterMaintenanceMode([FromBody] MaintenanceRequest request)
    {
        try
        {
            // Implement maintenance mode logic
            _logger.LogWarning("System entering maintenance mode: {Reason}", request.Reason);
            
            // Scale down to minimum nodes
            await _infrastructureManager.ScaleNodesAsync(1);
            
            return Ok(new { message = "System entered maintenance mode", reason = request.Reason });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error entering maintenance mode");
            return BadRequest($"Failed to enter maintenance mode: {ex.Message}");
        }
    }

    [HttpPost("emergency-stop")]
    public async Task<ActionResult> EmergencyStop([FromBody] EmergencyStopRequest request)
    {
        try
        {
            _logger.LogCritical("EMERGENCY STOP initiated: {Reason}", request.Reason);
            
            // Scale down to 0 nodes (emergency stop)
            await _infrastructureManager.ScaleNodesAsync(0);
            
            return Ok(new { message = "Emergency stop executed", reason = request.Reason });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during emergency stop");
            return BadRequest($"Failed to execute emergency stop: {ex.Message}");
        }
    }

    [HttpGet("system-health")]
    public async Task<ActionResult<SystemHealthReport>> GetSystemHealth()
    {
        try
        {
            var metrics = _infrastructureManager.GetInfrastructureMetrics();
            var alerts = await _infrastructureManager.GetActiveAlertsAsync();
            
            var health = new SystemHealthReport
            {
                OverallStatus = DetermineSystemStatus(metrics, alerts),
                NodeHealth = CalculateNodeHealth(metrics),
                UserLoadHealth = CalculateUserLoadHealth(metrics),
                ResourceHealth = CalculateResourceHealth(metrics),
                CriticalAlerts = alerts.Where(a => a.Severity == AlertSeverity.Critical).ToList(),
                Recommendations = GenerateRecommendations(metrics, alerts),
                Timestamp = DateTime.UtcNow
            };

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating system health report");
            return StatusCode(500, "Internal server error");
        }
    }

    private SystemStatus DetermineSystemStatus(InfrastructureMetrics metrics, List<AlertInfo> alerts)
    {
        if (alerts.Any(a => a.Severity == AlertSeverity.Critical))
            return SystemStatus.Critical;
        
        if (metrics.RunningNodes == 0)
            return SystemStatus.Down;
        
        if (alerts.Any(a => a.Severity == AlertSeverity.Warning))
            return SystemStatus.Warning;
        
        if (metrics.AverageNodeCpuUsage > 80 || metrics.AverageNodeMemoryUsage > 80)
            return SystemStatus.Warning;
        
        return SystemStatus.Healthy;
    }

    private HealthStatus CalculateNodeHealth(InfrastructureMetrics metrics)
    {
        if (metrics.RunningNodes == 0)
            return HealthStatus.Critical;
        
        var healthyNodeRatio = (double)metrics.RunningNodes / metrics.TotalNodes;
        
        if (healthyNodeRatio >= 0.9)
            return HealthStatus.Healthy;
        else if (healthyNodeRatio >= 0.7)
            return HealthStatus.Warning;
        else
            return HealthStatus.Critical;
    }

    private HealthStatus CalculateUserLoadHealth(InfrastructureMetrics metrics)
    {
        if (metrics.RunningNodes == 0)
            return HealthStatus.Critical;
        
        var usersPerNode = (double)metrics.ActiveUsers / metrics.RunningNodes;
        
        if (usersPerNode <= 100)
            return HealthStatus.Healthy;
        else if (usersPerNode <= 150)
            return HealthStatus.Warning;
        else
            return HealthStatus.Critical;
    }

    private HealthStatus CalculateResourceHealth(InfrastructureMetrics metrics)
    {
        var avgResourceUsage = (metrics.AverageNodeCpuUsage + metrics.AverageNodeMemoryUsage) / 2;
        
        if (avgResourceUsage <= 70)
            return HealthStatus.Healthy;
        else if (avgResourceUsage <= 85)
            return HealthStatus.Warning;
        else
            return HealthStatus.Critical;
    }

    private List<string> GenerateRecommendations(InfrastructureMetrics metrics, List<AlertInfo> alerts)
    {
        var recommendations = new List<string>();
        
        if (metrics.RunningNodes == 0)
            recommendations.Add("URGENT: No nodes are running. Start at least one node immediately.");
        
        if (metrics.ActiveUsers > metrics.RunningNodes * 150)
            recommendations.Add("Consider scaling up nodes to handle increased user load.");
        
        if (metrics.AverageNodeCpuUsage > 85)
            recommendations.Add("High CPU usage detected. Consider scaling up or optimizing node performance.");
        
        if (metrics.AverageNodeMemoryUsage > 85)
            recommendations.Add("High memory usage detected. Consider increasing node memory or scaling up.");
        
        if (alerts.Count(a => a.Severity == AlertSeverity.Critical) > 0)
            recommendations.Add("Critical alerts detected. Immediate attention required.");
        
        return recommendations;
    }

    private int GetNextAvailablePort()
    {
        // Simple port allocation - in production, implement proper port management
        return 8000 + Random.Shared.Next(1000);
    }

    private int GetNextAvailableRpcPort()
    {
        // Simple RPC port allocation
        return 8500 + Random.Shared.Next(1000);
    }
}
