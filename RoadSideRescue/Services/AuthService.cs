using System.Threading.Tasks;

namespace RoadSideRescue.Services
{
    public class AuthService : IAuthService
    {
        public Task<string> LoginAsync(string email, string password)
        {
            // TODO: implement actual JWT logic
            return Task.FromResult("fake-jwt-token");
        }

        public Task RegisterAsync(string email, string password)
        {
            // TODO: implement registration logic
            return Task.CompletedTask;
        }
    }
}

