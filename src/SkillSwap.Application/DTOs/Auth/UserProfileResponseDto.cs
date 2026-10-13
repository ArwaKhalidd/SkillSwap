namespace SkillSwap.Application.DTOs.Auth; // أو حسب مجلد الـ DTOs عندك

public sealed class UserProfileResponseDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}