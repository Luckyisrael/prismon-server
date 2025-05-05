using Prismon.Api.DTOs;

namespace Prismon.Api.Interface;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
}

public class AuthResponse
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string DeveloperId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public bool IsOnboardingComplete { get; set; }
}