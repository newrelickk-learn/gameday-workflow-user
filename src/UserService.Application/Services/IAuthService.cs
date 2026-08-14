using UserService.Application.DTOs;

namespace UserService.Application.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request);
}

