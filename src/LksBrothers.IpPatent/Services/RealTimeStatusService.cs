using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LksBrothers.IpPatent.Services
{
    public interface IRealTimeStatusService
    {
        Task SendStatusUpdateAsync(string submissionId, string status, object data = null);
        Task SendMainnetProgressAsync(string submissionId, List<MainnetUploadProgress> progress);
        Task SendNotificationAsync(string userId, string message, string type = "info");
        Task BroadcastSystemStatusAsync(string message);
    }

    public class RealTimeStatusService : IRealTimeStatusService
    {
        private readonly IHubContext<StatusHub> _hubContext;
        private readonly ILogger<RealTimeStatusService> _logger;

        public RealTimeStatusService(IHubContext<StatusHub> hubContext, ILogger<RealTimeStatusService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task SendStatusUpdateAsync(string submissionId, string status, object data = null)
        {
            try
            {
                var update = new StatusUpdate
                {
                    SubmissionId = submissionId,
                    Status = status,
                    Timestamp = DateTime.UtcNow,
                    Data = data
                };

                await _hubContext.Clients.All.SendAsync("StatusUpdate", update);
                _logger.LogInformation($"Status update sent for submission: {submissionId} - {status}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending status update for submission: {submissionId}");
            }
        }

        public async Task SendMainnetProgressAsync(string submissionId, List<MainnetUploadProgress> progress)
        {
            try
            {
                var progressUpdate = new MainnetProgressUpdate
                {
                    SubmissionId = submissionId,
                    Progress = progress,
                    Timestamp = DateTime.UtcNow,
                    CompletedCount = progress.Count(p => p.IsCompleted),
                    TotalCount = progress.Count,
                    OverallProgress = progress.Count > 0 ? (progress.Count(p => p.IsCompleted) * 100 / progress.Count) : 0
                };

                await _hubContext.Clients.All.SendAsync("MainnetProgress", progressUpdate);
                _logger.LogInformation($"Mainnet progress sent for submission: {submissionId} - {progressUpdate.OverallProgress}%");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending mainnet progress for submission: {submissionId}");
            }
        }

        public async Task SendNotificationAsync(string userId, string message, string type = "info")
        {
            try
            {
                var notification = new RealTimeNotification
                {
                    UserId = userId,
                    Message = message,
                    Type = type,
                    Timestamp = DateTime.UtcNow,
                    Id = Guid.NewGuid().ToString()
                };

                await _hubContext.Clients.User(userId).SendAsync("Notification", notification);
                _logger.LogInformation($"Notification sent to user: {userId} - {type}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification to user: {userId}");
            }
        }

        public async Task BroadcastSystemStatusAsync(string message)
        {
            try
            {
                var systemStatus = new SystemStatusUpdate
                {
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    Type = "system"
                };

                await _hubContext.Clients.All.SendAsync("SystemStatus", systemStatus);
                _logger.LogInformation($"System status broadcast: {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting system status");
            }
        }
    }

    public class StatusHub : Hub
    {
        private readonly ILogger<StatusHub> _logger;

        public StatusHub(ILogger<StatusHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, "AdminDashboard");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AdminDashboard");
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinSubmissionGroup(string submissionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Submission_{submissionId}");
            _logger.LogInformation($"Client {Context.ConnectionId} joined submission group: {submissionId}");
        }

        public async Task LeaveSubmissionGroup(string submissionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Submission_{submissionId}");
            _logger.LogInformation($"Client {Context.ConnectionId} left submission group: {submissionId}");
        }
    }

    // Supporting models
    public class StatusUpdate
    {
        public string SubmissionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public object Data { get; set; }
    }

    public class MainnetProgressUpdate
    {
        public string SubmissionId { get; set; } = string.Empty;
        public List<MainnetUploadProgress> Progress { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; }
        public int OverallProgress { get; set; }
    }

    public class MainnetUploadProgress
    {
        public string MainnetId { get; set; } = string.Empty;
        public string MainnetName { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public bool IsInProgress { get; set; }
        public string Status { get; set; } = string.Empty;
        public string TransactionHash { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? CompletedTime { get; set; }
    }

    public class RealTimeNotification
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class SystemStatusUpdate
    {
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
