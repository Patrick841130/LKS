using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LksBrothers.IpPatent.Services;
using LksBrothers.Core.Authentication;

namespace LksBrothers.IpPatent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IpPatentReviewController : ControllerBase
    {
        private readonly IIpPatentReviewService _reviewService;
        private readonly IMultiMainnetService _multiMainnetService;
        private readonly ILogger<IpPatentReviewController> _logger;

        public IpPatentReviewController(
            IIpPatentReviewService reviewService,
            IMultiMainnetService multiMainnetService,
            ILogger<IpPatentReviewController> logger)
        {
            _reviewService = reviewService;
            _multiMainnetService = multiMainnetService;
            _logger = logger;
        }

        /// <summary>
        /// Submit new IP PATENT for review (triggered automatically on upload)
        /// </summary>
        [HttpPost("submit")]
        public async Task<ActionResult<SubmissionResponse>> SubmitForReview([FromBody] SubmissionRequest request)
        {
            try
            {
                var userId = User.GetUserId();
                
                var submission = new IpPatentSubmission
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Title = request.Title,
                    Description = request.Description,
                    Type = request.Type,
                    FileHash = request.FileHash,
                    FileName = request.FileName,
                    Priority = GetSubmissionPriority(request.Type)
                };

                var success = await _reviewService.NotifyNewSubmissionAsync(submission);
                
                if (success)
                {
                    return Ok(new SubmissionResponse
                    {
                        Success = true,
                        SubmissionId = submission.Id,
                        Status = "PendingReview",
                        ReviewDeadline = submission.ReviewDeadline,
                        Message = $"Your {request.Type} submission has been received and will be reviewed within 7 days."
                    });
                }
                else
                {
                    return BadRequest(new { error = "Failed to submit for review" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting IP PATENT for review");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get pending reviews (Admin only)
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = "Admin,Reviewer")]
        public async Task<ActionResult<List<IpPatentSubmission>>> GetPendingReviews()
        {
            try
            {
                var pendingReviews = await _reviewService.GetPendingReviewsAsync();
                return Ok(pendingReviews);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending reviews");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Approve submission (Admin/Reviewer only)
        /// </summary>
        [HttpPost("{submissionId}/approve")]
        [Authorize(Roles = "Admin,Reviewer")]
        public async Task<ActionResult<ReviewResponse>> ApproveSubmission(
            string submissionId, 
            [FromBody] ReviewDecisionRequest request)
        {
            try
            {
                var reviewerId = User.GetUserId();
                var success = await _reviewService.ApproveSubmissionAsync(submissionId, reviewerId, request.Notes);
                
                if (success)
                {
                    return Ok(new ReviewResponse
                    {
                        Success = true,
                        SubmissionId = submissionId,
                        Decision = "Approved",
                        Message = "Submission approved and will be uploaded to 10 mainnets",
                        ReviewerId = reviewerId,
                        ReviewDate = DateTime.UtcNow
                    });
                }
                else
                {
                    return BadRequest(new { error = "Failed to approve submission" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving submission: {submissionId}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Reject submission (Admin/Reviewer only)
        /// </summary>
        [HttpPost("{submissionId}/reject")]
        [Authorize(Roles = "Admin,Reviewer")]
        public async Task<ActionResult<ReviewResponse>> RejectSubmission(
            string submissionId, 
            [FromBody] ReviewDecisionRequest request)
        {
            try
            {
                var reviewerId = User.GetUserId();
                var success = await _reviewService.RejectSubmissionAsync(submissionId, reviewerId, request.Notes);
                
                if (success)
                {
                    return Ok(new ReviewResponse
                    {
                        Success = true,
                        SubmissionId = submissionId,
                        Decision = "Rejected",
                        Message = "Submission has been rejected",
                        ReviewerId = reviewerId,
                        ReviewDate = DateTime.UtcNow,
                        RejectionReason = request.Notes
                    });
                }
                else
                {
                    return BadRequest(new { error = "Failed to reject submission" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting submission: {submissionId}");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get review statistics (Admin only)
        /// </summary>
        [HttpGet("stats")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ReviewStats>> GetReviewStats()
        {
            try
            {
                var stats = await _reviewService.GetReviewStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving review stats");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get available mainnets status
        /// </summary>
        [HttpGet("mainnets")]
        [Authorize(Roles = "Admin,Reviewer")]
        public async Task<ActionResult<List<MainnetInfo>>> GetMainnetsStatus()
        {
            try
            {
                var mainnets = await _multiMainnetService.GetAvailableMainnetsAsync();
                return Ok(mainnets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving mainnets status");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get user's submission status
        /// </summary>
        [HttpGet("my-submissions")]
        public async Task<ActionResult<List<UserSubmissionStatus>>> GetMySubmissions()
        {
            try
            {
                var userId = User.GetUserId();
                // Implementation would fetch user's submissions from repository
                var submissions = new List<UserSubmissionStatus>(); // Placeholder
                return Ok(submissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user submissions");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Manually trigger mainnet upload (Admin only)
        /// </summary>
        [HttpPost("{submissionId}/upload-to-mainnets")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<MainnetUploadResponse>> TriggerMainnetUpload(string submissionId)
        {
            try
            {
                var success = await _reviewService.UploadToMainnetsAsync(submissionId);
                
                if (success)
                {
                    return Ok(new MainnetUploadResponse
                    {
                        Success = true,
                        SubmissionId = submissionId,
                        Message = "Mainnet upload process started",
                        StartTime = DateTime.UtcNow
                    });
                }
                else
                {
                    return BadRequest(new { error = "Failed to start mainnet upload" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error triggering mainnet upload: {submissionId}");
                return BadRequest(new { error = ex.Message });
            }
        }

        private SubmissionPriority GetSubmissionPriority(string type)
        {
            return type switch
            {
                "Patent" => SubmissionPriority.High,
                "Trademark" => SubmissionPriority.Medium,
                "Copyright" => SubmissionPriority.Low,
                _ => SubmissionPriority.Medium
            };
        }
    }

    // Request/Response models
    public class SubmissionRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Patent, Trademark, Copyright
        public string FileHash { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public class SubmissionResponse
    {
        public bool Success { get; set; }
        public string SubmissionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ReviewDeadline { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ReviewDecisionRequest
    {
        public string Notes { get; set; } = string.Empty;
    }

    public class ReviewResponse
    {
        public bool Success { get; set; }
        public string SubmissionId { get; set; } = string.Empty;
        public string Decision { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ReviewerId { get; set; } = string.Empty;
        public DateTime ReviewDate { get; set; }
        public string RejectionReason { get; set; } = string.Empty;
    }

    public class UserSubmissionStatus
    {
        public string SubmissionId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SubmissionDate { get; set; }
        public DateTime? ReviewDate { get; set; }
        public int MainnetsPublished { get; set; }
        public string ReviewNotes { get; set; } = string.Empty;
    }

    public class MainnetUploadResponse
    {
        public bool Success { get; set; }
        public string SubmissionId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
    }
}
