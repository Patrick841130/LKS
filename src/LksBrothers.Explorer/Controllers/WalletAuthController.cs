using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.Signer;
using Nethereum.Util;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace LksBrothers.Explorer.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class WalletAuthController : ControllerBase
    {
        private readonly ILogger<WalletAuthController> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _jwtSecret;

        public WalletAuthController(ILogger<WalletAuthController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _jwtSecret = _configuration["JwtSecret"] ?? "lks-network-super-secret-key-change-in-production";
        }

        [HttpPost("wallet")]
        public async Task<IActionResult> AuthenticateWallet([FromBody] WalletAuthRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrEmpty(request.Account) || 
                    string.IsNullOrEmpty(request.Signature) || 
                    string.IsNullOrEmpty(request.Message))
                {
                    return BadRequest(new { success = false, error = "Missing required fields" });
                }

                // Validate timestamp (within 5 minutes)
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (Math.Abs(currentTime - request.Timestamp) > 300000) // 5 minutes
                {
                    return BadRequest(new { success = false, error = "Request expired" });
                }

                // Verify signature
                var isValidSignature = await VerifySignature(request.Account, request.Message, request.Signature);
                if (!isValidSignature)
                {
                    return Unauthorized(new { success = false, error = "Invalid signature" });
                }

                // Generate JWT token
                var token = GenerateJwtToken(request.Account);

                // Log successful authentication
                _logger.LogInformation($"Wallet authentication successful for account: {request.Account}");

                // Store user session (in production, use database)
                await StoreUserSession(request.Account, token);

                return Ok(new
                {
                    success = true,
                    token = token,
                    account = request.Account,
                    expiresIn = 86400 // 24 hours
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wallet authentication failed");
                return StatusCode(500, new { success = false, error = "Authentication failed" });
            }
        }

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyToken([FromBody] TokenVerificationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                {
                    return BadRequest(new { success = false, error = "Token required" });
                }

                var principal = ValidateJwtToken(request.Token);
                if (principal == null)
                {
                    return Unauthorized(new { success = false, error = "Invalid token" });
                }

                var account = principal.FindFirst("account")?.Value;
                if (string.IsNullOrEmpty(account))
                {
                    return Unauthorized(new { success = false, error = "Invalid token claims" });
                }

                return Ok(new
                {
                    success = true,
                    account = account,
                    valid = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token verification failed");
                return Unauthorized(new { success = false, error = "Invalid token" });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            try
            {
                // In production, invalidate token in database
                await InvalidateUserSession(request.Account);

                _logger.LogInformation($"User logged out: {request.Account}");

                return Ok(new { success = true, message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout failed");
                return StatusCode(500, new { success = false, error = "Logout failed" });
            }
        }

        [HttpGet("user/{account}")]
        public async Task<IActionResult> GetUserProfile(string account)
        {
            try
            {
                // Validate account format
                if (!IsValidEthereumAddress(account))
                {
                    return BadRequest(new { success = false, error = "Invalid account address" });
                }

                // Get user profile (in production, from database)
                var userProfile = await GetUserProfileFromDatabase(account);

                return Ok(new
                {
                    success = true,
                    profile = userProfile
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get user profile for {account}");
                return StatusCode(500, new { success = false, error = "Failed to get user profile" });
            }
        }

        private async Task<bool> VerifySignature(string account, string message, string signature)
        {
            try
            {
                var signer = new EthereumMessageSigner();
                var recoveredAddress = signer.EncodeUTF8AndEcRecover(message, signature);
                
                return string.Equals(recoveredAddress, account, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Signature verification failed");
                return false;
            }
        }

        private string GenerateJwtToken(string account)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSecret);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("account", account),
                    new Claim("type", "wallet"),
                    new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
                }),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private ClaimsPrincipal? ValidateJwtToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtSecret);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch
            {
                return null;
            }
        }

        private bool IsValidEthereumAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || !address.StartsWith("0x") || address.Length != 42)
                return false;

            return address.Substring(2).All(c => "0123456789abcdefABCDEF".Contains(c));
        }

        private async Task StoreUserSession(string account, string token)
        {
            // In production, store in database
            // For now, just log
            _logger.LogInformation($"Storing session for account: {account}");
            await Task.CompletedTask;
        }

        private async Task InvalidateUserSession(string account)
        {
            // In production, invalidate in database
            _logger.LogInformation($"Invalidating session for account: {account}");
            await Task.CompletedTask;
        }

        private async Task<object> GetUserProfileFromDatabase(string account)
        {
            // In production, get from database
            // For now, return mock data
            await Task.CompletedTask;
            
            return new
            {
                account = account,
                balance = "0",
                transactionCount = 0,
                firstSeen = DateTime.UtcNow,
                lastActive = DateTime.UtcNow,
                isContract = false
            };
        }
    }

    public class WalletAuthRequest
    {
        public string Account { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }

    public class TokenVerificationRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    public class LogoutRequest
    {
        public string Account { get; set; } = string.Empty;
    }
}
