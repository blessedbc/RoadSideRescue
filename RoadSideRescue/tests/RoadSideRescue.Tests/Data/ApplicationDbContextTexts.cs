using Microsoft.EntityFrameworkCore;
using RoadSideRescue.Data;
using RoadSideRescue.Models;
using Xunit;


namespace RoadSideRescue.Api.Tests.Data
{
    public class ApplicationDbContextTests
    {
        [Fact]
        public async Task CanInsertUserIntoDatabase()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb")
                .Options;

            using var context = new ApplicationDbContext(options);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "John Doe",
                Email = "john@example.com"
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var saved = await context.Users.FirstOrDefaultAsync(u => u.Email == "john@example.com");

            Assert.NotNull(saved);
            Assert.Equal("John Doe", saved.Name);
        }
    }
}



