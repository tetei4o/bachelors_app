using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using StoryCups.Web.Api;

namespace StoryCups.Web.Services;

/// <summary>
/// Лимитите от API-то (NFR-02) за действията, които сайтът изпълнява през интерактивната връзка:
/// те не минават през HTTP middleware-а, затова се броят тук, по потребител и със същите стойности.
/// </summary>
internal sealed class SiteRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<(string Policy, Guid UserId)> limiter;

    public SiteRateLimiter(IOptions<RateLimitingOptions> options)
    {
        var limits = options.Value;
        limiter = PartitionedRateLimiter.Create<(string Policy, Guid UserId), (string Policy, Guid UserId)>(key =>
            RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = key.Policy switch
                {
                    QrEndpoints.IssueRateLimitPolicy => limits.QrTokensPerMinute,
                    QrEndpoints.ScanRateLimitPolicy => limits.ScansPerMinute,
                    LoyaltyEndpoints.ActionRateLimitPolicy => limits.ActionsPerMinute,
                    _ => throw new ArgumentOutOfRangeException(nameof(key), key.Policy, "Непозната политика."),
                },
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    }

    public bool TryAcquire(string policy, Guid userId)
    {
        using var lease = limiter.AttemptAcquire((policy, userId));
        return lease.IsAcquired;
    }

    public void Dispose() => limiter.Dispose();
}
