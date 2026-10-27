namespace StoryCups.Contracts.Profile;

public sealed record ProfileDto(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles)
{
    /// <summary>
    /// Инициали за екрана на баристата (A19): без пълно име и имейл.
    /// </summary>
    public string Initials
    {
        get
        {
            var parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => char.ToUpperInvariant(parts[0][0]).ToString(),
                _ => string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[^1][0])),
            };
        }
    }
}
