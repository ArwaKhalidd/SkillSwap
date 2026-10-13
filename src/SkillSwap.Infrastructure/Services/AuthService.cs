using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Auth;
using SkillSwap.Infrastructure.Identity;

namespace SkillSwap.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IApplicationDbContext _context;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        IApplicationDbContext context)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _context = context;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        // Create new application user
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new Exception($"User registration failed: {errors}");
        }

        // Retrieve user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Generate JWT token
        var token = await _jwtTokenService.GenerateTokenAsync(user.Id, user.Email!, roles, cancellationToken);

        // Return authentication response
        return new AuthResponseDto
        {
            UserId = user.Id,
            Email = user.Email!,
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        // Find user by email
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || user.IsDeleted)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // Verify password
        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValidPassword)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // Retrieve user roles and generate token
        var roles = await _userManager.GetRolesAsync(user);
        var token = await _jwtTokenService.GenerateTokenAsync(user.Id, user.Email!, roles, cancellationToken);

        return new AuthResponseDto
        {
            UserId = user.Id,
            Email = user.Email!,
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };
    }

    public async Task<UserProfileResponseDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        return new UserProfileResponseDto
        {
            Id = user.Id,
            Email = user.Email!,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }

    public async Task<UserProfileResponseDto> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || user.IsDeleted)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        // تحديث البيانات (لو الـ ApplicationUser فيه خاصية زي DisplayName)
        // user.DisplayName = request.DisplayName;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new Exception($"Failed to update profile: {errors}");
        }

        return new UserProfileResponseDto
        {
            Id = user.Id,
            Email = user.Email!,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }
}