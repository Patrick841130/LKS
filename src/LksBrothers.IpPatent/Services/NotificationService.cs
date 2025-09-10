using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LksBrothers.IpPatent.Services
{
    public interface INotificationService
    {
        Task<bool> SendToReviewTeamAsync(ReviewNotification notification);
        Task<bool> SendToUserAsync(string userId, UserNotification notification);
        Task<bool> SendSlackNotificationAsync(string channel, string message);
        Task<bool> SendEmailNotificationAsync(string email, string subject, string body);
        Task<List<ReviewNotification>> GetPendingNotificationsAsync();
    }

    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly IEmailService _emailService;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        // Review team configuration
        private readonly List<ReviewTeamMember> _reviewTeam = new()
        {
            new ReviewTeamMember
            {
                Id = "reviewer-001",
                Name = "IP PATENT Review Lead",
                Email = "ip-review-lead@lksnetwork.io",
                Role = "Lead Reviewer",
                Specialties = new[] { "Patent", "Trademark", "Copyright" },
                NotificationPreferences = new[] { "Email", "Slack" }
            },
            new ReviewTeamMember
            {
                Id = "reviewer-002", 
                Name = "Patent Specialist",
                Email = "patent-specialist@lksnetwork.io",
                Role = "Patent Reviewer",
                Specialties = new[] { "Patent" },
                NotificationPreferences = new[] { "Email", "Slack" }
            },
            new ReviewTeamMember
            {
                Id = "reviewer-003",
                Name = "Trademark Specialist", 
                Email = "trademark-specialist@lksnetwork.io",
                Role = "Trademark Reviewer",
                Specialties = new[] { "Trademark" },
                NotificationPreferences = new[] { "Email" }
            },
            new ReviewTeamMember
            {
                Id = "reviewer-004",
                Name = "Copyright Specialist",
                Email = "copyright-specialist@lksnetwork.io", 
                Role = "Copyright Reviewer",
                Specialties = new[] { "Copyright" },
                NotificationPreferences = new[] { "Email", "Slack" }
            }
        };

        public NotificationService(
            ILogger<NotificationService> logger,
            IEmailService emailService,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _logger = logger;
            _emailService = emailService;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<bool> SendToReviewTeamAsync(ReviewNotification notification)
        {
            try
            {
                _logger.LogInformation($"Sending notification to review team for submission: {notification.SubmissionId}");

                var success = true;

                // Get relevant reviewers based on submission type
                var relevantReviewers = GetRelevantReviewers(notification.SubmissionType);

                foreach (var reviewer in relevantReviewers)
                {
                    try
                    {
                        // Send email notification
                        if (reviewer.NotificationPreferences.Contains("Email"))
                        {
                            var emailSuccess = await SendReviewEmailAsync(reviewer, notification);
                            if (!emailSuccess) success = false;
                        }

                        // Send Slack notification
                        if (reviewer.NotificationPreferences.Contains("Slack"))
                        {
                            var slackSuccess = await SendReviewSlackAsync(reviewer, notification);
                            if (!slackSuccess) success = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error sending notification to reviewer: {reviewer.Id}");
                        success = false;
                    }
                }

                // Send to general review channel
                await SendSlackNotificationAsync(
                    "#ip-patent-reviews",
                    FormatSlackMessage(notification)
                );

                _logger.LogInformation($"Review team notification completed for submission: {notification.SubmissionId}");
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to review team");
                return false;
            }
        }

        public async Task<bool> SendToUserAsync(string userId, UserNotification notification)
        {
            try
            {
                _logger.LogInformation($"Sending notification to user: {userId}");

                // Implementation would send notification to user via their preferred method
                // For now, we'll use email
                var userEmail = await GetUserEmailAsync(userId);
                if (!string.IsNullOrEmpty(userEmail))
                {
                    return await _emailService.SendEmailAsync(
                        userEmail,
                        notification.Subject,
                        notification.Body
                    );
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification to user: {userId}");
                return false;
            }
        }

        public async Task<bool> SendSlackNotificationAsync(string channel, string message)
        {
            try
            {
                var slackWebhookUrl = _configuration.GetValue<string>("Slack:WebhookUrl");
                if (string.IsNullOrEmpty(slackWebhookUrl))
                {
                    _logger.LogWarning("Slack webhook URL not configured");
                    return false;
                }

                var payload = new
                {
                    channel = channel,
                    text = message,
                    username = "LKS IP PATENT Bot",
                    icon_emoji = ":shield:"
                };

                var response = await _httpClient.PostAsJsonAsync(slackWebhookUrl, payload);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Slack notification");
                return false;
            }
        }

        public async Task<bool> SendEmailNotificationAsync(string email, string subject, string body)
        {
            try
            {
                return await _emailService.SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email notification");
                return false;
            }
        }

        public async Task<List<ReviewNotification>> GetPendingNotificationsAsync()
        {
            try
            {
                // Implementation would fetch from database
                return new List<ReviewNotification>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending notifications");
                throw;
            }
        }

        private List<ReviewTeamMember> GetRelevantReviewers(string submissionType)
        {
            return _reviewTeam.Where(r => 
                r.Specialties.Contains(submissionType) || 
                r.Role == "Lead Reviewer"
            ).ToList();
        }

        private async Task<bool> SendReviewEmailAsync(ReviewTeamMember reviewer, ReviewNotification notification)
        {
            var subject = $"New {notification.SubmissionType} Submission for Review - {notification.Title}";
            var body = $@"
                <h2>New IP PATENT Submission for Review</h2>
                <p><strong>Submission ID:</strong> {notification.SubmissionId}</p>
                <p><strong>Type:</strong> {notification.SubmissionType}</p>
                <p><strong>Title:</strong> {notification.Title}</p>
                <p><strong>Priority:</strong> {notification.Priority}</p>
                <p><strong>Review Deadline:</strong> {notification.ReviewDeadline:yyyy-MM-dd HH:mm}</p>
                <p><strong>Message:</strong> {notification.Message}</p>
                
                <h3>Next Steps:</h3>
                <ul>
                    <li>Review the submission within 7 days</li>
                    <li>Approve or reject with detailed notes</li>
                    <li>Approved submissions will be uploaded to 10 mainnets automatically</li>
                </ul>
                
                <p><a href='https://admin.lksnetwork.io/ip-patent/reviews/{notification.SubmissionId}'>Review Submission</a></p>
                
                <hr>
                <p><small>LKS BROTHERS IP PATENT Authority and Rights Blockchain Web3 Mainnet</small></p>
            ";

            return await _emailService.SendEmailAsync(reviewer.Email, subject, body);
        }

        private async Task<bool> SendReviewSlackAsync(ReviewTeamMember reviewer, ReviewNotification notification)
        {
            var message = $@"
🔔 *New {notification.SubmissionType} Submission for Review*

*Submission ID:* {notification.SubmissionId}
*Title:* {notification.Title}
*Priority:* {notification.Priority}
*Review Deadline:* {notification.ReviewDeadline:yyyy-MM-dd HH:mm}

*Assigned to:* {reviewer.Name}

<https://admin.lksnetwork.io/ip-patent/reviews/{notification.SubmissionId}|Review Submission>
            ";

            return await SendSlackNotificationAsync($"@{reviewer.Id}", message);
        }

        private string FormatSlackMessage(ReviewNotification notification)
        {
            return $@"
📋 *New IP PATENT Submission*

*Type:* {notification.SubmissionType}
*Title:* {notification.Title}
*Priority:* {notification.Priority}
*Review Deadline:* {notification.ReviewDeadline:yyyy-MM-dd HH:mm}

Review team has been notified. Submission will be uploaded to 10 mainnets upon approval.

*LKS BROTHERS IP PATENT Authority and Rights Blockchain Web3 Mainnet*
            ";
        }

        private async Task<string> GetUserEmailAsync(string userId)
        {
            // Implementation would fetch user email from database
            return "user@example.com"; // Placeholder
        }
    }

    // Supporting models
    public class ReviewTeamMember
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string[] Specialties { get; set; } = Array.Empty<string>();
        public string[] NotificationPreferences { get; set; } = Array.Empty<string>();
    }

    public class UserNotification
    {
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
