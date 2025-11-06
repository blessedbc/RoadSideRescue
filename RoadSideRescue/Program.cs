//using System.Text;
//using Microsoft.AspNetCore.Authentication.JwtBearer;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.IdentityModel.Tokens;
//using RoadSideRescue.Data;
//using RoadSideRescue.Hubs;
//using RoadSideRescue.Services;

//var builder = WebApplication.CreateBuilder(args);

//// Configuration helpers
//var configuration = builder.Configuration;

//// Register ApplicationDbContext with PostgreSQL (Npgsql)
//var connectionString = configuration.GetConnectionString("DefaultConnection")
//                       ?? throw new InvalidOperationException("DefaultConnection is not configured.");
//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseNpgsql(connectionString));

//// Add Auth service
//builder.Services.AddScoped<IAuthService, AuthService>();

//// Add SignalR
//builder.Services.AddSignalR();

//// Add controllers, Swagger
//builder.Services.AddControllers();
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

//// CORS policy for local frontend (adjust origins for production)
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowLocalhost", policy =>
//    {
//        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
//              .AllowAnyHeader()
//              .AllowAnyMethod()
//              .AllowCredentials();
//    });
//});

//// JWT Authentication
//var jwtKey = configuration["Jwt:Key"] ?? "dev-secret-key";
//var jwtIssuer = configuration["Jwt:Issuer"] ?? "RoadSideRescue";
//var jwtAudience = configuration["Jwt:Audience"] ?? "RoadSideRescueAudience";

//builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//    .AddJwtBearer(options =>
//    {
//        options.TokenValidationParameters = new TokenValidationParameters
//        {
//            ValidateIssuer = true,
//            ValidIssuer = jwtIssuer,
//            ValidateAudience = true,
//            ValidAudience = jwtAudience,
//            ValidateIssuerSigningKey = true,
//            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
//            ValidateLifetime = true
//        };

//        // Allow JWT via query string for SignalR websocket auth (access_token)
//        options.Events = new JwtBearerEvents
//        {
//            OnMessageReceived = context =>
//            {
//                var accessToken = context.Request.Query["access_token"];
//                var path = context.HttpContext.Request.Path;
//                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/requests"))
//                {
//                    context.Token = accessToken;
//                }
//                return Task.CompletedTask;
//            }
//        };
//    });

//var app = builder.Build();

//// Middleware pipeline
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();

//app.UseCors("AllowLocalhost");

//app.UseAuthentication();
//app.UseAuthorization();

//app.MapControllers();
//app.MapHub<RequestsHub>("/hubs/requests");

//app.Run();

using RoadSideRescue.Data;
using RoadSideRescue.Hubs;

public static class Program
{
    public static void Main(string[] args)
    {
        var app = CreateApp(args);
        app.Run();
    }

    private static WebApplication CreateApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureServices(builder);
        var app = builder.Build();
        ConfigureMiddleware(app);

        return app;
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                              ?? "Data Source=roadsiderescue.db";

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (connectionString.Contains("Host=") || connectionString.Contains("Server="))
                options.UseNpgsql(connectionString);
            else
                options.UseSqlite(connectionString);
        });

        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddSignalR();
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
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

        var jwtKey = configuration["Jwt:Key"] ?? "dev-secret-key";
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "RoadSideRescue";
        var jwtAudience = configuration["Jwt:Audience"] ?? "RoadSideRescueAudience";

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

    private static void ConfigureMiddleware(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseCors("AllowLocalhost");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<RequestsHub>("/hubs/requests");
    }
}

// Fix for ENC0088, ENC0004, ENC0033:
// These errors are caused by making edits to the body, modifiers, or top-level statements of the Program class or file while debugging with Edit and Continue (Hot Reload).
// The only way to resolve these is to stop debugging and restart the application.
// No code changes are required unless you want to avoid these errors in the future by:
//   - Avoiding edits to the Program class body, its modifiers, or top-level statements during a debug session.
//   - Refactoring code to minimize changes in Program.cs during debugging.

//
// To fix the current state, please stop debugging and restart the application.
// No code changes are needed in Program.cs.
//


