using LksBrothers.Core.Primitives;
using MessagePack;

namespace LksBrothers.Compliance.Models;

[MessagePackObject]
public class AMLRiskProfile
{
    [Key(0)]
    public required Address Address { get; set; }

    [Key(1)]
    public required List<AMLTransaction> TransactionHistory { get; set; } = new();

    [Key(2)]
    public double OverallRiskScore { get; set; }

    [Key(3)]
    public AMLRiskLevel RiskLevel { get; set; }

    [Key(4)]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Key(5)]
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    [Key(6)]
    public List<AMLAlert>? Alerts { get; set; } = new();

    [Key(7)]
    public Dictionary<string, double> RiskFactors { get; set; } = new();

    [Key(8)]
    public bool IsUnderInvestigation { get; set; }

    [Key(9)]
    public string? InvestigationNotes { get; set; }

    [Key(10)]
    public DateTimeOffset? LastScreenedAt { get; set; }
}

[MessagePackObject]
public class AMLTransaction
{
    [Key(0)]
    public required Hash Hash { get; set; }

    [Key(1)]
    public required UInt256 Amount { get; set; }

    [Key(2)]
    public required string Type { get; set; }

    [Key(3)]
    public required DateTimeOffset Timestamp { get; set; }

    [Key(4)]
    public required double RiskScore { get; set; }

    [Key(5)]
    public Address? CounterpartyAddress { get; set; }

    [Key(6)]
    public string? CounterpartyInfo { get; set; }

    [Key(7)]
    public List<string>? RiskIndicators { get; set; } = new();

    [Key(8)]
    public bool IsSuspicious { get; set; }

    [Key(9)]
    public string? SuspiciousReason { get; set; }

    [Key(10)]
    public bool IsReported { get; set; }

    [Key(11)]
    public DateTimeOffset? ReportedAt { get; set; }
}

[MessagePackObject]
public class AMLAlert
{
    [Key(0)]
    public required Hash Id { get; set; }

    [Key(1)]
    public required Address Address { get; set; }

    [Key(2)]
    public required AMLAlertType Type { get; set; }

    [Key(3)]
    public required string Description { get; set; }

    [Key(4)]
    public required AMLAlertSeverity Severity { get; set; }

    [Key(5)]
    public required DateTimeOffset CreatedAt { get; set; }

    [Key(6)]
    public AMLAlertStatus Status { get; set; } = AMLAlertStatus.Open;

    [Key(7)]
    public Hash? RelatedTransactionHash { get; set; }

    [Key(8)]
    public string? AssignedTo { get; set; }

    [Key(9)]
    public DateTimeOffset? ResolvedAt { get; set; }

    [Key(10)]
    public string? Resolution { get; set; }

    [Key(11)]
    public List<string>? Evidence { get; set; } = new();

    [Key(12)]
    public bool RequiresSAR { get; set; } // Suspicious Activity Report

    [Key(13)]
    public DateTimeOffset? SARFiledAt { get; set; }
}

[MessagePackObject]
public class SuspiciousActivityReport
{
    [Key(0)]
    public required Hash Id { get; set; }

    [Key(1)]
    public required Address SubjectAddress { get; set; }

    [Key(2)]
    public required string SuspiciousActivity { get; set; }

    [Key(3)]
    public required List<Hash> RelatedTransactions { get; set; }

    [Key(4)]
    public required UInt256 TotalAmount { get; set; }

    [Key(5)]
    public required DateTimeOffset ActivityPeriodStart { get; set; }

    [Key(6)]
    public required DateTimeOffset ActivityPeriodEnd { get; set; }

    [Key(7)]
    public required string ReportingOfficer { get; set; }

    [Key(8)]
    public required DateTimeOffset FiledAt { get; set; }

    [Key(9)]
    public string? RegulatoryReference { get; set; }

    [Key(10)]
    public SARStatus Status { get; set; } = SARStatus.Filed;

    [Key(11)]
    public string? FollowUpActions { get; set; }

    [Key(12)]
    public List<string>? Attachments { get; set; } = new();
}

