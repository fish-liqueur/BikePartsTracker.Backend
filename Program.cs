using BikePartsTracker.Data;
using BikePartsTracker.Services;
using BikePartsTracker.BackgroundJobs;
using BikePartsTracker.Hubs;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using BikePartsTracker.Localization;
using BikePartsTracker.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Localization (ADR 0006): backend error-message catalog + culture resolution.
// Supported cultures: English (fallback), German, Russian, Ukrainian.
var supportedCultures = new[] { "en", "de", "ru", "uk" };
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("en")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
    // Per-request messages are driven by Accept-Language (ADR 0006 §E3); an unknown/malformed
    // locale is ignored and falls back to the default culture (en) — it never 500s.
    options.ApplyCurrentCultureToResponseHeaders = true;
});
builder.Services.AddScoped<ILocalizedErrorFactory, LocalizedErrorFactory>();

// Validation failures use the shared Problem Details envelope with a stable code (ADR 0006 §E1).
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Type = "about:blank",
            Title = "Bad Request",
            Status = StatusCodes.Status400BadRequest,
        };
        problemDetails.Extensions["code"] = ErrorCodes.CommonValidation;

        return new BadRequestObjectResult(problemDetails)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

// Add CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://localhost:5000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromSeconds(86400)); // Cache preflight for 24 hours
    });
    
    // Also add a default policy for development that's more permissive
    if (builder.Environment.IsDevelopment())
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://localhost:5000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    }
});

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "BikePartsTracker",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "BikePartsTracker",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "your-super-secret-key-with-at-least-32-characters"))
        };

        // SignalR JS client sends the JWT as access_token query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments(UpdatesHub.HubPath))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Register services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpClient<IStravaService, StravaService>();
builder.Services.AddScoped<IStravaIntegrationService, StravaIntegrationService>();
builder.Services.AddScoped<IUsagePeriodDistanceService, UsagePeriodDistanceService>();
builder.Services.AddScoped<IMaintenanceTaskEvaluationService, MaintenanceTaskEvaluationService>();
builder.Services.AddScoped<IMaintenanceTaskShadowPeriodService, MaintenanceTaskShadowPeriodService>();
builder.Services.AddScoped<IPartUsageTrackingService, PartUsageTrackingService>();
builder.Services.AddScoped<IFillEmptySlotsFaultInjector, NullFillEmptySlotsFaultInjector>();
builder.Services.AddScoped<IRideMutationResolver, RideMutationResolver>();
builder.Services.AddScoped<IRideImportService, RideImportService>();
builder.Services.AddScoped<IGapFillScheduler, GapFillScheduler>();
builder.Services.AddScoped<IBackgroundJobHandler, BackgroundJobHandler>();
builder.Services.AddSingleton<IBackgroundJobQueue, ChannelBackgroundJobQueue>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

// In-process worker (ADR-0001 MVP). Disabled in Testing so integration tests drain the queue explicitly.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<BackgroundJobWorker>();
}

// Register EF Core with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Bike Parts Tracker API", 
        Version = "v1",
        Description = "API for tracking bike parts and maintenance"
    });
    
    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
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
            new string[] {}
        }
    });
});

var app = builder.Build();

// Auto-apply pending migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline
// CORS must be the first middleware to handle preflight OPTIONS requests
app.UseCors("AllowFrontend");

// Resolve the request culture (from Accept-Language) before the error handler, so a thrown
// AppException localizes its detail in the caller's language as it unwinds (ADR 0006 §E1/§E3).
app.UseRequestLocalization();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bike Parts Tracker API V1");
    });
}

// Skip HTTPS redirection in Docker containers (where we typically only have HTTP)
// This prevents redirect loops when HTTPS is not configured
var isRunningInContainer = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"));
if (!isRunningInContainer)
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<UpdatesHub>(UpdatesHub.HubPath);

app.Run();

// Expose entry assembly to WebApplicationFactory for integration tests
public partial class Program { }
