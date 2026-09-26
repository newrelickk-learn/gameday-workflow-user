using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UserService.Application.Services;

/// <summary>
/// 章クリアの引換券(HMAC署名付きトークン)。
///
/// ログインのトレースにgame-masterを混ぜないよう、このサービスからはgame-masterを呼ばず、
/// 「この会社がこの章を達成した」という署名付きトークンをログインレスポンスに載せるだけにする。
/// frontendがそれをgame-masterへ届けてクリアを記録する。
///
/// 形式はgame-master(SignedTokenService)・application-approval(signed_token.py)と揃えた
/// `base64url(JSON).base64url(HMAC-SHA256)`で、署名鍵はgame-masterのINTERNAL_API_KEYと同じ
/// InternalService:ApiKeyを使う。
/// </summary>
public static class ChapterClearToken
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public static string? Issue(string? key, int companyId, int chapter, TimeSpan ttl)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["companyId"] = companyId.ToString(),
            ["chapter"] = chapter,
            ["exp"] = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds(),
        });

        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(payloadB64));

        return $"{payloadB64}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] raw) =>
        Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
