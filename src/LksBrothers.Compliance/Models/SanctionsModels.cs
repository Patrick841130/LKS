using LksBrothers.Core.Primitives;
using MessagePack;

namespace LksBrothers.Compliance.Models;

[MessagePackObject]
public class SanctionsScreeningRequest
{
    [Key(0)]
    public required Address Address { get; set; }

    [Key(1)]
    public string? Name { get; set; }

    [Key(2)]
    public string? Country { get; set; }

    [Key(3)]
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    [Key(4)]
    public bool IsRealTime { get; set; } = true;

    [Key(5)]
    public List<string>? AdditionalIdentifiers { get; set; } = new();
}

[MessagePackObject]
public class SanctionsMatch
{
    [Key(0)]
    public required Hash Id { get; set; }

    [Key(1)]
    public required Address Address { get; set; }

    [Key(2)]
    public required string SanctionsList { get; set; } // "OFAC", "EU", "UN", etc.

    [Key(3)]
    public required string MatchedName { get; set; }

    [Key(4)]
    public required double ConfidenceScore { get; set; }

    [Key(5)]
    public required SanctionsMatchType MatchType { get; set; }

    [Key(6)]
    public required DateTimeOffset DetectedAt { get; set; }

    [Key(7)]
    public SanctionsMatchStatus Status { get; set; } = SanctionsMatchStatus.Active;

    [Key(8)]
    public string? SanctionsReason { get; set; }

    [Key(9)]
    public DateOnly? SanctionsDate { get; set; }

    [Key(10)]
    public string? ReferenceNumber { get; set; }

    [Key(11)]
    public List<string>? Aliases { get; set; } = new();

    [Key(12)]
    public string? AdditionalInfo { get; set; }

    [Key(13)]
    public bool IsBlocked { get; set; } = true;

    [Key(14)]
    public string? ReviewedBy { get; set; }

    [Key(15)]
    public DateTimeOffset? ReviewedAt { get; set; }
}

[MessagePackObject]
public class SanctionsListEntry
{
    [Key(0)]
    public required string Id { get; set; }

    [Key(1)]
    public required string Name { get; set; }

    [Key(2)]
    public required string ListName { get; set; }

    [Key(3)]
    public List<string>? Aliases { get; set; } = new();

    [Key(4)]
    public string? Country { get; set; }

    [Key(5)]
    public DateOnly? DateOfBirth { get; set; }

    [Key(6)]
    public string? PlaceOfBirth { get; set; }

    [Key(7)]
    public string? Nationality { get; set; }

    [Key(8)]
    public string? PassportNumber { get; set; }

    [Key(9)]
    public string? NationalId { get; set; }

    [Key(10)]
    public string? SanctionsReason { get; set; }

    [Key(11)]
    public DateOnly SanctionsDate { get; set; }

    [Key(12)]
    public string? ReferenceNumber { get; set; }

    [Key(13)]
    public SanctionsEntryType EntryType { get; set; }

    [Key(14)]
    public bool IsActive { get; set; } = true;

    [Key(15)]
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

[MessagePackObject]
public class SanctionsAlert
{
    [Key(0)]
    public required Hash Id { get; set; }

    [Key(1)]
    public required Address Address { get; set; }

    [Key(2)]
    public required SanctionsMatch Match { get; set; }

    [Key(3)]
    public required SanctionsAlertSeverity Severity { get; set; }

    [Key(4)]
    public required DateTimeOffset CreatedAt { get; set; }

    [Key(5)]
    public SanctionsAlertStatus Status { get; set; } = SanctionsAlertStatus.Open;

    [Key(6)]
    public string? AssignedTo { get; set; }

    [Key(7)]
    public DateTimeOffset? ResolvedAt { get; set; }

    [Key(8)]
    public string? Resolution { get; set; }

    [Key(9)]
    public bool IsEscalated { get; set; }

    [Key(10)]
    public DateTimeOffset? EscalatedAt { get; set; }

    [Key(11)]
    public List<string>? Actions { get; set; } = new();

    [Key(12)]
    public bool RequiresRegulatorNotification { get; set; }

