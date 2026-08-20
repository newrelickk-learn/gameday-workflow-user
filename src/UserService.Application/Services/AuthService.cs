using System.Collections.Concurrent;
using System.Net.Http.Json;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewRelicAgent = NewRelic.Api.Agent.NewRelic;
using UserService.Application.DTOs;
using UserService.Infrastructure.Data.Repositories;
using UserService.Infrastructure.Services;

namespace UserService.Application.Services;

public class AuthService : IAuthService
{
    // GameDay第0章: 一度でも正しいPod名で突破した会社(CompanyId)は、突破したUTC日付の間だけ
    // Pod名の入力を求めない。UTC 0時を過ぎると（日付が変われば）失効し、再度Pod名の入力が必要になる。
    // Serviceのselectorがリソース飽和Pod(USER_POD_ROLE=primary)1台に固定されているため、
    // プロセス内メモリで持つだけで一貫性が保てる。
    private static readonly ConcurrentDictionary<int, DateOnly> _podSaturationBypassedCompanies = new();

    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IJwtService jwtService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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
                // GameDay第0章クリア。ログイン応答を遅延・失敗させないよう、
                // fire-and-forgetで通知する（失敗しても握りつぶし、ログインには影響しない）。
                _ = NotifyChapterClearedAsync(chapter: 0, companyId: user.CompanyId.Value);
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
            }
        };

        return new LoginResult(LoginStatus.Success, response);
    }

    // New Relicのカスタム属性を現在のトランザクションに付与する。プロファイラエージェントが
    // アタッチされていない場合はNewRelic.GetAgent()がno-op実装を返すため、常に安全に呼び出せる。
    private static void AddCustomAttribute(string name, object value)
    {
        NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute(name, value);
    }

    // このPod自身がリソース飽和役(USER_POD_ROLE=primary)で、かつこのユーザーの会社が
    // まだPod名を突破していない（またはUTCの日付が変わり失効した）場合のみ、チェックが必要
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

        if (!_podSaturationBypassedCompanies.TryGetValue(companyId.Value, out var bypassedDate))
        {
            return true;
        }

        if (bypassedDate == DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return false;
        }

        // UTCの日付が変わった。失効。次回ログイン時はまたPod名の入力が必要になる
        _podSaturationBypassedCompanies.TryRemove(companyId.Value, out _);
        return true;
    }

    // GameDay演習: 章クリアをapplication-approvalサービスのchapter_progressテーブルに
    // 記録する（サービス間通信用の内部API、既存のUSER_SERVICE_API_KEYと同じ共有鍵を使う）。
    // ログイン応答のクリティカルパスには影響させないため、呼び出し元でfire-and-forgetし、
    // 例外はここで完全に握りつぶす。
    private async Task NotifyChapterClearedAsync(int chapter, int companyId)
    {
        try
        {
            var baseUrl = _configuration["ApplicationApprovalService:BaseUrl"]
                ?? "http://gameday-workflow-application-approval:8002";
            var apiKey = _configuration["InternalService:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                return;
            }

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/api/v1/internal/chapters/{chapter}/mark-cleared",
                new { companyId = companyId.ToString() });

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "NotifyChapterClearedAsync: 章クリア通知が失敗しました。chapter={Chapter}, companyId={CompanyId}, status={Status}",
                    chapter, companyId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // ログイン処理自体には一切影響させない
            _logger.LogWarning(ex, "NotifyChapterClearedAsync: 章クリア通知中に例外が発生しました。chapter={Chapter}, companyId={CompanyId}", chapter, companyId);
        }
    }
}

