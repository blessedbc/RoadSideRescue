using RoadSideRescue.Dto;

namespace RoadSideRescue.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse?> RegisterAsync(RegisterRequest request);
        Task<string?> LoginAsync(string email, string password);
    }
}
