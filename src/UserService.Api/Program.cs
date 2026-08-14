using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UserService.Api.Authentication;
using UserService.Api.Services;
using UserService.Application.Services;
using UserService.Infrastructure.Data;
using UserService.Infrastructure.Data.Repositories;
using UserService.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Kestrelの設定（環境変数で設定可能）
builder.WebHost.ConfigureKestrel(options =>
{
    var maxConcurrentConnections = builder.Configuration.GetValue<int>("Kestrel:Limits:MaxConcurrentConnections", 1000);
    var maxConcurrentUpgradedConnections = builder.Configuration.GetValue<int>("Kestrel:Limits:MaxConcurrentUpgradedConnections", 1000);
    var keepAliveTimeout = builder.Configuration.GetValue<int>("Kestrel:Limits:KeepAliveTimeoutSeconds", 120);
    var requestHeadersTimeout = builder.Configuration.GetValue<int>("Kestrel:Limits:RequestHeadersTimeoutSeconds", 30);

    options.Limits.MaxConcurrentConnections = maxConcurrentConnections;
    options.Limits.MaxConcurrentUpgradedConnections = maxConcurrentUpgradedConnections;
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(keepAliveTimeout);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(requestHeadersTimeout);
    options.Limits.MaxRequestBodySize = builder.Configuration.GetValue<long>("Kestrel:Limits:MaxRequestBodySize", 10 * 1024 * 1024); // 10MB
});

// スレッドプールの設定（環境変数で設定可能）
var minWorkerThreads = builder.Configuration.GetValue<int>("ThreadPool:MinWorkerThreads", 0);
var maxWorkerThreads = builder.Configuration.GetValue<int>("ThreadPool:MaxWorkerThreads", 1000);
var minCompletionPortThreads = builder.Configuration.GetValue<int>("ThreadPool:MinCompletionPortThreads", 0);
var maxCompletionPortThreads = builder.Configuration.GetValue<int>("ThreadPool:MaxCompletionPortThreads", 1000);

// 0の場合は、プロセッサ数に基づいて自動設定
if (minWorkerThreads == 0)
{
    minWorkerThreads = Environment.ProcessorCount * 2;
}
if (minCompletionPortThreads == 0)
{
    minCompletionPortThreads = Environment.ProcessorCount * 2;
}

ThreadPool.SetMinThreads(minWorkerThreads, minCompletionPortThreads);
ThreadPool.SetMaxThreads(maxWorkerThreads, maxCompletionPortThreads);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database接続文字列の構築（環境変数で設定可能）
var baseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? 
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var maxPoolSize = builder.Configuration.GetValue<int>("Database:MaxPoolSize", 200);
var minPoolSize = builder.Configuration.GetValue<int>("Database:MinPoolSize", 10);
var connectionLifetime = builder.Configuration.GetValue<int>("Database:ConnectionLifetime", 0);

var connectionString = $"{baseConnectionString};Maximum Pool Size={maxPoolSize};Minimum Pool Size={minPoolSize};Connection Lifetime={connectionLifetime};";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService.Application.Services.UserService>();
builder.Services.AddScoped<IJwtService, JwtService>();

// GameDay第0章: USER_POD_ROLE=primary のPodのみ内部でCPU負荷を発生させる（詳細はサービス内を参照）
builder.Services.AddHostedService<CpuSaturationService>();

// JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "UserService";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "UserService";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "JwtOrApiKey";
    options.DefaultChallengeScheme = "JwtOrApiKey";
})
.AddJwtBearer("JwtBearer", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
    };
})
.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", options => { })
.AddPolicyScheme("JwtOrApiKey", "JwtOrApiKey", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // X-API-Keyヘッダーがある場合はAPI Key認証を使用
        if (context.Request.Headers.ContainsKey("X-API-Key"))
        {
            return "ApiKey";
        }
        // それ以外はJWT認証を使用
        return "JwtBearer";
    };
});

builder.Services.AddAuthorization();

// HTTPSリダイレクトの設定（開発環境では無効化して警告を抑制）
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
    {
        options.RedirectStatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status307TemporaryRedirect;
        options.HttpsPort = null; // HTTPSポートをnullに設定して警告を抑制
    });
}

var app = builder.Build();

// Health check endpoint - ミドルウェアチェーンの前に配置して認証をバイパス
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTPSリダイレクトは本番環境でのみ有効化（開発環境ではHTTPのみを使用）
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make Program class accessible for WebApplicationFactory
public partial class Program { }

