namespace StoryCups.Contracts;

/// <summary>
/// Пътищата на REST API-то (ADR-001). Общи за сървъра и мобилното приложение.
/// </summary>
public static class ApiRoutes
{
    public const string Root = "/api/v1";

    public static class Auth
    {
        public const string Group = Root + "/auth";
        public const string Register = Group + "/register";
        public const string Login = Group + "/login";
        public const string Refresh = Group + "/refresh";
        public const string Logout = Group + "/logout";
    }

    public static class Profile
    {
        public const string Me = Root + "/me";
    }
}
