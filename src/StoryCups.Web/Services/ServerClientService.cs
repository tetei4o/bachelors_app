using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using StoryCups.Application.Common;
using StoryCups.Contracts.Common;

namespace StoryCups.Web.Services;

/// <summary>
/// Основа на услугите зад общите екрани в сайта (ADR-003): същите use case-ове като REST API-то, но без HTTP.
/// Потребителят и ролята му се взимат от състоянието на входа при всяко извикване, не от екрана.
/// </summary>
internal abstract class ServerClientService(
    AuthenticationStateProvider authenticationStateProvider,
    IServiceScopeFactory scopeFactory,
    SiteRateLimiter rateLimiter)
{
    protected const string TooManyRequests = "too_many_requests";

    /// <param name="roles">Ролите, които имат право на действието.</param>
    /// <param name="rateLimitPolicy">Политиката от API-то, чийто лимит важи и тук; празно за четене.</param>
    protected async Task<ClientResult<T>> RunAsync<T>(
        string[] roles,
        string? rateLimitPolicy,
        Func<IServiceProvider, Guid, ClaimsPrincipal, Task<ClientResult<T>>> action)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = state.User;
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return ClientResult<T>.Failure(ClientErrorCodes.Unauthorized, "Сесията е изтекла. Влезте отново.");
        }

        if (!roles.Any(user.IsInRole))
        {
            return ClientResult<T>.Failure(ClientErrorCodes.Forbidden, "Профилът ви няма достъп до това действие.");
        }

        if (rateLimitPolicy is not null && !rateLimiter.TryAcquire(rateLimitPolicy, userId))
        {
            return ClientResult<T>.Failure(TooManyRequests, "Твърде много заявки. Изчакайте малко и опитайте отново.");
        }

        // Отделен scope за всяко извикване: интерактивната връзка живее дълго, а DbContext не бива.
        await using var scope = scopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider, userId, user);
    }

    protected static ClientResult<T> From<T>(Result<T> result) => result.IsSuccess
        ? ClientResult<T>.Success(result.Value!)
        : ClientResult<T>.Failure(result.Error!.Code, result.Error.Message);
}
