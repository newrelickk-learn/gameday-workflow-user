using System.Collections.Concurrent;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using NewRelicAgent = NewRelic.Api.Agent.NewRelic;
using UserService.Application.DTOs;
using UserService.Infrastructure.Data.Repositories;
using UserService.Infrastructure.Services;

namespace UserService.Application.Services;

public class AuthService : IAuthService
{
    private static readonly ConcurrentDictionary<int, DateOnly> _podSaturationBypassedCompanies = new();

    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        IJwtService jwtService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _configuration = configuration;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user != null)
        {
            AddCustomAttribute("user.email", user.Email);
        }

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        var podSaturationRequired = RequiresPodSaturationCheck(user.CompanyId);
        var podSaturationBypassed = false;
        string? chapterClearToken = null;

        if (podSaturationRequired)
        {
            var podName = Environment.GetEnvironmentVariable("HOSTNAME");
            var impactedPodName = request.ImpactedPodName?.Trim();

            var matches = !string.IsNullOrEmpty(impactedPodName)
                && !string.IsNullOrEmpty(podName)
                && string.Equals(impactedPodName, podName, StringComparison.OrdinalIgnoreCase);

            if (!matches)
            {
                AddCustomAttribute("podSaturation.required", podSaturationRequired);
                AddCustomAttribute("podSaturation.bypassed", podSaturationBypassed);
                return new LoginResult(LoginStatus.PodSaturated);
            }

            podSaturationBypassed = true;

            if (user.CompanyId.HasValue)
            {
                _podSaturationBypassedCompanies[user.CompanyId.Value] = DateOnly.FromDateTime(DateTime.UtcNow);
                // 章0クリアはここからgame-masterを呼ばず、署名付きトークンをレスポンスに載せて
                // frontendからgame-masterへ記録してもらう(ログインのトレースにgame-masterを混ぜないため)。
                chapterClearToken = ChapterClearToken.Issue(
                    _configuration["InternalService:ApiKey"],
                    user.CompanyId.Value,
                    chapter: 0,
                    ChapterClearToken.DefaultTtl);
            }
        }

        var token = _jwtService.GenerateToken(user.Id, user.Email, user.Role);

        AddCustomAttribute("podSaturation.required", podSaturationRequired);
        AddCustomAttribute("podSaturation.bypassed", podSaturationBypassed);
        AddCustomAttribute("user.id", user.Id);
        AddCustomAttribute("user.role", user.Role);
        if (user.CompanyId.HasValue)
        {
            AddCustomAttribute("user.companyId", user.CompanyId.Value);
        }
        if (!string.IsNullOrEmpty(user.Department))
        {
            AddCustomAttribute("user.department", user.Department);
        }

        var response = new LoginResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id.ToString(),
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Department = user.Department
            },
            ChapterClearTokens = chapterClearToken != null ? new List<string> { chapterClearToken } : null
        };

        return new LoginResult(LoginStatus.Success, response);
    }

    private static void AddCustomAttribute(string name, object value)
    {
        NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute(name, value);
    }

    private bool RequiresPodSaturationCheck(int? companyId)
    {
        var podRole = _configuration["USER_POD_ROLE"];
        if (!string.Equals(podRole, "primary", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!companyId.HasValue)
        {
            return true;
        }

        if (companyId.Value is >= 101 and <= 120)
        {
            return false;
        }

        if (!_podSaturationBypassedCompanies.TryGetValue(companyId.Value, out var bypassedDate))
        {
            return true;
        }

        if (bypassedDate == DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return false;
        }

        _podSaturationBypassedCompanies.TryRemove(companyId.Value, out _);
        return true;
    }

    public int ResetPodSaturationBypass(int? companyId)
    {
        if (companyId.HasValue)
        {
            var removed = _podSaturationBypassedCompanies.TryRemove(companyId.Value, out _);
            return removed ? 1 : 0;
        }

        var count = _podSaturationBypassedCompanies.Count;
        _podSaturationBypassedCompanies.Clear();
        return count;
    }
}

