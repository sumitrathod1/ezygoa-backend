using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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
using TravelManagement.Core.Common;
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

// Compress JSON responses (calendar, booking lists, logos) — much smaller on mobile data
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);

// Controllers + JSON enum support
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        // Never send password hashes / reset tokens to clients
        options.JsonSerializerOptions.TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
        {
            Modifiers = { SensitiveJsonFields.Hide }
        };
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
                // Both hubs authenticate via ?access_token= (WebSockets can't send headers).
                // notificationHub was missing here, so its connections were anonymous.
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/BookingHub") || path.StartsWithSegments("/notificationHub")))
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
    // Limits are per client. They used to be single global buckets, so 5 logins per
    // minute (and 100 API calls per minute) were shared by EVERY user of the platform —
    // a handful of drivers signing in together would start getting 429s.
    static string ClientIp(HttpContext ctx) =>
        ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? ctx.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    // Strict limit for auth endpoints (login, forgot-password, reset-password) — per IP
    options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ClientIp(ctx), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        }));

    // General API — per signed-in user, falling back to IP
    options.AddPolicy("api", ctx => RateLimitPartition.GetSlidingWindowLimiter(
        ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value is { } uid
            ? $"user:{uid}" : $"ip:{ClientIp(ctx)}",
        _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 4,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10,
        }));

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

// Audit trail for financial records (scoped: needs the request's user/org)
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>())
           .UseNpgsql(
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
builder.Services.AddScoped<IDispatchRepository, DispatchRepository>();
builder.Services.AddScoped<ITripRepository, TripRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
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

// Multi-tenancy
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContext>();

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
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
        Console.WriteLine("Database migrated successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration error: {ex.Message}");
    }

    // ── Keep ID sequences ahead of existing data ─────────────────────────
    // Rows inserted with explicit IDs (imports, restores, HasData seeds) don't move the
    // identity sequence, so the next normal insert reuses a taken ID and fails with
    // 23505 "duplicate key ... PK_<Table>". Only ever moves a sequence forward.
    try
    {
        db.Database.ExecuteSqlRaw(@"
DO $$
DECLARE r record; seq text; mx bigint; lv bigint; called boolean;
BEGIN
  FOR r IN
    SELECT c.table_name, c.column_name
    FROM information_schema.columns c
    JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name
    WHERE c.table_schema = 'public' AND t.table_type = 'BASE TABLE'
      AND (c.is_identity = 'YES' OR c.column_default LIKE 'nextval(%')
  LOOP
    seq := pg_get_serial_sequence(format('%I.%I', 'public', r.table_name), r.column_name);
    CONTINUE WHEN seq IS NULL;
    EXECUTE format('SELECT COALESCE(MAX(%I), 0) FROM %I.%I', r.column_name, 'public', r.table_name) INTO mx;
    EXECUTE format('SELECT last_value, is_called FROM %s', seq) INTO lv, called;
    -- next value handed out would be lv (+1 if already called); make sure it is > mx
    IF mx > 0 AND (CASE WHEN called THEN lv ELSE lv - 1 END) < mx THEN
      PERFORM setval(seq, mx, true);
      RAISE NOTICE 'Sequence % moved to %', seq, mx;
    END IF;
  END LOOP;
END $$;");
        Console.WriteLine("ID sequences checked");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Sequence check skipped: {ex.Message}");
    }

    // ── Instagram image folder ──────────────────────────────────────────
    var igPath = app.Configuration["IG_IMAGES_PATH"] ?? "/data/ig";
    Directory.CreateDirectory(igPath);
    Console.WriteLine($"Instagram images folder: {igPath}");

    // ── Seed default Organisation (OrgId = 1) ───────────────────────────
    if (!db.Organizations.Any())
    {
        db.Organizations.Add(new Organization
        {
            Name      = "EzyGoa",
            Code      = "EZYGOA",
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        Console.WriteLine("Default organisation 'EzyGoa' created (OrgId=1)");
    }

    // ── Seed SuperAdmin (OrgId = 0) ─────────────────────────────────────
    // Guard must match the (UserName, OrgId) unique index, otherwise an existing
    // 'superadmin' row with a different role made this insert fail with 23505 and
    // crashed the API at startup.
    if (!db.Users.Any(u => u.Role == Role.SuperAdmin
                        || (u.UserName == "superadmin" && u.OrgId == 0)))
    {
        try
        {
            var superPwd = builder.Configuration["SuperAdmin:Password"] ?? "SuperAdmin@123";
            db.Users.Add(new User
            {
                EmployeeName = "Super Admin",
                UserName     = "superadmin",
                Password     = PasswordHasher.HashPassword(superPwd),
                Role         = Role.SuperAdmin,
                OrgId        = 0,          // not tied to any org
                Status       = true,
                Number       = "0000000000",
            });
            db.SaveChanges();
            Console.WriteLine("SuperAdmin created. Use the Change Password option after first login.");
        }
        catch (DbUpdateException ex)
        {
            // Never take the whole API down over seeding
            Console.WriteLine($"SuperAdmin seed skipped: {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}

// Global exception handler must be first
app.UseGlobalExceptionHandler();

app.UseResponseCompression();

// Request logging
app.UseRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
c.SwaggerEndpoint("/swagger/v1/swagger.json", "TravelManagement API v1");
c.RoutePrefix = "swagger";
});

// Serve /ig/* from the persistent image folder (public, no auth)
var igServePath = app.Configuration["IG_IMAGES_PATH"] ?? "/data/ig";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(igServePath),
    RequestPath  = "/ig"
});

app.UseCors("SignalRCors");
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRateLimiter();      // after authentication so limits can be partitioned per user
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<BookingHub>("/BookingHub");

app.Run();