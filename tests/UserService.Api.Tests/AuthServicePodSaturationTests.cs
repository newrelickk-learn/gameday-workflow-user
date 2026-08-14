using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Domain.Entities;
using UserService.Infrastructure.Data.Repositories;
using UserService.Infrastructure.Services;
using Xunit;

namespace UserService.Api.Tests;

/// <summary>
/// GameDay第0章: USER_POD_ROLE=primary のときだけ、正しいPod名(HOSTNAME)を提出しないとログインできない、
/// かつ一度突破した会社(CompanyId)はその後Pod名なしでもログインできる、という挙動のテスト。
/// </summary>
public class AuthServicePodSaturationTests
{
    private const string CorrectPassword = "password";

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
            })
            .Build();

        return new AuthService(new FakeUserRepository(user), new FakeJwtService(), configuration);
    }

    // AuthService内部のstatic「突破済み会社→突破したUTC日付」辞書に、テストからだけ直接書き込む
    // （実装は本番のUTC日付比較ロジックそのままにしつつ、「前日に突破済み」の状態を再現するため）
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

            // 同じ会社の別ユーザーが、Pod名なしで再ログインしても通る（新しいAuthServiceインスタンスでも突破済みは共有される）
            var secondAuthService = CreateAuthService(user, "primary");
            var second = await secondAuthService.LoginAsync(new LoginRequest
            {
                Email = user.Email,
                Password = CorrectPassword,
            });

            second.Status.Should().Be(LoginStatus.Success);
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
        // 前日のUTC日付で突破済みだったことにする
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
        // 今日のUTC日付で既に突破済みだったことにする
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

    // 会社(CompanyId)ごとの突破済みフラグはstatic(プロセス内共有)なので、テスト間で衝突しないよう
    // テストごとに異なるCompanyIdを振る。
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

        public Task<User> CreateAsync(User user) => Task.FromResult(user);

        public Task<User> UpdateAsync(User user) => Task.FromResult(user);

        public Task DeleteAsync(int id) => Task.CompletedTask;
    }

    private sealed class FakeJwtService : IJwtService
    {
        public string GenerateToken(int userId, string email, string role) => $"fake-token-{userId}";
    }
}
