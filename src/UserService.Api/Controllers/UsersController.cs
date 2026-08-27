using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewRelicAgent = NewRelic.Api.Agent.NewRelic;
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
            NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute("user.id", id);

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

    [HttpGet("batch")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsersByIds([FromQuery] List<string> ids)
    {
        try
        {
            if (ids == null || ids.Count == 0)
            {
                return Ok(Enumerable.Empty<UserDto>());
            }

            NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute("user.batchRequestedCount", ids.Count);

            var users = (await _userService.GetUsersByIdsAsync(ids)).ToList();
            NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute("user.batchFoundCount", users.Count);

            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users batch");
            return StatusCode(500, new { error = "INTERNAL_SERVER_ERROR", message = "サーバーエラーが発生しました" });
        }
    }

    [HttpGet("{id}/manager")]
    public async Task<ActionResult<UserDto>> GetManagerByUserId(string id)
    {
        try
        {
            NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute("user.id", id);

            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { error = "USER_NOT_FOUND", message = "指定されたユーザーIDのユーザーが見つかりません" });
            }

            var managerFound = user.ManagerId != null;
            NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute("user.managerFound", managerFound);

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

    [HttpGet("company")]
    [Authorize(Roles = "hr")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetCompanyUsers()
    {
        try
        {
            var caller = await GetCallerAsync();
            if (caller == null)
            {
                return Unauthorized(new { error = "UNAUTHORIZED", message = "認証情報からユーザーを特定できません" });
            }

            if (caller.CompanyId == null)
            {
                return StatusCode(403, new { error = "COMPANY_NOT_SET", message = "所属企業が設定されていません" });
            }

            var users = await _userService.GetUsersByCompanyIdAsync(caller.CompanyId.Value);
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company users");
            return StatusCode(500, new { error = "INTERNAL_SERVER_ERROR", message = "サーバーエラーが発生しました" });
        }
    }

    [HttpPatch("{id}/manager")]
    [Authorize(Roles = "hr")]
    public async Task<ActionResult<UserDto>> UpdateManager(string id, [FromBody] UpdateManagerRequestDto request)
    {
        try
        {
            NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute("user.id", id);
            if (request.ManagerId.HasValue)
            {
                NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute("user.newManagerId", request.ManagerId.Value);
            }

            var caller = await GetCallerAsync();
            if (caller == null)
            {
                return Unauthorized(new { error = "UNAUTHORIZED", message = "認証情報からユーザーを特定できません" });
            }

            if (caller.CompanyId == null)
            {
                return StatusCode(403, new { error = "COMPANY_NOT_SET", message = "所属企業が設定されていません" });
            }

            var result = await _userService.UpdateManagerAsync(caller.CompanyId.Value, id, request.ManagerId);
            NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute(
                "user.updateManagerResult",
                result.Success ? "Success" : (result.ErrorCode ?? "UNKNOWN_ERROR"));

            if (!result.Success)
            {
                return result.ErrorCode switch
                {
                    "USER_NOT_FOUND" => NotFound(new { error = result.ErrorCode, message = result.ErrorMessage }),
                    "FORBIDDEN_COMPANY_MISMATCH" => StatusCode(403, new { error = result.ErrorCode, message = result.ErrorMessage }),
                    "INVALID_MANAGER" => BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage }),
                    _ => StatusCode(500, new { error = "INTERNAL_SERVER_ERROR", message = "サーバーエラーが発生しました" }),
                };
            }

            return Ok(result.User);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating manager");
            return StatusCode(500, new { error = "INTERNAL_SERVER_ERROR", message = "サーバーエラーが発生しました" });
        }
    }

    private async Task<UserDto?> GetCallerAsync()
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (callerId == null)
        {
            return null;
        }

        var caller = await _userService.GetUserByIdAsync(callerId);
        if (caller != null)
        {
            NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute("caller.id", caller.Id);
            if (caller.CompanyId.HasValue)
            {
                NewRelicAgent.GetAgent().CurrentTransaction.AddCustomAttribute("caller.companyId", caller.CompanyId.Value);
            }
        }

        return caller;
    }
}
