using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StoryCups.Mobile.Auth;

namespace StoryCups.Mobile.Services;

/// <summary>
/// Заявки към API-то с Bearer токен.
/// </summary>
public sealed class ApiClient(HttpClient http, ApiSettings settings, AuthSession session)
{
    /// <summary>
    /// GET с токен. Връща null, ако потребителят не е влязъл или сървърът е отказал токена.
    /// </summary>
    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
        where T : class
    {
        var token = await session.GetAccessTokenAsync(cancellationToken);
        if (token is null)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, settings.Resolve(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await session.SignOutLocallyAsync();
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }
}
