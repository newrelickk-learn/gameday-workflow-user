using UserService.Application.DTOs;
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

        return new UserDto
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
}

