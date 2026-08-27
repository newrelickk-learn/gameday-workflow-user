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
    options.Limits.MaxRequestBodySize = builder.Configuration.GetValue<long>("Kestrel:Limits:MaxRequestBodySize", 10 * 1024 * 1024);
});

var minWorkerThreads = builder.Configuration.GetValue<int>("ThreadPool:MinWorkerThreads", 0);
var maxWorkerThreads = builder.Configuration.GetValue<int>("ThreadPool:MaxWorkerThreads", 1000);
var minCompletionPortThreads = builder.Configuration.GetValue<int>("ThreadPool:MinCompletionPortThreads", 0);
var maxCompletionPortThreads = builder.Configuration.GetValue<int>("ThreadPool:MaxCompletionPortThreads", 1000);

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var baseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var maxPoolSize = builder.Configuration.GetValue<int>("Database:MaxPoolSize", 200);
var minPoolSize = builder.Configuration.GetValue<int>("Database:MinPoolSize", 10);
var connectionLifetime = builder.Configuration.GetValue<int>("Database:ConnectionLifetime", 0);

var connectionString = $"{baseConnectionString};Maximum Pool Size={maxPoolSize};Minimum Pool Size={minPoolSize};Connection Lifetime={connectionLifetime};";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddHttpClient();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService.Application.Services.UserService>();
builder.Services.AddScoped<IJwtService, JwtService>();

builder.Services.AddHostedService<CpuSaturationService>();

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
        if (context.Request.Headers.ContainsKey("X-API-Key"))
        {
            return "ApiKey";
        }
        return "JwtBearer";
    };
});

builder.Services.AddAuthorization();

if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
    {
        options.RedirectStatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status307TemporaryRedirect;
        options.HttpsPort = null;
    });
}

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }

