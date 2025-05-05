using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prismon.Api.Data;
//using Prismon.Api.Hubs;
using Prismon.Api.Interface;
using Prismon.Api.Middleware;
using Prismon.Api.Models;
using Prismon.Api.Services;
using System.Text;
using AspNetCoreRateLimit;
using Solnet.Rpc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add detailed logging for DI and configuration diagnostics
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});

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

builder.Services.AddDbContext<PrismonDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<PrismonDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

// Config Services
builder.Services.Configure<SolanaConfig>(builder.Configuration.GetSection("SolanaConfig"));
builder.Services.Configure<EmailConfig>(builder.Configuration.GetSection("EmailConfig"));

// Solana RPC Client with diagnostics
builder.Services.AddSingleton<IRpcClient>(sp => ClientFactory.GetClient(sp.GetRequiredService<IOptions<SolanaConfig>>().Value.RpcUrl));

// HTTP Client registrations
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IBlobStorageService, BlobStorageService>();
builder.Services.AddHttpClient<IPythPriceFeedService, PythPriceFeedService>();

// Services
builder.Services.AddScoped<ISolanaService, SolanaService>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSignalR(); // Add SignalR
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAppService, AppService>();
builder.Services.AddScoped<IDeploymentService, DeploymentService>();
builder.Services.AddScoped<IUserOnboardingService, UserOnboardingService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IUserAuthService, UserAuthService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Register BlobStorageService
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<IPythPriceFeedService, PythPriceFeedService>();

// Register AIService once and only once
builder.Services.AddScoped<IAIService, AIService>();

//builder.Services.AddDataProtection()
   // .PersistKeysToFileSystem(new DirectoryInfo("/app/dataprotection-keys"))
    //.SetApplicationName("Prismon");
//builder.Services.AddScoped<ITransactionMonitoringService, TransactionMonitoringService>();

/* uncomment after hackathon */
var redisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") ?? builder.Configuration["Redis:ConnectionString"];
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
    // Fallback to in-memory cache if Redis is not configured
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddHostedService<ApiUsageCleanupService>();

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();

builder.Services.AddSingleton<IClientPolicyStore, MemoryCacheClientPolicyStore>();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
//builder.Services.AddScoped<IClientResolver, ApiKeyClientResolver>();
//builder.Services.AddDbContext<PrismonDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSignalR().AddMessagePackProtocol();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:8080")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Validate DI at startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        scope.ServiceProvider.GetRequiredService<IRpcClient>();
        scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
        scope.ServiceProvider.GetRequiredService<ISolanaService>();
        scope.ServiceProvider.GetRequiredService<IEmailService>();
        scope.ServiceProvider.GetRequiredService<IAIService>(); // Validate AIService resolution

        logger.LogInformation("Dependency injection validated successfully");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Dependency injection validation failed");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<RateLimitMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowLocalhost");
app.UseAuthentication();
app.UseApiKeyAuthentication();
app.UseAuthorization();
app.MapControllers();
//app.MapHub<TransactionHub>("/hub/transactions");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PrismonDbContext>();
    try
    {
        dbContext.Database.EnsureCreated();
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }
}

app.Run();