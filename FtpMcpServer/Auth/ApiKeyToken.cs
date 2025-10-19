using System;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FtpMcpServer.Auth
{
    /// <summary>
    /// Represents the decoded API key token payload. This is base64-encoded JSON provided via Authorization: Bearer or X-Api-Key.
    /// Required fields: server (or host), port, username, password, dir. Optional: ssl, passive, ignoreCertErrors, timeoutSeconds.
    /// </summary>
    public sealed class ApiKeyToken
    {
        [JsonPropertyName("server")]
        public string? Server { get; set; }

        [JsonPropertyName("host")]
        public string? Host { get; set; }

        [JsonPropertyName("port")]
        public int? Port { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("password")]
        public string? Password { get; set; }

        [JsonPropertyName("dir")]
        public string? Dir { get; set; }

        [JsonPropertyName("ssl")]
        public bool? UseSsl { get; set; }

        [JsonPropertyName("passive")]
        public bool? Passive { get; set; }

        [JsonPropertyName("ignoreCertErrors")]
        public bool? IgnoreCertErrors { get; set; }

        [JsonPropertyName("timeoutSeconds")]
        public int? TimeoutSeconds { get; set; }

        public static bool TryParseFromBase64(string base64, out ApiKeyToken? token)
        {
            token = null;
            if (string.IsNullOrWhiteSpace(base64)) return false;
            try
            {
                // Some clients may prefix with "Bearer ". Strip if present.
                var trimmed = base64.Trim();
                if (trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed.Substring("Bearer ".Length).Trim();
                }

                byte[] bytes = Convert.FromBase64String(trimmed);
                string json = System.Text.Encoding.UTF8.GetString(bytes);

                // Try to parse JSON object
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                token = JsonSerializer.Deserialize<ApiKeyToken>(json, options);

                return token != null;
            }
            catch
            {
                // Fallback: support simple colon-delimited format: server:port:username:password:dir
                try
                {
                    byte[] bytes = Convert.FromBase64String(base64.Trim());
                    string text = System.Text.Encoding.UTF8.GetString(bytes);
                    var parts = text.Split(':', 5, StringSplitOptions.None);
                    if (parts.Length >= 5)
                    {
                        token = new ApiKeyToken
                        {
                            Server = parts[0],
                            Host = parts[0],
                            Port = int.TryParse(parts[1], out var p) ? p : 21,
                            Username = parts[2],
                            Password = parts[3],
                            Dir = parts[4]
                        };
                        return true;
                    }
                }
                catch
                {
                    // ignored
                }
                return false;
            }
        }

        public ClaimsIdentity ToClaimsIdentity(string authenticationType)
        {
            string host = Host ?? Server ?? string.Empty;
            var identity = new ClaimsIdentity(authenticationType);
            identity.AddClaim(new Claim("ftp.host", host));
            if (Port is int port) identity.AddClaim(new Claim("ftp.port", port.ToString()));
            if (!string.IsNullOrEmpty(Username)) identity.AddClaim(new Claim("ftp.username", Username!));
            if (!string.IsNullOrEmpty(Password)) identity.AddClaim(new Claim("ftp.password", Password!));
            if (!string.IsNullOrEmpty(Dir)) identity.AddClaim(new Claim("ftp.dir", Dir!));
            if (UseSsl is bool ssl) identity.AddClaim(new Claim("ftp.ssl", ssl ? "true" : "false"));
            if (Passive is bool passive) identity.AddClaim(new Claim("ftp.passive", passive ? "true" : "false"));
            if (IgnoreCertErrors is bool ice) identity.AddClaim(new Claim("ftp.ignoreCertErrors", ice ? "true" : "false"));
            if (TimeoutSeconds is int ts) identity.AddClaim(new Claim("ftp.timeoutSeconds", ts.ToString()));
            return identity;
        }
    }
}