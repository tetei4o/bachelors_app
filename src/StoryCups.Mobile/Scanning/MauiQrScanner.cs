using StoryCups.Shared.Services;

namespace StoryCups.Mobile.Scanning;

/// <summary>
/// Скенерът за общия екран на баристата (ADR-003): същата native страница с камерата като в spike-а.
/// </summary>
public sealed class MauiQrScanner(IScannerService scanner) : IQrScanner
{
    public bool HasCamera => true;

    public async Task<QrScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var outcome = await scanner.ScanAsync(cancellationToken);
        return outcome.Status switch
        {
            ScanStatus.Scanned => new QrScanResult(QrScanStatus.Scanned, outcome.Value),
            ScanStatus.PermissionDenied => new QrScanResult(QrScanStatus.PermissionDenied),
            _ => new QrScanResult(QrScanStatus.Cancelled),
        };
    }
}
