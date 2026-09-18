using System.Text;
using System.Text.Json.Serialization;
using CoonMeeting.Dashboard.Api.Auth;
using CoonMeeting.Dashboard.Api.Config;
using CoonMeeting.Dashboard.Api.Models;
using CoonMeeting.Dashboard.Api.Repositories;
using CoonMeeting.Dashboard.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var databaseSettings = builder.Configuration.GetSection("Database").Get<DatabaseSettings>()
    ?? new DatabaseSettings();
builder.Services.AddSingleton(databaseSettings);
builder.Services.AddSingleton<LiteDbContext>();

builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddSingleton<IPendingInviteRepository, PendingInviteRepository>();
builder.Services.AddSingleton<IMeetingJoinLinkRepository, MeetingJoinLinkRepository>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<ISessionTokenService, SessionTokenService>();
builder.Services.AddSingleton<IInviteTokenService, InviteTokenService>();
builder.Services.AddSingleton<IInviteEmailSender, SmtpInviteEmailSender>();
builder.Services.AddSingleton<IGuestJoinEmailSender, SmtpGuestJoinEmailSender>();

var corsSettings = builder.Configuration.GetSection("Cors").Get<CorsSettings>() ?? new CorsSettings();
builder.Services.AddSingleton(corsSettings);

// Capture startup errors so a missing secret returns a clean 500 instead of leaking a stack
// trace - same convention as Coon.Meeting's own Program.cs for the same class of failure.
string? startupError = null;
SessionSettings sessionSettings;
CoonMeetingSettings coonMeetingSettings;
FrontendSettings frontendSettings;
SmtpSettings smtpSettings;

try
{
    sessionSettings = builder.Configuration.GetSection("Session").Get<SessionSettings>()
        ?? throw new InvalidOperationException("Session settings not configured.");
    coonMeetingSettings = builder.Configuration.GetSection("CoonMeeting").Get<CoonMeetingSettings>()
        ?? throw new InvalidOperationException("CoonMeeting settings not configured.");
    frontendSettings = builder.Configuration.GetSection("Frontend").Get<FrontendSettings>()
        ?? throw new InvalidOperationException("Frontend settings not configured.");
    smtpSettings = builder.Configuration.GetSection("Smtp").Get<SmtpSettings>()
        ?? throw new InvalidOperationException("Smtp settings not configured.");

    // A hardcoded default session-signing key would let anyone mint a valid session for any
    // user; failing to start is strictly better. The Coon.Meeting admin key is the same story
    // one layer down - without it, signup can never provision a real tenant. Smtp:Password is
    // required starting with Phase 2 - org invites now actually send email.
    RequireSecret(sessionSettings.SigningKey, "Session:SigningKey", "Session__SigningKey");
    RequireSecret(coonMeetingSettings.AdminProvisioningKey, "CoonMeeting:AdminProvisioningKey", "CoonMeeting__AdminProvisioningKey");
    RequireSecret(coonMeetingSettings.ApiBaseUrl, "CoonMeeting:ApiBaseUrl", "CoonMeeting__ApiBaseUrl"); // not a secret, but nothing here works without it either
    RequireSecret(frontendSettings.BaseUrl, "Frontend:BaseUrl", "Frontend__BaseUrl");
    RequireSecret(smtpSettings.Password, "Smtp:Password", "Smtp__Password");

    static void RequireSecret(string value, string configKey, string envVar)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{configKey} is not configured. Set the {envVar} environment variable " +
                "(or use dotnet user-secrets in development).");
        }
    }
}
catch (Exception ex)
{
    startupError = "The server is not correctly configured and cannot handle requests.";
    Console.WriteLine($"Startup Configuration Error: {ex.Message}\n{ex.StackTrace}");
    sessionSettings = new SessionSettings();
    coonMeetingSettings = new CoonMeetingSettings { ApiBaseUrl = "http://localhost" }; // HttpClient's BaseAddress rejects an empty/invalid URI
    frontendSettings = new FrontendSettings();
    smtpSettings = new SmtpSettings();
}

builder.Services.AddSingleton(sessionSettings);
builder.Services.AddSingleton(coonMeetingSettings);
builder.Services.AddSingleton(frontendSettings);
builder.Services.AddSingleton(smtpSettings);

builder.Services.AddHttpClient<ICoonMeetingClient, CoonMeetingClient>();

// Static allow-list, not the dynamic per-tenant CORS Coon.Meeting itself uses - this API has
// exactly one product's worth of browser callers (its own frontend, plus any standalone app
// built on coon-meeting-sdk that calls the public /api/v1/guest endpoints directly), not many
// integrators' worth.
//
// Reads Frontend:BaseUrl straight from configuration rather than frontendSettings.BaseUrl - the
// catch block below unconditionally resets frontendSettings to a blank instance whenever ANY
// required secret is missing, even one unrelated to it (e.g. Session:SigningKey). Building the
// CORS allow-list from the post-reset object meant a missing, unrelated secret silently dropped
// the admin frontend's own origin from CORS too - exactly what broke signup in production.
const string CorsPolicyName = "DashboardFrontends";
var allowedOrigins = new[] { builder.Configuration["Frontend:BaseUrl"] ?? string.Empty }.Concat(corsSettings.Parse())
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .Distinct()
    .ToArray();
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddAuthentication(DashboardSessionScheme.SchemeName)
    .AddJwtBearer(DashboardSessionScheme.SchemeName, options =>
    {
        options.MapInboundClaims = false; // keep claim types exactly as minted: "sub", "email", "name", "organizationId", "role"
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = sessionSettings.Issuer,
            ValidAudience = sessionSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                string.IsNullOrEmpty(sessionSettings.SigningKey) ? "startup-error-placeholder-key-not-used" : sessionSettings.SigningKey)),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Coon.Meeting Dashboard API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your session token.",
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coon.Meeting Dashboard API v1"));
}

// CORS has to run before ANYTHING that can short-circuit the pipeline with its own response -
// including the startup-error middleware right below. Otherwise a misconfigured deployment's
// 500 goes out with no CORS headers at all, and the browser reports a CORS failure instead of
// showing the real error - exactly what happened in production before this was reordered.
app.UseCors(CorsPolicyName);

// Startup Error Middleware - returns 500 for every request if a required secret was missing.
app.Use(async (context, next) =>
{
    if (!string.IsNullOrEmpty(startupError))
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(startupError);
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
