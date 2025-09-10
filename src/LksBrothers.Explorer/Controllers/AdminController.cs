using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using LksBrothers.Explorer.Data;
using LksBrothers.Explorer.Models;

namespace LksBrothers.Explorer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ExplorerDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ExplorerDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Username,
                    u.FirstName,
                    u.LastName,
                    u.Role,
                    u.IsActive,
                    u.IsEmailVerified,
                    u.CreatedAt,
                    u.LastLoginAt,
                    u.ApiCallsToday,
                    u.ApiCallLimit
                })
                .ToListAsync();

            var totalCount = await _context.Users.CountAsync();

            return Ok(new
            {
                users,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { error = "Failed to get users" });
        }
    }

    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUser(Guid userId)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Sessions.Where(s => s.IsActive))
                .Include(u => u.Activities.OrderByDescending(a => a.CreatedAt).Take(10))
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            return Ok(new
            {
                user.Id,
                user.Email,
                user.Username,
                user.FirstName,
                user.LastName,
                user.Role,
                user.IsActive,
                user.IsEmailVerified,
                user.CreatedAt,
                user.LastLoginAt,
                user.XrpAddress,
                user.LksAddress,
                user.ApiCallsToday,
                user.ApiCallLimit,
                user.TwoFactorEnabled,
                ActiveSessions = user.Sessions.Count,
                RecentActivities = user.Activities.Select(a => new
                {
                    a.Action,
                    a.Details,
                    a.CreatedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user details");
            return StatusCode(500, new { error = "Failed to get user details" });
        }
    }

    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> UpdateUserRole(Guid userId, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            var validRoles = new[] { "User", "Admin", "Validator" };
            if (!validRoles.Contains(request.Role))
            {
                return BadRequest(new { error = "Invalid role" });
            }

            user.Role = request.Role;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} role updated to {Role}", userId, request.Role);

            return Ok(new { message = "User role updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user role");
            return StatusCode(500, new { error = "Failed to update user role" });
        }
    }

    [HttpPut("users/{userId}/status")]
    public async Task<IActionResult> UpdateUserStatus(Guid userId, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            user.IsActive = request.IsActive;
            await _context.SaveChangesAsync();

            // Deactivate all sessions if user is being deactivated
            if (!request.IsActive)
            {
                var sessions = await _context.UserSessions
                    .Where(s => s.UserId == userId && s.IsActive)
                    .ToListAsync();

                foreach (var session in sessions)
                {
                    session.IsActive = false;
                }
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("User {UserId} status updated to {Status}", userId, request.IsActive ? "Active" : "Inactive");

            return Ok(new { message = "User status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user status");
            return StatusCode(500, new { error = "Failed to update user status" });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetSystemStats()
    {
        try
        {
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users.CountAsync(u => u.IsActive);
            var newUsersToday = await _context.Users.CountAsync(u => u.CreatedAt.Date == DateTime.UtcNow.Date);
            var activeSessions = await _context.UserSessions.CountAsync(s => s.IsActive);
            
            var usersByRole = await _context.Users
                .GroupBy(u => u.Role)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToListAsync();

            var recentActivities = await _context.UserActivities
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
                .Include(a => a.User)
                .Select(a => new
                {
                    a.Action,
                    a.Details,
                    a.CreatedAt,
                    User = a.User.Username
                })
                .ToListAsync();

            return Ok(new
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                NewUsersToday = newUsersToday,
                ActiveSessions = activeSessions,
                UsersByRole = usersByRole,
                RecentActivities = recentActivities
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system stats");
            return StatusCode(500, new { error = "Failed to get system stats" });
        }
    }

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            // Don't allow deleting admin users
            if (user.Role == "Admin")
            {
                return BadRequest(new { error = "Cannot delete admin users" });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deleted", userId);

            return Ok(new { message = "User deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user");
            return StatusCode(500, new { error = "Failed to delete user" });
        }
    }
}

public class UpdateRoleRequest
{
    public string Role { get; set; } = string.Empty;
}

public class UpdateStatusRequest
{
    public bool IsActive { get; set; }
}
