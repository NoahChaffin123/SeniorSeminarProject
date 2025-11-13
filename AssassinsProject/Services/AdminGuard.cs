using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace AssassinsProject.Services
{
    /// <summary>
    /// Session-backed admin gate using a single passcode from configuration.
    /// Not related to any 'Admin' namespaces you might already have.
    /// </summary>
    public class AdminGuard
    {
        public const string SessionKey = "IsAdmin";

        private readonly IConfiguration _cfg;

        public AdminGuard(IConfiguration cfg)
        {
            _cfg = cfg;
        }

        public bool IsAdmin(HttpContext http)
        {
            return http?.Session?.GetInt32(SessionKey) == 1;
        }

        public bool TrySignIn(HttpContext http, string? passcode)
        {
            var expected = _cfg["Admin:Passcode"] ?? string.Empty;
            if (!string.IsNullOrEmpty(passcode) &&
                string.Equals(passcode, expected, StringComparison.Ordinal))
            {
                http.Session.SetInt32(SessionKey, 1);
                return true;
            }
            return false;
        }

        public void SignOut(HttpContext http)
        {
            http.Session.Remove(SessionKey);
        }
    }
}
