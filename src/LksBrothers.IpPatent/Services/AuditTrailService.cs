using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LksBrothers.IpPatent.Services
{
    public interface IAuditTrailService
    {
        Task LogEventAsync(AuditEvent auditEvent);
        Task<List<AuditEvent>> GetAuditTrailAsync(string submissionId);
        Task<List<AuditEvent>> GetUserAuditTrailAsync(string userId, DateTime? fromDate = null);
        Task<List<AuditEvent>> GetSystemAuditTrailAsync(DateTime fromDate, DateTime toDate);
        Task<AuditReport> GenerateAuditReportAsync(AuditReportRequest request);
    }

    public class AuditTrailService : IAuditTrailService
    {
        private readonly ILogger<AuditTrailService> _logger;
        private readonly IAuditRepository _auditRepository;
        private readonly IRealTimeStatusService _realTimeService;

        public AuditTrailService(
            ILogger<AuditTrailService> logger,
            IAuditRepository auditRepository,
            IRealTimeStatusService realTimeService)
        {
            _logger = logger;
            _auditRepository = auditRepository;
            _realTimeService = realTimeService;
        }

        public async Task LogEventAsync(AuditEvent auditEvent)
        {
            try
            {
                auditEvent.Id = Guid.NewGuid().ToString();
                auditEvent.Timestamp = DateTime.UtcNow;
                auditEvent.IpAddress = GetClientIpAddress();
                auditEvent.UserAgent = GetUserAgent();

                await _auditRepository.SaveAuditEventAsync(auditEvent);

                // Send real-time update for critical events
                if (IsCriticalEvent(auditEvent.EventType))
                {
                    await _realTimeService.BroadcastSystemStatusAsync(
                        $"Audit Event: {auditEvent.EventType} - {auditEvent.Description}"
                    );
                }

                _logger.LogInformation($"Audit event logged: {auditEvent.EventType} - {auditEvent.Description}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit event");
                throw;
            }
        }

        public async Task<List<AuditEvent>> GetAuditTrailAsync(string submissionId)
        {
            try
            {
                return await _auditRepository.GetAuditEventsBySubmissionAsync(submissionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving audit trail for submission: {submissionId}");
                throw;
            }
        }

        public async Task<List<AuditEvent>> GetUserAuditTrailAsync(string userId, DateTime? fromDate = null)
        {
            try
            {
                var startDate = fromDate ?? DateTime.UtcNow.AddDays(-30);
                return await _auditRepository.GetAuditEventsByUserAsync(userId, startDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user audit trail: {userId}");
                throw;
            }
        }

        public async Task<List<AuditEvent>> GetSystemAuditTrailAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                return await _auditRepository.GetAuditEventsByDateRangeAsync(fromDate, toDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system audit trail");
                throw;
            }
        }

        public async Task<AuditReport> GenerateAuditReportAsync(AuditReportRequest request)
        {
            try
            {
                _logger.LogInformation($"Generating audit report: {request.ReportType}");

                var events = await GetEventsForReport(request);
                
                var report = new AuditReport
                {
                    Id = Guid.NewGuid().ToString(),
                    ReportType = request.ReportType,
                    GeneratedBy = request.GeneratedBy,
                    GeneratedAt = DateTime.UtcNow,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    TotalEvents = events.Count,
                    Events = events,
                    Summary = GenerateReportSummary(events, request.ReportType)
                };

                await _auditRepository.SaveAuditReportAsync(report);
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audit report");
                throw;
            }
        }

        private async Task<List<AuditEvent>> GetEventsForReport(AuditReportRequest request)
        {
            return request.ReportType switch
            {
                "UserActivity" => await _auditRepository.GetAuditEventsByUserAsync(request.UserId, request.FromDate),
                "SubmissionActivity" => await _auditRepository.GetAuditEventsBySubmissionAsync(request.SubmissionId),
                "SystemActivity" => await _auditRepository.GetAuditEventsByDateRangeAsync(request.FromDate, request.ToDate),
                "SecurityEvents" => await _auditRepository.GetSecurityEventsAsync(request.FromDate, request.ToDate),
                "ComplianceReport" => await _auditRepository.GetComplianceEventsAsync(request.FromDate, request.ToDate),
                _ => await _auditRepository.GetAuditEventsByDateRangeAsync(request.FromDate, request.ToDate)
            };
        }

        private AuditReportSummary GenerateReportSummary(List<AuditEvent> events, string reportType)
        {
            var summary = new AuditReportSummary
            {
                TotalEvents = events.Count,
                EventsByType = events.GroupBy(e => e.EventType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                EventsByUser = events.Where(e => !string.IsNullOrEmpty(e.UserId))
                    .GroupBy(e => e.UserId)
                    .ToDictionary(g => g.Key, g => g.Count()),
                CriticalEvents = events.Count(e => IsCriticalEvent(e.EventType)),
                SecurityEvents = events.Count(e => IsSecurityEvent(e.EventType)),
                ComplianceEvents = events.Count(e => IsComplianceEvent(e.EventType))
            };

            return summary;
        }

        private bool IsCriticalEvent(string eventType)
        {
            var criticalEvents = new[]
            {
                "SUBMISSION_APPROVED",
                "SUBMISSION_REJECTED", 
                "MAINNET_UPLOAD_FAILED",
                "SECURITY_BREACH",
                "UNAUTHORIZED_ACCESS",
                "DATA_CORRUPTION"
            };
            return criticalEvents.Contains(eventType);
        }

        private bool IsSecurityEvent(string eventType)
        {
            var securityEvents = new[]
            {
                "LOGIN_FAILED",
                "UNAUTHORIZED_ACCESS",
                "PERMISSION_DENIED",
                "SECURITY_BREACH",
                "SUSPICIOUS_ACTIVITY"
            };
            return securityEvents.Contains(eventType);
        }

        private bool IsComplianceEvent(string eventType)
        {
            var complianceEvents = new[]
            {
                "DATA_ACCESS",
                "DATA_EXPORT",
                "GDPR_REQUEST",
                "AUDIT_LOG_ACCESS",
                "COMPLIANCE_VIOLATION"
            };
            return complianceEvents.Contains(eventType);
        }

        private string GetClientIpAddress()
        {
            // Implementation would get actual IP address from HTTP context
            return "127.0.0.1";
        }

        private string GetUserAgent()
        {
            // Implementation would get actual user agent from HTTP context
            return "LKS-IP-PATENT-System/1.0";
        }
    }

    // Supporting models
    public class AuditEvent
    {
        public string Id { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public string Severity { get; set; } = "Info"; // Info, Warning, Error, Critical
        public string Source { get; set; } = string.Empty;
    }

    public class AuditReportRequest
    {
        public string ReportType { get; set; } = string.Empty;
        public string GeneratedBy { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public List<string> EventTypes { get; set; } = new();
    }

    public class AuditReport
    {
        public string Id { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public string GeneratedBy { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalEvents { get; set; }
        public List<AuditEvent> Events { get; set; } = new();
        public AuditReportSummary Summary { get; set; } = new();
    }

    public class AuditReportSummary
    {
        public int TotalEvents { get; set; }
        public Dictionary<string, int> EventsByType { get; set; } = new();
        public Dictionary<string, int> EventsByUser { get; set; } = new();
        public int CriticalEvents { get; set; }
        public int SecurityEvents { get; set; }
        public int ComplianceEvents { get; set; }
    }
}
