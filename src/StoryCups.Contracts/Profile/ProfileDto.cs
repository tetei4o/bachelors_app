namespace StoryCups.Contracts.Profile;

public sealed record ProfileDto(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles)
{
    /// <summary>
    /// Инициали за екрана на баристата (A19): без пълно име и имейл.
    /// </summary>
    public string Initials => DisplayNames.Initials(DisplayName);
}
