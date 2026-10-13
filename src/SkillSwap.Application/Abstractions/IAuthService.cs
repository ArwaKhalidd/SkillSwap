using SkillSwap.Application.DTOs.Auth;

namespace SkillSwap.Application.Abstractions; 

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<UserProfileResponseDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserProfileResponseDto> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default);


}