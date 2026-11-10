using BarcodeScanning;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using StoryCups.Contracts.Loyalty;
using StoryCups.Contracts.Profile;
using StoryCups.Mobile.Auth;
using StoryCups.Mobile.Scanning;
using StoryCups.Mobile.Services;
using StoryCups.RestClient;
using StoryCups.Shared.Services;

namespace StoryCups.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeScanning()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(Preferences.Default);
        builder.Services.AddSingleton(SecureStorage.Default);
        builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(15) });

        // Сесия с JWT и refresh токен (ADR-004); всичко минава през /api/v1.
        builder.Services.AddSingleton<ApiSettings>();
        builder.Services.AddSingleton<AuthSession>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<AuthenticationStateProvider, SessionAuthenticationStateProvider>();
        builder.Services.AddSingleton<IProfileService, HttpProfileService>();

        // Картата на клиента и действията на баристата: общите екрани от StoryCups.Shared през REST API-то.
        builder.Services.AddSingleton<IApiConnection, MobileApiConnection>();
        builder.Services.AddSingleton<ApiCaller>();
        builder.Services.AddSingleton<LoyaltyApiClient>();
        builder.Services.AddSingleton<ICustomerCardService>(services => services.GetRequiredService<LoyaltyApiClient>());
        builder.Services.AddSingleton<IBaristaService>(services => services.GetRequiredService<LoyaltyApiClient>());
        builder.Services.AddSingleton(new ClientUiOptions());

        // Скенер на баристата: native MAUI страница с камерата (ADR-003).
        builder.Services.AddSingleton<IScannerService, MauiScannerService>();
        builder.Services.AddSingleton<IQrScanner, MauiQrScanner>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
