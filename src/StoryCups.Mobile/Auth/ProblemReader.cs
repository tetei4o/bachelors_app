using System.Net.Http.Json;
using System.Text.Json;

namespace StoryCups.Mobile.Auth;

/// <summary>
/// Чете съобщението за грешка от ProblemDetails на API-то.
/// </summary>
internal static class ProblemReader
{
    public static async Task<string> ReadMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (problem.ValueKind == JsonValueKind.Object && problem.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } text)
            {
                return text;
            }
        }
        catch (JsonException)
        {
        }

        return $"Сървърът отговори с код {(int)response.StatusCode}.";
    }
}
