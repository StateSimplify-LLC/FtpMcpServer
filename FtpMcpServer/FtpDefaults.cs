using System;

namespace FtpMcpServer
{
    public sealed class FtpDefaults
    {
        public string? Host { get; init; }
        public int Port { get; init; } = 21;
        public string? Username { get; init; }
        public string? Password { get; init; }
        public bool UseSsl { get; init; } = false;
        public bool Passive { get; init; } = true;
        public bool IgnoreCertErrors { get; init; } = false;
        public int TimeoutSeconds { get; init; } = 30;
        public string? DefaultPath { get; init; }

        private static bool ParseBool(string? v, bool d) =>
            v is null ? d :
            (v.Equals("1", StringComparison.OrdinalIgnoreCase) ||
             v.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             v.Equals("yes", StringComparison.OrdinalIgnoreCase));

        public static FtpDefaults FromClaims(System.Security.Claims.ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true)
                throw new InvalidOperationException("No authenticated user; cannot build FtpDefaults from claims.");

            string? host = user.FindFirst("ftp.host")?.Value;
            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("Missing required claim 'ftp.host'.");

            int port = 21;
            if (int.TryParse(user.FindFirst("ftp.port")?.Value, out var p) && p > 0) port = p;

            string? username = user.FindFirst("ftp.username")?.Value;
            string? password = user.FindFirst("ftp.password")?.Value;

            bool useSsl = ParseBool(user.FindFirst("ftp.ssl")?.Value, false);
            bool passive = ParseBool(user.FindFirst("ftp.passive")?.Value, true);
            bool ignoreCertErrors = ParseBool(user.FindFirst("ftp.ignoreCertErrors")?.Value, false);

            int timeout = 30;
            if (int.TryParse(user.FindFirst("ftp.timeoutSeconds")?.Value, out var ts) && ts > 0) timeout = ts;

            string? dir = user.FindFirst("ftp.dir")?.Value;

            return new FtpDefaults
            {
                Host = host,
                Port = port,
                Username = username,
                Password = password,
                UseSsl = useSsl,
                Passive = passive,
                IgnoreCertErrors = ignoreCertErrors,
                TimeoutSeconds = timeout,
                DefaultPath = string.IsNullOrEmpty(dir) ? "/" : dir
            };
        }
    }
}