using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using StoryCups.Contracts;
using StoryCups.Contracts.Common;

namespace StoryCups.RestClient;

/// <summary>
/// Една заявка към REST API-то с Bearer токен. Не хвърля изключения за очаквани откази: връща
/// <see cref="ClientResult{T}"/> с кода от ProblemDetails или с код за грешка при клиента (няма връзка, няма вход).
/// </summary>
public sealed class ApiCaller(HttpClient http, IApiConnection connection)
{
    public const string TooManyRequests = "too_many_requests";

    /// <param name="idempotencyKey">Отива в заглавието Idempotency-Key; при повторен опит се подава същият (ADR-008).</param>
    public async Task<ClientResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body = null,
        Guid? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await connection.GetAccessTokenAsync(cancellationToken);
            if (token is null)
            {
                return ClientResult<T>.Failure(ClientErrorCodes.Unauthorized, "Влезте отново.");
            }

            using var request = new HttpRequestMessage(method, connection.Resolve(path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType());
            }

            if (idempotencyKey is not null)
            {
                request.Headers.Add(ApiHeaders.IdempotencyKey, idempotencyKey.Value.ToString());
            }

            using var response = await http.SendAsync(request, cancellationToken);
            switch (response.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    await connection.OnUnauthorizedAsync();
                    return ClientResult<T>.Failure(ClientErrorCodes.Unauthorized, "Сесията е изтекла. Влезте отново.");
                case HttpStatusCode.Forbidden when !IsProblem(response):
                    return ClientResult<T>.Failure(ClientErrorCodes.Forbidden, "Профилът ви няма достъп до това действие.");
                case HttpStatusCode.TooManyRequests:
                    return ClientResult<T>.Failure(TooManyRequests, "Твърде много заявки. Изчакайте малко и опитайте отново.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return await ReadProblemAsync<T>(response, cancellationToken);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return value is null
                ? ClientResult<T>.Failure(ClientErrorCodes.Unexpected, "Празен отговор от сървъра.")
                : ClientResult<T>.Success(value);
        }
        catch (HttpRequestException)
        {
            return NetworkError<T>();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Изтекло време за изчакване на HttpClient, не отказ от екрана.
            return NetworkError<T>();
        }
        catch (InvalidOperationException)
        {
            return ClientResult<T>.Failure(ClientErrorCodes.Network, "Задайте адреса на сървъра в настройките.");
        }
        catch (JsonException)
        {
            return ClientResult<T>.Failure(ClientErrorCodes.Unexpected, "Сървърът върна отговор, който приложението не разбира.");
        }
    }

    /// <summary>
    /// Заявката не е стигнала до сървъра или отговорът не е получен: клиентът не знае кое от двете.
    /// Затова действията носят идемпотентен ключ и повторният опит е безопасен (ADR-007).
    /// </summary>
    private static ClientResult<T> NetworkError<T>() =>
        ClientResult<T>.Failure(ClientErrorCodes.Network, "Няма връзка със сървъра.");

    private static bool IsProblem(HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType == "application/problem+json";

    /// <summary>
    /// ProblemDetails от API-то: машинният код е в "code", текстът за екрана в "title";
    /// при невалидни данни първото съобщение от "errors" е по-точно от общото заглавие.
    /// </summary>
    private static async Task<ClientResult<T>> ReadProblemAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string? code = null;
        string? message = null;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (problem.ValueKind == JsonValueKind.Object)
            {
                code = problem.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;
                message = problem.TryGetProperty("title", out var title) ? title.GetString() : null;
                if (problem.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    message = errors.EnumerateObject()
                        .SelectMany(field => field.Value.EnumerateArray())
                        .Select(error => error.GetString())
                        .FirstOrDefault(text => !string.IsNullOrEmpty(text)) ?? message;
                }
            }
        }
        catch (JsonException)
        {
        }

        return ClientResult<T>.Failure(
            code ?? (response.StatusCode == HttpStatusCode.Forbidden ? ClientErrorCodes.Forbidden : ClientErrorCodes.Unexpected),
            string.IsNullOrEmpty(message) ? $"Сървърът отговори с код {(int)response.StatusCode}." : message);
    }
}
