using System;
using System.Threading.Tasks;
using RoadSideRescue.Models;


namespace RoadSideRescue.Api.Services
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task AddAsync(User user);
    }
}
