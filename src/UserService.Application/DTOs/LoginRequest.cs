namespace UserService.Application.DTOs;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // GameDay第0章: リソースがサチっているPodを突き止めた際に、そのPod名（k8sのHOSTNAME）を入力する欄。
    // 通常のログインでは不要（null/空でよい）。
    public string? ImpactedPodName { get; set; }
}

