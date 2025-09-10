using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LksBrothers.Explorer.Services
{
    public class AuthService
    {
        private readonly string _secretKey = "LKS-BROTHERS-SECRET-KEY-FOR-JWT-TOKENS-2024";
        private readonly string _issuer = "lks-brothers-mainnet";
        private readonly string _audience = "lks-explorer";
        private readonly Dictionary<string, User> _users = new();

        public AuthService()
        {
            InitializeDefaultUsers();
        }

        private void InitializeDefaultUsers()
        {
            // Demo users for testing
            _users.Add("admin@lksnetwork.io", new User
            {
                Email = "admin@lksnetwork.io",
                PasswordHash = HashPassword("admin123"),
                Role = "Admin",
                Name = "LKS Admin",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            _users.Add("validator@lksnetwork.io", new User
            {
                Email = "validator@lksnetwork.io",
                PasswordHash = HashPassword("validator123"),
                Role = "Validator",
                Name = "Validator User",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            _users.Add("user@lksnetwork.io", new User
            {
                Email = "user@lksnetwork.io",
                PasswordHash = HashPassword("user123"),
                Role = "User",
                Name = "Regular User",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        public AuthResult Login(string email, string password)
        {
            if (!_users.TryGetValue(email, out var user))
            {
                return new AuthResult
                {
                    Success = false,
                    Message = "Invalid email or password"
                };
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                return new AuthResult
                {
                    Success = false,
                    Message = "Invalid email or password"
                };
            }

            if (!user.IsActive)
            {
                return new AuthResult
                {
                    Success = false,
                    Message = "Account is disabled"
                };
            }

            var token = GenerateJwtToken(user);
            user.LastLoginAt = DateTime.UtcNow;

            return new AuthResult
            {
                Success = true,
                Token = token,
                User = new UserInfo
                {
                    Email = user.Email,
                    Name = user.Name,
                    Role = user.Role
                },
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };
        }

        public AuthResult Register(string email, string password, string name, string role = "User")
        {
            if (_users.ContainsKey(email))
            {
                return new AuthResult
                {
                    Success = false,
                    Message = "Email already exists"
                };
            }

            if (password.Length < 6)
            {
                return new AuthResult
                {
                    Success = false,
                    Message = "Password must be at least 6 characters"
                };
            }

            var user = new User
            {
                Email = email,
                PasswordHash = HashPassword(password),
                Name = name,
                Role = role,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _users.Add(email, user);

            var token = GenerateJwtToken(user);

            return new AuthResult
            {
                Success = true,
                Token = token,
                User = new UserInfo
                {
                    Email = user.Email,
                    Name = user.Name,
                    Role = user.Role
                },
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };
        }

        public bool ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_secretKey);

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public UserInfo? GetUserFromToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwt = tokenHandler.ReadJwtToken(token);

                var email = jwt.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
                if (email != null && _users.TryGetValue(email, out var user))
                {
                    return new UserInfo
                    {
                        Email = user.Email,
                        Name = user.Name,
                        Role = user.Role
                    };
                }
            }
            catch
            {
                // Token is invalid
            }

            return null;
        }

        public IEnumerable<UserInfo> GetAllUsers()
        {
            return _users.Values.Select(u => new UserInfo
            {
                Email = u.Email,
                Name = u.Name,
                Role = u.Role
            });
        }

        public bool UpdateUserRole(string email, string role)
        {
            if (_users.TryGetValue(email, out var user))
            {
                user.Role = role;
                return true;
            }
            return false;
        }

        public bool DeactivateUser(string email)
        {
            if (_users.TryGetValue(email, out var user))
            {
                user.IsActive = false;
                return true;
            }
            return false;
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.Name),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("userId", user.Email)
                }),
                Expires = DateTime.UtcNow.AddHours(24),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "LKS_SALT"));
            return Convert.ToBase64String(hashedBytes);
        }

        private bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
    }

    public class User
    {
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class UserInfo
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public UserInfo? User { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? Message { get; set; }
    }
}
