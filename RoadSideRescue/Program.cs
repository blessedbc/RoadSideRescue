using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RoadSideRescue.Data;
using RoadSideRescue.Hubs;
using RoadSideRescue.Models;
using RoadSideRescue.Services;
using System.Text;
using Microsoft.AspNetCore.Identity;
using RoadSideRescue.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// -------------------- CONFIGURATION --------------------
var configuration = builder.Configuration;
var environment = builder.Environment;

// -------------------- SERVICES CONFIGURATION --------------------

// Database
var connectionString = configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("DefaultConnection string is missing");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (IsPostgresConnection(connectionString))
        options.UseNpgsql(connectionString);
    else
        options.UseSqlite(connectionString);
});

// Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();


// Swagger & API Explorer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// JWT Authentication
AddJwtAuthentication(builder, configuration);

var app = builder.Build();

// -------------------- MIDDLEWARE --------------------

if (environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts(); // production security
}

app.UseHttpsRedirection();
app.UseCors("AllowLocalhost");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<RequestsHub>("/hubs/requests");

// -------------------- DATABASE MIGRATION & SEEDING --------------------

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("📦 Applying database migrations...");
        dbContext.Database.Migrate();
        logger.LogInformation("✅ Database migration applied.");

        if (environment.IsDevelopment())
        {
            logger.LogInformation("🌱 Seeding development database...");
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            await SeedDatabaseAsync(dbContext, passwordHasher, logger);
            logger.LogInformation("✅ Database seeding completed.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Database migration/seeding failed");
        throw;
    }
}

app.Run();

// -------------------- HELPER METHODS --------------------

static bool IsPostgresConnection(string connectionString)
{
    return connectionString.Contains("Host=") || connectionString.Contains("Server=");
}

static void AddJwtAuthentication(WebApplicationBuilder builder, IConfiguration configuration)
{
    var jwtKey = configuration["Jwt:Key"]
                 ?? throw new InvalidOperationException("JWT Key not configured");
    var jwtIssuer = configuration["Jwt:Issuer"]
                    ?? throw new InvalidOperationException("JWT Issuer not configured");
    var jwtAudience = configuration["Jwt:Audience"]
                      ?? throw new InvalidOperationException("JWT Audience not configured");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateLifetime = true
            };

            // Allow SignalR to receive JWT from query string
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/requests"))
                        context.Token = accessToken;
                    return Task.CompletedTask;
                }
            };
        });
}

static async Task SeedDatabaseAsync(ApplicationDbContext context, IPasswordHasher<User> passwordHasher, ILogger logger)
{
    // --- USERS ---
    var user = await context.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
    if (user == null)
    {
        user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = "test@example.com",
            Phone = "1234567890",
            Role = 0,
            RatingAverage = 0,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "Password123!");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        logger.LogInformation("✅ Sample user inserted.");
    }

    // --- AGENT ---
    var agent = await context.Agents.FirstOrDefaultAsync(a => a.UserId == user.Id);
    if (agent == null)
    {
        agent = new Agent
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Status = AgentStatus.Online,
            CurrentLat = -33.9249,
            CurrentLng = 18.4241,
            LastSeenAt = DateTime.UtcNow,
            ServiceRadiusMeters = 5000,
            SkillsJson = "[\"Towing\",\"Battery Jump\"]"
        };
        context.Agents.Add(agent);
        await context.SaveChangesAsync();
        logger.LogInformation("✅ Sample agent inserted.");
    }

    // --- REQUEST ---
    var request = await context.Requests.FirstOrDefaultAsync(r => r.OwnerId == user.Id);
    if (request == null)
    {
        request = new Request
        {
            Id = Guid.NewGuid(),
            OwnerId = user.Id,
            Status = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lat = -33.9258,
            Lng = 18.4232,
            Address = "Cape Town, South Africa",
            Description = "Car stuck on the road",
            VehicleType = "Sedan",
            Severity = 2,
            AssignedAgentId = agent.Id,
            EtaMinutes = 15,
            Photos = new List<string>()
        };
        context.Requests.Add(request);
        await context.SaveChangesAsync();
        logger.LogInformation("✅ Sample request inserted.");
    }

    // --- MESSAGE ---
    var message = await context.Messages.FirstOrDefaultAsync(m => m.RequestId == request.Id);
    if (message == null)
    {
        message = new Message
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            FromUserId = user.Id,
            Text = "Please help, my car is stuck!",
            CreatedAt = DateTime.UtcNow
        };
        context.Messages.Add(message);
        await context.SaveChangesAsync();
        logger.LogInformation("✅ Sample message inserted.");
    }
}

