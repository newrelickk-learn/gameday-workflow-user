using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.DTOs;
using UserService.Application.Services;

namespace UserService.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUserById(string id)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { error = "USER_NOT_FOUND", message = "指定されたユーザーIDのユーザーが見つかりません" });
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user");
            return StatusCode(500, new { error = "INTERNAL_SERVER_ERROR", message = "サーバーエラーが発生しました" });
        }
    }

    [HttpGet("{id}/manager")]
    public async Task<ActionResult<UserDto>> GetManagerByUserId(string id)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { error = "USER_NOT_FOUND", message = "指定されたユーザーIDのユーザーが見つかりません" });
            }

            if (user.ManagerId == null)
            {
                return NotFound(new { error = "MANAGER_NOT_FOUND", message = "承認者が見つかりません" });
            }

            var manager = await _userService.GetUserByIdAsync(user.ManagerId.Value.ToString());
            if (manager == null)
            {
                return NotFound(new { error = "MANAGER_NOT_FOUND", message = "承認者が見つかりません" });
            }

            return Ok(manager);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving manager");
            return StatusCode(500, new { error = "INTERNAL_SERVER_ERROR", message = "サーバーエラーが発生しました" });
        }
    }
}

