using System.ComponentModel.DataAnnotations;

namespace LksBrothers.IpPatent.Models
{
    public class PatentSearchResult
    {
        public string Query { get; set; } = string.Empty;
        public int TotalResults { get; set; }
        public List<Patent> Patents { get; set; } = new();
        public PatentAnalysis Analysis { get; set; } = new();
        public DateTime SearchTimestamp { get; set; }
    }

    public class PatentSearchOptions
    {
        public int MaxResults { get; set; } = 100;
        public string[] Categories { get; set; } = Array.Empty<string>();
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string[] Countries { get; set; } = Array.Empty<string>();
        public bool IncludeExpired { get; set; } = false;
    }

    public class Patent
    {
        public string Id { get; set; } = string.Empty;
        public string PatentNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Claims { get; set; } = new();
        public List<string> Inventors { get; set; } = new();
        public string Assignee { get; set; } = string.Empty;
        public DateTime FilingDate { get; set; }
        public DateTime? GrantDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public PatentStatus Status { get; set; }
        public string Country { get; set; } = string.Empty;
        public List<string> Categories { get; set; } = new();
        public decimal? EstimatedValue { get; set; }
        public string BlockchainHash { get; set; } = string.Empty;
    }

    public class PatentApplication
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Claims { get; set; } = new();
        public List<string> Inventors { get; set; } = new();
        public string Assignee { get; set; } = string.Empty;
        public PatentStatus Status { get; set; }
        public DateTime SubmissionDate { get; set; }
        public DateTime? ReviewDate { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public string PaymentTransactionId { get; set; } = string.Empty;
        public List<string> Documents { get; set; } = new();
        public string ExaminerNotes { get; set; } = string.Empty;
        public decimal ServiceFee { get; set; }
    }

    public class PatentApplicationRequest
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        [StringLength(10000)]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public List<string> Claims { get; set; } = new();
        
        [Required]
        public List<string> Inventors { get; set; } = new();
        
        public string Assignee { get; set; } = string.Empty;
        public List<string> Documents { get; set; } = new();
        public decimal ServiceFee { get; set; }
    }

    public class Trademark
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public List<string> Classes { get; set; } = new();
        public TrademarkStatus Status { get; set; }
        public DateTime FilingDate { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string Country { get; set; } = string.Empty;
        public decimal? EstimatedValue { get; set; }
        public string BlockchainHash { get; set; } = string.Empty;
    }

    public class Copyright
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public CopyrightType Type { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime RegistrationDate { get; set; }
        public string Author { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public CopyrightStatus Status { get; set; }
        public decimal? EstimatedValue { get; set; }
        public string BlockchainHash { get; set; } = string.Empty;
    }

    public class IpPortfolio
    {
        public string UserId { get; set; } = string.Empty;
        public List<Patent> Patents { get; set; } = new();
        public List<Trademark> Trademarks { get; set; } = new();
        public List<Copyright> Copyrights { get; set; } = new();
        public decimal TotalValue { get; set; }
        public DateTime LastUpdated { get; set; }
        public IpPortfolioStats Stats { get; set; } = new();
    }

    public class IpPortfolioStats
    {
        public int TotalPatents { get; set; }
        public int ActivePatents { get; set; }
        public int PendingPatents { get; set; }
        public int TotalTrademarks { get; set; }
        public int ActiveTrademarks { get; set; }
        public int TotalCopyrights { get; set; }
        public decimal TotalPortfolioValue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public int LicensingDeals { get; set; }
    }

    public class PatentAnalysis
    {
        public double SimilarityScore { get; set; }
        public string NoveltyAssessment { get; set; } = string.Empty;
        public List<string> PriorArtReferences { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public double PatentabilityScore { get; set; }
        public List<string> SimilarPatents { get; set; } = new();
        public string RiskAssessment { get; set; } = string.Empty;
    }

    public class IpServicePayment
    {
        public string UserId { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "LKS";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class IpPaymentRecord
    {
        public string PaymentId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string TransactionHash { get; set; } = string.Empty;
    }

    public class IpTransaction
    {
        public string Type { get; set; } = string.Empty;
        public string ApplicationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Hash { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class PaymentRequest
    {
        public string FromUserId { get; set; } = string.Empty;
        public string ToAddress { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "LKS";
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class BlockchainTransaction
    {
        public string Type { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public decimal GasFee { get; set; } = 0;
        public string Hash { get; set; } = string.Empty;
    }

    public enum PatentStatus
    {
        Draft,
        Submitted,
        UnderReview,
        Approved,
        Granted,
        Rejected,
        Expired,
        Abandoned
    }

    public enum TrademarkStatus
    {
        Applied,
        Published,
        Opposed,
        Registered,
        Renewed,
        Cancelled,
        Expired
    }

    public enum CopyrightStatus
    {
        Created,
        Registered,
        Published,
        Licensed,
        Expired,
        PublicDomain
    }

    public enum CopyrightType
    {
        Literary,
        Musical,
        Artistic,
        Dramatic,
        Software,
        Database,
        Audiovisual,
        Sound
    }
}
