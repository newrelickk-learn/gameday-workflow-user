using UserService.Application.DTOs;
using UserService.Domain.Entities;
using UserService.Infrastructure.Data.Repositories;

namespace UserService.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto?> GetUserByIdAsync(string id)
    {
        if (!int.TryParse(id, out var userId))
        {
            return null;
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        return ToDto(user);
    }

    public async Task<IEnumerable<UserDto>> GetUsersByCompanyIdAsync(int companyId)
    {
        var users = await _userRepository.GetByCompanyIdAsync(companyId);
        return users.Select(ToDto);
    }

    public async Task<UpdateManagerResult> UpdateManagerAsync(int actingCompanyId, string targetUserId, int? newManagerId)
    {
        if (!int.TryParse(targetUserId, out var targetId))
        {
            return new UpdateManagerResult
            {
                Success = false,
                ErrorCode = "USER_NOT_FOUND",
                ErrorMessage = "指定されたユーザーIDのユーザーが見つかりません"
            };
        }

        var targetUser = await _userRepository.GetByIdAsync(targetId);
        if (targetUser == null)
        {
            return new UpdateManagerResult
            {
                Success = false,
                ErrorCode = "USER_NOT_FOUND",
                ErrorMessage = "指定されたユーザーIDのユーザーが見つかりません"
            };
        }

        if (targetUser.CompanyId != actingCompanyId)
        {
            return new UpdateManagerResult
            {
                Success = false,
                ErrorCode = "FORBIDDEN_COMPANY_MISMATCH",
                ErrorMessage = "他社のユーザーは編集できません"
            };
        }

        if (newManagerId.HasValue)
        {
            if (newManagerId.Value == targetId)
            {
                return new UpdateManagerResult
                {
                    Success = false,
                    ErrorCode = "INVALID_MANAGER",
                    ErrorMessage = "自分自身を直属の上長に設定することはできません"
                };
            }

            var managerUser = await _userRepository.GetByIdAsync(newManagerId.Value);
            if (managerUser == null || managerUser.CompanyId != actingCompanyId)
            {
                return new UpdateManagerResult
                {
                    Success = false,
                    ErrorCode = "INVALID_MANAGER",
                    ErrorMessage = "指定された直属の上長が見つからない、または他社のユーザーです"
                };
            }
        }

        targetUser.ManagerId = newManagerId;
        targetUser.UpdatedAt = DateTime.UtcNow;
        var updated = await _userRepository.UpdateAsync(targetUser);

        return new UpdateManagerResult
        {
            Success = true,
            User = ToDto(updated)
        };
    }

    private static UserDto ToDto(User user) => new UserDto
    {
        Id = user.Id.ToString(),
        Name = user.Name,
        Email = user.Email,
        Role = user.Role,
        Department = user.Department,
        CompanyId = user.CompanyId,
        ManagerId = user.ManagerId
    };
}
