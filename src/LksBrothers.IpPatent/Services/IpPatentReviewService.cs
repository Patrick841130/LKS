using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LksBrothers.IpPatent.Services
{
    public interface IIpPatentReviewService
    {
        Task<bool> NotifyNewSubmissionAsync(IpPatentSubmission submission);
        Task<List<IpPatentSubmission>> GetPendingReviewsAsync();
        Task<bool> ApproveSubmissionAsync(string submissionId, string reviewerId, string notes);
        Task<bool> RejectSubmissionAsync(string submissionId, string reviewerId, string reason);
        Task<bool> UploadToMainnetsAsync(string submissionId);
        Task<ReviewStats> GetReviewStatsAsync();
    }

    public class IpPatentReviewService : IIpPatentReviewService
    {
        private readonly ILogger<IpPatentReviewService> _logger;
        private readonly INotificationService _notificationService;
        private readonly IMultiMainnetService _multiMainnetService;
        private readonly IIpPatentRepository _repository;
        private readonly IEmailService _emailService;

        public IpPatentReviewService(
            ILogger<IpPatentReviewService> logger,
            INotificationService notificationService,
            IMultiMainnetService multiMainnetService,
            IIpPatentRepository repository,
            IEmailService emailService)
        {
            _logger = logger;
            _notificationService = notificationService;
            _multiMainnetService = multiMainnetService;
            _repository = repository;
            _emailService = emailService;
        }

        public async Task<bool> NotifyNewSubmissionAsync(IpPatentSubmission submission)
        {
            try
            {
                _logger.LogInformation($"Processing new IP PATENT submission: {submission.Id}");

                // Set submission status to pending review
                submission.Status = SubmissionStatus.PendingReview;
                submission.SubmissionDate = DateTime.UtcNow;
                submission.ReviewDeadline = DateTime.UtcNow.AddDays(7); // 1 week review period

                // Save to database
                await _repository.SaveSubmissionAsync(submission);

                // Send notification to review team
                var notification = new ReviewNotification
                {
                    Type = "NewSubmission",
                    SubmissionId = submission.Id,
                    UserId = submission.UserId,
                    Title = submission.Title,
                    SubmissionType = submission.Type,
                    Priority = GetSubmissionPriority(submission),
                    ReviewDeadline = submission.ReviewDeadline,
                    Message = $"New {submission.Type} submission '{submission.Title}' requires review",
                    CreatedAt = DateTime.UtcNow
                };

                await _notificationService.SendToReviewTeamAsync(notification);

                // Send confirmation email to user
                await _emailService.SendSubmissionConfirmationAsync(submission);

                // Log submission for audit trail
                await LogSubmissionEventAsync(submission.Id, "SUBMITTED", $"New {submission.Type} submission received");

                _logger.LogInformation($"Successfully processed submission notification: {submission.Id}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing submission notification: {submission.Id}");
                return false;
            }
        }

        public async Task<List<IpPatentSubmission>> GetPendingReviewsAsync()
        {
            try
            {
                var pendingSubmissions = await _repository.GetSubmissionsByStatusAsync(SubmissionStatus.PendingReview);
                
                // Sort by priority and submission date
                return pendingSubmissions
                    .OrderBy(s => s.Priority)
                    .ThenBy(s => s.SubmissionDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending reviews");
                throw;
            }
        }

        public async Task<bool> ApproveSubmissionAsync(string submissionId, string reviewerId, string notes)
        {
            try
            {
                _logger.LogInformation($"Approving submission: {submissionId} by reviewer: {reviewerId}");

                var submission = await _repository.GetSubmissionAsync(submissionId);
                if (submission == null)
                {
                    _logger.LogWarning($"Submission not found: {submissionId}");
                    return false;
                }

                // Update submission status
                submission.Status = SubmissionStatus.Approved;
                submission.ReviewerId = reviewerId;
                submission.ReviewDate = DateTime.UtcNow;
                submission.ReviewNotes = notes;
                submission.ApprovalDate = DateTime.UtcNow;

                await _repository.UpdateSubmissionAsync(submission);

                // Start multi-mainnet upload process
                await UploadToMainnetsAsync(submissionId);

                // Send approval notification to user
                await _emailService.SendApprovalNotificationAsync(submission);

                // Log approval event
                await LogSubmissionEventAsync(submissionId, "APPROVED", $"Approved by {reviewerId}: {notes}");

                _logger.LogInformation($"Successfully approved submission: {submissionId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving submission: {submissionId}");
                return false;
            }
        }

        public async Task<bool> RejectSubmissionAsync(string submissionId, string reviewerId, string reason)
        {
            try
            {
                _logger.LogInformation($"Rejecting submission: {submissionId} by reviewer: {reviewerId}");

                var submission = await _repository.GetSubmissionAsync(submissionId);
                if (submission == null)
                {
                    _logger.LogWarning($"Submission not found: {submissionId}");
                    return false;
                }

                // Update submission status
                submission.Status = SubmissionStatus.Rejected;
                submission.ReviewerId = reviewerId;
                submission.ReviewDate = DateTime.UtcNow;
                submission.ReviewNotes = reason;
                submission.RejectionReason = reason;

                await _repository.UpdateSubmissionAsync(submission);

                // Send rejection notification to user
                await _emailService.SendRejectionNotificationAsync(submission);

                // Log rejection event
                await LogSubmissionEventAsync(submissionId, "REJECTED", $"Rejected by {reviewerId}: {reason}");

                _logger.LogInformation($"Successfully rejected submission: {submissionId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting submission: {submissionId}");
                return false;
            }
        }

        public async Task<bool> UploadToMainnetsAsync(string submissionId)
        {
            try
            {
                _logger.LogInformation($"Starting multi-mainnet upload for submission: {submissionId}");

                var submission = await _repository.GetSubmissionAsync(submissionId);
                if (submission == null || submission.Status != SubmissionStatus.Approved)
                {
                    _logger.LogWarning($"Submission not approved for mainnet upload: {submissionId}");
                    return false;
                }

                // Update status to uploading
                submission.Status = SubmissionStatus.UploadingToMainnets;
                submission.MainnetUploadStarted = DateTime.UtcNow;
                await _repository.UpdateSubmissionAsync(submission);

                // Upload to all 10 mainnets
                var uploadResults = await _multiMainnetService.UploadToAllMainnetsAsync(new MainnetUploadRequest
                {
                    SubmissionId = submissionId,
                    Title = submission.Title,
                    Description = submission.Description,
                    FileHash = submission.FileHash,
                    SubmissionType = submission.Type,
                    UserId = submission.UserId,
                    ApprovalDate = submission.ApprovalDate,
                    ReviewerId = submission.ReviewerId
                });

                // Update submission with mainnet results
                submission.MainnetUploadResults = uploadResults;
                submission.MainnetUploadCompleted = DateTime.UtcNow;
                
                if (uploadResults.All(r => r.Success))
                {
                    submission.Status = SubmissionStatus.PublishedOnMainnets;
                    await _emailService.SendPublicationNotificationAsync(submission);
                    await LogSubmissionEventAsync(submissionId, "PUBLISHED", "Successfully published on all 10 mainnets");
                }
                else
                {
                    submission.Status = SubmissionStatus.PartiallyPublished;
                    await _emailService.SendPartialPublicationNotificationAsync(submission);
                    await LogSubmissionEventAsync(submissionId, "PARTIAL_PUBLISH", $"Published on {uploadResults.Count(r => r.Success)}/10 mainnets");
                }

                await _repository.UpdateSubmissionAsync(submission);

                _logger.LogInformation($"Completed multi-mainnet upload for submission: {submissionId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading to mainnets: {submissionId}");
                
                // Update submission status to failed
                var submission = await _repository.GetSubmissionAsync(submissionId);
                if (submission != null)
                {
                    submission.Status = SubmissionStatus.UploadFailed;
                    await _repository.UpdateSubmissionAsync(submission);
                    await LogSubmissionEventAsync(submissionId, "UPLOAD_FAILED", ex.Message);
                }
                
                return false;
            }
        }

        public async Task<ReviewStats> GetReviewStatsAsync()
        {
            try
            {
                var stats = new ReviewStats
                {
                    PendingReviews = await _repository.GetSubmissionCountByStatusAsync(SubmissionStatus.PendingReview),
                    ApprovedThisWeek = await _repository.GetSubmissionCountByStatusAndDateAsync(SubmissionStatus.Approved, DateTime.UtcNow.AddDays(-7)),
                    RejectedThisWeek = await _repository.GetSubmissionCountByStatusAndDateAsync(SubmissionStatus.Rejected, DateTime.UtcNow.AddDays(-7)),
                    PublishedOnMainnets = await _repository.GetSubmissionCountByStatusAsync(SubmissionStatus.PublishedOnMainnets),
                    AverageReviewTime = await _repository.GetAverageReviewTimeAsync(),
                    OverdueReviews = await _repository.GetOverdueReviewsCountAsync(),
                    TotalSubmissions = await _repository.GetTotalSubmissionsCountAsync(),
                    LastUpdated = DateTime.UtcNow
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving review stats");
                throw;
            }
        }

        private SubmissionPriority GetSubmissionPriority(IpPatentSubmission submission)
        {
            // Priority logic based on submission type and user tier
            return submission.Type switch
            {
                "Patent" => SubmissionPriority.High,
                "Trademark" => SubmissionPriority.Medium,
                "Copyright" => SubmissionPriority.Low,
                _ => SubmissionPriority.Medium
            };
        }

        private async Task LogSubmissionEventAsync(string submissionId, string eventType, string description)
        {
            var auditLog = new SubmissionAuditLog
            {
                SubmissionId = submissionId,
                EventType = eventType,
                Description = description,
                Timestamp = DateTime.UtcNow,
                Source = "IpPatentReviewService"
            };

            await _repository.SaveAuditLogAsync(auditLog);
        }
    }

    // Supporting models
    public class IpPatentSubmission
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Patent, Trademark, Copyright
        public string FileHash { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public SubmissionStatus Status { get; set; }
        public SubmissionPriority Priority { get; set; }
        public DateTime SubmissionDate { get; set; }
        public DateTime ReviewDeadline { get; set; }
        public string ReviewerId { get; set; } = string.Empty;
        public DateTime? ReviewDate { get; set; }
        public string ReviewNotes { get; set; } = string.Empty;
        public DateTime? ApprovalDate { get; set; }
        public string RejectionReason { get; set; } = string.Empty;
        public DateTime? MainnetUploadStarted { get; set; }
        public DateTime? MainnetUploadCompleted { get; set; }
        public List<MainnetUploadResult> MainnetUploadResults { get; set; } = new();
    }

    public class ReviewNotification
    {
        public string Type { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string SubmissionType { get; set; } = string.Empty;
        public SubmissionPriority Priority { get; set; }
        public DateTime ReviewDeadline { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ReviewStats
    {
        public int PendingReviews { get; set; }
        public int ApprovedThisWeek { get; set; }
        public int RejectedThisWeek { get; set; }
        public int PublishedOnMainnets { get; set; }
        public TimeSpan AverageReviewTime { get; set; }
        public int OverdueReviews { get; set; }
        public int TotalSubmissions { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class SubmissionAuditLog
    {
        public string SubmissionId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public enum SubmissionStatus
    {
        PendingReview,
        UnderReview,
        Approved,
        Rejected,
        UploadingToMainnets,
        PublishedOnMainnets,
        PartiallyPublished,
        UploadFailed
    }

    public enum SubmissionPriority
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
}
