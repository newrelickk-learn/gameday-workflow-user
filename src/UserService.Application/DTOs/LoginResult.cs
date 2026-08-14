namespace UserService.Application.DTOs;

public enum LoginStatus
{
    Success,
    InvalidCredentials,

    // GameDay第0章: リソース飽和Podの特定待ち（正しいPod名が未提出）
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
