using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
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
            FileExtensionContentTypeProvider contentTypeProvider,
            [Description("Remote path to the file (URL-encoded if it includes slashes)")] string? path = null,
            CancellationToken cancellationToken = default)
        {
            // Resolve the remote path using the optional path and the defaults (falls back to defaults.DefaultPath when null).
            var remotePath = FtpPathHelper.ResolvePath(defaults, path);

            byte[] bytes = await Task.Run(
                () => FluentFtpService.DownloadBytes(defaults, remotePath),
                cancellationToken).ConfigureAwait(false);

            string b64 = Convert.ToBase64String(bytes);
            string mime = contentTypeProvider.TryGetContentType(remotePath, out var contentType)
                ? contentType
                : "application/octet-stream";

            return new BlobResourceContents
            {
                Uri = requestContext.Params?.Uri ?? FtpPathHelper.BuildUri(defaults, remotePath).ToString(),
                MimeType = mime,
                Blob = b64
            };
        }
    }
}