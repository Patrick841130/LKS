using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LksBrothers.IpPatent.Templates;
using LksBrothers.IpPatent.Models;

namespace LksBrothers.IpPatent.Services
{
    public interface IEmailService
    {
        Task<bool> SendSubmissionConfirmationAsync(IpPatentSubmission submission);
        Task<bool> SendReviewTeamNotificationAsync(string reviewerEmail, string reviewerName, IpPatentSubmission submission, string priority = "Normal");
        Task<bool> SendApprovalNotificationAsync(IpPatentSubmission submission, string reviewNotes = "");
        Task<bool> SendRejectionNotificationAsync(IpPatentSubmission submission, string reviewNotes);
        Task<bool> SendBlockchainPublishedNotificationAsync(IpPatentSubmission submission, List<MainnetUploadResult> uploadResults);
        Task<bool> SendSystemMaintenanceNotificationAsync(string userEmail, string userName, DateTime maintenanceStart, DateTime maintenanceEnd, string reason);
        Task<bool> SendSubscriptionReminderAsync(string userEmail, string userName, string subscriptionId, decimal amountDue, DateTime dueDate, int lksCoinsAllocated);
        Task<bool> SendBulkNotificationAsync(List<string> recipients, string subject, string htmlContent);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly IAuditTrailService _auditService;
        private readonly SmtpClient _smtpClient;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IAuditTrailService auditService)
        {
            _configuration = configuration;
            _logger = logger;
            _auditService = auditService;

            // Configure SMTP client
            var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["Email:SmtpUsername"];
            var smtpPassword = _configuration["Email:SmtpPassword"];
            var enableSsl = bool.Parse(_configuration["Email:EnableSsl"] ?? "true");

            _fromEmail = _configuration["Email:FromEmail"] ?? "noreply@lksnetwork.io";
            _fromName = _configuration["Email:FromName"] ?? "LKS BROTHERS IP PATENT Authority";

            _smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30000 // 30 seconds timeout
            };
        }

        public async Task<bool> SendSubmissionConfirmationAsync(IpPatentSubmission submission)
        {
            try
            {
                var userEmail = await GetUserEmailAsync(submission.UserId);
                var userName = await GetUserNameAsync(submission.UserId);

                if (string.IsNullOrEmpty(userEmail))
                {
                    _logger.LogWarning($"No email found for user {submission.UserId}");
                    return false;
                }

                var htmlContent = EmailTemplates.SubmissionConfirmation(
                    userName, 
                    submission.Id, 
                    submission.Title, 
                    submission.Type.ToString(), 
                    submission.SubmissionDate, 
                    submission.ReviewDeadline
                );

                var success = await SendEmailAsync(
                    userEmail, 
                    userName,
                    "IP PATENT Submission Confirmation - " + submission.Id, 
                    htmlContent
                );

                if (success)
                {
                    await _auditService.LogEventAsync(new AuditEvent
                    {
                        EventType = "EMAIL_SENT",
                        EntityType = "Submission",
                        EntityId = submission.Id,
                        UserId = submission.UserId,
                        Description = "Submission confirmation email sent",
                        Details = new { EmailType = "SubmissionConfirmation", Recipient = userEmail }
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send submission confirmation email for {submission.Id}");
                return false;
            }
        }

        public async Task<bool> SendReviewTeamNotificationAsync(string reviewerEmail, string reviewerName, IpPatentSubmission submission, string priority = "Normal")
        {
            try
            {
                var htmlContent = EmailTemplates.ReviewTeamNotification(
                    reviewerName,
                    submission.Id,
                    submission.Title,
                    submission.Type.ToString(),
                    submission.UserId,
                    submission.SubmissionDate,
                    submission.ReviewDeadline,
                    priority
                );

                var success = await SendEmailAsync(
                    reviewerEmail,
                    reviewerName,
                    $"[{priority}] New IP PATENT Review Assignment - {submission.Id}",
                    htmlContent
                );

                if (success)
                {
                    await _auditService.LogEventAsync(new AuditEvent
                    {
                        EventType = "EMAIL_SENT",
                        EntityType = "Submission",
                        EntityId = submission.Id,
                        UserId = "SYSTEM",
                        Description = "Review team notification email sent",
                        Details = new { EmailType = "ReviewTeamNotification", Reviewer = reviewerEmail, Priority = priority }
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send review team notification for {submission.Id}");
                return false;
            }
        }

        public async Task<bool> SendApprovalNotificationAsync(IpPatentSubmission submission, string reviewNotes = "")
        {
            try
            {
                var userEmail = await GetUserEmailAsync(submission.UserId);
                var userName = await GetUserNameAsync(submission.UserId);

                if (string.IsNullOrEmpty(userEmail))
                {
                    _logger.LogWarning($"No email found for user {submission.UserId}");
                    return false;
                }

                var htmlContent = EmailTemplates.ApprovalNotification(
                    userName,
                    submission.Id,
                    submission.Title,
                    submission.Type.ToString(),
                    DateTime.UtcNow,
                    reviewNotes
                );

                var success = await SendEmailAsync(
                    userEmail,
                    userName,
                    "🎉 IP PATENT Approved - " + submission.Id,
                    htmlContent
                );

                if (success)
                {
                    await _auditService.LogEventAsync(new AuditEvent
                    {
                        EventType = "EMAIL_SENT",
                        EntityType = "Submission",
                        EntityId = submission.Id,
                        UserId = submission.UserId,
                        Description = "Approval notification email sent",
                        Details = new { EmailType = "ApprovalNotification", Recipient = userEmail }
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send approval notification for {submission.Id}");
                return false;
            }
        }

        public async Task<bool> SendRejectionNotificationAsync(IpPatentSubmission submission, string reviewNotes)
        {
            try
            {
                var userEmail = await GetUserEmailAsync(submission.UserId);
                var userName = await GetUserNameAsync(submission.UserId);

                if (string.IsNullOrEmpty(userEmail))
                {
                    _logger.LogWarning($"No email found for user {submission.UserId}");
                    return false;
                }

                var htmlContent = EmailTemplates.RejectionNotification(
                    userName,
                    submission.Id,
                    submission.Title,
                    submission.Type.ToString(),
                    DateTime.UtcNow,
                    reviewNotes
                );

                var success = await SendEmailAsync(
                    userEmail,
                    userName,
                    "IP PATENT Review Update - " + submission.Id,
                    htmlContent
                );

                if (success)
                {
                    await _auditService.LogEventAsync(new AuditEvent
                    {
                        EventType = "EMAIL_SENT",
                        EntityType = "Submission",
                        EntityId = submission.Id,
                        UserId = submission.UserId,
                        Description = "Rejection notification email sent",
                        Details = new { EmailType = "RejectionNotification", Recipient = userEmail }
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send rejection notification for {submission.Id}");
                return false;
            }
        }

        public async Task<bool> SendBlockchainPublishedNotificationAsync(IpPatentSubmission submission, List<MainnetUploadResult> uploadResults)
        {
            try
            {
                var userEmail = await GetUserEmailAsync(submission.UserId);
                var userName = await GetUserNameAsync(submission.UserId);

                if (string.IsNullOrEmpty(userEmail))
                {
                    _logger.LogWarning($"No email found for user {submission.UserId}");
                    return false;
                }

                var htmlContent = EmailTemplates.BlockchainPublishedNotification(
                    userName,
                    submission.Id,
                    submission.Title,
                    submission.Type.ToString(),
                    DateTime.UtcNow,
                    uploadResults
                );

                var success = await SendEmailAsync(
                    userEmail,
                    userName,
                    "🚀 Blockchain Registration Complete - " + submission.Id,
                    htmlContent
                );

                if (success)
                {
                    await _auditService.LogEventAsync(new AuditEvent
                    {
                        EventType = "EMAIL_SENT",
                        EntityType = "Submission",
                        EntityId = submission.Id,
                        UserId = submission.UserId,
                        Description = "Blockchain published notification email sent",
                        Details = new { 
                            EmailType = "BlockchainPublishedNotification", 
                            Recipient = userEmail,
                            SuccessfulUploads = uploadResults.Count(r => r.Success),
                            TotalUploads = uploadResults.Count
                        }
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send blockchain published notification for {submission.Id}");
                return false;
            }
        }

        public async Task<bool> SendSystemMaintenanceNotificationAsync(string userEmail, string userName, DateTime maintenanceStart, DateTime maintenanceEnd, string reason)
        {
            try
            {
                var htmlContent = EmailTemplates.SystemMaintenanceNotification(
                    userName,
                    maintenanceStart,
                    maintenanceEnd,
                    reason
                );

                var success = await SendEmailAsync(
                    userEmail,
                    userName,
                    "Scheduled System Maintenance - LKS BROTHERS IP PATENT",
                    htmlContent
                );

                if (success)
                {
                    await _auditService.LogEventAsync(new AuditEvent
                    {
                        EventType = "EMAIL_SENT",
                        EntityType = "System",
                        EntityId = "MAINTENANCE",
                        UserId = "SYSTEM",
                        Description = "System maintenance notification email sent",
                        Details = new { EmailType = "SystemMaintenance", Recipient = userEmail }
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send system maintenance notification to {userEmail}");
                return false;
            }
        }

        public async Task<bool> SendSubscriptionReminderAsync(string userEmail, string userName, string subscriptionId, decimal amountDue, DateTime dueDate, int lksCoinsAllocated)
        {
            try
            {
                var htmlContent = EmailTemplates.SubscriptionReminderNotification(
                    userName,
                    subscriptionId,
                    amountDue,
                    dueDate,
                    lksCoinsAllocated
                );

                var success = await SendEmailAsync(
                    userEmail,
                    userName,
                    "IP PATENT Subscription Payment Reminder",
                    htmlContent
                );

                if (success)
                {
                    await _auditService.LogEventAsync(new AuditEvent
                    {
                        EventType = "EMAIL_SENT",
                        EntityType = "Subscription",
                        EntityId = subscriptionId,
                        UserId = "SYSTEM",
                        Description = "Subscription reminder email sent",
                        Details = new { EmailType = "SubscriptionReminder", Recipient = userEmail, AmountDue = amountDue }
                    });
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send subscription reminder to {userEmail}");
                return false;
            }
        }

        public async Task<bool> SendBulkNotificationAsync(List<string> recipients, string subject, string htmlContent)
        {
            var successCount = 0;
            var tasks = new List<Task<bool>>();

            foreach (var recipient in recipients)
            {
                tasks.Add(SendEmailAsync(recipient, "", subject, htmlContent));
            }

            var results = await Task.WhenAll(tasks);
            successCount = results.Count(r => r);

            _logger.LogInformation($"Bulk email sent: {successCount}/{recipients.Count} successful");

            await _auditService.LogEventAsync(new AuditEvent
            {
                EventType = "BULK_EMAIL_SENT",
                EntityType = "System",
                EntityId = "BULK_NOTIFICATION",
                UserId = "SYSTEM",
                Description = $"Bulk email notification sent to {recipients.Count} recipients",
                Details = new { 
                    EmailType = "BulkNotification", 
                    TotalRecipients = recipients.Count,
                    SuccessfulSends = successCount,
                    Subject = subject
                }
            });

            return successCount > 0;
        }

        private async Task<bool> SendEmailAsync(string toEmail, string toName, string subject, string htmlContent)
        {
            try
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    Body = htmlContent,
                    IsBodyHtml = true,
                    Priority = MailPriority.Normal
                };

                mailMessage.To.Add(new MailAddress(toEmail, toName));

                // Add headers for better deliverability
                mailMessage.Headers.Add("X-Mailer", "LKS BROTHERS IP PATENT Authority");
                mailMessage.Headers.Add("X-Priority", "3");
                mailMessage.Headers.Add("X-MSMail-Priority", "Normal");

                await _smtpClient.SendMailAsync(mailMessage);
                
                _logger.LogInformation($"Email sent successfully to {toEmail}: {subject}");
                return true;
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, $"SMTP error sending email to {toEmail}: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error sending email to {toEmail}: {ex.Message}");
                return false;
            }
        }

        private async Task<string> GetUserEmailAsync(string userId)
        {
            // This would typically query your user database
            // For now, return a placeholder implementation
            try
            {
                // TODO: Implement actual user lookup from database
                // var user = await _userRepository.GetByIdAsync(userId);
                // return user?.Email;
                
                return $"user-{userId}@example.com"; // Placeholder
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get email for user {userId}");
                return null;
            }
        }

        private async Task<string> GetUserNameAsync(string userId)
        {
            // This would typically query your user database
            // For now, return a placeholder implementation
            try
            {
                // TODO: Implement actual user lookup from database
                // var user = await _userRepository.GetByIdAsync(userId);
                // return user?.FullName ?? user?.Username;
                
                return $"User {userId}"; // Placeholder
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get name for user {userId}");
                return "Valued User";
            }
        }

        public void Dispose()
        {
            _smtpClient?.Dispose();
        }
    }
}
