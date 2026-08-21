using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewRelicAgent = NewRelic.Api.Agent.NewRelic;
using UserService.Application.DTOs;
using UserService.Application.Services;

namespace UserService.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute("login.result", result.Status.ToString());

            return result.Status switch
            {
                LoginStatus.Success => Ok(result.Response),
                LoginStatus.PodSaturated => StatusCode(503, new { error = "POD_SATURATED", message = "現在サーバーが高負荷のためログインできません" }),
                _ => Unauthorized(new { error = "INVALID_CREDENTIALS", message = "メールアドレスまたはパスワードが正しくありません" }),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { error = "INTERNAL_SERVER_ERROR", message = "サーバーエラーが発生しました" });
        }
    }

    /// <summary>
    /// [運用用途] GameDay第0章のPod飽和バイパス状態をリセットする。
    /// Deploymentの再起動（New Relicエージェント接続の切断・インスタンス入れ替えを伴う）を
    /// せずに済ませるための代替手段。X-API-Key認証のみで呼べる（ユーザーのJWTは不要）。
    /// companyIdを指定すればその会社のみ、省略すれば全社をリセットする。
    /// </summary>
    [HttpDelete("pod-saturation-bypass")]
    [Authorize(AuthenticationSchemes = "ApiKey")]
    public ActionResult ResetPodSaturationBypass([FromQuery] int? companyId)
    {
        var resetCount = _authService.ResetPodSaturationBypass(companyId);
        NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute("podSaturation.resetCount", resetCount);
        if (companyId.HasValue)
        {
            NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute("podSaturation.resetCompanyId", companyId.Value);
        }
        return Ok(new { reset = resetCount });
    }
}

