namespace LksBrothers.Explorer.DTOs;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public UserProfile User { get; set; } = null!;
}

public class UserProfile
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string? XrpAddress { get; set; }
    public string? LksAddress { get; set; }
    public string? PreferredCurrency { get; set; }
    public string? TimeZone { get; set; }
    public bool EmailNotifications { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? ApiKey { get; set; }
    public int ApiCallsToday { get; set; }
    public int ApiCallLimit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class UpdateProfileRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? XrpAddress { get; set; }
    public string? LksAddress { get; set; }
    public string? PreferredCurrency { get; set; }
    public string? TimeZone { get; set; }
    public bool EmailNotifications { get; set; } = true;
}
