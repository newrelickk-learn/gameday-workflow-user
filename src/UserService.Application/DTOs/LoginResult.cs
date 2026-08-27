namespace UserService.Application.DTOs;

public enum LoginStatus
{
    Success,
    InvalidCredentials,

    PodSaturated,
}

public class LoginResult
{
    public LoginStatus Status { get; }
    public LoginResponse? Response { get; }

    public LoginResult(LoginStatus status, LoginResponse? response = null)
    {
        Status = status;
        Response = response;
    }
}
