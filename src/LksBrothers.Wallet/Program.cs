using LksBrothers.Wallet.Services;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddMemoryCache();

// Simple authentication - Google OAuth only
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Google:ClientId"] ?? "";
    options.ClientSecret = builder.Configuration["Google:ClientSecret"] ?? "";
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration["Jwt:Key"] ?? "super-secret-key-for-lks-wallet"))
    };
});

// Wallet services
builder.Services.AddSingleton<WalletService>();
builder.Services.AddSingleton<SimpleAuthService>();

var app = builder.Build();

// Minimal middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Serve the wallet UI
app.MapFallbackToFile("index.html");

app.Run();
