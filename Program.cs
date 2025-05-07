using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prismon.Api.Data;
using Prismon.Api.Middleware;
using Prismon.Api.Services;
using System.Text;
using AspNetCoreRateLimit;
using Solnet.Rpc;
using Microsoft.Extensions.Options;
using DotNetEnv;
using Prismon.Api.Interface;
using Prismon.Api.Models;
using Prismon.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Load .env file for local development
Env.Load();

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

// Logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});

// Database
var isDevelopment = builder.Environment.IsDevelopment();
var connectionString = isDevelopment
    ? builder.Configuration.GetConnectionString("DefaultConnection")
    : Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING") ?? 
      builder.Configuration.GetConnectionString("Supabase");

builder.Services.AddDbContext<PrismonDbContext>(options =>
{
    if (isDevelopment)
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<PrismonDbContext>()
.AddDefaultTokenProviders();

// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? builder.Configuration["Jwt:Key"];
    if (string.IsNullOrEmpty(jwtKey))
    {
        throw new InvalidOperationException("JWT secret key is missing from configuration.");
    }
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

// Controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Prismon API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter JWT with 'Bearer ' prefix (e.g., 'Bearer eyJ...')",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter your API Key (e.g., 'prismon_dev123_app456')",
        Name = "X-API-Key",
        Type = SecuritySchemeType.ApiKey
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        }
    });
});

// Configuration Services
builder.Services.Configure<SolanaConfig>(builder.Configuration.GetSection("SolanaConfig"));
builder.Services.Configure<PaystackConfig>(builder.Configuration.GetSection("Paystack"));

// HTTP Clients
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IBlobStorageService, BlobStorageService>();
builder.Services.AddHttpClient<IPythPriceFeedService, PythPriceFeedService>();

// Services
var solanaConfig = builder.Configuration.GetSection("SolanaConfig").Get<SolanaConfig>();
var rpcUrl = Environment.GetEnvironmentVariable("SOLANA_RPC_URL") ?? 
             solanaConfig?.RpcUrl ?? 
             "https://api.devnet.solana.com"; 
if (string.IsNullOrEmpty(rpcUrl))
{
    throw new InvalidOperationException("Solana RPC URL is missing or invalid.");
}
builder.Services.AddSingleton<IRpcClient>(sp => 
    ClientFactory.GetClient(rpcUrl));
builder.Services.AddScoped<ISolanaService, SolanaService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAppService, AppService>();
builder.Services.AddScoped<IDeploymentService, DeploymentService>();
builder.Services.AddScoped<IUserOnboardingService, UserOnboardingService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IUserAuthService, UserAuthService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IPythPriceFeedService, PythPriceFeedService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

builder.Services.AddScoped<IEmailService>(sp =>
    new EmailService(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<EmailService>>()));
builder.Services.AddHostedService<ApiUsageCleanupService>();


// Caching
var redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") ?? 
                     builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "Prismon_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IClientPolicyStore, MemoryCacheClientPolicyStore>();

// SignalR and CORS
builder.Services.AddSignalR().AddMessagePackProtocol();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Miscellaneous
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

await app.ApplyMigrationsAsync<PrismonDbContext>();
// Startup Validation
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        scope.ServiceProvider.GetRequiredService<PrismonDbContext>();
        scope.ServiceProvider.GetRequiredService<IRpcClient>();
        scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
        scope.ServiceProvider.GetRequiredService<ISolanaService>();
        scope.ServiceProvider.GetRequiredService<IEmailService>();
        scope.ServiceProvider.GetRequiredService<IAIService>();

        logger.LogInformation("Dependency injection validated successfully");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Dependency injection validation failed");
        throw;
    }

    // Database Migration
    var dbContext = scope.ServiceProvider.GetRequiredService<PrismonDbContext>();
    try
    {
        if (builder.Environment.IsDevelopment())
        {
            dbContext.Database.EnsureCreated(); // SQLite: Create database
        }
        else
        {
            dbContext.Database.Migrate(); // Supabase: Apply migrations
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }
}

// Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");
app.UseMiddleware<RateLimitMiddleware>();
app.UseApiKeyAuthentication(); 
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();