using UserService.Application.DTOs;

namespace UserService.Application.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
}

