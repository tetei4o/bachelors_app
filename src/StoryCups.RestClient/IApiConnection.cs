namespace StoryCups.RestClient;

/// <summary>
/// Какво му трябва на клиента на API-то от приложението, в което работи: адресът на сървъра и access токенът.
/// В мобилното приложение идват от настройките и от сесията; в тестовете от сървъра в паметта.
/// </summary>
public interface IApiConnection
{
    /// <summary>Пълен адрес към път от API-то, например /api/v1/me.</summary>
    /// <exception cref="InvalidOperationException">Адресът на сървъра не е зададен.</exception>
    Uri Resolve(string path);

    /// <summary>Валиден access токен или null, ако потребителят не е влязъл.</summary>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Сървърът е отказал токена (401): приложението излиза локално и показва входа.</summary>
    Task OnUnauthorizedAsync();
}
