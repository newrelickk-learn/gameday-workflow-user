namespace UserService.Application.DTOs;

public class UpdateManagerResult
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public UserDto? User { get; set; }
}
