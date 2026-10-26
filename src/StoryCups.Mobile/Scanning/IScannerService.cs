namespace StoryCups.Mobile.Scanning;

public enum ScanStatus
{
    Scanned,
    Cancelled,
    PermissionDenied,
}

/// <summary>
/// Резултат от едно сканиране. Elapsed е от включването на камерата до разпознаването (критерий на spike-а, ADR-003).
/// </summary>
public sealed record ScanOutcome(ScanStatus Status, string? Value = null, TimeSpan Elapsed = default);

/// <summary>
/// Отваря камерата и връща първия разпознат QR код.
/// </summary>
public interface IScannerService
{
    Task<ScanOutcome> ScanAsync(CancellationToken cancellationToken = default);
}
