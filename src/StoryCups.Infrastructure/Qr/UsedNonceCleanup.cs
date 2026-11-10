using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StoryCups.Infrastructure.Persistence;

namespace StoryCups.Infrastructure.Qr;

/// <summary>
/// Изтрива nonce на изтекли токени (ADR-005). След изтичането им токенът се спира от проверката на срока,
/// затова редът вече не е нужен и таблицата остава малка.
/// </summary>
public sealed class UsedNonceCleaner(AppDbContext db, TimeProvider timeProvider)
{
    public Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        return db.UsedQrNonces.Where(nonce => nonce.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
    }
}

/// <summary>
/// Пуска <see cref="UsedNonceCleaner"/> през равни интервали, докато сървърът работи.
/// </summary>
internal sealed class UsedNonceCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<QrOptions> options,
    TimeProvider timeProvider,
    ILogger<UsedNonceCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.NonceCleanupInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var deleted = await scope.ServiceProvider.GetRequiredService<UsedNonceCleaner>().DeleteExpiredAsync(stoppingToken);
                    if (deleted > 0)
                    {
                        logger.LogInformation("Изтрити са {Count} изтекли nonce.", deleted);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Неуспешно почистване не спира сървъра; следващият интервал опитва отново.
                    logger.LogWarning(ex, "Почистването на изтеклите nonce не успя.");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
