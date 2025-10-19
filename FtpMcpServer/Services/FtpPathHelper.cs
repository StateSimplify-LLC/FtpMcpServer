using System;

namespace FtpMcpServer.Services
{
    internal static class FtpPathHelper
    {
        public static string ResolvePath(FtpDefaults defaults, string? path)
        {
            var resolved = string.IsNullOrWhiteSpace(path)
                ? defaults.DefaultPath
                : path;

            if (string.IsNullOrWhiteSpace(resolved))
            {
                resolved = "/";
            }

            resolved = resolved.Replace('\\', '/');

            if (!resolved.StartsWith("/", StringComparison.Ordinal))
            {
                resolved = "/" + resolved;
            }

            return resolved;
        }

        public static string GetHostOrThrow(FtpDefaults defaults)
        {
            return defaults.Host ?? throw new ArgumentException("Host is required");
        }

        public static Uri BuildUri(FtpDefaults defaults, string remotePath)
        {
            var host = GetHostOrThrow(defaults);
            var path = string.IsNullOrWhiteSpace(remotePath) ? "/" : remotePath;
            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            var segments = path.Split(new[] { '/' }, StringSplitOptions.None);
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0)
                {
                    continue;
                }

                segments[i] = Uri.EscapeDataString(segments[i]);
            }

            string escaped = string.Join("/", segments);
            string uriStr = $"ftp://{host}:{defaults.Port}{escaped}";
            return new Uri(uriStr, UriKind.Absolute);
        }

        public static string CombineDirectoryAndName(string path, string newName)
        {
            var directory = "/";
            var normalized = string.IsNullOrEmpty(path) ? "/" : path;
            var lastSlash = normalized.LastIndexOf('/');
            if (lastSlash > 0)
            {
                directory = normalized.Substring(0, lastSlash);
            }

            if (string.IsNullOrEmpty(directory))
            {
                directory = "/";
            }

            if (!directory.EndsWith("/", StringComparison.Ordinal))
            {
                directory += "/";
            }

            return directory + newName;
        }
    }
}
