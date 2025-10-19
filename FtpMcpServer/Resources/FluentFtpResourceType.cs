using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using FtpMcpServer.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FtpMcpServer.Resources
{
    [McpServerResourceType]
    public class FluentFtpResourceType
    {
        private static FtpDefaults BuildDefaults(
            FtpDefaults defaults,
            string? host,
            int? port,
            string? username,
            string? password,
            bool? useSsl,
            bool? passive,
            bool? ignoreCertErrors,
            int? timeoutSeconds)
        {
            return new FtpDefaults
            {
                Host = host ?? defaults.Host,
                Port = port ?? defaults.Port,
                Username = username ?? defaults.Username,
                Password = password ?? defaults.Password,
                UseSsl = useSsl ?? defaults.UseSsl,
                Passive = passive ?? defaults.Passive,
                IgnoreCertErrors = ignoreCertErrors ?? defaults.IgnoreCertErrors,
                TimeoutSeconds = timeoutSeconds ?? defaults.TimeoutSeconds,
                DefaultPath = defaults.DefaultPath
            };
        }

        [McpServerResource(
            UriTemplate = "resource://ftp/file{?host,port,path,username,password,useSsl,passive,ignoreCertErrors,timeoutSeconds}",
            Name = "ftp_file",
            MimeType = "application/octet-stream")]
        [Description("Reads a file from an FTP server and returns it as a resource.")]
        public static async Task<ResourceContents> Read(
            RequestContext<ReadResourceRequestParams> requestContext,
            FtpDefaults defaults,
            [Description("FTP host name or IP address")] string? host = null,
            [Description("Remote path to the file (URL-encoded if it includes slashes)")] string? path = null,
            [Description("Server port, defaults to 21")] int? port = null,
            [Description("Username if authentication is required")] string? username = null,
            [Description("Password for the specified username")] string? password = null,
            [Description("Use explicit FTPS (TLS). Defaults to false")] bool? useSsl = null,
            [Description("Use passive mode. Defaults to true")] bool? passive = null,
            [Description("Ignore TLS certificate errors (INSECURE). Defaults to false")] bool? ignoreCertErrors = null,
            [Description("Timeout in seconds, defaults to 30")] int? timeoutSeconds = null)
        {
            var effectiveDefaults = BuildDefaults(defaults, host, port, username, password, useSsl, passive, ignoreCertErrors, timeoutSeconds);
            var remotePath = FtpPathHelper.ResolvePath(effectiveDefaults, path);

            byte[] bytes = await Task.Run(
                () => FluentFtpService.DownloadBytes(effectiveDefaults, remotePath),
                requestContext.CancellationToken).ConfigureAwait(false);

            string b64 = Convert.ToBase64String(bytes);
            string mime = MimeHelper.GetMimeType(remotePath);

            return new BlobResourceContents
            {
                Uri = requestContext.Params?.Uri ?? FtpPathHelper.BuildUri(effectiveDefaults, remotePath).ToString(),
                MimeType = mime,
                Blob = b64
            };
        }
    }
}
