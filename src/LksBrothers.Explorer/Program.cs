using LksBrothers.Explorer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Serilog;
using Microsoft.AspNetCore.HttpOverrides;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/lks-explorer-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { 
            Title = "LKS COIN Explorer API", 
            Version = "v1",
            Description = "Professional blockchain explorer API for LKS COIN mainnet"
        });
        c.AddSecurityDefinition("Bearer", new()
        {
            Description = "JWT Authorization header using the Bearer scheme",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
    });

    // Register custom services
    builder.Services.AddScoped<ExplorerService>();
    builder.Services.AddScoped<StateService>();
    builder.Services.AddHttpClient();
    builder.Services.AddScoped<BlockchainStatsService>();
    builder.Services.AddScoped<ValidatorService>();
    builder.Services.AddScoped<AnalyticsService>();
    builder.Services.AddScoped<SearchService>();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<CyberSecurityService>();
    builder.Services.AddHostedService<SecurityMonitoringService>();

    // Add Memory Cache
    builder.Services.AddMemoryCache();

    // Add JWT Authentication with environment variables
    var jwtKey = builder.Configuration["JwtSettings:SecretKey"] ?? 
                 Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ??
                 "LKS-BROTHERS-SECRET-KEY-FOR-JWT-TOKENS-2024-DEVELOPMENT";
    
    var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? 
                    Environment.GetEnvironmentVariable("JWT_ISSUER") ??
                    "LksBrothers.Explorer";
    
    var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? 
                      Environment.GetEnvironmentVariable("JWT_AUDIENCE") ??
                      "LksBrothers.Explorer";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();

    // Add Rate Limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("ApiPolicy", opt =>
        {
            opt.PermitLimit = 100;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 20;
        });
    });

    // Add CORS with environment configuration
    var allowedOrigins = builder.Configuration["CorsSettings:AllowedOrigins"]?.Split(',') ??
                        Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")?.Split(',') ??
                        new[] { "*" };

    builder.Services.AddCors(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.AddPolicy("DevelopmentPolicy", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        }
        else
        {
            options.AddPolicy("ProductionPolicy", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        }
    });

    // Add Health Checks
    builder.Services.AddHealthChecks();

    // Configure forwarded headers for production
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "LKS COIN Explorer API v1");
            c.RoutePrefix = "api-docs";
        });
        app.UseCors("DevelopmentPolicy");
    }
    else
    {
        app.UseForwardedHeaders();
        app.UseHsts();
        app.UseCors("ProductionPolicy");
    }

    // Security headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        
        if (!app.Environment.IsDevelopment())
        {
            var csp = builder.Configuration["Security:ContentSecurityPolicy"] ??
                     "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:;";
            context.Response.Headers.Add("Content-Security-Policy", csp);
        }
        
        await next();
    });

    app.UseHttpsRedirection();
    app.UseMiddleware<DDoSProtectionMiddleware>();
    app.UseMiddleware<SecurityMiddleware>();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseStaticFiles();
    
    // Add health check endpoint
    app.MapHealthChecks("/health");
    
    app.MapControllers().RequireRateLimiting("ApiPolicy");

    // Serve the demo explorer at root
    app.MapFallbackToFile("demo-explorer.html");

    Log.Information("LKS COIN Explorer starting up...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
