namespace StoryCups.Contracts.Auth;

/// <summary>
/// Отговор 202 на регистрацията. Еднакъв за нов и за вече използван имейл.
/// </summary>
public sealed record RegistrationAcceptedResponse(string Message);
