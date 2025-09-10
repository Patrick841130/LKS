using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using LksBrothers.Explorer.Data;
using LksBrothers.Explorer.Models;
using LksBrothers.Explorer.DTOs;

namespace LksBrothers.Explorer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly ExplorerDbContext _context;
    private readonly ILogger<UserController> _logger;
    private readonly IConfiguration _configuration;

    public UserController(ExplorerDbContext context, ILogger<UserController> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { error = "Email and password are required" });
            }

            // Check if user already exists
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest(new { error = "User with this email already exists" });
            }

            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return BadRequest(new { error = "Username already taken" });
            }

            // Create new user
            var user = new User
            {
                Email = request.Email,
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Role = "User",
                ApiKey = Guid.NewGuid().ToString("N")
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Log activity
            await LogUserActivity(user.Id, "User Registration", "New user registered");

            _logger.LogInformation("New user registered: {Email}", request.Email);

            return Ok(new { 
                message = "User registered successfully", 
                userId = user.Id,
                apiKey = user.ApiKey
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user");
            return StatusCode(500, new { error = "Registration failed" });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { error = "Invalid credentials" });
            }

            // Generate JWT token
            var token = GenerateJwtToken(user);

            // Create session
            var session = new UserSession
            {
                UserId = user.Id,
                Token = token,
                DeviceInfo = Request.Headers.UserAgent.ToString(),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            _context.UserSessions.Add(session);

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Log activity
            await LogUserActivity(user.Id, "User Login", "User logged in successfully");

            return Ok(new LoginResponse
            {
                Token = token,
                User = new UserProfile
                {
                    Id = user.Id,
                    Email = user.Email,
                    Username = user.Username,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role,
                    ProfileImageUrl = user.ProfileImageUrl,
                    ApiKey = user.ApiKey
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { error = "Login failed" });
        }
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            return Ok(new UserProfile
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role,
                ProfileImageUrl = user.ProfileImageUrl,
                XrpAddress = user.XrpAddress,
                LksAddress = user.LksAddress,
                PreferredCurrency = user.PreferredCurrency,
                TimeZone = user.TimeZone,
                EmailNotifications = user.EmailNotifications,
                TwoFactorEnabled = user.TwoFactorEnabled,
                ApiKey = user.ApiKey,
                ApiCallsToday = user.ApiCallsToday,
                ApiCallLimit = user.ApiCallLimit,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile");
            return StatusCode(500, new { error = "Failed to get profile" });
        }
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            // Update fields
            if (!string.IsNullOrEmpty(request.FirstName))
                user.FirstName = request.FirstName;
            if (!string.IsNullOrEmpty(request.LastName))
                user.LastName = request.LastName;
            if (!string.IsNullOrEmpty(request.XrpAddress))
                user.XrpAddress = request.XrpAddress;
            if (!string.IsNullOrEmpty(request.LksAddress))
                user.LksAddress = request.LksAddress;
            if (!string.IsNullOrEmpty(request.PreferredCurrency))
                user.PreferredCurrency = request.PreferredCurrency;
            if (!string.IsNullOrEmpty(request.TimeZone))
                user.TimeZone = request.TimeZone;

            user.EmailNotifications = request.EmailNotifications;

            await _context.SaveChangesAsync();
            await LogUserActivity(userId, "Profile Update", "User updated profile information");

            return Ok(new { message = "Profile updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile");
            return StatusCode(500, new { error = "Failed to update profile" });
        }
    }

    [HttpGet("activity")]
    [Authorize]
    public async Task<IActionResult> GetUserActivity([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            var activities = await _context.UserActivities
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.Action,
                    a.Details,
                    a.IpAddress,
                    a.CreatedAt
                })
                .ToListAsync();

            var totalCount = await _context.UserActivities.CountAsync(a => a.UserId == userId);

            return Ok(new
            {
                activities,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user activity");
            return StatusCode(500, new { error = "Failed to get activity" });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var userId = GetCurrentUserId();
            var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");

            // Deactivate session
            var session = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.Token == token && s.UserId == userId);

            if (session != null)
            {
                session.IsActive = false;
                await _context.SaveChangesAsync();
            }

            await LogUserActivity(userId, "User Logout", "User logged out");

            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { error = "Logout failed" });
        }
    }

    private string GenerateJwtToken(User user)
    {
        var jwtKey = _configuration["JWT_KEY"] ?? throw new InvalidOperationException("JWT_KEY not configured");
        var jwtIssuer = _configuration["JWT_ISSUER"] ?? "LKS_NETWORK";
        var jwtAudience = _configuration["JWT_AUDIENCE"] ?? "LKS_NETWORK";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException());
    }

    private async Task LogUserActivity(Guid userId, string action, string details)
    {
        var activity = new UserActivity
        {
            UserId = userId,
            Action = action,
            Details = details,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        _context.UserActivities.Add(activity);
        await _context.SaveChangesAsync();
    }
}
