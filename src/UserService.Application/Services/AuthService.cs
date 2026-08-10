using BCrypt.Net;
using UserService.Application.DTOs;
using UserService.Infrastructure.Data.Repositories;
using UserService.Infrastructure.Services;

namespace UserService.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;

    public AuthService(IUserRepository userRepository, IJwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        var token = _jwtService.GenerateToken(user.Id, user.Email, user.Role);

        return new LoginResponse
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
    }
}