[MessagePackObject]
public class AMLScreeningRequest
{
    [Key(0)]
    public required Address Address { get; set; }

    [Key(1)]
    public required UInt256 Amount { get; set; }

    [Key(2)]
    public required string TransactionType { get; set; }

    [Key(3)]
    public Address? CounterpartyAddress { get; set; }

    [Key(4)]
    public string? AdditionalInfo { get; set; }

    [Key(5)]
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    [Key(6)]
    public bool IsRealTime { get; set; } = true;
}

public class AMLResult
{
    public required bool IsClear { get; set; }
    public string? ErrorMessage { get; set; }
    public AMLRiskProfile? Profile { get; set; }
    public List<AMLAlert>? Alerts { get; set; }

    public static AMLResult Clear(AMLRiskProfile profile) => 
        new AMLResult { IsClear = true, Profile = profile };

    public static AMLResult Failed(string error) => 
        new AMLResult { IsClear = false, ErrorMessage = error };

    public static AMLResult Flagged(AMLRiskProfile profile, List<AMLAlert> alerts) => 
        new AMLResult { IsClear = false, Profile = profile, Alerts = alerts, ErrorMessage = "AML alerts generated" };
}

public enum AMLRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum AMLAlertType
{
    UnusualTransactionPattern,
    HighVelocityTransactions,
    LargeTransactionAmount,
    StructuredTransactions,
    GeographicRisk,
    CounterpartyRisk,
    PoliticallyExposedPerson,
    SanctionsMatch,
    BlacklistMatch,
    SuspiciousCounterparty
}

public enum AMLAlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum AMLAlertStatus
{
    Open,
    UnderReview,
    Escalated,
    Resolved,
    FalsePositive,
    Closed
}

public enum SARStatus
{
    Draft,
    Filed,
    Acknowledged,
    UnderInvestigation,
    Closed
}

[MessagePackObject]
public class AMLStatistics
{
    [Key(0)]
    public int TotalScreenings { get; set; }

    [Key(1)]
    public int ClearScreenings { get; set; }

    [Key(2)]
    public int FlaggedScreenings { get; set; }

    [Key(3)]
    public int TotalAlerts { get; set; }

    [Key(4)]
    public int OpenAlerts { get; set; }

    [Key(5)]
    public int ResolvedAlerts { get; set; }

    [Key(6)]
    public int SARsFiled { get; set; }

    [Key(7)]
    public double AverageRiskScore { get; set; }

    [Key(8)]
    public Dictionary<AMLAlertType, int> AlertsByType { get; set; } = new();

    [Key(9)]
    public Dictionary<AMLRiskLevel, int> ProfilesByRiskLevel { get; set; } = new();

    [Key(10)]
    public UInt256 TotalSuspiciousAmount { get; set; }

    [Key(11)]
    public double FalsePositiveRate { get; set; }

    [Key(12)]
    public double AverageResolutionTimeHours { get; set; }
}

[MessagePackObject]
public class AMLConfiguration
{
    [Key(0)]
    public double HighRiskThreshold { get; set; } = 0.7;

    [Key(1)]
    public double MediumRiskThreshold { get; set; } = 0.4;

    [Key(2)]
    public UInt256 LargeTransactionThreshold { get; set; } = UInt256.Parse("10000000000000000000000"); // 10,000 LKS

    [Key(3)]
    public int VelocityThresholdPerHour { get; set; } = 50;

    [Key(4)]
    public int VelocityThresholdPerDay { get; set; } = 500;

    [Key(5)]
    public double StructuringDetectionThreshold { get; set; } = 0.8;

    [Key(6)]
    public int MaxTransactionHistoryDays { get; set; } = 365;

    [Key(7)]
    public bool EnableRealTimeScreening { get; set; } = true;

    [Key(8)]
    public bool AutoGenerateSAR { get; set; } = false;

    [Key(9)]
    public List<string> HighRiskCountries { get; set; } = new();

    [Key(10)]
    public List<string> HighRiskBusinessTypes { get; set; } = new();
}
