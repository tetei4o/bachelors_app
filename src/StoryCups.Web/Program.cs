using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using StoryCups.Contracts;
using StoryCups.Contracts.Profile;
using StoryCups.Infrastructure;
using StoryCups.Infrastructure.Auth;
using StoryCups.Infrastructure.Seeding;
using StoryCups.Web;
using StoryCups.Web.Api;
using StoryCups.Web.Components;
using StoryCups.Web.Components.Account;
using StoryCups.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Сайт: Blazor с интерактивен сървърен режим; страниците за акаунта са статичен SSR (ADR-001).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<IProfileService, ServerProfileService>();

// Кирилицата се извежда като текст, а не като &#x...; ентитита.
builder.Services.AddWebEncoders(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic));

builder.Services.AddInfrastructure(builder.Configuration);

// Две схеми (ADR-004): бисквитка за сайта по подразбиране и JWT Bearer само за /api.
var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});
authentication.AddIdentityCookies();
authentication.AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = jwt.Value.CreateValidationParameters();
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ApiAuthorization.PolicyName, policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser());

builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, context =>
    {
        var permits = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value.AuthPermitsPerMinute;
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var app = builder.Build();

static bool IsApi(HttpContext context) => context.Request.Path.StartsWithSegments(ApiRoutes.Root);

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.MapOpenApi();
}
else
{
    // API-то връща ProblemDetails, а сайтът показва страница за грешка.
    app.UseWhen(IsApi, api => api.UseExceptionHandler());
    app.UseWhen(context => !IsApi(context), site => site.UseExceptionHandler("/Error", createScopeForErrors: true));
    app.UseHsts();
}

app.UseWhen(
    context => !IsApi(context),
    site => site.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapAuthEndpoints();
app.MapProfileEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(StoryCups.Shared._Imports).Assembly);
app.MapAdditionalIdentityEndpoints();

await app.Services.InitializeDatabaseAsync();
await app.RunAsync();
