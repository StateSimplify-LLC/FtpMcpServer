using System;
using System.ComponentModel;
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
            // Resolve the remote path using the optional path and the defaults (falls back to defaults.DefaultPath when null).
            var remotePath = GetRemotePath(defaults, path);

            byte[] bytes = await Task.Run(
                () => ftpService.DownloadBytes(defaults, remotePath),
                cancellationToken).ConfigureAwait(false);

            string b64 = Convert.ToBase64String(bytes);
            string mime = contentTypeProvider.TryGetContentType(remotePath, out var contentType)
                ? contentType
                : "application/octet-stream";

            return new BlobResourceContents
            {
                Uri = requestContext.Params?.Uri ?? BuildUri(defaults, remotePath).ToString(),
                MimeType = mime,
                Blob = b64
            };
        }

        private static string GetRemotePath(FtpDefaults defaults, string? path)
        {
            var candidate = string.IsNullOrWhiteSpace(path) ? defaults.DefaultPath : path;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = "/";
            }

            return RemotePaths.GetFtpPath(candidate);
        }

        private static Uri BuildUri(FtpDefaults defaults, string remotePath)
        {
            if (string.IsNullOrWhiteSpace(defaults.Host))
            {
                throw new ArgumentException("Host is required");
            }

            var ftpPath = RemotePaths.GetFtpPath(remotePath);
            return new UriBuilder("ftp", defaults.Host, defaults.Port, ftpPath).Uri;
        }
    }
}
