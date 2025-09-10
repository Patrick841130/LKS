using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LksBrothers.Explorer.Services;

namespace LksBrothers.Explorer.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var result = _authService.Login(request.Email, request.Password);
            
            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(result);
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            var result = _authService.Register(request.Email, request.Password, request.Name, request.Role ?? "User");
            
            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(result);
        }

        [HttpPost("validate")]
        public IActionResult ValidateToken([FromBody] ValidateTokenRequest request)
        {
            var isValid = _authService.ValidateToken(request.Token);
            var user = isValid ? _authService.GetUserFromToken(request.Token) : null;

            return Ok(new { valid = isValid, user });
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult GetCurrentUser()
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var user = _authService.GetUserFromToken(token);

            if (user == null)
            {
                return Unauthorized();
            }

            return Ok(user);
        }

        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetAllUsers()
        {
            var users = _authService.GetAllUsers();
            return Ok(users);
        }

        [HttpPut("users/{email}/role")]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateUserRole(string email, [FromBody] UpdateRoleRequest request)
        {
            var success = _authService.UpdateUserRole(email, request.Role);
            
            if (!success)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new { message = "Role updated successfully" });
        }

        [HttpDelete("users/{email}")]
        [Authorize(Roles = "Admin")]
        public IActionResult DeactivateUser(string email)
        {
            var success = _authService.DeactivateUser(email);
            
            if (!success)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new { message = "User deactivated successfully" });
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Role { get; set; }
    }

    public class ValidateTokenRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    public class UpdateRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }
}
