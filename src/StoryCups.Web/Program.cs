using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using StoryCups.Contracts;
using StoryCups.Contracts.Loyalty;
using StoryCups.Contracts.Profile;
using StoryCups.Domain.Identity;
using StoryCups.Infrastructure;
using StoryCups.Infrastructure.Auth;
using StoryCups.Infrastructure.Seeding;
using StoryCups.Shared.Services;
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

// Общите екрани „Карта“ и „Сканиране“ (ADR-003): в сайта услугите им викат Application слоя без HTTP.
builder.Services.AddSingleton<SiteRateLimiter>();
builder.Services.AddScoped<ICustomerCardService, ServerCustomerCardService>();
builder.Services.AddScoped<IBaristaService, ServerBaristaService>();
builder.Services.AddSingleton<IQrScanner, NoCameraQrScanner>();
builder.Services.AddSingleton(new ClientUiOptions { ShowQrTokenText = builder.Environment.IsDevelopment() });

// Кирилицата, „кавичките“, € и „è“ от менюто се извеждат като текст, а не като &#x...; ентитита.
builder.Services.AddWebEncoders(options =>
    options.TextEncoderSettings = new TextEncoderSettings(
        UnicodeRanges.BasicLatin,
        UnicodeRanges.Latin1Supplement,
        UnicodeRanges.GeneralPunctuation,
        UnicodeRanges.CurrencySymbols,
        UnicodeRanges.Cyrillic));

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
        .RequireAuthenticatedUser())
    .AddPolicy(ApiAuthorization.CustomerPolicy, policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireRole(AppRoles.Customer))
    .AddPolicy(ApiAuthorization.StaffPolicy, policy => policy
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireRole(AppRoles.Barista, AppRoles.Admin));

builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, context =>
    {
        var permits = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value.AuthPermitsPerMinute;
        return PerMinute(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", permits);
    });

    // Формите на страниците за акаунта (ADR-010): броят се само изпратените форми (POST), по IP адрес и общо
    // за всички страници. Отварянето на страница не се ограничава.
    options.AddPolicy(AccountRateLimiting.PolicyName, context =>
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }

        var permits = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value.AccountFormPostsPerMinute;
        return PerMinute(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", permits);
    });

    // Отказана заявка към API-то остава с код 429. Отказана форма от сайта се пренасочва към страница с
    // обяснение: празен отговор 429 би показал страницата „Не е намерено“.
    options.OnRejected = (context, _) =>
    {
        var http = context.HttpContext;
        if (!http.Request.Path.StartsWithSegments(ApiRoutes.Root))
        {
            http.Response.StatusCode = StatusCodes.Status303SeeOther;
            http.Response.Headers.Location = http.Request.PathBase + AccountRateLimiting.RejectedPath;
        }

        return ValueTask.CompletedTask;
    };

    // Издаването на QR токен се брои по клиент, а сканирането по бариста (ADR-005). Авторизацията вече е минала,
    // затова потребителят е известен; ключът на дяла е неговият Id от access токена.
    options.AddPolicy(QrEndpoints.IssueRateLimitPolicy, context =>
    {
        var permits = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value.QrTokensPerMinute;
        return PerMinute(UserPartition(context), permits);
    });
    options.AddPolicy(QrEndpoints.ScanRateLimitPolicy, context =>
    {
        var permits = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value.ScansPerMinute;
        return PerMinute(UserPartition(context), permits);
    });

    options.AddPolicy(LoyaltyEndpoints.ActionRateLimitPolicy, context =>
    {
        var permits = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value.ActionsPerMinute;
        return PerMinute(UserPartition(context), permits);
    });

    static string UserPartition(HttpContext context) =>
        context.User.TryGetUserId(out var userId)
            ? userId.ToString()
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    static RateLimitPartition<string> PerMinute(string partitionKey, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

static bool IsApi(HttpContext context) => context.Request.Path.StartsWithSegments(ApiRoutes.Root);

if (app.Environment.IsDevelopment())
{
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
app.MapQrEndpoints();
app.MapLoyaltyEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(StoryCups.Shared._Imports).Assembly);
app.MapAdditionalIdentityEndpoints();

await app.Services.InitializeDatabaseAsync();
await app.RunAsync();
