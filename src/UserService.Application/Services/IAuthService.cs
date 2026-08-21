using UserService.Application.DTOs;

namespace UserService.Application.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request);

    /// <summary>
    /// GameDay第0章のPod飽和バイパス状態（メモリ上の突破済み会社リスト）をリセットする。
    /// companyIdを指定すればその会社のみ、nullなら全社をリセットする。
    /// Deploymentの再起動を伴わずに済ませるための運用用途。
    /// </summary>
    /// <returns>リセットした会社数</returns>
    int ResetPodSaturationBypass(int? companyId);
}

