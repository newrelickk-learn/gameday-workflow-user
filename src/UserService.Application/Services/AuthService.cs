using System.Collections.Concurrent;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using UserService.Application.DTOs;
using UserService.Infrastructure.Data.Repositories;
using UserService.Infrastructure.Services;

namespace UserService.Application.Services;

public class AuthService : IAuthService
{
    // GameDay第0章: 一度でも正しいPod名で突破した会社(CompanyId)は、突破した時刻から
    // POD_SATURATION_BYPASS_HOURS（既定24時間）が経つまでPod名の入力を求めない。
    // Serviceのselectorがリソース飽和Pod(USER_POD_ROLE=primary)1台に固定されているため、
    // プロセス内メモリで持つだけで一貫性が保てる。
    private static readonly ConcurrentDictionary<int, DateTime> _podSaturationBypassedCompanies = new();

    private const int DefaultPodSaturationBypassHours = 24;

    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;

    public AuthService(IUserRepository userRepository, IJwtService jwtService, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _configuration = configuration;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        if (RequiresPodSaturationCheck(user.CompanyId))
        {
            var podName = Environment.GetEnvironmentVariable("HOSTNAME");
            var impactedPodName = request.ImpactedPodName?.Trim();

            var matches = !string.IsNullOrEmpty(impactedPodName)
                && !string.IsNullOrEmpty(podName)
                && string.Equals(impactedPodName, podName, StringComparison.OrdinalIgnoreCase);

            if (!matches)
            {
                return new LoginResult(LoginStatus.PodSaturated);
            }

            if (user.CompanyId.HasValue)
            {
                _podSaturationBypassedCompanies[user.CompanyId.Value] = DateTime.UtcNow;
            }
        }

        var token = _jwtService.GenerateToken(user.Id, user.Email, user.Role);

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
            }
        };

        return new LoginResult(LoginStatus.Success, response);
    }

    // このPod自身がリソース飽和役(USER_POD_ROLE=primary)で、かつこのユーザーの会社が
    // まだPod名を突破していない（または突破から一定時間が経ち失効した）場合のみ、チェックが必要
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

        if (!_podSaturationBypassedCompanies.TryGetValue(companyId.Value, out var bypassedAt))
        {
            return true;
        }

        var ttlHours = int.TryParse(_configuration["POD_SATURATION_BYPASS_HOURS"], out var configuredHours)
            ? configuredHours
            : DefaultPodSaturationBypassHours;

        if (DateTime.UtcNow - bypassedAt < TimeSpan.FromHours(ttlHours))
        {
            return false;
        }

        // 失効。次回ログイン時はまたPod名の入力が必要になる
        _podSaturationBypassedCompanies.TryRemove(companyId.Value, out _);
        return true;
    }
}

