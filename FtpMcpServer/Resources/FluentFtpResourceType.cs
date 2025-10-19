using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using FtpMcpServer;
using FluentFTP;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FtpMcpServer.Resources
{
    [McpServerResourceType]
    public class FluentFtpResourceType
    {
        private static FtpConnectionInfo BuildConnectionInfo(
            FtpDefaults defaults,
            string? host,
            string? path,
            int? port,
            string? username,
            string? password,
            bool? useSsl,
            bool? passive,
            bool? ignoreCertErrors,
            int? timeoutSeconds)
        {
            return new FtpConnectionInfo
            {
                Host = host ?? defaults.Host ?? throw new ArgumentException("Host is required"),
                Port = port ?? defaults.Port,
                Path = path ?? defaults.DefaultPath ?? "/",
                Username = username ?? defaults.Username,
                Password = password ?? defaults.Password,
                UseSsl = useSsl ?? defaults.UseSsl,
                Passive = passive ?? defaults.Passive,
                IgnoreCertErrors = ignoreCertErrors ?? defaults.IgnoreCertErrors,
                TimeoutSeconds = timeoutSeconds ?? defaults.TimeoutSeconds
            };
        }

        private static FtpClient CreateClient(FtpConnectionInfo info)
        {
            var client = new FtpClient(info.Host, info.Port, info.Username, info.Password);

            client.Config.ConnectTimeout = Math.Max(1000, info.TimeoutSeconds * 1000);
            client.Config.ReadTimeout = Math.Max(1000, info.TimeoutSeconds * 1000);
            client.Config.DataConnectionConnectTimeout = Math.Max(1000, info.TimeoutSeconds * 1000);
            client.Config.DataConnectionReadTimeout = Math.Max(1000, info.TimeoutSeconds * 1000);
            client.Config.DataConnectionType = info.Passive ? FtpDataConnectionType.AutoPassive : FtpDataConnectionType.AutoActive;
            client.Config.EncryptionMode = info.UseSsl ? FtpEncryptionMode.Explicit : FtpEncryptionMode.None;

            if (info.IgnoreCertErrors)
            {
                client.ValidateCertificate += (control, e) =>
                {
                    e.Accept = true;
                };
            }

            return client;
        }

        private static async Task<byte[]> DownloadAsync(FtpConnectionInfo info, CancellationToken cancellationToken)
        {
            using var client = CreateClient(info);
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            var data = await client.DownloadBytesAsync(info.Path, token: cancellationToken).ConfigureAwait(false);
            return data ?? Array.Empty<byte>();
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
            var info = BuildConnectionInfo(defaults, host, path, port, username, password, useSsl, passive, ignoreCertErrors, timeoutSeconds);

            byte[] bytes = await DownloadAsync(info, requestContext.CancellationToken).ConfigureAwait(false);
            string b64 = Convert.ToBase64String(bytes);
            string mime = MimeHelper.GetMimeType(info.Path);

            return new BlobResourceContents
            {
                Uri = requestContext.Params?.Uri ?? info.BuildUri().ToString(),
                MimeType = mime,
                Blob = b64
            };
        }
    }
}
