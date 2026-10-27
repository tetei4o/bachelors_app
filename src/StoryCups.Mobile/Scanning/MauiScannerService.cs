using BarcodeScanning;

namespace StoryCups.Mobile.Scanning;

/// <summary>
/// Показва <see cref="ScannerPage"/> модално върху BlazorWebView и чака резултата.
/// </summary>
public sealed class MauiScannerService : IScannerService
{
    public async Task<ScanOutcome> ScanAsync(CancellationToken cancellationToken = default)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (!await Methods.AskForRequiredPermissionAsync())
            {
                return new ScanOutcome(ScanStatus.PermissionDenied);
            }

            var navigation = Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation
                ?? throw new InvalidOperationException("Няма активен прозорец.");
            var page = new ScannerPage();
            await navigation.PushModalAsync(page);
            return await page.Result.WaitAsync(cancellationToken);
        });
    }
}
