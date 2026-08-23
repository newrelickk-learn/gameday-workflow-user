using UserService.Application.DTOs;

namespace UserService.Application.Services;

public interface IUserService
{
    Task<UserDto?> GetUserByIdAsync(string id);
    Task<IEnumerable<UserDto>> GetUsersByCompanyIdAsync(int companyId);
    Task<IEnumerable<UserDto>> GetUsersByIdsAsync(IEnumerable<string> ids);
    Task<UpdateManagerResult> UpdateManagerAsync(int actingCompanyId, string targetUserId, int? newManagerId);
}

