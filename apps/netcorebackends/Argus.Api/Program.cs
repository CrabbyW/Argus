using System.Text;
using Argus.Api.Configuration;
using Argus.Api.Database;
using Argus.Api.Middleware;
using Argus.Api.Services;
using log4net;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// --- Logging: log4net only, never Console.Write ---
XmlConfigurator.Configure(new FileInfo(Path.Combine(AppContext.BaseDirectory, "log4net.config")));
var logger = LogManager.GetLogger(typeof(Program));

// --- Configuration ---
var connectionString = builder.Configuration.GetConnectionString("ArgusDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'ArgusDatabase' is missing. Copy appsettings.Example.json to appsettings.Development.json.");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is missing or shorter than 32 characters. Set it in configuration or the ARGUS_JWT_SIGNINGKEY environment variable.");
}

// --- Persistence ---
builder.Services.AddDbContext<ArgusDbContext>(options => options.UseSqlServer(connectionString));

// --- Application services ---
builder.Services.AddScoped<IInstallationService, InstallationService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IAppRepositoryService, AppRepositoryService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();

// --- Authentication (username + password -> JWT; no Windows/Negotiate in Argus) ---
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// --- CORS for the Vite dev server ---
const string DevCorsPolicy = "ArgusDevCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Argus API",
        Version = "v1",
        Description = "Installation inventory - where is what installed."
    });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token returned by /api/auth/login."
    };

    options.AddSecurityDefinition("Bearer", scheme);

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer")] = new List<string>()
    });
});

var app = builder.Build();

// Must sit before everything else so it can catch their exceptions.
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(DevCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// --- Migrate + seed on startup so the demo runs from a single command ---
if (builder.Configuration.GetValue("Database:MigrateAndSeedOnStartup", true))
{
    // No fallback literal here on purpose: a password baked into the source is one that
    // ships to every checkout and quietly outlives every later change to configuration.
    var demoPassword = builder.Configuration["Seed:AdminPassword"]
        ?? throw new InvalidOperationException(
            "Seed:AdminPassword is missing. Set it in configuration, or turn off Database:MigrateAndSeedOnStartup.");
    await DbSeeder.MigrateAndSeedAsync(app.Services, demoPassword);
}

logger.Info("Argus API started.");

app.Run();
