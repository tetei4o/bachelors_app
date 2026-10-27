namespace StoryCups.Mobile.Auth;

/// <summary>
/// Адресът на сървъра. Пази се в Preferences, за да може телефонът да сочи към компютъра за разработка.
/// </summary>
public sealed class ApiSettings(IPreferences preferences)
{
    private const string Key = "storycups.server_url";

    public string Value => preferences.Get(Key, DefaultValue);

    public Uri? BaseUri => TryNormalize(Value, out var uri) ? uri : null;

    public bool TrySet(string value, out string? error)
    {
        if (!TryNormalize(value, out var uri))
        {
            error = "Въведете пълен адрес, например http://192.168.1.10:5056/";
            return false;
        }

        preferences.Set(Key, uri.AbsoluteUri);
        error = null;
        return true;
    }

    /// <summary>
    /// Пълен адрес към път от API-то, например /api/v1/me.
    /// </summary>
    public Uri Resolve(string path) =>
        new(BaseUri ?? throw new InvalidOperationException("Адресът на сървъра не е зададен."), path.TrimStart('/'));

    private static bool TryNormalize(string value, out Uri uri)
    {
        var trimmed = value.Trim();
        var withSlash = trimmed.EndsWith('/') ? trimmed : trimmed + "/";
        return Uri.TryCreate(withSlash, UriKind.Absolute, out uri!)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    // Емулаторът на Android вижда компютъра като 10.0.2.2. В Release адресът се задава изрично.
    private static string DefaultValue =>
#if DEBUG
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5056/" : "http://localhost:5056/";
#else
        string.Empty;
#endif
}
