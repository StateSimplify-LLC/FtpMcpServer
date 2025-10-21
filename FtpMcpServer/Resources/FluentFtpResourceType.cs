using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP.Helpers;
using FtpMcpServer.Services;
using Microsoft.AspNetCore.StaticFiles;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FtpMcpServer.Resources
{
    [McpServerResourceType]
    public class FluentFtpResourceType
    {
        [McpServerResource(
            UriTemplate = "resource://ftp/file{?path}",
            Name = "ftp_file",
            MimeType = "application/octet-stream")]
        [Description("Reads a file from an FTP server using only the provided FtpDefaults for connection settings; an optional path may be provided.")]
        public static async Task<ResourceContents> Read(
            RequestContext<ReadResourceRequestParams> requestContext,
            FtpDefaults defaults,
            IFluentFtpService ftpService,
            FileExtensionContentTypeProvider contentTypeProvider,
            [Description("Remote path to the file (URL-encoded if it includes slashes)")] string? path = null,
            CancellationToken cancellationToken = default)
        {
            // Resolve the remote path from bound param, raw URI query/fragment, or defaults.
            var remotePath = GetRemotePath(requestContext, defaults, path);

            // Download file bytes (run on thread pool if service method is synchronous)
            byte[] bytes = await Task.Run(
                () => ftpService.DownloadBytes(defaults, remotePath),
                cancellationToken).ConfigureAwait(false);

            // IMPORTANT: Zero-length files are valid. Do not throw on empty arrays.
            if (bytes is null)
            {
                throw new InvalidOperationException($"No bytes were returned when downloading '{remotePath}'.");
            }

            string b64 = Convert.ToBase64String(bytes);

            // Derive a MIME type; fall back to octet-stream
            string fileNameForMime = Path.GetFileName(remotePath);
            string mime = contentTypeProvider.TryGetContentType(fileNameForMime, out var contentType)
                ? contentType
                : "application/octet-stream";

            return new BlobResourceContents
            {
                // Prefer the exact request URI if present so the client can correlate results;
                // otherwise synthesize an ftp:// URI for clarity.
                Uri = requestContext.Params?.Uri ?? BuildUri(defaults, remotePath).ToString(),
                MimeType = mime,
                Blob = b64
            };
        }

        private static string GetRemotePath(
            RequestContext<ReadResourceRequestParams> requestContext,
            FtpDefaults defaults,
            string? boundPath)
        {
            // 1) Bound parameter from MCP binding (works if path was URL-encoded so '/' survived parsing)
            string? candidate = NormalizePath(boundPath);

            // 2) Try to extract from the request URI query string (handles unencoded '/')
            candidate ??= NormalizePath(ExtractQueryValue(requestContext?.Params?.Uri, "path"));

            // 3) If not in query, try URI fragment (supports templates that use {#path})
            candidate ??= NormalizePath(ExtractFromFragment(requestContext?.Params?.Uri, "path"));

            // 4) Last resort: use server defaults
            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = string.IsNullOrWhiteSpace(defaults?.DefaultPath) ? "/" : defaults!.DefaultPath!;
            }

            // Ensure a valid FTP path format
            return RemotePaths.GetFtpPath(candidate!);
        }

        private static string? NormalizePath(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            // Decode once or twice to handle accidental double-encoding
            string once = Uri.UnescapeDataString(raw);
            string twice;
            try
            {
                twice = Uri.UnescapeDataString(once);
            }
            catch
            {
                twice = once;
            }

            // Standardize separators
            twice = twice.Replace('\\', '/');

            return twice.Length == 0 ? null : twice;
        }

        private static string? ExtractQueryValue(string? uriString, string key)
        {
            if (string.IsNullOrWhiteSpace(uriString))
            {
                return null;
            }

            if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var query = uri.Query; // includes leading '?'
            if (string.IsNullOrEmpty(query))
            {
                return null;
            }

            // Split on '&' and ';' to be tolerant
            var pairs = query.TrimStart('?').Split(new[] { '&', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var idx = pair.IndexOf('=');
                if (idx <= 0) continue;

                var k = pair.Substring(0, idx);
                var v = pair.Substring(idx + 1);

                if (string.Equals(Uri.UnescapeDataString(k), key, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(v);
                }
            }

            return null;
        }

        private static string? ExtractFromFragment(string? uriString, string key)
        {
            if (string.IsNullOrWhiteSpace(uriString))
            {
                return null;
            }

            if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var frag = uri.Fragment; // starts with '#'
            if (string.IsNullOrEmpty(frag))
            {
                return null;
            }

            var trimmed = frag.TrimStart('#');
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            // Support either "#<path>" or "#path=<path>"
            if (trimmed.Contains("=", StringComparison.Ordinal))
            {
                var idx = trimmed.IndexOf('=');
                if (idx > 0)
                {
                    var k = trimmed.Substring(0, idx);
                    var v = trimmed.Substring(idx + 1);
                    if (string.Equals(Uri.UnescapeDataString(k), key, StringComparison.OrdinalIgnoreCase))
                    {
                        return Uri.UnescapeDataString(v);
                    }
                    return null;
                }
            }

            return Uri.UnescapeDataString(trimmed);
        }

        private static Uri BuildUri(FtpDefaults defaults, string remotePath)
        {
            if (string.IsNullOrWhiteSpace(defaults?.Host))
            {
                throw new ArgumentException("Host is required");
            }

            var ftpPath = RemotePaths.GetFtpPath(remotePath);
            return new UriBuilder("ftp", defaults.Host, defaults.Port, ftpPath).Uri;
        }
    }

}