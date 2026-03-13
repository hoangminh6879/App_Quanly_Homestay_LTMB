using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using Nhom1.Data;
using Nhom1.Models;
using Nhom1.Services;
using Nhom1.Hubs;
using Serilog;
using System.Text;
using StackExchange.Redis;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/nhom1-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog();

// Add Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
    );
});

// Add Identity
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Add JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!))
    };
    // Allow JWT access token to be passed in query string for SignalR websocket requests
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"].ToString();
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/call"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Add CORS for Flutter/Mobile apps
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Controllers + Views (for Admin UI)
builder.Services.AddControllersWithViews();

// Keep manual ModelState handling in controllers (return our ApiResponse) instead of automatic 400
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

// Configure distributed cache and Data Protection with best-effort Redis. If Redis is not available
// at startup, fall back to an in-memory distributed cache so the app continues to function
// in developer environments (prevents 500s when Redis isn't running).
var redisConnection = builder.Configuration["Redis:Connection"] ?? "localhost:6379";
try
{
    // Try to establish a connection to Redis. If this succeeds, register the Redis-backed
    // IDistributedCache and persist DataProtection keys to Redis so multiple instances can
    // share keys.
    var multiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnection);

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
    });

    builder.Services.AddDataProtection()
        .PersistKeysToStackExchangeRedis(multiplexer, "DataProtection-Keys")
        .SetApplicationName("HomestayApp");

    Log.Information("Connected to Redis at {RedisConnection}. Using Redis for IDistributedCache and DataProtection.", redisConnection);
}
catch (Exception ex)
{
    // Redis is not available — fall back to in-memory distributed cache so requests that rely
    // on IDistributedCache (OTP, refresh tokens) don't throw. DataProtection will use the
    // default key storage (file system / DPAPI) in this case.
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddDataProtection().SetApplicationName("HomestayApp");
    Log.Warning(ex, "Redis at {RedisConnection} is unavailable. Falling back to in-memory distributed cache.", redisConnection);
}

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Homestay API",
        Version = "v1",
        Description = "API for Homestay Booking System - Flutter App Backend"
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IHomestayService, HomestayService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
// Promotion service should be registered before services that depend on it
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
// Conversation service for messaging (matching WebHS architecture)
builder.Services.AddScoped<IConversationService, ConversationService>();
// HttpClient factory (for PayPal)
builder.Services.AddHttpClient();
// PayPal service
builder.Services.AddScoped<IPayPalService, PayPalService>();
builder.Services.AddScoped<IChatAIService, ChatAIService>();
// Email service (SMTP - Gmail)
builder.Services.AddScoped<IMailService, SmtpMailService>();
// Add SignalR (for real-time signaling for calls)
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseHttpsRedirection();

// Enable Static Files (for serving images from wwwroot)
app.UseStaticFiles();

// Enable CORS (before authentication)
app.UseCors("AllowAll");

// Enable Swagger in all environments (for Conveyor testing)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Homestay API V1");
    c.RoutePrefix = "swagger"; // Swagger at /swagger
    c.DocumentTitle = "Homestay API Documentation";
});

// Use Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers and default route for MVC views
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

// Map SignalR hubs
app.MapHub<CallHub>("/hubs/call");

// Initialize database (using existing WebHS database)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        // Test connection
        if (context.Database.CanConnect())
        {
            Log.Information("Successfully connected to WebHS database");
        }
        else
        {
            Log.Warning("Cannot connect to database. Please check connection string.");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while connecting to the database");
    }
}
    // Ensure Admin role exists and optionally seed an Admin user from configuration
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<User>>();

            var adminRoleName = "Admin";
            if (!await roleManager.RoleExistsAsync(adminRoleName))
            {
                await roleManager.CreateAsync(new IdentityRole(adminRoleName));
            }

            var adminEmail = builder.Configuration["Admin:Email"]; // configure in appsettings.Development.json or env
            var adminPassword = builder.Configuration["Admin:Password"];
            if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
            {
                var adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser == null)
                {
                    adminUser = new User
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true,
                        FirstName = "Admin",
                        LastName = "User"
                    };

                    var result = await userManager.CreateAsync(adminUser, adminPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, adminRoleName);
                    }
                }
                else
                {
                    // Ensure role assigned
                    if (!await userManager.IsInRoleAsync(adminUser, adminRoleName))
                        await userManager.AddToRoleAsync(adminUser, adminRoleName);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while ensuring admin role/user");
        }
    }
Log.Information("Homestay API Server started successfully");
Log.Information("Swagger UI available at: https://localhost:5001 (or your Conveyor URL)");

app.Run();
