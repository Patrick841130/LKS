using System;
using System.Collections.Generic;
using System.Text;

namespace LksBrothers.IpPatent.Templates
{
    public static class EmailTemplates
    {
        private static readonly string BaseTemplate = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name="viewport" content=""width=device-width, initial-scale=1.0"">
    <title>LKS BROTHERS IP PATENT Authority</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 0; background-color: #f8fafc; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px 20px; text-align: center; }}
        .header h1 {{ color: #ffffff; margin: 0; font-size: 28px; font-weight: bold; }}
        .header p {{ color: #e2e8f0; margin: 5px 0 0 0; font-size: 14px; }}
        .content {{ padding: 40px 30px; }}
        .status-badge {{ display: inline-block; padding: 8px 16px; border-radius: 20px; font-size: 12px; font-weight: bold; text-transform: uppercase; margin: 10px 0; }}
        .status-pending {{ background-color: #fef3c7; color: #92400e; }}
        .status-approved {{ background-color: #d1fae5; color: #065f46; }}
        .status-rejected {{ background-color: #fee2e2; color: #991b1b; }}
        .status-published {{ background-color: #e9d5ff; color: #6b21a8; }}
        .info-box {{ background-color: #f8fafc; border-left: 4px solid #667eea; padding: 20px; margin: 20px 0; }}
        .button {{ display: inline-block; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; margin: 20px 0; }}
        .footer {{ background-color: #1e293b; color: #94a3b8; padding: 30px; text-align: center; font-size: 12px; }}
        .footer a {{ color: #667eea; text-decoration: none; }}
        .mainnet-list {{ background-color: #f1f5f9; padding: 15px; border-radius: 8px; margin: 15px 0; }}
        .mainnet-item {{ display: flex; justify-content: space-between; padding: 5px 0; }}
        .success {{ color: #059669; }}
        .failed {{ color: #dc2626; }}
        .progress-bar {{ background-color: #e5e7eb; height: 8px; border-radius: 4px; margin: 10px 0; }}
        .progress-fill {{ background: linear-gradient(90deg, #667eea 0%, #764ba2 100%); height: 100%; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>LKS BROTHERS</h1>
            <p>IP PATENT Authority & Rights Blockchain</p>
        </div>
        <div class=""content"">
            {0}
        </div>
        <div class=""footer"">
            <p><strong>LKS BROTHERS IP PATENT Authority</strong></p>
            <p>Securing intellectual property rights on the blockchain</p>
            <p>
                <a href=""https://lksnetwork.io"">Website</a> | 
                <a href=""https://lksnetwork.io/support"">Support</a> | 
                <a href=""https://lksnetwork.io/dashboard"">Dashboard</a>
            </p>
            <p style=""margin-top: 20px; color: #64748b;"">
                This email was sent from an automated system. Please do not reply directly to this email.
                <br>For support, contact us at <a href=""mailto:support@lksnetwork.io"">support@lksnetwork.io</a>
            </p>
        </div>
    </div>
</body>
</html>";

        public static string SubmissionConfirmation(string userName, string submissionId, string title, string type, DateTime submissionDate, DateTime reviewDeadline)
        {
            var content = $@"
                <h2>Submission Confirmation</h2>
                <p>Dear {userName},</p>
                <p>Thank you for submitting your intellectual property application to the LKS BROTHERS IP PATENT Authority. We have successfully received your submission and it is now in our review queue.</p>
                
                <div class=""info-box"">
                    <h3>Submission Details</h3>
                    <p><strong>Submission ID:</strong> {submissionId}</p>
                    <p><strong>Title:</strong> {title}</p>
                    <p><strong>Type:</strong> {type}</p>
                    <p><strong>Submitted:</strong> {submissionDate:MMMM dd, yyyy 'at' HH:mm} UTC</p>
                    <p><strong>Review Deadline:</strong> {reviewDeadline:MMMM dd, yyyy}</p>
                </div>

                <span class=""status-badge status-pending"">Pending Review</span>

                <h3>What Happens Next?</h3>
                <ul>
                    <li><strong>Review Process:</strong> Our expert team will review your submission within 7 business days</li>
                    <li><strong>Blockchain Registration:</strong> Upon approval, your IP will be registered across 10 LKS BROTHERS mainnets</li>
                    <li><strong>Certificate Generation:</strong> You'll receive a blockchain-verified certificate of registration</li>
                    <li><strong>Zero Fees:</strong> All blockchain transactions are sponsored by the LKS Foundation</li>
                </ul>

                <a href=""https://lksnetwork.io/dashboard/submissions/{submissionId}"" class=""button"">Track Your Submission</a>

                <p>You will receive email notifications at each stage of the review process. If you have any questions, please don't hesitate to contact our support team.</p>
                
                <p>Best regards,<br><strong>LKS BROTHERS IP PATENT Authority Team</strong></p>";

            return string.Format(BaseTemplate, content);
        }

        public static string ReviewTeamNotification(string reviewerName, string submissionId, string title, string type, string userId, DateTime submissionDate, DateTime reviewDeadline, string priority = "Normal")
        {
            var content = $@"
                <h2>New IP PATENT Submission for Review</h2>
                <p>Dear {reviewerName},</p>
                <p>A new intellectual property submission has been assigned to you for review. Please review the details below and take appropriate action within the specified deadline.</p>
                
                <div class=""info-box"">
                    <h3>Submission Details</h3>
                    <p><strong>Submission ID:</strong> {submissionId}</p>
                    <p><strong>Title:</strong> {title}</p>
                    <p><strong>Type:</strong> {type}</p>
                    <p><strong>Submitter:</strong> {userId}</p>
                    <p><strong>Submitted:</strong> {submissionDate:MMMM dd, yyyy 'at' HH:mm} UTC</p>
                    <p><strong>Review Deadline:</strong> {reviewDeadline:MMMM dd, yyyy}</p>
                    <p><strong>Priority:</strong> <span style=""color: {(priority == "High" ? "#dc2626" : priority == "Medium" ? "#d97706" : "#059669")}"">{priority}</span></p>
                </div>

                <span class=""status-badge status-pending"">Awaiting Review</span>

                <h3>Review Guidelines</h3>
                <ul>
                    <li>Verify originality and uniqueness of the intellectual property</li>
                    <li>Check compliance with LKS BROTHERS IP PATENT standards</li>
                    <li>Ensure all required documentation is complete</li>
                    <li>Validate technical specifications and claims</li>
                </ul>

                <a href=""https://lksnetwork.io/admin/review/{submissionId}"" class=""button"">Review Submission</a>

                <p><strong>Important:</strong> Please complete your review before the deadline to maintain our service level agreements.</p>
                
                <p>Best regards,<br><strong>LKS BROTHERS IP PATENT Authority System</strong></p>";

            return string.Format(BaseTemplate, content);
        }

        public static string ApprovalNotification(string userName, string submissionId, string title, string type, DateTime approvalDate, string reviewNotes = "")
        {
            var content = $@"
                <h2>🎉 Submission Approved!</h2>
                <p>Dear {userName},</p>
                <p>Congratulations! Your intellectual property submission has been approved by our review team and is now being registered across the LKS BROTHERS blockchain network.</p>
                
                <div class=""info-box"">
                    <h3>Approval Details</h3>
                    <p><strong>Submission ID:</strong> {submissionId}</p>
                    <p><strong>Title:</strong> {title}</p>
                    <p><strong>Type:</strong> {type}</p>
                    <p><strong>Approved:</strong> {approvalDate:MMMM dd, yyyy 'at' HH:mm} UTC</p>
                </div>

                <span class=""status-badge status-approved"">Approved</span>

                {(string.IsNullOrEmpty(reviewNotes) ? "" : $@"
                <h3>Reviewer Notes</h3>
                <div class=""info-box"">
                    <p>{reviewNotes}</p>
                </div>")}

                <h3>Next Steps</h3>
                <ul>
                    <li><strong>Blockchain Registration:</strong> Your IP is being uploaded to 10 LKS BROTHERS mainnets</li>
                    <li><strong>Certificate Generation:</strong> A blockchain-verified certificate will be generated</li>
                    <li><strong>Global Protection:</strong> Your IP rights are now protected across the LKS ecosystem</li>
                    <li><strong>Portfolio Access:</strong> View your IP portfolio in your dashboard</li>
                </ul>

                <a href=""https://lksnetwork.io/dashboard/submissions/{submissionId}"" class=""button"">View Registration Progress</a>

                <p>You will receive another notification once the blockchain registration is complete. Thank you for choosing LKS BROTHERS IP PATENT Authority!</p>
                
                <p>Best regards,<br><strong>LKS BROTHERS IP PATENT Authority Team</strong></p>";

            return string.Format(BaseTemplate, content);
        }

        public static string RejectionNotification(string userName, string submissionId, string title, string type, DateTime rejectionDate, string reviewNotes)
        {
            var content = $@"
                <h2>Submission Review Update</h2>
                <p>Dear {userName},</p>
                <p>Thank you for your intellectual property submission to the LKS BROTHERS IP PATENT Authority. After careful review, we regret to inform you that your submission requires additional work before it can be approved.</p>
                
                <div class=""info-box"">
                    <h3>Submission Details</h3>
                    <p><strong>Submission ID:</strong> {submissionId}</p>
                    <p><strong>Title:</strong> {title}</p>
                    <p><strong>Type:</strong> {type}</p>
                    <p><strong>Reviewed:</strong> {rejectionDate:MMMM dd, yyyy 'at' HH:mm} UTC</p>
                </div>

                <span class=""status-badge status-rejected"">Requires Revision</span>

                <h3>Reviewer Feedback</h3>
                <div class=""info-box"">
                    <p>{reviewNotes}</p>
                </div>

                <h3>What You Can Do</h3>
                <ul>
                    <li><strong>Review Feedback:</strong> Carefully consider the reviewer's comments</li>
                    <li><strong>Make Improvements:</strong> Address the issues identified in the feedback</li>
                    <li><strong>Resubmit:</strong> Submit a revised version of your application</li>
                    <li><strong>Get Support:</strong> Contact our team if you need clarification</li>
                </ul>

                <a href=""https://lksnetwork.io/dashboard/submissions/{submissionId}"" class=""button"">View Submission Details</a>
                <a href=""https://lksnetwork.io/submit"" class=""button"" style=""background: #059669; margin-left: 10px;"">Submit Revision</a>

                <p>We encourage you to address the feedback and resubmit your application. Our team is here to help you succeed in protecting your intellectual property.</p>
                
                <p>Best regards,<br><strong>LKS BROTHERS IP PATENT Authority Team</strong></p>";

            return string.Format(BaseTemplate, content);
        }

        public static string BlockchainPublishedNotification(string userName, string submissionId, string title, string type, DateTime publishDate, List<MainnetUploadResult> uploadResults)
        {
            var successCount = uploadResults.Count(r => r.Success);
            var totalCount = uploadResults.Count;
            var progressPercentage = (successCount * 100) / totalCount;

            var mainnetListHtml = new StringBuilder();
            mainnetListHtml.Append(@"<div class=""mainnet-list"">");
            mainnetListHtml.Append("<h4>Blockchain Registration Results</h4>");
            
            foreach (var result in uploadResults)
            {
                var statusClass = result.Success ? "success" : "failed";
                var statusIcon = result.Success ? "✓" : "✗";
                mainnetListHtml.Append($@"
                    <div class=""mainnet-item"">
                        <span>{result.MainnetName}</span>
                        <span class=""{statusClass}"">{statusIcon} {(result.Success ? "Success" : "Failed")}</span>
                    </div>");
            }
            mainnetListHtml.Append("</div>");

            var content = $@"
                <h2>🚀 Blockchain Registration Complete!</h2>
                <p>Dear {userName},</p>
                <p>Excellent news! Your intellectual property has been successfully registered on the LKS BROTHERS blockchain network. Your IP rights are now permanently secured and globally protected.</p>
                
                <div class=""info-box"">
                    <h3>Registration Details</h3>
                    <p><strong>Submission ID:</strong> {submissionId}</p>
                    <p><strong>Title:</strong> {title}</p>
                    <p><strong>Type:</strong> {type}</p>
                    <p><strong>Published:</strong> {publishDate:MMMM dd, yyyy 'at' HH:mm} UTC</p>
                    <p><strong>Success Rate:</strong> {successCount}/{totalCount} mainnets ({progressPercentage}%)</p>
                </div>

                <span class=""status-badge status-published"">Published on Blockchain</span>

                <div class=""progress-bar"">
                    <div class=""progress-fill"" style=""width: {progressPercentage}%""></div>
                </div>

                {mainnetListHtml}

                <h3>Your Benefits</h3>
                <ul>
                    <li><strong>Immutable Protection:</strong> Your IP is permanently recorded on blockchain</li>
                    <li><strong>Global Recognition:</strong> Protected across all LKS BROTHERS networks</li>
                    <li><strong>Instant Verification:</strong> Blockchain-verified authenticity</li>
                    <li><strong>Zero Transaction Fees:</strong> All costs covered by LKS Foundation</li>
                    <li><strong>Certificate Available:</strong> Download your blockchain certificate</li>
                </ul>

                <a href=""https://lksnetwork.io/dashboard/submissions/{submissionId}"" class=""button"">View Certificate</a>
                <a href=""https://lksnetwork.io/dashboard/portfolio"" class=""button"" style=""background: #059669; margin-left: 10px;"">View Portfolio</a>

                <p>Congratulations on successfully protecting your intellectual property with LKS BROTHERS! Your innovation is now secured for the future.</p>
                
                <p>Best regards,<br><strong>LKS BROTHERS IP PATENT Authority Team</strong></p>";

            return string.Format(BaseTemplate, content);
        }

        public static string SystemMaintenanceNotification(string userName, DateTime maintenanceStart, DateTime maintenanceEnd, string reason)
        {
            var content = $@"
                <h2>Scheduled System Maintenance</h2>
                <p>Dear {userName},</p>
                <p>We are writing to inform you of scheduled maintenance on the LKS BROTHERS IP PATENT Authority system. During this time, some services may be temporarily unavailable.</p>
                
                <div class=""info-box"">
                    <h3>Maintenance Details</h3>
                    <p><strong>Start Time:</strong> {maintenanceStart:MMMM dd, yyyy 'at' HH:mm} UTC</p>
                    <p><strong>End Time:</strong> {maintenanceEnd:MMMM dd, yyyy 'at' HH:mm} UTC</p>
                    <p><strong>Duration:</strong> {(maintenanceEnd - maintenanceStart).TotalHours:F1} hours</p>
                    <p><strong>Reason:</strong> {reason}</p>
                </div>

                <h3>Services Affected</h3>
                <ul>
                    <li>IP PATENT submission portal</li>
                    <li>Dashboard and portfolio access</li>
                    <li>Blockchain registration services</li>
                    <li>Certificate generation</li>
                </ul>

                <h3>What to Expect</h3>
                <ul>
                    <li>Existing submissions will continue processing after maintenance</li>
                    <li>No data will be lost during the maintenance window</li>
                    <li>All services will be fully restored after completion</li>
                    <li>We will send a confirmation email when maintenance is complete</li>
                </ul>

                <p>We apologize for any inconvenience this may cause and appreciate your patience as we work to improve our services.</p>
                
                <p>Best regards,<br><strong>LKS BROTHERS IP PATENT Authority Team</strong></p>";

            return string.Format(BaseTemplate, content);
        }

        public static string SubscriptionReminderNotification(string userName, string subscriptionId, decimal amountDue, DateTime dueDate, int lksCoinsAllocated)
        {
            var content = $@"
                <h2>IP PATENT Subscription Payment Reminder</h2>
                <p>Dear {userName},</p>
                <p>This is a friendly reminder that your monthly IP PATENT subscription payment is due soon. Your continued subscription ensures uninterrupted access to our premium IP protection services.</p>
                
                <div class=""info-box"">
                    <h3>Payment Details</h3>
                    <p><strong>Subscription ID:</strong> {subscriptionId}</p>
                    <p><strong>Amount Due:</strong> ${amountDue:F2} USD</p>
                    <p><strong>Due Date:</strong> {dueDate:MMMM dd, yyyy}</p>
                    <p><strong>LKS Coins Allocated:</strong> {lksCoinsAllocated:N0} LKS</p>
                </div>

                <h3>Your Subscription Benefits</h3>
                <ul>
                    <li><strong>Unlimited IP Registrations:</strong> Submit as many applications as needed</li>
                    <li><strong>Priority Processing:</strong> Faster review and approval times</li>
                    <li><strong>Premium Templates:</strong> Access to professional IP templates</li>
                    <li><strong>24/7 Support:</strong> Dedicated customer support team</li>
                    <li><strong>LKS Coin Access:</strong> Use allocated coins immediately</li>
                </ul>

                <a href=""https://lksnetwork.io/dashboard/subscription"" class=""button"">Manage Subscription</a>
                <a href=""https://lksnetwork.io/dashboard/payment"" class=""button"" style=""background: #059669; margin-left: 10px;"">Make Payment</a>

                <p>Your LKS coins remain available for use while your subscription is active. Thank you for being a valued member of the LKS BROTHERS ecosystem!</p>
                
                <p>Best regards,<br><strong>LKS BROTHERS IP PATENT Authority Team</strong></p>";

            return string.Format(BaseTemplate, content);
        }
    }

    public class MainnetUploadResult
    {
        public string MainnetName { get; set; }
        public bool Success { get; set; }
        public string TransactionHash { get; set; }
        public string ErrorMessage { get; set; }
    }
}
