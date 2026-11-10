namespace StoryCups.Contracts.Profile;

public static class DisplayNames
{
    /// <summary>
    /// Инициали от името за показване. Баристата вижда само тях, без пълно име и имейл (A19).
    /// </summary>
    public static string Initials(string? displayName)
    {
        var parts = (displayName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => char.ToUpperInvariant(parts[0][0]).ToString(),
            _ => string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[^1][0])),
        };
    }
}
