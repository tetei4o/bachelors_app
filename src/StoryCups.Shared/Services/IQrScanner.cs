namespace StoryCups.Shared.Services;

public enum QrScanStatus
{
    Scanned,
    Cancelled,
    PermissionDenied,
}

public sealed record QrScanResult(QrScanStatus Status, string? Value = null);

/// <summary>
/// Скенерът на QR кодове зад общия екран на баристата (ADR-003). В приложението е камерата на телефона;
/// сайтът няма скенер и там кодът се въвежда като текст.
/// </summary>
public interface IQrScanner
{
    /// <summary>Има ли камера, която общият екран може да отвори.</summary>
    bool HasCamera { get; }

    Task<QrScanResult> ScanAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Реализация за среда без камера.
/// </summary>
public sealed class NoCameraQrScanner : IQrScanner
{
    public bool HasCamera => false;

    public Task<QrScanResult> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new QrScanResult(QrScanStatus.Cancelled));
}
