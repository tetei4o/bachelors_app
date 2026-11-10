namespace StoryCups.Contracts.Common;

/// <summary>
/// Резултат, който общите компоненти получават от услугите си (ADR-003). Един и същ вид за сайта, където услугата
/// вика Application слоя, и за приложението, където минава през HTTP. Грешката носи машинен код и текст за екрана.
/// </summary>
public sealed record ClientResult<T>(T? Value, string? ErrorCode, string? ErrorMessage)
{
    public bool IsSuccess => ErrorCode is null;

    /// <summary>Заявката не е стигнала до сървъра или отговорът не е получен.</summary>
    public bool IsNetworkError => ErrorCode == ClientErrorCodes.Network;

    public static ClientResult<T> Success(T value) => new(value, null, null);

    public static ClientResult<T> Failure(string code, string message) => new(default, code, message);
}

/// <summary>
/// Кодове за грешки, които възникват при клиента, а не в отговор на сървъра.
/// </summary>
public static class ClientErrorCodes
{
    public const string Network = "network_error";

    /// <summary>Няма влязъл потребител или сървърът е отказал токена.</summary>
    public const string Unauthorized = "unauthorized";

    /// <summary>Влезлият потребител няма нужната роля.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>Отговор, който клиентът не разпознава.</summary>
    public const string Unexpected = "unexpected_response";
}
