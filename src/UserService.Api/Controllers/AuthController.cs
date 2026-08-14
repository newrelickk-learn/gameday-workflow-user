using Microsoft.AspNetCore.Mvc;
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
}