    [Key(13)]
    public DateTimeOffset? RegulatorNotifiedAt { get; set; }
}

public class SanctionsResult
{
    public required bool IsClear { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SanctionsMatch>? Matches { get; set; }
    public List<SanctionsAlert>? Alerts { get; set; }

    public static SanctionsResult Clear(string message) => 
        new SanctionsResult { IsClear = true, ErrorMessage = message };

    public static SanctionsResult Sanctioned(string error) => 
        new SanctionsResult { IsClear = false, ErrorMessage = error };

    public static SanctionsResult Failed(string error) => 
        new SanctionsResult { IsClear = false, ErrorMessage = error };

    public static SanctionsResult WithMatches(List<SanctionsMatch> matches, List<SanctionsAlert> alerts) => 
        new SanctionsResult { IsClear = false, Matches = matches, Alerts = alerts, ErrorMessage = "Sanctions matches found" };
}

public enum SanctionsMatchType
{
    ExactMatch,
    FuzzyMatch,
    AliasMatch,
    PartialMatch
}

public enum SanctionsMatchStatus
{
    Active,
    UnderReview,
    FalsePositive,
    Confirmed,
    Resolved
}

public enum SanctionsEntryType
{
    Individual,
    Entity,
    Vessel,
    Aircraft,
    Address
}

public enum SanctionsAlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum SanctionsAlertStatus
{
    Open,
    UnderReview,
    Escalated,
    Resolved,
    FalsePositive,
    Closed
}

[MessagePackObject]
public class SanctionsStatistics
{
    [Key(0)]
    public int TotalScreenings { get; set; }

    [Key(1)]
    public int ClearScreenings { get; set; }

    [Key(2)]
    public int MatchedScreenings { get; set; }

    [Key(3)]
    public int TotalMatches { get; set; }

    [Key(4)]
    public int ActiveMatches { get; set; }

    [Key(5)]
    public int FalsePositives { get; set; }

    [Key(6)]
    public int ConfirmedMatches { get; set; }

    [Key(7)]
    public Dictionary<string, int> MatchesByList { get; set; } = new();

    [Key(8)]
    public Dictionary<SanctionsMatchType, int> MatchesByType { get; set; } = new();

    [Key(9)]
    public double AverageConfidenceScore { get; set; }

    [Key(10)]
    public double FalsePositiveRate { get; set; }

    [Key(11)]
    public int TotalAlerts { get; set; }

    [Key(12)]
    public int OpenAlerts { get; set; }

    [Key(13)]
    public double AverageResolutionTimeHours { get; set; }

    [Key(14)]
    public int RegulatorNotifications { get; set; }
}

[MessagePackObject]
public class SanctionsConfiguration
{
    [Key(0)]
    public double ExactMatchThreshold { get; set; } = 1.0;

    [Key(1)]
    public double FuzzyMatchThreshold { get; set; } = 0.85;

    [Key(2)]
    public double AliasMatchThreshold { get; set; } = 0.9;

    [Key(3)]
    public bool EnableRealTimeScreening { get; set; } = true;

    [Key(4)]
    public bool AutoBlockOnMatch { get; set; } = true;

    [Key(5)]
    public bool RequireManualReview { get; set; } = true;

    [Key(6)]
    public int ListUpdateIntervalHours { get; set; } = 24;

    [Key(7)]
    public List<string> EnabledLists { get; set; } = new() { "OFAC", "EU", "UN", "HMT" };

    [Key(8)]
    public bool EnableGeographicScreening { get; set; } = true;

    [Key(9)]
    public List<string> HighRiskCountries { get; set; } = new();

    [Key(10)]
    public bool NotifyRegulatorsOnMatch { get; set; } = true;

    [Key(11)]
    public int MaxAlertRetentionDays { get; set; } = 2555; // 7 years

    [Key(12)]
    public bool EnableAuditLogging { get; set; } = true;
}

[MessagePackObject]
public class SanctionsAuditLog
{
    [Key(0)]
    public required Hash Id { get; set; }

    [Key(1)]
    public required Address Address { get; set; }

    [Key(2)]
    public required string Action { get; set; }

    [Key(3)]
    public required DateTimeOffset Timestamp { get; set; }

    [Key(4)]
    public string? UserId { get; set; }

    [Key(5)]
    public string? Details { get; set; }

    [Key(6)]
    public Hash? RelatedMatchId { get; set; }

    [Key(7)]
    public Hash? RelatedAlertId { get; set; }

    [Key(8)]
    public string? IpAddress { get; set; }

    [Key(9)]
    public string? UserAgent { get; set; }
}
