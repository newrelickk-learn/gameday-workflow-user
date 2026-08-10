using UserService.Application.DTOs;

namespace UserService.Application.Services;

public interface IUserService
{
    Task<UserDto?> GetUserByIdAsync(string id);
}

