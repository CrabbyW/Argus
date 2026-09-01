using System.Text;
using Argus.Api.Configuration;
using Argus.Api.Database;
using Argus.Api.Database.Interceptors;
using Argus.Api.Middleware;
using Argus.Api.Services;
using log4net;
using log4net.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Negotiate;
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
builder.Services.Configure<AuditLogOptions>(builder.Configuration.GetSection(AuditLogOptions.SectionName));
builder.Services.Configure<WindowsAuthOptions>(builder.Configuration.GetSection(WindowsAuthOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is missing or shorter than 32 characters. Set it in configuration or the ARGUS_JWT_SIGNINGKEY environment variable.");
}

// --- Persistence ---
// The journal interceptor is scoped like the context it hangs on, because the username it records
// belongs to the request.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<EntityJournalInterceptor>();

builder.Services.AddDbContext<ArgusDbContext>((serviceProvider, options) => options
    .UseSqlServer(connectionString)
    .AddInterceptors(serviceProvider.GetRequiredService<EntityJournalInterceptor>()));

// --- Application services ---
builder.Services.AddScoped<IInstallationService, InstallationService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IAppRepositoryService, AppRepositoryService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEntityJournalService, EntityJournalService>();

// Writes to log4net and holds no state of its own.
builder.Services.AddSingleton<ILoginAuditLog, LoginAuditLog>();

// Stateless and file-backed, so one instance serves every request.
builder.Services.AddSingleton<ILogFileService, LogFileService>();

// Log files are written by log4net and expired by this: an age rule in days, which
// log4net's file-count rolling cannot express.
builder.Services.AddHostedService<LogRetentionService>();

// --- Authentication ---
// Two ways in, one kind of session: both the password form and the Windows handshake end at a
// JWT, so every endpoint but `/api/auth/windows-login` only ever sees a bearer token.
//
// Negotiate is registered unconditionally because an endpoint's authentication scheme is fixed at
// startup, while `WindowsAuth:Enabled` is a setting; the switch is enforced in AuthController,
// which refuses the endpoint and hides the button when it is off. Registering the handler costs
// nothing until something actually negotiates against it.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddNegotiate()
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
        .AllowAnyMethod()
        // The Windows handshake travels on the request itself, so the browser only sends it when
        // credentials are allowed. Safe next to WithOrigins — it is the wildcard origin that
        // credentials may not be combined with.
        .AllowCredentials());
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

// Outermost, so the status it records is the one the client actually received — including
// the 500 the exception handler below turns an unhandled exception into.
app.UseMiddleware<ActionAuditLoggingMiddleware>();

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
    // How many installations the demo grid should hold. The seeder only ever tops the table up
    // to this number, so lowering it later deletes nothing; 0 keeps just the hand-written rows.
    var demoInstallationCount = builder.Configuration.GetValue("Seed:InstallationCount", 200);
    await DbSeeder.MigrateAndSeedAsync(app.Services, demoPassword, demoInstallationCount);
}

logger.Info("Argus API started.");

app.Run();
