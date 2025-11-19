using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RoadSideRescue.Api.Services;
using RoadSideRescue.Data;
using RoadSideRescue.Hubs;
using RoadSideRescue.Models;
using RoadSideRescue.Services;
using System.Text;
public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables();

        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets<Program>();
        }

        // CONFIGURATION

        var configuration = builder.Configuration;
        var environment = builder.Environment;

        // SERVICES CONFIGURATION

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

        // Register the interface from the Services.Interfaces namespace to match the controller's dependency
        builder.Services.AddScoped<RoadSideRescue.Services.Interfaces.IAuthService, AuthService>();
        builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddSignalR();
        builder.Services.AddControllers();
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<RoadSideRescue.Services.Interfaces.IRequestService, RoadSideRescue.Services.RequestService>();

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

        // Authorization policies (role-based)
        // Centralize policy registration here so controllers can reference policy names.
        builder.Services.AddAuthorization(options =>
        {
            // "role" claim contains UserRole enum text (e.g., "Agent")
            options.AddPolicy("AgentOnly", policy =>
                policy.RequireClaim("role", "Agent"));

            // Add Owner policy for future use if needed
            options.AddPolicy("OwnerOnly", policy =>
                policy.RequireClaim("role", "Owner"));
        });

        var app = builder.Build();

        // MIDDLEWARE

        if (environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        else
        {
            // Global production exception handler: log server-side and return generic problem details
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    var feature = context.Features.Get<IExceptionHandlerFeature>();
                    var ex = feature?.Error;

                    logger.LogError(ex, "Unhandled exception while processing request.");

                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/problem+json";

                    var problem = new
                    {
                        type = "https://httpstatuses.com/500",
                        title = "An unexpected error occurred.",
                        status = StatusCodes.Status500InternalServerError
                    };

                    await context.Response.WriteAsJsonAsync(problem);
                });
            });

            app.UseHsts(); // production security
        }

        app.UseHttpsRedirection();
        app.UseCors("AllowLocalhost");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<RequestsHub>("/hubs/requests");

        // DATABASE MIGRATION & SEEDING (migration gate)
        // Controlled via config:
        //  - Database:ApplyMigrations (bool?) => If not set: true in Development, false otherwise.
        //  - Database:FailOnPendingMigrations (bool) => If true and ApplyMigrations==false, abort startup when pending migrations exist.
        var applyMigrationsConfig = configuration.GetValue<bool?>("Database:ApplyMigrations");
        var applyMigrations = applyMigrationsConfig ?? environment.IsDevelopment();
        var failOnPending = configuration.GetValue<bool>("Database:FailOnPendingMigrations", false);

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                if (applyMigrations)
                {
                    logger.LogInformation("📦 Applying database migrations (Database:ApplyMigrations = true)...");
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
                else
                {
                    var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList();
                    if (pendingMigrations.Any())
                    {
                        logger.LogWarning("⚠️ Pending EF Core migrations detected but automatic migrations are disabled (Database:ApplyMigrations = false).");
                        logger.LogWarning("Pending migrations count: {count}", pendingMigrations.Count);

                        if (failOnPending)
                        {
                            logger.LogError("❌ Database start aborted because Database:FailOnPendingMigrations = true and migrations are pending.");
                            throw new InvalidOperationException("Pending EF Core migrations exist. Set Database:ApplyMigrations=true to apply migrations at startup or run migrations manually as part of your deployment.");
                        }
                    }
                    else
                    {
                        logger.LogInformation("No pending migrations and automatic migrations are disabled.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Database migration/seeding failed");
                throw;
            }
        }

        await app.RunAsync();
    }

    // HELPER METHODS

    static bool IsPostgresConnection(string connectionString)
    {
        return connectionString.Contains("Host=") || connectionString.Contains("Server=");
    }

    static void AddJwtAuthentication(WebApplicationBuilder builder, IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"]
                     ?? throw new InvalidOperationException("JWT Key not configured. Set it via __Manage User Secrets__ or an environment variable (Jwt__Key).");

        // Try to interpret configured key as Base64 first (common when storing binary keys),
        // fall back to UTF8 bytes when it's plain text.
        byte[] keyBytes;
        try
        {
            var decoded = Convert.FromBase64String(jwtKey);
            if (decoded != null && decoded.Length >= 1)
            {
                // Accept base64 only if it decodes to something sensible; otherwise fall back.
                keyBytes = decoded;
            }
            else
            {
                keyBytes = Encoding.UTF8.GetBytes(jwtKey);
            }
        }
        catch (FormatException)
        {
            // Not base64 — use UTF8 bytes
            keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        }

        if (keyBytes.Length < 16)
        {
            throw new InvalidOperationException(
                $"JWT Key must be at least 128 bits (16 bytes). Configured key length (bytes): {keyBytes.Length}. " +
                $"If you intended to use a base64 key, provide a base64-encoded value that decodes to >=16 bytes. " +
                $"For local development prefer __Manage User Secrets__ or set the environment variable Jwt__Key.");
        }

        var jwtIssuer = configuration["Jwt:Issuer"]
                        ?? throw new InvalidOperationException("JWT Issuer not configured");
        var jwtAudience = configuration["Jwt:Audience"]
                          ?? throw new InvalidOperationException("JWT Audience not configured");

        var signingKey = new SymmetricSecurityKey(keyBytes);

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
                    IssuerSigningKey = signingKey,
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
        // USERS

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

        // AGENT

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

        // REQUEST

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

        // MESSAGE

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

    // Helper used in temp debug (add below)
    static bool TryBase64Decode(string s, out byte[] bytes)
    {
        try { bytes = Convert.FromBase64String(s); return true; }
        catch { bytes = Array.Empty<byte>(); return false; }
    }
}
