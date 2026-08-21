namespace UserService.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Department { get; set; }
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
    public int? ManagerId { get; set; }
    // GameDay第1章の対象者（入社手続きの登録漏れでManagerIdがNULLの新人エンジニア）かどうか。
    // 各社slot15のみtrue。02-init-user.sqlで設定される。
    public bool IsChapter1Target { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

