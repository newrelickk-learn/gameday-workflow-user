namespace UserService.Application.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;

    /// <summary>
    /// 章クリアの引換券(章0: 影響を受けているPodを特定してログインできた)。
    /// frontendがgame-masterへクリアを記録するために使う。
    /// </summary>
    public List<string>? ChapterClearTokens { get; set; }
}

