using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Domain.Entities;
using UserService.Infrastructure.Data.Repositories;
using UserService.Infrastructure.Services;
using Xunit;

namespace UserService.Api.Tests;

public class AuthServicePodSaturationTests
{
    private const string CorrectPassword = "password";
    private const string InternalApiKey = "test-internal-api-key";

    private static User BuildUser(int companyId) => new()
    {
        Id = 1,
        Name = "早坂",
        Email = "hayasaka.naoto@example.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword),
        Role = "engineer",
        CompanyId = companyId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static AuthService CreateAuthService(User user, string? podRole)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["USER_POD_ROLE"] = podRole,
                ["InternalService:ApiKey"] = InternalApiKey,
            })
            .Build();

        return new AuthService(
            new FakeUserRepository(user),
            new FakeJwtService(),
            configuration);
    }

    private static void SeedBypassedDate(int companyId, DateOnly date)
    {
        var field = typeof(AuthService).GetField(
            "_podSaturationBypassedCompanies",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var dict = (ConcurrentDictionary<int, DateOnly>)field.GetValue(null)!;
        dict[companyId] = date;
    }

    [Fact]
    public async Task Login_WhenPodRoleIsPrimary_AndPodNameMissing_ReturnsPodSaturated()
    {
        var companyId = NextCompanyId();
        var user = BuildUser(companyId);
        var authService = CreateAuthService(user, "primary");

        var result = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email,
            Password = CorrectPassword,
        });

        result.Status.Should().Be(LoginStatus.PodSaturated);
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task Login_WhenPodRoleIsPrimary_AndPodNameWrong_ReturnsPodSaturated()
    {
        Environment.SetEnvironmentVariable("HOSTNAME", "gameday-workflow-user-abc123");
        try
        {
            var companyId = NextCompanyId();
            var user = BuildUser(companyId);
            var authService = CreateAuthService(user, "primary");

            var result = await authService.LoginAsync(new LoginRequest
            {
                Email = user.Email,
                Password = CorrectPassword,
                ImpactedPodName = "gameday-workflow-user-standby-zzz999",
            });

            result.Status.Should().Be(LoginStatus.PodSaturated);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOSTNAME", null);
        }
    }

    [Fact]
    public async Task Login_WhenPodRoleIsPrimary_AndPodNameCorrect_ReturnsSuccess_CaseInsensitive()
    {
        Environment.SetEnvironmentVariable("HOSTNAME", "gameday-workflow-user-abc123");
        try
        {
            var companyId = NextCompanyId();
            var user = BuildUser(companyId);
            var authService = CreateAuthService(user, "primary");

            var result = await authService.LoginAsync(new LoginRequest
            {
                Email = user.Email,
                Password = CorrectPassword,
                ImpactedPodName = "  GAMEDAY-WORKFLOW-USER-ABC123  ",
            });

            result.Status.Should().Be(LoginStatus.Success);
            result.Response.Should().NotBeNull();
            result.Response!.Token.Should().NotBeNullOrEmpty();
            // 章0クリアはgame-masterを呼ばず、引換券をレスポンスに載せてfrontendに届けてもらう。
            result.Response.ChapterClearTokens.Should().ContainSingle();
            var token = result.Response.ChapterClearTokens![0];
            VerifySignature(token).Should().BeTrue();
            var payload = DecodePayload(token);
            payload.GetProperty("companyId").GetString().Should().Be(companyId.ToString());
            payload.GetProperty("chapter").GetInt32().Should().Be(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOSTNAME", null);
        }
    }

    [Fact]
    public async Task Login_AfterCompanyBypassed_SucceedsAgainWithoutPodName()
    {
        Environment.SetEnvironmentVariable("HOSTNAME", "gameday-workflow-user-abc123");
        try
        {
            var companyId = NextCompanyId();
            var user = BuildUser(companyId);
            var authService = CreateAuthService(user, "primary");

            var first = await authService.LoginAsync(new LoginRequest
            {
                Email = user.Email,
                Password = CorrectPassword,
                ImpactedPodName = "gameday-workflow-user-abc123",
            });
            first.Status.Should().Be(LoginStatus.Success);

            var secondAuthService = CreateAuthService(user, "primary");
            var second = await secondAuthService.LoginAsync(new LoginRequest
            {
                Email = user.Email,
                Password = CorrectPassword,
            });

            second.Status.Should().Be(LoginStatus.Success);
            // 当日すでにバイパス済みの再ログインでは、改めて章0のクリアを記録しない。
            second.Response!.ChapterClearTokens.Should().BeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("HOSTNAME", null);
        }
    }

    [Fact]
    public async Task Login_WhenBypassedOnAPreviousUtcDate_RequiresPodNameAgain()
    {
        var companyId = NextCompanyId();
        var user = BuildUser(companyId);
        SeedBypassedDate(companyId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));

        var authService = CreateAuthService(user, "primary");
        var result = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email,
            Password = CorrectPassword,
        });

        result.Status.Should().Be(LoginStatus.PodSaturated);
    }

    [Fact]
    public async Task Login_WhenBypassedOnTheSameUtcDate_DoesNotRequirePodNameAgain()
    {
        var companyId = NextCompanyId();
        var user = BuildUser(companyId);
        SeedBypassedDate(companyId, DateOnly.FromDateTime(DateTime.UtcNow));

        var authService = CreateAuthService(user, "primary");
        var result = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email,
            Password = CorrectPassword,
        });

        result.Status.Should().Be(LoginStatus.Success);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("standby")]
    public async Task Login_WhenPodRoleIsNotPrimary_NeverRequiresPodName(string? podRole)
    {
        var companyId = NextCompanyId();
        var user = BuildUser(companyId);
        var authService = CreateAuthService(user, podRole);

        var result = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email,
            Password = CorrectPassword,
        });

        result.Status.Should().Be(LoginStatus.Success);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(110)]
    [InlineData(120)]
    public async Task Login_WhenCompanyIdIsInTestCompanyRange_NeverRequiresPodName(int companyId)
    {
        var user = BuildUser(companyId);
        var authService = CreateAuthService(user, "primary");

        var result = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email,
            Password = CorrectPassword,
        });

        result.Status.Should().Be(LoginStatus.Success);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(121)]
    public async Task Login_WhenCompanyIdIsJustOutsideTestCompanyRange_StillRequiresPodName(int companyId)
    {
        var user = BuildUser(companyId);
        var authService = CreateAuthService(user, "primary");

        var result = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email,
            Password = CorrectPassword,
        });

        result.Status.Should().Be(LoginStatus.PodSaturated);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsInvalidCredentials_RegardlessOfPodRole()
    {
        var companyId = NextCompanyId();
        var user = BuildUser(companyId);
        var authService = CreateAuthService(user, "primary");

        var result = await authService.LoginAsync(new LoginRequest
        {
            Email = user.Email,
            Password = "wrong-password",
            ImpactedPodName = "gameday-workflow-user-abc123",
        });

        result.Status.Should().Be(LoginStatus.InvalidCredentials);
    }

    private static bool VerifySignature(string token)
    {
        var parts = token.Split('.');
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(InternalApiKey));
        var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.ASCII.GetBytes(parts[0])))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return parts.Length == 2 && parts[1] == expected;
    }

    private static JsonElement DecodePayload(string token)
    {
        var payloadB64 = token.Split('.')[0].Replace('-', '+').Replace('_', '/');
        payloadB64 = payloadB64.PadRight(payloadB64.Length + (4 - payloadB64.Length % 4) % 4, '=');
        return JsonDocument.Parse(Convert.FromBase64String(payloadB64)).RootElement;
    }

    private static int _companyIdCounter = 900_000;
    private static int NextCompanyId() => Interlocked.Increment(ref _companyIdCounter);

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly User _user;

        public FakeUserRepository(User user)
        {
            _user = user;
        }

        public Task<User?> GetByIdAsync(int id) =>
            Task.FromResult(id == _user.Id ? _user : null);

        public Task<User?> GetByEmailAsync(string email) =>
            Task.FromResult(string.Equals(email, _user.Email, StringComparison.OrdinalIgnoreCase) ? _user : null);

        public Task<IEnumerable<User>> GetAllAsync() =>
            Task.FromResult<IEnumerable<User>>(new[] { _user });

        public Task<IEnumerable<User>> GetByCompanyIdAsync(int companyId) =>
            Task.FromResult<IEnumerable<User>>(_user.CompanyId == companyId ? new[] { _user } : Array.Empty<User>());

        public Task<IEnumerable<User>> GetByIdsAsync(IEnumerable<int> ids) =>
            Task.FromResult<IEnumerable<User>>(ids.Contains(_user.Id) ? new[] { _user } : Array.Empty<User>());

        public Task<User> CreateAsync(User user) => Task.FromResult(user);

        public Task<User> UpdateAsync(User user) => Task.FromResult(user);

        public Task DeleteAsync(int id) => Task.CompletedTask;
    }

    private sealed class FakeJwtService : IJwtService
    {
        public string GenerateToken(int userId, string email, string role) => $"fake-token-{userId}";
    }
}
