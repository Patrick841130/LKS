using LksBrothers.Core.Primitives;
using MessagePack;

namespace LksBrothers.Compliance.Models;

[MessagePackObject]
public class KYCRequest
{
    [Key(0)]
    public required Address Address { get; set; }

    [Key(1)]
    public required string FirstName { get; set; }

    [Key(2)]
    public required string LastName { get; set; }

    [Key(3)]
    public required DateOnly DateOfBirth { get; set; }

    [Key(4)]
    public required string Nationality { get; set; }

    [Key(5)]
    public string? Email { get; set; }

    [Key(6)]
    public string? PhoneNumber { get; set; }

    [Key(7)]
    public required List<KYCDocument> Documents { get; set; } = new();

    [Key(8)]
    public Address? ResidenceAddress { get; set; }

    [Key(9)]
    public string? Occupation { get; set; }

    [Key(10)]
    public UInt256? AnnualIncome { get; set; }

    [Key(11)]
    public string? SourceOfFunds { get; set; }

    [Key(12)]
    public bool IsPoliticallyExposed { get; set; }

    [Key(13)]
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
}

[MessagePackObject]
public class KYCDocument
{
    [Key(0)]
    public required string Type { get; set; } // "passport", "drivers_license", "national_id", "utility_bill"

    [Key(1)]
    public required string Number { get; set; }

    [Key(2)]
    public required string IssuingCountry { get; set; }

    [Key(3)]
    public DateOnly? ExpiryDate { get; set; }

    [Key(4)]
    public required byte[] DocumentImage { get; set; }

    [Key(5)]
    public byte[]? SelfieImage { get; set; }

    [Key(6)]
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    [Key(7)]
    public KYCDocumentStatus Status { get; set; } = KYCDocumentStatus.Pending;

    [Key(8)]
    public string? VerificationNotes { get; set; }
}

[MessagePackObject]
public class KYCRecord
{
    [Key(0)]
    public required Hash Id { get; set; }

    [Key(1)]
    public required Address Address { get; set; }

    [Key(2)]
    public required string FirstName { get; set; }

    [Key(3)]
    public required string LastName { get; set; }

    [Key(4)]
    public required DateOnly DateOfBirth { get; set; }

    [Key(5)]
    public required string Nationality { get; set; }

    [Key(6)]
    public required string DocumentType { get; set; }

    [Key(7)]
    public required string DocumentNumber { get; set; }

    [Key(8)]
    public required KYCVerificationLevel VerificationLevel { get; set; }

    [Key(9)]
    public required KYCStatus Status { get; set; }

    [Key(10)]
    public required DateTimeOffset VerifiedAt { get; set; }

    [Key(11)]
    public required DateTimeOffset ExpiresAt { get; set; }

    [Key(12)]
    public required double RiskScore { get; set; }

    [Key(13)]
    public string? VerifiedBy { get; set; }

    [Key(14)]
    public string? Notes { get; set; }

    [Key(15)]
    public List<KYCFlag>? Flags { get; set; } = new();

    [Key(16)]
    public DateTimeOffset? LastReviewedAt { get; set; }
}

[MessagePackObject]
public class KYCFlag
{
    [Key(0)]
    public required string Type { get; set; }

    [Key(1)]
    public required string Description { get; set; }

    [Key(2)]
    public required KYCFlagSeverity Severity { get; set; }

    [Key(3)]
    public required DateTimeOffset CreatedAt { get; set; }

    [Key(4)]
    public bool IsResolved { get; set; }

    [Key(5)]
    public DateTimeOffset? ResolvedAt { get; set; }

    [Key(6)]
    public string? Resolution { get; set; }
}

public class KYCResult
{
    public required bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public KYCRecord? Record { get; set; }

    public static KYCResult Success(KYCRecord record) => 
        new KYCResult { IsSuccess = true, Record = record };

    public static KYCResult Failed(string error) => 
        new KYCResult { IsSuccess = false, ErrorMessage = error };
}

public enum KYCStatus
{
    Pending,
    UnderReview,
    Verified,
    Rejected,
    Expired,
    Suspended
}

public enum KYCVerificationLevel
{
    Basic,      // Email + Phone verification
    Enhanced,   // Basic + ID document
    Full,       // Enhanced + Address verification + Income verification
    Institutional // Full + Enhanced due diligence
}

public enum KYCDocumentStatus
{
    Pending,
    Verified,
    Rejected,
    Expired
}

public enum KYCFlagSeverity
{
    Low,
    Medium,
    High,
    Critical
}

[MessagePackObject]
public class KYCBatch
{
    [Key(0)]
    public required Hash Id { get; set; }

    [Key(1)]
    public required List<KYCRequest> Requests { get; set; }

    [Key(2)]
    public required DateTimeOffset SubmittedAt { get; set; }

    [Key(3)]
    public KYCBatchStatus Status { get; set; } = KYCBatchStatus.Processing;

    [Key(4)]
    public int ProcessedCount { get; set; }

    [Key(5)]
    public int SuccessCount { get; set; }

    [Key(6)]
    public int FailedCount { get; set; }

    [Key(7)]
    public DateTimeOffset? CompletedAt { get; set; }

    [Key(8)]
    public List<string>? Errors { get; set; } = new();
}

public enum KYCBatchStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

[MessagePackObject]
public class KYCStatistics
{
    [Key(0)]
    public int TotalApplications { get; set; }

    [Key(1)]
    public int VerifiedApplications { get; set; }

    [Key(2)]
    public int RejectedApplications { get; set; }

    [Key(3)]
    public int PendingApplications { get; set; }

    [Key(4)]
    public double AverageProcessingTimeHours { get; set; }

    [Key(5)]
    public double VerificationRate { get; set; }

    [Key(6)]
    public Dictionary<string, int> RejectionReasons { get; set; } = new();

    [Key(7)]
    public Dictionary<KYCVerificationLevel, int> VerificationLevels { get; set; } = new();

    [Key(8)]
    public Dictionary<string, int> DocumentTypes { get; set; } = new();

    [Key(9)]
    public int ExpiredRecords { get; set; }

    [Key(10)]
    public int FlaggedRecords { get; set; }
}
