using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LksBrothers.Explorer.Models;

[Table("Users")]
public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [StringLength(50)]
    public string? FirstName { get; set; }

    [StringLength(50)]
    public string? LastName { get; set; }

    [StringLength(20)]
    public string Role { get; set; } = "User"; // User, Admin, Validator

    public bool IsEmailVerified { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public string? ProfileImageUrl { get; set; }

    // Wallet addresses
    public string? XrpAddress { get; set; }
    public string? LksAddress { get; set; }

    // User preferences
    public string? PreferredCurrency { get; set; } = "USD";
    public string? TimeZone { get; set; } = "UTC";
    public bool EmailNotifications { get; set; } = true;
    public bool TwoFactorEnabled { get; set; } = false;

    // API access
    public string? ApiKey { get; set; }
    public int ApiCallsToday { get; set; } = 0;
    public int ApiCallLimit { get; set; } = 1000;

    // Navigation properties
    public virtual ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    public virtual ICollection<UserActivity> Activities { get; set; } = new List<UserActivity>();
    public virtual ICollection<SavedSearch> SavedSearches { get; set; } = new List<SavedSearch>();
}

[Table("UserSessions")]
public class UserSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public string Token { get; set; } = string.Empty;

    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}

[Table("UserActivities")]
public class UserActivity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}

[Table("SavedSearches")]
public class SavedSearch
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string SearchQuery { get; set; } = string.Empty;

    public string? SearchType { get; set; } // Block, Transaction, Address
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}
