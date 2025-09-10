using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LksBrothers.Core.Primitives;

namespace LksBrothers.Wallet.Services;

public class SimpleAuthService
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly string _jwtKey;

    public SimpleAuthService(IMemoryCache cache, IConfiguration config)
    {
        _cache = cache;
        _config = config;
        _jwtKey = config["Jwt:Key"] ?? "super-secret-key-for-lks-wallet";
    }

    public async Task<SimpleLoginResult> LoginWithGoogleAsync(string googleToken, string email, string name)
    {
        try
        {
            // Generate wallet address from email (deterministic)
            var walletAddress = GenerateWalletAddress(email);
            
            // Create or get user session
            var user = new SimpleUser
            {
                Email = email,
                Name = name,
                WalletAddress = walletAddress,
                LoginTime = DateTimeOffset.UtcNow
            };

            // Generate simple JWT
            var token = GenerateJwtToken(user);
            
            // Cache user session
            _cache.Set($"user_{email}", user, TimeSpan.FromHours(24));
            
            return new SimpleLoginResult
            {
                Success = true,
                Token = token,
                User = user
            };
        }
        catch (Exception ex)
        {
            return new SimpleLoginResult
            {
                Success = false,
                Error = $"Login failed: {ex.Message}"
            };
        }
    }

    public SimpleUser? GetUser(string email)
    {
        return _cache.Get<SimpleUser>($"user_{email}");
    }

    public bool IsValidSession(string email)
    {
        var user = GetUser(email);
        return user != null && user.LoginTime.AddHours(24) > DateTimeOffset.UtcNow;
    }

    private Address GenerateWalletAddress(string email)
    {
        // Simple deterministic address generation from email
        var emailBytes = Encoding.UTF8.GetBytes(email.ToLowerInvariant());
        var hash = Hash.ComputeHash(emailBytes);
        return new Address(hash.ToByteArray().Take(20).ToArray());
    }

    private string GenerateJwtToken(SimpleUser user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtKey);
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("email", user.Email),
                new Claim("name", user.Name),
                new Claim("wallet", user.WalletAddress.ToString())
            }),
            Expires = DateTime.UtcNow.AddHours(24),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

public class SimpleUser
{
    public required string Email { get; set; }
    public required string Name { get; set; }
    public required Address WalletAddress { get; set; }
    public required DateTimeOffset LoginTime { get; set; }
}

public class SimpleLoginResult
{
    public required bool Success { get; set; }
    public string? Token { get; set; }
    public SimpleUser? User { get; set; }
    public string? Error { get; set; }
}
