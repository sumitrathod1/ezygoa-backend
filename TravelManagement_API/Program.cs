using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using TravelManagement.API.Hubs;
using TravelManagement.API.Infrastructure;
using TravelManagement.BusinessLogicLayer.Services.Implementation;
using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Implementation;
using TravelManagement.DataAccessLayer.Repository.Interface;

var builder = WebApplication.CreateBuilder(args);

// Firebase initialization
var firebasePath = Path.Combine(Directory.GetCurrentDirectory(), "ezytravel-89107-firebase-adminsdk-fbsvc-5beb567f5d.json");
var firebaseJson = builder.Configuration["Firebase:AdminJson"];

try
{
    FirebaseApp.Create(new AppOptions()
    {
        Credential = !string.IsNullOrEmpty(firebaseJson)
            ? GoogleCredential.FromJson(firebaseJson)
            : GoogleCredential.FromFile(firebasePath)
    });
    Console.WriteLine("Firebase initialized successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"Firebase initialization failed: {ex.Message}");
}

// Controllers + JSON enum support
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// JWT Authentication
var jwtKey = builder.Configuration["JwtSettings:Key"];
if (string.IsNullOrEmpty(jwtKey))
    throw new Exception("JWT Key is missing. Please set JwtSettings:Key in appsettings.json.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/BookingHub"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TravelManagement API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Strict limit for auth endpoints (login, forgot-password, reset-password)
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // General API sliding window
    options.AddSlidingWindowLimiter("api", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 4;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// CORS — allow configured origins + local dev networks
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRCors", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)   // allow Capacitor, web, localhost, null-origin APK
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Database
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null);
            npgsql.CommandTimeout(60);
        }));

// SignalR
builder.Services.AddSignalR();

// Settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<EmailSmtpSettings>(builder.Configuration.GetSection("EmailSmtp"));

// Repositories (DAL)
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<ITravelAgentsRepository, TravelAgentsRepository>();
builder.Services.AddScoped<IEmailRepository, EmailRepository>();
builder.Services.AddScoped<IInquiryRepository, InquiryRepository>();

// Services (BLL)
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<ITravelAgentsService, TravelAgentsService>();
builder.Services.AddScoped<IInquiryService, InquiryService>();
builder.Services.AddScoped<IRateChartRepository, RateChartRepository>();
builder.Services.AddScoped<IRateChartService, RateChartService>();
builder.Services.AddScoped<ISalaryRepository, SalaryRepository>();
builder.Services.AddScoped<ISalaryService, SalaryService>();

// Infrastructure (API layer)
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<EmailBookingBackgroundService>();
builder.Services.AddHostedService<AutoSalaryHostedService>();
builder.Services.AddHostedService<AutoEMIHostedService>();

// QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
        Console.WriteLine("Database migrated successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration error: {ex.Message}");
    }
}

// Global exception handler must be first
app.UseGlobalExceptionHandler();

// Request logging
app.UseRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
c.SwaggerEndpoint("/swagger/v1/swagger.json", "TravelManagement API v1");
c.RoutePrefix = "swagger";
});

app.UseCors("SignalRCors");
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<BookingHub>("/BookingHub");

app.Run();