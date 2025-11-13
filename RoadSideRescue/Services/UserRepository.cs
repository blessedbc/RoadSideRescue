using RoadSideRescue.Data;
using RoadSideRescue.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace RoadSideRescue.Api.Services
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public UserRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _dbContext.Users
                                   .AsNoTracking()
                                   .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task AddAsync(User user)
        {
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
        }
    }
}
