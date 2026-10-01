namespace AuthBridge.DTOs;

public class AuthResponseDto
{
    public bool Succeeded { get; set; }

    public string? Token { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public string? UserId { get; set; }

    public string? Email { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public bool RequiresMfa { get; set; }

    public List<string> Errors { get; set; } = new();
}
