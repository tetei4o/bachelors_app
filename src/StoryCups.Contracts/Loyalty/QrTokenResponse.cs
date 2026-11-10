namespace StoryCups.Contracts.Loyalty;

/// <summary>
/// Нов QR токен на клиента (ADR-005, ADR-006). Клиентът се ръководи по относителните секунди, а не по своя часовник:
/// показва кода <see cref="LifetimeSeconds"/> от получаването и иска нов след <see cref="RefreshAfterSeconds"/>.
/// </summary>
public sealed record QrTokenResponse(string Token, DateTimeOffset ExpiresAt, int LifetimeSeconds, int RefreshAfterSeconds);
