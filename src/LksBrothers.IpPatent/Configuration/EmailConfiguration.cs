using System;
using System.Collections.Generic;

namespace LksBrothers.IpPatent.Configuration
{
    public class EmailConfiguration
    {
        public SmtpSettings Smtp { get; set; } = new SmtpSettings();
        public BrandingSettings Branding { get; set; } = new BrandingSettings();
        public TemplateSettings Templates { get; set; } = new TemplateSettings();
        public DeliverySettings Delivery { get; set; } = new DeliverySettings();
        public SecuritySettings Security { get; set; } = new SecuritySettings();
    }

    public class SmtpSettings
    {
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public string Username { get; set; }
        public string Password { get; set; }
        public bool EnableSsl { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 30;
        public string FromEmail { get; set; } = "noreply@lksnetwork.io";
        public string FromName { get; set; } = "LKS BROTHERS IP PATENT Authority";
        public string ReplyToEmail { get; set; } = "support@lksnetwork.io";
    }

    public class BrandingSettings
    {
        public string CompanyName { get; set; } = "LKS BROTHERS";
        public string ServiceName { get; set; } = "IP PATENT Authority";
        public string LogoUrl { get; set; } = "https://lksnetwork.io/assets/logo.png";
        public string WebsiteUrl { get; set; } = "https://lksnetwork.io";
        public string SupportUrl { get; set; } = "https://lksnetwork.io/support";
        public string DashboardUrl { get; set; } = "https://lksnetwork.io/dashboard";
        public string PrimaryColor { get; set; } = "#667eea";
        public string SecondaryColor { get; set; } = "#764ba2";
        public string AccentColor { get; set; } = "#059669";
    }

    public class TemplateSettings
    {
        public string DefaultLanguage { get; set; } = "en";
        public List<string> SupportedLanguages { get; set; } = new List<string> { "en", "es", "fr", "de", "zh" };
        public bool EnableMultiLanguage { get; set; } = true;
        public string TemplateDirectory { get; set; } = "Templates";
        public Dictionary<string, string> CustomTemplates { get; set; } = new Dictionary<string, string>();
    }

    public class DeliverySettings
    {
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 60;
        public int BulkEmailBatchSize { get; set; } = 50;
        public int BulkEmailDelayMs { get; set; } = 1000;
        public bool EnableDeliveryTracking { get; set; } = true;
        public bool EnableBounceHandling { get; set; } = true;
        public List<string> BlockedDomains { get; set; } = new List<string>();
    }

    public class SecuritySettings
    {
        public bool EnableDkim { get; set; } = true;
        public bool EnableSpf { get; set; } = true;
        public bool EnableDmarc { get; set; } = true;
        public string DkimSelector { get; set; } = "lks-selector";
        public bool RequireTls { get; set; } = true;
        public List<string> AllowedDomains { get; set; } = new List<string>();
        public bool EnableEmailValidation { get; set; } = true;
        public int RateLimitPerHour { get; set; } = 100;
    }

    public class EmailMetrics
    {
        public int TotalSent { get; set; }
        public int TotalDelivered { get; set; }
        public int TotalBounced { get; set; }
        public int TotalFailed { get; set; }
        public double DeliveryRate => TotalSent > 0 ? (double)TotalDelivered / TotalSent * 100 : 0;
        public double BounceRate => TotalSent > 0 ? (double)TotalBounced / TotalSent * 100 : 0;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class EmailTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Subject { get; set; }
        public string HtmlContent { get; set; }
        public string TextContent { get; set; }
        public string Language { get; set; } = "en";
        public Dictionary<string, object> DefaultVariables { get; set; } = new Dictionary<string, object>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }

    public class EmailQueue
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ToEmail { get; set; }
        public string ToName { get; set; }
        public string Subject { get; set; }
        public string HtmlContent { get; set; }
        public string TextContent { get; set; }
        public int Priority { get; set; } = 5; // 1-10, 1 being highest priority
        public int RetryCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ScheduledAt { get; set; }
        public DateTime? SentAt { get; set; }
        public EmailStatus Status { get; set; } = EmailStatus.Pending;
        public string ErrorMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    public enum EmailStatus
    {
        Pending,
        Sending,
        Sent,
        Delivered,
        Bounced,
        Failed,
        Cancelled
    }

    public enum EmailType
    {
        SubmissionConfirmation,
        ReviewTeamNotification,
        ApprovalNotification,
        RejectionNotification,
        BlockchainPublished,
        SystemMaintenance,
        SubscriptionReminder,
        BulkNotification,
        Custom
    }

    public class EmailAnalytics
    {
        public Dictionary<EmailType, EmailMetrics> MetricsByType { get; set; } = new Dictionary<EmailType, EmailMetrics>();
        public Dictionary<string, EmailMetrics> MetricsByLanguage { get; set; } = new Dictionary<string, EmailMetrics>();
        public Dictionary<DateTime, int> DailySendCounts { get; set; } = new Dictionary<DateTime, int>();
        public List<string> TopBouncedDomains { get; set; } = new List<string>();
        public double OverallDeliveryRate { get; set; }
        public double OverallBounceRate { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
